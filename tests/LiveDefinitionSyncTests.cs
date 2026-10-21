using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using StsCardEditor;

class LiveDefinitionSyncTests
{
    static int passed;
    static void Check(bool condition,string name) { if(!condition) throw new Exception(name); passed++; Console.WriteLine("PASS "+name); }
    static CustomCardModel Card(string id) { return new CustomCardModel { id=id,name=id,type="SKILL",target="SELF",description="获得1点格挡。",upgradeDescription="获得1点格挡。",effects=new List<EffectModel>{new EffectModel{type="BLOCK",amount=1,target="SELF"}} }; }
    static void Main()
    {
        try {
            using(FakeGame game=new FakeGame("success",null,null)) {
                DefinitionSyncResult result=game.Sync(); ContentDoc saved=JsonUtil.FromJson<ContentDoc>(JsonUtil.ReadUtf8(game.Content));
                Check(result.AppliedIds.Count==2 && result.Persisted && result.Error==null,"live definition sync waits for both final registration acknowledgements");
                Check(game.DefinitionOnly && game.Applied.Count==2,"sync sends only DEFINITION scope and never adds owned card instances");
                Check(saved.customCards.Any(c=>c.id=="stseditor_sync_a") && saved.customCards.Any(c=>c.id=="stseditor_sync_b") && saved.customCards.Any(c=>c.id=="stseditor_existing") && saved.overrides["Strike_R"].damage==17,"successful sync persists new definitions and preserves existing cards and overrides");
                Check(File.Exists(result.BackupPath) && JsonUtil.ReadUtf8(result.BackupPath)==game.Before,"live content update retains a readable exact backup");
            }
            using(FakeGame game=new FakeGame("invalid","stseditor_sync_b",null)) {
                bool failed=false; try{game.Sync();}catch(InvalidDataException){failed=true;}
                Check(failed && game.Applied.Count==0 && JsonUtil.ReadUtf8(game.Content)==game.Before,"whole batch prevalidation rejects an invalid card before runtime or disk changes");
            }
            using(FakeGame game=new FakeGame("partial",null,"stseditor_sync_b")) {
                DefinitionSyncResult result=game.Sync(); ContentDoc saved=JsonUtil.FromJson<ContentDoc>(JsonUtil.ReadUtf8(game.Content));
                Check(result.AppliedIds.SequenceEqual(new[]{"stseditor_sync_a"}) && result.Error!=null && result.Persisted,"partial runtime rejection reports confirmed cards rather than claiming complete success");
                Check(saved.customCards.Any(c=>c.id=="stseditor_sync_a") && !saved.customCards.Any(c=>c.id=="stseditor_sync_b"),"partial sync persists only definitions confirmed by the game");
            }
            using(FakeGame game=new FakeGame("disk-failure",null,null)) {
                game.Content=Path.Combine(game.Folder,"blocked-destination"); Directory.CreateDirectory(game.Content);
                DefinitionSyncResult result=game.Sync();
                Check(result.AppliedIds.Count==2 && !result.Persisted && result.Error.Contains("保存失败"),"disk failure clearly distinguishes live registration from persistence");
            }
            using(FakeGame game=new FakeGame("no-run",null,null)) {
                game.InRun=false; bool failed=false; try{game.Sync();}catch(InvalidOperationException){failed=true;}
                Check(failed && game.Applied.Count==0 && JsonUtil.ReadUtf8(game.Content)==game.Before,"main menu sync explains entering a run without silently writing future-only definitions");
            }
            foreach(bool combat in new[]{false,true})using(FakeGame game=new FakeGame("revision-"+combat,null,null)) {
                game.InCombat=combat;DefinitionSyncResult result=game.Sync(true);
                Check(game.LastScopes.Contains("MASTER_ID")&&game.LastScopes.Contains("DEFINITION")&&game.LastScopes.Contains("COMBAT_ID")==combat,"revision sync targets existing master cards and combat copies only when in combat: "+combat);
                Check(result.AppliedIds.Count==2&&result.AffectedInstances==4&&result.Persisted&&result.Error==null,"revision sync waits for final acknowledgements and reports existing affected instances: "+combat);
            }
            Console.WriteLine("TOTAL "+passed+" live-definition protocol checks passed");
        } catch(Exception error) { Console.WriteLine("FAIL "+error); Environment.ExitCode=1; }
    }

    sealed class FakeGame:IDisposable
    {
        public string Folder,Content,Before;
        public bool DefinitionOnly=true,InRun=true;
        public bool InCombat;
        public List<string> LastScopes=new List<string>();
        public readonly HashSet<string> Applied=new HashSet<string>();
        readonly TcpListener listener;
        readonly Thread thread;
        readonly string invalidId,failId;
        Exception serverError;
        long revision=10;
        string pendingId;
        public FakeGame(string name,string invalid,string fail)
        {
            invalidId=invalid; failId=fail;
            Folder=Path.Combine(AppPaths.AppDir,"sync-fixtures",name+"-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(Folder,"sts-card-editor"));
            Content=Path.Combine(Folder,"sts-card-editor","content.json"); ContentDoc doc=new ContentDoc(); doc.customCards.Add(Card("stseditor_existing")); doc.overrides["Strike_R"]=new CardOverrideModel{damage=17};
            Before=JsonUtil.ToJson(doc); JsonUtil.WriteUtf8(Content,Before);
            listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
            JsonUtil.WriteUtf8(Path.Combine(Folder,"sts-card-editor","bridge.json"),JsonUtil.ToJson(new BridgeInfo { protocolVersion=1,host="127.0.0.1",port=((IPEndPoint)listener.LocalEndpoint).Port,token="definition-test" }));
            thread=new Thread(Run); thread.Start();
        }
        public DefinitionSyncResult Sync(bool includeExisting=false)
        {
            using(LiveClient client=new LiveClient()) { client.Connect(Folder); return LiveDefinitionSync.Sync(client,new List<CustomCardModel>{Card("stseditor_sync_a"),Card("stseditor_sync_b")},Content,includeExisting); }
        }
        void Run()
        {
            try {
                using(TcpClient socket=listener.AcceptTcpClient())
                using(StreamReader reader=new StreamReader(socket.GetStream(),Encoding.UTF8))
                using(StreamWriter writer=new StreamWriter(socket.GetStream(),new UTF8Encoding(false)){AutoFlush=true}) {
                    string line;
                    while((line=reader.ReadLine())!=null) {
                        Dictionary<string,object> req=(Dictionary<string,object>)JsonUtil.FromJson(line),data=new Dictionary<string,object>();
                        Dictionary<string,object> payload=(Dictionary<string,object>)req["payload"];
                        string op=(string)req["op"]; bool pending=false;
                        if(op=="status") {data["inRun"]=InRun;data["inCombat"]=InCombat;}
                        else if(op=="validate" || op=="apply") {
                            IList scopes=(IList)payload["scopes"]; DefinitionOnly &= scopes.Count==1 && (string)scopes[0]=="DEFINITION" && (string)payload["instanceId"]=="";
                            LastScopes=scopes.Cast<object>().Select(Convert.ToString).ToList();
                            string id=(string)payload["cardId"];
                            if(op=="validate") { data["valid"]=id!=invalidId; data["errors"]=id==invalidId?new[]{"unsupported test card"}:new string[0]; }
                            else if(id==failId) { data["applied"]=false; data["error"]="intentional runtime rejection"; }
                            else { pending=true; pendingId=id; }
                        } else if(op=="result") { Applied.Add(pendingId); pendingId=null; revision++; data["applied"]=true; data["definitionUpdated"]=true; data["affectedInstances"]=LastScopes.Contains("MASTER_ID")?2:0; }
                        else if(op=="cardCatalog") data["cards"]=Applied.Select(id=>new Dictionary<string,object>{{"cardId",id},{"isCustom",true}}).ToArray();
                        else throw new InvalidOperationException("Unexpected operation "+op);
                        writer.WriteLine(JsonUtil.ToJson(new Dictionary<string,object>{{"ok",true},{"pending",pending},{"revision",revision},{"requestId",req["requestId"]},{"data",data}}));
                    }
                }
            } catch(Exception error) { serverError=error; }
        }
        public void Dispose() { listener.Stop(); if(!thread.Join(5000)) throw new Exception("Fake game did not stop"); if(serverError!=null) throw serverError; }
    }
}

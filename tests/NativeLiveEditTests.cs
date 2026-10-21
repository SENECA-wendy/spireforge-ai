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
class NativeLiveEditTests
{
    static int passed;
    static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    static void Main()
    {
        try {
            Catalog catalog=ProfileStore.LoadCatalog();CatalogCard strike=catalog.cards.Find(c=>c.id=="Strike_R"),bash=catalog.cards.Find(c=>c.id=="Bash");
            var previous=new Dictionary<string,CardOverrideModel>{{strike.id,new CardOverrideModel{name="已有名称",draw=2,damage=7}}};
            var plans=NativeLiveEditService.Plan(new[]{strike,bash},new CardOverrideModel{damage=20},previous,false);
            Check(plans[0].Values.damage==20 && plans[0].Values.upgradeDamage==23 && plans[1].Values.upgradeDamage==22,"batch base damage preserves each native card's distinct upgrade increment");
            Check(plans[0].Values.name=="已有名称" && plans[0].Values.draw==2 && previous[strike.id].damage==7,"partial field edits preserve other overrides without mutating the source cache");
            var explicitUpgrade=NativeLiveEditService.Plan(new[]{strike},new CardOverrideModel{damage=20,upgradeDamage=25},previous,false);
            Check(explicitUpgrade[0].Values.upgradeDamage==25,"explicit upgraded damage is a final target value");
            var blood=catalog.cards.Find(c=>c.id=="Blood for Blood");var zero=NativeLiveEditService.Plan(new[]{blood},new CardOverrideModel{cost=0},previous,false);
            Check(zero[0].Values.upgradeCost==0,"native cost upgrade delta cannot accidentally create an X-cost card");
            bool rejected=false;try{NativeLiveEditService.Plan(new[]{strike,catalog.cards.Find(c=>c.id=="Defend_R")},new CardOverrideModel{damage=20},previous,false);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"mixed batch refuses a damage field on a card that does not use damage");
            var reset=NativeLiveEditService.Plan(new[]{strike},null,previous,true);
            Check(reset[0].Values.damage==6 && reset[0].Values.upgradeDamage==9 && reset[0].Values.cost==1 && reset[0].Values.draw==2,"numeric reset restores native base and upgraded stats while preserving unrelated settings");
            string armaments=NativeCardPreview.Build(catalog.cards.Find(c=>c.id=="Armaments"));
            Check(armaments.Contains("一张") && armaments.Contains("所有") && !armaments.Contains("!B!"),"native introduction uses distinct localized upgrade text with resolved numbers");
            string limit=NativeCardPreview.Build(catalog.cards.Find(c=>c.id=="Limit Break"));
            Check(limit.Split(new[]{"升级后"},StringSplitOptions.None)[1].IndexOf("消耗",StringComparison.Ordinal)<0,"Limit Break upgrade introduction correctly removes Exhaust");
            Verify(plans,false,false,true);Verify(plans,true,false,true);Verify(plans,false,true,true);Verify(plans,false,false,false);
            var other=NativeLiveEditService.Plan(new[]{catalog.cards.Find(c=>c.id=="Strike_G"),catalog.cards.Find(c=>c.id=="Strike_P")},new CardOverrideModel{damage=20},previous,false);Verify(other,false,false,true);Verify(other,true,false,true);
            Console.WriteLine("TOTAL "+passed+" native live-edit checks passed");
        }catch(Exception error){Console.WriteLine("FAIL "+error);Environment.ExitCode=1;}
    }
    static void Verify(List<NativeEditPlan> plans,bool combat,bool reject,bool inRun)
    {
        TcpListener listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Exception serverError=null;List<string> committed=new List<string>();bool correctScopes=true;long revision=100;
        string directory=Path.Combine(AppPaths.AppDir,"native-protocol",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(directory,"sts-card-editor"));
        JsonUtil.WriteUtf8(Path.Combine(directory,"sts-card-editor","bridge.json"),JsonUtil.ToJson(new BridgeInfo{protocolVersion=1,host="127.0.0.1",port=((IPEndPoint)listener.LocalEndpoint).Port,token="native-test"}));
        Thread server=new Thread(delegate(){try{using(TcpClient socket=listener.AcceptTcpClient())using(StreamReader reader=new StreamReader(socket.GetStream(),Encoding.UTF8))using(StreamWriter writer=new StreamWriter(socket.GetStream(),new UTF8Encoding(false)){AutoFlush=true}){
            string line,pendingId=null;while((line=reader.ReadLine())!=null){var request=(Dictionary<string,object>)JsonUtil.FromJson(line);string op=(string)request["op"];var payload=(Dictionary<string,object>)request["payload"];var data=new Dictionary<string,object>();bool pending=false;
                if(op=="status"){data["inRun"]=inRun;data["inCombat"]=combat;}
                else if(op=="validate"||op=="apply"){
                    var scopes=((IList)payload["scopes"]).Cast<string>().ToList();correctScopes &= (string)payload["kind"]=="override" && scopes.Contains("MASTER_ID") && scopes.Contains("DEFINITION") && scopes.Contains("COMBAT_ID")==combat && (string)payload["instanceId"]=="";
                    if(op=="validate"){bool valid=!(reject && (string)payload["cardId"]=="Bash");data["valid"]=valid;data["errors"]=valid?new string[0]:new[]{"intentional validation rejection"};}
                    else{pending=true;pendingId=(string)payload["cardId"];}
                }else if(op=="result"){committed.Add(pendingId);pendingId=null;revision++;data["applied"]=true;data["affectedInstances"]=combat?4:2;}
                else throw new Exception("Unexpected operation: "+op);
                writer.WriteLine(JsonUtil.ToJson(new Dictionary<string,object>{{"ok",true},{"pending",pending},{"revision",revision},{"data",data},{"requestId",request["requestId"]}}));
            }
        }}catch(Exception error){serverError=error;}});server.Start();
        bool failed=false;NativeEditResult result=null;
        try{using(LiveClient client=new LiveClient()){client.Connect(directory);try{result=NativeLiveEditService.Apply(client,plans);}catch(InvalidOperationException){failed=true;}}}
        finally{listener.Stop();if(!server.Join(5000))throw new Exception("test bridge did not stop");if(serverError!=null)throw serverError;}
        if(!inRun)Check(failed && committed.Count==0,"outside a run live editing refuses without a silent offline fallback");
        else if(reject)Check(failed && committed.Count==0,"all selected native cards are validated before the first mutation");
        else{
            Check(correctScopes,combat?"combat edit targets existing run deck, battle copies, and future definitions":"map edit targets master deck and definitions without requesting combat zones");
            Check(!failed && result.Applied.Count==2 && result.Instances==(combat?8:4) && committed.Count==2,"native batch waits for final acknowledgements and reports affected existing instances");
        }
    }
}

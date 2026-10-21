using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StsCardEditor
{
    public class DefinitionSyncResult
    {
        public readonly List<string> AppliedIds = new List<string>();
        public string Error;
        public string BackupPath;
        public bool Persisted;
        public Dictionary<string,object> CatalogData;
        public int AffectedInstances;
    }

    public static class LiveDefinitionSync
    {
        public static DefinitionSyncResult Sync(LiveClient client, List<CustomCardModel> cards, string contentPath, bool includeExisting=false)
        {
            if (cards == null || cards.Count == 0) throw new InvalidOperationException(UiText.T("没有启用的自定义卡牌可同步。"));
            if (cards.Any(c=>c==null || !c.enabled) || cards.Select(c=>c.id).Distinct().Count()!=cards.Count)
                throw new InvalidDataException(UiText.T("自定义卡列表含无效、停用或重复项。"));
            LiveResponse status=client.Status();
            if (!status.Ok || !Bool(status.Data,"inRun")) throw new InvalidOperationException(UiText.T("请先进入本局，在地图或可操作的战斗回合同步。"));
            List<string> scopes=new List<string>{"DEFINITION"};
            if(includeExisting){scopes.Add("MASTER_ID");if(Bool(status.Data,"inCombat"))scopes.Add("COMBAT_ID");}
            // Validate the entire batch before changing the first definition.
            foreach(CustomCardModel card in cards) {
                List<string> errors=ProfileStore.ValidateCustomCard(card,null,null);
                if(errors.Count>0) throw new InvalidDataException(card.name+"："+string.Join("；",errors.ToArray()));
                LiveResponse validation=client.Validate("custom",card.id,"",card,null,scopes,status.Revision+1);
                if(!validation.Ok || !Bool(validation.Data,"valid")) throw new InvalidDataException(card.name+UiText.T(" 校验失败：")+Errors(validation));
            }
            ContentDoc content;
            if(File.Exists(contentPath)) {
                content=JsonUtil.FromJson<ContentDoc>(JsonUtil.ReadUtf8(contentPath));
                if(content==null || content.schemaVersion>2) throw new InvalidDataException(UiText.T("现有游戏定义文件无效或版本过新，未覆盖。"));
            } else content=new ContentDoc();
            if(content.customCards==null) content.customCards=new List<CustomCardModel>();
            DefinitionSyncResult result=new DefinitionSyncResult();
            foreach(CustomCardModel card in cards) {
                try {
                    status=client.Status(); if(!status.Ok || !Bool(status.Data,"inRun")) throw new InvalidOperationException(UiText.T("游戏局已退出，未继续同步。"));
                    if(includeExisting){scopes=new List<string>{"DEFINITION","MASTER_ID"};if(Bool(status.Data,"inCombat"))scopes.Add("COMBAT_ID");}
                    LiveResponse applied=client.Apply("custom",card.id,"",card,null,scopes,status.Revision+1);
                    if(!applied.Ok || !Bool(applied.Data,"applied") || !Bool(applied.Data,"definitionUpdated")) throw new InvalidOperationException(Errors(applied));
                    result.AppliedIds.Add(card.id);
                    object affected;if(applied.Data.TryGetValue("affectedInstances",out affected))result.AffectedInstances+=Convert.ToInt32(affected);
                    content.customCards.RemoveAll(c=>c!=null && c.id==card.id); content.customCards.Add(card);
                } catch(Exception error) { result.Error=card.name+"："+error.Message; break; }
            }
            if(result.AppliedIds.Count>0) {
                try {
                    string pending=contentPath+".pending-"+Guid.NewGuid().ToString("N");
                    JsonUtil.WriteUtf8(pending,JsonUtil.ToJson(content));
                    if(File.Exists(contentPath)) {
                        result.BackupPath=Path.Combine(Path.GetDirectoryName(contentPath),"content-before-live-sync-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N")+".json");
                        File.Replace(pending,contentPath,result.BackupPath);
                    } else File.Move(pending,contentPath);
                    result.Persisted=true;
                } catch(Exception error) { result.Error=(result.Error==null?"":result.Error+"；")+UiText.T("运行时已注册，但定义保存失败：")+error.Message; }
                try {
                    LiveResponse catalog=client.CardCatalog();
                    if(!catalog.Ok) throw new InvalidOperationException(catalog.Error);
                    result.CatalogData=catalog.Data;
                    HashSet<string> registered=new HashSet<string>(); object raw;
                    if(catalog.Data.TryGetValue("cards",out raw) && raw is IList) foreach(object item in (IList)raw) {
                        Dictionary<string,object> row=item as Dictionary<string,object>;
                        if(row!=null && row.ContainsKey("cardId")) registered.Add(Convert.ToString(row["cardId"]));
                    }
                    if(result.AppliedIds.Any(id=>!registered.Contains(id))) throw new InvalidOperationException(UiText.T("注册后的游戏卡库确认不完整，请重新读取牌池。"));
                } catch(Exception error) { result.Error=(result.Error==null?"":result.Error+"；")+error.Message; }
            }
            return result;
        }

        private static bool Bool(Dictionary<string,object> data,string key) { return data!=null && data.ContainsKey(key) && Convert.ToBoolean(data[key]); }
        private static string Errors(LiveResponse response)
        {
            if(!string.IsNullOrEmpty(response.Error)) return response.Error;
            object value; List<string> errors=new List<string>();
            if(response.Data.TryGetValue("errors",out value) && value is IList) foreach(object item in (IList)value) errors.Add(Convert.ToString(item));
            if(response.Data.TryGetValue("error",out value) && value!=null) errors.Add(Convert.ToString(value));
            return errors.Count>0 ? string.Join("；",errors.ToArray()) : UiText.T("游戏未确认同步成功。");
        }
    }
}

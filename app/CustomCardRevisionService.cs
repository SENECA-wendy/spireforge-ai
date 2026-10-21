using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StsCardEditor
{
    public class CardNumberField
    {
        public string Path,Label,Text,Help; public int Effect=-1; public string Property,Parameter;
        public double Min=0,Max=999; public bool Nullable=true,Integer=true;
    }
    public class CardRevisionSave
    {
        public Profile Profile;public CustomCardModel Card;public string HistoryFolder;
    }
    public static class CustomCardRevisionService
    {
        static string HistoryKey(string id){using(SHA256 hash=SHA256.Create())return Regex.Replace(id,@"[^A-Za-z0-9_.-]","_").Substring(0,Math.Min(id.Length,48))+"-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-","").Substring(0,12);}
        public static CustomCardModel Clone(CustomCardModel card){return JsonUtil.FromJson<CustomCardModel>(JsonUtil.ToJson(card));}
        static string Text(object value){return value==null?"":Convert.ToString(value,CultureInfo.InvariantCulture);}
        static bool Numeric(object value){return value is byte || value is short || value is int || value is long || value is float || value is double || value is decimal;}
        static int Primary(CustomCardModel card,string stat)
        {
            string[] types=stat=="damage"?new[]{"DAMAGE"}:stat=="block"?new[]{"BLOCK"}:new[]{"APPLY_POWER","START_TURN_BLOCK","DRAW_ON_PLAY"};
            foreach(string type in types){int index=card.effects.FindIndex(e=>e.type==type);if(index>=0)return index;}return -1;
        }
        public static List<CardNumberField> Numbers(CustomCardModel card)
        {
            List<CardNumberField> rows=new List<CardNumberField>();
            rows.Add(new CardNumberField{Path="cost",Property="cost",Label="基础费用",Text=Text(card.cost),Min=-2,Max=99,Nullable=false,Help="-1为X费，-2为不可打出"});
            rows.Add(new CardNumberField{Path="upgradeCost",Property="upgradeCost",Label="升级后费用",Text=Text(card.upgradeCost),Min=-2,Max=99,Help="留空沿用基础费用"});
            foreach(string stat in new[]{"damage","block","magicNumber"}) {
                int index=Primary(card,stat);string up="upgrade"+char.ToUpperInvariant(stat[0])+stat.Substring(1);object value=typeof(CustomCardModel).GetProperty(stat).GetValue(card,null),higher=typeof(CustomCardModel).GetProperty(up).GetValue(card,null);
                if(index<0 && value==null && higher==null)continue;
                string label=stat=="damage"?"伤害":stat=="block"?"格挡":"附加数值";
                rows.Add(new CardNumberField{Path=stat,Property=stat,Label="基础"+label,Effect=index,Text=Text(value??(index<0?null:(object)card.effects[index].amount)),Help=index<0?"仅当Java代码读取此卡牌字段时生效":"与首个对应内置效果同步"});
                rows.Add(new CardNumberField{Path=up,Property=up,Label="升级后"+label,Effect=index,Text=Text(higher??(index<0?null:(object)card.effects[index].upgradeAmount)),Help="留空沿用基础值"});
            }
            for(int i=0;i<card.effects.Count;i++) {
                EffectModel effect=card.effects[i]; bool primary=new[]{"damage","block","magicNumber"}.Any(s=>Primary(card,s)==i);
                bool named=effect.type=="JAVA_EXTENSION" && effect.parameters!=null && effect.parameters.Values.Any(Numeric);
                foreach(string property in new[]{"amount","upgradeAmount","hits","upgradeHits"}) {
                    if(primary && (property=="amount"||property=="upgradeAmount"))continue;
                    if(named && (property=="amount"||property=="upgradeAmount"))continue;
                    object value=typeof(EffectModel).GetProperty(property).GetValue(effect,null);if((property=="hits"||property=="upgradeHits") && effect.type!="DAMAGE" && value==null)continue;
                    string label=property=="amount"?"基础数值":property=="upgradeAmount"?"升级数值":property=="hits"?"基础段数":"升级段数";
                    rows.Add(new CardNumberField{Path="effect."+i+"."+property,Property=property,Effect=i,Label=(i+1)+". "+effect.type+" · "+label,Text=Text(value),Min=property.Contains("hits")||property.Contains("Hits")?1:0,Max=property.Contains("hits")||property.Contains("Hits")?10:999,Help=effect.type=="JAVA_EXTENSION"?"旧Java扩展参数，具体含义取决于源码":"实际效果参数"});
                }
                if(effect.parameters!=null)foreach(var pair in effect.parameters)if(Numeric(pair.Value))rows.Add(new CardNumberField{Path="parameter."+i+"."+pair.Key,Effect=i,Parameter=pair.Key,Label="参数 · "+pair.Key,Text=Text(pair.Value),Min=-1000000000,Max=1000000000,Nullable=false,Integer=false,Help="Java代码按此名称读取；改值后检查描述是否仍一致"});
            }
            return rows;
        }
        public static CustomCardModel ApplyNumbers(CustomCardModel source,IEnumerable<CardNumberField> fields)
        {
            CustomCardModel card=Clone(source);
            foreach(CardNumberField field in fields) {
                object value=null;
                if(!string.IsNullOrWhiteSpace(field.Text)) {
                    double n;if(!double.TryParse(field.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out n)||double.IsNaN(n)||double.IsInfinity(n)||n<field.Min||n>field.Max||(field.Integer&&Math.Truncate(n)!=n))throw new InvalidDataException(field.Label+"需为 "+field.Min+" 至 "+field.Max+" 的"+(field.Integer?"整数":"数值")+"。");
                    value=field.Integer?(object)(int)n:n;
                }else if(!field.Nullable)throw new InvalidDataException(field.Label+"不能为空。");
                if(field.Parameter!=null){card.effects[field.Effect].parameters[field.Parameter]=value;continue;}
                if(field.Path.StartsWith("effect.")){typeof(EffectModel).GetProperty(field.Property).SetValue(card.effects[field.Effect],value,null);continue;}
                typeof(CustomCardModel).GetProperty(field.Property).SetValue(card,value,null);
                if(field.Effect>=0)typeof(EffectModel).GetProperty(field.Property.StartsWith("upgrade")?"upgradeAmount":"amount").SetValue(card.effects[field.Effect],value,null);
            }
            return card;
        }
        public static void Validate(CustomCardModel card)
        {
            List<string> errors=ProfileStore.ValidateCustomCard(card,null,null);
            if(card.upgradeCost.HasValue && (card.upgradeCost< -2||card.upgradeCost>99))errors.Add("升级费用需为-2至99。");
            foreach(string property in new[]{"damage","upgradeDamage","block","upgradeBlock","magicNumber","upgradeMagicNumber"}){int? value=(int?)typeof(CustomCardModel).GetProperty(property).GetValue(card,null);if(value<0||value>999)errors.Add(property+"需为0至999。");}
            if(card.effects!=null)foreach(EffectModel e in card.effects.Where(e=>e!=null && e.upgradeAmount.HasValue)) {
                CustomCardModel upgraded=Clone(card);upgraded.effects=new List<EffectModel>{JsonUtil.FromJson<EffectModel>(JsonUtil.ToJson(e))};upgraded.effects[0].amount=e.upgradeAmount;errors.AddRange(ProfileStore.ValidateCustomCard(upgraded,null,null));
            }
            if(errors.Count>0)throw new InvalidDataException(string.Join("\n",errors.Distinct().ToArray()));
        }
        public static string Context(CustomCardModel card,string dataDirectory)
        {
            Dictionary<string,object> context=new Dictionary<string,object>{{"card",card}};Dictionary<string,object> extensions=new Dictionary<string,object>();
            foreach(EffectModel effect in card.effects.Where(e=>e.type=="JAVA_EXTENSION")) {
                Match match=Regex.Match(effect.extensionId??"",@"^ai\.([A-Za-z0-9]+)$");if(!match.Success)continue;
                string folder=Path.Combine(dataDirectory,"generated",match.Groups[1].Value),source=Path.Combine(folder,"src");
                Dictionary<string,string> files=new Dictionary<string,string>();int length=0;
                if(Directory.Exists(source))foreach(string path in Directory.GetFiles(source,"*.java").Take(8)){string code=JsonUtil.ReadUtf8(path);length+=code.Length;if(length>100000)break;files[Path.GetFileName(path)]=code;}
                extensions[effect.extensionId]=new{sources=files,sourceAvailable=files.Count>0};
            }
            context["extensions"]=extensions;return JsonUtil.ToJson(context);
        }
        public static CreativeBuildResult Revise(ApiSettings settings,CustomCardModel card,string changes,string gameDir,double creativity)
        {
            if(string.IsNullOrWhiteSpace(changes))throw new InvalidOperationException("请填写希望AI修改的地方。");
            return CreativeCardService.GenerateCore((system,input,temp)=>ApiClient.GenerateRaw(settings,system,input,temp),changes,"",gameDir,creativity,null,Clone(card),Context(card,AppPaths.DataDir));
        }
        public static CardRevisionSave Save(string profilePath,string historyRoot,CustomCardModel original,CustomCardModel edited,string report)
        {
            if(original.id!=edited.id)throw new InvalidDataException("再修改必须保留原卡ID。");Validate(edited);
            Profile current=JsonUtil.FromJson<Profile>(JsonUtil.ReadUtf8(profilePath));
            if(current==null||current.schemaVersion>2||current.customCards==null)throw new InvalidDataException("当前卡库格式无法安全覆盖。");
            int index=current.customCards.FindIndex(c=>c.id==original.id);
            if(index<0||JsonUtil.ToJson(current.customCards[index])!=JsonUtil.ToJson(original))throw new InvalidOperationException("这张卡已被其他窗口修改或删除。请关闭编辑窗口并重开编辑器后再编辑，避免覆盖新版本。");
            string folder=Path.Combine(historyRoot,HistoryKey(original.id),DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(folder);
            File.Copy(profilePath,Path.Combine(folder,"profile-before.json"));JsonUtil.WriteUtf8(Path.Combine(folder,"before.json"),JsonUtil.ToJson(original));JsonUtil.WriteUtf8(Path.Combine(folder,"after.json"),JsonUtil.ToJson(edited));JsonUtil.WriteUtf8(Path.Combine(folder,"validation.txt"),report??"手动修改，仅字段校验；仍需实战验证。");
            current.customCards[index]=Clone(edited);string pending=profilePath+".pending-"+Guid.NewGuid().ToString("N");
            JsonUtil.WriteUtf8(pending,JsonUtil.ToJson(current));try{File.Replace(pending,profilePath,null);}finally{if(File.Exists(pending))File.Delete(pending);}
            return new CardRevisionSave{Profile=current,Card=Clone(edited),HistoryFolder=folder};
        }
        public static CustomCardModel Previous(string historyRoot,string id)
        {
            string folder=Path.Combine(historyRoot,HistoryKey(id));if(!Directory.Exists(folder))throw new InvalidOperationException("还没有这张卡的保存历史。");
            foreach(string revision in Directory.GetDirectories(folder).OrderByDescending(p=>p,StringComparer.Ordinal)) {
                string path=Path.Combine(revision,"before.json");if(!File.Exists(path))continue;
                CustomCardModel card=JsonUtil.FromJson<CustomCardModel>(JsonUtil.ReadUtf8(path));if(card!=null&&card.id==id)return card;
            }
            throw new InvalidOperationException("没有可读取的历史版本。");
        }
    }
}

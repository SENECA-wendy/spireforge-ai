using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace StsCardEditor
{
    public class CreativeContract
    {
        public string summary { get; set; }
        public List<string> requirements { get; set; }
        public Dictionary<string, object> parameters { get; set; }
        public bool allowMasterDeckChange { get; set; }
        public List<CreativeProbe> probes { get; set; }
        public List<string> unverified { get; set; }
    }
    public class CreativeProbe
    {
        public string metric { get; set; }
        public string cardName { get; set; }
        public string upgradedCardName { get; set; }
        public int? baseExpected { get; set; }
        public int? upgradedExpected { get; set; }
    }
    public class CreativeAudit
    {
        public List<string> issues { get; set; }
        public List<string> unverified { get; set; }
    }
    public static class CreativeValidation
    {
        public const string ContractSchema = "{summary:'具体规则',requirements:['逐条可核对的要求'],parameters:{shots:6,upgradedShots:6,isInnate:true,cardColor:'RED'},allowMasterDeckChange:false,probes:[{metric:'cardsToHand',cardName:'开枪',upgradedCardName:'开枪+',baseExpected:6,upgradedExpected:6}],unverified:['无法在简化环境验证的事件、概率或交互']}";
        public const string ProbeMetrics = "cardsToHand,cardsToDrawPile,cardsToDiscard,energyGained,blockGained,cardsDrawn,hpLost,powerApplications,masterDeckSize,handSize,drawPileSize,discardPileSize";

        public static void ValidateContract(CreativeContract contract, string request, bool establishedMasterDeckChange=false)
        {
            if(contract==null || string.IsNullOrWhiteSpace(contract.summary) || contract.requirements==null || contract.requirements.Count==0)
                throw new InvalidDataException("缺少明确的卡牌规则清单。");
            if(contract.parameters==null) contract.parameters=new Dictionary<string,object>();
            if(contract.parameters.Count>64) throw new InvalidDataException("命名参数过多。");
            foreach(var pair in contract.parameters) {
                if(!Regex.IsMatch(pair.Key,"^[A-Za-z][A-Za-z0-9_]{0,63}$"))
                    throw new InvalidDataException("无效的命名参数："+pair.Key);
                object value=pair.Value;
                if(value is bool)continue;
                if(value is string) {
                    if(((string)value).Length>4096)throw new InvalidDataException("命名参数 "+pair.Key+" 的文字超过4096字符。");
                    continue;
                }
                if(value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal) {
                    double number=Convert.ToDouble(value,CultureInfo.InvariantCulture);
                    if(double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(number)>1000000000)
                        throw new InvalidDataException("命名参数 "+pair.Key+" 的数值必须为有限数字，且绝对值不超过1000000000。");
                    continue;
                }
                throw new InvalidDataException("命名参数 "+pair.Key+" 只支持数字、布尔开关或文字，不支持空值、数组和对象。");
            }
            if(contract.allowMasterDeckChange && !establishedMasterDeckChange && !Regex.IsMatch(request??"",@"永久|主牌组|本局[^。\r\n]{0,16}(?:牌组|牌库)"))
                throw new InvalidDataException("需求未明确要求永久/主牌组修改，应采用战斗内范围；不能擅自删除本局主牌组。");
            if(contract.probes==null) contract.probes=new List<CreativeProbe>();
            if(contract.unverified==null) contract.unverified=new List<string>();
            if(contract.probes.Count>24) throw new InvalidDataException("行为检查数量过多。");
            foreach(CreativeProbe probe in contract.probes) {
                if(probe==null || !ProbeMetrics.Split(',').Contains(probe.metric) || !probe.baseExpected.HasValue || !probe.upgradedExpected.HasValue)
                    throw new InvalidDataException("行为检查必须使用支持的指标并明确基础和升级预期。");
                if(probe.baseExpected<0 || probe.baseExpected>1000000 || probe.upgradedExpected<0 || probe.upgradedExpected>1000000)
                    throw new InvalidDataException("行为检查预期超出范围。");
            }
            if(contract.probes.Count==0 && contract.unverified.Count==0) throw new InvalidDataException("必须提供行为检查，或明确哪些机制需要实战验证。");
            // Independently extract explicit temporary-card counts, rather than trusting a model's own tests.
            foreach(Match count in Regex.Matches(request??"",@"(?<n>\d+|[一二三四五六七八九十两])\s*张\s*[【「](?<name>[^】」]+)[】」][^。\r\n]{0,12}(?:加入|放入|放进)[^。\r\n]{0,8}手牌|(?:加入|放入|放进)[^。\r\n]{0,8}手牌[^。\r\n]{0,8}(?<n>\d+|[一二三四五六七八九十两])\s*张\s*[【「](?<name>[^】」]+)[】」]")) {
                int expected=Number(count.Groups["n"].Value); string name=count.Groups["name"].Value;
                if(!contract.probes.Any(p=>p.metric=="cardsToHand" && p.cardName==name && p.baseExpected==expected))
                    throw new InvalidDataException("需求明确要求 "+expected+" 张【"+name+"】加入手牌，行为检查必须覆盖该数量。");
            }
        }
        static int Number(string value) { int n;if(int.TryParse(value,out n))return n;return value=="两"?2:"零一二三四五六七八九十".IndexOf(value,StringComparison.Ordinal); }
        public static bool HasMasterDeckMutation(string code){return Regex.IsMatch(code??"",@"\bmasterDeck\s*(?:\.\s*group\s*)?\.\s*(?:clear|remove\w*|add\w*|set|sort|shuffle)\s*\(|\bmasterDeck\s*(?:\.\s*group\s*)?=(?!=)");}

        public static void ValidateDesign(CreativeDesign design)
        {
            CreativeContract contract=design.contract;
            if(contract==null)return; // Existing offline compiler API remains compatible; new AI generation always supplies a contract.
            string code=string.Join("\n",design.sources.Values.ToArray());
            if(Regex.IsMatch(code,@"\bctx\s*\.\s*amount\b")) throw new InvalidDataException("新设计禁止把 ctx.amount 当作机制参数。请使用规则清单中的 ctx.parameters 命名参数。");
            if(!contract.allowMasterDeckChange && HasMasterDeckMutation(code))
                throw new InvalidDataException("规则只允许战斗内操作，代码试图修改永久主牌组 masterDeck；允许只读查询，不允许擅自增删。");
            foreach(var pair in contract.parameters)
                if(!code.Contains("\""+pair.Key+"\"")) throw new InvalidDataException("规则参数未在代码中绑定："+pair.Key);
            if(HasExhaustClause(design.card.description) && !design.card.exhaust)
                throw new InvalidDataException("基础描述写了“消耗”，但 exhaust=false；请启用真实消耗属性。");
            if(HasExhaustClause(design.card.upgradeDescription) && !design.card.exhaust && !Regex.IsMatch(code,@"\.exhaust\s*=\s*true"))
                throw new InvalidDataException("升级描述写了“消耗”，但没有对应的消耗实现。");
        }
        static bool HasExhaustClause(string description) { return Regex.IsMatch(description??"",@"(?:^|[。；;\s])消耗(?:[。；;\s]|$)"); }
        public static Dictionary<string,object> Parameters(CreativeContract contract)
        {
            Dictionary<string,object> result=new Dictionary<string,object>();
            if(contract!=null) foreach(var pair in contract.parameters)result[pair.Key]=pair.Value;
            return result;
        }
        public static string Report(CreativeContract contract,string behavior,CreativeAudit audit)
        {
            string report="规则清单：\r\n"+string.Join("\r\n",contract.requirements.Select(r=>"• "+r).ToArray());
            report+="\r\n\r\n规则与属性检查：通过\r\nJava 编译：通过\r\n"+behavior+"\r\n模型独立一致性审核：通过（不等于实战证明）";
            List<string> pending=contract.unverified.Concat(audit.unverified??new List<string>()).Distinct().ToList();
            report+="\r\n\r\n仍需实战验证：\r\n"+(pending.Count==0?"完整游戏环境、遗物/能力交互与动画结算。":string.Join("\r\n",pending.Select(p=>"• "+p).ToArray()));
            return report;
        }
    }
}

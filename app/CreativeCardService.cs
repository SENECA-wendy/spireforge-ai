using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StsCardEditor
{
    public class CreativeDesign
    {
        public CustomCardModel card { get; set; }
        public string designSummary { get; set; }
        public Dictionary<string, string> sources { get; set; }
        public CreativeContract contract { get; set; }
    }

    public class CreativeBuildResult
    {
        public CustomCardModel Card;
        public string ExtensionId;
        public string JarPath;
        public string ManifestPath;
        public string SourceDirectory;
        public string DesignSummary;
        public string ValidationReport;
    }

    public static class CreativeCardService
    {
        public static CreativeBuildResult Generate(ApiSettings settings, string prompt, string name, string gameDir, double creativity, string color="RED")
        {
            return GenerateCore((system,input,temperature)=>ApiClient.GenerateRaw(settings,system,input,temperature),prompt,name,gameDir,creativity,null,null,null,color);
        }

        public static CreativeBuildResult GenerateCore(Func<string,string,double,string> ask, string prompt, string name, string gameDir, double creativity, string sdkOverride, CustomCardModel revisionBase=null, string revisionContext=null, string requestedColor="RED")
        {
            string jdk = FindJdk();
            if (!File.Exists(sdkOverride ?? Path.Combine(AppPaths.AppDir, "sdk", "card-extension-sdk.jar"))) throw new FileNotFoundException("缺少高级扩展SDK，请使用完整发行目录。");
            if (string.IsNullOrEmpty(gameDir) || !GameLocator.IsGameDir(gameDir)) throw new InvalidOperationException("请先设置游戏目录。");
            string key = Guid.NewGuid().ToString("N");
            string package = "user.generated.g" + key;
            string archive = Path.Combine(AppPaths.DataDir, "generated", key); Directory.CreateDirectory(archive);
            string input = prompt + (string.IsNullOrWhiteSpace(name) ? "" : "\n指定卡名：" + name);
            string color=revisionBase==null?requestedColor:revisionBase.color;if(!Professions.CustomColors.Contains(color))throw new InvalidDataException("无效的卡牌职业归属。");input+="\n卡牌职业归属："+Professions.Name(color)+"（"+color+"）。生成子卡时也采用此颜色，除非用户另有明确要求。";
            if(revisionBase!=null) input+="\n这是对已有卡牌的再修改。用户本次修改要求优先；原卡其他机制保持不变。卡牌ID必须保留为 "+revisionBase.id+"，代码使用ctx.card.cardID而不是写死新卡ID。可调整数值使用命名参数，包括生成的子卡数值。原卡定义及可用源码：\n"+(revisionContext??JsonUtil.ToJson(revisionBase));
            JsonUtil.WriteUtf8(Path.Combine(archive,"request.txt"),input);
            string contractSystem="规则整理：你是杀戮尖塔卡牌设计师。发挥创意补全用户未指定的细节，但用户明确的数量、概率、目标、范围、持续时间、升级效果必须原样保留。"
                +"只输出JSON："+CreativeValidation.ContractSchema+"。parameters仅列出Java机制需要动态读取的参数，示例键无需照抄；固有/消耗/费用/颜色等固定卡牌属性写入requirements并由实现阶段card属性承载，无需重复作为参数。所有参数使用有含义的英文名称；升级数值单独命名。数字保持JSON数字，开关使用true/false，颜色/模式等使用JSON字符串；不允许null、数组或对象。"
                +"牌库含义不明确时默认只清理本场战斗牌堆，allowMasterDeckChange=false；只有用户明确要求永久删除或修改主牌组才可设true。"
                +"probes是无遗物、无额外能力的简化环境，分别调用一次基础/升级onUse并执行支持的即时动作后检查；指标只允许 "+CreativeValidation.ProbeMetrics+"。"
                +"初始hand/drawPile/discardPile/masterDeck各有2张测试牌，exhaustPile/limbo为空，正在打出的卡不在牌区。cardsToHand/DrawPile/Discard只统计生成后仍在相应牌区的牌，可用cardName和upgradedCardName分别指定确切名字。"
                +"所有预期必须是整数。概率、延迟触发、复杂交互列入unverified，不能把没有触发的未来效果写成已经发生。无法提供可靠即时检查时probes=[]并明确unverified。至少覆盖用户明确要求的即时生成张数。";
            CreativeContract contract=null;string ruleError="";
            for(int attempt=0;attempt<3;attempt++) {
                string raw=ask(contractSystem,input+ruleError,creativity);JsonUtil.WriteUtf8(Path.Combine(archive,"rules-response-"+attempt+".json"),raw);
                try { contract=JsonUtil.FromJson<CreativeContract>(raw);CreativeValidation.ValidateContract(contract,prompt,revisionBase!=null&&CreativeValidation.HasMasterDeckMutation(revisionContext));break; }
                catch(Exception error) { if(attempt==2)throw new InvalidDataException("规则清单未通过校验："+error.Message,error);ruleError="\n上次规则错误，请修正并保留原需求："+error.Message; }
            }
            JsonUtil.WriteUtf8(Path.Combine(archive,"contract.json"),JsonUtil.ToJson(contract));
            input+="\n已确定的规则（不得自行修改）：\n"+JsonUtil.ToJson(contract);
            string system = "实现：你是资深Java8游戏开发者。严格实现给定规则清单；创意已经在规则阶段确定。"
                + "可以实现规则中确定的事件触发、资源转换、跨回合记忆与动态计算，不得擅自添加或替换用户机制。"
                + "必须实现用户要求，不用无关状态占位，不静默删效果。不能实现则输出{\"unsupported\":true,\"reason\":\"具体原因\"}。"
                + "只输出JSON：{card:{name,description,upgradeDescription,type,rarity,target,cost,upgradeCost,image,exhaust,innate,ethereal,retain},designSummary:'实现和边界',sources:{'CardLogic.java':'完整源码','可选辅助类.java':'源码'}}。消耗/固有/虚无/保留必须设置真实布尔属性，不能只写在描述。"
                + "type仅ATTACK/SKILL/POWER，rarity仅COMMON/UNCOMMON/RARE，target仅SELF/ENEMY/ALL_ENEMY/NONE。image用red/attack/bash或red/skill/shrug_it_off或red/power/metallicize。"
                + "所有源码必须声明 package " + package + "; 主类必须 public final class CardLogic implements com.stseditor.runtime.CardExtension。"
                + "实现 public void onUse(com.stseditor.runtime.CardExtensionContext ctx)。ctx.card是CustomCard(AbstractCard子类)，ctx.player是AbstractPlayer，ctx.target是AbstractMonster可null。禁止使用含糊的ctx.amount。"
                + "ctx.parameters是Gson JsonObject，包含规则中所有命名参数，保持原JSON类型。数字用getAsInt()/getAsDouble()，布尔用getAsBoolean()，文字用getAsString()；例如ctx.parameters.get(ctx.card.upgraded ? \"upgradedShots\" : \"shots\").getAsInt()。所有声明参数必须实际使用，代码和描述不能另写不同的常数。"
                + "onUse在游戏线程调用。可直接使用游戏API，自己编写AbstractPower子类、动作、事件回调，不局限于内置效果白名单。不得使用外部依赖。"
                + "优先把动作加入 AbstractDungeon.actionManager.addToBottom(...)。当前可用能量读取EnergyPanel.totalCount，ctx.player.energy.energy为本场每回合回复量；牌区hand/drawPile/discardPile/exhaustPile/masterDeck的group为ArrayList<AbstractCard>。"
                + "AbstractPlayer的正确导入是com.megacrit.cardcrawl.characters.AbstractPlayer，不能写core.AbstractPlayer。EnergyManager在core包，EnergyPanel在ui.panels包。EnergyManager没有maxEnergy字段；energy是本场每回合回复量，energyMaster是基础量，EnergyPanel.totalCount才是当前可用能量。本场效果不得改energyMaster，临时修改energy时要在胜利/移除时恢复并避免重复恢复。增加当前能量使用GainEnergyAction或player.gainEnergy，不能只改energy字段冒充当前可用能量。"
                + "常用动作：new GainEnergyAction(int),new DrawCardAction(ctx.player,int),new GainBlockAction(ctx.player,ctx.player,int),new LoseHPAction(ctx.player,ctx.player,int),"
                + "new ApplyPowerAction(target,ctx.player,power,int),new DamageAction(ctx.target,new DamageInfo(ctx.player,int,DamageInfo.DamageType.NORMAL),AbstractGameAction.AttackEffect.NONE)。"
                + "自定义AbstractPower有字段ID,name,owner,amount,description,type,isTurnBased；构造后loadRegion('armor')；可覆写atStartOfTurn(),atEndOfTurn(boolean),onCardDraw(AbstractCard),onAfterCardPlayed(AbstractCard),onVictory(),onRemove(),updateDescription(),stackPower(int)。字符串用Java双引号。"
                + "状态每场战斗独立，别把对局状态放static字段。牌区遍历用副本避免并发修改。别修改正在结算的动作。所有0费可在onUse中排入new ApplyPowerAction(ctx.player,ctx.player,new com.stseditor.runtime.ZeroCostPower(ctx.player),1)。"
                + "高级扩展代码仅限游戏机制，不得访问文件、网络、系统设置、反射、动态类加载或启动进程；不得System.exit。构造和static初始化不要改变游戏状态。"
                + "最多8个Java文件，总源码不超过100000字符。描述必须如实说明持续时间、触发上限和升级行为。编译通过不等于已实战，不要声称已测试。";
            system += "\n下列参考来自本机实际游戏，不要猜测包名或方法签名：\n" + BuildApiReference(gameDir, jdk);
            string json = ask(system,input,0.2);
            for (int attempt = 0; attempt < 3; attempt++) {
                JsonUtil.WriteUtf8(Path.Combine(archive, "model-response-" + attempt + ".json"), json);
                try {
                    Dictionary<string, object> root = JsonUtil.FromJson(json) as Dictionary<string, object>;
                    if (root != null && root.ContainsKey("unsupported") && Convert.ToBoolean(root["unsupported"]))
                        throw new NotSupportedException("设计需要额外引擎支持：" + (root.ContainsKey("reason") ? Convert.ToString(root["reason"]) : "未提供原因"));
                    CreativeDesign design = JsonUtil.FromJson<CreativeDesign>(json);
                    if(design!=null)design.contract=contract;
                    CreativeBuildResult result=Compile(design, gameDir, archive, key, package, sdkOverride,revisionBase,color);
                    string behavior=CreativeBehavior.Check(result,contract,gameDir,jdk);
                    string auditRaw=ask("审核：独立检查用户原始需求、固定规则、卡牌描述/属性/Java代码是否一致。不要设计新机制，不因个人平衡偏好报错。重点检查数量、概率、目标、升级、持续范围、永久主牌组变化及消耗。只输出JSON {issues:['明确缺陷及修复建议，没有则空数组'],unverified:['尚需实战的机制']}。不要声称执行过代码。",
                        "用户需求：\n"+prompt+(revisionBase==null?"":"\n原卡（本次要求之外的机制应保留）：\n"+(revisionContext??JsonUtil.ToJson(revisionBase)))+"\n规则和实现：\n"+JsonUtil.ToJson(design)+"\n本地行为检查：\n"+behavior,0.1);
                    JsonUtil.WriteUtf8(Path.Combine(archive,"audit-"+attempt+".json"),auditRaw);
                    CreativeAudit audit=JsonUtil.FromJson<CreativeAudit>(auditRaw);
                    if(audit==null || audit.issues==null)throw new InvalidDataException("审核输出缺少issues清单。");
                    if(audit.issues.Count>0)throw new InvalidDataException("一致性审核未通过：\n"+string.Join("\n",audit.issues.ToArray()));
                    result.ValidationReport=CreativeValidation.Report(contract,behavior,audit);
                    result.Card.notes="AI自由设计；规则/编译/一致性检查已完成，仍需实战。"+result.DesignSummary;
                    JsonUtil.WriteUtf8(Path.Combine(archive,"design.json"),JsonUtil.ToJson(design));
                    JsonUtil.WriteUtf8(Path.Combine(archive,"validation-report.txt"),result.ValidationReport);
                    return result;
                } catch (NotSupportedException) { throw;
                } catch (Exception error) {
                    JsonUtil.WriteUtf8(Path.Combine(archive,"failure-"+attempt+".txt"),error.ToString());
                    if (attempt >= 2) throw new InvalidDataException("自由设计未通过规则/编译/行为/一致性检查，源码已保留。\n" + error.Message, error);
                    string diagnostics = error.Message.Length > 6000 ? error.Message.Substring(0, 6000) : error.Message;
                    json = ask(system,
                        input + "\n上一版设计：\n" + json + "\n编译/行为/审核错误：\n" + diagnostics + "\n修正上述错误，保持固定规则不变，返回完整JSON。", 0.15);
                }
            }
            throw new InvalidOperationException("未取得可用设计。");
        }

        public static CreativeBuildResult Compile(CreativeDesign design, string gameDir, string outputDir, string key, string package, string sdkOverride, CustomCardModel revisionBase=null, string requestedColor="RED")
        {
            if (design == null || design.card == null || design.sources == null || !design.sources.ContainsKey("CardLogic.java"))
                throw new InvalidDataException("设计缺少card或CardLogic.java源码。");
            if (!Regex.IsMatch(key, "^[a-zA-Z0-9]+$") || !Regex.IsMatch(package, @"^user\.generated\.[A-Za-z0-9]+$")) throw new InvalidDataException("生成命名空间无效。");
            ValidateSources(design.sources, package);
            string gameJar = Path.Combine(gameDir, "desktop-1.0-original.jar");
            if (!File.Exists(gameJar)) gameJar = Path.Combine(gameDir, "desktop-1.0.jar");
            if (!File.Exists(gameJar)) throw new FileNotFoundException("需要本地游戏jar进行编译。", gameJar);
            List<string> importCorrections;
            Dictionary<string,string> repairedSources=JavaImportRepair.Repair(design.sources,gameJar,out importCorrections);
            if(importCorrections.Count>0) {
                string repairFolder=Path.Combine(outputDir,"import-repair",Guid.NewGuid().ToString("N"));
                foreach(var source in design.sources)JsonUtil.WriteUtf8(Path.Combine(repairFolder,source.Key),source.Value);
                JsonUtil.WriteUtf8(Path.Combine(repairFolder,"corrections.txt"),string.Join("\r\n",importCorrections.ToArray()));
                design.sources=repairedSources;
            }
            CreativeValidation.ValidateDesign(design);
            CustomCardModel card = design.card;
            if(revisionBase!=null && !string.IsNullOrWhiteSpace(revisionBase.image))card.image=revisionBase.image;
            if(string.IsNullOrWhiteSpace(card.image))card.image=card.type=="POWER"?"red/power/metallicize":card.type=="ATTACK"?"red/attack/bash":"red/skill/shrug_it_off";
            if (string.IsNullOrWhiteSpace(card.description) || string.IsNullOrWhiteSpace(card.upgradeDescription)) throw new InvalidDataException("自由设计需要明确的基础和升级描述。");
            card.id = revisionBase==null?"stseditor_ai_"+key:revisionBase.id; card.color = revisionBase==null?requestedColor:revisionBase.color; card.enabled = revisionBase==null || revisionBase.enabled;
            card.effects = new List<EffectModel> { new EffectModel { type = "JAVA_EXTENSION", extensionId = "ai." + key, amount = 1, upgradeAmount = design.contract==null?2:1, parameters=CreativeValidation.Parameters(design.contract), target = "SELF" } };
            card.notes = "AI自由设计，Java扩展；仅已编译，需实战验证。" + design.designSummary;
            List<string> errors = ProfileStore.ValidateCustomCard(card, null, null);
            if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors.ToArray()));
            string sdk = sdkOverride ?? Path.Combine(AppPaths.AppDir, "sdk", "card-extension-sdk.jar");
            if (!File.Exists(sdk)) throw new FileNotFoundException("缺少高级扩展SDK，请使用完整发行目录。", sdk);
            string jdk = FindJdk();
            string sourceDir = Path.Combine(outputDir, "src"); Directory.CreateDirectory(sourceDir);
            string classes = Path.Combine(outputDir, "classes-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(classes);
            List<string> files = new List<string>();
            foreach (KeyValuePair<string, string> item in design.sources) {
                string file = Path.Combine(sourceDir, item.Key); JsonUtil.WriteUtf8(file, item.Value); files.Add(file);
            }
            JsonUtil.WriteUtf8(Path.Combine(outputDir, "design.json"), JsonUtil.ToJson(design));
            Run(Path.Combine(jdk, "bin", "javac.exe"), "-J-Duser.language=en -J-Dfile.encoding=UTF-8 --release 8 -proc:none -encoding UTF-8 -cp " + Quote(gameJar + ";" + sdk) + " -d " + Quote(classes) + " " + string.Join(" ", files.Select(Quote).ToArray()), outputDir);
            string jar = Path.Combine(outputDir, "ai." + key + ".jar");
            Run(Path.Combine(jdk, "bin", "jar.exe"), "cf " + Quote(jar) + " -C " + Quote(classes) + " .", outputDir);
            string manifest = Path.Combine(outputDir, "ai." + key + ".extension.json");
            JsonUtil.WriteUtf8(manifest, JsonUtil.ToJson(new Dictionary<string, object> { {"apiVersion",1}, {"id","ai."+key}, {"name",card.name}, {"className",package+".CardLogic"}, {"jar",Path.GetFileName(jar)}, {"sha256",GameLocator.HashFile(jar)}, {"enabled",true} }));
            return new CreativeBuildResult { Card = card, ExtensionId = "ai." + key, JarPath = jar, ManifestPath = manifest, SourceDirectory = sourceDir, DesignSummary = design.designSummary };
        }

        public static void ValidateSources(Dictionary<string,string> sources, string package)
        {
            if (sources.Count < 1 || sources.Count > 8 || sources.Values.Sum(delegate(string s) { return s == null ? 0 : s.Length; }) > 100000) throw new InvalidDataException("源码数量或长度超出限制。");
            foreach (KeyValuePair<string,string> source in sources) {
                if (!Regex.IsMatch(source.Key, @"^[A-Za-z_$][A-Za-z0-9_$]*\.java$") || source.Value == null) throw new InvalidDataException("源码文件名无效。");
                if (!Regex.IsMatch(source.Value, @"\bpackage\s+" + Regex.Escape(package) + @"\s*;")) throw new InvalidDataException("源码必须使用指定的独立包名：" + package);
                if (Regex.IsMatch(source.Value, @"java\s*\.\s*(io|net|nio|lang\s*\.\s*(reflect|invoke))\b|\b(Runtime|ProcessBuilder|ClassLoader|URLClassLoader|SecurityManager)\b|System\s*\.\s*(exit|setProperty|load|loadLibrary|setSecurityManager)|Class\s*\.\s*forName|\b(native|while)\s*(\(|\{)|for\s*\(\s*;\s*;"))
                    throw new InvalidDataException("自由卡牌源码包含非游戏操作或无界循环；请改用有限的游戏动作和事件回调。");
            }
        }

        public static void Install(CreativeBuildResult result, string gameDir)
        {
            if (!File.Exists(Path.Combine(gameDir, "sts-card-editor", "patch.jar"))) throw new InvalidOperationException("请先安装新版游戏适配层，再保存代码卡牌。");
            string folder = Path.Combine(gameDir, "sts-card-editor", "extensions"); Directory.CreateDirectory(folder);
            string destination = Path.Combine(folder, Path.GetFileName(result.JarPath));
            if (File.Exists(destination) && !string.Equals(GameLocator.HashFile(destination), GameLocator.HashFile(result.JarPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("同ID扩展文件已存在且内容不同，已停止覆盖。");
            if (!File.Exists(destination)) File.Copy(result.JarPath, destination);
            JsonUtil.WriteUtf8(Path.Combine(folder, Path.GetFileName(result.ManifestPath)), JsonUtil.ReadUtf8(result.ManifestPath));
        }

        private static string FindJdk()
        {
            List<string> locations = new List<string>();
            string configured = Environment.GetEnvironmentVariable("JAVA_HOME"); if (!string.IsNullOrEmpty(configured)) locations.Add(configured);
            locations.Add(@"D:\MCLDownload\ext\jre-v64-220420\jdk17"); locations.Add(@"D:\AAA PYCHARM\PyCharm 2026.2\jbr");
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string entry in path.Split(Path.PathSeparator)) if (File.Exists(Path.Combine(entry, "javac.exe"))) locations.Add(Path.GetDirectoryName(entry.TrimEnd(Path.DirectorySeparatorChar)));
            foreach (string location in locations) if (File.Exists(Path.Combine(location, "bin", "javac.exe"))) return location;
            throw new InvalidOperationException("自由代码设计需要本机JDK 9+（目标Java8）；请设置JAVA_HOME，普通效果模式无需JDK。");
        }

        private static string BuildApiReference(string gameDir, string jdk)
        {
            string jar = Path.Combine(gameDir, "desktop-1.0-original.jar");
            if (!File.Exists(jar)) jar = Path.Combine(gameDir, "desktop-1.0.jar");
            StringBuilder reference = new StringBuilder("Available action class names (use exact fully qualified imports):\n");
            using (ZipArchive archive = ZipFile.OpenRead(jar)) {
                foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName.StartsWith("com/megacrit/cardcrawl/actions/", StringComparison.Ordinal) && e.FullName.EndsWith(".class", StringComparison.Ordinal) && !e.FullName.Contains("$")))
                    reference.AppendLine(entry.FullName.Substring(0, entry.FullName.Length - 6).Replace('/', '.'));
            }
            reference.AppendLine("Public signatures of game classes:");
            reference.Append(Run(Path.Combine(jdk, "bin", "javap.exe"), "-J-Duser.language=en -J-Dfile.encoding=UTF-8 -public -classpath " + Quote(jar)
                + " com.megacrit.cardcrawl.powers.AbstractPower com.megacrit.cardcrawl.core.AbstractCreature com.megacrit.cardcrawl.characters.AbstractPlayer com.megacrit.cardcrawl.core.EnergyManager com.megacrit.cardcrawl.ui.panels.EnergyPanel com.megacrit.cardcrawl.cards.AbstractCard com.megacrit.cardcrawl.actions.AbstractGameAction com.megacrit.cardcrawl.actions.common.GainEnergyAction", gameDir));
            return reference.ToString();
        }

        private static string Quote(string value) { if (value.Contains("\"") || value.Contains("\n") || value.Contains("\r")) throw new InvalidDataException("路径含无效字符。"); return "\"" + value + "\""; }
        private static string Run(string exe, string args, string directory)
        {
            ProcessStartInfo start = new ProcessStartInfo(exe, args) { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            using (Process process = Process.Start(start)) {
                System.Threading.Tasks.Task<string> stdout = process.StandardOutput.ReadToEndAsync(); System.Threading.Tasks.Task<string> stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(45000)) { process.Kill(); throw new TimeoutException("Java编译或打包超时。"); }
                string text = stdout.Result + "\n" + stderr.Result;
                if (process.ExitCode != 0) throw new InvalidDataException("Java编译/打包失败：\n" + text);
                return text;
            }
        }
    }
}

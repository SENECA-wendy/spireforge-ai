using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using StsCardEditor;
class CreativeValidationTests
{
 static int passed;
 static void Check(bool value,string label){if(!value)throw new Exception("FAIL "+label);passed++;Console.WriteLine("PASS "+label);}
 static void Reject(Action action,string fragment,string label){try{action();}catch(Exception e){Check(e.Message.Contains(fragment),label+" ("+e.Message.Split('\n')[0]+")");return;}throw new Exception("FAIL accepted "+label);}
 static CreativeContract Contract(){return new CreativeContract{summary="生成六张开枪，升级后六张开枪+，消耗；仅本场战斗",requirements=new List<string>{"普通版生成6张开枪","升级版生成6张开枪+","消耗，保留永久主牌组"},parameters=new Dictionary<string,object>{{"shots",6},{"upgradedShots",6}},probes=new List<CreativeProbe>{new CreativeProbe{metric="cardsToHand",cardName="开枪",upgradedCardName="开枪+",baseExpected=6,upgradedExpected=6}},unverified=new List<string>{"实际战斗动画与能力交互"}};}
 static CreativeDesign Design(string package,bool wrong)
 {
  return new CreativeDesign{card=new CustomCardModel{name="测试轮盘",description="将6张【开枪】加入你的手牌。消耗。",upgradeDescription="将6张【开枪+】加入你的手牌。消耗。",type="SKILL",target="SELF",exhaust=true,cost=0},contract=Contract(),designSummary="数量检查",sources=new Dictionary<string,string>{{"CardLogic.java",
   "package "+package+"; import com.stseditor.runtime.*; import com.megacrit.cardcrawl.cards.AbstractCard; import com.megacrit.cardcrawl.dungeons.AbstractDungeon; import com.megacrit.cardcrawl.actions.common.MakeTempCardInHandAction; public final class CardLogic implements CardExtension { public void onUse(CardExtensionContext ctx){int n=ctx.parameters.get(ctx.card.upgraded?\"upgradedShots\":\"shots\").getAsInt();for(int i=0;i<"+(wrong?"1":"n")+";i++){Shot s=new Shot();if(ctx.card.upgraded)s.upgrade();AbstractDungeon.actionManager.addToBottom(new MakeTempCardInHandAction(s,1));}} public static class Shot extends AbstractCard { public Shot(){super(\"test_shot\",\"开枪\",\"red/attack/bash\",0,\"测试\",CardType.ATTACK,CardColor.RED,CardRarity.COMMON,CardTarget.ENEMY);} public void use(com.megacrit.cardcrawl.characters.AbstractPlayer p,com.megacrit.cardcrawl.monsters.AbstractMonster m){} public AbstractCard makeCopy(){return new Shot();} public void upgrade(){if(!upgraded)upgradeName();}} }"}}};
 }
 static string sdk,game,jdk;
 static CreativeBuildResult Build(CreativeDesign design,string key){return CreativeCardService.Compile(design,game,Path.Combine(AppPaths.AppDir,"creative-validation",key),key,"user.generated.g"+key,sdk);}
 static void Main(string[] args)
 {
  try{
   sdk=Path.GetFullPath(Path.Combine(AppPaths.AppDir,"..","..","sdk","card-extension-sdk.jar"));game=Environment.GetEnvironmentVariable("SPIREFORGE_TEST_GAME_DIR") ?? GameLocator.FindGameDir();jdk=Environment.GetEnvironmentVariable("JAVA_HOME");
   TestParameterTypes();TestImportRepair();
   foreach(string rulePath in args){CreativeContract replay=JsonUtil.FromJson<CreativeContract>(JsonUtil.ReadUtf8(rulePath));CreativeValidation.ValidateContract(replay,"",true);Check(true,"recorded rule response parses and validates: "+Path.GetFileName(rulePath));}
   CreativeContract c=Contract();CreativeValidation.ValidateContract(c,"将6张【开枪】加入你的手牌");Check(true,"explicit count is independently checked against contract");
   c.probes[0].baseExpected=1;Reject(()=>CreativeValidation.ValidateContract(c,"将六张【开枪】加入你的手牌"),"6 张","Chinese numeral count cannot be replaced by a model's own incorrect expectation");
   c=Contract();c.parameters["bad-key"]=3;Reject(()=>CreativeValidation.ValidateContract(c,""),"命名参数","invalid parameter names rejected");
   c=Contract();c.allowMasterDeckChange=true;Reject(()=>CreativeValidation.ValidateContract(c,"清空战斗抽牌堆"),"永久","permanent deck mutation requires explicit scope in user intent");
   c=Contract();c.probes[0].metric="winGame";Reject(()=>CreativeValidation.ValidateContract(c,""),"行为检查","unsupported probe metrics are rejected");
   string key="contract"+DateTime.Now.Ticks;CreativeDesign d=Design("user.generated.g"+key,false);
   d.card.exhaust=false;Reject(()=>CreativeValidation.ValidateDesign(d),"exhaust=false","description cannot claim Exhaust while flag is false");d.card.exhaust=true;
   string original=d.sources["CardLogic.java"];d.sources["CardLogic.java"]=original.Replace("int n=ctx.parameters.get(ctx.card.upgraded?\"upgradedShots\":\"shots\").getAsInt()","int n=ctx.amount");Reject(()=>CreativeValidation.ValidateDesign(d),"ctx.amount","legacy ambiguous amount cannot be used by new AI designs");
   d.sources["CardLogic.java"]=original.Replace("int n=ctx.parameters","ctx.player.masterDeck.group.clear();int n=ctx.parameters");Reject(()=>CreativeValidation.ValidateDesign(d),"masterDeck","combat-only contract cannot silently mutate permanent deck");
   d.sources["CardLogic.java"]=original.Replace("int n=ctx.parameters","int deckSize=ctx.player.masterDeck.size();int n=ctx.parameters");CreativeValidation.ValidateDesign(d);Check(true,"read-only permanent deck queries remain available for creative mechanics");
   d.sources["CardLogic.java"]=original;d.contract.parameters["unused"]=9;Reject(()=>CreativeValidation.ValidateDesign(d),"unused","declared parameters must be bound by generated code");d.contract.parameters.Remove("unused");
   d.contract.parameters["isInnate"]=true;d.contract.parameters["disabled"]=false;d.contract.parameters["cardColor"]="RED";d.contract.parameters["ratio"]=0.5;
   d.sources["CardLogic.java"]=original.Replace("int n=ctx.parameters","if(!ctx.parameters.get(\"isInnate\").getAsBoolean() || ctx.parameters.get(\"disabled\").getAsBoolean() || !ctx.parameters.get(\"cardColor\").getAsString().equals(ctx.card.color.name()) || ctx.parameters.get(\"ratio\").getAsDouble()!=0.5)throw new IllegalStateException(\"parameter type lost\");int n=ctx.parameters");
   CreativeBuildResult good=Build(d,key);Check(Convert.ToDouble(good.Card.effects[0].parameters["shots"])==6 && good.Card.exhaust,"compiler preserves named parameters and actual Exhaust flag");
   Check(good.Card.effects[0].parameters["isInnate"] is bool && (bool)good.Card.effects[0].parameters["isInnate"] && !(bool)good.Card.effects[0].parameters["disabled"] && (string)good.Card.effects[0].parameters["cardColor"]=="RED","compiled card retains boolean and text parameter types");
   string report=CreativeBehavior.Check(good,d.contract,game,jdk);Check(report.Contains("2 项通过")&&!report.Contains("未运行"),"actual compiled Java reads booleans, text and decimal and produces six base/upgraded cards");
   string badKey="wrongcount"+DateTime.Now.Ticks;CreativeDesign bad=Design("user.generated.g"+badKey,true);CreativeBuildResult wrong=Build(bad,badKey);Reject(()=>CreativeBehavior.Check(wrong,bad.contract,game,jdk),"expected=6, actual=1","code that compiles but creates one instead of six is rejected");
   string crashKey="crash"+DateTime.Now.Ticks;CreativeDesign crash=Design("user.generated.g"+crashKey,false);crash.sources["CardLogic.java"]=crash.sources["CardLogic.java"].Replace("int n=ctx.parameters", "String broken=null;broken.length();int n=ctx.parameters");CreativeBuildResult crashBuild=Build(crash,crashKey);Reject(()=>CreativeBehavior.Check(crashBuild,crash.contract,game,jdk),"NullPointerException","runtime logic errors trigger repair rather than a false unsupported pass");
   string pendingKey="pending"+DateTime.Now.Ticks;CreativeDesign pending=Design("user.generated.g"+pendingKey,false);pending.sources["CardLogic.java"]=pending.sources["CardLogic.java"].Replace("int n=ctx.parameters","ctx.player.gainGold(1);int n=ctx.parameters");CreativeBuildResult pendingBuild=Build(pending,pendingKey);string pendingReport=CreativeBehavior.Check(pendingBuild,pending.contract,game,jdk);Check(pendingReport.Contains("未运行完成")&&pendingReport.Contains("未验证项")&&!pendingReport.Contains("2 项通过"),"unsupported game interactions are labeled unverified rather than passed");
   TestPipeline();
   Console.WriteLine("TOTAL "+passed+" creative-generation checks passed (no external model calls)");
  }catch(Exception e){Console.WriteLine(e);Environment.Exit(1);}
 }
 static void TestParameterTypes()
 {
  string json=JsonUtil.ToJson(Contract());
  CreativeContract mixed=JsonUtil.FromJson<CreativeContract>(json.Replace("\"shots\":6","\"shots\":6,\"isInnate\":true,\"disabled\":false,\"cardColor\":\"GREEN\",\"ratio\":0.5"));
  CreativeValidation.ValidateContract(mixed,"");
  Check(mixed.parameters["isInnate"] is bool && (bool)mixed.parameters["isInnate"],"JSON true is accepted as a boolean instead of being converted to Double");
  Check(mixed.parameters["disabled"] is bool && !(bool)mixed.parameters["disabled"],"JSON false is retained as false");
  Check((string)mixed.parameters["cardColor"]=="GREEN" && Convert.ToDouble(mixed.parameters["ratio"])==0.5,"text and fractional parameters are accepted together with integer counts");
  CreativeContract roundtrip=JsonUtil.FromJson<CreativeContract>(JsonUtil.ToJson(mixed));
  Check(roundtrip.parameters["isInnate"] is bool && roundtrip.parameters["cardColor"] is string && Convert.ToDouble(roundtrip.parameters["shots"])==6,"saved mixed contracts preserve scalar types on reload");
  Dictionary<string,object> parameters=CreativeValidation.Parameters(mixed);
  Check(parameters["isInnate"] is bool && parameters["cardColor"] is string,"effect parameter transfer preserves boolean and text values");
  foreach(object invalid in new object[]{null,new object[]{1,2},new Dictionary<string,object>{{"nested",1}}}) {
   CreativeContract c=Contract();c.parameters["invalidSetting"]=invalid;
   Reject(()=>CreativeValidation.ValidateContract(c,""),"invalidSetting","invalid composite/null parameter names are included in diagnostics");
  }
  foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,1000000001d,-1000000001d}) {
   CreativeContract c=Contract();c.parameters["invalidNumber"]=invalid;
   Reject(()=>CreativeValidation.ValidateContract(c,""),"invalidNumber","nonfinite and excessive numbers remain rejected");
  }
  CreativeContract oversized=Contract();oversized.parameters["longText"]=new string('a',4097);
  Reject(()=>CreativeValidation.ValidateContract(oversized,""),"longText","oversized text is rejected with its parameter name");
  CreativeContract legacy=JsonUtil.FromJson<CreativeContract>(json);CreativeValidation.ValidateContract(legacy,"");
  Check(Convert.ToDouble(legacy.parameters["shots"])==6,"existing numeric-only contracts remain compatible");
 }
 static void TestPipeline()
 {
  int implementationCalls=0,auditCalls=0,rulesCalls=0;bool repaired=false,lowTemperature=true;
  CreativeBuildResult result=CreativeCardService.GenerateCore((system,input,temp)=>{
   if(system.StartsWith("规则整理")){rulesCalls++;return JsonUtil.ToJson(Contract());}
   if(system.StartsWith("审核")){auditCalls++;return JsonUtil.ToJson(new CreativeAudit{issues=new List<string>(),unverified=new List<string>{"战斗中的完整表现"}});}
   implementationCalls++;lowTemperature&=temp<=0.2;repaired|=input.Contains("expected=6, actual=1");
   string package=Regex.Match(system,@"package (user\.generated\.[a-zA-Z0-9]+)").Groups[1].Value;
   return JsonUtil.ToJson(Design(package,implementationCalls==1));
  },"将6张【开枪】加入你的手牌，升级后为6张开枪+。消耗。","",game,1.0,sdk);
  Check(rulesCalls==1 && implementationCalls==2 && auditCalls==1 && repaired,"generation repairs a behavior mismatch before independent audit");
  Check(lowTemperature,"creative planning and precise implementation use separate temperatures");
  Check(result.ValidationReport.Contains("2 项通过") && result.ValidationReport.Contains("独立一致性审核") && result.ValidationReport.Contains("仍需实战"),"preview report distinguishes executed checks, model audit, and real-game validation");
  int codeCalls=0,reviews=0;bool receivedAudit=false;
  CreativeCardService.GenerateCore((system,input,temp)=>{
   if(system.StartsWith("规则整理"))return JsonUtil.ToJson(Contract());
   if(system.StartsWith("审核")){reviews++;return JsonUtil.ToJson(new CreativeAudit{issues=reviews==1?new List<string>{"测试审核缺陷：请修复说明"}:new List<string>(),unverified=new List<string>()});}
   codeCalls++;receivedAudit|=input.Contains("测试审核缺陷");string package=Regex.Match(system,@"package (user\.generated\.[a-zA-Z0-9]+)").Groups[1].Value;return JsonUtil.ToJson(Design(package,false));
  },"将6张【开枪】加入你的手牌。消耗。","",game,1,sdk);
  Check(codeCalls==2 && reviews==2 && receivedAudit,"independent audit failures feed concrete diagnostics back to implementation");
  int importsCodeCalls=0;
  CreativeBuildResult imported=CreativeCardService.GenerateCore((system,input,temp)=>{
   if(system.StartsWith("规则整理"))return JsonUtil.ToJson(Contract());
   if(system.StartsWith("审核"))return JsonUtil.ToJson(new CreativeAudit{issues=new List<string>(),unverified=new List<string>()});
   importsCodeCalls++;string package=Regex.Match(system,@"package (user\.generated\.[a-zA-Z0-9]+)").Groups[1].Value;
   CreativeDesign d=Design(package,false);d.sources["CardLogic.java"]=d.sources["CardLogic.java"].Replace("import com.stseditor.runtime.*;","import com.stseditor.runtime.*; import com.megacrit.cardcrawl.core.AbstractPlayer;");return JsonUtil.ToJson(d);
  },"将6张【开枪】加入你的手牌。消耗。","",game,1,sdk);
  Check(importsCodeCalls==1 && imported.ValidationReport.Contains("2 项通过") && Directory.Exists(Path.Combine(Path.GetDirectoryName(imported.JarPath),"import-repair")),"wrong AbstractPlayer import is locally repaired, compiled and executed without another model implementation call");
 }
 static void TestImportRepair()
 {
  string jar=Path.Combine(game,"desktop-1.0-original.jar");List<string> corrections;
  string wrong="import com.megacrit.cardcrawl.core.AbstractPlayer;";
  Func<string,Dictionary<string,string>> files=code=>new Dictionary<string,string>{{"CardLogic.java",code}};
  var result=JavaImportRepair.Repair(files(wrong),jar,out corrections);
  Check(corrections.Count==1 && result["CardLogic.java"].Contains("characters.AbstractPlayer"),"missing game import resolves through the installed jar's unique class name");
  string correct="import com.megacrit.cardcrawl.characters.AbstractPlayer;";
  result=JavaImportRepair.Repair(files(correct),jar,out corrections);Check(corrections.Count==0 && result["CardLogic.java"]==correct,"valid imports are preserved byte for byte");
  string comment="/* "+wrong+" */\n// "+wrong+"\nString message=\""+wrong+"\";\n"+wrong;
  result=JavaImportRepair.Repair(files(comment),jar,out corrections);Check(corrections.Count==1 && result["CardLogic.java"].StartsWith(comment.Substring(0,comment.LastIndexOf(wrong,StringComparison.Ordinal))),"comments and string literals are never rewritten as imports");
  string unknown="import com.megacrit.cardcrawl.core.DoesNotExist;";
  result=JavaImportRepair.Repair(files(unknown),jar,out corrections);Check(corrections.Count==0 && result["CardLogic.java"]==unknown,"unknown class names are left for compiler diagnostics rather than guessed");
  string other="import static com.megacrit.cardcrawl.core.AbstractPlayer.VALUE;\nimport com.megacrit.cardcrawl.core.*;\nimport thirdparty.AbstractPlayer;";
  result=JavaImportRepair.Repair(files(other),jar,out corrections);Check(corrections.Count==0 && result["CardLogic.java"]==other,"static, wildcard and external imports are left unchanged");
  string ambiguous=Path.Combine(AppPaths.AppDir,"ambiguous-imports.jar");
  using(var archive=ZipFile.Open(ambiguous,ZipArchiveMode.Create)){archive.CreateEntry("com/megacrit/cardcrawl/a/AbstractPlayer.class");archive.CreateEntry("com/megacrit/cardcrawl/b/AbstractPlayer.class");}
  try{result=JavaImportRepair.Repair(files(wrong),ambiguous,out corrections);Check(corrections.Count==0 && result["CardLogic.java"]==wrong,"ambiguous class names are not automatically replaced");}finally{File.Delete(ambiguous);}
 }
}

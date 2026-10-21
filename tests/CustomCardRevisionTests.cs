using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using StsCardEditor;
class CustomCardRevisionTests
{
 static int passed;static string root;
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);Console.WriteLine("PASS "+label);passed++;}
 static T Field<T>(object instance,string name){return (T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);}
 static object Call(object instance,string name,params object[] args){return instance.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(instance,args);}
 static void Reject(Action action,string message,string label){try{action();}catch(Exception e){Check(e.Message.Contains(message),label);return;}throw new Exception("FAIL "+label);}
 static CustomCardModel Card(){return new CustomCardModel{id="stseditor_revision_target",name="可修改卡",description="造成 !D! 点伤害。抽1张牌。",upgradeDescription="造成11点伤害。抽2张牌。",damage=7,upgradeDamage=11,cost=1,upgradeCost=0,type="ATTACK",target="ENEMY",effects=new List<EffectModel>{new EffectModel{type="DAMAGE",amount=7,upgradeAmount=11,hits=2,target="ENEMY"},new EffectModel{type="DRAW",amount=1,upgradeAmount=2,target="SELF"}}};}
 [STAThread]static void Main(){try{root=Path.Combine(AppPaths.AppDir,"revision-fixtures",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Numbers();Persistence();Dialog();AiRevision();Console.WriteLine("TOTAL "+passed+" custom-card revision checks passed (isolated files and model responses)");}catch(Exception e){Console.WriteLine(e);Environment.Exit(1);}}
 static void Numbers()
 {
  CustomCardModel source=Card();string before=JsonUtil.ToJson(source);var rows=CustomCardRevisionService.Numbers(source);rows.Single(r=>r.Path=="damage").Text="15";rows.Single(r=>r.Path=="upgradeDamage").Text="21";rows.Single(r=>r.Path=="effect.1.amount").Text="3";
  CustomCardModel edited=CustomCardRevisionService.ApplyNumbers(source,rows);CustomCardRevisionService.Validate(edited);
  Check(edited.damage==15&&edited.upgradeDamage==21&&edited.effects[0].amount==15&&edited.effects[0].upgradeAmount==21,"manual base/upgraded damage updates real effect parameters together");
  Check(edited.effects[1].amount==3&&edited.effects[0].hits==2&&edited.effects[0].target=="ENEMY"&&JsonUtil.ToJson(source)==before,"manual editing preserves unrelated mechanics and never mutates source");
  rows.Single(r=>r.Path=="cost").Text="-3";Reject(()=>CustomCardRevisionService.ApplyNumbers(source,rows),"基础费用","invalid numeric input is rejected before saving");
  rows.Single(r=>r.Path=="cost").Text="1";rows.Single(r=>r.Path=="effect.1.upgradeAmount").Text="100";edited=CustomCardRevisionService.ApplyNumbers(source,rows);Reject(()=>CustomCardRevisionService.Validate(edited),"抽牌数量","upgraded effect values use the same actual engine limits as base effects");
  CustomCardModel java=Card();java.damage=java.upgradeDamage=null;java.effects=new List<EffectModel>{new EffectModel{type="JAVA_EXTENSION",extensionId="ai.old",amount=1,upgradeAmount=2,parameters=new Dictionary<string,object>{{"shots",6},{"ratio",0.5},{"mode","random"}}}};
  var fields=CustomCardRevisionService.Numbers(java);fields.Single(f=>f.Parameter=="shots").Text="8";CustomCardModel updated=CustomCardRevisionService.ApplyNumbers(java,fields);
  Check(Convert.ToDouble(updated.effects[0].parameters["shots"])==8&&Convert.ToDouble(updated.effects[0].parameters["ratio"])==0.5&&(string)updated.effects[0].parameters["mode"]=="random","named Java numeric parameters are editable without losing nonnumeric settings");
  Check(!fields.Any(f=>f.Path=="damage")&&!fields.Any(f=>f.Path=="effect.0.amount"),"Java cards hide unused base stats and obsolete amount when named parameters exist");
  java.effects[0].parameters=null;fields=CustomCardRevisionService.Numbers(java);fields.Single(f=>f.Path=="effect.0.amount").Text="6";Check(CustomCardRevisionService.ApplyNumbers(java,fields).effects[0].amount==6,"legacy Java extension amount remains manually editable");
 }
 static void Persistence()
 {
  string profile=Path.Combine(root,"profile.json"),history=Path.Combine(root,"history");CustomCardModel original=Card(),edited=CustomCardRevisionService.Clone(original);edited.name="修订名称";Profile p=new Profile();p.customCards.Add(original);p.customCards.Add(new CustomCardModel{id="stseditor_other",name="其他卡",effects=new List<EffectModel>{new EffectModel{type="GAIN_ENERGY",amount=1}}});p.overrides["Strike_R"]=new CardOverrideModel{damage=20};JsonUtil.WriteUtf8(profile,JsonUtil.ToJson(p));string before=JsonUtil.ReadUtf8(profile);
  CardRevisionSave saved=CustomCardRevisionService.Save(profile,history,original,edited,"manual");
  Check(saved.Card.id==original.id&&saved.Profile.customCards.Count==2&&saved.Profile.customCards.Single(c=>c.id==original.id).name=="修订名称","revision replaces same card ID without adding a duplicate");
  Check(saved.Profile.overrides["Strike_R"].damage==20&&saved.Profile.customCards.Single(c=>c.id=="stseditor_other").name=="其他卡","revision preserves other cards and native overrides");
  Check(JsonUtil.ReadUtf8(Path.Combine(saved.HistoryFolder,"profile-before.json"))==before&&CustomCardRevisionService.Previous(history,original.id).name==original.name,"exact backup and previous-card history are available");
  string current=JsonUtil.ReadUtf8(profile);Reject(()=>CustomCardRevisionService.Save(profile,history,original,edited,"stale"),"其他窗口","stale editor cannot overwrite a card modified by another window");Check(JsonUtil.ReadUtf8(profile)==current,"conflict leaves latest profile bytes unchanged");
  CustomCardModel newer=CustomCardRevisionService.Clone(edited);newer.cost=2;p=JsonUtil.FromJson<Profile>(current);p.customCards.Single(c=>c.id=="stseditor_other").name="外部新名字";JsonUtil.WriteUtf8(profile,JsonUtil.ToJson(p));saved=CustomCardRevisionService.Save(profile,history,edited,newer,"manual");Check(saved.Profile.customCards.Single(c=>c.id=="stseditor_other").name=="外部新名字","concurrent changes to unrelated cards are merged from disk");
  CustomCardModel invalid=CustomCardRevisionService.Clone(newer);invalid.id="stseditor_new_id";Reject(()=>CustomCardRevisionService.Save(profile,history,newer,invalid,"bad"),"保留原卡ID","revision cannot accidentally change the stable identity");
 }
 static void Dialog()
 {
  CustomCardModel original=Card();string before=JsonUtil.ToJson(original);bool saved=false;int requests=0;
  using(CustomCardEditorDialog dialog=new CustomCardEditorDialog(original,(card,changes)=>{requests++;Check(card.damage==16&&changes.Contains("12"),"AI revision receives current manual draft and requested changes");CustomCardModel ai=CustomCardRevisionService.Clone(card);ai.damage=12;ai.effects[0].amount=12;ai.name="AI修订草稿";return new CreativeBuildResult{Card=ai,ValidationReport="测试审核报告"};},(card,build,report)=>{saved=true;return new CardRevisionSave{Card=card,Profile=new Profile()};},()=>original)) {
   DataGridView grid=Field<DataGridView>(dialog,"numbers");foreach(DataGridViewRow row in grid.Rows)if(((CardNumberField)row.Tag).Path=="damage")row.Cells["value"].Value="16";
   Check(dialog.ReadDraft().damage==16&&!saved&&JsonUtil.ToJson(original)==before,"editing a library card stays in an isolated draft until explicit save");
   Field<TextBox>(dialog,"instructions").Text="伤害改为12";Call(dialog,"Generate");DateTime deadline=DateTime.UtcNow.AddSeconds(5);while(Field<bool>(dialog,"busy")&&DateTime.UtcNow<deadline){Application.DoEvents();Thread.Sleep(10);}
   Check(requests==1&&dialog.ReadDraft().name=="AI修订草稿"&&dialog.ReadDraft().id==original.id&&!saved,"AI result updates preview without automatically saving or syncing");
   Call(dialog,"LoadDraft",original,null);Check(dialog.ReadDraft().damage==7&&dialog.ReadDraft().name==original.name&&!saved,"reset discards AI draft without touching library");
   dialog.ClientSize=new Size(1120,760);ForceHandles(dialog);using(Bitmap image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(0,0,dialog.Width,dialog.Height));image.Save(Path.Combine(AppPaths.AppDir,"custom-card-revision-v1.5.2.png"));}
   Check(grid.Width>450&&Field<TextBox>(dialog,"preview").Height>350,"revision dialog allocates readable parameter grid and side-by-side reference preview");
  }
 }
 static void ForceHandles(Control c){IntPtr handle=c.Handle;c.PerformLayout();foreach(Control child in c.Controls)ForceHandles(child);}
 static void AiRevision()
 {
  CustomCardModel original=Card();original.color="COLORLESS";original.image="red/skill/shrug_it_off";original.enabled=false;original.effects=new List<EffectModel>{new EffectModel{type="JAVA_EXTENSION",extensionId="ai.originalsource",amount=1}};
  string source=Path.Combine(root,"generated","originalsource","src");Directory.CreateDirectory(source);JsonUtil.WriteUtf8(Path.Combine(source,"CardLogic.java"),"// original unique source marker");string context=CustomCardRevisionService.Context(original,root);
  Check(context.Contains("original unique source marker")&&context.Contains(original.id),"revision context resolves original source by extension ID");
  bool received=false;string sdk=Path.GetFullPath(Path.Combine(AppPaths.AppDir,"..","..","sdk","card-extension-sdk.jar"));
  CreativeBuildResult result=CreativeCardService.GenerateCore((system,input,temp)=>{
   received|=input.Contains("original unique source marker")&&input.Contains("改为3")&&input.Contains(original.id);
   if(system.StartsWith("规则整理"))return JsonUtil.ToJson(new CreativeContract{summary="基础3能量，升级4能量",requirements=new List<string>{"基础3能量，升级4能量"},parameters=new Dictionary<string,object>{{"energy",3},{"upgradedEnergy",4}},probes=new List<CreativeProbe>{new CreativeProbe{metric="energyGained",baseExpected=3,upgradedExpected=4}},unverified=new List<string>{"真实遗物交互"}});
   if(system.StartsWith("审核"))return JsonUtil.ToJson(new CreativeAudit{issues=new List<string>(),unverified=new List<string>()});
   string package=Regex.Match(system,@"package (user\.generated\.[a-zA-Z0-9]+)").Groups[1].Value;
   return JsonUtil.ToJson(new CreativeDesign{card=new CustomCardModel{name="修订能量",description="获得3点能量。",upgradeDescription="获得4点能量。",cost=0,type="SKILL",target="SELF"},designSummary="测试修订",sources=new Dictionary<string,string>{{"CardLogic.java","package "+package+"; import com.stseditor.runtime.*; import com.megacrit.cardcrawl.dungeons.AbstractDungeon; import com.megacrit.cardcrawl.actions.common.GainEnergyAction; public final class CardLogic implements CardExtension {public void onUse(CardExtensionContext ctx){if(!ctx.card.cardID.equals(\""+original.id+"\"))throw new IllegalArgumentException(\"identity\"); AbstractDungeon.actionManager.addToBottom(new GainEnergyAction(ctx.parameters.get(ctx.card.upgraded?\"upgradedEnergy\":\"energy\").getAsInt()));}}"}}});
  },"能量改为3，升级后4，其余保留","",GameLocator.FindGameDir(),1,sdk,original,context);
  Check(received&&result.Card.id==original.id&&result.Card.color=="COLORLESS"&&result.Card.image==original.image&&!result.Card.enabled,"AI revision retains card identity, image, color and enabled state while receiving original context");
  Check(result.ExtensionId!="ai.originalsource"&&File.Exists(result.JarPath)&&result.ValidationReport.Contains("2 项通过"),"AI revision compiles a fresh extension version and executes base/upgraded checks with stable card ID");
  Check(original.name=="可修改卡"&&original.effects[0].extensionId=="ai.originalsource","AI revision does not mutate old definition or extension reference");
 }
}

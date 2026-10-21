using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using StsCardEditor;
class FourProfessionTests
{
 static int passed;static Catalog catalog;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);passed++;}
 static T Field<T>(object obj,string name){return (T)obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);}
 static void Set(object obj,string name,object value){obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);}
 static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,args);}
 static CatalogCard Card(string id){return catalog.cards.Single(c=>c.id==id);}
 static void Select(ComboBox box,string color){box.SelectedItem=box.Items.Cast<ProfessionChoice>().Single(c=>c.Color==color);}
 static string Id(object obj){return Convert.ToString(obj.GetType().GetField("Id").GetValue(obj));}
 static CatalogCard Native(object obj){return (CatalogCard)obj.GetType().GetField("Card").GetValue(obj);}
 [STAThread]static void Main(){try{catalog=ProfileStore.LoadCatalog();CatalogChecks();UiChecks();AiChecks();Console.WriteLine("TOTAL "+passed+" four-profession checks passed (local game catalog, isolated UI and model responses)");}catch(Exception e){Console.WriteLine("FAIL "+e);Environment.Exit(1);}}
 static void CatalogChecks()
 {
  Check(catalog.cards.Count==300&&catalog.cards.Select(c=>c.id).Distinct().Count()==300,"four-class catalog contains exactly 300 distinct registered cards");
  Catalog repeated=JsonUtil.FromJson<Catalog>(JsonUtil.ReadUtf8(Path.GetFullPath(Path.Combine(AppPaths.AppDir,"..","catalog-regression.json"))));Check(repeated.cards.Count==catalog.cards.Count&&catalog.cards.All(c=>typeof(CatalogCard).GetProperties().All(p=>object.Equals(p.GetValue(c,null),p.GetValue(repeated.cards.Single(r=>r.id==c.id),null)))),"release catalog matches a fresh extraction of actual registered card constructors and upgrades");
  foreach(string color in Professions.NativeColors)Check(catalog.cards.Count(c=>c.color==color)==75&&catalog.cards.Where(c=>c.color==color).All(c=>!string.IsNullOrWhiteSpace(c.name)&&!string.IsNullOrWhiteSpace(c.description)&&!string.IsNullOrWhiteSpace(c.upgradeDescription)),color+" has 75 localized base/upgraded card definitions");
  string[] missing=catalog.cards.Where(c=>NativeCardPreview.Build(c).Contains("未设置")||Regex.IsMatch(NativeCardPreview.Build(c),@"![DBM]!" )).Select(c=>c.id).ToArray();Check(missing.Length==0,"all 300 introductions resolve numeric placeholders: "+string.Join(",",missing));
  Check(Card("Neutralize").damage==3&&Card("Neutralize").upgradeDamage==4&&Card("Deadly Poison").magicNumber==5&&Card("Deadly Poison").upgradeMagicNumber==7,"Silent damage and poison base/upgrade values match actual constructors");
  Check(Card("Zap").cost==1&&Card("Zap").upgradeCost==0&&Card("Dualcast").upgradeCost==0&&Card("Defend_B").upgradeBlock==8,"Defect cost upgrades and basic block values are extracted exactly");
  Check(Card("Eruption").damage==9&&Card("Eruption").upgradeDamage==9&&Card("Eruption").upgradeCost==1&&Card("Vigilance").upgradeBlock==12,"Watcher Eruption changes cost while Vigilance changes block");
  Check(Card("Halt").block==3&&Card("Halt").upgradeBlock==4&&Card("Halt").magicNumber==9&&Card("Halt").upgradeMagicNumber==14,"Watcher secondary block value is separate from ordinary block");
  Check(Card("Fission").description!=Card("Fission").upgradeDescription&&Card("Fission").upgradeDescription.Contains("激发"),"Fission upgraded text describes evoking rather than removing orbs");
  Check(Card("Searing Blow").upgradeDamage==16&&Card("Rampage").upgradeDamage==8&&Card("Hemokinesis").upgradeMagicNumber==2,"Ironclad special upgrades retain accurate actual field values");
  Check(Card("PathToVictory").specialNote.Contains("印记")&&Card("Multi-Cast").specialNote.Contains("X费")&&Card("Catalyst").specialNote.Contains("乘数"),"special mechanism notes resolve actual IDs that differ from class names");
  var strikes=Professions.NativeColors.Select(c=>Card(Professions.Strike(c))).ToArray();var plans=NativeLiveEditService.Plan(strikes,new CardOverrideModel{damage=20},new Dictionary<string,CardOverrideModel>(),false);
  Check(plans.Count==4&&plans.Select(p=>p.Id).Distinct().Count()==4&&plans.All(p=>p.Values.damage==20&&p.Values.upgradeDamage==23),"four same-name Strikes remain distinct by ID during batch edits");
  plans=NativeLiveEditService.Plan(new[]{Card("Bash"),Card("Neutralize"),Card("Eruption")},new CardOverrideModel{damage=20},new Dictionary<string,CardOverrideModel>(),false);
  Check(plans[0].Values.upgradeDamage==22&&plans[1].Values.upgradeDamage==21&&plans[2].Values.upgradeDamage==20,"mixed-profession batches preserve each card's distinct native upgrade delta");
  foreach(string color in Professions.CustomColors){CustomCardModel card=new CustomCardModel{id="stseditor_color_"+color,name="职业卡",color=color,effects=new List<EffectModel>{new EffectModel{type="GAIN_ENERGY",amount=1}}};Check(ProfileStore.ValidateCustomCard(card,null,null).Count==0&&CustomCardPreview.Build(card).Contains(Professions.Name(color)),"custom specification and preview accept "+color);}
 }
 static void UiChecks()
 {
  using(MainForm form=new MainForm()){
   ComboBox native=Field<ComboBox>(form,"cmbNativeProfession");ListBox nativeList=Field<ListBox>(form,"lstCards");
   foreach(string color in Professions.NativeColors){Select(native,color);Check(nativeList.Items.Count==75&&nativeList.Items.Cast<object>().All(o=>Native(o).color==color),"native editing filter exposes only "+color+" without losing multi-select");}
   Select(native,"ALL");Check(nativeList.Items.Count==300&&nativeList.SelectionMode==SelectionMode.MultiExtended,"all-profession native view retains batch selection");
   Profile profile=new Profile();foreach(string color in new[]{"RED","BLUE","COLORLESS"})profile.customCards.Add(new CustomCardModel{id="stseditor_filter_"+color,name="筛选测试"+color,color=color,effects=new List<EffectModel>{new EffectModel{type="GAIN_ENERGY",amount=1}}});Set(form,"profile",profile);Call(form,"RefreshCustomList");
   ComboBox deck=Field<ComboBox>(form,"cmbDeckProfession");ListBox sources=Field<ListBox>(form,"cmbDeckSource");Select(deck,"GREEN");Check(sources.Items.Count==76&&sources.Items.Cast<object>().Any(o=>Id(o)=="stseditor_filter_COLORLESS")&&!sources.Items.Cast<object>().Any(o=>Id(o)=="stseditor_filter_BLUE"),"deck source filter combines chosen profession with universal colorless custom cards");
   Select(deck,"CUSTOM");Check(sources.Items.Count==3&&sources.Items.Cast<object>().All(o=>Id(o).StartsWith("stseditor_")),"custom-only source filter can expose custom cards from other professions");
   List<object> runtime=catalog.cards.Select(c=>(object)new Dictionary<string,object>{{"cardId",c.id},{"name",c.name},{"isCustom",false},{"obtainable",true}}).ToList();foreach(CustomCardModel c in profile.customCards)runtime.Add(new Dictionary<string,object>{{"cardId",c.id},{"name",c.name},{"isCustom",true},{"obtainable",true}});Call(form,"UpdatePoolSources",runtime);
   HashSet<string> draft=Field<HashSet<string>>(form,"poolDraft");draft.Clear();draft.Add("Strike_R");draft.Add("Strike_B");ComboBox pool=Field<ComboBox>(form,"cmbPoolProfession");Select(pool,"GREEN");Check(draft.SetEquals(new[]{"Strike_R","Strike_B"})&&Field<ListBox>(form,"lstPoolSources").Items.Count==76,"switching pool profession never deletes mixed-profession allowed IDs");
   Call(form,"AddVisiblePoolSources");Check(draft.Count==78&&draft.Contains("Deadly Poison")&&!draft.Contains("Eruption"),"add-all only adds currently filtered candidates while preserving prior allowed cards");
   foreach(string color in Professions.NativeColors){Select(pool,color);Call(form,"SetStrikeOnlyPool");Check(draft.SetEquals(new[]{Professions.Strike(color)}),"single-Strike shortcut selects the correct "+color+" card ID");}
   Select(native,"AUTO");Select(deck,"AUTO");Select(pool,"AUTO");Call(form,"DetectProfession",new Dictionary<string,object>{{"cardColor","BLUE"},{"playerClass","DEFECT"}});Check(nativeList.Items.Count==75&&nativeList.Items.Cast<object>().All(o=>Native(o).color=="BLUE"),"connecting to a Defect run updates automatic native profession filter");
   Call(form,"SetStrikeOnlyPool");Check(draft.SetEquals(new[]{"Strike_B"}),"automatic pool shortcut follows detected current run profession");
   Select(native,"GREEN");Call(form,"DetectProfession",new Dictionary<string,object>{{"cardColor","PURPLE"},{"playerClass","WATCHER"}});Check(nativeList.Items.Cast<object>().All(o=>Native(o).color=="GREEN")&&Professions.Choice(native)=="GREEN","reading another run never overrides an explicitly chosen profession filter");
   ComboBox creation=Field<ComboBox>(form,"cmbAiProfession");creation.SelectedItem="BLUE";Field<TextBox>(form,"txtPrompt").Text="0费技能，抽2张牌。";Call(form,"ParseLocal");Check(Field<CustomCardModel>(form,"generatedCard").color=="BLUE","offline custom creation honors selected profession");
   TabControl outer=Field<TabControl>(form,"tabs");outer.SelectedTab=Field<TabPage>(form,"cardEditorPage");Select(native,"PURPLE");Field<TextBox>(form,"txtFilter").Text="Eruption";Force(form);Save(form,"four-profession-native-v1.6.png");
   Field<TextBox>(form,"txtPoolSearch").Text="";Select(pool,"GREEN");outer.SelectedTab=outer.TabPages.Cast<TabPage>().Single(p=>p.Text=="本局可获得牌池");Force(form);Save(form,"four-profession-pool-v1.6.png");Check(Field<ListBox>(form,"lstPoolSources").Width>250&&Field<TextBox>(form,"txtPoolPreview").Width>250,"profession filter fits acquisition-pool layout with preview");
  }
  using(CustomCardEditorDialog dialog=new CustomCardEditorDialog(new CustomCardModel{id="stseditor_edit_color",name="职业",effects=new List<EffectModel>{new EffectModel{type="GAIN_ENERGY",amount=1}}},(a,b)=>null,(a,b,c)=>null,()=>null)){
   ComboBox colors=Field<ComboBox>(dialog,"color");colors.SelectedItem="PURPLE";Check(colors.Items.Count==5&&dialog.ReadDraft().color=="PURPLE","library re-editing exposes all four professions plus colorless");
  }
 }
 static void Force(Control c){IntPtr handle=c.Handle;c.PerformLayout();foreach(Control child in c.Controls)Force(child);}
 static void Save(Form form,string name){using(Bitmap image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height));image.Save(Path.Combine(AppPaths.AppDir,name));}}
 static void AiChecks()
 {
  string sdk=Path.GetFullPath(Path.Combine(AppPaths.AppDir,"..","..","sdk","card-extension-sdk.jar"));
  foreach(string color in new[]{"GREEN","BLUE","PURPLE"}){
   bool selectedColorSent=false;CreativeBuildResult result=CreativeCardService.GenerateCore((system,input,temp)=>{
    selectedColorSent|=input.Contains(Professions.Name(color)+"（"+color+"）");
    if(system.StartsWith("规则整理"))return JsonUtil.ToJson(new CreativeContract{summary="基础2能量，升级3能量",requirements=new List<string>{"获得2/3能量"},parameters=new Dictionary<string,object>{{"energy",2},{"upgradedEnergy",3}},probes=new List<CreativeProbe>{new CreativeProbe{metric="energyGained",baseExpected=2,upgradedExpected=3}},unverified=new List<string>{"实战验证"}});
    if(system.StartsWith("审核"))return JsonUtil.ToJson(new CreativeAudit{issues=new List<string>(),unverified=new List<string>()});
    string package=Regex.Match(system,@"package (user\.generated\.[a-zA-Z0-9]+)").Groups[1].Value;
    return JsonUtil.ToJson(new CreativeDesign{card=new CustomCardModel{name="职业能量测试",description="获得2点能量。",upgradeDescription="获得3点能量。",cost=0,type="SKILL",target="SELF"},designSummary="职业测试",sources=new Dictionary<string,string>{{"CardLogic.java","package "+package+";import com.stseditor.runtime.*;import com.megacrit.cardcrawl.cards.AbstractCard;import com.megacrit.cardcrawl.dungeons.AbstractDungeon;import com.megacrit.cardcrawl.actions.common.GainEnergyAction;public final class CardLogic implements CardExtension{public void onUse(CardExtensionContext ctx){if(ctx.card.color!=AbstractCard.CardColor."+color+")throw new IllegalArgumentException(\"wrong color\");AbstractDungeon.actionManager.addToBottom(new GainEnergyAction(ctx.parameters.get(ctx.card.upgraded?\"upgradedEnergy\":\"energy\").getAsInt()));}}"}}});
   },"获得2点能量，升级3点","",(Environment.GetEnvironmentVariable("SPIREFORGE_TEST_GAME_DIR") ?? GameLocator.FindGameDir()),1,sdk,null,null,color);
   Check(selectedColorSent&&result.Card.color==color,"AI creation passes and preserves "+color+" profession");
   Check(File.Exists(result.JarPath)&&result.ValidationReport.Contains("2 项通过"),"compiled "+color+" card executes base/upgraded checks with correct actual color");
  }
 }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using StsCardEditor;
class LocalizationTests
{
 static int passed;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
 static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
 static IEnumerable<Control> Walk(Control control){yield return control;foreach(Control child in control.Controls)foreach(Control item in Walk(child))yield return item;}
 static void Force(Control control){IntPtr handle=control.Handle;control.PerformLayout();foreach(Control child in control.Controls)Force(child);}
 static void Save(Form form,string name){Force(form);using(Bitmap bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(Path.Combine(AppPaths.AppDir,name));}}
 [STAThread]static void Main(string[] args){if(args.Length==2&&args[0]=="--read-preference"){UiText.Initialize(args[1]);Environment.Exit(UiText.IsEnglish?0:2);return;}try{Run();Console.WriteLine("TOTAL "+passed+" localization checks passed (isolated UI; no model calls)");}catch(Exception e){Console.WriteLine("FAIL "+e);Environment.Exit(1);}finally{UiText.SetLanguage("zh-CN",false);}}
 static void Run()
 {
  UiText.SetLanguage("zh-CN",false);
  Check(UiText.T("AI 创作")=="AI 创作","Chinese is preserved in Chinese mode");
  using(MainForm form=new MainForm()) {
   TextBox prompt=Field<TextBox>(form,"txtPrompt"),name=Field<TextBox>(form,"txtCardName"),damage=Field<TextBox>(form,"txtDamage");
   ListBox cards=Field<ListBox>(form,"lstCards");ComboBox profession=Field<ComboBox>(form,"cmbNativeProfession"),language=Field<ComboBox>(form,"cmbUiLanguage");
   prompt.Text="攻击 Power：生成8张牌，不修改原文";name.Text="攻击";damage.Text="42";
   string selected=cards.SelectedItem.ToString();int[] selection=cards.SelectedIndices.Cast<int>().ToArray();string professionId=Professions.Choice(profession);
   string beforeProfile=JsonUtil.ReadUtf8(AppPaths.ProfilePath);
   TabControl tabs=Field<TabControl>(form,"tabs");TabPage selectedTab=tabs.SelectedTab;
   UiText.SetLanguage("en",false);Application.DoEvents();
   Check(form.Text.Contains("SpireForge AI")&&!Regex.IsMatch(form.Text,"[\u4e00-\u9fff]"),"English title uses the AI-creation brand");
   Check(tabs.TabPages.Cast<TabPage>().All(p=>!Regex.IsMatch(p.Text,"[\u4e00-\u9fff]")),"all main navigation labels switch to English");
   Check(Walk(form).OfType<Button>().All(b=>!Regex.IsMatch(b.Text,"[\u4e00-\u9fff]")),"all action button labels switch to English");
   Check(prompt.Text=="攻击 Power：生成8张牌，不修改原文"&&name.Text=="攻击","language switching preserves user prompts and custom names exactly");
   Check(damage.Text=="42","unsaved numeric edits survive language switching");
   Check(selection.SequenceEqual(cards.SelectedIndices.Cast<int>())&&professionId==Professions.Choice(profession)&&tabs.SelectedTab==selectedTab,"card multi-selection, character filter and active tab remain intact");
   Check(JsonUtil.ReadUtf8(AppPaths.ProfilePath)==beforeProfile,"switching language does not write the card library");
   Check(Professions.Name("GREEN")=="Silent"&&Professions.Name("BLUE")=="Defect"&&Professions.Name("PURPLE")=="Watcher","character display names are localized without changing their IDs");
   Check(cards.GetItemText(cards.Items[0]).Contains("Ironclad"),"native list character labels follow the active language");
   ComboBox groups=Field<ComboBox>(form,"cmbRunGroup");Check(Convert.ToString(groups.Items[0])=="主牌组"&&groups.GetItemText(groups.Items[0])=="Master deck","translated option display preserves stable internal card-zone values");
   Catalog catalog=ProfileStore.LoadCatalog();string preview=NativeCardPreview.Build(catalog.cards.Single(c=>c.id=="Bash"));
   Check(preview.Contains("Bash")&&preview.Contains("Vulnerable")&&!preview.Contains("!D!"),"English native preview uses actual localized game text and resolves values");
   Check(UiText.NativeCard(catalog.cards.Single(c=>c.id=="Eruption")).name=="Eruption","English native names are available for other characters");
   CustomCardModel custom=new CustomCardModel{id="stseditor_i18n",name="攻击",description="保留这段原文 Power",upgradeDescription="升级后原文",color="BLUE",cost=0,innate=true,effects=new List<EffectModel>{new EffectModel{type="GAIN_ENERGY",amount=1}}};
   string customPreview=CustomCardPreview.Build(custom);
   Check(customPreview.Contains("保留这段原文 Power")&&customPreview.Contains("升级后原文")&&customPreview.Contains("Properties: Innate"),"custom card content is preserved while preview labels are translated");
   Save(form,"spireforge-en-v1.7.png");
   UiText.SetLanguage("zh-CN",false);Application.DoEvents();
   Check(tabs.TabPages[0].Text=="卡牌编辑 · 实时"&&damage.Text=="42"&&prompt.Text=="攻击 Power：生成8张牌，不修改原文","switching back restores Chinese labels and preserves drafts");
   Save(form,"spireforge-zh-v1.7.png");
   using(CustomCardEditorDialog dialog=new CustomCardEditorDialog(custom,null,null,null)) {
    TextBox desc=Field<TextBox>(dialog,"description");desc.Text="用户未保存的描述";
    UiText.SetLanguage("en",false);Application.DoEvents();
    Check(desc.Text=="用户未保存的描述"&&Field<TextBox>(dialog,"name").Text=="攻击","revision dialog preserves unsaved card text on language change");
    Check(dialog.Text.EndsWith("攻击"),"dialog title preserves the custom card name even when it matches a translation key");
    Check(Walk(dialog).OfType<Button>().All(b=>!Regex.IsMatch(b.Text,"[\u4e00-\u9fff]")),"revision dialog actions are localized");
    Save(dialog,"spireforge-revision-en-v1.7.png");
   }
   Check(UiText.T("校验失败")=="Validation failed"&&UiText.T("费用")=="Cost","validation labels and dialog captions are translated");
  }
  UiText.SetLanguage("zh-CN",false);
  string preferences=Path.Combine(AppPaths.AppDir,"localization-settings-test.json");UiText.SetLanguage("en",true,preferences);
  using(Process child=Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--read-preference \""+preferences+"\""){UseShellExecute=false,CreateNoWindow=true})){child.WaitForExit();Check(child.ExitCode==0,"a fresh process restores the persisted English preference");}
  UiText.SetLanguage("zh-CN",true,preferences);Check(JsonUtil.ReadUtf8(preferences).Contains("zh-CN"),"switching back persists Chinese without touching API settings");File.Delete(preferences);
 }
}

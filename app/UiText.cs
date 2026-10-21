using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace StsCardEditor
{
    public static class UiText
    {
        static readonly Dictionary<string,string> english=UiStrings.Create();
        static readonly Dictionary<string,string> chinese=english.GroupBy(p=>p.Value).ToDictionary(g=>g.Key,g=>g.First().Key);
        static readonly Regex forward=Pattern(english.Keys),backward=Pattern(chinese.Keys);
        static bool initialized;
        public static bool IsEnglish {get;private set;}
        public static event EventHandler LanguageChanged;
        static Dictionary<string,CatalogCard> nativeEnglish;
        public static CatalogCard NativeCard(CatalogCard card) {
            if(card==null||!IsEnglish)return card;
            if(nativeEnglish==null) {
                nativeEnglish=new Dictionary<string,CatalogCard>();
                try {string file=Path.Combine(AppPaths.AppDir,"catalog.en.json");if(File.Exists(file))foreach(CatalogCard item in JsonUtil.FromJson<Catalog>(JsonUtil.ReadUtf8(file)).cards)nativeEnglish[item.id]=item;}catch{}
            }
            CatalogCard localized;return nativeEnglish.TryGetValue(card.id,out localized)?localized:card;
        }
        public static string NativeName(string id,string fallback) {
            if(!IsEnglish)return fallback;CatalogCard card=NativeCard(new CatalogCard{id=id,name=fallback});return card.name??fallback;
        }
        public static string SettingsPath {get {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"STSCardEditor","ui-language.json");}}
        static Regex Pattern(IEnumerable<string> keys) {
            return new Regex(string.Join("|",keys.Where(k=>k.Length>1).OrderByDescending(k=>k.Length).Select(k=>(char.IsLetter(k[0])&&k[0]<128?@"(?<![A-Za-z])":"")+Regex.Escape(k)+(char.IsLetter(k[k.Length-1])&&k[k.Length-1]<128?@"(?![A-Za-z])":"")).ToArray()),RegexOptions.Compiled);
        }
        public static void Initialize(string preferencePath=null) {
            if(initialized)return;initialized=true;
            string path=preferencePath??SettingsPath;
            try {if(File.Exists(path)){var doc=JsonUtil.FromJson(JsonUtil.ReadUtf8(path)) as Dictionary<string,object>;object language;IsEnglish=doc!=null&&doc.TryGetValue("language",out language)&&Convert.ToString(language)=="en";}}catch{IsEnglish=false;}
        }
        public static string T(string source) {
            if(source==null)return null;string value;
            if(IsEnglish)return english.TryGetValue(source,out value)?value:source;
            return chinese.TryGetValue(source,out value)?value:source;
        }
        public static string Display(string source) {
            if(source==null)return null;string exact=T(source);if(exact!=source)return exact;
            return IsEnglish?forward.Replace(source,m=>english[m.Value]):backward.Replace(source,m=>chinese[m.Value]);
        }
        public static void SetLanguage(string language,bool persist=true,string preferencePath=null) {
            if(language!="zh-CN"&&language!="en")throw new ArgumentException("Unsupported UI language");
            bool value=language=="en";
            if(persist)JsonUtil.WriteUtf8(preferencePath??SettingsPath,JsonUtil.ToJson(new{language=language}));
            initialized=true;if(IsEnglish==value)return;IsEnglish=value;
            EventHandler changed=LanguageChanged;if(changed!=null)changed(null,EventArgs.Empty);
        }
        sealed class BoundText {public string Source;public bool Updating;}
        public static void Attach(Form form,Action refresh=null) {
            HashSet<Control> bound=new HashSet<Control>();List<Action> updates=new List<Action>();
            Action<Control> visit=null;visit=control=>{
                if(!bound.Add(control))return;
                if(!(control is TextBoxBase) && !(control is UpDownBase) && !(control is ListControl) && !(control is CustomCardEditorDialog) && (control is Form || control is Label || control is ButtonBase || control is GroupBox || control is TabPage)) {
                    BoundText state=new BoundText{Source=control.Text};
                    Action update=()=>{if(control.IsDisposed)return;state.Updating=true;try{control.Text=Display(state.Source);}finally{state.Updating=false;}};
                    control.TextChanged+=delegate{if(state.Updating)return;state.Source=control.Text;update();};updates.Add(update);update();
                }
                ListControl list=control as ListControl;
                if(list!=null) {
                    ComboBox combo=list as ComboBox;ListBox box=list as ListBox;
                    if(combo!=null) {
                        int selected=combo.SelectedIndex;combo.FormattingEnabled=true;
                        combo.Format+=delegate(object sender,ListControlConvertEventArgs e){if(e.ListItem is string)e.Value=T(Convert.ToString(e.ListItem));};
                        combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=Math.Max(22,combo.Font.Height+6);
                        combo.DrawItem+=delegate(object sender,DrawItemEventArgs e){e.DrawBackground();if(e.Index>=0)TextRenderer.DrawText(e.Graphics,combo.GetItemText(combo.Items[e.Index]),e.Font,e.Bounds,e.ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);e.DrawFocusRectangle();};
                        if(selected>=0)combo.SelectedIndex=selected;
                    }
                    if(box!=null) {
                        box.DrawMode=DrawMode.OwnerDrawFixed;box.ItemHeight=Math.Max(22,box.Font.Height+6);
                        box.DrawItem+=delegate(object sender,DrawItemEventArgs e){e.DrawBackground();if(e.Index>=0){object item=box.Items[e.Index];string text=item is string?T((string)item):Convert.ToString(item);TextRenderer.DrawText(e.Graphics,text,e.Font,e.Bounds,e.ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);}e.DrawFocusRectangle();};
                    }
                    updates.Add(()=>{if(!list.IsDisposed)list.Invalidate();});
                }
                DataGridView grid=control as DataGridView;
                if(grid!=null) {
                    foreach(DataGridViewColumn column in grid.Columns){string header=column.HeaderText;updates.Add(()=>column.HeaderText=T(header));}
                    grid.CellFormatting+=delegate(object sender,DataGridViewCellFormattingEventArgs e){if(e.ColumnIndex>=0 && (grid.Columns[e.ColumnIndex].Name=="label" || grid.Columns[e.ColumnIndex].Name=="help")){e.Value=Display(Convert.ToString(e.Value));e.FormattingApplied=true;}};
                    updates.Add(()=>grid.Invalidate());
                }
                StatusStrip strip=control as StatusStrip;
                if(strip!=null)foreach(ToolStripItem item in strip.Items){BoundText state=new BoundText{Source=item.Text};Action update=()=>{state.Updating=true;try{item.Text=Display(state.Source);}finally{state.Updating=false;}};item.TextChanged+=delegate{if(!state.Updating){state.Source=item.Text;update();}};updates.Add(update);update();}
                foreach(Control child in control.Controls)visit(child);
                control.ControlAdded+=delegate(object sender,ControlEventArgs e){visit(e.Control);};
            };
            visit(form);
            EventHandler changed=delegate {if(form.IsDisposed)return;form.SuspendLayout();try{foreach(Action update in updates)update();if(refresh!=null)refresh();}finally{form.ResumeLayout(true);}};
            LanguageChanged+=changed;form.Disposed+=delegate{LanguageChanged-=changed;};
        }
    }
    public partial class MainForm
    {
        ComboBox cmbUiLanguage;
        void BuildLanguageBar() {
            TableLayoutPanel bar=new TableLayoutPanel{Dock=DockStyle.Top,Height=54,Padding=new Padding(12,6,12,5),ColumnCount=3};
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,136));
            bar.Controls.Add(new Label{Text="SpireForge AI · 尖塔铸卡",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font(Font.FontFamily,13,FontStyle.Bold)},0,0);
            bar.Controls.Add(new Label{Text="语言 / Language",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,Padding=new Padding(0,0,8,0)},1,0);
            cmbUiLanguage=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,AccessibleName="Language / 语言",Margin=new Padding(0,5,0,0)};
            cmbUiLanguage.Items.AddRange(new object[]{"简体中文","English"});cmbUiLanguage.SelectedIndex=UiText.IsEnglish?1:0;
            cmbUiLanguage.SelectedIndexChanged+=delegate{try{UiText.SetLanguage(cmbUiLanguage.SelectedIndex==1?"en":"zh-CN");}catch(Exception error){MessageBox.Show(error.Message,UiText.T("保存失败:\n"),MessageBoxButtons.OK,MessageBoxIcon.Error);}};
            bar.Controls.Add(cmbUiLanguage,2,0);Controls.Add(bar);tabs.BringToFront();
        }
        void RefreshLocalizedPreviews() {
            ShowCustomCardPreview();ShowDeckSourcePreview();
            if(txtNativeDescription!=null)txtNativeDescription.Text=NativeCardPreview.Build(SelectedNativeCards().FirstOrDefault());
            List<CatalogCard> selected=SelectedNativeCards();
            if(selected.Count==1){lblCardName.Text=UiText.NativeCard(selected[0]).name+" · "+Professions.Name(selected[0].color);lblCardId.Text=UiText.NativeCard(selected[0]).name+" ["+selected[0].id+"]";}
            if(lstPoolSources!=null && lstPoolSources.SelectedItems.Count>0)ShowPoolIntroduction(lstPoolSources);
            else if(lstPoolAllowed!=null && lstPoolAllowed.SelectedItems.Count>0)ShowPoolIntroduction(lstPoolAllowed);
        }
    }
}

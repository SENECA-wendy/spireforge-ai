using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace StsCardEditor
{
    public class CustomCardEditorDialog : Form
    {
        readonly CustomCardModel original;
        CustomCardModel draft;CreativeBuildResult generated;
        readonly Func<CustomCardModel,string,CreativeBuildResult> revise;
        readonly Func<CustomCardModel,CreativeBuildResult,string,CardRevisionSave> save;
        readonly Func<CustomCardModel> previous;
        TabControl sections;DataGridView numbers;TextBox name,description,upgradeDescription,instructions,preview,before,report;
        ComboBox type,rarity,target,color;CheckBox exhaust,innate,ethereal,retain,enabled;
        Label status;Panel editor;FlowLayoutPanel actions;Button generate;bool loading,busy,dirty;string checkedSnapshot;
        public CardRevisionSave SaveResult {get;private set;}
        public bool SyncRequested {get;private set;}
        public CustomCardEditorDialog(CustomCardModel card,Func<CustomCardModel,string,CreativeBuildResult> revise,Func<CustomCardModel,CreativeBuildResult,string,CardRevisionSave> save,Func<CustomCardModel> previous)
        {
            this.original=CustomCardRevisionService.Clone(card);this.revise=revise;this.save=save;this.previous=previous;
            Text=UiText.T("编辑自定义卡牌 · ")+card.name;StartPosition=FormStartPosition.CenterParent;Size=new Size(1180,840);MinimumSize=new Size(980,720);Font=new Font("Microsoft YaHei UI",9F);AutoScaleMode=AutoScaleMode.Dpi;ShowInTaskbar=false;
            TableLayoutPanel root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(12)};root.RowStyles.Add(new RowStyle(SizeType.Absolute,52));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,60));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));Controls.Add(root);
            root.Controls.Add(new Label{Dock=DockStyle.Fill,Text=UiText.T("编辑已有卡牌，保存时保留ID：")+card.id+UiText.T("\r\n只改描述不会改变实际效果。手动数值无需API；AI修改会先生成草稿供你检查。"),ForeColor=Color.SteelBlue},0,0);
            editor=new Panel{Dock=DockStyle.Fill};root.Controls.Add(editor,0,1);
            TableLayoutPanel columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));editor.Controls.Add(columns);
            sections=new TabControl{Dock=DockStyle.Fill};columns.Controls.Add(sections,0,0);
            TabPage numericPage=new TabPage(UiText.T("手动数值"));sections.TabPages.Add(numericPage);
            TableLayoutPanel numericRoot=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(6)};numericRoot.RowStyles.Add(new RowStyle(SizeType.Absolute,68));numericRoot.RowStyles.Add(new RowStyle(SizeType.Percent,100));numericPage.Controls.Add(numericRoot);
            numericRoot.Controls.Add(new Label{Dock=DockStyle.Fill,Text=UiText.T("双击“值”进行修改。费用 -1=X费，-2=不可打出。\r\nJava代码写死且未暴露的数值，请通过AI再修改；普通伤害/格挡字段只在实际被代码使用时有效。")},0,0);
            numbers=new DataGridView{Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,SelectionMode=DataGridViewSelectionMode.CellSelect,MultiSelect=false};
            numbers.Columns.Add(new DataGridViewTextBoxColumn{Name="label",HeaderText=UiText.T("数值项目"),ReadOnly=true,Width=205});numbers.Columns.Add(new DataGridViewTextBoxColumn{Name="value",HeaderText=UiText.T("值"),Width=85});numbers.Columns.Add(new DataGridViewTextBoxColumn{Name="help",HeaderText=UiText.T("说明"),ReadOnly=true,AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill});numbers.DefaultCellStyle.WrapMode=DataGridViewTriState.True;numbers.DataError+=delegate(object s,DataGridViewDataErrorEventArgs e){e.ThrowException=false;};numbers.CellEndEdit+=delegate{Changed();};numericRoot.Controls.Add(numbers,0,1);
            TabPage metadata=new TabPage(UiText.T("描述与属性"));sections.TabPages.Add(metadata);
            TableLayoutPanel fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=9,Padding=new Padding(8)};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<6;i++)fields.RowStyles.Add(new RowStyle(SizeType.Absolute,i==5?58:36));fields.RowStyles.Add(new RowStyle(SizeType.Percent,50));fields.RowStyles.Add(new RowStyle(SizeType.Percent,50));fields.RowStyles.Add(new RowStyle(SizeType.Absolute,40));metadata.Controls.Add(fields);
            name=Field(fields,UiText.T("卡牌名称"),0);type=Choice(fields,UiText.T("类型"),1,new[]{"ATTACK","SKILL","POWER","STATUS","CURSE"});rarity=Choice(fields,UiText.T("稀有度"),2,new[]{"BASIC","COMMON","UNCOMMON","RARE","SPECIAL","CURSE"});target=Choice(fields,UiText.T("目标"),3,new[]{"SELF","ENEMY","ALL_ENEMY","NONE","SELF_AND_ENEMY","ALL"});color=Choice(fields,UiText.T("职业归属"),4,Professions.CustomColors);Professions.FormatColors(color);
            FlowLayoutPanel flags=new FlowLayoutPanel{Dock=DockStyle.Fill};exhaust=Flag(flags,UiText.T("消耗"));innate=Flag(flags,UiText.T("固有"));ethereal=Flag(flags,UiText.T("虚无"));retain=Flag(flags,UiText.T("保留"));enabled=Flag(flags,UiText.T("启用"));fields.Controls.Add(new Label{Text=UiText.T("真实属性"),AutoSize=true},0,5);fields.Controls.Add(flags,1,5);
            description=Field(fields,UiText.T("基础描述"),6,true);upgradeDescription=Field(fields,UiText.T("升级描述"),7,true);
            Button rebuild=Button(UiText.T("从内置效果重建描述"),delegate{try{CustomCardModel c=ReadDraft();if(c.effects.Any(e=>e.type=="JAVA_EXTENSION"))throw new InvalidOperationException(UiText.T("Java扩展描述请手动或通过AI调整，不能用内置效果覆盖。"));description.Text=LocalParser.BuildDescription(c,false);upgradeDescription.Text=LocalParser.BuildDescription(c,true);}catch(Exception e){Message(e.Message,true);}});fields.Controls.Add(rebuild,1,8);
            TabPage aiPage=new TabPage(UiText.T("AI再修改"));sections.TabPages.Add(aiPage);
            TableLayoutPanel ai=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(8)};ai.RowStyles.Add(new RowStyle(SizeType.Absolute,58));ai.RowStyles.Add(new RowStyle(SizeType.Absolute,130));ai.RowStyles.Add(new RowStyle(SizeType.Absolute,44));ai.RowStyles.Add(new RowStyle(SizeType.Percent,100));aiPage.Controls.Add(ai);
            ai.Controls.Add(new Label{Dock=DockStyle.Fill,Text=UiText.T("基于当前草稿和原卡源码修改，未要求变化的机制尽量保留。\r\n例如：生成数量改为8张；伤害改为10；保留升级规则。通常3次模型请求，修复会增加用量。")},0,0);
            instructions=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical,AccessibleName=UiText.T("AI修改要求")};ai.Controls.Add(instructions,0,1);generate=Button(UiText.T("让AI修改当前草稿"),delegate{Generate();});ai.Controls.Add(generate,0,2);report=CustomCardPreview.CreateTextBox(UiText.T("AI修改检查报告"));report.Text=UiText.T("AI结果只更新草稿；检查预览后再保存。未找到源码时会基于现有定义重建，结果需要仔细验证。");ai.Controls.Add(report,0,3);
            TabControl compare=new TabControl{Dock=DockStyle.Fill};columns.Controls.Add(compare,1,0);TabPage currentPage=new TabPage(UiText.T("当前草稿预览")),originalPage=new TabPage(UiText.T("保存前对照"));compare.TabPages.Add(currentPage);compare.TabPages.Add(originalPage);preview=CustomCardPreview.CreateTextBox(UiText.T("当前草稿预览"));before=CustomCardPreview.CreateTextBox(UiText.T("修改前卡牌"));currentPage.Padding=originalPage.Padding=new Padding(10);currentPage.Controls.Add(preview);originalPage.Controls.Add(before);before.Text=CustomCardPreview.Build(original);
            status=new Label{Dock=DockStyle.Fill,Padding=new Padding(4,8,0,0)};root.Controls.Add(status,0,2);actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};root.Controls.Add(actions,0,3);
            actions.Controls.Add(Button(UiText.T("保存到卡库"),delegate{Save(false);}));actions.Controls.Add(Button(UiText.T("保存并实时同步"),delegate{Save(true);}));actions.Controls.Add(Button(UiText.T("还原本次草稿"),delegate{LoadDraft(original,null);}));actions.Controls.Add(Button(UiText.T("载入上次保存前版本"),delegate{try{LoadDraft(previous(),null);Message(UiText.T("已载入历史版本为草稿，尚未保存或同步。"),false);}catch(Exception e){Message(e.Message,true);}}));
            Button cancel=Button(UiText.T("关闭"),delegate{Close();});actions.Controls.Add(cancel);CancelButton=cancel;
            FormClosing+=delegate(object s,FormClosingEventArgs e){if(busy){e.Cancel=true;Message(UiText.T("AI正在生成，请等待本次请求完成；尚未保存到卡库。"),true);}else if(dirty && DialogResult!=DialogResult.OK && MessageBox.Show(this,UiText.T("草稿尚未保存，关闭并放弃本次修改？"),UiText.T("未保存的修改"),MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)e.Cancel=true;};
            LoadDraft(card,null);
            UiText.Attach(this,delegate{Text=UiText.T("编辑自定义卡牌 · ")+original.name;RefreshPreview();});
        }
        Button Button(string text,Action action){Button b=new Button{Text=text,AutoSize=true,Height=34,MinimumSize=new Size(110,34),Margin=new Padding(4)};b.Click+=delegate{action();};return b;}
        TextBox Field(TableLayoutPanel panel,string label,int row,bool multiline=false){panel.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(0,8,0,0)},0,row);TextBox text=new TextBox{Dock=DockStyle.Fill,Multiline=multiline,ScrollBars=multiline?ScrollBars.Vertical:ScrollBars.None,AccessibleName=label};text.TextChanged+=delegate{Changed();};panel.Controls.Add(text,1,row);return text;}
        ComboBox Choice(TableLayoutPanel panel,string label,int row,string[] items){panel.Controls.Add(new Label{Text=label,AutoSize=true,Margin=new Padding(0,8,0,0)},0,row);ComboBox box=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName=label};box.Items.AddRange(items);box.SelectedIndexChanged+=delegate{Changed();};panel.Controls.Add(box,1,row);return box;}
        CheckBox Flag(FlowLayoutPanel panel,string label){CheckBox box=new CheckBox{Text=label,AutoSize=true,Margin=new Padding(3,7,7,0)};box.CheckedChanged+=delegate{Changed();};panel.Controls.Add(box);return box;}
        void Message(string message,bool error){status.Text=message;status.ForeColor=error?Color.Firebrick:Color.SteelBlue;}
        void LoadDraft(CustomCardModel card,CreativeBuildResult build)
        {
            loading=true;draft=CustomCardRevisionService.Clone(card);generated=build;name.Text=card.name;description.Text=card.description;upgradeDescription.Text=card.upgradeDescription;type.SelectedItem=card.type;rarity.SelectedItem=card.rarity;target.SelectedItem=card.target;color.SelectedItem=card.color;exhaust.Checked=card.exhaust;innate.Checked=card.innate;ethereal.Checked=card.ethereal;retain.Checked=card.retain;enabled.Checked=card.enabled;
            numbers.Rows.Clear();foreach(CardNumberField field in CustomCardRevisionService.Numbers(draft)){int i=numbers.Rows.Add(field.Label,field.Text,field.Help);numbers.Rows[i].Tag=field;}
            loading=false;dirty=JsonUtil.ToJson(card)!=JsonUtil.ToJson(original);checkedSnapshot=build==null?null:JsonUtil.ToJson(ReadDraft());report.Text=build==null?UiText.T("手动修改仅执行字段校验；尚未验证实际机制。"):build.ValidationReport;RefreshPreview();Message(build==null?UiText.T("正在编辑草稿。保存到卡库后，点击实时同步可更新本局已有同ID卡牌。"):UiText.T("AI修改已生成草稿。请检查对照和报告，再决定是否保存。"),false);
        }
        public CustomCardModel ReadDraft()
        {
            numbers.EndEdit();List<CardNumberField> fields=new List<CardNumberField>();
            foreach(DataGridViewRow row in numbers.Rows){CardNumberField field=(CardNumberField)row.Tag;field.Text=Convert.ToString(row.Cells["value"].Value);fields.Add(field);}
            CustomCardModel c=CustomCardRevisionService.ApplyNumbers(draft,fields);c.name=name.Text.Trim();c.description=description.Text;c.upgradeDescription=upgradeDescription.Text;c.type=Convert.ToString(type.SelectedItem);c.rarity=Convert.ToString(rarity.SelectedItem);c.target=Convert.ToString(target.SelectedItem);c.color=Convert.ToString(color.SelectedItem);c.exhaust=exhaust.Checked;c.innate=innate.Checked;c.ethereal=ethereal.Checked;c.retain=retain.Checked;c.enabled=enabled.Checked;return c;
        }
        void Changed(){if(loading)return;dirty=true;Message(UiText.T("草稿已修改。请核对描述与效果参数；尚未保存或同步。"),false);RefreshPreview();}
        void RefreshPreview(){try{CustomCardModel card=ReadDraft();preview.Text=CustomCardPreview.Build(card);foreach(DataGridViewRow row in numbers.Rows)row.Cells["value"].ErrorText="";if(generated!=null)report.Text=generated.ValidationReport+(JsonUtil.ToJson(card)==checkedSnapshot?"":UiText.T("\r\n\r\n之后进行了手动修改，以上检查不覆盖这些改动。"));}catch(Exception e){foreach(DataGridViewRow row in numbers.Rows){CardNumberField field=(CardNumberField)row.Tag;if(e.Message.StartsWith(field.Label))row.Cells["value"].ErrorText=e.Message;}Message(e.Message,true);}}
        void Generate()
        {
            if(busy)return;CustomCardModel current;
            try{current=ReadDraft();CustomCardRevisionService.Validate(current);if(string.IsNullOrWhiteSpace(instructions.Text))throw new InvalidOperationException(UiText.T("请先填写修改要求。"));}catch(Exception e){Message(e.Message,true);return;}
            string changes=instructions.Text;busy=true;editor.Enabled=false;actions.Enabled=false;Message(UiText.T("AI处理中：读取原卡 → 规则整理 → 实现与行为检查 → 独立审核。尚未修改卡库。"),false);
            BackgroundWorker worker=new BackgroundWorker();worker.DoWork+=delegate(object s,DoWorkEventArgs e){e.Result=revise(current,changes);};worker.RunWorkerCompleted+=delegate(object s,RunWorkerCompletedEventArgs e){busy=false;editor.Enabled=true;actions.Enabled=true;if(e.Error!=null){Message(UiText.T("AI修改失败，原草稿保留：")+e.Error.Message,true);report.Text=e.Error.ToString();}else LoadDraft(((CreativeBuildResult)e.Result).Card,(CreativeBuildResult)e.Result);worker.Dispose();};worker.RunWorkerAsync();
        }
        void Save(bool sync)
        {
            try{CustomCardModel card=ReadDraft();CustomCardRevisionService.Validate(card);if(sync&&!card.enabled)throw new InvalidOperationException(UiText.T("请先勾选“启用”，或只保存到卡库。"));
                string validation=generated!=null&&JsonUtil.ToJson(card)==checkedSnapshot?generated.ValidationReport:UiText.T("手动修改；仅字段校验通过，机制和描述一致性仍需实战验证。");
                if((generated==null||JsonUtil.ToJson(card)!=checkedSnapshot)&&!(card.notes??"").StartsWith(UiText.T("手动修改，实际效果待验证。")))card.notes=UiText.T("手动修改，实际效果待验证。")+(card.notes??"");
                SaveResult=save(card,generated,validation);SyncRequested=sync;dirty=false;DialogResult=DialogResult.OK;Close();
            }catch(Exception e){Message(UiText.T("未保存：")+e.Message,true);}
        }
    }
}

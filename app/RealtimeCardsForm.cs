using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StsCardEditor
{
    public partial class MainForm
    {
        private TabPage cardEditorPage, advancedLivePage;
        private TabControl cardEditorSections;
        private TextBox txtNativeDescription, txtNativeUpgradeCost;
        private CheckBox chkNativePersist;
        private bool nativeBusy, nativeSelectionLoading;
        private Dictionary<string,CardOverrideModel> nativeSessionOverrides;
        private ErrorProvider nativeErrors;

        private static void MultiSelect(ListBox list)
        {
            list.SelectionMode=SelectionMode.MultiExtended;
            list.KeyDown += delegate(object sender,KeyEventArgs e) { if(e.Control && e.KeyCode==Keys.A) { list.BeginUpdate(); for(int i=0;i<list.Items.Count;i++) list.SetSelected(i,true); list.EndUpdate(); e.Handled=true; } };
        }

        private void BuildRealtimeCardsTab()
        {
            nativeSessionOverrides=JsonUtil.FromJson<Dictionary<string,CardOverrideModel>>(JsonUtil.ToJson(profile.overrides));
            nativeErrors=new ErrorProvider(this){BlinkStyle=ErrorBlinkStyle.NeverBlink};Disposed+=delegate{nativeErrors.Dispose();};
            cardEditorPage=NewTab(UiText.T("卡牌编辑 · 实时")); cardEditorSections=new TabControl{Dock=DockStyle.Fill}; cardEditorPage.Controls.Add(cardEditorSections);
            TabPage page=new TabPage(UiText.T("四职业数值 · 多选批改")); cardEditorSections.TabPages.Add(page);
            TableLayoutPanel root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(8)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,86)); page.Controls.Add(root);
            FlowLayoutPanel header=DeckFlow();
            header.Controls.Add(DeckButton(UiText.T("连接 / 读取本局数值"),delegate { ReadNativeSnapshot(); }));
            header.Controls.Add(DeckButton(UiText.T("实时应用到选中卡牌"),delegate { ApplyNativeCards(false); }));
            header.Controls.Add(DeckButton(UiText.T("实时恢复原版数值"),delegate { ApplyNativeCards(true); }));
            chkNativePersist=new CheckBox{Text=UiText.T("重启游戏后也保留"),AutoSize=true,Margin=new Padding(12,9,0,0)}; header.Controls.Add(chkNativePersist); root.Controls.Add(header,0,0);
            TableLayoutPanel columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1}; columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,32)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,68)); root.Controls.Add(columns,0,1);
            TableLayoutPanel left=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4}; left.RowStyles.Add(new RowStyle(SizeType.Absolute,42));left.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); left.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); left.RowStyles.Add(new RowStyle(SizeType.Percent,100)); columns.Controls.Add(left,0,0);
            FlowLayoutPanel profession=DeckFlow();profession.Controls.Add(new Label{Text=UiText.T("职业"),AutoSize=true,Margin=new Padding(2,9,4,0)});cmbNativeProfession=Professions.Filter(false);cmbNativeProfession.SelectedIndexChanged+=delegate{RefreshNativeList();};profession.Controls.Add(cmbNativeProfession);left.Controls.Add(profession,0,0);
            FlowLayoutPanel search=DeckFlow(); search.Controls.Add(new Label{Text=UiText.T("搜索"),AutoSize=true,Margin=new Padding(2,9,4,0)}); txtFilter=new TextBox{Width=210}; txtFilter.TextChanged+=delegate{RefreshCardList();}; search.Controls.Add(txtFilter); left.Controls.Add(search,0,1);
            FlowLayoutPanel filter=DeckFlow(); cmbType=new ComboBox{Width=100,DropDownStyle=ComboBoxStyle.DropDownList}; cmbType.Items.AddRange(new object[]{"全部",UiText.T("攻击"),UiText.T("技能"),UiText.T("能力")}); cmbType.SelectedIndex=0; cmbType.SelectedIndexChanged+=delegate{RefreshCardList();}; filter.Controls.Add(cmbType);
            filter.Controls.Add(DeckButton(UiText.T("全选"),delegate{nativeSelectionLoading=true;for(int i=0;i<lstCards.Items.Count;i++)lstCards.SetSelected(i,true);nativeSelectionLoading=false;ShowSelectedCard();}));
            filter.Controls.Add(DeckButton(UiText.T("清除选择"),delegate{lstCards.ClearSelected();})); left.Controls.Add(filter,0,2);
            lstCards=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false,HorizontalScrollbar=true}; MultiSelect(lstCards); lstCards.SelectedIndexChanged+=delegate{if(!nativeSelectionLoading)ShowSelectedCard();}; left.Controls.Add(lstCards,0,3);
            TableLayoutPanel right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(10,0,0,0)};
            right.RowStyles.Add(new RowStyle(SizeType.Absolute,62)); right.RowStyles.Add(new RowStyle(SizeType.Absolute,150)); right.RowStyles.Add(new RowStyle(SizeType.Absolute,56)); right.RowStyles.Add(new RowStyle(SizeType.Percent,100)); columns.Controls.Add(right,1,0);
            Panel title=new Panel{Dock=DockStyle.Fill}; lblCardName=new Label{Dock=DockStyle.Top,Height=27,Font=new Font(Font,FontStyle.Bold)}; lblCardId=new Label{Dock=DockStyle.Bottom,Height=30}; title.Controls.Add(lblCardName); title.Controls.Add(lblCardId); right.Controls.Add(title,0,0);
            TableLayoutPanel fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=5};
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,24));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,26));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,24));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,26));
            fields.Controls.Add(new Label{Text=UiText.T("基础数值"),AutoSize=true},0,0); fields.Controls.Add(new Label{Text=UiText.T("升级后数值"),AutoSize=true},2,0);
            txtCost=NativeField(fields,UiText.T("费用"),0,1,-2,99); txtNativeUpgradeCost=NativeField(fields,UiText.T("升级后费用"),2,1,-2,99);
            txtDamage=NativeField(fields,UiText.T("伤害"),0,2,0,999);txtUpDamage=NativeField(fields,UiText.T("升级后伤害"),2,2,0,999);
            txtBlock=NativeField(fields,UiText.T("格挡"),0,3,0,999);txtUpBlock=NativeField(fields,UiText.T("升级后格挡"),2,3,0,999);
            txtMagic=NativeField(fields,UiText.T("附加数值"),0,4,0,999);txtUpMagic=NativeField(fields,UiText.T("升级后附加值"),2,4,0,999); right.Controls.Add(fields,0,1);
            lblCardSpecial=new Label{Dock=DockStyle.Fill,ForeColor=Color.DarkOrange};right.Controls.Add(lblCardSpecial,0,2);
            GroupBox preview=new GroupBox{Text=UiText.T("原版卡牌介绍与升级效果"),Dock=DockStyle.Fill,Padding=new Padding(10)};txtNativeDescription=CustomCardPreview.CreateTextBox(UiText.T("四职业原生卡牌介绍"));preview.Controls.Add(txtNativeDescription);right.Controls.Add(preview,0,3);
            lblCardBase=new Label(); // Raw reference values are presented in the readable preview.
            lblCardSaveState=new Label{Dock=DockStyle.Fill,Padding=new Padding(5),ForeColor=Color.SteelBlue,Text=UiText.T("地图或可操作战斗回合可实时修改本局主牌组、战斗副本和后续同名新牌。\r\nCtrl / Shift 多选，Ctrl+A 全选。留空不改；修改基础值而升级值留空时，沿用各牌原版升级增量。")};root.Controls.Add(lblCardSaveState,0,2);
        }

        private TextBox NativeField(TableLayoutPanel panel,string name,int col,int row,int min,int max)
        {
            panel.Controls.Add(new Label{Text=name,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},col,row);
            TextBox text=new TextBox{Dock=DockStyle.Fill,Margin=new Padding(4)};panel.Controls.Add(text,col+1,row);
            text.Validating+=delegate{string error;ParseNullable(text.Text,name,min,max,out error);nativeErrors.SetError(text,error??"");};text.TextChanged+=delegate{nativeErrors.SetError(text,"");};return text;
        }

        private List<CatalogCard> SelectedNativeCards() { return lstCards.SelectedItems.Cast<CardListItem>().Select(i=>i.Card).ToList(); }
        private void RefreshNativeList()
        {
            if(lstCards==null || catalog==null) return;
            HashSet<string> selected=new HashSet<string>(SelectedNativeCards().Select(c=>c.id));string search=txtFilter.Text.Trim();string type=new[]{"全部","ATTACK","SKILL","POWER"}[Math.Max(0,cmbType.SelectedIndex)];
            nativeSelectionLoading=true;lstCards.BeginUpdate();lstCards.Items.Clear();
            foreach(CatalogCard card in catalog.cards.OrderBy(c=>c.name)) {
                if(!Professions.NativeColors.Contains(card.color) || !Professions.Matches(cmbNativeProfession,card.color,false,detectedProfession) || (type!="全部" && card.type!=type))continue;
                if(search.Length>0 && (card.name+" "+card.id+" "+card.description+" "+UiText.NativeCard(card).name+" "+UiText.NativeCard(card).description).IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
                int index=lstCards.Items.Add(new CardListItem(card));if(selected.Contains(card.id))lstCards.SetSelected(index,true);
            }
            if(lstCards.SelectedItems.Count==0 && lstCards.Items.Count>0)lstCards.SelectedIndex=0;
            lstCards.EndUpdate();nativeSelectionLoading=false;ShowNativeSelection();
        }
        private void ShowNativeSelection()
        {
            List<CatalogCard> cards=SelectedNativeCards();
            foreach(TextBox text in new[]{txtCost,txtNativeUpgradeCost,txtDamage,txtUpDamage,txtBlock,txtUpBlock,txtMagic,txtUpMagic})text.Text="";
            lblCardName.Text=cards.Count==0?UiText.T("未选择卡牌"):cards.Count==1?UiText.NativeCard(cards[0]).name+" · "+Professions.Name(cards[0].color):UiText.T("已选择 ")+cards.Count+UiText.T(" 种卡牌");
            lblCardId.Text=string.Join("、",cards.Take(6).Select(c=>UiText.NativeCard(c).name+" ["+c.id+"]").ToArray())+(cards.Count>6?"…":"");
            txtNativeDescription.Text=NativeCardPreview.Build(cards.FirstOrDefault());
            lblCardSpecial.Text=cards.Count>1?UiText.T("多选批改：非空字段应用到每种所选卡牌；升级增量分别按原版计算。"):cards.Count==1?(NativeCardPreview.MechanismNote(cards[0])??UiText.T("附加数值对应卡牌使用的层数、倍率或抽牌数等，具体见介绍。")):"";
            if(cards.Count==1){CardOverrideModel values;if(nativeSessionOverrides.TryGetValue(cards[0].id,out values)){txtCost.Text=FmtEdit(values.cost);txtNativeUpgradeCost.Text=FmtEdit(values.upgradeCost);txtDamage.Text=FmtEdit(values.damage);txtUpDamage.Text=FmtEdit(values.upgradeDamage);txtBlock.Text=FmtEdit(values.block);txtUpBlock.Text=FmtEdit(values.upgradeBlock);txtMagic.Text=FmtEdit(values.magicNumber);txtUpMagic.Text=FmtEdit(values.upgradeMagicNumber);}}
        }
        private CardOverrideModel ReadNativeValues()
        {
            CardOverrideModel value=new CardOverrideModel();string error;
            value.cost=ParseNullable(txtCost.Text,UiText.T("费用"),-2,99,out error);if(error!=null)throw new InvalidOperationException(error);
            value.upgradeCost=ParseNullable(txtNativeUpgradeCost.Text,UiText.T("升级后费用"),-2,99,out error);if(error!=null)throw new InvalidOperationException(error);
            value.damage=ParseNullable(txtDamage.Text,UiText.T("伤害"),0,999,out error);if(error!=null)throw new InvalidOperationException(error);
            value.upgradeDamage=ParseNullable(txtUpDamage.Text,UiText.T("升级后伤害"),0,999,out error);if(error!=null)throw new InvalidOperationException(error);
            value.block=ParseNullable(txtBlock.Text,UiText.T("格挡"),0,999,out error);if(error!=null)throw new InvalidOperationException(error);
            value.upgradeBlock=ParseNullable(txtUpBlock.Text,UiText.T("升级后格挡"),0,999,out error);if(error!=null)throw new InvalidOperationException(error);
            value.magicNumber=ParseNullable(txtMagic.Text,UiText.T("附加数值"),0,999,out error);if(error!=null)throw new InvalidOperationException(error);
            value.upgradeMagicNumber=ParseNullable(txtUpMagic.Text,UiText.T("升级后附加值"),0,999,out error);if(error!=null)throw new InvalidOperationException(error);
            if(!value.cost.HasValue&&!value.upgradeCost.HasValue&&!value.damage.HasValue&&!value.upgradeDamage.HasValue&&!value.block.HasValue&&!value.upgradeBlock.HasValue&&!value.magicNumber.HasValue&&!value.upgradeMagicNumber.HasValue)throw new InvalidOperationException(UiText.T("请至少填写一个需要实时修改的字段。"));return value;
        }
        private void NativeWorker(Func<LiveClient,object> work,Action<object> completed)
        {
            if(nativeBusy)return;if(string.IsNullOrEmpty(gameDir)){lblCardSaveState.Text=UiText.T("请先在设置中选择游戏目录。");return;}
            string directory=gameDir;nativeBusy=true;tabs.Enabled=false;lblCardSaveState.Text=UiText.T("正在等待游戏确认…");
            BackgroundWorker worker=new BackgroundWorker();worker.DoWork+=delegate(object sender,DoWorkEventArgs e){using(LiveClient client=new LiveClient()){client.Connect(directory);e.Result=work(client);}};
            worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e){nativeBusy=false;tabs.Enabled=true;try{if(e.Error!=null)throw e.Error;completed(e.Result);}catch(Exception error){lblCardSaveState.Text=UiText.T("未完成：")+error.Message;lblCardSaveState.ForeColor=Color.Firebrick;}worker.Dispose();};worker.RunWorkerAsync();
        }
        private void ApplyNativeCards(bool reset)
        {
            List<NativeEditPlan> plans;try{plans=NativeLiveEditService.Plan(SelectedNativeCards(),reset?null:ReadNativeValues(),nativeSessionOverrides,reset);}catch(Exception error){lblCardSaveState.Text=error.Message;lblCardSaveState.ForeColor=Color.Firebrick;return;}
            bool persist=chkNativePersist.Checked;
            NativeWorker(client=>NativeLiveEditService.Apply(client,plans),delegate(object raw){NativeEditResult result=(NativeEditResult)raw;
                foreach(NativeEditPlan plan in result.Applied)nativeSessionOverrides[plan.Id]=plan.Values;
                string note=UiText.T("游戏已确认 ")+result.Applied.Count+" / "+plans.Count+UiText.T(" 种卡牌，涉及 ")+result.Instances+UiText.T(" 个现有实例。");
                if(persist && result.Applied.Count>0){try{PersistNativeEdits(result.Applied);note+=UiText.T("已保存重启后的定义。");}catch(Exception error){result.Error=UiText.T("实时已生效，但保存失败：")+error.Message;}}
                if(result.Error!=null)note+=UiText.T("\r\n未完全完成：")+result.Error;else note+=UiText.T("无需重启。");
                lblCardSaveState.Text=note;lblCardSaveState.ForeColor=result.Error==null?Color.DarkGreen:Color.DarkOrange;
            });
        }
        private void PersistNativeEdits(List<NativeEditPlan> plans)
        {
            string path=AppPaths.ContentPath(gameDir);ContentDoc content=File.Exists(path)?JsonUtil.FromJson<ContentDoc>(JsonUtil.ReadUtf8(path)):new ContentDoc();
            if(content==null || content.schemaVersion>2)throw new InvalidDataException(UiText.T("现有定义文件不可覆盖。"));
            if(content.overrides==null)content.overrides=new Dictionary<string,CardOverrideModel>();foreach(NativeEditPlan plan in plans)content.overrides[plan.Id]=plan.Values;
            string pending=path+".pending-"+Guid.NewGuid().ToString("N");JsonUtil.WriteUtf8(pending,JsonUtil.ToJson(content));
            if(File.Exists(path))File.Replace(pending,path,path+".before-native-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".json");else File.Move(pending,path);
            foreach(NativeEditPlan plan in plans)profile.overrides[plan.Id]=plan.Values;ProfileStore.SaveProfile(profile);
        }
        private void ReadNativeSnapshot()
        {
            NativeWorker(client=>client.Snapshot(),delegate(object raw){LiveResponse snapshot=(LiveResponse)raw;if(!snapshot.Ok || !DBool(snapshot.Data,"inRun"))throw new InvalidOperationException(UiText.T("请先进入本局。"));DetectProfession(snapshot.Data);PopulateLiveTree(snapshot.Data);
                List<CatalogCard> selected=SelectedNativeCards();HashSet<string> ids=new HashSet<string>(selected.Select(c=>c.id));var groups=DObject(snapshot.Data,"groups");List<string> lines=new List<string>();List<Dictionary<string,object>> owned=new List<Dictionary<string,object>>();
                foreach(object item in DList(groups,"masterDeck")){var card=item as Dictionary<string,object>;if(card!=null && ids.Contains(DStr(card,"cardId"))){owned.Add(card);lines.Add(DStr(card,"name")+UiText.T("：费用 ")+DStr(card,"cost")+UiText.T("，伤害 ")+DStr(card,"baseDamage")+UiText.T("，格挡 ")+DStr(card,"baseBlock")+UiText.T("，附加值 ")+DStr(card,"baseMagicNumber"));}}
                if(selected.Count==1 && owned.Count>0){CatalogCard original=selected[0];var normal=owned.FirstOrDefault(c=>!DBool(c,"upgraded"));var upgraded=owned.FirstOrDefault(c=>DBool(c,"upgraded"));
                    txtCost.Text=normal==null?"":DStr(normal,"cost");txtDamage.Text=normal==null||!original.damage.HasValue?"":DStr(normal,"baseDamage");txtBlock.Text=normal==null||!original.block.HasValue?"":DStr(normal,"baseBlock");txtMagic.Text=normal==null||!original.magicNumber.HasValue?"":DStr(normal,"baseMagicNumber");
                    txtNativeUpgradeCost.Text=upgraded==null?"":DStr(upgraded,"cost");txtUpDamage.Text=upgraded==null||!original.damage.HasValue?"":DStr(upgraded,"baseDamage");txtUpBlock.Text=upgraded==null||!original.block.HasValue?"":DStr(upgraded,"baseBlock");txtUpMagic.Text=upgraded==null||!original.magicNumber.HasValue?"":DStr(upgraded,"baseMagicNumber");}
                lblCardSaveState.Text=UiText.T("已读取本局主牌组。")+(lines.Count==0?UiText.T("当前主牌组没有所选卡，可修改后续同名新牌定义。"):string.Join("；",lines.Take(4).ToArray()));lblCardSaveState.ForeColor=Color.DarkGreen;
            });
        }
        private void OpenAdvancedLiveEditor(){tabs.SelectedTab=cardEditorPage;cardEditorSections.SelectedTab=advancedLivePage;liveSubTabs.SelectedIndex=1;}
    }
}

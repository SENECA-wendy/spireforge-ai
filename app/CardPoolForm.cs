using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace StsCardEditor
{
    public partial class MainForm
    {
        private ListBox lstPoolSources, lstPoolAllowed;
        private TextBox txtPoolSearch;
        private TextBox txtPoolPreview;
        private Label lblPoolState, lblPoolCount;
        private readonly Dictionary<string,DeckSource> poolSources = new Dictionary<string,DeckSource>();
        private readonly HashSet<string> poolDraft = new HashSet<string>();
        private string poolRunKey;
        private bool poolBusy;

        private void BuildCardPoolTab()
        {
            TabPage page = NewTab(UiText.T("本局可获得牌池"));
            TableLayoutPanel root = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=5 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,86));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,70)); page.Controls.Add(root);
            FlowLayoutPanel header = DeckFlow();
            header.Controls.Add(DeckButton(UiText.T("连接 / 读取本局牌池"), delegate { ReadRunCardPool(); }));
            header.Controls.Add(DeckButton(UiText.T("同步自定义卡"), delegate { SyncDefinitionsToGame(); }));
            header.Controls.Add(DeckButton(UiText.T("应用此牌池到本局"), delegate { ApplyRunCardPool(true); }));
            header.Controls.Add(DeckButton(UiText.T("恢复本局原版牌池"), delegate { ApplyRunCardPool(false); }));
            header.Controls.Add(new Label { AutoSize=true, Text=UiText.T("控制之后生成的战斗奖励与商店卡牌（含无色牌位）；少量牌种可重复出现。"), Margin=new Padding(10,10,0,0) });
            root.Controls.Add(header,0,0);
            FlowLayoutPanel search = DeckFlow(); search.Controls.Add(new Label { Text=UiText.T("找卡"),AutoSize=true,Margin=new Padding(2,8,4,0) });
            txtPoolSearch = new TextBox { Width=300 }; txtPoolSearch.TextChanged += delegate { RenderPoolDraft(); }; search.Controls.Add(txtPoolSearch);
            cmbPoolProfession=Professions.Filter(true);cmbPoolProfession.SelectedIndexChanged+=delegate{RenderPoolDraft();};search.Controls.Add(cmbPoolProfession);
            lblPoolCount = new Label { AutoSize=true,Margin=new Padding(14,8,0,0) }; search.Controls.Add(lblPoolCount); root.Controls.Add(search,0,1);
            TableLayoutPanel columns = new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1 };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,34)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,32)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,34));
            GroupBox source = new GroupBox { Text=UiText.T("可选择的卡牌（Ctrl / Shift 多选）"),Dock=DockStyle.Fill,Padding=new Padding(8) };
            GroupBox selected = new GroupBox { Text=UiText.T("本局允许出现的牌种（待应用）"),Dock=DockStyle.Fill,Padding=new Padding(8) };
            lstPoolSources = new ListBox { Dock=DockStyle.Fill,SelectionMode=SelectionMode.MultiExtended,HorizontalScrollbar=true,IntegralHeight=false };
            lstPoolAllowed = new ListBox { Dock=DockStyle.Fill,SelectionMode=SelectionMode.MultiExtended,HorizontalScrollbar=true,IntegralHeight=false };
            MultiSelect(lstPoolSources);MultiSelect(lstPoolAllowed);
            lstPoolSources.SelectedIndexChanged+=delegate{ShowPoolIntroduction(lstPoolSources);};lstPoolAllowed.SelectedIndexChanged+=delegate{ShowPoolIntroduction(lstPoolAllowed);};
            source.Controls.Add(lstPoolSources); selected.Controls.Add(lstPoolAllowed); columns.Controls.Add(source,0,0); columns.Controls.Add(selected,1,0); root.Controls.Add(columns,0,2);
            GroupBox preview=new GroupBox{Text=UiText.T("所选卡牌介绍 / 升级效果"),Dock=DockStyle.Fill,Padding=new Padding(10)};txtPoolPreview=CustomCardPreview.CreateTextBox(UiText.T("牌池卡牌介绍"));preview.Controls.Add(txtPoolPreview);columns.Controls.Add(preview,2,0);
            FlowLayoutPanel actions = DeckFlow();
            actions.Controls.Add(DeckButton(UiText.T("加入选中牌种 →"), delegate { foreach (DeckSource item in lstPoolSources.SelectedItems) poolDraft.Add(item.Id); PoolDraftChanged(); }));
            actions.Controls.Add(DeckButton(UiText.T("移除选中牌种"), delegate { foreach (DeckSource item in lstPoolAllowed.SelectedItems) poolDraft.Remove(item.Id); PoolDraftChanged(); }));
            actions.Controls.Add(DeckButton(UiText.T("加入当前筛选全部"), delegate { AddVisiblePoolSources(); }));
            actions.Controls.Add(DeckButton(UiText.T("清空待选列表"), delegate { poolDraft.Clear(); PoolDraftChanged(); }));
            actions.Controls.Add(DeckButton(UiText.T("只留所选职业打击"), delegate { SetStrikeOnlyPool(); }));
            actions.Controls.Add(new Label { Text=UiText.T("筛选不删除允许列表，可混合四职业牌。只留一种打击时，奖励和商店仅出现该牌。自定义卡请先同步。"),AutoSize=true,Margin=new Padding(8,8,0,0) });
            root.Controls.Add(actions,0,3);
            lblPoolState = new Label { Dock=DockStyle.Fill,Text=UiText.T("先读取本局牌池，再选择允许出现的卡牌并应用。地图上可以操作。"),Padding=new Padding(6),ForeColor=Color.SteelBlue };
            root.Controls.Add(lblPoolState,0,4);
            if (catalog != null) foreach (CatalogCard card in catalog.cards) poolSources[card.id] = new DeckSource(card.id,card.name,false);
            RenderPoolDraft();
        }

        private void SetStrikeOnlyPool() { string color=Professions.Resolve(cmbPoolProfession,detectedProfession);if(!Professions.NativeColors.Contains(color))color=detectedProfession;poolDraft.Clear(); poolDraft.Add(Professions.Strike(color)); PoolDraftChanged(); }
        private void AddVisiblePoolSources(){foreach(DeckSource source in lstPoolSources.Items)poolDraft.Add(source.Id);PoolDraftChanged();}
        private void PoolDraftChanged() { RenderPoolDraft(); lblPoolState.Text=UiText.T("待应用：")+poolDraft.Count+UiText.T(" 种卡牌。点击“应用此牌池到本局”后生效。"); lblPoolState.ForeColor=Color.DarkOrange; }
        private void RenderPoolDraft()
        {
            if (lstPoolSources == null) return;
            string search = txtPoolSearch.Text.Trim(); lstPoolSources.BeginUpdate(); lstPoolAllowed.BeginUpdate();
            lstPoolSources.Items.Clear(); lstPoolAllowed.Items.Clear();
            foreach (DeckSource source in poolSources.Values.OrderBy(s=>s.Name).ThenBy(s=>s.Id))
                if (Professions.Matches(cmbPoolProfession,SourceProfession(source.Id),source.Custom,detectedProfession) && (search.Length==0 || source.Id.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0 || source.Name.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0 || (!source.Custom && UiText.NativeName(source.Id,source.Name).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0))) lstPoolSources.Items.Add(source);
            foreach (string id in poolDraft.OrderBy(i=>i)) { DeckSource source; lstPoolAllowed.Items.Add(poolSources.TryGetValue(id,out source) ? source : new DeckSource(id,id,false)); }
            lstPoolSources.EndUpdate(); lstPoolAllowed.EndUpdate(); lblPoolCount.Text=UiText.T("当前候选 ")+lstPoolSources.Items.Count+" / "+poolSources.Count+UiText.T(" 种 | 允许 ")+poolDraft.Count+UiText.T(" 种");
        }

        private void PoolWorker(Func<LiveClient,object> work, Action<object> completed)
        {
            if (poolBusy) return;
            if (string.IsNullOrEmpty(gameDir)) { lblPoolState.Text=UiText.T("请先设置游戏目录。"); return; }
            poolBusy=true; tabs.Enabled=false; lblPoolState.Text=UiText.T("正在与本局游戏同步…");
            string directory=gameDir; BackgroundWorker worker=new BackgroundWorker();
            worker.DoWork += delegate(object sender,DoWorkEventArgs e) { using (LiveClient client=new LiveClient()) { client.Connect(directory); e.Result=work(client); } };
            worker.RunWorkerCompleted += delegate(object sender,RunWorkerCompletedEventArgs e) {
                poolBusy=false; tabs.Enabled=true;
                try { if(e.Error!=null) throw e.Error; completed(e.Result); }
                catch(Exception error) { lblPoolState.Text=error.Message; lblPoolState.ForeColor=Color.Firebrick; }
                worker.Dispose();
            }; worker.RunWorkerAsync();
        }

        private void ReadRunCardPool()
        {
            PoolWorker(delegate(LiveClient client) {
                LiveResponse caps=client.Capabilities(); if(!caps.Ok || !DBool(caps.Data,"runCardPool")) throw new InvalidOperationException(UiText.T("游戏需要 v1.4 适配层，请更新后重启游戏。"));
                LiveResponse status=client.Status(); if(!status.Ok || !DBool(status.Data,"inRun")) throw new InvalidOperationException(UiText.T("请先进入一局游戏，地图选路线时即可编辑。"));
                LiveResponse policy=client.CardPool(), cards=client.CardCatalog(); if(!policy.Ok || !cards.Ok) throw new InvalidOperationException(policy.Error ?? cards.Error);
                return new LiveResponse[] { policy,cards };
            }, delegate(object value) {
                LiveResponse[] results=(LiveResponse[])value; poolRunKey=DStr(results[0].Data,"runKey");DetectProfession(results[0].Data);poolSources.Clear();
                UpdatePoolSources(DList(results[1].Data,"cards"));
                poolDraft.Clear(); foreach(object id in DList(results[0].Data,"cardIds")) poolDraft.Add(Convert.ToString(id)); RenderPoolDraft();
                lblPoolState.Text=DBool(results[0].Data,"enabled") ? UiText.T("本局限制已启用：")+poolDraft.Count+UiText.T(" 种卡牌。") : UiText.T("本局正在使用原版牌池。可用“只留所选职业打击”快速设置。");
                lblPoolState.ForeColor=Color.DarkGreen;
            });
        }

        private void UpdatePoolSources(IList cards)
        {
            poolSources.Clear();
            foreach(object raw in cards) {
                Dictionary<string,object> card=raw as Dictionary<string,object>; if(card==null || !DBool(card,"obtainable")) continue;
                string id=DStr(card,"cardId"); bool custom=DBool(card,"isCustom");
                if(!custom && (catalog==null || !catalog.cards.Any(c=>c.id==id && Professions.NativeColors.Contains(c.color))))continue;
                if(custom && !profile.customCards.Exists(c=>c.id==id && c.enabled)) continue;
                poolSources[id]=new DeckSource(id,DStr(card,"name"),custom);
            }
            RenderPoolDraft();
        }

        private void ShowPoolIntroduction(ListBox list)
        {
            if(txtPoolPreview==null)return;DeckSource source=list.SelectedItem as DeckSource;
            if(source==null){if(lstPoolSources.SelectedItems.Count==0 && lstPoolAllowed.SelectedItems.Count==0)txtPoolPreview.Text=UiText.T("选择卡牌，查看基础介绍和升级效果。");return;}
            txtPoolPreview.Text=(list.SelectedItems.Count>1?UiText.T("已选择 ")+list.SelectedItems.Count+UiText.T(" 种卡牌，以下为首张介绍。\r\n\r\n"):"")+CardIntroduction(source.Id,source.Name);txtPoolPreview.SelectionStart=0;
        }

        private Dictionary<string,object> CardPoolPayload(bool enabled)
        {
            if(string.IsNullOrEmpty(poolRunKey)) throw new InvalidOperationException(UiText.T("请先连接 / 读取本局牌池。"));
            if(enabled && poolDraft.Count==0) throw new InvalidOperationException(UiText.T("至少保留一种卡牌，或点击恢复本局原版牌池。"));
            return new Dictionary<string,object> { {"runKey",poolRunKey},{"enabled",enabled},{"cardIds",poolDraft.OrderBy(id=>id).ToArray()} };
        }

        private void ApplyRunCardPool(bool enabled)
        {
            Dictionary<string,object> payload;
            try { payload=CardPoolPayload(enabled); } catch(Exception error) { lblPoolState.Text=error.Message; lblPoolState.ForeColor=Color.Firebrick; return; }
            PoolWorker(delegate(LiveClient client) { LiveResponse status=client.Status(); if(!status.Ok) throw new InvalidOperationException(status.Error); return client.SetCardPool(payload,Math.Max(status.Revision+1,DateTime.UtcNow.Ticks)); }, delegate(object value) {
                LiveResponse result=(LiveResponse)value; if(!result.Ok || !DBool(result.Data,"applied")) throw new InvalidOperationException(result.Error ?? DStr(result.Data,"error"));
                if(!enabled) poolDraft.Clear(); RenderPoolDraft();
                lblPoolState.Text=enabled ? UiText.T("已应用本局牌池：")+poolDraft.Count+UiText.T(" 种。之后生成的战斗奖励、商店卡牌及补货按此列表出现；继续存档会保留。") : UiText.T("已恢复本局原版牌池，之后按游戏原有规则生成。");
                lblPoolState.ForeColor=Color.DarkGreen;
            });
        }
    }
}

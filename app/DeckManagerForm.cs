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
        private DeckLibrary deckLibrary;
        private bool deckLibraryWritable = true;
        private DeckPreset deckDraft;
        private bool deckUiLoading;
        private bool deckDirty;
        private ComboBox cmbPresets;
        private TextBox txtPresetName;
        private DataGridView gridPreset;
        private DataGridView gridRunDeck;
        private BindingList<DeckEntry> presetRows;
        private ListBox cmbDeckSource;
        private TextBox txtDeckSearch;
        private CheckBox chkOnlyCustomSources;
        private NumericUpDown numPresetCount;
        private NumericUpDown numPresetUpgrades;
        private ComboBox cmbRunGroup;
        private CheckBox chkSyncCombatDeck;
        private Label lblDeckManagerState;
        private Label lblRunDeckSummary;
        private Dictionary<string, object> deckSnapshot;
        private List<DeckSource> deckSources = new List<DeckSource>();
        private List<DeckSource> runtimeDeckSources = new List<DeckSource>();
        private bool deckBusy;
        private TabPage deckManagerPage;

        private static FlowLayoutPanel DeckFlow()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = false, WrapContents = true, Padding = new Padding(2), Margin = new Padding(0) };
        }

        private static Button DeckButton(string text, EventHandler handler)
        {
            Button button = new Button { Text = text, AutoSize = true, Height = 30, Margin = new Padding(3), Padding = new Padding(5, 2, 5, 2) };
            button.Click += handler;
            return button;
        }

        private static DataGridView DeckGrid()
        {
            return new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true, BackgroundColor = Color.White, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        }

        private static void DeckColumn(DataGridView grid, string header, string property, bool readOnly, float weight)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = header, DataPropertyName = property, Name = property, ReadOnly = readOnly, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });
        }

        private void BuildDeckManagerTab()
        {
            deckManagerPage = NewTab(UiText.T("卡组与本局牌组"));
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            deckManagerPage.Controls.Add(root);
            FlowLayoutPanel header = DeckFlow();
            header.Controls.Add(DeckButton(UiText.T("连接 / 刷新本局"), delegate { RefreshDeckManager(); }));
            header.Controls.Add(DeckButton(UiText.T("将本局主牌组存为预设"), delegate { CaptureRunPreset(); }));
            header.Controls.Add(new Label { Text = UiText.T("地图选路线时也可编辑主牌组，后续战斗使用修改后的卡组。多选可用 Ctrl / Shift。"), AutoSize = true, Margin = new Padding(12, 10, 0, 0) });
            root.Controls.Add(header, 0, 0);

            TableLayoutPanel columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            root.Controls.Add(columns, 0, 1);
            GroupBox presets = new GroupBox { Text = UiText.T("自定义卡组预设"), Dock = DockStyle.Fill, Padding = new Padding(8) };
            GroupBox current = new GroupBox { Text = UiText.T("本局实时牌组"), Dock = DockStyle.Fill, Padding = new Padding(8) };
            columns.Controls.Add(presets, 0, 0); columns.Controls.Add(current, 1, 0);
            TableLayoutPanel left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7 };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
            presets.Controls.Add(left);
            FlowLayoutPanel presetPicker = DeckFlow();
            cmbPresets = new ComboBox { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPresets.SelectedIndexChanged += delegate { SelectDeckPreset(); };
            presetPicker.Controls.Add(cmbPresets);
            presetPicker.Controls.Add(DeckButton(UiText.T("新建"), delegate { NewDeckPreset(); }));
            presetPicker.Controls.Add(DeckButton(UiText.T("删除预设"), delegate { DeleteDeckPreset(); }));
            left.Controls.Add(presetPicker, 0, 0);
            FlowLayoutPanel naming = DeckFlow();
            naming.Controls.Add(new Label { Text = UiText.T("名称"), AutoSize = true, Margin = new Padding(0, 8, 3, 0) });
            txtPresetName = new TextBox { Width = 235 };
            txtPresetName.TextChanged += delegate { if (!deckUiLoading) deckDirty = true; };
            naming.Controls.Add(txtPresetName);
            naming.Controls.Add(DeckButton(UiText.T("保存卡组"), delegate { SaveDeckDraft(true); }));
            left.Controls.Add(naming, 0, 1);
            gridPreset = DeckGrid();
            DeckColumn(gridPreset, UiText.T("卡牌"), "name", true, 36);
            DeckColumn(gridPreset, "ID", "cardId", true, 34);
            DeckColumn(gridPreset, UiText.T("数量"), "count", false, 15);
            DeckColumn(gridPreset, UiText.T("升级次数"), "upgrades", false, 15);
            gridPreset.CellValueChanged += delegate { if (!deckUiLoading) deckDirty = true; };
            gridPreset.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; DeckMessage(UiText.T("数量和升级次数必须为整数。"), true); };
            left.Controls.Add(gridPreset, 0, 2);
            FlowLayoutPanel presetActions = DeckFlow();
            presetActions.Controls.Add(DeckButton(UiText.T("移除选中条目"), delegate { RemovePresetRows(); }));
            presetActions.Controls.Add(DeckButton(UiText.T("整套实时加入所选牌区"), delegate { AddPresetToRun(); }));
            left.Controls.Add(presetActions, 0, 3);
            FlowLayoutPanel sourceSearch = DeckFlow();
            sourceSearch.Controls.Add(new Label { Text = UiText.T("找卡"), AutoSize = true, Margin = new Padding(0, 8, 3, 0) });
            txtDeckSearch = new TextBox { Width = 165 };
            txtDeckSearch.TextChanged += delegate { FilterDeckSources(); };
            sourceSearch.Controls.Add(txtDeckSearch);
            cmbDeckProfession=Professions.Filter(true);cmbDeckProfession.SelectedIndexChanged+=delegate{FilterDeckSources();};sourceSearch.Controls.Add(cmbDeckProfession);
            chkOnlyCustomSources = new CheckBox { Text = UiText.T("仅自定义卡"), AutoSize = true };
            chkOnlyCustomSources.CheckedChanged += delegate { FilterDeckSources(); };
            sourceSearch.Controls.Add(chkOnlyCustomSources);
            cmbDeckSource = new ListBox { Width = 475, Height = 128, IntegralHeight=false, HorizontalScrollbar=true };
            MultiSelect(cmbDeckSource);
            cmbDeckSource.SelectedIndexChanged += delegate { ShowDeckSourcePreview(); };
            sourceSearch.Controls.Add(cmbDeckSource);
            left.Controls.Add(sourceSearch, 0, 4);
            FlowLayoutPanel sourceActions = DeckFlow();
            sourceActions.Controls.Add(new Label { Text = UiText.T("数量"), AutoSize = true, Margin = new Padding(0, 8, 3, 0) });
            numPresetCount = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 1, Width = 60 };
            sourceActions.Controls.Add(numPresetCount);
            sourceActions.Controls.Add(new Label { Text = UiText.T("升级次数"), AutoSize = true, Margin = new Padding(6, 8, 3, 0) });
            numPresetUpgrades = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 0, Width = 60 };
            sourceActions.Controls.Add(numPresetUpgrades);
            sourceActions.Controls.Add(DeckButton(UiText.T("加入预设"), delegate { AddSourceToPreset(); }));
            sourceActions.Controls.Add(DeckButton(UiText.T("实时加入本局"), delegate { AddSourceToRun(); }));
            left.Controls.Add(sourceActions, 0, 5);
            GroupBox description = new GroupBox { Text = UiText.T("所选卡牌描述（可滚动查看升级效果）"), Dock = DockStyle.Fill, Padding = new Padding(8) };
            txtDeckSourcePreview = CustomCardPreview.CreateTextBox(UiText.T("找卡列表所选卡牌描述"));
            description.Controls.Add(txtDeckSourcePreview);
            left.Controls.Add(description, 0, 6);

            TableLayoutPanel right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            current.Controls.Add(right);
            FlowLayoutPanel runFilters = DeckFlow();
            cmbRunGroup = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbRunGroup.Items.AddRange(new string[] { "主牌组", "手牌", "抽牌堆", "弃牌堆", "消耗堆" });
            cmbRunGroup.SelectedIndex = 0;
            cmbRunGroup.SelectedIndexChanged += delegate { chkSyncCombatDeck.Enabled = cmbRunGroup.SelectedIndex == 0; RefreshRunDeckView(deckSnapshot); };
            runFilters.Controls.Add(cmbRunGroup);
            chkSyncCombatDeck = new CheckBox { Text = UiText.T("主牌组操作同步至当前战斗"), AutoSize = true, Checked = false };
            runFilters.Controls.Add(chkSyncCombatDeck);
            right.Controls.Add(runFilters, 0, 0);
            lblRunDeckSummary = new Label { Dock = DockStyle.Fill, Text = UiText.T("请连接游戏，进入一局后刷新。"), AutoEllipsis = true };
            right.Controls.Add(lblRunDeckSummary, 0, 1);
            gridRunDeck = DeckGrid(); gridRunDeck.ReadOnly = true;
            DeckColumn(gridRunDeck, UiText.T("卡牌"), "name", true, 40);
            DeckColumn(gridRunDeck, UiText.T("升级次数"), "timesUpgraded", true, 15);
            DeckColumn(gridRunDeck, UiText.T("费用"), "cost", true, 12);
            DeckColumn(gridRunDeck, UiText.T("伤害"), "damage", true, 12);
            DeckColumn(gridRunDeck, UiText.T("格挡"), "block", true, 12);
            gridRunDeck.CellDoubleClick += delegate { EditRunCardValues(); };
            right.Controls.Add(gridRunDeck, 0, 2);
            FlowLayoutPanel runActions = DeckFlow();
            runActions.Controls.Add(DeckButton(UiText.T("升级选中卡牌"), delegate { ModifyRunSelection("UPGRADE"); }));
            runActions.Controls.Add(DeckButton(UiText.T("从本局移除选中卡牌"), delegate { ModifyRunSelection("REMOVE"); }));
            runActions.Controls.Add(DeckButton(UiText.T("编辑选中卡数值 / 效果"), delegate { EditRunCardValues(); }));
            right.Controls.Add(runActions, 0, 3);
            right.Controls.Add(new Label { Dock = DockStyle.Fill, Text = UiText.T("主牌组修改影响本局后续战斗，并由游戏正常保存。\r\n战斗牌区修改仅本场生效。同步加入的牌进入当前抽牌堆。\r\n先关闭游戏中的选牌 / 牌组界面；结算期间会等待安全时机。") }, 0, 4);
            lblDeckManagerState = new Label { Dock = DockStyle.Fill, Padding = new Padding(4, 8, 4, 0), Text = UiText.T("卡组预设独立保存。修改条目后点击“保存卡组”。每次实时加入最多 100 张。"), ForeColor = Color.DimGray };
            root.Controls.Add(lblDeckManagerState, 0, 2);
            try { deckLibrary = DeckStore.Load(DeckStore.PathName); }
            catch (Exception e) { deckLibrary = new DeckLibrary(); deckLibraryWritable = false; DeckMessage(e.Message + UiText.T("；卡组库已设为只读，避免覆盖原文件。"), true); }
            RefreshPresetPicker(null);
            RefreshDeckSources();
        }

        private void DeckMessage(string message, bool error)
        {
            if (lblDeckManagerState == null) return;
            lblDeckManagerState.Text = message;
            lblDeckManagerState.ForeColor = error ? Color.Firebrick : Color.DarkGreen;
        }

        private void RefreshPresetPicker(string id)
        {
            deckUiLoading = true;
            cmbPresets.Items.Clear();
            foreach (DeckPreset preset in deckLibrary.decks) cmbPresets.Items.Add(preset);
            if (id != null) foreach (DeckPreset preset in cmbPresets.Items) if (preset.id == id) { cmbPresets.SelectedItem = preset; break; }
            if (cmbPresets.SelectedIndex < 0 && cmbPresets.Items.Count > 0) cmbPresets.SelectedIndex = 0;
            deckUiLoading = false;
            LoadDeckDraft(cmbPresets.SelectedItem as DeckPreset);
        }

        private void LoadDeckDraft(DeckPreset preset)
        {
            deckUiLoading = true;
            deckDraft = preset == null ? null : JsonUtil.FromJson<DeckPreset>(JsonUtil.ToJson(preset));
            txtPresetName.Text = deckDraft == null ? "" : deckDraft.name;
            presetRows = new BindingList<DeckEntry>(deckDraft == null ? new List<DeckEntry>() : deckDraft.entries);
            gridPreset.DataSource = presetRows;
            deckDirty = false;
            deckUiLoading = false;
        }

        private bool FinishDeckDraft()
        {
            if (!deckDirty || deckDraft == null) return true;
            DialogResult answer = MessageBox.Show(UiText.T("当前卡组有未保存修改，是否保存？"), UiText.T("卡组预设"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) return false;
            return answer != DialogResult.Yes || SaveDeckDraft(false);
        }

        private void SelectDeckPreset()
        {
            if (deckUiLoading) return;
            DeckPreset selected = cmbPresets.SelectedItem as DeckPreset;
            if (!FinishDeckDraft())
            {
                deckUiLoading = true;
                foreach (DeckPreset preset in cmbPresets.Items) if (deckDraft != null && preset.id == deckDraft.id) { cmbPresets.SelectedItem = preset; break; }
                deckUiLoading = false;
                return;
            }
            if (selected != null) {
                DeckPreset refreshed = deckLibrary.decks.Find(delegate(DeckPreset p) { return p.id == selected.id; });
                deckUiLoading = true;
                cmbPresets.SelectedItem = refreshed;
                deckUiLoading = false;
                LoadDeckDraft(refreshed);
            } else LoadDeckDraft(null);
        }

        private bool SaveDeckDraft(bool showSuccess)
        {
            try
            {
                if (!deckLibraryWritable) throw new InvalidOperationException(UiText.T("卡组库读取失败，不能覆盖原文件。"));
                if (deckDraft == null) throw new InvalidOperationException(UiText.T("请先新建一个卡组预设。"));
                if (!gridPreset.EndEdit()) throw new InvalidOperationException(UiText.T("请先修正表格中的数字。"));
                deckDraft.name = txtPresetName.Text.Trim();
                DeckStore.Validate(deckDraft, false);
                IncludeDeckDefinitions(deckDraft);
                DeckLibrary updated = JsonUtil.FromJson<DeckLibrary>(JsonUtil.ToJson(deckLibrary));
                DeckPreset saved = JsonUtil.FromJson<DeckPreset>(JsonUtil.ToJson(deckDraft));
                int index = updated.decks.FindIndex(delegate(DeckPreset d) { return d.id == deckDraft.id; });
                if (index < 0) updated.decks.Add(saved); else updated.decks[index] = saved;
                DeckStore.Save(DeckStore.PathName, updated);
                deckLibrary = updated;
                string id = deckDraft.id;
                deckDirty = false;
                RefreshPresetPicker(id);
                if (showSuccess) DeckMessage(UiText.T("已保存卡组“") + deckDraft.name + "”。", false);
                return true;
            }
            catch (Exception e) { DeckMessage(e.Message, true); return false; }
        }

        private void NewDeckPreset()
        {
            if (!deckLibraryWritable || !FinishDeckDraft()) return;
            DeckPreset preset = new DeckPreset { name = UiText.T("我的卡组 ") + DateTime.Now.ToString("HH:mm:ss") };
            LoadDeckDraft(preset);
            deckDirty = true;
            SaveDeckDraft(true);
        }

        private void DeleteDeckPreset()
        {
            DeckPreset preset = cmbPresets.SelectedItem as DeckPreset;
            if (preset == null || !deckLibraryWritable) return;
            if (MessageBox.Show(UiText.T("删除卡组预设“") + preset.name + UiText.T("”？\n\n不会移除本局卡牌或删除卡牌定义。卡组库修改前会自动备份。"), UiText.T("删除预设"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                DeckLibrary updated = JsonUtil.FromJson<DeckLibrary>(JsonUtil.ToJson(deckLibrary));
                updated.decks.RemoveAll(delegate(DeckPreset d) { return d.id == preset.id; });
                DeckStore.Save(DeckStore.PathName, updated);
                deckLibrary = updated;
                deckDirty = false;
                RefreshPresetPicker(null);
                DeckMessage(UiText.T("已删除预设“") + preset.name + UiText.T("”，备份位于 data/backups。"), false);
            }
            catch (Exception e) { DeckMessage(e.Message, true); }
        }

        private void RemovePresetRows()
        {
            if (deckDraft == null) return;
            List<DeckEntry> selected = new List<DeckEntry>();
            foreach (DataGridViewRow row in gridPreset.SelectedRows) if (row.DataBoundItem is DeckEntry) selected.Add((DeckEntry)row.DataBoundItem);
            foreach (DeckEntry entry in selected) presetRows.Remove(entry);
            if (selected.Count > 0) { deckDirty = true; DeckMessage(UiText.T("已从预设草稿移除 ") + selected.Count + UiText.T(" 项；点击保存卡组。"), false); }
        }

        private void RefreshDeckSources()
        {
            if (cmbDeckSource == null) return;
            Dictionary<string, DeckSource> sources = new Dictionary<string, DeckSource>();
            if (catalog != null && catalog.cards != null) foreach (CatalogCard c in catalog.cards) sources[c.id] = new DeckSource(c.id, c.name, false);
            // Runtime retains definitions for cards already in a save. Picker membership
            // is owned by the editor library, so deletion/disable cannot be undone by refresh.
            foreach (DeckSource source in runtimeDeckSources)
                if (!source.Custom && catalog!=null && catalog.cards.Any(c=>c.id==source.Id && Professions.NativeColors.Contains(c.color))) sources[source.Id] = source;
            if (profile != null) foreach (CustomCardModel c in profile.customCards) if (c.enabled) sources[c.id] = new DeckSource(c.id, c.name, true);
            deckSources = sources.Values.OrderByDescending(delegate(DeckSource s) { return s.Custom; }).ThenBy(delegate(DeckSource s) { return s.Name; }).ToList();
            FilterDeckSources();
        }

        private void FilterDeckSources()
        {
            if (cmbDeckSource == null) return;
            HashSet<string> oldIds=new HashSet<string>(cmbDeckSource.SelectedItems.Cast<DeckSource>().Select(s=>s.Id));
            string search = txtDeckSearch.Text.Trim();
            cmbDeckSource.BeginUpdate(); cmbDeckSource.Items.Clear();
            foreach (DeckSource source in deckSources)
            {
                if (chkOnlyCustomSources.Checked && !source.Custom) continue;
                if(!Professions.Matches(cmbDeckProfession,SourceProfession(source.Id),source.Custom,detectedProfession))continue;
                if (search.Length > 0 && (source.Name + " " + source.Id+" "+(source.Custom?source.Name:UiText.NativeName(source.Id,source.Name))).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                cmbDeckSource.Items.Add(source);
                if (oldIds.Contains(source.Id)) cmbDeckSource.SetSelected(cmbDeckSource.Items.Count-1,true);
            }
            if (cmbDeckSource.SelectedIndex < 0 && cmbDeckSource.Items.Count > 0) cmbDeckSource.SelectedIndex = 0;
            cmbDeckSource.EndUpdate();
            ShowDeckSourcePreview();
        }

        private void AddSourceToPreset()
        {
            try
            {
                if (deckDraft == null) { NewDeckPreset(); if (deckDraft == null) return; }
                if(cmbDeckSource.SelectedItems.Count==0)throw new InvalidOperationException(UiText.T("请先选择卡牌，可 Ctrl / Shift 多选。"));
                foreach(DeckSource source in cmbDeckSource.SelectedItems)presetRows.Add(new DeckEntry{cardId=source.Id,name=source.Name,count=(int)numPresetCount.Value,upgrades=(int)numPresetUpgrades.Value});
                deckDirty=true;
                DeckMessage(UiText.T("已加入预设草稿；数量和升级次数可在表格中修改，完成后保存。"), false);
            }
            catch (Exception e) { DeckMessage(e.Message, true); }
        }

        private void IncludeDeckDefinitions(DeckPreset preset)
        {
            Dictionary<string, CustomCardModel> definitions = new Dictionary<string, CustomCardModel>();
            if (preset.customCards != null) foreach (CustomCardModel c in preset.customCards) definitions[c.id] = c;
            foreach (CustomCardModel c in profile.customCards) definitions[c.id] = c;
            foreach (LiveInstanceItem item in liveInstanceItems)
            {
                object raw;
                if (item.Card.TryGetValue("spec", out raw) && raw != null)
                {
                    CustomCardModel c = JsonUtil.FromJson<CustomCardModel>(JsonUtil.ToJson(raw));
                    definitions[c.id] = c;
                }
            }
            preset.customCards = new List<CustomCardModel>();
            HashSet<string> seen = new HashSet<string>();
            foreach (DeckEntry entry in preset.entries)
                if (definitions.ContainsKey(entry.cardId) && seen.Add(entry.cardId)) preset.customCards.Add(definitions[entry.cardId]);
        }

        private void RefreshRunDeckView(Dictionary<string, object> snapshot)
        {
            if (gridRunDeck == null) return;
            DetectProfession(snapshot);
            deckSnapshot = snapshot;
            HashSet<string> selected = new HashSet<string>();
            foreach (DataGridViewRow row in gridRunDeck.SelectedRows)
                if (row.Tag is Dictionary<string, object>) selected.Add(DStr((Dictionary<string, object>)row.Tag, "instanceId"));
            gridRunDeck.Rows.Clear();
            Dictionary<string, object> groups = snapshot == null ? null : DObject(snapshot, "groups");
            IList cards = groups == null ? null : DList(groups, RunGroupKey());
            if (cards != null) foreach (object raw in cards)
            {
                Dictionary<string, object> card = raw as Dictionary<string, object>;
                if (card == null) continue;
                int i = gridRunDeck.Rows.Add(DStr(card, "name"), DStr(card, "timesUpgraded"), DStr(card, "cost"), DStr(card, "damage"), DStr(card, "block"));
                gridRunDeck.Rows[i].Tag = card;
            }
            gridRunDeck.ClearSelection();
            foreach (DataGridViewRow row in gridRunDeck.Rows)
                if (selected.Contains(DStr((Dictionary<string, object>)row.Tag, "instanceId"))) row.Selected = true;
            lblRunDeckSummary.Text = snapshot != null && DBool(snapshot, "inRun")
                ? Convert.ToString(cmbRunGroup.SelectedItem) + "：" + gridRunDeck.Rows.Count + UiText.T(" 张  |  ") + (DBool(snapshot, "inCombat") ? UiText.T("战斗中") : UiText.T("战斗外")) + UiText.T("  |  刷新于 ") + DateTime.Now.ToString("HH:mm:ss")
                : UiText.T("当前没有可编辑的本局牌组。请进入一局后刷新。");
        }

        private static Dictionary<string, object> DObject(Dictionary<string, object> obj, string key)
        {
            object value; return obj != null && obj.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        private static IList DList(Dictionary<string, object> obj, string key)
        {
            object value;
            return obj != null && obj.TryGetValue(key, out value) && value is IList ? (IList)value : new ArrayList();
        }

        private string RunGroupKey()
        {
            string[] groups = { "masterDeck", "hand", "drawPile", "discardPile", "exhaustPile" };
            return groups[Math.Max(0, cmbRunGroup.SelectedIndex)];
        }

        private Dictionary<string, object> DeckEditPayload(string action)
        {
            if (deckSnapshot == null || !DBool(deckSnapshot, "inRun")) throw new InvalidOperationException(UiText.T("请先连接并刷新本局牌组。"));
            Dictionary<string, object> versions = DObject(deckSnapshot, "groupVersions");
            if (versions == null) throw new InvalidOperationException(UiText.T("游戏仍加载旧版适配层。请退出游戏，应用新版补丁，再启动一次。"));
            return new Dictionary<string, object> {
                { "action", action }, { "group", RunGroupKey() }, { "runKey", DStr(deckSnapshot, "runKey") },
                { "expectedVersion", DStr(versions, RunGroupKey()) }, { "syncCombat", cmbRunGroup.SelectedIndex == 0 && chkSyncCombatDeck.Checked }
            };
        }

        private void AddSourceToRun()
        {
            try
            {
                DeckPreset single = new DeckPreset { name = UiText.T("单卡") };
                if(cmbDeckSource.SelectedItems.Count==0)throw new InvalidOperationException(UiText.T("请先选择卡牌，可 Ctrl / Shift 多选。"));
                foreach(DeckSource source in cmbDeckSource.SelectedItems)single.entries.Add(new DeckEntry{cardId=source.Id,name=source.Name,count=(int)numPresetCount.Value,upgrades=(int)numPresetUpgrades.Value});
                IncludeDeckDefinitions(single);
                AddDeckToRun(single);
            }
            catch (Exception e) { DeckMessage(e.Message, true); }
        }

        private void AddPresetToRun()
        {
            if (!SaveDeckDraft(false)) return;
            try { DeckStore.Validate(deckDraft, true); AddDeckToRun(deckDraft); }
            catch (Exception e) { DeckMessage(e.Message, true); }
        }

        private void AddDeckToRun(DeckPreset preset)
        {
            Dictionary<string, object> payload = DeckEditPayload("ADD");
            int total = preset.entries.Sum(delegate(DeckEntry e) { return e.count; });
            if (total > 100) throw new InvalidOperationException(UiText.T("每次实时加入最多 100 张牌，请拆分卡组。"));
            payload["entries"] = preset.entries;
            payload["customCards"] = preset.customCards;
            ExecuteDeckEdit(payload, UiText.T("正在加入 ") + total + UiText.T(" 张牌，等待游戏安全时机…"));
        }

        private void ModifyRunSelection(string action)
        {
            try
            {
                Dictionary<string, object> payload = DeckEditPayload(action);
                List<string> ids = new List<string>();
                foreach (DataGridViewRow row in gridRunDeck.SelectedRows) ids.Add(DStr((Dictionary<string, object>)row.Tag, "instanceId"));
                if (ids.Count == 0) throw new InvalidOperationException(UiText.T("请在右侧选择卡牌；可用 Ctrl / Shift 多选。"));
                if (action == "REMOVE" && MessageBox.Show(UiText.T("从“") + cmbRunGroup.SelectedItem + UiText.T("”移除选中的 ") + ids.Count + UiText.T(" 张卡？")
                    + (cmbRunGroup.SelectedIndex == 0 && chkSyncCombatDeck.Checked ? UiText.T("\n同时移除当前战斗中对应的卡牌副本。") : "")
                    + UiText.T("\n不会删除卡牌定义或卡组预设。"), UiText.T("移除本局卡牌"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                payload["instanceIds"] = ids;
                ExecuteDeckEdit(payload, UiText.T("正在") + (action == "REMOVE" ? UiText.T("移除") : UiText.T("升级")) + UiText.T("选中卡牌，等待游戏安全时机…"));
            }
            catch (Exception e) { DeckMessage(e.Message, true); }
        }

        private void ExecuteDeckEdit(Dictionary<string, object> payload, string message)
        {
            RunDeckWorker(delegate(LiveClient client) {
                LiveResponse capabilities = client.Capabilities();
                if (!capabilities.Ok || !DList(capabilities.Data, "ops").Contains("deckEdit"))
                    throw new InvalidOperationException(UiText.T("适配层版本过旧，请退出游戏、应用新版补丁后重新启动。"));
                LiveResponse status = client.Status();
                if (!status.Ok) throw new InvalidOperationException(status.Error);
                LiveResponse result = client.EditDeck(payload, status.Revision + 1);
                if (!result.Ok || !DBool(result.Data, "edited")) throw new InvalidOperationException(result.Error ?? DStr(result.Data, "error"));
                return result;
            }, delegate(LiveResponse result) {
                DeckMessage(UiText.T("已完成：") + DStr(result.Data, "changed") + UiText.T(" 张。") + DStr(result.Data, "saveNote"), false);
                if (result.Data.ContainsKey("warning")) DeckMessage(lblDeckManagerState.Text + " " + DStr(result.Data, "warning"), true);
                Logger.Log(UiText.T("牌组操作成功 request=") + result.RequestId + " action=" + DStr(result.Data, "action") + " changed=" + DStr(result.Data, "changed"));
                RefreshDeckManager();
            }, message);
        }

        private void RefreshDeckManager()
        {
            RunDeckWorker(delegate(LiveClient client) {
                LiveResponse snapshot = client.Snapshot();
                if (!snapshot.Ok) throw new InvalidOperationException(snapshot.Error);
                LiveResponse cards = client.CardCatalog();
                if (cards.Ok) snapshot.Data["availableCards"] = DList(cards.Data, "cards");
                return snapshot;
            }, delegate(LiveResponse result) {
                liveRevision = result.Revision;
                PopulateLiveTree(result.Data);
                IList cards = DList(result.Data, "availableCards");
                runtimeDeckSources.Clear();
                foreach (object raw in cards) {
                    Dictionary<string, object> card = raw as Dictionary<string, object>;
                    if (card != null) runtimeDeckSources.Add(new DeckSource(DStr(card, "cardId"), DStr(card, "name"), DBool(card, "isCustom")));
                }
                RefreshDeckSources();
                if (!result.Data.ContainsKey("groupVersions")) DeckMessage(UiText.T("已连接旧版适配层，请退出游戏并应用新版补丁后再使用牌组操作。"), true);
            }, null);
        }

        private void RunDeckWorker(Func<LiveClient, LiveResponse> work, Action<LiveResponse> completed, string message)
        {
            if (deckBusy) return;
            if (string.IsNullOrEmpty(gameDir)) { DeckMessage(UiText.T("请先设置游戏目录。"), true); return; }
            deckBusy = true; tabs.Enabled = false;
            if (message != null) DeckMessage(message, false);
            string directory = gameDir;
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { using (LiveClient client = new LiveClient()) { client.Connect(directory); e.Result = work(client); } };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
                deckBusy = false; tabs.Enabled = true;
                if (e.Error != null) { DeckMessage(e.Error.Message + UiText.T(" 如牌组已变化，请刷新后重试。"), true); Logger.Log(UiText.T("牌组操作失败: ") + e.Error.Message); }
                else { try { completed((LiveResponse)e.Result); } catch (Exception error) { DeckMessage(error.Message, true); } }
                worker.Dispose();
            };
            worker.RunWorkerAsync();
        }

        private void CaptureRunPreset()
        {
            if (!deckLibraryWritable || !FinishDeckDraft()) return;
            try
            {
                Dictionary<string, object> groups = DObject(deckSnapshot, "groups");
                IList cards = groups == null ? null : DList(groups, "masterDeck");
                if (cards == null || cards.Count == 0) throw new InvalidOperationException(UiText.T("请先刷新本局主牌组。"));
                DeckPreset preset = new DeckPreset { name = UiText.T("本局卡组 ") + DateTime.Now.ToString("MM-dd HH:mm:ss") };
                foreach (object raw in cards) {
                    Dictionary<string, object> card = (Dictionary<string, object>)raw;
                    string id = DStr(card, "cardId"); int upgrades = Convert.ToInt32(card["timesUpgraded"]); int misc = card.ContainsKey("misc") ? Convert.ToInt32(card["misc"]) : 0;
                    DeckEntry entry = preset.entries.Find(delegate(DeckEntry x) { return x.cardId == id && x.upgrades == upgrades && x.misc == misc; });
                    if (entry == null) preset.entries.Add(new DeckEntry { cardId = id, name = DStr(card, "name"), count = 1, upgrades = upgrades, misc = misc }); else entry.count++;
                }
                IncludeDeckDefinitions(preset);
                LoadDeckDraft(preset); deckDirty = true;
                if (SaveDeckDraft(false)) DeckMessage(UiText.T("已保存主牌组为预设，包含卡牌 ID、升级次数及 misc；不复制战斗临时增益。"), false);
            }
            catch (Exception e) { DeckMessage(e.Message, true); }
        }

        private void EditRunCardValues()
        {
            if (gridRunDeck.SelectedRows.Count != 1) { DeckMessage(UiText.T("请选择一张卡牌后编辑数值或效果。"), true); return; }
            Dictionary<string, object> card = gridRunDeck.SelectedRows[0].Tag as Dictionary<string, object>;
            string id = DStr(card, "instanceId");
            cmbLiveGroupFilter.SelectedIndex = 0; txtLiveSearch.Text = "";
            foreach (object raw in lstLiveInstances.Items) {
                LiveInstanceItem item = raw as LiveInstanceItem;
                if (item != null && item.InstanceId == id) {
                    lstLiveInstances.SelectedItem = item;
                    chkLiveScopeInstance.Checked = true; chkLiveScopeMaster.Checked = false; chkLiveScopeCombat.Checked = false; chkLiveScopeDefinition.Checked = false;
                    OpenAdvancedLiveEditor();
                    return;
                }
            }
            DeckMessage(UiText.T("该实例已变化，请刷新。"), true);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (deckBusy || poolBusy || definitionSyncBusy || nativeBusy) { e.Cancel = true; SetStatus(UiText.T("正在等待游戏确认，请完成后再关闭。")); return; }
            if (!FinishDeckDraft()) { e.Cancel = true; return; }
            base.OnFormClosing(e);
        }

        private class DeckSource
        {
            public string Id; public string Name; public bool Custom;
            public DeckSource(string id, string name, bool custom) { Id = id; Name = name; Custom = custom; }
            public override string ToString() { return (Custom ? UiText.T("[自定义] ")+Name : UiText.NativeName(Id,Name)) + "  [" + Id + "]"; }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Forms;

namespace StsCardEditor
{
    public partial class MainForm : Form
    {
        private Catalog catalog;
        private Profile profile;
        private ApiSettings apiSettings;
        private GameInfo gameInfo;
        private string gameDir;
        private CustomCardModel generatedCard;
        private CreativeBuildResult generatedCreativeBuild;
        private bool definitionSyncBusy;
        private ComboBox cmbCreativeMode;
        private NumericUpDown numCreativity;

        private LiveClient liveClient;
        private Dictionary<string, object> selectedLiveInstance;
        private CustomCardModel liveSpec;
        private bool selectedLiveIsCustom;
        private long liveRevision = 1L;
        private string liveSelectedInstanceId = "";

        private TabControl tabs;
        private TabControl liveSubTabs;
        private ToolStripStatusLabel statusLabel;
        private Label lblLiveConnection;
        private Label lblLiveGame;
        private Label lblLiveCombat;
        private Label lblLiveSafe;
        private Label lblLiveRevision;
        private Label lblLiveApplyState;
        private Label lblLiveCurrentValues;
        private Label lblLiveScopeSummary;
        private Label lblLiveDescriptionState;
        private ComboBox cmbLivePreset;
        private ListBox lstLiveInstances;
        private ComboBox cmbLiveGroupFilter;
        private TextBox txtLiveSearch;
        private TextBox txtLiveInfo;
        private bool liveTreeUpdating;
        private readonly List<LiveInstanceItem> liveInstanceItems = new List<LiveInstanceItem>();
        private Dictionary<string, object> lastLiveSnapshot;
        private TextBox txtLiveName;
        private TextBox txtLiveCost;
        private TextBox txtLiveDamage;
        private TextBox txtLiveUpDamage;
        private TextBox txtLiveBlock;
        private TextBox txtLiveUpBlock;
        private TextBox txtLiveMagic;
        private TextBox txtLiveUpMagic;
        private TextBox txtLiveDescription;
        private TextBox txtLiveUpDescription;
        private CheckBox chkLiveExhaust;
        private CheckBox chkLiveInnate;
        private CheckBox chkLiveEthereal;
        private CheckBox chkLiveRetain;
        private CheckBox chkLiveScopeInstance;
        private CheckBox chkLiveScopeCombat;
        private CheckBox chkLiveScopeMaster;
        private CheckBox chkLiveScopeDefinition;
        private CheckBox chkLivePersist;
        private CheckBox chkLiveAutoDescription;
        private ListBox lstLiveEffects;
        private ComboBox cmbLiveEffectType;
        private NumericUpDown numLiveAmount;
        private NumericUpDown numLiveUpAmount;
        private NumericUpDown numLiveHits;
        private NumericUpDown numLiveUpHits;
        private ComboBox cmbLivePower;
        private ComboBox cmbLiveTarget;
        private ComboBox cmbLiveCondition;
        private NumericUpDown numLiveConditionMin;
        private TextBox txtLiveCardRef;
        private ComboBox cmbLiveDestination;
        private CheckBox chkLiveCardUpgrade;
        private Label lblLiveEffectHint;
        private Button btnLiveConnect;
        private Button btnLiveApply;

        private TextBox txtGameDir;
        private Label lblGameVersion;
        private Label lblGameBuild;
        private Label lblJarHash;
        private Label lblCompat;
        private Label lblRunning;
        private Label lblPatchState;
        private TextBox txtGameNotes;

        private TextBox txtFilter;
        private ComboBox cmbType;
        private ListBox lstCards;
        private Label lblCardName;
        private Label lblCardId;
        private Label lblCardBase;
        private Label lblCardSpecial;
        private TextBox txtCost;
        private TextBox txtDamage;
        private TextBox txtUpDamage;
        private TextBox txtBlock;
        private TextBox txtUpBlock;
        private TextBox txtMagic;
        private TextBox txtUpMagic;
        private Label lblCardSaveState;

        private TextBox txtPrompt;
        private TextBox txtCardName;
        private TextBox txtPreview;
        private Label lblAiStatus;
        private TextBox txtBaseUrl;
        private TextBox txtModel;
        private TextBox txtApiKey;
        private NumericUpDown numTimeout;

        private ListBox lstCustom;
        private ListBox lstSaveBackups;

        private Label lblApplyState;
        private Label lblApplySummary;
        private Label lblInstallInfo;
        private TextBox txtApplyLog;
        private Button btnApply;
        private Button btnRestore;

        private TextBox txtLog;
        private Button btnAiGenerate;
        private Button btnTestApi;

        public MainForm()
        {
            UiText.Initialize();
            AppPaths.EnsureDataDirs();
            this.Text = UiText.T("SpireForge AI｜尖塔铸卡 v1.7.0");
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1600, 1040);
            this.MinimumSize = new Size(1400, 900);
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            try
            {
                catalog = ProfileStore.LoadCatalog();
            }
            catch (Exception e)
            {
                MessageBox.Show(UiText.T("无法加载 catalog.json。\n\n") + e.Message, UiText.T("启动错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            profile = ProfileStore.LoadProfile();
            apiSettings = ProfileStore.LoadSettings();

            BuildUi();
            DetectGame(null);
            RefreshCardList();
            RefreshCustomList();
            RefreshLog();
            BuildLanguageBar();
            UiText.Attach(this,RefreshLocalizedPreviews);
            Logger.Log(UiText.T("程序启动"));
        }

        private void BuildUi()
        {
            statusLabel = new ToolStripStatusLabel(UiText.T("就绪"));
            StatusStrip status = new StatusStrip();
            status.Items.Add(statusLabel);
            status.Dock = DockStyle.Bottom;
            this.Controls.Add(status);

            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            this.Controls.Add(tabs);
            tabs.BringToFront();

            BuildCompactNavigation();
        }

        private TabPage NewTab(string text)
        {
            TabPage p = new TabPage(text);
            p.Padding = new Padding(8);
            tabs.TabPages.Add(p);
            return p;
        }

        private static Label LabelAt(string text, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = text;
            l.Left = x;
            l.Top = y;
            l.Width = w;
            l.Height = 22;
            return l;
        }

        private static TextBox TextAt(int x, int y, int w)
        {
            TextBox t = new TextBox();
            t.Left = x;
            t.Top = y;
            t.Width = w;
            return t;
        }

        private static Button ButtonAt(string text, int x, int y, int w, int h, EventHandler handler)
        {
            Button b = new Button();
            b.Text = text;
            b.Left = x;
            b.Top = y;
            b.Width = w;
            b.Height = h;
            b.Click += handler;
            return b;
        }

        private static GroupBox GroupAt(string text, int x, int y, int w, int h)
        {
            GroupBox g = new GroupBox();
            g.Text = text;
            g.Left = x;
            g.Top = y;
            g.Width = w;
            g.Height = h;
            return g;
        }

        private void SetStatus(string text)
        {
            statusLabel.Text = text;
        }

        private void BuildGameTab()
        {
            TabPage p = NewTab(UiText.T("游戏与状态"));
            p.AutoScroll = true;

            p.Controls.Add(LabelAt(UiText.T("游戏目录"), 16, 18, 100));
            txtGameDir = TextAt(110, 15, 720);
            txtGameDir.ReadOnly = true;
            p.Controls.Add(txtGameDir);
            p.Controls.Add(ButtonAt(UiText.T("自动检测"), 840, 13, 100, 28, delegate { DetectGame(null); }));
            p.Controls.Add(ButtonAt(UiText.T("浏览..."), 948, 13, 100, 28, delegate { BrowseGame(); }));

            GroupBox g = GroupAt(UiText.T("兼容性与启动方式"), 16, 58, 1032, 210);
            p.Controls.Add(g);
            lblGameVersion = LabelAt(UiText.T("游戏版本: -"), 16, 28, 300);
            lblGameBuild = LabelAt(UiText.T("Steam 构建号: -"), 16, 56, 300);
            lblJarHash = LabelAt("desktop-1.0.jar SHA-256: -", 16, 84, 1000);
            lblCompat = LabelAt(UiText.T("兼容性: -"), 16, 112, 1000);
            lblRunning = LabelAt(UiText.T("游戏进程: -"), 16, 140, 300);
            lblPatchState = LabelAt(UiText.T("补丁状态: -"), 16, 166, 700);
            g.Controls.Add(lblGameVersion);
            g.Controls.Add(lblGameBuild);
            g.Controls.Add(lblJarHash);
            g.Controls.Add(lblCompat);
            g.Controls.Add(lblRunning);
            g.Controls.Add(lblPatchState);

            txtGameNotes = new TextBox();
            txtGameNotes.Left = 16;
            txtGameNotes.Top = 286;
            txtGameNotes.Width = 1032;
            txtGameNotes.Height = 210;
            txtGameNotes.Multiline = true;
            txtGameNotes.ReadOnly = true;
            txtGameNotes.ScrollBars = ScrollBars.Vertical;
            txtGameNotes.Text = UiText.T("说明：\r\n")
                + UiText.T("1. 本工具不通过 ModTheSpire / BaseMod 启动，也不要求安装它们。\r\n")
                + UiText.T("2. 应用时会把 desktop-1.0.jar 重命名为 desktop-1.0-original.jar，并放置一个很小的 desktop-1.0.jar 启动包装器。\r\n")
                + UiText.T("   包装器通过 manifest 的 Class-Path 先加载 sts-card-editor\\patch.jar，再加载原版 jar；Steam 的 javaw -jar 启动会自然使用它。\r\n")
                + UiText.T("3. config.json 保持原版，不修改 Steam 启动选项。这是对本地游戏文件的修改，不是“完全未修改”的原版；\r\n")
                + UiText.T("   不需要管理员权限（除非游戏目录只读）。\r\n")
                + UiText.T("4. Steam 成就链路保留在游戏自身代码中；本工具不做解锁、伪造或绕过授权。\r\n")
                + UiText.T("5. 恢复原版会把 desktop-1.0-original.jar 改回 desktop-1.0.jar，并删除 patch.jar；\r\n")
                + UiText.T("   存档备份与游戏文件恢复是两件不同的事。\r\n")
                + UiText.T("6. 自定义卡牌会写入存档；卸载补丁后，含自定义卡的存档可能无法正常显示这些卡牌。请先备份存档。");
            p.Controls.Add(txtGameNotes);

            p.Controls.Add(ButtonAt(UiText.T("刷新状态"), 16, 510, 110, 30, delegate { DetectGame(gameDir); }));
            p.Controls.Add(ButtonAt(UiText.T("打开游戏目录"), 136, 510, 130, 30, delegate { OpenGameDir(); }));
            p.Controls.Add(ButtonAt(UiText.T("通过 Steam 启动"), 276, 510, 150, 30, delegate { LaunchGame(); }));
        }

        private void BuildCardsTab() { BuildRealtimeCardsTab(); }

        private void BuildAiTab()
        {
            TabPage p = NewTab(UiText.T("AI 创作"));
            p.Controls.Add(LabelAt(UiText.T("自然语言描述"), 16, 18, 120));
            txtPrompt = new TextBox();
            txtPrompt.Left = 16;
            txtPrompt.Top = 44;
            txtPrompt.Width = 500;
            txtPrompt.Height = 200;
            txtPrompt.Multiline = true;
            txtPrompt.ScrollBars = ScrollBars.Vertical;
            txtPrompt.Text = UiText.T("1 费攻击，造成 9 点伤害，施加 2 层易伤，升级后伤害变为 12。");
            p.Controls.Add(txtPrompt);
            p.Controls.Add(LabelAt(UiText.T("创作模式"), 16, 254, 70));
            cmbCreativeMode = new ComboBox { Left = 90, Top = 250, Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCreativeMode.Items.AddRange(new string[] { UiText.T("自由设计（生成并编译 Java 扩展）"), UiText.T("普通效果（内置机制）") });
            cmbCreativeMode.SelectedIndex = 0;
            p.Controls.Add(cmbCreativeMode);
            p.Controls.Add(LabelAt(UiText.T("创意"), 370, 254, 50));
            numCreativity = new NumericUpDown { Left = 420, Top = 250, Width = 90, Minimum = 0, Maximum = 1, DecimalPlaces = 1, Increment = 0.1M, Value = 1 };
            p.Controls.Add(numCreativity);
            p.Controls.Add(LabelAt(UiText.T("卡名"), 16, 284, 60));
            txtCardName = TextAt(100, 281, 190);
            p.Controls.Add(txtCardName);
            p.Controls.Add(LabelAt(UiText.T("职业"),300,284,45));cmbAiProfession=new ComboBox{Left=350,Top=281,Width=166,DropDownStyle=ComboBoxStyle.DropDownList};cmbAiProfession.Items.AddRange(Professions.CustomColors);cmbAiProfession.SelectedItem="RED";Professions.FormatColors(cmbAiProfession);cmbAiProfession.SelectionChangeCommitted+=delegate{aiProfessionChosen=true;};p.Controls.Add(cmbAiProfession);
            p.Controls.Add(ButtonAt(UiText.T("本地解析"), 16, 316, 150, 32, delegate { ParseLocal(); }));
            btnAiGenerate = ButtonAt(UiText.T("AI 生成"), 176, 316, 110, 32, delegate { GenerateAi(); });
            p.Controls.Add(btnAiGenerate);
            p.Controls.Add(ButtonAt(UiText.T("校验并保存到卡库"), 296, 316, 180, 32, delegate { SaveGeneratedCard(); }));
            lblAiStatus = LabelAt("", 16, 356, 500);
            lblAiStatus.Text = UiText.T("自由设计通常请求模型3次；失败修复会增加用量。");
            p.Controls.Add(lblAiStatus);


            txtPreview = new TextBox();
            txtPreview.Left = 536;
            txtPreview.Top = 44;
            txtPreview.Width = 512;
            txtPreview.Height = 320;
            txtPreview.Multiline = true;
            txtPreview.ReadOnly = true;
            txtPreview.ScrollBars = ScrollBars.Both;
            txtPreview.WordWrap = false;
            p.Controls.Add(txtPreview);

            GroupBox g = GroupAt(UiText.T("模型服务（可选，与 OpenCode 凭据独立）"), 16, 392, 1032, 220);
            p.Controls.Add(g);
            g.Controls.Add(LabelAt("Base URL", 16, 28, 90));
            txtBaseUrl = TextAt(110, 25, 380);
            txtBaseUrl.Text = apiSettings.baseUrl;
            g.Controls.Add(txtBaseUrl);
            g.Controls.Add(LabelAt(UiText.T("模型名称"), 16, 62, 90));
            txtModel = TextAt(110, 59, 220);
            txtModel.Text = apiSettings.model;
            g.Controls.Add(txtModel);
            if (IsDeepSeekWebAddress(apiSettings.baseUrl))
            {
                // Do not send a POST to the web console. Fill the known official API
                // values in the UI; they are persisted only when the user clicks 保存设置.
                txtBaseUrl.Text = "https://api.deepseek.com/v1";
                if (string.IsNullOrEmpty(txtModel.Text) || string.Equals(txtModel.Text.Trim(), "ds", StringComparison.OrdinalIgnoreCase))
                {
                    txtModel.Text = "deepseek-chat";
                }
                lblAiStatus.Text = UiText.T("已自动填入 DeepSeek 官方 API 地址；确认 Key 后测试连接。");
                lblAiStatus.ForeColor = Color.DarkGreen;
            }
            g.Controls.Add(LabelAt("API Key", 16, 96, 90));
            txtApiKey = TextAt(110, 93, 380);
            txtApiKey.UseSystemPasswordChar = true;
            txtApiKey.Text = apiSettings.apiKey;
            g.Controls.Add(txtApiKey);
            g.Controls.Add(LabelAt(UiText.T("超时"), 16, 130, 60));
            numTimeout = new NumericUpDown();
            numTimeout.Left = 110;
            numTimeout.Top = 127;
            numTimeout.Width = 80;
            numTimeout.Minimum = 5;
            numTimeout.Maximum = 300;
            numTimeout.Value = Math.Max(5, Math.Min(300, apiSettings.timeoutSeconds));
            g.Controls.Add(numTimeout);
            g.Controls.Add(ButtonAt(UiText.T("保存设置"), 520, 24, 110, 30, delegate { SaveApiSettings(); }));
            btnTestApi = ButtonAt(UiText.T("测试连接"), 640, 24, 110, 30, delegate { TestApi(); });
            g.Controls.Add(btnTestApi);
            g.Controls.Add(ButtonAt(UiText.T("DeepSeek 示例"), 760, 24, 160, 30, delegate { FillDeepSeekPreset(); }));
            g.Controls.Add(LabelAt(UiText.T("Base URL 填 API 地址（例 https://api.deepseek.com/v1）；不要填网页；模型名如 deepseek-chat。"), 16, 168, 1000));
            g.Controls.Add(LabelAt(UiText.T("Key 使用 DPAPI 加密保存；自由设计分规则、实现、审核多轮请求，发送需求、API签名、代码及报错。"), 16, 192, 1000));
        }

        private void BuildCustomTab() { BuildModernCustomLibrary(); }

        private void BuildLiveTab()
        {
            TabPage p = NewTab(UiText.T("游戏内实时"));
            p.AutoScroll = true;
            GroupBox status = GroupAt(UiText.T("适配层与连接"), 16, 10, 1032, 105);
            p.Controls.Add(status);
            lblLiveConnection = LabelAt(UiText.T("未连接。请先应用补丁，再通过 Steam 正常启动游戏。"), 16, 24, 980);
            lblLiveGame = LabelAt(UiText.T("游戏: -"), 16, 50, 270);
            lblLiveCombat = LabelAt(UiText.T("战斗: -"), 300, 50, 180);
            lblLiveSafe = LabelAt(UiText.T("安全时机: -"), 490, 50, 250);
            lblLiveRevision = LabelAt("Revision: -", 750, 50, 250);
            status.Controls.Add(lblLiveConnection);
            status.Controls.Add(lblLiveGame);
            status.Controls.Add(lblLiveCombat);
            status.Controls.Add(lblLiveSafe);
            status.Controls.Add(lblLiveRevision);
            btnLiveConnect = ButtonAt(UiText.T("连接游戏"), 16, 76, 100, 24, delegate { ConnectLive(); });
            status.Controls.Add(btnLiveConnect);
            status.Controls.Add(ButtonAt(UiText.T("刷新快照"), 126, 76, 100, 24, delegate { RefreshLive(true); }));
            status.Controls.Add(ButtonAt(UiText.T("断开"), 236, 76, 80, 24, delegate { DisconnectLive(); }));

            liveSubTabs = new TabControl();
            liveSubTabs.Left = 16;
            liveSubTabs.Top = 125;
            liveSubTabs.Width = 1032;
            liveSubTabs.Height = 515;
            p.Controls.Add(liveSubTabs);

            BuildLiveSnapshotTab();
            BuildLiveBasicTab();
            BuildLiveEffectsTab();

            Panel actions = new Panel();
            actions.Left = 16;
            actions.Top = 642;
            actions.Width = 1032;
            actions.Height = 42;
            p.Controls.Add(actions);
            btnLiveApply = ButtonAt(UiText.T("校验并应用"), 0, 4, 130, 32, delegate { ApplyLive(); });
            actions.Controls.Add(btnLiveApply);
            actions.Controls.Add(ButtonAt(UiText.T("仅校验"), 140, 4, 100, 32, delegate { ValidateLive(); }));
            lblLiveApplyState = LabelAt("", 260, 10, 760);
            actions.Controls.Add(lblLiveApplyState);
        }

        private void BuildLiveSnapshotTab()
        {
            TabPage t = new TabPage(UiText.T("牌区快照"));
            t.Padding = new Padding(6);
            liveSubTabs.TabPages.Add(t);
            t.Controls.Add(LabelAt(UiText.T("牌区"), 10, 13, 40));
            cmbLiveGroupFilter = new ComboBox();
            cmbLiveGroupFilter.Left = 52;
            cmbLiveGroupFilter.Top = 10;
            cmbLiveGroupFilter.Width = 120;
            cmbLiveGroupFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbLiveGroupFilter.Items.AddRange(new string[] { "全部", "主牌组", "手牌", "抽牌堆", "弃牌堆", "消耗堆" });
            cmbLiveGroupFilter.SelectedIndex = 0;
            cmbLiveGroupFilter.SelectedIndexChanged += delegate { ApplyLiveInstanceFilter(); };
            t.Controls.Add(cmbLiveGroupFilter);
            t.Controls.Add(LabelAt(UiText.T("搜索"), 182, 13, 40));
            txtLiveSearch = new TextBox();
            txtLiveSearch.Left = 222;
            txtLiveSearch.Top = 10;
            txtLiveSearch.Width = 218;
            txtLiveSearch.TextChanged += delegate { ApplyLiveInstanceFilter(); };
            t.Controls.Add(txtLiveSearch);

            lstLiveInstances = new ListBox();
            lstLiveInstances.Left = 10;
            lstLiveInstances.Top = 42;
            lstLiveInstances.Width = 430;
            lstLiveInstances.Height = 390;
            lstLiveInstances.IntegralHeight = false;
            lstLiveInstances.SelectedIndexChanged += delegate { ShowLiveNode(); };
            t.Controls.Add(lstLiveInstances);
            t.Controls.Add(ButtonAt(UiText.T("选中手牌第一张"), 10, 438, 140, 28, delegate { SelectFirstHandCard(); }));
            t.Controls.Add(ButtonAt(UiText.T("刷新当前快照"), 160, 438, 120, 28, delegate { RefreshLive(true); }));
            t.Controls.Add(LabelAt(UiText.T("直接点击卡牌行；分组筛选和搜索可缩小列表。"), 290, 444, 420));
            txtLiveInfo = new TextBox();
            txtLiveInfo.Left = 450;
            txtLiveInfo.Top = 10;
            txtLiveInfo.Width = 550;
            txtLiveInfo.Height = 458;
            txtLiveInfo.Multiline = true;
            txtLiveInfo.ReadOnly = true;
            txtLiveInfo.ScrollBars = ScrollBars.Both;
            txtLiveInfo.WordWrap = false;
            txtLiveInfo.Text = UiText.T("先连接游戏，再点击左侧卡牌行。\r\n列表项格式：[牌区] 名称 #实例编号。");
            t.Controls.Add(txtLiveInfo);
        }

        private void BuildLiveBasicTab()
        {
            TabPage t = new TabPage(UiText.T("基础数值与范围"));
            t.Padding = new Padding(6);
            t.AutoScroll = true;
            liveSubTabs.TabPages.Add(t);

            GroupBox g = GroupAt(UiText.T("选中卡牌"), 10, 8, 1000, 250);
            t.Controls.Add(g);
            g.Controls.Add(LabelAt(UiText.T("名称"), 16, 25, 80));
            txtLiveName = TextAt(100, 22, 300);
            g.Controls.Add(txtLiveName);
            g.Controls.Add(LabelAt(UiText.T("费用"), 16, 60, 60));
            txtLiveCost = TextAt(80, 57, 70);
            g.Controls.Add(txtLiveCost);
            g.Controls.Add(LabelAt(UiText.T("伤害"), 170, 60, 50));
            txtLiveDamage = TextAt(220, 57, 70);
            g.Controls.Add(txtLiveDamage);
            g.Controls.Add(LabelAt(UiText.T("升级伤害"), 310, 60, 80));
            txtLiveUpDamage = TextAt(390, 57, 70);
            g.Controls.Add(txtLiveUpDamage);
            g.Controls.Add(LabelAt(UiText.T("格挡"), 16, 95, 60));
            txtLiveBlock = TextAt(80, 92, 70);
            g.Controls.Add(txtLiveBlock);
            g.Controls.Add(LabelAt(UiText.T("升级格挡"), 170, 95, 80));
            txtLiveUpBlock = TextAt(250, 92, 70);
            g.Controls.Add(txtLiveUpBlock);
            g.Controls.Add(LabelAt("Magic", 340, 95, 60));
            txtLiveMagic = TextAt(400, 92, 70);
            g.Controls.Add(txtLiveMagic);
            g.Controls.Add(LabelAt(UiText.T("升级 Magic"), 490, 95, 90));
            txtLiveUpMagic = TextAt(580, 92, 70);
            g.Controls.Add(txtLiveUpMagic);
            g.Controls.Add(LabelAt(UiText.T("描述"), 16, 130, 60));
            txtLiveDescription = new TextBox();
            txtLiveDescription.Left = 80;
            txtLiveDescription.Top = 127;
            txtLiveDescription.Width = 540;
            txtLiveDescription.Height = 42;
            txtLiveDescription.Multiline = true;
            g.Controls.Add(txtLiveDescription);
            g.Controls.Add(LabelAt(UiText.T("升级描述"), 16, 178, 70));
            txtLiveUpDescription = new TextBox();
            txtLiveUpDescription.Left = 90;
            txtLiveUpDescription.Top = 175;
            txtLiveUpDescription.Width = 530;
            txtLiveUpDescription.Height = 42;
            txtLiveUpDescription.Multiline = true;
            g.Controls.Add(txtLiveUpDescription);
            chkLiveExhaust = new CheckBox();
            chkLiveExhaust.Text = UiText.T("消耗");
            chkLiveExhaust.Left = 650;
            chkLiveExhaust.Top = 25;
            g.Controls.Add(chkLiveExhaust);
            chkLiveInnate = new CheckBox();
            chkLiveInnate.Text = UiText.T("固有");
            chkLiveInnate.Left = 720;
            chkLiveInnate.Top = 25;
            g.Controls.Add(chkLiveInnate);
            chkLiveEthereal = new CheckBox();
            chkLiveEthereal.Text = UiText.T("虚无");
            chkLiveEthereal.Left = 790;
            chkLiveEthereal.Top = 25;
            g.Controls.Add(chkLiveEthereal);
            chkLiveRetain = new CheckBox();
            chkLiveRetain.Text = UiText.T("保留");
            chkLiveRetain.Left = 860;
            chkLiveRetain.Top = 25;
            g.Controls.Add(chkLiveRetain);
            g.Controls.Add(ButtonAt(UiText.T("从效果重建描述"), 670, 70, 140, 26, delegate { RebuildLiveDescription(); }));
            g.Controls.Add(ButtonAt(UiText.T("填入当前/默认值"), 670, 105, 140, 26, delegate { ReadCurrentLiveValues(); }));
            g.Controls.Add(ButtonAt(UiText.T("使用当前描述"), 820, 70, 140, 26, delegate { UseCurrentLiveDescription(); }));
            g.Controls.Add(ButtonAt(UiText.T("清空输入"), 820, 105, 140, 26, delegate { ClearLiveFields(); }));
            chkLiveAutoDescription = new CheckBox();
            chkLiveAutoDescription.Text = UiText.T("应用时按效果自动生成描述");
            chkLiveAutoDescription.Left = 670;
            chkLiveAutoDescription.Top = 145;
            chkLiveAutoDescription.Width = 260;
            chkLiveAutoDescription.Checked = true;
            chkLiveAutoDescription.CheckedChanged += delegate
            {
                UpdateLiveDescriptionState();
                if (chkLiveAutoDescription.Checked && liveSpec != null) RebuildLiveDescription();
            };
            g.Controls.Add(chkLiveAutoDescription);
            lblLiveCurrentValues = LabelAt(UiText.T("当前实例值: 未选择"), 16, 220, 960);
            lblLiveCurrentValues.Font = new Font(this.Font.FontFamily, 8F);
            g.Controls.Add(lblLiveCurrentValues);
            lblLiveDescriptionState = LabelAt(UiText.T("自动描述: 开启（改效果后重建）"), 670, 175, 320);
            lblLiveDescriptionState.ForeColor = Color.DimGray;
            g.Controls.Add(lblLiveDescriptionState);

            GroupBox scopes = GroupAt(UiText.T("修改范围（默认仅当前实例）"), 10, 266, 1000, 125);
            t.Controls.Add(scopes);
            chkLiveScopeInstance = new CheckBox();
            chkLiveScopeInstance.Text = UiText.T("仅当前选中的这一张实例");
            chkLiveScopeInstance.Left = 16;
            chkLiveScopeInstance.Top = 25;
            chkLiveScopeInstance.Width = 280;
            chkLiveScopeInstance.Checked = true;
            chkLiveScopeInstance.CheckedChanged += delegate { UpdateLiveScopeSummary(); };
            scopes.Controls.Add(chkLiveScopeInstance);
            chkLiveScopeCombat = new CheckBox();
            chkLiveScopeCombat.Text = UiText.T("当前战斗中同 ID 的所有副本");
            chkLiveScopeCombat.Left = 16;
            chkLiveScopeCombat.Top = 52;
            chkLiveScopeCombat.Width = 300;
            chkLiveScopeCombat.CheckedChanged += delegate { UpdateLiveScopeSummary(); };
            scopes.Controls.Add(chkLiveScopeCombat);
            chkLiveScopeMaster = new CheckBox();
            chkLiveScopeMaster.Text = UiText.T("本局主牌组中同 ID 的卡牌");
            chkLiveScopeMaster.Left = 16;
            chkLiveScopeMaster.Top = 79;
            chkLiveScopeMaster.Width = 300;
            chkLiveScopeMaster.CheckedChanged += delegate { UpdateLiveScopeSummary(); };
            scopes.Controls.Add(chkLiveScopeMaster);
            chkLiveScopeDefinition = new CheckBox();
            chkLiveScopeDefinition.Text = UiText.T("后续新创建的同 ID 卡牌定义");
            chkLiveScopeDefinition.Left = 330;
            chkLiveScopeDefinition.Top = 25;
            chkLiveScopeDefinition.Width = 300;
            chkLiveScopeDefinition.CheckedChanged += delegate { UpdateLiveScopeSummary(); };
            scopes.Controls.Add(chkLiveScopeDefinition);
            chkLivePersist = new CheckBox();
            chkLivePersist.Text = UiText.T("同时保存为下次启动使用的定义");
            chkLivePersist.Left = 330;
            chkLivePersist.Top = 52;
            chkLivePersist.Width = 300;
            scopes.Controls.Add(chkLivePersist);
            scopes.Controls.Add(LabelAt(UiText.T("默认只改当前实例；定义与实例分开；结算中的动作不半途替换。"), 330, 78, 640));
            lblLiveScopeSummary = LabelAt(UiText.T("当前范围: 仅当前实例"), 330, 100, 640);
            lblLiveScopeSummary.ForeColor = Color.SteelBlue;
            scopes.Controls.Add(lblLiveScopeSummary);

        }

        private void BuildLiveEffectsTab()
        {
            TabPage t = new TabPage(UiText.T("效果列表编辑"));
            t.Padding = new Padding(6);
            t.AutoScroll = true;
            liveSubTabs.TabPages.Add(t);

            lstLiveEffects = new ListBox();
            lstLiveEffects.Left = 10;
            lstLiveEffects.Top = 10;
            lstLiveEffects.Width = 470;
            lstLiveEffects.Height = 350;
            lstLiveEffects.SelectedIndexChanged += delegate { LoadSelectedEffect(); };
            t.Controls.Add(lstLiveEffects);
            t.Controls.Add(ButtonAt(UiText.T("添加"), 10, 370, 70, 28, delegate { AddLiveEffect(); }));
            t.Controls.Add(ButtonAt(UiText.T("更新选中"), 90, 370, 90, 28, delegate { UpdateLiveEffect(); }));
            t.Controls.Add(ButtonAt(UiText.T("删除"), 190, 370, 70, 28, delegate { DeleteLiveEffect(); }));
            t.Controls.Add(ButtonAt(UiText.T("上移"), 270, 370, 70, 28, delegate { MoveLiveEffect(-1); }));
            t.Controls.Add(ButtonAt(UiText.T("下移"), 350, 370, 70, 28, delegate { MoveLiveEffect(1); }));
            t.Controls.Add(LabelAt(UiText.T("快速模板"), 10, 407, 70));
            cmbLivePreset = new ComboBox();
            cmbLivePreset.Left = 80;
            cmbLivePreset.Top = 404;
            cmbLivePreset.Width = 180;
            cmbLivePreset.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbLivePreset.Items.AddRange(new string[] { "伤害 6", "格挡 5", "抽 1 张牌", "每打出牌抽 1", "易伤 2 层", "生成一张牌" });
            cmbLivePreset.SelectedIndex = 0;
            t.Controls.Add(cmbLivePreset);
            t.Controls.Add(ButtonAt(UiText.T("载入模板"), 270, 402, 90, 26, delegate { LoadLivePreset(); }));
            t.Controls.Add(LabelAt(UiText.T("左侧列表顺序就是执行顺序。先选中一条，再改右侧参数，点“更新选中”。"), 10, 437, 470));

            GroupBox g = GroupAt(UiText.T("效果参数"), 500, 8, 520, 450);
            t.Controls.Add(g);
            g.Controls.Add(LabelAt(UiText.T("类型"), 16, 25, 50));
            cmbLiveEffectType = new ComboBox();
            cmbLiveEffectType.Left = 70;
            cmbLiveEffectType.Top = 22;
            cmbLiveEffectType.Width = 170;
            cmbLiveEffectType.DropDownStyle = ComboBoxStyle.DropDownList;
            string[] types = new string[] { "DAMAGE", "BLOCK", "DRAW", "GAIN_ENERGY", "LOSE_HP", "HEAL", "DISCARD_CARDS", "EXHAUST_CARDS", "ADD_CARD", "APPLY_POWER", "START_TURN_BLOCK", "DRAW_ON_PLAY", "ALL_CARDS_ZERO_COST", "JAVA_EXTENSION" };
            cmbLiveEffectType.Items.AddRange(types);
            cmbLiveEffectType.SelectedIndex = 0;
            cmbLiveEffectType.SelectedIndexChanged += delegate { UpdateLiveEffectInputs(); };
            g.Controls.Add(cmbLiveEffectType);
            g.Controls.Add(LabelAt(UiText.T("数值"), 260, 25, 50));
            numLiveAmount = new NumericUpDown();
            numLiveAmount.Left = 310;
            numLiveAmount.Top = 22;
            numLiveAmount.Width = 70;
            numLiveAmount.Minimum = 0;
            numLiveAmount.Maximum = 999;
            numLiveAmount.Value = 1;
            g.Controls.Add(numLiveAmount);
            g.Controls.Add(LabelAt(UiText.T("升级值"), 385, 25, 50));
            numLiveUpAmount = new NumericUpDown();
            numLiveUpAmount.Left = 435;
            numLiveUpAmount.Top = 22;
            numLiveUpAmount.Width = 50;
            numLiveUpAmount.Minimum = 0;
            numLiveUpAmount.Maximum = 999;
            g.Controls.Add(numLiveUpAmount);
            g.Controls.Add(LabelAt(UiText.T("攻击段数"), 16, 60, 70));
            numLiveHits = new NumericUpDown();
            numLiveHits.Left = 90;
            numLiveHits.Top = 57;
            numLiveHits.Width = 60;
            numLiveHits.Minimum = 1;
            numLiveHits.Maximum = 10;
            numLiveHits.Value = 1;
            g.Controls.Add(numLiveHits);
            g.Controls.Add(LabelAt(UiText.T("升级段数"), 170, 60, 70));
            numLiveUpHits = new NumericUpDown();
            numLiveUpHits.Left = 245;
            numLiveUpHits.Top = 57;
            numLiveUpHits.Width = 60;
            numLiveUpHits.Minimum = 0;
            numLiveUpHits.Maximum = 10;
            g.Controls.Add(numLiveUpHits);
            g.Controls.Add(LabelAt(UiText.T("目标"), 320, 60, 50));
            cmbLiveTarget = new ComboBox();
            cmbLiveTarget.Left = 370;
            cmbLiveTarget.Top = 57;
            cmbLiveTarget.Width = 110;
            cmbLiveTarget.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbLiveTarget.Items.AddRange(new string[] { "ENEMY", "ALL_ENEMY", "SELF", "NONE", "ALL", "SELF_AND_ENEMY" });
            cmbLiveTarget.SelectedIndex = 0;
            g.Controls.Add(cmbLiveTarget);
            g.Controls.Add(LabelAt(UiText.T("状态"), 16, 95, 50));
            cmbLivePower = new ComboBox();
            cmbLivePower.Left = 70;
            cmbLivePower.Top = 92;
            cmbLivePower.Width = 130;
            cmbLivePower.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbLivePower.Items.AddRange(new string[] { "", "Strength", "Dexterity", "Vulnerable", "Weak", "Frail", "Poison", "Thorns" });
            cmbLivePower.SelectedIndex = 0;
            g.Controls.Add(cmbLivePower);
            g.Controls.Add(LabelAt(UiText.T("条件"), 220, 95, 50));
            cmbLiveCondition = new ComboBox();
            cmbLiveCondition.Left = 270;
            cmbLiveCondition.Top = 92;
            cmbLiveCondition.Width = 120;
            cmbLiveCondition.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbLiveCondition.Items.AddRange(new string[] { "", "HAS_POWER", "HP_BELOW_PERCENT", "HAS_BLOCK", "HAND_COUNT_AT_LEAST", "ENERGY_AT_LEAST", "ENEMY_COUNT_AT_LEAST" });
            cmbLiveCondition.SelectedIndex = 0;
            g.Controls.Add(cmbLiveCondition);
            g.Controls.Add(LabelAt(UiText.T("值"), 395, 95, 20));
            numLiveConditionMin = new NumericUpDown();
            numLiveConditionMin.Left = 415;
            numLiveConditionMin.Top = 92;
            numLiveConditionMin.Width = 45;
            numLiveConditionMin.Minimum = 0;
            numLiveConditionMin.Maximum = 999;
            numLiveConditionMin.Value = 1;
            g.Controls.Add(numLiveConditionMin);
            g.Controls.Add(LabelAt(UiText.T("卡牌/扩展 ID"), 16, 130, 84));
            txtLiveCardRef = TextAt(100, 127, 170);
            g.Controls.Add(txtLiveCardRef);
            g.Controls.Add(LabelAt(UiText.T("位置"), 285, 130, 50));
            cmbLiveDestination = new ComboBox();
            cmbLiveDestination.Left = 335;
            cmbLiveDestination.Top = 127;
            cmbLiveDestination.Width = 100;
            cmbLiveDestination.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbLiveDestination.Items.AddRange(new string[] { "HAND", "DRAW", "DISCARD" });
            cmbLiveDestination.SelectedIndex = 0;
            g.Controls.Add(cmbLiveDestination);
            chkLiveCardUpgrade = new CheckBox();
            chkLiveCardUpgrade.Text = UiText.T("升级");
            chkLiveCardUpgrade.Left = 440;
            chkLiveCardUpgrade.Top = 128;
            chkLiveCardUpgrade.Width = 50;
            g.Controls.Add(chkLiveCardUpgrade);
            lblLiveEffectHint = LabelAt("", 16, 168, 470);
            lblLiveEffectHint.Height = 46;
            g.Controls.Add(lblLiveEffectHint);
            g.Controls.Add(LabelAt(UiText.T("数值为 -1 表示不使用；升级值为 0 表示升级不改变。"), 16, 222, 470));
            g.Controls.Add(LabelAt(UiText.T("条件可作用于格挡/抽牌/能量/状态/生成卡。"), 16, 247, 470));
            g.Controls.Add(LabelAt(UiText.T("ADD_CARD 使用已注册卡 ID，数量 1-10。"), 16, 272, 470));
            UpdateLiveEffectInputs();
        }

        private void ConnectLive()
        {
            if (gameDir == null)
            {
                MessageBox.Show(UiText.T("请先选择游戏目录。"), UiText.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                if (liveClient == null) liveClient = new LiveClient();
                liveClient.Connect(gameDir);
                lblLiveConnection.Text = UiText.T("已连接本机运行时适配层。");
                lblLiveConnection.ForeColor = Color.DarkGreen;
                RefreshLive(false);
            }
            catch (Exception e)
            {
                lblLiveConnection.Text = UiText.T("未连接：") + e.Message;
                lblLiveConnection.ForeColor = Color.Firebrick;
                if (liveClient != null) liveClient.Disconnect();
                MessageBox.Show(e.Message + UiText.T("\n\n如果游戏未加载适配层：请先退出游戏，在“应用与恢复”页应用补丁，再通过 Steam 正常启动一次。"), UiText.T("无法连接游戏运行时"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DisconnectLive()
        {
            if (liveClient != null) liveClient.Disconnect();
            lblLiveConnection.Text = UiText.T("已断开。游戏可以继续运行。");
            lblLiveConnection.ForeColor = Color.DimGray;
            lblLiveGame.Text = UiText.T("游戏: -");
            lblLiveCombat.Text = UiText.T("战斗: -");
            lblLiveSafe.Text = UiText.T("安全时机: -");
            lblLiveRevision.Text = "Revision: -";
        }

        private void RefreshLive(bool reportErrors)
        {
            try
            {
                if (liveClient == null || !liveClient.Connected)
                {
                    if (liveClient == null) liveClient = new LiveClient();
                    liveClient.Connect(gameDir);
                }
                LiveResponse status = liveClient.Status();
                if (!status.Ok)
                {
                    throw new InvalidOperationException(status.Error == null ? "status failed" : status.Error);
                }
                Dictionary<string, object> d = status.Data;
                liveRevision = status.Revision;
                lblLiveConnection.Text = UiText.T("已连接: 127.0.0.1");
                lblLiveConnection.ForeColor = Color.DarkGreen;
                lblLiveGame.Text = UiText.T("游戏: ") + DStr(d, "gameVersion") + UiText.T(" / 层 ") + DStr(d, "layerVersion") + " / PID " + DStr(d, "pid");
                lblLiveCombat.Text = UiText.T("战斗: ") + (DBool(d, "inCombat") ? UiText.T("是") : UiText.T("否")) + " (" + DStr(d, "roomPhase") + ")";
                lblLiveSafe.Text = UiText.T("安全时机: ") + (DBool(d, "safeToApply") ? UiText.T("可以应用") : (DBool(d, "safeToEditDeck") ? UiText.T("地图中可编辑主牌组和牌池") : UiText.T("等待（动作/界面/拖牌中）")));
                lblLiveRevision.Text = "Revision: " + liveRevision;
                LiveResponse snap = liveClient.Snapshot();
                if (!snap.Ok)
                {
                    throw new InvalidOperationException(snap.Error == null ? "snapshot failed" : snap.Error);
                }
                PopulateLiveTree(snap.Data);
                if (reportErrors) SetStatus(UiText.T("实时快照已刷新。"));
            }
            catch (Exception e)
            {
                lblLiveConnection.Text = UiText.T("未连接：") + e.Message;
                lblLiveConnection.ForeColor = Color.Firebrick;
                if (liveClient != null) liveClient.Disconnect();
                if (reportErrors) MessageBox.Show(e.Message, UiText.T("实时连接失败"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void PopulateLiveTree(Dictionary<string, object> snapshot)
        {
            DetectProfession(snapshot);
            lastLiveSnapshot = snapshot;
            liveInstanceItems.Clear();
            object groupsObj;
            Dictionary<string, object> groups = snapshot != null && snapshot.TryGetValue("groups", out groupsObj)
                ? groupsObj as Dictionary<string, object> : null;
            if (groups != null)
            {
                string[] order = new string[] { "masterDeck", "hand", "drawPile", "discardPile", "exhaustPile" };
                string[] labels = new string[] { "主牌组", "手牌", "抽牌堆", "弃牌堆", "消耗堆" };
                for (int i = 0; i < order.Length; i++)
                {
                    object arrObj;
                    IList arr = groups.TryGetValue(order[i], out arrObj) ? arrObj as IList : null;
                    if (arr == null) continue;
                    foreach (object o in arr)
                    {
                        Dictionary<string, object> card = o as Dictionary<string, object>;
                        if (card != null) liveInstanceItems.Add(new LiveInstanceItem(order[i], labels[i], card));
                    }
                }
            }
            ApplyLiveInstanceFilter();
            RefreshRunDeckView(snapshot);
        }

        private void ApplyLiveInstanceFilter()
        {
            if (lstLiveInstances == null) return;
            liveTreeUpdating = true;
            string keep = liveSelectedInstanceId;
            string group = cmbLiveGroupFilter == null ? "全部" : Convert.ToString(cmbLiveGroupFilter.SelectedItem);
            string search = txtLiveSearch == null ? "" : txtLiveSearch.Text.Trim().ToLowerInvariant();
            string groupKey = null;
            if (group == "主牌组") groupKey = "masterDeck";
            else if (group == "手牌") groupKey = "hand";
            else if (group == "抽牌堆") groupKey = "drawPile";
            else if (group == "弃牌堆") groupKey = "discardPile";
            else if (group == "消耗堆") groupKey = "exhaustPile";
            lstLiveInstances.BeginUpdate();
            lstLiveInstances.Items.Clear();
            int selectIndex = -1;
            int firstHand = -1;
            foreach (LiveInstanceItem item in liveInstanceItems)
            {
                if (groupKey != null && item.GroupKey != groupKey) continue;
                if (search.Length > 0)
                {
                    string hay = (item.CardName + " " + item.CardId + " " + item.InstanceId).ToLowerInvariant();
                    if (hay.IndexOf(search) < 0) continue;
                }
                int idx = lstLiveInstances.Items.Add(item);
                if (item.InstanceId == keep) selectIndex = idx;
                if (firstHand < 0 && item.GroupKey == "hand") firstHand = idx;
            }
            bool inCombat = lastLiveSnapshot != null && DBool(lastLiveSnapshot, "inCombat");
            if (selectIndex < 0 && string.IsNullOrEmpty(keep) && inCombat) selectIndex = firstHand;
            if (selectIndex < 0 && lstLiveInstances.Items.Count > 0) selectIndex = 0;
            if (selectIndex >= 0) lstLiveInstances.SelectedIndex = selectIndex;
            lstLiveInstances.EndUpdate();
            liveTreeUpdating = false;
            if (selectIndex >= 0) ShowLiveNode();
            else
            {
                txtLiveInfo.Text = liveInstanceItems.Count == 0
                    ? UiText.T("当前没有可显示的卡牌实例。可能不在局内，或牌区为空。")
                    : UiText.T("没有符合筛选条件的卡牌。请把“牌区”切回“全部”或清空搜索。");
            }
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            return id.Length <= 8 ? id : id.Substring(0, 8);
        }

        private void SelectFirstHandCard()
        {
            if (lstLiveInstances == null || lstLiveInstances.Items.Count == 0)
            {
                MessageBox.Show(UiText.T("还没有牌区快照。请先连接游戏并刷新快照。"), UiText.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            for (int i = 0; i < lstLiveInstances.Items.Count; i++)
            {
                LiveInstanceItem item = lstLiveInstances.Items[i] as LiveInstanceItem;
                if (item != null && item.GroupKey == "hand")
                {
                    lstLiveInstances.SelectedIndex = i;
                    lstLiveInstances.Focus();
                    ShowLiveNode();
                    return;
                }
            }
            MessageBox.Show(UiText.T("当前筛选结果中没有手牌。请把“牌区”切回“全部”或清空搜索。"), UiText.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowLiveNode()
        {
            if (liveTreeUpdating || lstLiveInstances == null) return;
            LiveInstanceItem item = lstLiveInstances.SelectedItem as LiveInstanceItem;
            if (item == null) return;
            Dictionary<string, object> card = item.Card;
            selectedLiveInstance = card;
            string iid = DStr(card, "instanceId");
            bool changed = iid != liveSelectedInstanceId;
            liveSelectedInstanceId = iid;
            selectedLiveIsCustom = DBool(card, "isCustom");
            lblLiveCurrentValues.Text = UiText.T("当前 费用 ") + DStr(card, "cost") + "/" + DStr(card, "costForTurn")
                + UiText.T(" | 伤害 ") + DStr(card, "baseDamage") + "/" + DStr(card, "damage")
                + UiText.T(" | 格挡 ") + DStr(card, "baseBlock") + "/" + DStr(card, "block")
                + " | Magic " + DStr(card, "baseMagicNumber") + "/" + DStr(card, "magicNumber");
            txtLiveInfo.Text = UiText.T("组: ") + item.GroupLabel
                + "\r\ninstanceId: " + iid
                + "\r\ncardId: " + DStr(card, "cardId")
                + "\r\nname: " + DStr(card, "name")
                + "\r\nupgraded: " + DBool(card, "upgraded") + " / times=" + DStr(card, "timesUpgraded")
                + "\r\ncost: " + DStr(card, "cost") + " / costForTurn: " + DStr(card, "costForTurn")
                + "\r\nbaseDamage: " + DStr(card, "baseDamage") + " / current damage: " + DStr(card, "damage")
                + "\r\nbaseBlock: " + DStr(card, "baseBlock") + " / current block: " + DStr(card, "block")
                + "\r\nbaseMagic: " + DStr(card, "baseMagicNumber") + " / current magic: " + DStr(card, "magicNumber")
                + "\r\n\r\nrawDescription:\r\n" + DStr(card, "rawDescription");

            liveSpec = null;
            if (selectedLiveIsCustom)
            {
                object specObj;
                if (card.TryGetValue("spec", out specObj) && specObj != null)
                {
                    try
                    {
                        liveSpec = JsonUtil.FromJson<CustomCardModel>(JsonUtil.ToJson(specObj));
                        if (liveSpec.effects == null) liveSpec.effects = new List<EffectModel>();
                    }
                    catch (Exception e)
                    {
                        txtLiveInfo.AppendText(UiText.T("\r\n\r\nspec 解析失败: ") + e.Message);
                    }
                }
            }
            FillLiveFields(true);
            if (changed)
            {
                chkLiveScopeInstance.Checked = true;
                chkLiveScopeCombat.Checked = false;
                chkLiveScopeMaster.Checked = false;
                chkLiveScopeDefinition.Checked = false;
            }
            lblLiveApplyState.Text = selectedLiveIsCustom
                ? UiText.T("自定义卡：可实时修改数值、名称、描述和效果。")
                : UiText.T("原版卡牌：支持数值覆盖和名称/描述；特殊行为不会被重写。");
            if (selectedLiveIsCustom)
            {
                if (chkLiveAutoDescription.Checked && liveSpec != null && liveSpec.effects != null && liveSpec.effects.Count > 0)
                    RebuildLiveDescription();
                lblLiveDescriptionState.Text = chkLiveAutoDescription.Checked
                    ? UiText.T("自动描述: 开启（改效果后重建）")
                    : UiText.T("自动描述: 关闭（使用手填描述）");
                lblLiveDescriptionState.ForeColor = chkLiveAutoDescription.Checked ? Color.DarkGreen : Color.DarkOrange;
            }
            else
            {
                lblLiveDescriptionState.Text = UiText.T("原版卡: 已填入当前/默认值，可清空后只改部分字段");
                lblLiveDescriptionState.ForeColor = Color.DimGray;
                ReadCurrentLiveValues();
            }
            UpdateLiveScopeSummary();
        }

        private void UpdateLiveScopeSummary()
        {
            if (lblLiveScopeSummary == null) return;
            List<string> scopes = SelectedLiveScopes();
            string text = scopes.Count == 0
                ? UiText.T("当前范围: 未选择（无法应用）")
                : UiText.T("当前范围: ") + string.Join(" + ", scopes.ToArray());
            lblLiveScopeSummary.Text = text;
            lblLiveScopeSummary.ForeColor = scopes.Count == 0 ? Color.Firebrick : Color.SteelBlue;
        }

        private void UpdateLiveDescriptionState()
        {
            if (lblLiveDescriptionState == null) return;
            if (!selectedLiveIsCustom)
            {
                lblLiveDescriptionState.Text = UiText.T("原版卡: 无效果列表，可手填名称/描述");
                lblLiveDescriptionState.ForeColor = Color.DimGray;
                return;
            }
            lblLiveDescriptionState.Text = chkLiveAutoDescription.Checked
                ? UiText.T("自动描述: 开启（改效果后重建）")
                : UiText.T("自动描述: 关闭（使用手填描述）");
            lblLiveDescriptionState.ForeColor = chkLiveAutoDescription.Checked ? Color.DarkGreen : Color.DarkOrange;
        }

        private CatalogCard FindCatalogCard(string id)
        {
            if (catalog == null || catalog.cards == null) return null;
            foreach (CatalogCard c in catalog.cards) if (c.id == id) return c;
            return null;
        }

        private void ReadCurrentLiveValues()
        {
            if (selectedLiveInstance == null)
            {
                MessageBox.Show(UiText.T("请先选择一个具体卡牌实例。"), UiText.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string cardId = DStr(selectedLiveInstance, "cardId");
            if (selectedLiveIsCustom)
            {
                object specObj;
                if (selectedLiveInstance.TryGetValue("spec", out specObj) && specObj != null)
                {
                    liveSpec = JsonUtil.FromJson<CustomCardModel>(JsonUtil.ToJson(specObj));
                    if (liveSpec.effects == null) liveSpec.effects = new List<EffectModel>();
                    FillLiveFields(true);
                    lblLiveApplyState.Text = UiText.T("已填入当前自定义卡定义。留空字段表示不修改。");
                    lblLiveApplyState.ForeColor = Color.DarkGreen;
                }
                return;
            }
            CatalogCard cc = FindCatalogCard(cardId);
            if (cc == null)
            {
                MessageBox.Show(UiText.T("卡牌目录中找不到 ") + cardId, UiText.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            txtLiveName.Text = DStr(selectedLiveInstance, "name").TrimEnd('+');
            txtLiveCost.Text = cc.cost.ToString();
            txtLiveDamage.Text = FmtEdit(cc.damage);
            txtLiveUpDamage.Text = FmtEdit(cc.upgradeDamage);
            txtLiveBlock.Text = FmtEdit(cc.block);
            txtLiveUpBlock.Text = FmtEdit(cc.upgradeBlock);
            txtLiveMagic.Text = FmtEdit(cc.magicNumber);
            txtLiveUpMagic.Text = FmtEdit(cc.upgradeMagicNumber);
            txtLiveDescription.Text = DStr(selectedLiveInstance, "rawDescription");
            txtLiveUpDescription.Text = "";
            lblLiveApplyState.Text = UiText.T("已填入当前卡牌的默认数值/当前描述。留空字段仍表示不修改。");
            lblLiveApplyState.ForeColor = Color.DarkGreen;
        }

        private void UseCurrentLiveDescription()
        {
            if (selectedLiveInstance == null)
            {
                MessageBox.Show(UiText.T("请先选择一个具体卡牌实例。"), UiText.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            txtLiveDescription.Text = DStr(selectedLiveInstance, "rawDescription");
            if (selectedLiveIsCustom && liveSpec != null)
            {
                liveSpec.description = txtLiveDescription.Text;
                chkLiveAutoDescription.Checked = false;
                lblLiveDescriptionState.Text = UiText.T("已使用当前描述，自动描述已关闭。");
                lblLiveDescriptionState.ForeColor = Color.DarkOrange;
            }
            else
            {
                lblLiveDescriptionState.Text = UiText.T("已填入当前描述（原版卡请手动维护）。");
                lblLiveDescriptionState.ForeColor = Color.DarkOrange;
            }
        }

        private void ClearLiveFields()
        {
            txtLiveName.Text = "";
            txtLiveCost.Text = "";
            txtLiveDamage.Text = "";
            txtLiveUpDamage.Text = "";
            txtLiveBlock.Text = "";
            txtLiveUpBlock.Text = "";
            txtLiveMagic.Text = "";
            txtLiveUpMagic.Text = "";
            txtLiveDescription.Text = "";
            txtLiveUpDescription.Text = "";
            lblLiveApplyState.Text = selectedLiveIsCustom
                ? UiText.T("输入已清空；自定义卡至少要重新填写名称、费用和至少一条效果。")
                : UiText.T("输入已清空；空字段表示不修改。");
            lblLiveApplyState.ForeColor = Color.DimGray;
        }

        private void FillLiveFields(bool resetEffects)
        {
            if (selectedLiveIsCustom && liveSpec != null)
            {
                txtLiveName.Text = liveSpec.name;
                txtLiveCost.Text = liveSpec.cost.ToString();
                txtLiveDamage.Text = FmtEdit(liveSpec.damage);
                txtLiveUpDamage.Text = FmtEdit(liveSpec.upgradeDamage);
                txtLiveBlock.Text = FmtEdit(liveSpec.block);
                txtLiveUpBlock.Text = FmtEdit(liveSpec.upgradeBlock);
                txtLiveMagic.Text = FmtEdit(liveSpec.magicNumber);
                txtLiveUpMagic.Text = FmtEdit(liveSpec.upgradeMagicNumber);
                txtLiveDescription.Text = liveSpec.description;
                txtLiveUpDescription.Text = liveSpec.upgradeDescription;
                chkLiveExhaust.Checked = liveSpec.exhaust;
                chkLiveInnate.Checked = liveSpec.innate;
                chkLiveEthereal.Checked = liveSpec.ethereal;
                chkLiveRetain.Checked = liveSpec.retain;
                if (resetEffects) PopulateLiveEffects();
            }
            else
            {
                txtLiveName.Text = "";
                txtLiveCost.Text = "";
                txtLiveDamage.Text = "";
                txtLiveUpDamage.Text = "";
                txtLiveBlock.Text = "";
                txtLiveUpBlock.Text = "";
                txtLiveMagic.Text = "";
                txtLiveUpMagic.Text = "";
                txtLiveDescription.Text = "";
                txtLiveUpDescription.Text = "";
                chkLiveExhaust.Checked = false;
                chkLiveInnate.Checked = false;
                chkLiveEthereal.Checked = false;
                chkLiveRetain.Checked = false;
                if (resetEffects) lstLiveEffects.Items.Clear();
            }
        }

        private void PopulateLiveEffects()
        {
            if (lstLiveEffects == null) return;
            lstLiveEffects.BeginUpdate();
            lstLiveEffects.Items.Clear();
            if (liveSpec != null && liveSpec.effects != null)
            {
                foreach (EffectModel e in liveSpec.effects) lstLiveEffects.Items.Add(EffectSummary(e));
            }
            lstLiveEffects.EndUpdate();
            if (lstLiveEffects.SelectedIndex < 0 && lstLiveEffects.Items.Count > 0)
                lstLiveEffects.SelectedIndex = 0;
        }

        private static string EffectSummary(EffectModel e)
        {
            if (e == null) return UiText.T("(空)");
            StringBuilder sb = new StringBuilder();
            string target = TargetName(e.target);
            if (e.type == "DAMAGE")
            {
                sb.Append(UiText.T("造成 ")).Append(e.amount.HasValue ? e.amount.Value : 0).Append(UiText.T(" 点伤害"));
                if (e.hits.HasValue && e.hits.Value > 1) sb.Append(UiText.T("，重复 ")).Append(e.hits.Value).Append(UiText.T(" 次"));
                if (target.Length > 0) sb.Append("（").Append(target).Append("）");
            }
            else if (e.type == "BLOCK") sb.Append(UiText.T("获得 ")).Append(e.amount.HasValue ? e.amount.Value : 0).Append(UiText.T(" 点格挡"));
            else if (e.type == "DRAW") sb.Append(UiText.T("抽 ")).Append(e.amount.HasValue ? e.amount.Value : 0).Append(UiText.T(" 张牌"));
            else if (e.type == "GAIN_ENERGY") sb.Append(UiText.T("获得 ")).Append(e.amount.HasValue ? e.amount.Value : 0).Append(UiText.T(" 点能量"));
            else if (e.type == "LOSE_HP") sb.Append(UiText.T("失去 ")).Append(e.amount.HasValue ? e.amount.Value : 0).Append(UiText.T(" 点生命"));
            else if (e.type == "HEAL") sb.Append(UiText.T("回复 ")).Append(e.amount.HasValue ? e.amount.Value : 0).Append(UiText.T(" 点生命"));
            else if (e.type == "DISCARD" || e.type == "DISCARD_CARDS") sb.Append(UiText.T("丢弃 ")).Append(e.amount.HasValue ? e.amount.Value : 0).Append(UiText.T(" 张牌"));
            else if (e.type == "EXHAUST" || e.type == "EXHAUST_CARDS") sb.Append(UiText.T("消耗 ")).Append(e.amount.HasValue ? e.amount.Value : 0).Append(UiText.T(" 张牌"));
            else if (e.type == "APPLY_POWER") sb.Append(UiText.T("施加 ")).Append(e.amount.HasValue ? e.amount.Value : 0).Append(UiText.T(" 层")).Append(PowerName(e.power));
            else if (e.type == "START_TURN_BLOCK") sb.Append(UiText.T("回合开始获得 ")).Append(e.amount.HasValue ? e.amount.Value : 0).Append(UiText.T(" 点格挡"));
            else if (e.type == "DRAW_ON_PLAY") sb.Append(UiText.T("每打出一张牌，抽 ")).Append(e.amount.HasValue ? e.amount.Value : 0).Append(UiText.T(" 张牌"));
            else if (e.type == "ALL_CARDS_ZERO_COST") sb.Append(UiText.T("本场战斗所有固定费用牌0费"));
            else if (e.type == "JAVA_EXTENSION") sb.Append(UiText.T("Java 扩展 ")).Append(e.extensionId);
            else if (e.type == "ADD_CARD") sb.Append(UiText.T("生成 ")).Append(e.amount.HasValue ? e.amount.Value : 0).Append(UiText.T(" 张 ")).Append(e.cardId).Append("（").Append(DestinationName(e.destination)).Append("）");
            else sb.Append(e.type);
            if (e.condition != null) sb.Append(UiText.T("；条件：")).Append(ConditionName(e.condition));
            return sb.ToString();
        }

        private static string TargetName(string t)
        {
            if (t == "ENEMY") return UiText.T("单体敌人");
            if (t == "ALL_ENEMY" || t == "ALL") return UiText.T("全体敌人");
            if (t == "SELF") return UiText.T("自己");
            if (t == "SELF_AND_ENEMY") return UiText.T("自己与敌人");
            return "";
        }

        private static string DestinationName(string d)
        {
            if (d == "DRAW" || d == "DRAW_PILE") return "抽牌堆";
            if (d == "DISCARD" || d == "DISCARD_PILE") return "弃牌堆";
            return "手牌";
        }

        private static string PowerName(string p)
        {
            if (p == "Strength") return UiText.T("力量");
            if (p == "Dexterity") return UiText.T("敏捷");
            if (p == "Vulnerable") return UiText.T("易伤");
            if (p == "Weak" || p == "Weakened") return UiText.T("虚弱");
            if (p == "Frail") return UiText.T("脆弱");
            if (p == "Poison") return UiText.T("中毒");
            if (p == "Thorns") return UiText.T("荆棘");
            return p == null ? UiText.T("状态") : p;
        }

        private static string ConditionName(ConditionModel c)
        {
            if (c == null) return "";
            if (c.type == "HAS_POWER") return UiText.T("有") + PowerName(c.power);
            if (c.type == "HAS_BLOCK") return UiText.T("格挡至少 ") + (c.minAmount.HasValue ? c.minAmount.Value : 0);
            if (c.type == "HAND_COUNT_AT_LEAST") return UiText.T("手牌至少 ") + (c.minAmount.HasValue ? c.minAmount.Value : 0);
            if (c.type == "ENERGY_AT_LEAST") return UiText.T("能量至少 ") + (c.minAmount.HasValue ? c.minAmount.Value : 0);
            if (c.type == "ENEMY_COUNT_AT_LEAST") return UiText.T("敌人至少 ") + (c.minAmount.HasValue ? c.minAmount.Value : 0);
            if (c.type == "HP_BELOW_PERCENT") return UiText.T("生命低于 ") + (c.percent.HasValue ? c.percent.Value : 50) + "%";
            return c.type;
        }

        private void LoadSelectedEffect()
        {
            if (liveSpec == null || lstLiveEffects.SelectedIndex < 0 || lstLiveEffects.SelectedIndex >= liveSpec.effects.Count) return;
            EffectModel e = liveSpec.effects[lstLiveEffects.SelectedIndex];
            if (e == null) return;
            cmbLiveEffectType.SelectedItem = e.type;
            numLiveAmount.Value = ClampDecimal(e.amount.HasValue ? e.amount.Value : 0, numLiveAmount.Minimum, numLiveAmount.Maximum);
            numLiveUpAmount.Value = ClampDecimal(e.upgradeAmount.HasValue ? e.upgradeAmount.Value : 0, numLiveUpAmount.Minimum, numLiveUpAmount.Maximum);
            numLiveHits.Value = ClampDecimal(e.hits.HasValue ? e.hits.Value : 1, numLiveHits.Minimum, numLiveHits.Maximum);
            numLiveUpHits.Value = ClampDecimal(e.upgradeHits.HasValue ? e.upgradeHits.Value : 0, numLiveUpHits.Minimum, numLiveUpHits.Maximum);
            cmbLivePower.SelectedItem = e.power == null ? "" : e.power;
            cmbLiveTarget.SelectedItem = string.IsNullOrEmpty(e.target) ? "ENEMY" : e.target;
            txtLiveCardRef.Text = e.type == "JAVA_EXTENSION" ? e.extensionId ?? "" : e.cardId ?? "";
            cmbLiveDestination.SelectedItem = string.IsNullOrEmpty(e.destination) ? "HAND" : e.destination;
            chkLiveCardUpgrade.Checked = e.cardUpgrade.HasValue && e.cardUpgrade.Value;
            cmbLiveCondition.SelectedItem = e.condition == null ? "" : e.condition.type;
            if (e.condition != null)
            {
                int v = e.condition.type == "HP_BELOW_PERCENT"
                    ? (e.condition.percent.HasValue ? e.condition.percent.Value : 50)
                    : (e.condition.minAmount.HasValue ? e.condition.minAmount.Value : 1);
                numLiveConditionMin.Value = ClampDecimal(v, numLiveConditionMin.Minimum, numLiveConditionMin.Maximum);
            }
            UpdateLiveEffectInputs();
        }

        private static decimal ClampDecimal(int value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private EffectModel ReadEffectFromControls()
        {
            EffectModel e = new EffectModel();
            e.type = Convert.ToString(cmbLiveEffectType.SelectedItem);
            int amount = (int)numLiveAmount.Value;
            int upAmount = (int)numLiveUpAmount.Value;
            e.amount = amount;
            e.upgradeAmount = upAmount > 0 ? (int?)upAmount : null;
            e.target = Convert.ToString(cmbLiveTarget.SelectedItem);
            e.power = Convert.ToString(cmbLivePower.SelectedItem);
            if (string.IsNullOrEmpty(e.power)) e.power = null;
            if (e.type == "DRAW_ON_PLAY") e.target = "SELF";
            if (e.type == "ALL_CARDS_ZERO_COST") { e.target = "SELF"; e.amount = 1; e.upgradeAmount = null; }
            if (e.type == "JAVA_EXTENSION") {
                e.extensionId = txtLiveCardRef.Text.Trim();
                if (liveSpec != null && lstLiveEffects.SelectedIndex >= 0 && lstLiveEffects.SelectedIndex < liveSpec.effects.Count)
                    e.parameters = liveSpec.effects[lstLiveEffects.SelectedIndex].parameters;
            }
            if (e.type == "DAMAGE")
            {
                int hits = (int)numLiveHits.Value;
                int upHits = (int)numLiveUpHits.Value;
                e.hits = hits > 1 ? (int?)hits : null;
                e.upgradeHits = upHits > 0 ? (int?)upHits : null;
            }
            if (e.type == "ADD_CARD")
            {
                e.cardId = txtLiveCardRef.Text.Trim();
                e.destination = Convert.ToString(cmbLiveDestination.SelectedItem);
                e.cardUpgrade = chkLiveCardUpgrade.Checked ? (bool?)true : null;
            }
            string ct = Convert.ToString(cmbLiveCondition.SelectedItem);
            if (!string.IsNullOrEmpty(ct))
            {
                ConditionModel c = new ConditionModel();
                c.type = ct;
                int min = (int)numLiveConditionMin.Value;
                if (ct == "HP_BELOW_PERCENT") c.percent = min;
                else c.minAmount = min;
                if (ct == "HAS_POWER") c.power = string.IsNullOrEmpty(e.power) ? "Strength" : e.power;
                e.condition = c;
            }
            return e;
        }

        private void UpdateLiveEffectInputs()
        {
            if (cmbLiveEffectType == null) return;
            string type = Convert.ToString(cmbLiveEffectType.SelectedItem);
            bool damage = type == "DAMAGE";
            bool block = type == "BLOCK";
            bool power = type == "APPLY_POWER";
            bool add = type == "ADD_CARD";
            numLiveHits.Enabled = damage;
            numLiveUpHits.Enabled = damage;
            cmbLiveTarget.Enabled = damage || block || power;
            cmbLivePower.Enabled = power;
            txtLiveCardRef.Enabled = add || type == "JAVA_EXTENSION";
            cmbLiveDestination.Enabled = add;
            chkLiveCardUpgrade.Enabled = add;
            if (lblLiveEffectHint == null) return;
            if (damage)
                lblLiveEffectHint.Text = UiText.T("DAMAGE：造成数值点伤害；目标 ENEMY/ALL_ENEMY；攻击段数 1-10。");
            else if (block)
                lblLiveEffectHint.Text = UiText.T("BLOCK：获得格挡；目标通常为 SELF；敏捷/脆弱由游戏结算。");
            else if (power)
                lblLiveEffectHint.Text = UiText.T("APPLY_POWER：施加状态；先在“状态”下拉选状态，再填层数。");
            else if (add)
                lblLiveEffectHint.Text = UiText.T("ADD_CARD：把指定 cardId 的卡加入手牌/抽牌堆/弃牌堆；必须是已注册卡。");
            else if (type == "START_TURN_BLOCK")
                lblLiveEffectHint.Text = UiText.T("START_TURN_BLOCK：打出后获得一个“每回合开始获得格挡”的能力。");
            else if (type == "DRAW_ON_PLAY")
                lblLiveEffectHint.Text = UiText.T("DRAW_ON_PLAY：打出后获得能力，之后每打出一张牌抽数值张牌。");
            else if (type == "ALL_CARDS_ZERO_COST")
                lblLiveEffectHint.Text = UiText.T("本场战斗固定费用牌0费，覆盖之后抽到/生成的牌；X费和不可打出的牌除外。");
            else if (type == "JAVA_EXTENSION")
                lblLiveEffectHint.Text = UiText.T("在卡牌/扩展 ID 输入已安装的扩展ID；参数可通过JSON导入，使用真实Java代码执行。");
            else if (type == "DRAW")
                lblLiveEffectHint.Text = UiText.T("DRAW：抽牌。");
            else if (type == "GAIN_ENERGY")
                lblLiveEffectHint.Text = UiText.T("GAIN_ENERGY：获得能量。");
            else if (type == "LOSE_HP")
                lblLiveEffectHint.Text = UiText.T("LOSE_HP：直接失去生命，不是受到攻击伤害。");
            else if (type == "HEAL")
                lblLiveEffectHint.Text = UiText.T("HEAL：回复生命。");
            else if (type == "DISCARD_CARDS" || type == "DISCARD")
                lblLiveEffectHint.Text = UiText.T("DISCARD_CARDS：选择并丢弃牌；复用原版选牌界面。");
            else if (type == "EXHAUST_CARDS" || type == "EXHAUST")
                lblLiveEffectHint.Text = UiText.T("EXHAUST_CARDS：选择并消耗牌；复用原版选牌界面。");
            else
                lblLiveEffectHint.Text = UiText.T("选择效果类型后填写参数。");
            lblLiveEffectHint.ForeColor = Color.SteelBlue;
        }

        private void LoadLivePreset()
        {
            if (cmbLivePreset == null) return;
            string p = Convert.ToString(cmbLivePreset.SelectedItem);
            if (p == "伤害 6")
            {
                cmbLiveEffectType.SelectedItem = "DAMAGE";
                numLiveAmount.Value = 6; numLiveUpAmount.Value = 9;
                numLiveHits.Value = 1; numLiveUpHits.Value = 0;
                cmbLiveTarget.SelectedItem = "ENEMY";
            }
            else if (p == "格挡 5")
            {
                cmbLiveEffectType.SelectedItem = "BLOCK";
                numLiveAmount.Value = 5; numLiveUpAmount.Value = 8;
                cmbLiveTarget.SelectedItem = "SELF";
            }
            else if (p == "抽 1 张牌")
            {
                cmbLiveEffectType.SelectedItem = "DRAW";
                numLiveAmount.Value = 1; numLiveUpAmount.Value = 0;
            }
            else if (p == "每打出牌抽 1")
            {
                cmbLiveEffectType.SelectedItem = "DRAW_ON_PLAY";
                numLiveAmount.Value = 1; numLiveUpAmount.Value = 0;
                cmbLiveTarget.SelectedItem = "SELF";
            }
            else if (p == "易伤 2 层")
            {
                cmbLiveEffectType.SelectedItem = "APPLY_POWER";
                numLiveAmount.Value = 2; numLiveUpAmount.Value = 3;
                cmbLivePower.SelectedItem = "Vulnerable";
                cmbLiveTarget.SelectedItem = "ENEMY";
            }
            else if (p == "生成一张牌")
            {
                cmbLiveEffectType.SelectedItem = "ADD_CARD";
                numLiveAmount.Value = 1; numLiveUpAmount.Value = 0;
                cmbLiveDestination.SelectedItem = "HAND";
                if (profile.customCards.Count > 0 && string.IsNullOrEmpty(txtLiveCardRef.Text))
                    txtLiveCardRef.Text = profile.customCards[0].id;
            }
            UpdateLiveEffectInputs();
            lblLiveEffectHint.Text = UiText.T("模板已载入。检查参数后点“添加”；不会自动加入效果列表。");
        }

        private void AddLiveEffect()
        {
            if (liveSpec == null) { MessageBox.Show(UiText.T("请先选择一个自定义卡实例。"), UiText.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            liveSpec.effects.Add(ReadEffectFromControls());
            PopulateLiveEffects();
            SyncStatsFromEffects();
            RebuildLiveDescription();
            lstLiveEffects.SelectedIndex = liveSpec.effects.Count - 1;
        }

        private void UpdateLiveEffect()
        {
            if (liveSpec == null || lstLiveEffects.SelectedIndex < 0) return;
            int i = lstLiveEffects.SelectedIndex;
            liveSpec.effects[i] = ReadEffectFromControls();
            PopulateLiveEffects();
            SyncStatsFromEffects();
            RebuildLiveDescription();
            lstLiveEffects.SelectedIndex = i;
        }

        private void DeleteLiveEffect()
        {
            if (liveSpec == null || lstLiveEffects.SelectedIndex < 0) return;
            int i = lstLiveEffects.SelectedIndex;
            liveSpec.effects.RemoveAt(i);
            PopulateLiveEffects();
            SyncStatsFromEffects();
            RebuildLiveDescription();
            if (lstLiveEffects.Items.Count > 0) lstLiveEffects.SelectedIndex = Math.Min(i, lstLiveEffects.Items.Count - 1);
        }

        private void MoveLiveEffect(int delta)
        {
            if (liveSpec == null || lstLiveEffects.SelectedIndex < 0) return;
            int i = lstLiveEffects.SelectedIndex;
            int j = i + delta;
            if (j < 0 || j >= liveSpec.effects.Count) return;
            EffectModel tmp = liveSpec.effects[i];
            liveSpec.effects[i] = liveSpec.effects[j];
            liveSpec.effects[j] = tmp;
            PopulateLiveEffects();
            lstLiveEffects.SelectedIndex = j;
        }

        private void SyncStatsFromEffects()
        {
            if (liveSpec == null) return;
            foreach (EffectModel e in liveSpec.effects)
            {
                if (e.type == "DAMAGE" && e.amount.HasValue)
                {
                    liveSpec.damage = e.amount;
                    liveSpec.upgradeDamage = e.upgradeAmount;
                    break;
                }
            }
            foreach (EffectModel e in liveSpec.effects)
            {
                if (e.type == "BLOCK" && e.amount.HasValue)
                {
                    liveSpec.block = e.amount;
                    liveSpec.upgradeBlock = e.upgradeAmount;
                    break;
                }
            }
            foreach (EffectModel e in liveSpec.effects)
            {
                if ((e.type == "APPLY_POWER" || e.type == "START_TURN_BLOCK" || e.type == "DRAW_ON_PLAY") && e.amount.HasValue)
                {
                    liveSpec.magicNumber = e.amount;
                    liveSpec.upgradeMagicNumber = e.upgradeAmount;
                    break;
                }
            }
            txtLiveDamage.Text = FmtEdit(liveSpec.damage);
            txtLiveUpDamage.Text = FmtEdit(liveSpec.upgradeDamage);
            txtLiveBlock.Text = FmtEdit(liveSpec.block);
            txtLiveUpBlock.Text = FmtEdit(liveSpec.upgradeBlock);
            txtLiveMagic.Text = FmtEdit(liveSpec.magicNumber);
            txtLiveUpMagic.Text = FmtEdit(liveSpec.upgradeMagicNumber);
        }

        private void RebuildLiveDescription()
        {
            if (liveSpec == null)
            {
                MessageBox.Show(UiText.T("当前选中的是原版卡牌。原版卡没有效果列表；请直接填写描述，或先选择一个自定义卡实例。"),
                    UiText.T("无法从效果生成"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            liveSpec.description = LocalParser.BuildDescription(liveSpec, false);
            liveSpec.upgradeDescription = LocalParser.BuildDescription(liveSpec, true);
            txtLiveDescription.Text = liveSpec.description;
            txtLiveUpDescription.Text = liveSpec.upgradeDescription;
            if (lblLiveDescriptionState != null)
            {
                if (string.IsNullOrEmpty(liveSpec.description))
                {
                    lblLiveDescriptionState.Text = UiText.T("自动描述失败：效果列表为空或效果没有可生成的文本。");
                    lblLiveDescriptionState.ForeColor = Color.Firebrick;
                }
                else
                {
                    lblLiveDescriptionState.Text = UiText.T("已根据 ") + liveSpec.effects.Count + UiText.T(" 条效果生成描述。");
                    lblLiveDescriptionState.ForeColor = Color.DarkGreen;
                }
            }
        }

        private List<string> SelectedLiveScopes()
        {
            List<string> scopes = new List<string>();
            if (chkLiveScopeInstance.Checked) scopes.Add("INSTANCE");
            if (chkLiveScopeCombat.Checked) scopes.Add("COMBAT_ID");
            if (chkLiveScopeMaster.Checked) scopes.Add("MASTER_ID");
            if (chkLiveScopeDefinition.Checked) scopes.Add("DEFINITION");
            return scopes;
        }

        private bool ReadLivePayload(out string kind, out string cardId, out string instanceId, out CustomCardModel spec, out CardOverrideModel ov, out List<string> scopes, out string error)
        {
            kind = null; cardId = null; instanceId = liveSelectedInstanceId; spec = null; ov = null; scopes = SelectedLiveScopes(); error = null;
            if (selectedLiveInstance == null) { error = UiText.T("请先选择一个牌区实例。"); return false; }
            cardId = DStr(selectedLiveInstance, "cardId");
            if (scopes.Count == 0) { error = UiText.T("至少选择一个修改范围。"); return false; }
            if (scopes.Contains("INSTANCE") && string.IsNullOrEmpty(instanceId)) { error = UiText.T("INSTANCE 范围需要具体实例。"); return false; }
            if (selectedLiveIsCustom)
            {
                kind = "custom";
                if (liveSpec == null) { error = UiText.T("没有可用的自定义卡定义。"); return false; }
                if (liveSpec.effects == null) liveSpec.effects = new List<EffectModel>();
                string nameErr;
                int? cost = ParseNullable(txtLiveCost.Text, UiText.T("费用"), -2, 99, out nameErr);
                if (nameErr != null) { error = nameErr; return false; }
                if (!cost.HasValue) { error = UiText.T("自定义卡必须有费用。"); return false; }
                liveSpec.name = txtLiveName.Text.Trim().TrimEnd('+');
                if (liveSpec.name.Length == 0) { error = UiText.T("自定义卡名称不能为空。"); return false; }
                liveSpec.cost = cost.Value;
                liveSpec.damage = ParseNullable(txtLiveDamage.Text, UiText.T("伤害"), 0, 999, out nameErr);
                if (nameErr != null) { error = nameErr; return false; }
                liveSpec.upgradeDamage = ParseNullable(txtLiveUpDamage.Text, UiText.T("升级伤害"), 0, 999, out nameErr);
                if (nameErr != null) { error = nameErr; return false; }
                liveSpec.block = ParseNullable(txtLiveBlock.Text, UiText.T("格挡"), 0, 999, out nameErr);
                if (nameErr != null) { error = nameErr; return false; }
                liveSpec.upgradeBlock = ParseNullable(txtLiveUpBlock.Text, UiText.T("升级格挡"), 0, 999, out nameErr);
                if (nameErr != null) { error = nameErr; return false; }
                liveSpec.magicNumber = ParseNullable(txtLiveMagic.Text, "Magic", 0, 999, out nameErr);
                if (nameErr != null) { error = nameErr; return false; }
                liveSpec.upgradeMagicNumber = ParseNullable(txtLiveUpMagic.Text, UiText.T("升级 Magic"), 0, 999, out nameErr);
                if (nameErr != null) { error = nameErr; return false; }
                liveSpec.description = txtLiveDescription.Text;
                liveSpec.upgradeDescription = txtLiveUpDescription.Text;
                liveSpec.exhaust = chkLiveExhaust.Checked;
                liveSpec.innate = chkLiveInnate.Checked;
                liveSpec.ethereal = chkLiveEthereal.Checked;
                liveSpec.retain = chkLiveRetain.Checked;
                liveSpec.id = cardId;
                liveSpec.enabled = true;
                SyncStatsIntoEffects();
                if (chkLiveAutoDescription.Checked) RebuildLiveDescription();
                else if (string.IsNullOrEmpty(liveSpec.description)) RebuildLiveDescription();
                if (string.IsNullOrEmpty(liveSpec.description))
                {
                    error = UiText.T("无法从当前效果生成描述。请检查效果列表是否为空，或关闭自动描述并手动填写。");
                    return false;
                }
                spec = liveSpec;
                List<string> errs = ProfileStore.ValidateCustomCard(spec, catalog, null);
                if (errs.Count > 0) { error = string.Join("\n", errs.ToArray()); return false; }
            }
            else
            {
                kind = "override";
                ov = new CardOverrideModel();
                string err;
                string newName = txtLiveName.Text.Trim();
                if (newName.Length > 0) ov.name = newName.TrimEnd('+');
                if (txtLiveDescription.Text.Trim().Length > 0) ov.description = txtLiveDescription.Text.Trim();
                ov.cost = ParseNullable(txtLiveCost.Text, UiText.T("费用"), -2, 99, out err); if (err != null) { error = err; return false; }
                ov.damage = ParseNullable(txtLiveDamage.Text, UiText.T("伤害"), 0, 999, out err); if (err != null) { error = err; return false; }
                ov.upgradeDamage = ParseNullable(txtLiveUpDamage.Text, UiText.T("升级伤害"), 0, 999, out err); if (err != null) { error = err; return false; }
                ov.block = ParseNullable(txtLiveBlock.Text, UiText.T("格挡"), 0, 999, out err); if (err != null) { error = err; return false; }
                ov.upgradeBlock = ParseNullable(txtLiveUpBlock.Text, UiText.T("升级格挡"), 0, 999, out err); if (err != null) { error = err; return false; }
                ov.magicNumber = ParseNullable(txtLiveMagic.Text, "Magic", 0, 999, out err); if (err != null) { error = err; return false; }
                ov.upgradeMagicNumber = ParseNullable(txtLiveUpMagic.Text, UiText.T("升级 Magic"), 0, 999, out err); if (err != null) { error = err; return false; }
                if (ov.cost == null && ov.damage == null && ov.upgradeDamage == null && ov.block == null
                    && ov.upgradeBlock == null && ov.magicNumber == null && ov.upgradeMagicNumber == null
                    && string.IsNullOrEmpty(ov.name) && string.IsNullOrEmpty(ov.description))
                {
                    error = UiText.T("至少填写一个要修改的数值、名称或描述字段。");
                    return false;
                }
            }
            return true;
        }

        private void SyncStatsIntoEffects()
        {
            if (liveSpec == null) return;
            SyncStatIntoEffects("DAMAGE", liveSpec.damage, liveSpec.upgradeDamage, liveSpec.target);
            SyncStatIntoEffects("BLOCK", liveSpec.block, liveSpec.upgradeBlock, "SELF");
            if (liveSpec.magicNumber.HasValue)
            {
                bool hasMagic = false;
                foreach (EffectModel e in liveSpec.effects)
                    if (e.type == "APPLY_POWER" || e.type == "START_TURN_BLOCK" || e.type == "DRAW_ON_PLAY") hasMagic = true;
                if (!hasMagic)
                {
                    EffectModel e = new EffectModel();
                    e.type = "APPLY_POWER";
                    e.power = "Vulnerable";
                    e.amount = liveSpec.magicNumber;
                    e.upgradeAmount = liveSpec.upgradeMagicNumber;
                    e.target = liveSpec.target == "ALL_ENEMY" ? "ALL_ENEMY" : "ENEMY";
                    liveSpec.effects.Add(e);
                }
                else
                {
                    foreach (EffectModel e in liveSpec.effects)
                    {
                        if (e.type == "APPLY_POWER" || e.type == "START_TURN_BLOCK" || e.type == "DRAW_ON_PLAY")
                        {
                            e.amount = liveSpec.magicNumber;
                            e.upgradeAmount = liveSpec.upgradeMagicNumber;
                            break;
                        }
                    }
                }
            }
        }

        private void SyncStatIntoEffects(string type, int? amount, int? upgrade, string target)
        {
            if (!amount.HasValue) return;
            foreach (EffectModel e in liveSpec.effects)
            {
                if (e.type == type)
                {
                    e.amount = amount;
                    e.upgradeAmount = upgrade;
                    return;
                }
            }
            EffectModel n = new EffectModel();
            n.type = type;
            n.amount = amount;
            n.upgradeAmount = upgrade;
            n.target = target == null ? "ENEMY" : target;
            liveSpec.effects.Add(n);
        }

        private void ValidateLive()
        {
            string kind, cardId, instanceId, error;
            CustomCardModel spec;
            CardOverrideModel ov;
            List<string> scopes;
            if (!ReadLivePayload(out kind, out cardId, out instanceId, out spec, out ov, out scopes, out error))
            {
                lblLiveApplyState.Text = error;
                lblLiveApplyState.ForeColor = Color.Firebrick;
                return;
            }
            try
            {
                if (liveClient == null || !liveClient.Connected) liveClient = ConnectForLive();
                LiveResponse r = liveClient.Validate(kind, cardId, instanceId, spec, ov, scopes, liveRevision + 1);
                if (!r.Ok) { lblLiveApplyState.Text = UiText.T("校验失败: ") + r.Error; lblLiveApplyState.ForeColor = Color.Firebrick; return; }
                List<string> errs = DataErrors(r.Data);
                if (errs.Count == 0)
                {
                    string counts = FormatScopeCounts(r.Data);
                    lblLiveApplyState.Text = UiText.T("校验通过") + (counts.Length > 0 ? "；" + counts : "") + "。";
                    lblLiveApplyState.ForeColor = Color.DarkGreen;
                }
                else { lblLiveApplyState.Text = UiText.T("校验失败: ") + string.Join("；", errs.ToArray()); lblLiveApplyState.ForeColor = Color.Firebrick; }
            }
            catch (Exception e)
            {
                lblLiveApplyState.Text = UiText.T("校验失败: ") + e.Message;
                lblLiveApplyState.ForeColor = Color.Firebrick;
            }
        }

        private LiveClient ConnectForLive()
        {
            LiveClient c = new LiveClient();
            c.Connect(gameDir);
            liveClient = c;
            return c;
        }

        private void ApplyLive()
        {
            string kind, cardId, instanceId, error;
            CustomCardModel spec;
            CardOverrideModel ov;
            List<string> scopes;
            if (!ReadLivePayload(out kind, out cardId, out instanceId, out spec, out ov, out scopes, out error))
            {
                lblLiveApplyState.Text = error;
                lblLiveApplyState.ForeColor = Color.Firebrick;
                return;
            }
            try
            {
                if (liveClient == null || !liveClient.Connected) liveClient = ConnectForLive();
                LiveResponse v = liveClient.Validate(kind, cardId, instanceId, spec, ov, scopes, liveRevision + 1);
                if (!v.Ok) { lblLiveApplyState.Text = UiText.T("校验失败: ") + v.Error; lblLiveApplyState.ForeColor = Color.Firebrick; return; }
                List<string> errs = DataErrors(v.Data);
                if (errs.Count > 0) { lblLiveApplyState.Text = UiText.T("校验失败: ") + string.Join("；", errs.ToArray()); lblLiveApplyState.ForeColor = Color.Firebrick; return; }

                long revision = Math.Max(liveRevision + 1, DateTime.UtcNow.Ticks);
                lblLiveApplyState.Text = UiText.T("等待游戏线程在安全时机应用...");
                lblLiveApplyState.ForeColor = Color.DarkOrange;
                LiveResponse r = liveClient.Apply(kind, cardId, instanceId, spec, ov, scopes, revision);
                if (!r.Ok)
                {
                    lblLiveApplyState.Text = UiText.T("应用失败: ") + r.Error;
                    lblLiveApplyState.ForeColor = Color.Firebrick;
                    return;
                }
                if (!DBool(r.Data, "applied"))
                {
                    List<string> applyErrors = DataErrors(r.Data);
                    lblLiveApplyState.Text = UiText.T("应用未完成: ") + (applyErrors.Count > 0 ? string.Join("；", applyErrors.ToArray()) : DStr(r.Data, "error"));
                    lblLiveApplyState.ForeColor = Color.Firebrick;
                    return;
                }
                liveRevision = r.Revision;
                lblLiveApplyState.Text = UiText.T("应用成功。revision=") + r.Revision
                    + UiText.T("，受影响实例=") + DStr(r.Data, "affectedInstances")
                    + (DBool(r.Data, "definitionUpdated") ? UiText.T("，定义已更新") : "");
                lblLiveApplyState.ForeColor = Color.DarkGreen;
                Logger.Log("Live apply success requestId=" + r.RequestId + " card=" + cardId
                    + " revision=" + r.Revision + " affected=" + DStr(r.Data, "affectedInstances"));
                if (chkLivePersist.Checked)
                {
                    try
                    {
                        PersistLiveChanges(kind, cardId, spec, ov);
                        lblLiveApplyState.Text += UiText.T("；已写入下次启动定义");
                    }
                    catch (Exception pe)
                    {
                        lblLiveApplyState.Text += UiText.T("；运行时已应用，但保存定义失败: ") + pe.Message;
                        lblLiveApplyState.ForeColor = Color.DarkOrange;
                        Logger.Log("Live persist failed: " + pe.Message);
                    }
                }
                RefreshLive(false);
            }
            catch (Exception e)
            {
                lblLiveApplyState.Text = UiText.T("应用失败: ") + e.Message;
                lblLiveApplyState.ForeColor = Color.Firebrick;
                if (liveClient != null) liveClient.Disconnect();
            }
        }

        private void PersistLiveChanges(string kind, string cardId, CustomCardModel spec, CardOverrideModel ov)
        {
            if (gameDir == null) throw new InvalidOperationException(UiText.T("游戏目录未设置。"));
            if (kind == "custom" && spec != null)
            {
                bool replaced = false;
                for (int i = 0; i < profile.customCards.Count; i++)
                {
                    if (profile.customCards[i].id == cardId)
                    {
                        profile.customCards[i] = spec;
                        replaced = true;
                        break;
                    }
                }
                if (!replaced) profile.customCards.Add(spec);
                ProfileStore.RestoreCustomCard(profile, cardId);
            }
            else if (kind == "override" && ov != null)
            {
                profile.overrides[cardId] = ov;
            }
            ProfileStore.SaveProfile(profile);
            string contentPath = AppPaths.ContentPath(gameDir);
            ContentDoc doc = ProfileStore.BuildContent(profile);
            ContentDoc existing = ProfileStore.ReadContent(contentPath);
            ProfileStore.MergeMissing(doc, existing);
            if (kind == "custom" && spec != null)
            {
                bool replaced = false;
                for (int i = 0; i < doc.customCards.Count; i++)
                {
                    if (doc.customCards[i].id == cardId) { doc.customCards[i] = spec; replaced = true; break; }
                }
                if (!replaced) doc.customCards.Add(spec);
            }
            else if (kind == "override" && ov != null)
            {
                doc.overrides[cardId] = ov;
            }
            JsonUtil.WriteUtf8(contentPath, JsonUtil.ToJson(doc));
            Logger.Log("Live definition persisted to content.json: " + contentPath);
        }

        private static string FormatScopeCounts(Dictionary<string, object> data)
        {
            if (data == null) return "";
            object obj;
            if (!data.TryGetValue("scopeCounts", out obj) || obj == null) return "";
            Dictionary<string, object> c = obj as Dictionary<string, object>;
            if (c == null) return "";
            return UiText.T("目标手牌 ") + DStr(c, "hand")
                + UiText.T("，抽牌堆 ") + DStr(c, "drawPile")
                + UiText.T("，弃牌堆 ") + DStr(c, "discardPile")
                + UiText.T("，消耗堆 ") + DStr(c, "exhaustPile")
                + UiText.T("，主牌组 ") + DStr(c, "masterDeck")
                + UiText.T("，定义 ") + (DBool(c, "definition") ? UiText.T("是") : UiText.T("否"))
                + UiText.T("，合计 ") + DStr(c, "total");
        }

        private static List<string> DataErrors(Dictionary<string, object> data)
        {
            List<string> list = new List<string>();
            if (data == null) return list;
            object obj;
            if (!data.TryGetValue("errors", out obj) || obj == null) return list;
            IList arr = obj as IList;
            if (arr != null) foreach (object o in arr) list.Add(Convert.ToString(o));
            return list;
        }

        private static string DStr(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return "-";
            return Convert.ToString(d[key]);
        }

        private static bool DBool(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return false;
            try { return Convert.ToBoolean(d[key]); } catch { return false; }
        }

        private void BuildApplyTab()
        {
            TabPage p = NewTab(UiText.T("应用与恢复"));
            lblApplyState = LabelAt(UiText.T("状态: -"), 16, 18, 1000);
            lblApplyState.Font = new Font(this.Font, FontStyle.Bold);
            p.Controls.Add(lblApplyState);
            lblApplySummary = LabelAt("", 16, 50, 1000);
            p.Controls.Add(lblApplySummary);
            lblInstallInfo = LabelAt("", 16, 82, 1000);
            p.Controls.Add(lblInstallInfo);

            btnApply = ButtonAt(UiText.T("应用修改到游戏"), 16, 124, 160, 36, delegate { ApplyPatch(); });
            p.Controls.Add(btnApply);
            btnRestore = ButtonAt(UiText.T("恢复原版启动"), 186, 124, 140, 36, delegate { RestoreOriginal(); });
            p.Controls.Add(btnRestore);
            p.Controls.Add(ButtonAt(UiText.T("打开游戏目录"), 336, 124, 130, 36, delegate { OpenGameDir(); }));
            p.Controls.Add(ButtonAt(UiText.T("通过 Steam 启动"), 476, 124, 150, 36, delegate { LaunchGame(); }));

            txtApplyLog = new TextBox();
            txtApplyLog.Left = 16;
            txtApplyLog.Top = 178;
            txtApplyLog.Width = 1032;
            txtApplyLog.Height = 390;
            txtApplyLog.Multiline = true;
            txtApplyLog.ReadOnly = true;
            txtApplyLog.ScrollBars = ScrollBars.Vertical;
            p.Controls.Add(txtApplyLog);
        }

        private void BuildLogsTab()
        {
            TabPage p = NewTab(UiText.T("日志与诊断"));
            txtLog = new TextBox();
            txtLog.Left = 16;
            txtLog.Top = 18;
            txtLog.Width = 1032;
            txtLog.Height = 570;
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Both;
            txtLog.WordWrap = false;
            p.Controls.Add(txtLog);
            p.Controls.Add(ButtonAt(UiText.T("刷新日志"), 16, 600, 110, 30, delegate { RefreshLog(); }));
            p.Controls.Add(ButtonAt(UiText.T("打开日志目录"), 136, 600, 130, 30, delegate { OpenLogDir(); }));
            p.Controls.Add(ButtonAt(UiText.T("导出诊断 ZIP"), 276, 600, 140, 30, delegate { ExportDiagnostics(); }));
        }

        private void DetectGame(string preferred)
        {
            string dir = preferred;
            if (string.IsNullOrEmpty(dir) || !GameLocator.IsGameDir(dir)) dir = GameLocator.FindGameDir();
            if (string.IsNullOrEmpty(dir))
            {
                txtGameDir.Text = "";
                gameDir = null;
                gameInfo = null;
                SetStatus(UiText.T("未找到游戏目录，请手动选择。"));
                UpdateGameLabels();
                return;
            }
            LoadGame(dir);
        }

        private void LoadGame(string dir)
        {
            gameDir = dir;
            txtGameDir.Text = dir;
            try
            {
                gameInfo = GameLocator.GetInfo(dir, catalog);
                if (ProfileStore.ImportGameContent(dir, profile))
                {
                    RefreshCustomList();
                    if(nativeSessionOverrides!=null)foreach(var item in profile.overrides)if(!nativeSessionOverrides.ContainsKey(item.Key))nativeSessionOverrides[item.Key]=JsonUtil.FromJson<CardOverrideModel>(JsonUtil.ToJson(item.Value));
                    RefreshApplyTab();
                }
                UpdateGameLabels();
                RefreshSaveBackups();
                RefreshApplyTab();
                SetStatus(UiText.T("已连接: ") + dir);
                Logger.Log(UiText.T("连接游戏目录: ") + dir + " hash=" + gameInfo.jarHash + " compatible=" + gameInfo.compatible);
            }
            catch (Exception e)
            {
                MessageBox.Show(UiText.T("读取游戏信息失败:\n") + e.Message, UiText.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateGameLabels()
        {
            if (gameInfo == null)
            {
                lblGameVersion.Text = UiText.T("游戏版本: -");
                lblGameBuild.Text = UiText.T("Steam 构建号: -");
                lblJarHash.Text = "desktop-1.0.jar SHA-256: -";
                lblCompat.Text = UiText.T("兼容性: -");
                lblRunning.Text = UiText.T("游戏进程: -");
                lblPatchState.Text = UiText.T("补丁状态: -");
                return;
            }
            lblGameVersion.Text = UiText.T("游戏版本: ") + gameInfo.version;
            lblGameBuild.Text = UiText.T("Steam 构建号: ") + gameInfo.buildId;
            lblJarHash.Text = "desktop-1.0.jar SHA-256: " + gameInfo.jarHash;
            lblCompat.Text = UiText.T("兼容性: ") + gameInfo.compatibilityText;
            lblCompat.ForeColor = gameInfo.compatible ? Color.DarkGreen : Color.Firebrick;
            lblRunning.Text = UiText.T("游戏进程: ") + (gameInfo.running ? UiText.T("正在运行") : UiText.T("未运行"));
            lblPatchState.Text = UiText.T("补丁状态: ") + (gameInfo.patchInstalled ? UiText.T("已应用（wrapper jar，需要重启游戏后生效）") : UiText.T("未应用"));
            lblPatchState.ForeColor = gameInfo.patchInstalled ? Color.DarkOrange : Color.DimGray;
            RefreshApplyTab();
        }

        private void BrowseGame()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = UiText.T("选择 Slay the Spire 安装目录（包含 desktop-1.0.jar）");
                if (d.ShowDialog() == DialogResult.OK)
                {
                    if (GameLocator.IsGameDir(d.SelectedPath)) LoadGame(d.SelectedPath);
                    else MessageBox.Show(UiText.T("该目录不包含 desktop-1.0.jar / config.json / SlayTheSpire.exe。"), UiText.T("目录无效"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OpenGameDir()
        {
            if (gameDir == null || !Directory.Exists(gameDir)) return;
            Process.Start("explorer.exe", "\"" + gameDir + "\"");
        }

        private void LaunchGame()
        {
            try
            {
                Process.Start("steam://rungameid/646570");
                SetStatus(UiText.T("已请求 Steam 启动游戏。请稍候。"));
            }
            catch (Exception e)
            {
                MessageBox.Show(UiText.T("启动 Steam 游戏失败:\n") + e.Message, UiText.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshCardList() { RefreshNativeList(); }

        private void ShowSelectedCard() { ShowNativeSelection(); }

        private static string Fmt(int? v)
        {
            return v.HasValue ? v.Value.ToString() : "-";
        }

        private static string FmtEdit(int? v)
        {
            return v.HasValue ? v.Value.ToString() : "";
        }

        private static int? ParseNullable(string text, string field, int min, int max, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(text.Trim())) return null;
            int v;
            if (!int.TryParse(text.Trim(), out v))
            {
                error = field + UiText.T(" 必须是整数。");
                return null;
            }
            if (v < min || v > max)
            {
                error = field + UiText.T(" 必须在 ") + min + UiText.T(" 到 ") + max + UiText.T(" 之间。");
                return null;
            }
            return v;
        }

        private void ShowErr(string text)
        {
            MessageBox.Show(text, UiText.T("输入错误"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ParseLocal()
        {
            generatedCreativeBuild = null;
            string err;
            CustomCardModel c;
            if (!LocalParser.TryParse(txtPrompt.Text, txtCardName.Text, out c, out err))
            {
                lblAiStatus.Text = UiText.T("解析失败");
                txtPreview.WordWrap = true;
                txtPreview.Text = err;
                generatedCard = null;
                return;
            }
            generatedCard = c;
            c.color=SelectedCreationColor();
            lblAiStatus.Text = UiText.T("本地解析成功。请检查预览后保存。");
            txtPreview.WordWrap = false;
            txtPreview.Text = UiText.T("描述:\r\n") + c.description + UiText.T("\r\n\r\n升级后:\r\n") + c.upgradeDescription + "\r\n\r\nCardSpec:\r\n" + JsonUtil.ToJson(c);
        }

        private void GenerateAi()
        {
            generatedCard = null;
            generatedCreativeBuild = null;
            apiSettings.baseUrl = txtBaseUrl.Text.Trim();
            apiSettings.model = txtModel.Text.Trim();
            apiSettings.apiKey = txtApiKey.Text;
            apiSettings.timeoutSeconds = (int)numTimeout.Value;
            if (string.IsNullOrEmpty(apiSettings.baseUrl) || string.IsNullOrEmpty(apiSettings.model) || string.IsNullOrEmpty(apiSettings.apiKey))
            {
                MessageBox.Show(UiText.T("请先填写 Base URL、模型名称和 API Key。没有 API 时可使用“本地解析（离线）”。"), UiText.T("未配置模型"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            btnAiGenerate.Enabled = false;
            lblAiStatus.ForeColor = SystemColors.ControlText;
            lblAiStatus.Text = UiText.T("正在请求模型...");
            string prompt = txtPrompt.Text;
            bool creativeMode = cmbCreativeMode.SelectedIndex == 0;
            string desiredName = txtCardName.Text;
            string currentGameDir = gameDir;
            double creativity = (double)numCreativity.Value;
            string creationColor=SelectedCreationColor();
            lblAiStatus.Text = creativeMode ? UiText.T("规则整理 → 实现 → 行为检查 → 独立审核（失败最多修复两次）…") : UiText.T("正在生成内置效果…");
            BackgroundWorker w = new BackgroundWorker();
            w.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                e.Result = creativeMode ? (object)CreativeCardService.Generate(apiSettings, prompt, desiredName, currentGameDir, creativity,creationColor)
                    : (object)ApiClient.GenerateCard(apiSettings, prompt,creationColor);
            };
            w.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                btnAiGenerate.Enabled = true;
                if (e.Error != null)
                {
                    lblAiStatus.Text = UiText.T("AI 生成失败");
                    txtPreview.WordWrap = true;
                    txtPreview.Text = e.Error.Message;
                    Logger.Log(UiText.T("AI 生成失败: ") + e.Error.Message);
                    return;
                }
                try
                {
                    CreativeBuildResult creative = e.Result as CreativeBuildResult;
                    string json = creative == null ? Convert.ToString(e.Result) : JsonUtil.ToJson(creative.Card);
                    CustomCardModel c = JsonUtil.FromJson<CustomCardModel>(json);
                    NormalizeGenerated(c, txtCardName.Text);
                    List<string> errs = ProfileStore.ValidateCustomCard(c, catalog, profile);
                    if (errs.Count > 0)
                    {
                        lblAiStatus.Text = UiText.T("模型输出未通过校验");
                        txtPreview.WordWrap = true;
                        txtPreview.Text = string.Join("\r\n", errs.ToArray()) + UiText.T("\r\n\r\n原始 JSON:\r\n") + json;
                        generatedCard = null;
                        return;
                    }
                    generatedCard = c;
                    generatedCreativeBuild = creative;
                    if (creative == null) {
                        c.description = LocalParser.BuildDescription(c, false);
                        c.upgradeDescription = LocalParser.BuildDescription(c, true);
                    }
                    lblAiStatus.Text = creative == null ? UiText.T("AI 生成成功。请检查预览后保存。") : UiText.T("规则/编译/审核完成；行为检查结果见预览，仍需实战验证。");
                    txtPreview.WordWrap = false;
                    txtPreview.Text = UiText.T("描述:\r\n") + c.description + UiText.T("\r\n\r\n升级后:\r\n") + c.upgradeDescription + "\r\n\r\nCardSpec:\r\n" + JsonUtil.ToJson(c);
                    if (creative != null) txtPreview.AppendText(UiText.T("\r\n\r\n检查报告：\r\n")+creative.ValidationReport+UiText.T("\r\n\r\n设计说明：") + creative.DesignSummary + UiText.T("\r\n源码：") + creative.SourceDirectory);
                }
                catch (Exception ex)
                {
                    lblAiStatus.Text = UiText.T("模型输出不是有效的 CardSpec");
                    txtPreview.WordWrap = true;
                    txtPreview.Text = ex.Message;
                }
            };
            w.RunWorkerAsync();
        }

        private void NormalizeGenerated(CustomCardModel c, string name)
        {
            if (c == null) throw new InvalidDataException(UiText.T("模型输出为空。"));
            if (string.IsNullOrEmpty(c.id)) c.id = ProfileStore.NewCardId();
            if (string.IsNullOrEmpty(c.name)) c.name = string.IsNullOrEmpty(name) ? UiText.T("AI 卡牌") : name;
            if(string.IsNullOrEmpty(c.color))c.color = SelectedCreationColor();
            if (string.IsNullOrEmpty(c.type)) c.type = "ATTACK";
            if (string.IsNullOrEmpty(c.rarity)) c.rarity = "COMMON";
            if (string.IsNullOrEmpty(c.target)) c.target = "ENEMY";
            if (string.IsNullOrEmpty(c.image)) c.image = c.type == "POWER" ? "red/power/metallicize" : (c.type == "SKILL" ? "red/skill/shrug_it_off" : "red/attack/bash");
            if (c.effects == null) c.effects = new List<EffectModel>();
            if (c.keywords == null) c.keywords = new List<string>();
            c.enabled = true;
            if (c.damage == null) c.damage = FirstAmount(c.effects, "DAMAGE");
            if (c.upgradeDamage == null) c.upgradeDamage = FirstUpgrade(c.effects, "DAMAGE");
            if (c.block == null) c.block = FirstAmount(c.effects, "BLOCK");
            if (c.upgradeBlock == null) c.upgradeBlock = FirstUpgrade(c.effects, "BLOCK");
            if (c.magicNumber == null) c.magicNumber = FirstAmount(c.effects, "APPLY_POWER");
            if (c.magicNumber == null) c.magicNumber = FirstAmount(c.effects, "START_TURN_BLOCK");
            if (c.magicNumber == null) c.magicNumber = FirstAmount(c.effects, "DRAW_ON_PLAY");
            if (c.upgradeMagicNumber == null) c.upgradeMagicNumber = FirstUpgrade(c.effects, "APPLY_POWER");
            if (c.upgradeMagicNumber == null) c.upgradeMagicNumber = FirstUpgrade(c.effects, "START_TURN_BLOCK");
            if (c.upgradeMagicNumber == null) c.upgradeMagicNumber = FirstUpgrade(c.effects, "DRAW_ON_PLAY");
            if (string.IsNullOrEmpty(c.description)) c.description = LocalParser.BuildDescription(c, false);
            if (string.IsNullOrEmpty(c.upgradeDescription)) c.upgradeDescription = LocalParser.BuildDescription(c, true);
        }

        private static int? FirstAmount(List<EffectModel> fx, string type)
        {
            foreach (EffectModel e in fx) if (e.type == type && e.amount.HasValue) return e.amount;
            return null;
        }

        private static int? FirstUpgrade(List<EffectModel> fx, string type)
        {
            foreach (EffectModel e in fx) if (e.type == type && e.upgradeAmount.HasValue) return e.upgradeAmount;
            return null;
        }

        private void SaveGeneratedCard()
        {
            if (generatedCard == null)
            {
                MessageBox.Show(UiText.T("请先生成一张卡牌。"), UiText.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            NormalizeGenerated(generatedCard, txtCardName.Text);
            Profile temp = new Profile();
            foreach (CustomCardModel c in profile.customCards) if (c.id != generatedCard.id) temp.customCards.Add(c);
            List<string> errs = ProfileStore.ValidateCustomCard(generatedCard, catalog, temp);
            if (errs.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errs.ToArray()), UiText.T("校验失败"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (generatedCreativeBuild != null && generatedCreativeBuild.Card.id == generatedCard.id) {
                try { CreativeCardService.Install(generatedCreativeBuild, gameDir); }
                catch (Exception e) { MessageBox.Show(UiText.T("扩展未安装，未保存卡牌：") + e.Message, UiText.T("自由设计"), MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            }
            bool replaced = false;
            for (int i = 0; i < profile.customCards.Count; i++)
            {
                if (profile.customCards[i].id == generatedCard.id)
                {
                    profile.customCards[i] = generatedCard;
                    replaced = true;
                    break;
                }
            }
            if (!replaced) profile.customCards.Add(generatedCard);
            ProfileStore.RestoreCustomCard(profile, generatedCard.id);
            ProfileStore.SaveProfile(profile);
            RefreshCustomList();
            lblAiStatus.Text = UiText.T("已保存到自定义卡库（尚未应用到游戏）。");
            SetStatus(UiText.T("自定义卡已保存。请到“自定义卡库”或“应用与恢复”继续。"));
        }

        private static bool IsDeepSeekWebAddress(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string s = url.ToLowerInvariant();
            return s.Contains("platform.deepseek.com")
                || s.Contains("www.deepseek.com")
                || s.Contains("chat.deepseek.com");
        }

        private void FillDeepSeekPreset()
        {
            txtBaseUrl.Text = "https://api.deepseek.com/v1";
            txtModel.Text = "deepseek-chat";
            lblAiStatus.ForeColor = Color.DarkGreen;
            lblAiStatus.Text = UiText.T("已填入官方 API 地址；确认 Key 后测试连接。");
        }

        private void SaveApiSettings()
        {
            apiSettings.baseUrl = txtBaseUrl.Text.Trim();
            apiSettings.model = txtModel.Text.Trim();
            apiSettings.apiKey = txtApiKey.Text;
            apiSettings.timeoutSeconds = (int)numTimeout.Value;
            try
            {
                ProfileStore.SaveSettings(apiSettings);
                MessageBox.Show(UiText.T("模型设置已保存（API Key 已加密）。"), UiText.T("已保存"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception e)
            {
                MessageBox.Show(UiText.T("保存失败:\n") + e.Message, UiText.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TestApi()
        {
            apiSettings.baseUrl = txtBaseUrl.Text.Trim();
            apiSettings.model = txtModel.Text.Trim();
            apiSettings.apiKey = txtApiKey.Text;
            apiSettings.timeoutSeconds = (int)numTimeout.Value;
            btnTestApi.Enabled = false;
            lblAiStatus.ForeColor = SystemColors.ControlText;
            lblAiStatus.Text = UiText.T("正在测试连接...");
            BackgroundWorker w = new BackgroundWorker();
            w.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = ApiClient.TestConnection(apiSettings); };
            w.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                btnTestApi.Enabled = true;
                if (e.Error != null)
                {
                    lblAiStatus.Text = UiText.T("连接失败");
                    txtPreview.WordWrap = true;
                    txtPreview.Text = e.Error.Message;
                }
                else
                {
                    lblAiStatus.Text = UiText.T("连接成功");
                    txtPreview.WordWrap = false;
                    txtPreview.Text = UiText.T("连接测试响应:\r\n") + Convert.ToString(e.Result);
                }
            };
            w.RunWorkerAsync();
        }

        private void RefreshCustomList()
        {
            if (lstCustom == null) return;
            HashSet<string> selectedIds=new HashSet<string>();foreach(CustomListItem selected in lstCustom.SelectedItems)selectedIds.Add(selected.Card.id);
            int oldIndex = lstCustom.SelectedIndex;
            lstCustom.BeginUpdate();
            lstCustom.Items.Clear();
            foreach (CustomCardModel c in profile.customCards)
            {
                lstCustom.Items.Add(new CustomListItem(c));
                if (selectedIds.Contains(c.id)) lstCustom.SetSelected(lstCustom.Items.Count-1,true);
            }
            if (lstCustom.SelectedIndex < 0 && lstCustom.Items.Count > 0) lstCustom.SelectedIndex = Math.Min(Math.Max(oldIndex, 0), lstCustom.Items.Count - 1);
            lstCustom.EndUpdate();
            RefreshDeckSources();
            ShowCustomCardPreview();
            List<string> hiddenPoolIds=new List<string>();
            foreach(var source in poolSources)if(source.Value.Custom && !profile.customCards.Exists(c=>c.id==source.Key && c.enabled))hiddenPoolIds.Add(source.Key);
            foreach(string id in hiddenPoolIds)poolSources.Remove(id);
            bool renamed=false;foreach(CustomCardModel c in profile.customCards)if(c.enabled && poolSources.ContainsKey(c.id) && poolSources[c.id].Name!=c.name){poolSources[c.id]=new DeckSource(c.id,c.name,true);renamed=true;}
            if(hiddenPoolIds.Count>0||renamed)RenderPoolDraft();
        }

        private void ExportCustom()
        {
            CustomListItem item = lstCustom.SelectedItem as CustomListItem;
            if (item == null) return;
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "CardSpec JSON|*.json";
                d.FileName = item.Card.id + ".json";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    JsonUtil.WriteUtf8(d.FileName, JsonUtil.ToJson(item.Card));
                    SetStatus(UiText.T("已导出 ") + d.FileName);
                }
            }
        }

        private void ImportCustom()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = UiText.T("CardSpec JSON|*.json|所有文件|*.*");
                if (d.ShowDialog() != DialogResult.OK) return;
                try
                {
                    CustomCardModel c = JsonUtil.FromJson<CustomCardModel>(JsonUtil.ReadUtf8(d.FileName));
                    NormalizeGenerated(c, c.name);
                    Profile temp = new Profile();
                    foreach (CustomCardModel x in profile.customCards) if (x.id != c.id) temp.customCards.Add(x);
                    List<string> errs = ProfileStore.ValidateCustomCard(c, catalog, temp);
                    if (errs.Count > 0) { MessageBox.Show(string.Join("\n", errs.ToArray()), UiText.T("校验失败"), MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    bool replaced = false;
                    for (int i = 0; i < profile.customCards.Count; i++) if (profile.customCards[i].id == c.id) { profile.customCards[i] = c; replaced = true; break; }
                    if (!replaced) profile.customCards.Add(c);
                    ProfileStore.RestoreCustomCard(profile, c.id);
                    ProfileStore.SaveProfile(profile);
                    RefreshCustomList();
                    SetStatus(UiText.T("已导入 ") + c.name);
                }
                catch (Exception e)
                {
                    MessageBox.Show(UiText.T("导入失败:\n") + e.Message, UiText.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void RefreshSaveBackups()
        {
            if (lstSaveBackups == null) return;
            lstSaveBackups.Items.Clear();
            foreach (string f in SaveEditor.ListBackups()) lstSaveBackups.Items.Add(f);
        }

        private bool IsCustomCardInAnySave(string id)
        {
            if (gameDir == null || string.IsNullOrEmpty(id)) return false;
            foreach (string save in SaveEditor.FindSaves(gameDir))
            {
                try
                {
                    if (SaveEditor.ReadDeck(save).Contains(id)) return true;
                }
                catch
                {
                }
            }
            return false;
        }

        /**
         * The game save stores only a card ID. If content.json no longer has the
         * matching definition, CardGroup.loadCard falls back to Madness ("疯狂").
         * Keep the editor profile and the installed content in sync before a save
         * is modified, and preserve definitions already present on either side.
         */
        private ContentDoc SyncCustomDefinitions()
        {
            if (gameDir == null) throw new InvalidOperationException(UiText.T("游戏目录未设置。"));
            ProfileStore.ImportGameContent(gameDir, profile);
            string contentPath = AppPaths.ContentPath(gameDir);
            ContentDoc existing = ProfileStore.ReadContent(contentPath);
            ContentDoc doc = ProfileStore.BuildContent(profile);
            ProfileStore.MergeMissing(doc, existing);
            JsonUtil.WriteUtf8(contentPath, JsonUtil.ToJson(doc));
            Logger.Log(UiText.T("同步卡牌定义: ") + contentPath + " custom=" + doc.customCards.Count + " overrides=" + doc.overrides.Count);
            return doc;
        }

        private static bool ContainsCustomCard(ContentDoc doc, string id)
        {
            if (doc == null || doc.customCards == null || string.IsNullOrEmpty(id)) return false;
            foreach (CustomCardModel c in doc.customCards)
            {
                if (c != null && c.id == id) return true;
            }
            return false;
        }

        private List<string> FindMissingSaveDefinitions(ContentDoc doc)
        {
            List<string> missing = new List<string>();
            if (gameDir == null) return missing;
            foreach (string save in SaveEditor.FindSaves(gameDir))
            {
                try
                {
                    foreach (string id in SaveEditor.ReadDeck(save))
                    {
                        if (!string.IsNullOrEmpty(id) && id.StartsWith("stseditor_")
                            && !ContainsCustomCard(doc, id) && !missing.Contains(id))
                        {
                            missing.Add(id);
                        }
                    }
                }
                catch
                {
                }
            }
            return missing;
        }

        private void SyncDefinitionsToGame()
        {
            if (gameDir == null) { MessageBox.Show(UiText.T("请先选择游戏目录。"), UiText.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (gameInfo == null || !gameInfo.patchInstalled)
            {
                MessageBox.Show(UiText.T("还没有安装适配层。请先在“应用与恢复”页应用修改。"), UiText.T("请先应用适配层"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (GameLocator.IsGameRunning()) { SyncDefinitionsLive(); return; }
            try
            {
                ContentDoc doc = SyncCustomDefinitions();
                RefreshCustomList();
                List<string> missing = FindMissingSaveDefinitions(doc);
                if (missing.Count > 0)
                {
                    MessageBox.Show(UiText.T("已同步现有定义，但存档里仍有这些自定义卡找不到定义：\n\n")
                        + string.Join("\n", missing.ToArray())
                        + UiText.T("\n\n它们在游戏里会显示成“疯狂”。需要从备份恢复这些卡的定义，或恢复一个不含它们的存档。"),
                        UiText.T("仍有定义缺失"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show(UiText.T("已把编辑器里的自定义卡定义同步到游戏，当前存档里的自定义卡 ID 都能找到定义。\n\n请重启游戏并选择“继续”。"),
                        UiText.T("同步完成"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                SetStatus(UiText.T("卡牌定义已同步，重启游戏后生效。"));
            }
            catch (Exception e)
            {
                MessageBox.Show(UiText.T("同步失败:\n") + e.Message, UiText.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SyncDefinitionsLive()
        {
            if(definitionSyncBusy) return;
            List<CustomCardModel> cards=new List<CustomCardModel>();
            foreach(CustomCardModel card in profile.customCards) if(card!=null && card.enabled)
                cards.Add(JsonUtil.FromJson<CustomCardModel>(JsonUtil.ToJson(card)));
            if(cards.Count==0) { SetStatus(UiText.T("没有启用的自定义卡牌可同步。")); return; }
            string directory=gameDir, contentPath=AppPaths.ContentPath(gameDir);
            definitionSyncBusy=true; tabs.Enabled=false; SetStatus(UiText.T("正在向当前游戏注册自定义卡定义…"));
            BackgroundWorker worker=new BackgroundWorker();
            worker.DoWork += delegate(object sender,DoWorkEventArgs e) {
                using(LiveClient client=new LiveClient()) { client.Connect(directory); e.Result=LiveDefinitionSync.Sync(client,cards,contentPath); }
            };
            worker.RunWorkerCompleted += delegate(object sender,RunWorkerCompletedEventArgs e) {
                definitionSyncBusy=false; tabs.Enabled=true;
                if(e.Error!=null) {
                    SetStatus(UiText.T("实时同步失败：")+e.Error.Message);
                    MessageBox.Show(e.Error.Message,UiText.T("实时同步失败"),MessageBoxButtons.OK,MessageBoxIcon.Warning);
                } else {
                    DefinitionSyncResult result=(DefinitionSyncResult)e.Result;
                    if(result.CatalogData!=null) UpdatePoolSources(DList(result.CatalogData,"cards"));
                    string message=UiText.T("已实时同步 ")+result.AppliedIds.Count+" / "+cards.Count+UiText.T(" 张自定义卡。");
                    if(result.Error!=null) message+=UiText.T("\n未完全完成：")+result.Error;
                    else message+=UiText.T("\n无需退出游戏。到“本局可获得牌池”点“连接 / 读取本局牌池”，即可搜索并加入新卡。");
                    if(result.Persisted) message+=UiText.T("\n定义已保存，之后重启仍可加载。");
                    SetStatus(message.Replace("\n"," "));
                    MessageBox.Show(message,result.Error==null?UiText.T("实时同步完成"):UiText.T("实时同步部分完成"),MessageBoxButtons.OK,result.Error==null?MessageBoxIcon.Information:MessageBoxIcon.Warning);
                    Logger.Log(UiText.T("实时同步自定义卡 registered=")+result.AppliedIds.Count+" persisted="+result.Persisted+" error="+(result.Error??"none"));
                }
                worker.Dispose();
            }; worker.RunWorkerAsync();
        }

        private void RestoreSave()
        {
            if (lstSaveBackups.SelectedItem == null) { MessageBox.Show(UiText.T("请选择一个备份。"), UiText.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (gameDir == null) { MessageBox.Show(UiText.T("请先选择游戏目录。"), UiText.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string backup = Convert.ToString(lstSaveBackups.SelectedItem);
            string save = Path.Combine(Path.Combine(gameDir, "saves"), Path.GetFileName(backup));
            if (MessageBox.Show(UiText.T("确定用备份覆盖当前存档吗？\n") + save, UiText.T("确认"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                SaveEditor.Restore(backup, save);
                SetStatus(UiText.T("存档已恢复。"));
            }
            catch (Exception e)
            {
                MessageBox.Show(UiText.T("恢复存档失败:\n") + e.Message, UiText.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshApplyTab()
        {
            if (lblApplyState == null) return;
            if (gameInfo == null)
            {
                lblApplyState.Text = UiText.T("状态: 未连接游戏");
                lblApplySummary.Text = "";
                lblInstallInfo.Text = "";
                btnApply.Enabled = false;
                btnRestore.Enabled = false;
                return;
            }
            btnApply.Enabled = gameInfo.compatible && !gameInfo.running;
            btnRestore.Enabled = !gameInfo.running;
            lblApplyState.Text = UiText.T("状态: ") + (gameInfo.patchInstalled ? UiText.T("补丁已安装（需要重启游戏）") : UiText.T("未安装补丁"))
                + (gameInfo.running ? UiText.T("，游戏正在运行") : "");
            lblApplyState.ForeColor = gameInfo.patchInstalled ? Color.DarkOrange : Color.DimGray;
            int overrides = profile.overrides.Count;
            int custom = 0;
            foreach (CustomCardModel c in profile.customCards) if (c.enabled) custom++;
            lblApplySummary.Text = UiText.T("编辑器配置: ") + overrides + UiText.T(" 张原版卡牌覆盖，") + custom + UiText.T(" 张启用的自定义卡。");
            string info = PatchService.GetInstallInfo(gameDir);
            lblInstallInfo.Text = info == null ? UiText.T("最近安装记录: 无") : UiText.T("最近安装记录: ") + info;
            if (txtApplyLog != null)
            {
                txtApplyLog.Text = UiText.T("游戏目录: ") + gameDir + "\r\n"
                    + UiText.T("兼容性: ") + gameInfo.compatibilityText + "\r\n"
                    + UiText.T("补丁: ") + (gameInfo.patchInstalled ? UiText.T("已安装（wrapper jar）") : UiText.T("未安装")) + "\r\n"
                    + UiText.T("说明: 应用后请通过 Steam 正常启动，启动选项选择“玩 Slay the Spire”。\r\n")
                    + UiText.T("恢复原版会把 desktop-1.0-original.jar 改回 desktop-1.0.jar 并删除 patch.jar；不会自动删除存档。");
            }
        }

        private void ApplyPatch()
        {
            if (gameInfo == null || !gameInfo.compatible)
            {
                MessageBox.Show(UiText.T("游戏版本不兼容，已停止写入。\n") + (gameInfo == null ? "" : gameInfo.compatibilityText), UiText.T("拒绝写入"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (GameLocator.IsGameRunning())
            {
                MessageBox.Show(UiText.T("游戏正在运行。请先退出游戏。"), UiText.T("请先退出游戏"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show(UiText.T("将把 desktop-1.0.jar 重命名为 desktop-1.0-original.jar，并安装一个启动包装器。\n\nSteam 正常启动时会通过包装器加载补丁；这不是“完全未修改”的原版，工具会保留原版 jar 和 config.json 备份。\n确定继续吗？"), UiText.T("确认应用"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            btnApply.Enabled = false;
            btnRestore.Enabled = false;
            txtApplyLog.Text = UiText.T("正在构建补丁...");
            BackgroundWorker w = new BackgroundWorker();
            w.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                PatchService.Apply(gameDir, catalog, profile, gameInfo);
            };
            w.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                btnApply.Enabled = true;
                btnRestore.Enabled = true;
                if (e.Error != null)
                {
                    txtApplyLog.Text = UiText.T("应用失败:\r\n") + e.Error.Message;
                    MessageBox.Show(UiText.T("应用失败:\n") + e.Error.Message, UiText.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Logger.Log(UiText.T("应用失败: ") + e.Error.Message);
                    return;
                }
                LoadGame(gameDir);
                txtApplyLog.Text = UiText.T("应用成功。\r\n\r\n请通过 Steam 正常启动游戏，在启动选项中选择“玩 Slay the Spire”（不要选择 Mod 启动入口）。\r\n")
                    + UiText.T("进入游戏后选择“继续”，自定义卡牌应出现在牌组中，原版卡牌数值按编辑器配置生效。\r\n")
                    + UiText.T("如果游戏更新，请恢复原版并重新生成目录/补丁。");
                MessageBox.Show(UiText.T("适配层已安装。请通过 Steam 正常启动游戏。\r\n进入本局后可实时修改卡牌、编辑主牌组，或设置卡牌奖励与商店牌池。"), UiText.T("完成"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            w.RunWorkerAsync();
        }

        private void RestoreOriginal()
        {
            if (gameInfo == null) return;
            if (GameLocator.IsGameRunning())
            {
                MessageBox.Show(UiText.T("游戏正在运行。请先退出游戏。"), UiText.T("请先退出游戏"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show(UiText.T("恢复原版会把 desktop-1.0-original.jar 改回 desktop-1.0.jar，并删除 patch.jar。\n不会删除存档或编辑器配置。\n确定继续吗？"), UiText.T("确认恢复"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                string source = PatchService.Restore(gameDir);
                LoadGame(gameDir);
                txtApplyLog.Text = UiText.T("已恢复原版启动。\r\n来源: ") + source + UiText.T("\r\n\r\n下次通过 Steam 启动即为原版。");
                MessageBox.Show(UiText.T("已恢复原版启动。\r\n\r\n来源:\n") + source, UiText.T("完成"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception e)
            {
                MessageBox.Show(UiText.T("恢复失败:\n") + e.Message, UiText.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshLog()
        {
            if (txtLog == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(UiText.T("=== 应用日志 ==="));
            sb.AppendLine(Logger.Tail(200000));
            if (gameDir != null)
            {
                sb.AppendLine();
                sb.AppendLine(UiText.T("=== 游戏内运行时日志 (sts-card-editor/runtime.log) ==="));
                string r = AppPaths.RuntimeLogPath(gameDir);
                if (File.Exists(r))
                {
                    string text = File.ReadAllText(r, Encoding.UTF8);
                    if (text.Length > 200000) text = text.Substring(text.Length - 200000);
                    sb.AppendLine(text);
                }
                else
                {
                    sb.AppendLine(UiText.T("(尚未生成)"));
                }
            }
            txtLog.Text = sb.ToString();
        }

        private void OpenLogDir()
        {
            AppPaths.EnsureDataDirs();
            Process.Start("explorer.exe", "\"" + AppPaths.DataDir + "\"");
        }

        private void ExportDiagnostics()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "ZIP|*.zip";
                d.FileName = "sts-card-editor-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip";
                if (d.ShowDialog() != DialogResult.OK) return;
                try
                {
                    using (ZipArchive zip = ZipFile.Open(d.FileName, ZipArchiveMode.Create))
                    {
                        AddZipFile(zip, AppPaths.AppLogPath, "app.log");
                        if (gameDir != null) AddZipFile(zip, AppPaths.RuntimeLogPath(gameDir), "runtime.log");
                        AddZipFile(zip, AppPaths.ProfilePath, "profile.json");
                        AddZipFile(zip, AppPaths.CatalogPath, "catalog.json");
                        if (gameDir != null) AddZipFile(zip, Path.Combine(gameDir, "config.json"), "config.json");
                        StringBuilder info = new StringBuilder();
                        info.AppendLine("generated=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        if (gameInfo != null)
                        {
                            info.AppendLine("gameDir=" + gameInfo.directory);
                            info.AppendLine("buildId=" + gameInfo.buildId);
                            info.AppendLine("jarHash=" + gameInfo.jarHash);
                            info.AppendLine("compatible=" + gameInfo.compatible);
                            info.AppendLine("patchInstalled=" + gameInfo.patchInstalled);
                            info.AppendLine("running=" + gameInfo.running);
                        }
                        ZipArchiveEntry e = zip.CreateEntry("diagnostics.txt");
                        using (StreamWriter w = new StreamWriter(e.Open(), Encoding.UTF8)) w.Write(info.ToString());
                    }
                    SetStatus(UiText.T("诊断已导出: ") + d.FileName);
                    MessageBox.Show(UiText.T("诊断已导出:\n") + d.FileName, UiText.T("完成"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception e)
                {
                    MessageBox.Show(UiText.T("导出失败:\n") + e.Message, UiText.T("错误"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static void AddZipFile(ZipArchive zip, string path, string entryName)
        {
            if (path == null || !File.Exists(path)) return;
            try
            {
                ZipArchiveEntry e = zip.CreateEntry(entryName);
                using (Stream dst = e.Open())
                using (FileStream src = File.OpenRead(path)) src.CopyTo(dst);
            }
            catch
            {
            }
        }

        private class LiveInstanceItem
        {
            public readonly string GroupKey;
            public readonly string GroupLabel;
            public readonly Dictionary<string, object> Card;
            public LiveInstanceItem(string groupKey, string groupLabel, Dictionary<string, object> card)
            {
                GroupKey = groupKey;
                GroupLabel = groupLabel;
                Card = card;
            }
            public string CardName { get { return DStr(Card, "name"); } }
            public string CardId { get { return DStr(Card, "cardId"); } }
            public string InstanceId { get { return DStr(Card, "instanceId"); } }
            public override string ToString()
            {
                string name = CardName;
                bool up = DBool(Card, "upgraded");
                if (up && !name.EndsWith("+")) name += "+";
                return "[" + UiText.T(GroupLabel) + "] " + name + "  [" + CardId + "]  #" + ShortId(InstanceId);
            }
        }

        private class CardListItem
        {
            public readonly CatalogCard Card;
            public CardListItem(CatalogCard c) { Card = c; }
            public override string ToString()
            {
                string s = UiText.NativeCard(Card).name + " · "+Professions.Name(Card.color)+"  [" + Card.id + "]";
                if (Card.special) s += UiText.T("  （特殊机制）");
                return s;
            }
        }

        private class CustomListItem
        {
            public readonly CustomCardModel Card;
            public CustomListItem(CustomCardModel c) { Card = c; }
            public override string ToString()
            {
                return (Card.enabled ? UiText.T("[启用] ") : UiText.T("[停用] ")) + Card.name + " · "+Professions.Name(Card.color)+"  (" + Card.cost + UiText.T("费)  ") + Card.id;
            }
        }
    }
}

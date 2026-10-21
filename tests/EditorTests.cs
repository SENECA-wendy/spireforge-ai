using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using StsCardEditor;

class EditorTests
{
    static int passed;
    static void Check(bool result, string name) { if (!result) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static object Field(object obj, string name) { return obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj); }
    static void SetField(object obj, string name, object value) { obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value); }
    static object Call(object obj, string name, params object[] args) { return obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args); }
    static string Read(string path) { return File.ReadAllText(path, Encoding.UTF8); }

    [STAThread]
    static void Main()
    {
        try { RunTests(); }
        catch (Exception error) { Console.WriteLine("FAIL " + error.GetType().Name + ": " + error.Message); if (error.InnerException != null) Console.WriteLine(error.InnerException.Message); Environment.ExitCode = 1; }
    }
    static void RunTests()
    {
        string path = Path.Combine(AppPaths.DataDir, "library-tests.json");
        DeckLibrary library = new DeckLibrary();
        DeckPreset preset = new DeckPreset { name = "测试卡组" };
        preset.entries.Add(new DeckEntry { cardId = "Strike_R", name = "打击", count = 3, upgrades = 1, misc = 7 });
        preset.customCards.Add(new CustomCardModel { id = "stseditor_fixture", name = "自定义样例" });
        library.decks.Add(preset);
        DeckStore.Save(path, library);
        DeckLibrary loaded = DeckStore.Load(path);
        Check(loaded.decks.Count == 1 && loaded.decks[0].entries[0].count == 3 && loaded.decks[0].entries[0].upgrades == 1 && loaded.decks[0].entries[0].misc == 7 && loaded.decks[0].customCards[0].id == "stseditor_fixture", "preset round trip preserves counts, upgrades, misc, custom definitions");
        loaded.decks.Clear(); DeckStore.Save(path, loaded);
        Check(DeckStore.Load(path).decks.Count == 0, "deleted preset stays deleted after reload");
        bool recovered = false;
        foreach (string backup in Directory.GetFiles(Path.Combine(AppPaths.DataDir, "backups"), "decks-*.json"))
            if (DeckStore.Load(backup).decks.Count == 1) recovered = true;
        Check(recovered, "preset deletion has readable recovery backup");
        library.decks[0].entries[0].count = -1;
        string before = Read(path); bool rejected = false;
        try { DeckStore.Save(path, library); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && before == Read(path), "invalid preset cannot overwrite saved library");
        string badPath = Path.Combine(AppPaths.DataDir, "malformed.json"); File.WriteAllText(badPath, "{bad");
        rejected = false; try { DeckStore.Load(badPath); } catch { rejected = true; }
        Check(rejected && Read(badPath) == "{bad", "malformed library is preserved for recovery");

        TestLiveClient();
        CustomCardModel zero; string parseError;
        Check(LocalParser.TryParse("0费能力，本场战斗你的所有牌能量消耗为0费。", "空城计", out zero, out parseError)
            && CardGenerationRules.HasEffect(zero, "ALL_CARDS_ZERO_COST") && zero.type == "POWER", "natural-language zero-cost request compiles to real cost effect");
        Check(zero.description.Contains("本场战斗") && zero.description.Contains("X费与不可打出的牌除外"), "zero-cost description states duration and special-cost exclusions");
        CustomCardModel fake = new CustomCardModel { id = "stseditor_fake", name = "错误占位", description = "你的所有牌能量消耗为0费。" };
        fake.effects.Add(new EffectModel { type = "APPLY_POWER", power = "Dexterity", amount = 1, target = "SELF" });
        Check(CardGenerationRules.ValidateIntent(fake.description, fake).Count > 0 && ProfileStore.ValidateCustomCard(fake, null, null).Count > 0, "Dexterity placeholder cannot masquerade as zero-cost effect");
        Check(!LocalParser.TryParse("永久让你的所有牌变为0费", "永久", out fake, out parseError), "unsupported permanent duration is rejected instead of substituted");
        Check(ProfileStore.SupportedEffect("JAVA_EXTENSION"), "advanced compiled extension node is accepted by schema");
        TestCreativeCompiler();

        Application.EnableVisualStyles();
        using (MainForm form = new MainForm())
        {
            TabControl tabs = (TabControl)Field(form, "tabs");
            TestCompactUi(form);
            Check(((ComboBox)Field(form, "cmbCreativeMode")).SelectedIndex == 0, "AI defaults to compiled free design mode");
            Check(((NumericUpDown)Field(form, "numCreativity")).Value == 1, "AI creative setting defaults to its maximum");
            Check(tabs.TabPages.Cast<TabPage>().Any(p=>p.Text=="本局可获得牌池"), "editor exposes a separate acquisition pool tab");
            SetField(form,"poolRunKey","fixture-pool-run"); Call(form,"SetStrikeOnlyPool");
            Dictionary<string,object> poolPayload=(Dictionary<string,object>)Call(form,"CardPoolPayload",true);
            Check((string)poolPayload["runKey"]=="fixture-pool-run" && ((string[])poolPayload["cardIds"]).SequenceEqual(new string[]{"Strike_R"}), "strike-only shortcut creates a run-bound singleton whitelist");
            ((HashSet<string>)Field(form,"poolDraft")).Clear(); bool emptyPoolRejected=false;
            try { Call(form,"CardPoolPayload",true); } catch(TargetInvocationException error) { emptyPoolRejected=error.InnerException is InvalidOperationException; }
            Check(emptyPoolRejected && !(bool)((Dictionary<string,object>)Call(form,"CardPoolPayload",false))["enabled"], "empty whitelist is rejected while explicit vanilla restore remains available");
            Call(form,"SetStrikeOnlyPool");
            Profile poolProfile=(Profile)Field(form,"profile");
            CustomCardModel syncedPoolCard=new CustomCardModel{id="stseditor_pool_sync_fixture",name="实时注册测试"}; poolProfile.customCards.Add(syncedPoolCard);
            Call(form,"UpdatePoolSources",new object[]{new object[]{
                new Dictionary<string,object>{{"cardId","Strike_R"},{"name","打击"},{"obtainable",true},{"isCustom",false}},
                new Dictionary<string,object>{{"cardId",syncedPoolCard.id},{"name",syncedPoolCard.name},{"obtainable",true},{"isCustom",true}}
            }});
            Check(((ListBox)Field(form,"lstPoolSources")).Items.Cast<object>().Any(item=>item.ToString().Contains(syncedPoolCard.name)) && ((HashSet<string>)Field(form,"poolDraft")).SetEquals(new[]{"Strike_R"}),"live catalog refresh exposes synced custom cards without overwriting an unapplied pool draft");
            poolProfile.customCards.Remove(syncedPoolCard);
            foreach(TabPage pool in tabs.TabPages) if(pool.Text=="本局可获得牌池") {
                tabs.SelectedTab=pool; pool.Size=new Size(1540,900); pool.PerformLayout(); ForceHandles(pool);
                Check(((ListBox)Field(form,"lstPoolAllowed")).Width>400 && ((ListBox)Field(form,"lstPoolSources")).Width>400, "acquisition pool layout allocates both multiselect lists");
                using(Bitmap bitmap=new Bitmap(pool.Width,pool.Height)) { pool.DrawToBitmap(bitmap,new Rectangle(0,0,pool.Width,pool.Height)); bitmap.Save(Path.Combine(AppPaths.AppDir,"run-card-pool-v1.4.png")); }
            }
            foreach (TabPage ai in ((TabControl)Field(form,"customSections")).TabPages) if (ai.Text == "AI 创作") {
                tabs.SelectedTab = ((TabControl)Field(form,"customSections")).Parent as TabPage; ((TabControl)Field(form,"customSections")).SelectedTab=ai; ai.Size = new Size(1540, 900); ai.PerformLayout(); ForceHandles(ai);
                using (Bitmap bitmap = new Bitmap(ai.Width, ai.Height)) { ai.DrawToBitmap(bitmap, new Rectangle(0,0,ai.Width,ai.Height)); bitmap.Save(Path.Combine(AppPaths.AppDir, "ai-free-design-v1.3.png")); }
            }
            TabPage page = (TabPage)Field(form, "deckManagerPage");
            Check(tabs.TabPages.Contains(page) && page.Text == "卡组与本局牌组", "editor builds deck manager tab");
            Call(form, "NewDeckPreset");
            Call(form, "AddSourceToPreset");
            ((TextBox)Field(form, "txtPresetName")).Text = "回归测试预设";
            Check((bool)Call(form, "SaveDeckDraft", false), "preset UI saves selected source");
            Check(DeckStore.Load(DeckStore.PathName).decks.Count > 0, "UI preset persisted to separate deck library");
            Dictionary<string, object> card1 = Card("masterDeck", "same-id-a", "Strike_R");
            Dictionary<string, object> card2 = Card("masterDeck", "same-id-b", "Strike_R");
            Dictionary<string, object> snapshot = new Dictionary<string, object> { {"inRun", true}, {"inCombat", true}, {"runKey", "fixture-run"}, {"groupVersions", new Dictionary<string, object> {{"masterDeck", "fixture-v1"}}},
                {"groups", new Dictionary<string, object> { {"masterDeck", new object[] { card1, card2 }}, {"hand", new object[0]}, {"drawPile", new object[0]}, {"discardPile", new object[0]}, {"exhaustPile", new object[0]} }} };
            Call(form, "PopulateLiveTree", snapshot);
            DataGridView grid = (DataGridView)Field(form, "gridRunDeck");
            Check(grid.Rows.Count == 2 && ((Dictionary<string, object>)grid.Rows[0].Tag)["instanceId"].ToString() != ((Dictionary<string, object>)grid.Rows[1].Tag)["instanceId"].ToString(), "same-name cards have distinct selectable row identities");
            Dictionary<string, object> payload = (Dictionary<string, object>)Call(form, "DeckEditPayload", "REMOVE");
            Check((string)payload["expectedVersion"] == "fixture-v1" && (string)payload["runKey"] == "fixture-run" && !(bool)payload["syncCombat"], "UI request binds displayed deck version and explicit sync option");
            grid.Rows[1].Selected = true; Call(form, "EditRunCardValues");
            Check(tabs.SelectedTab == (TabPage)Field(form,"cardEditorPage") && ((string)Field(form, "liveSelectedInstanceId")) == "masterDeck:same-id-b", "selected run card opens existing numeric editor by exact instance");
            tabs.SelectedTab = page;
            form.CreateControl(); form.PerformLayout();
            page.Size = new Size(1540, 900);
            page.PerformLayout(); ForceHandles(page);
            using (Bitmap bitmap = new Bitmap(page.Width, page.Height)) { page.DrawToBitmap(bitmap, new Rectangle(0,0,page.Width,page.Height)); bitmap.Save(Path.Combine(AppPaths.AppDir, "deck-manager-render.png")); }
            Check(grid.Width > 300 && gridPresetWidth(form) > 250, "offscreen layout allocates both card grids");
            TestCustomLibrary(form);
        }
        Console.WriteLine("TOTAL " + passed + " editor checks passed (in-process tests, no live game)");
    }

    static bool SourceHas(ListBox picker, string id) {
        foreach (object item in picker.Items) if ((string)item.GetType().GetField("Id").GetValue(item) == id) return true;
        return false;
    }

    static void TestCompactUi(MainForm form)
    {
        TabControl tabs=(TabControl)Field(form,"tabs");
        Check(tabs.TabPages.Count==5 && tabs.TabPages.Cast<TabPage>().Select(p=>p.Text).SequenceEqual(new[]{"卡牌编辑 · 实时","卡组与本局牌组","本局可获得牌池","自定义卡牌","设置与维护"}),"compact navigation exposes five purposeful main entries");
        Check(FindText(form,"保存到编辑器")==null && FindText(form,"加入当前存档牌组")==null,"obsolete offline save and save-injection controls are removed");
        ListBox native=(ListBox)Field(form,"lstCards"),sources=(ListBox)Field(form,"cmbDeckSource"),library=(ListBox)Field(form,"lstCustom");
        Check(native.SelectionMode==SelectionMode.MultiExtended && sources.SelectionMode==SelectionMode.MultiExtended && library.SelectionMode==SelectionMode.MultiExtended,"native editor, deck source picker, and custom library all support multiselect");
        native.ClearSelected();for(int i=0;i<native.Items.Count;i++)if(((CatalogCard)native.Items[i].GetType().GetField("Card").GetValue(native.Items[i])).id=="Armaments")native.SetSelected(i,true);
        Check(((TextBox)Field(form,"txtNativeDescription")).Text.Contains("所有") && ((TextBox)Field(form,"txtNativeDescription")).Text.Contains("一张"),"real-time editor shows accurate basic and upgraded native card explanations");
        ListBox pool=(ListBox)Field(form,"lstPoolSources");if(pool.Items.Count>0)pool.SelectedIndex=0;
        Check(!((TextBox)Field(form,"txtPoolPreview")).Text.Contains("选择一张卡牌") && ((TextBox)Field(form,"txtPoolPreview")).Text.Contains("升级后"),"pool selection presents native card details and upgrade description");
        TabPage page=(TabPage)Field(form,"cardEditorPage");tabs.SelectedTab=page;page.Size=new Size(1540,900);page.PerformLayout();ForceHandles(page);
        using(Bitmap bitmap=new Bitmap(page.Width,page.Height)){page.DrawToBitmap(bitmap,new Rectangle(0,0,page.Width,page.Height));bitmap.Save(Path.Combine(AppPaths.AppDir,"native-editor-v1.5.png"));}
    }
    static Control FindText(Control root,string text){if(root.Text==text)return root;foreach(Control child in root.Controls){Control found=FindText(child,text);if(found!=null)return found;}return null;}

    static void TestCustomLibrary(MainForm form)
    {
        CustomCardModel custom = new CustomCardModel { id = "stseditor_preview", name = "记忆测试", cost = 1, upgradeCost = 0,
            description = "造成 !D! 点伤害。 NL 抽 !M! 张牌。", upgradeDescription = "#g造成 !D! 点伤害，抽 !M! 张牌。",
            damage = 7, upgradeDamage = 11, magicNumber = 1, upgradeMagicNumber = 2 };
        custom.effects.Add(new EffectModel { type = "DAMAGE", amount = 7, upgradeAmount = 11 });
        Profile fixture = new Profile(); fixture.customCards.Add(custom);
        fixture.customCards.Add(new CustomCardModel { id = "stseditor_keep", name = "保留样例" });
        fixture.customCards.Add(new CustomCardModel { id = "stseditor_disabled", name = "停用样例", enabled = false });
        SetField(form, "profile", fixture);
        IList runtime = (IList)Field(form, "runtimeDeckSources"); runtime.Clear();
        Type sourceType = typeof(MainForm).GetNestedType("DeckSource", BindingFlags.NonPublic);
        runtime.Add(Activator.CreateInstance(sourceType, new object[] { custom.id, "运行时旧名字", true }));
        runtime.Add(Activator.CreateInstance(sourceType, new object[] { "stseditor_disabled", "运行时停用卡", true }));
        runtime.Add(Activator.CreateInstance(sourceType, new object[] { "stseditor_runtime_only", "运行时残留", true }));
        runtime.Add(Activator.CreateInstance(sourceType, new object[] { "OriginalFromGame", "原版卡", false }));
        ListBox picker = (ListBox)Field(form, "cmbDeckSource");
        ((CheckBox)Field(form, "chkOnlyCustomSources")).Checked = true;
        Call(form, "RefreshCustomList");
        Check(picker.Items.Count == 2 && SourceHas(picker, custom.id) && !SourceHas(picker, "stseditor_disabled") && !SourceHas(picker, "stseditor_runtime_only"), "custom picker exactly matches enabled library despite stale runtime catalog");
        ListBox library = (ListBox)Field(form, "lstCustom"); library.ClearSelected(); library.SelectedIndex = 0;
        string libraryText = ((TextBox)Field(form, "txtCustomCardPreview")).Text;
        Check(libraryText.Contains("记忆测试") && libraryText.Contains("造成 7 点伤害") && libraryText.Contains("造成 11 点伤害") && libraryText.Contains("升级后（0 费）") && !libraryText.Contains("!D!") && !libraryText.Contains("#g"), "library selection shows readable base and upgraded descriptions with concrete numbers");
        picker.ClearSelected();
        foreach (object source in picker.Items) if ((string)source.GetType().GetField("Id").GetValue(source) == custom.id) { picker.SelectedItem = source; break; }
        Check(((TextBox)Field(form, "txtDeckSourcePreview")).Text == libraryText, "source picker and library show identical custom-card preview");
        ProfileStore.MarkCustomCardDeleted(fixture, custom.id); ProfileStore.SaveProfile(fixture); Call(form, "RefreshCustomList");
        Check(library.Items.Count == 2 && picker.Items.Count == 1 && !SourceHas(picker, custom.id), "deleting library card immediately removes it from every custom source picker");
        Call(form, "RefreshDeckSources");
        Check(!SourceHas(picker, custom.id) && !((TextBox)Field(form, "txtCustomCardPreview")).Text.Contains("记忆测试"), "refresh does not resurrect deleted runtime card or leave stale description");
        string game = Path.Combine(AppPaths.AppDir, "deleted-card-game");
        string contentPath = AppPaths.ContentPath(game);
        ContentDoc content = new ContentDoc(); content.customCards.Add(custom); content.customCards.Add(new CustomCardModel { id = "stseditor_new_import", name = "游戏新卡" });
        JsonUtil.WriteUtf8(contentPath, JsonUtil.ToJson(content)); string before = Read(contentPath);
        Profile reloaded = ProfileStore.LoadProfile(); ProfileStore.ImportGameContent(game, reloaded);
        Check(ProfileStore.IsCustomCardDeleted(reloaded, custom.id) && !reloaded.customCards.Exists(delegate(CustomCardModel c) { return c.id == custom.id; })
            && reloaded.customCards.Exists(delegate(CustomCardModel c) { return c.id == "stseditor_new_import"; }), "deletion survives save reload and game-content import without blocking new cards");
        Check(Read(contentPath) == before, "library deletion and import preserve game definitions needed by existing saves");
        ProfileStore.RestoreCustomCard(reloaded, custom.id); reloaded.customCards.Add(custom); SetField(form, "profile", reloaded); Call(form, "RefreshCustomList");
        Check(SourceHas(picker, custom.id) && !ProfileStore.IsCustomCardDeleted(reloaded, custom.id), "explicit reimport can restore previously deleted custom card");
        CustomCardModel fallback = new CustomCardModel { id = "stseditor_fallback", name = "自动描述" };
        fallback.effects.Add(new EffectModel { type = "BLOCK", amount = 4, upgradeAmount = 6 });
        string fallbackText = CustomCardPreview.Build(fallback);
        Check(fallbackText.Contains("4 点格挡") && fallbackText.Contains("6 点格挡"), "missing descriptions are derived from effects with correct upgraded values");
        ((TextBox)Field(form, "txtDeckSearch")).Text = "不存在的搜索结果";
        Check(picker.Items.Count == 0 && ((TextBox)Field(form, "txtDeckSourcePreview")).Text.Contains("选择一张卡牌"), "empty search clears previous card description");
        ((TextBox)Field(form, "txtDeckSearch")).Text = "";
        TabControl tabs = (TabControl)Field(form, "tabs");
        foreach (TabPage page in ((TabControl)Field(form,"customSections")).TabPages) if (page == (TabPage)Field(form,"customLibraryPage")) {
            tabs.SelectedTab = ((TabControl)Field(form,"customSections")).Parent as TabPage; ((TabControl)Field(form,"customSections")).SelectedTab=page; library.SelectedIndex = reloaded.customCards.FindIndex(delegate(CustomCardModel c) { return c.id == custom.id; });
            page.Size = new Size(1540, 900); page.PerformLayout(); ForceHandles(page);
            using (Bitmap bitmap = new Bitmap(page.Width, page.Height)) { page.DrawToBitmap(bitmap, new Rectangle(0,0,page.Width,page.Height)); bitmap.Save(Path.Combine(AppPaths.AppDir, "custom-library-v1.2.1.png")); }
        }
        TabPage deckPage = (TabPage)Field(form, "deckManagerPage"); tabs.SelectedTab = deckPage;
        picker.ClearSelected();
        foreach (object source in picker.Items) if ((string)source.GetType().GetField("Id").GetValue(source) == custom.id) { picker.SelectedItem = source; break; }
        deckPage.Size = new Size(1540, 900); deckPage.PerformLayout(); ForceHandles(deckPage);
        using (Bitmap bitmap = new Bitmap(deckPage.Width, deckPage.Height)) { deckPage.DrawToBitmap(bitmap, new Rectangle(0,0,deckPage.Width,deckPage.Height)); bitmap.Save(Path.Combine(AppPaths.AppDir, "source-preview-v1.2.1.png")); }
    }
    static void ForceHandles(Control control) { IntPtr handle = control.Handle; control.PerformLayout(); foreach (Control child in control.Controls) ForceHandles(child); }
    static void TestCreativeCompiler()
    {
        string key = "offlinetest" + DateTime.Now.Ticks;
        string package = "user.generated.g" + key;
        CreativeDesign design = new CreativeDesign {
            card = new CustomCardModel { name = "自由设计测试", description = "获得1点能量。", upgradeDescription = "获得2点能量。", type = "SKILL", target = "SELF" },
            designSummary = "离线编译用例，不调用模型", sources = new Dictionary<string,string>()
        };
        design.sources["CardLogic.java"] = "package " + package + "; import com.stseditor.runtime.*; import com.megacrit.cardcrawl.actions.common.GainEnergyAction; import com.megacrit.cardcrawl.dungeons.AbstractDungeon; public final class CardLogic implements CardExtension { public void onUse(CardExtensionContext ctx) { AbstractDungeon.actionManager.addToBottom(new GainEnergyAction(ctx.amount)); } }";
        string sdk = Path.GetFullPath(Path.Combine(AppPaths.AppDir, "..", "..", "sdk", "card-extension-sdk.jar"));
        CreativeBuildResult result = CreativeCardService.Compile(design, GameLocator.FindGameDir(), Path.Combine(AppPaths.AppDir, "creative-compiler", key), key, package, sdk);
        Check(File.Exists(result.JarPath) && File.Exists(result.ManifestPath) && result.Card.effects[0].type == "JAVA_EXTENSION", "free design compiles real Java and produces linked manifest/card");
        string fakeGame = Path.Combine(AppPaths.AppDir, "creative-install-game"); Directory.CreateDirectory(Path.Combine(fakeGame,"sts-card-editor")); File.WriteAllText(Path.Combine(fakeGame,"sts-card-editor","patch.jar"),"fixture");
        CreativeCardService.Install(result, fakeGame);
        Check(File.Exists(Path.Combine(fakeGame,"sts-card-editor","extensions",Path.GetFileName(result.JarPath))), "saving free design installs matching compiled extension");
        Dictionary<string,string> dangerous = new Dictionary<string,string> { {"CardLogic.java", "package " + package + "; class CardLogic { void f(){ System.exit(0); } }"} };
        bool rejected = false; try { CreativeCardService.ValidateSources(dangerous,package); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "generated source policy rejects host termination operation");
        rejected = false; try { CreativeCardService.ValidateSources(new Dictionary<string,string>{{"../escape.java", "package " + package + ";"}},package); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "generated source filenames cannot escape build directory");
    }
    static int gridPresetWidth(MainForm form) { return ((DataGridView)Field(form, "gridPreset")).Width; }
    static Dictionary<string, object> Card(string group, string uuid, string id) {
        return new Dictionary<string, object> { {"instanceId", group+":"+uuid}, {"uuid",uuid}, {"cardId",id}, {"name","打击"}, {"timesUpgraded",0}, {"upgraded",false}, {"isCustom",false}, {"cost",1}, {"costForTurn",1}, {"baseDamage",6}, {"damage",6}, {"baseBlock",0}, {"block",0}, {"baseMagicNumber",0}, {"magicNumber",0}, {"rawDescription","造成 !D! 点伤害。"}, {"misc",0} };
    }
    static void TestLiveClient()
    {
        string game = Path.Combine(AppPaths.AppDir, "fixture-game"); Directory.CreateDirectory(Path.Combine(game, "sts-card-editor"));
        TcpListener server = new TcpListener(IPAddress.Loopback, 0); server.Start();
        JsonUtil.WriteUtf8(Path.Combine(game, "sts-card-editor", "bridge.json"), JsonUtil.ToJson(new BridgeInfo { protocolVersion=1, host="127.0.0.1", port=((IPEndPoint)server.LocalEndpoint).Port, token="test-token" }));
        Exception serverError = null;
        Thread thread = new Thread(delegate() {
            try {
                using (TcpClient socket = server.AcceptTcpClient())
                using (StreamReader reader = new StreamReader(socket.GetStream()))
                using (StreamWriter writer = new StreamWriter(socket.GetStream(), new UTF8Encoding(false)) { AutoFlush = true }) {
                    string mutationId = null;
                    for (int i = 0; i < 3; i++) {
                        Dictionary<string,object> req = (Dictionary<string,object>)JsonUtil.FromJson(reader.ReadLine()); string op = (string)req["op"];
                        bool pending = op == "deckEdit";
                        if (pending) mutationId = (string)req["requestId"];
                        if (op == "result" && mutationId != (string)req["requestId"]) throw new Exception("poll request ID mismatch");
                        writer.WriteLine(JsonUtil.ToJson(new Dictionary<string,object> { {"ok",true}, {"pending",pending}, {"requestId",req["requestId"]}, {"revision",2}, {"data",new Dictionary<string,object>{{"edited",op=="result"}}} }));
                    }
                }
            } catch (Exception e) { serverError=e; }
        }); thread.Start();
        using (LiveClient client = new LiveClient()) { client.Connect(game); LiveResponse result = client.EditDeck(new Dictionary<string,object>{{"action","REMOVE"}},2); Check(result.Ok && !result.Pending && (bool)result.Data["edited"], "live client waits for final mutation acknowledgement"); }
        thread.Join(5000); server.Stop(); if (serverError != null) throw serverError;
        Check(!thread.IsAlive, "local protocol test completes without external service");
    }
}

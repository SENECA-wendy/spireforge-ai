using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace StsCardEditor
{
    public static class ProfileStore
    {
        public static Catalog LoadCatalog()
        {
            if (!File.Exists(AppPaths.CatalogPath))
            {
                throw new FileNotFoundException("缺少 catalog.json，请从发行目录完整复制文件。", AppPaths.CatalogPath);
            }
            Catalog c = JsonUtil.FromJson<Catalog>(JsonUtil.ReadUtf8(AppPaths.CatalogPath));
            if (c == null || c.cards == null) throw new InvalidDataException("catalog.json 格式无效。");
            return c;
        }

        public static Profile LoadProfile()
        {
            AppPaths.EnsureDataDirs();
            if (!File.Exists(AppPaths.ProfilePath)) return new Profile();
            try
            {
                Profile p = JsonUtil.FromJson<Profile>(JsonUtil.ReadUtf8(AppPaths.ProfilePath));
                if (p == null) return new Profile();
                if (p.overrides == null) p.overrides = new Dictionary<string, CardOverrideModel>();
                if (p.customCards == null) p.customCards = new List<CustomCardModel>();
                if (p.deletedCustomCardIds == null) p.deletedCustomCardIds = new List<string>();
                if (p.schemaVersion < 2) p.schemaVersion = 2; // v1 content is forward-compatible
                return p;
            }
            catch (Exception e)
            {
                string bad = AppPaths.ProfilePath + ".bad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                try { File.Copy(AppPaths.ProfilePath, bad, true); } catch { }
                Logger.Log("读取编辑器配置失败，已备份到 " + bad + ": " + e.Message);
                return new Profile();
            }
        }

        public static void SaveProfile(Profile profile)
        {
            AppPaths.EnsureDataDirs();
            JsonUtil.WriteUtf8(AppPaths.ProfilePath, JsonUtil.ToJson(profile));
        }

        /**
         * The game content.json is the set of definitions currently installed in
         * the game. When the desktop profile is empty (first run after a previous
         * install), import the missing definitions so a later "Apply" does not
         * silently drop them.
         */
        public static bool ImportGameContent(string gameDir, Profile profile)
        {
            string path = AppPaths.ContentPath(gameDir);
            if (!File.Exists(path)) return false;
            ContentDoc doc;
            try
            {
                doc = JsonUtil.FromJson<ContentDoc>(JsonUtil.ReadUtf8(path));
            }
            catch (Exception e)
            {
                Logger.Log("读取游戏 content.json 失败: " + e.Message);
                return false;
            }
            if (doc == null) return false;
            bool changed = false;
            if (doc.overrides != null)
            {
                foreach (KeyValuePair<string, CardOverrideModel> kv in doc.overrides)
                {
                    if (kv.Value != null && !profile.overrides.ContainsKey(kv.Key))
                    {
                        profile.overrides[kv.Key] = kv.Value;
                        changed = true;
                    }
                }
            }
            if (doc.customCards != null)
            {
                foreach (CustomCardModel c in doc.customCards)
                {
                    if (c == null || string.IsNullOrEmpty(c.id) || IsCustomCardDeleted(profile, c.id)) continue;
                    bool exists = false;
                    foreach (CustomCardModel x in profile.customCards) if (x.id == c.id) { exists = true; break; }
                    if (!exists)
                    {
                        profile.customCards.Add(c);
                        changed = true;
                    }
                }
            }
            if (changed) SaveProfile(profile);
            if (changed) Logger.Log("已从游戏 content.json 合并 " + (doc.customCards == null ? 0 : doc.customCards.Count) + " 张自定义卡定义");
            return changed;
        }

        public static bool IsCustomCardDeleted(Profile profile, string id)
        {
            return profile != null && profile.deletedCustomCardIds != null && profile.deletedCustomCardIds.Contains(id);
        }

        public static void MarkCustomCardDeleted(Profile profile, string id)
        {
            profile.customCards.RemoveAll(delegate(CustomCardModel c) { return c != null && c.id == id; });
            if (profile.deletedCustomCardIds == null) profile.deletedCustomCardIds = new List<string>();
            if (!profile.deletedCustomCardIds.Contains(id)) profile.deletedCustomCardIds.Add(id);
        }

        public static void RestoreCustomCard(Profile profile, string id)
        {
            if (profile.deletedCustomCardIds != null) profile.deletedCustomCardIds.Remove(id);
        }

        public static ContentDoc ReadContent(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtil.FromJson<ContentDoc>(JsonUtil.ReadUtf8(path)); }
            catch (Exception e)
            {
                Logger.Log("读取 content.json 失败: " + e.Message);
                return null;
            }
        }

        public static void MergeMissing(ContentDoc target, ContentDoc source)
        {
            if (target == null || source == null) return;
            if (target.overrides == null) target.overrides = new Dictionary<string, CardOverrideModel>();
            if (target.customCards == null) target.customCards = new List<CustomCardModel>();
            if (source.overrides != null)
            {
                foreach (KeyValuePair<string, CardOverrideModel> kv in source.overrides)
                {
                    if (kv.Value != null && !target.overrides.ContainsKey(kv.Key)) target.overrides[kv.Key] = kv.Value;
                }
            }
            if (source.customCards != null)
            {
                foreach (CustomCardModel c in source.customCards)
                {
                    if (c == null || string.IsNullOrEmpty(c.id)) continue;
                    bool exists = false;
                    foreach (CustomCardModel x in target.customCards) if (x.id == c.id) { exists = true; break; }
                    if (!exists) target.customCards.Add(c);
                }
            }
        }

        public static ContentDoc BuildContent(Profile profile)
        {
            ContentDoc doc = new ContentDoc();
            foreach (KeyValuePair<string, CardOverrideModel> kv in profile.overrides)
            {
                if (kv.Value != null) doc.overrides[kv.Key] = kv.Value;
            }
            foreach (CustomCardModel c in profile.customCards)
            {
                if (c != null && c.enabled) doc.customCards.Add(c);
            }
            return doc;
        }

        public static List<string> ValidateCustomCard(CustomCardModel c, Catalog catalog, Profile profile)
        {
            List<string> errors = new List<string>();
            if (c == null) { errors.Add("卡牌为空。"); return errors; }
            errors.AddRange(CardGenerationRules.ValidateIntent((c.description ?? "") + " " + (c.upgradeDescription ?? ""), c));
            if (string.IsNullOrEmpty(c.id) || !c.id.StartsWith("stseditor_")) errors.Add("卡牌 ID 必须以 stseditor_ 开头。");
            if (string.IsNullOrEmpty(c.name)) errors.Add("卡牌名称不能为空。");
            if (c.cost < -2 || c.cost > 99) errors.Add("费用必须在 -2 到 99 之间。");
            if (c.effects == null || c.effects.Count == 0) errors.Add("至少需要一个效果。");
            if (catalog != null && catalog.cards != null)
            {
                foreach (CatalogCard v in catalog.cards)
                {
                    if (v.id == c.id) errors.Add("ID 与游戏原版卡牌冲突: " + c.id);
                }
            }
            if (profile != null)
            {
                int count = 0;
                foreach (CustomCardModel x in profile.customCards)
                {
                    if (x.id == c.id) count++;
                }
                if (count > 1) errors.Add("自定义卡 ID 重复: " + c.id);
            }
            if (c.effects != null)
            {
                foreach (EffectModel e in c.effects)
                {
                    if (!SupportedEffect(e.type)) errors.Add("不支持的效果: " + (e.type == null ? "(空)" : e.type));
                    if (e.type == "JAVA_EXTENSION" && (string.IsNullOrEmpty(e.extensionId) || !System.Text.RegularExpressions.Regex.IsMatch(e.extensionId, @"^[A-Za-z0-9_.-]{1,120}$"))) errors.Add("Java 扩展需要合法的 extensionId；对应本地扩展必须已安装启用。");
                    if (e.type == "APPLY_POWER" && !SupportedPower(e.power)) errors.Add("不支持的状态: " + (e.power == null ? "(空)" : e.power));
                    if (e.condition != null && !SupportedCondition(e.condition.type)) errors.Add("不支持的条件: " + e.condition.type);
                    if (e.type == "DAMAGE" && (!e.amount.HasValue || e.amount.Value < 0 || e.amount.Value > 999)) errors.Add("伤害数值必须在 0 到 999 之间。");
                    if (e.type == "BLOCK" && (!e.amount.HasValue || e.amount.Value < 0 || e.amount.Value > 999)) errors.Add("格挡数值必须在 0 到 999 之间。");
                    if (e.type == "APPLY_POWER" && (!e.amount.HasValue || e.amount.Value < 1 || e.amount.Value > 999)) errors.Add("状态层数必须在 1 到 999 之间。如果描述是“每打出一张牌抽牌”，效果类型应使用 DRAW_ON_PLAY，而不是 APPLY_POWER 0。");
                    if (e.type == "DRAW" && (!e.amount.HasValue || e.amount.Value < 1 || e.amount.Value > 20)) errors.Add("抽牌数量必须在 1 到 20 之间。");
                    if (e.type == "START_TURN_BLOCK" && (!e.amount.HasValue || e.amount.Value < 1 || e.amount.Value > 999)) errors.Add("每回合格挡必须在 1 到 999 之间。");
                    if (e.type == "DRAW_ON_PLAY" && (!e.amount.HasValue || e.amount.Value < 1 || e.amount.Value > 20)) errors.Add("每打出一张牌抽牌数必须在 1 到 20 之间。");
                    if ((e.type == "EXHAUST_CARDS" || e.type == "EXHAUST" || e.type == "DISCARD" || e.type == "DISCARD_CARDS")
                        && (!e.amount.HasValue || e.amount.Value < 1 || e.amount.Value > 10)) errors.Add("弃置/消耗数量必须在 1 到 10 之间。");
                    if (e.type == "ADD_CARD")
                    {
                        if (string.IsNullOrEmpty(e.cardId)) errors.Add("ADD_CARD 必须指定 cardId。");
                        else if (catalog != null && catalog.cards != null && e.cardId == c.id) errors.Add("ADD_CARD 不能引用自身。");
                        if (!e.amount.HasValue || e.amount.Value < 1 || e.amount.Value > 10) errors.Add("ADD_CARD 数量必须在 1 到 10 之间。");
                    }
                    if (e.hits.HasValue && (e.hits.Value < 1 || e.hits.Value > 10)) errors.Add("攻击段数必须在 1 到 10 之间。");
                    if (e.upgradeHits.HasValue && (e.upgradeHits.Value < 1 || e.upgradeHits.Value > 10)) errors.Add("升级后攻击段数必须在 1 到 10 之间。");
                }
            }
            string[] validTypes = new string[] { "ATTACK", "SKILL", "POWER", "STATUS", "CURSE" };
            string[] validTargets = new string[] { "ENEMY", "ALL_ENEMY", "SELF", "NONE", "SELF_AND_ENEMY", "ALL" };
            string[] validRarities = new string[] { "BASIC", "SPECIAL", "COMMON", "UNCOMMON", "RARE", "CURSE" };
            if (Array.IndexOf(validTypes, c.type) < 0) errors.Add("卡牌类型无效: " + c.type);
            if (Array.IndexOf(validTargets, c.target) < 0) errors.Add("目标类型无效: " + c.target);
            if (Array.IndexOf(validRarities, c.rarity) < 0) errors.Add("稀有度无效: " + c.rarity);
            if (!Array.Exists(Professions.CustomColors,value=>value==c.color)) errors.Add("请选择四职业或无色归属。");
            return errors;
        }

        public static bool SupportedEffect(string type)
        {
            if (type == null) return false;
            string[] ok = new string[] {
                "DAMAGE", "BLOCK", "DRAW", "GAIN_ENERGY", "LOSE_HP", "HEAL",
                "DISCARD", "DISCARD_CARDS", "EXHAUST_CARDS", "EXHAUST",
                "ADD_CARD", "APPLY_POWER", "START_TURN_BLOCK", "DRAW_ON_PLAY", "ALL_CARDS_ZERO_COST", "JAVA_EXTENSION"
            };
            return Array.IndexOf(ok, type) >= 0;
        }

        public static bool SupportedPower(string power)
        {
            if (power == null) return false;
            string[] ok = new string[] { "Strength", "Dexterity", "Vulnerable", "Weak", "Weakened", "Frail", "Poison", "Thorns" };
            return Array.IndexOf(ok, power) >= 0;
        }

        public static bool SupportedCondition(string type)
        {
            if (type == null) return false;
            string[] ok = new string[] {
                "HAS_POWER", "HP_BELOW_PERCENT", "HAS_BLOCK",
                "HAND_COUNT_AT_LEAST", "ENERGY_AT_LEAST", "ENEMY_COUNT_AT_LEAST"
            };
            return Array.IndexOf(ok, type) >= 0;
        }

        public static ApiSettings LoadSettings()
        {
            ApiSettings s = new ApiSettings();
            try
            {
                if (!File.Exists(AppPaths.SettingsPath)) return s;
                Dictionary<string, object> d = JsonUtil.FromJson(JsonUtil.ReadUtf8(AppPaths.SettingsPath)) as Dictionary<string, object>;
                if (d == null) return s;
                s.baseUrl = d.ContainsKey("baseUrl") ? Convert.ToString(d["baseUrl"]) : "";
                s.model = d.ContainsKey("model") ? Convert.ToString(d["model"]) : "";
                s.timeoutSeconds = d.ContainsKey("timeoutSeconds") ? Convert.ToInt32(d["timeoutSeconds"]) : 60;
                string enc = d.ContainsKey("apiKeyProtected") ? Convert.ToString(d["apiKeyProtected"]) : "";
                if (!string.IsNullOrEmpty(enc))
                {
                    try
                    {
                        byte[] data = Convert.FromBase64String(enc);
                        byte[] plain = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                        s.apiKey = Encoding.UTF8.GetString(plain);
                    }
                    catch
                    {
                        s.apiKey = "";
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("读取模型设置失败: " + e.Message);
            }
            return s;
        }

        public static void SaveSettings(ApiSettings s)
        {
            try
            {
                string dir = Path.GetDirectoryName(AppPaths.SettingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["baseUrl"] = s.baseUrl == null ? "" : s.baseUrl;
                d["model"] = s.model == null ? "" : s.model;
                d["timeoutSeconds"] = s.timeoutSeconds;
                if (!string.IsNullOrEmpty(s.apiKey))
                {
                    byte[] data = ProtectedData.Protect(Encoding.UTF8.GetBytes(s.apiKey), null, DataProtectionScope.CurrentUser);
                    d["apiKeyProtected"] = Convert.ToBase64String(data);
                }
                JsonUtil.WriteUtf8(AppPaths.SettingsPath, JsonUtil.ToJson(d));
            }
            catch (Exception e)
            {
                Logger.Log("保存模型设置失败: " + e.Message);
                throw;
            }
        }

        private static int _idCounter;

        public static string NewCardId()
        {
            int n;
            lock (typeof(ProfileStore))
            {
                n = ++_idCounter;
            }
            return "stseditor_" + DateTime.UtcNow.Ticks.ToString("x") + "_" + n.ToString("x");
        }
    }
}

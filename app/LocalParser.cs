using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StsCardEditor
{
    public static class LocalParser
    {
        private static readonly string[] Unsupported = new string[] {
            "随机", "复制", "生成一张", "生成 1 张", "弃牌堆", "抽牌堆", "手牌中的", "消耗手牌",
            "每打出一张", "每当你", "每当", "受到伤害时", "回合结束时", "根据", "翻倍", "费用变为",
            "X费", "X 费", "所有牌", "获得一张", "升级后费用"
        };

        public static bool TryParse(string text, string name, out CustomCardModel card, out string error)
        {
            card = null;
            error = null;
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                error = "请输入卡牌描述。";
                return false;
            }
            string s = text.Trim();
            bool zeroCost = CardGenerationRules.RequestsZeroCost(s);
            if (zeroCost && Regex.IsMatch(s, "本回合|整局|本局|永久|整场游戏")) { error = "全牌0费当前仅支持本场战斗；其他持续范围需要高级扩展。"; return false; }
            bool supportedDrawOnPlay = Regex.IsMatch(s, @"每(?:当)?你?打出(?:一)?张牌|每当你使用一张牌");
            foreach (string bad in Unsupported)
            {
                if (zeroCost && (bad == "所有牌" || bad == "费用变为")) continue;
                if (supportedDrawOnPlay && (bad == "每打出一张" || bad == "每当你" || bad == "每当"))
                {
                    continue;
                }
                if (s.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    error = "当前效果引擎不支持“" + bad + "”。请改成数值型伤害、格挡、抽牌、能量、生命或已支持的状态效果。";
                    return false;
                }
            }

            CustomCardModel c = new CustomCardModel();
            c.id = ProfileStore.NewCardId();
            c.color = "RED";
            c.rarity = "COMMON";
            c.name = string.IsNullOrEmpty(name) ? "自定义卡牌" : name.Trim();
            c.notes = "本地解析生成";

            Match mCost = Regex.Match(s, @"(\d+)\s*费");
            if (mCost.Success) c.cost = int.Parse(mCost.Groups[1].Value);

            bool attackWord = s.Contains("攻击");
            bool skillWord = s.Contains("技能");
            bool powerWord = s.Contains("能力");
            bool aoe = s.Contains("所有敌人") || s.Contains("全体敌人") || s.Contains("群体");
            bool startTurnBlock = s.Contains("每回合开始");

            // Damage
            int? damage = null;
            int? upgradeDamage = null;
            int? damageHits = null;
            int? upgradeDamageHits = null;
            Match mDmg = Regex.Match(s, @"造成\s*(\d+)\s*点伤害");
            int damagePos = mDmg.Success ? mDmg.Index : int.MaxValue;
            if (mDmg.Success) damage = int.Parse(mDmg.Groups[1].Value);
            Match mHits = Regex.Match(s, @"造成\s*\d+\s*点伤害\s*(\d+|[一二两三四五六七八九十]+)\s*次");
            if (mHits.Success)
            {
                damageHits = ParseSmallCount(mHits.Groups[1].Value);
            }
            if (damage.HasValue)
            {
                Match u = Regex.Match(s, @"升级后[^。；;\n]*?(\d+)\s*点伤害");
                if (!u.Success) u = Regex.Match(s, @"升级后[^。；;\n]*?伤害[^0-9]{0,8}(\d+)");
                if (u.Success) upgradeDamage = int.Parse(u.Groups[1].Value);
                Match uh = Regex.Match(s, @"升级后[^。；;\n]*?(\d+|[一二两三四五六七八九十]+)\s*次");
                if (uh.Success) upgradeDamageHits = ParseSmallCount(uh.Groups[1].Value);
            }

            // Start-of-turn block (remove the phrase before normal block parsing)
            int? startBlock = null;
            int? startBlockUp = null;
            int startBlockPos = int.MaxValue;
            string withoutStart = s;
            if (startTurnBlock)
            {
                Match b = Regex.Match(s, @"每回合开始[^。；;\n]*?获得\s*(\d+)\s*点?格挡");
                if (!b.Success) b = Regex.Match(s, @"每回合开始[^。；;\n]*?(\d+)\s*点?格挡");
                if (b.Success)
                {
                    startBlock = int.Parse(b.Groups[1].Value);
                    startBlockPos = b.Index;
                    withoutStart = s.Replace(b.Value, "");
                    Match u = Regex.Match(s, @"升级后[^。；;\n]*?获得\s*(\d+)\s*点?格挡");
                    if (!u.Success) u = Regex.Match(s, @"升级后[^。；;\n]*?获得\s*(\d+)\s*点");
                    if (!u.Success) u = Regex.Match(s, @"升级后[^。；;\n]*?(\d+)\s*点?格挡");
                    if (u.Success) startBlockUp = int.Parse(u.Groups[1].Value);
                }
            }

            // Normal block
            int? block = null;
            int? blockUp = null;
            Match mBlock = Regex.Match(withoutStart, @"获得\s*(\d+)\s*点?格挡");
            int blockPos = mBlock.Success ? mBlock.Index : int.MaxValue;
            if (mBlock.Success) block = int.Parse(mBlock.Groups[1].Value);
            if (block.HasValue)
            {
                Match u = Regex.Match(s, @"升级后[^。；;\n]*?获得\s*(\d+)\s*点?格挡");
                if (!u.Success) u = Regex.Match(s, @"升级后[^。；;\n]*?(\d+)\s*点?格挡");
                if (u.Success) blockUp = int.Parse(u.Groups[1].Value);
            }

            // Draw / energy / HP
            bool drawOnPlay = supportedDrawOnPlay;
            int? draw = null;
            Match mDraw = Regex.Match(s, @"抽\s*(\d+)\s*张牌");
            int drawPos = mDraw.Success ? mDraw.Index : int.MaxValue;
            if (mDraw.Success) draw = int.Parse(mDraw.Groups[1].Value);
            int? drawUp = null;
            Match mDrawU = Regex.Match(s, @"升级后[^。；;\n]*?抽\s*(\d+)\s*张牌");
            if (mDrawU.Success) drawUp = int.Parse(mDrawU.Groups[1].Value);

            int? energy = null;
            Match mEnergy = Regex.Match(s, @"(?:获得|得到)\s*(\d+)\s*点?能量");
            int energyPos = mEnergy.Success ? mEnergy.Index : int.MaxValue;
            if (mEnergy.Success) energy = int.Parse(mEnergy.Groups[1].Value);

            int? loseHp = null;
            Match mLose = Regex.Match(s, @"失去\s*(\d+)\s*点?生命");
            int losePos = mLose.Success ? mLose.Index : int.MaxValue;
            if (mLose.Success) loseHp = int.Parse(mLose.Groups[1].Value);

            // Powers
            List<EffectModel> powerEffects = new List<EffectModel>();
            List<KeyValuePair<int, EffectModel>> ordered = new List<KeyValuePair<int, EffectModel>>();
            MatchCollection debuffs = Regex.Matches(s, @"(?:施加|给予)\s*(\d+)\s*层?\s*(易伤|虚弱|脆弱|中毒)");
            foreach (Match m in debuffs)
            {
                EffectModel e = new EffectModel();
                e.type = "APPLY_POWER";
                e.amount = int.Parse(m.Groups[1].Value);
                e.power = PowerId(m.Groups[2].Value);
                e.target = aoe ? "ALL_ENEMY" : "ENEMY";
                Match u = Regex.Match(s, @"升级后[^。；;\n]*?(\d+)\s*层?\s*" + Regex.Escape(m.Groups[2].Value));
                if (u.Success) e.upgradeAmount = int.Parse(u.Groups[1].Value);
                powerEffects.Add(e);
                ordered.Add(new KeyValuePair<int, EffectModel>(m.Index, e));
            }
            MatchCollection buffs = Regex.Matches(s, @"(?:获得)\s*(\d+)\s*层?\s*(力量|敏捷|荆棘)");
            foreach (Match m in buffs)
            {
                EffectModel e = new EffectModel();
                e.type = "APPLY_POWER";
                e.amount = int.Parse(m.Groups[1].Value);
                e.power = PowerId(m.Groups[2].Value);
                e.target = "SELF";
                Match u = Regex.Match(s, @"升级后[^。；;\n]*?(\d+)\s*层?\s*" + Regex.Escape(m.Groups[2].Value));
                if (u.Success) e.upgradeAmount = int.Parse(u.Groups[1].Value);
                powerEffects.Add(e);
                ordered.Add(new KeyValuePair<int, EffectModel>(m.Index, e));
            }

            // Type / target
            if (attackWord) c.type = "ATTACK";
            else if (skillWord) c.type = "SKILL";
            else if (powerWord || drawOnPlay || zeroCost) c.type = "POWER";
            else if (damage.HasValue) c.type = "ATTACK";
            else if (startBlock.HasValue) c.type = "POWER";
            else c.type = "SKILL";

            if (aoe && c.type == "ATTACK") c.target = "ALL_ENEMY";
            else if (c.type == "POWER") c.target = "SELF";
            else if (c.type == "ATTACK") c.target = "ENEMY";
            else if (powerEffects.Count > 0 && !block.HasValue && !draw.HasValue) c.target = "ENEMY";
            else c.target = "NONE";

            c.image = c.type == "ATTACK" ? "red/attack/bash" : (c.type == "POWER" ? "red/power/metallicize" : "red/skill/shrug_it_off");

            // Build effects in text order so that, for example, losing HP happens
            // before drawing when the description says so.
            if (zeroCost) ordered.Add(new KeyValuePair<int, EffectModel>(0, new EffectModel { type = "ALL_CARDS_ZERO_COST", target = "SELF", amount = 1 }));
            if (damage.HasValue)
            {
                EffectModel e = new EffectModel();
                e.type = "DAMAGE";
                e.amount = damage;
                e.upgradeAmount = upgradeDamage;
                e.hits = damageHits;
                e.upgradeHits = upgradeDamageHits;
                e.target = aoe ? "ALL_ENEMY" : "ENEMY";
                ordered.Add(new KeyValuePair<int, EffectModel>(damagePos, e));
            }
            if (block.HasValue)
            {
                EffectModel e = new EffectModel();
                e.type = "BLOCK";
                e.amount = block;
                e.upgradeAmount = blockUp;
                e.target = "SELF";
                ordered.Add(new KeyValuePair<int, EffectModel>(blockPos, e));
            }
            if (startBlock.HasValue)
            {
                EffectModel e = new EffectModel();
                e.type = "START_TURN_BLOCK";
                e.amount = startBlock;
                e.upgradeAmount = startBlockUp;
                e.target = "SELF";
                ordered.Add(new KeyValuePair<int, EffectModel>(startBlockPos, e));
            }
            if (draw.HasValue)
            {
                EffectModel e = new EffectModel();
                e.type = drawOnPlay ? "DRAW_ON_PLAY" : "DRAW";
                e.amount = draw;
                e.upgradeAmount = drawUp;
                e.target = "SELF";
                ordered.Add(new KeyValuePair<int, EffectModel>(drawPos, e));
            }
            if (energy.HasValue)
            {
                EffectModel e = new EffectModel();
                e.type = "GAIN_ENERGY";
                e.amount = energy;
                e.target = "SELF";
                ordered.Add(new KeyValuePair<int, EffectModel>(energyPos, e));
            }
            if (loseHp.HasValue)
            {
                EffectModel e = new EffectModel();
                e.type = "LOSE_HP";
                e.amount = loseHp;
                e.target = "SELF";
                ordered.Add(new KeyValuePair<int, EffectModel>(losePos, e));
            }
            ordered.Sort(delegate(KeyValuePair<int, EffectModel> a, KeyValuePair<int, EffectModel> b)
            {
                return a.Key.CompareTo(b.Key);
            });
            List<EffectModel> fx = new List<EffectModel>();
            foreach (KeyValuePair<int, EffectModel> kv in ordered) fx.Add(kv.Value);
            if (s.Contains("消耗") && !s.Contains("消耗手牌")) c.exhaust = true;
            if (s.Contains("虚无")) c.ethereal = true;
            if (s.Contains("固有")) c.innate = true;
            if (s.Contains("保留")) c.retain = true;

            // Conditions. Only the explicit, measurable forms are accepted.
            ConditionModel condition = null;
            if (s.Contains("如果"))
            {
                condition = ParseCondition(s);
                if (condition == null)
                {
                    error = "无法解析该条件。当前支持：自己有力量、格挡至少 N、手牌至少 N、能量至少 N、敌人至少 N、生命低于 N%。";
                    return false;
                }
                bool attached = false;
                foreach (EffectModel e in fx)
                {
                    if (e.type == "BLOCK" || e.type == "DRAW" || e.type == "GAIN_ENERGY"
                        || e.type == "ADD_CARD" || e.type == "APPLY_POWER")
                    {
                        e.condition = condition;
                        attached = true;
                    }
                }
                if (!attached)
                {
                    error = "条件必须作用于格挡、抽牌、能量、状态或生成卡牌效果。";
                    return false;
                }
            }

            if (fx.Count == 0)
            {
                error = "没有识别到任何已支持的效果。支持：伤害、格挡、抽牌、能量、失去生命、易伤/虚弱/脆弱/中毒/力量/敏捷/荆棘、每回合开始格挡、每打出一张牌抽牌。";
                return false;
            }

            // Fill card statistics
            c.effects = fx;
            c.damage = FirstAmount(fx, "DAMAGE");
            c.upgradeDamage = FirstUpgrade(fx, "DAMAGE");
            c.block = FirstAmount(fx, "BLOCK");
            c.upgradeBlock = FirstUpgrade(fx, "BLOCK");
            c.magicNumber = FirstAmount(fx, "APPLY_POWER");
            if (!c.magicNumber.HasValue) c.magicNumber = FirstAmount(fx, "START_TURN_BLOCK");
            if (!c.magicNumber.HasValue) c.magicNumber = FirstAmount(fx, "DRAW_ON_PLAY");
            c.upgradeMagicNumber = FirstUpgrade(fx, "APPLY_POWER");
            if (!c.upgradeMagicNumber.HasValue) c.upgradeMagicNumber = FirstUpgrade(fx, "START_TURN_BLOCK");
            if (!c.upgradeMagicNumber.HasValue) c.upgradeMagicNumber = FirstUpgrade(fx, "DRAW_ON_PLAY");
            c.description = BuildDescription(c, false);
            c.upgradeDescription = BuildDescription(c, true);

            List<string> errs = ProfileStore.ValidateCustomCard(c, null, null);
            if (errs.Count > 0)
            {
                error = string.Join("\n", errs.ToArray());
                return false;
            }
            card = c;
            return true;
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

        private static ConditionModel ParseCondition(string s)
        {
            ConditionModel c = new ConditionModel();
            Match m = Regex.Match(s, @"如果[^。；;\n]*?(?:自己)?(?:有|具有)\s*(力量|敏捷)");
            if (m.Success)
            {
                c.type = "HAS_POWER";
                c.power = m.Groups[1].Value == "力量" ? "Strength" : "Dexterity";
                c.minAmount = 1;
                return c;
            }
            m = Regex.Match(s, @"如果[^。；;\n]*?格挡至少(?:为)?\s*(\d+)");
            if (m.Success)
            {
                c.type = "HAS_BLOCK";
                c.minAmount = int.Parse(m.Groups[1].Value);
                return c;
            }
            m = Regex.Match(s, @"如果[^。；;\n]*?手牌至少(?:为)?\s*(\d+)");
            if (m.Success)
            {
                c.type = "HAND_COUNT_AT_LEAST";
                c.minAmount = int.Parse(m.Groups[1].Value);
                return c;
            }
            m = Regex.Match(s, @"如果[^。；;\n]*?能量至少(?:为)?\s*(\d+)");
            if (m.Success)
            {
                c.type = "ENERGY_AT_LEAST";
                c.minAmount = int.Parse(m.Groups[1].Value);
                return c;
            }
            m = Regex.Match(s, @"如果[^。；;\n]*?敌人至少(?:为)?\s*(\d+)");
            if (m.Success)
            {
                c.type = "ENEMY_COUNT_AT_LEAST";
                c.minAmount = int.Parse(m.Groups[1].Value);
                return c;
            }
            m = Regex.Match(s, @"如果[^。；;\n]*?生命低于\s*(\d+)\s*%");
            if (m.Success)
            {
                c.type = "HP_BELOW_PERCENT";
                c.percent = int.Parse(m.Groups[1].Value);
                return c;
            }
            return null;
        }

        private static int ParseSmallCount(string text)
        {
            int n;
            if (int.TryParse(text, out n)) return n;
            if (text == "一") return 1;
            if (text == "二" || text == "两") return 2;
            if (text == "三") return 3;
            if (text == "四") return 4;
            if (text == "五") return 5;
            if (text == "六") return 6;
            if (text == "七") return 7;
            if (text == "八") return 8;
            if (text == "九") return 9;
            if (text == "十") return 10;
            return 1;
        }

        private static string PowerId(string cn)
        {
            if (cn == "易伤") return "Vulnerable";
            if (cn == "虚弱") return "Weak";
            if (cn == "脆弱") return "Frail";
            if (cn == "中毒") return "Poison";
            if (cn == "力量") return "Strength";
            if (cn == "敏捷") return "Dexterity";
            if (cn == "荆棘") return "Thorns";
            return cn;
        }

        private static string PowerCn(string id)
        {
            if (id == "Vulnerable") return "易伤";
            if (id == "Weak" || id == "Weakened") return "虚弱";
            if (id == "Frail") return "脆弱";
            if (id == "Poison") return "中毒";
            if (id == "Strength") return "力量";
            if (id == "Dexterity") return "敏捷";
            if (id == "Thorns") return "荆棘";
            return id;
        }

        public static string BuildDescription(CustomCardModel c, bool upgraded)
        {
            List<string> parts = new List<string>();
            int damageIndex = 0;
            int blockIndex = 0;
            int magicIndex = 0;
            for (int i = 0; i < c.effects.Count; i++)
            {
                EffectModel e = c.effects[i];
                int amount = upgraded && e.upgradeAmount.HasValue ? e.upgradeAmount.Value : (e.amount.HasValue ? e.amount.Value : 0);
                string prefix = "";
                if (e.condition != null)
                {
                    if (e.condition.type == "HAS_POWER" && e.condition.power == "Strength") prefix = "如果你有力量，";
                    else if (e.condition.type == "HP_BELOW_PERCENT") prefix = "如果你的生命低于 " + (e.condition.percent.HasValue ? e.condition.percent.Value : 50) + "%，";
                    else if (e.condition.type == "HAS_BLOCK") prefix = "如果你有至少 " + (e.condition.minAmount.HasValue ? e.condition.minAmount.Value : 1) + " 点格挡，";
                    else if (e.condition.type == "HAND_COUNT_AT_LEAST") prefix = "如果你手牌至少 " + (e.condition.minAmount.HasValue ? e.condition.minAmount.Value : 1) + " 张，";
                    else if (e.condition.type == "ENERGY_AT_LEAST") prefix = "如果你能量至少 " + (e.condition.minAmount.HasValue ? e.condition.minAmount.Value : 1) + "，";
                    else if (e.condition.type == "ENEMY_COUNT_AT_LEAST") prefix = "如果敌人至少 " + (e.condition.minAmount.HasValue ? e.condition.minAmount.Value : 1) + " 个，";
                }
                if (e.type == "DAMAGE")
                {
                    string dmg = upgraded ? amount.ToString() : (damageIndex == 0 && !HasMultiple(c, "DAMAGE") ? "!D!" : amount.ToString());
                    string who = (e.target == "ALL_ENEMY" || e.target == "ALL") ? "对所有敌人" : "";
                    int hits = upgraded && e.upgradeHits.HasValue ? e.upgradeHits.Value : (e.hits.HasValue ? e.hits.Value : 1);
                    string hitText = hits > 1 ? (" " + hits + " 次") : "";
                    parts.Add(prefix + who + "造成 " + dmg + " 点伤害" + hitText + "。");
                    damageIndex++;
                }
                else if (e.type == "BLOCK")
                {
                    string b = upgraded ? amount.ToString() : (blockIndex == 0 && !HasMultiple(c, "BLOCK") ? "!B!" : amount.ToString());
                    parts.Add(prefix + "获得 " + b + " 点格挡。");
                    blockIndex++;
                }
                else if (e.type == "START_TURN_BLOCK")
                {
                    string b = upgraded ? amount.ToString() : "!M!";
                    parts.Add("每回合开始时，获得 " + b + " 点格挡。");
                    magicIndex++;
                }
                else if (e.type == "DRAW_ON_PLAY")
                {
                    string d = upgraded ? amount.ToString() : "!M!";
                    parts.Add("每当你打出一张牌，抽 " + d + " 张牌。");
                    magicIndex++;
                }
                else if (e.type == "APPLY_POWER")
                {
                    string p = upgraded ? amount.ToString() : (magicIndex == 0 && !HasMultiple(c, "APPLY_POWER") ? "!M!" : amount.ToString());
                    string who = (e.target == "ALL_ENEMY" || e.target == "ALL") ? "对所有敌人" : "";
                    parts.Add(prefix + who + "施加 " + p + " 层" + PowerCn(e.power) + "。");
                    magicIndex++;
                }
                else if (e.type == "DRAW") parts.Add(prefix + "抽 " + amount + " 张牌。");
                else if (e.type == "GAIN_ENERGY") parts.Add(prefix + "获得 " + amount + " 点能量。");
                else if (e.type == "LOSE_HP") parts.Add("失去 " + amount + " 点生命。");
                else if (e.type == "HEAL") parts.Add("回复 " + amount + " 点生命。");
                else if (e.type == "DISCARD" || e.type == "DISCARD_CARDS") parts.Add("丢弃 " + amount + " 张牌。");
                else if (e.type == "EXHAUST_CARDS" || e.type == "EXHAUST") parts.Add("消耗 " + amount + " 张牌。");
                else if (e.type == "ADD_CARD")
                {
                    string dest = e.destination == null ? "HAND" : e.destination.ToUpperInvariant();
                    string where = dest == "DRAW" || dest == "DRAW_PILE" ? "抽牌堆" : (dest == "DISCARD" || dest == "DISCARD_PILE" ? "弃牌堆" : "手牌");
                    parts.Add("将 " + amount + " 张牌加入" + where + "。");
                }
                else if (e.type == "ALL_CARDS_ZERO_COST") parts.Add(CardGenerationRules.ZeroCostDescription);
                else if (e.type == "JAVA_EXTENSION") parts.Add("执行已安装的 Java 扩展：" + e.extensionId + "。具体行为见该扩展说明。");
            }
            if (c.exhaust) parts.Add("消耗。");
            if (c.ethereal) parts.Add("虚无。");
            if (c.innate) parts.Add("固有。");
            if (c.retain) parts.Add("保留。");
            return string.Join(" ", parts.ToArray());
        }

        private static bool HasMultiple(CustomCardModel c, string type)
        {
            int n = 0;
            foreach (EffectModel e in c.effects) if (e.type == type) n++;
            return n > 1;
        }
    }
}

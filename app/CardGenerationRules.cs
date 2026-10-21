using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StsCardEditor
{
    public static class CardGenerationRules
    {
        public const string ZeroCostDescription = "本场战斗中，你的所有固定费用卡牌变为0费，包括之后抽到和生成的牌。X费与不可打出的牌除外。";

        public static bool RequestsZeroCost(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return Regex.IsMatch(text, @"(?:所有|全部|一切|全体).{0,12}(?:卡牌|牌).{0,28}(?:0\s*费|零费|(?:费用|消耗|能耗).{0,8}(?:0|零))|all\s+cards?.{0,30}(?:cost\s*(?:0|zero)|free)", RegexOptions.IgnoreCase);
        }

        public static bool HasEffect(CustomCardModel card, string type)
        {
            return card != null && card.effects != null && card.effects.Exists(delegate(EffectModel effect) { return effect != null && effect.type == type; });
        }

        public static List<string> ValidateIntent(string text, CustomCardModel card)
        {
            List<string> errors = new List<string>();
            if (HasEffect(card, "JAVA_EXTENSION")) return errors; // Compiled extension owns its documented semantics.
            if (RequestsZeroCost(text)) {
                if (!HasEffect(card, "ALL_CARDS_ZERO_COST")) errors.Add("要求所有牌0费，但实际效果缺少 ALL_CARDS_ZERO_COST；不能用敏捷等状态替代。");
                else if (Regex.IsMatch(text ?? "", "本回合|整局|本局|永久|整场游戏")) errors.Add("当前全牌0费持续本场战斗，尚不支持只持续本回合或整局永久生效。");
            }
            if (Regex.IsMatch(text ?? "", "能量上限|每回合(?:额外|多)抽|额外回合")) errors.Add("该需求包含尚未内置的持续机制，请先实现并安装 Java 扩展，不能用一次能量或打牌抽牌替代。");
            return errors;
        }
    }
}

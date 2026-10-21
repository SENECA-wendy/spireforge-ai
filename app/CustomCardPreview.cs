using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace StsCardEditor
{
    public static class CustomCardPreview
    {
        public static TextBox CreateTextBox(string accessibleName)
        {
            return new TextBox { Dock = DockStyle.Fill, ReadOnly = true, WordWrap = true, Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = SystemColors.Window, ForeColor = SystemColors.WindowText,
                BorderStyle = BorderStyle.None, AccessibleName = accessibleName,
                Text = UiText.T("选择一张卡牌，查看基础描述和升级效果。") };
        }

        public static string Build(CustomCardModel card)
        {
            if (card == null) return UiText.T("选择一张卡牌，查看基础描述和升级效果。");
            StringBuilder text = new StringBuilder();
            text.AppendLine(card.name + "  [" + card.id + "]");
            text.AppendLine(TypeName(card.type) + " · " + Professions.Name(card.color) + " · " + (card.enabled ? UiText.T("已启用") : UiText.T("已停用")));
            text.AppendLine();
            text.AppendLine(UiText.T("基础（") + Cost(card.cost) + "）");
            text.AppendLine(Description(card, false));
            text.AppendLine();
            text.AppendLine(UiText.T("升级后（") + Cost(card.upgradeCost ?? card.cost) + "）");
            text.AppendLine(Description(card, true));
            List<string> tags = new List<string>();
            if (card.exhaust) tags.Add(UiText.T("消耗"));
            if (card.ethereal) tags.Add(UiText.T("虚无"));
            if (card.innate) tags.Add(UiText.T("固有"));
            if (card.retain) tags.Add(UiText.T("保留"));
            if (tags.Count > 0) { text.AppendLine(); text.AppendLine(UiText.T("属性：") + string.Join("、", tags.ToArray())); }
            return text.ToString().TrimEnd();
        }

        private static string Description(CustomCardModel card, bool upgraded)
        {
            string raw = upgraded ? card.upgradeDescription : card.description;
            if (string.IsNullOrWhiteSpace(raw)) {
                if (card.effects != null && card.effects.Count > 0) {
                    CustomCardModel safe = JsonUtil.FromJson<CustomCardModel>(JsonUtil.ToJson(card));
                    safe.effects = safe.effects.Where(delegate(EffectModel effect) { return effect != null; }).ToList();
                    raw = LocalParser.BuildDescription(safe, upgraded);
                } else raw = card.description;
            }
            if (string.IsNullOrWhiteSpace(raw)) return UiText.T("尚未填写描述。");
            raw = raw.Replace("!D!", Number(card, upgraded, "D"))
                .Replace("!B!", Number(card, upgraded, "B"))
                .Replace("!M!", Number(card, upgraded, "M"))
                .Replace("[E]", UiText.T("能量")).Replace("[R]", UiText.T("红色能量"))
                .Replace("[G]", UiText.T("绿色能量")).Replace("[B]", UiText.T("蓝色能量")).Replace("[W]", UiText.T("能量"));
            raw = Regex.Replace(raw, @"\bNL\b", Environment.NewLine);
            return Regex.Replace(raw, @"#[rgbypw]", "").Trim();
        }

        private static string Number(CustomCardModel card, bool upgraded, string variable)
        {
            int? normal = variable == "D" ? card.damage : variable == "B" ? card.block : card.magicNumber;
            int? higher = variable == "D" ? card.upgradeDamage : variable == "B" ? card.upgradeBlock : card.upgradeMagicNumber;
            if (upgraded && higher.HasValue) return higher.Value.ToString();
            if (normal.HasValue) return normal.Value.ToString();
            if (card.effects != null) foreach (EffectModel effect in card.effects) {
                if (effect == null) continue;
                bool matches = variable == "D" ? effect.type == "DAMAGE" : variable == "B" ? effect.type == "BLOCK"
                    : effect.type == "APPLY_POWER" || effect.type == "START_TURN_BLOCK" || effect.type == "DRAW_ON_PLAY";
                if (matches && effect.amount.HasValue) return (upgraded ? effect.upgradeAmount ?? effect.amount.Value : effect.amount.Value).ToString();
            }
            return UiText.T("未设置");
        }

        private static string Cost(int value) { return value == -1 ? UiText.T("X 费") : value == -2 ? UiText.T("不可打出") : value + UiText.T(" 费"); }
        private static string TypeName(string type) {
            switch (type) { case "ATTACK": return UiText.T("攻击"); case "SKILL": return UiText.T("技能"); case "POWER": return UiText.T("能力"); case "STATUS": return UiText.T("状态"); case "CURSE": return UiText.T("诅咒"); default: return type ?? UiText.T("卡牌"); }
        }
    }

    public partial class MainForm
    {
        private TextBox txtCustomCardPreview;
        private TextBox txtDeckSourcePreview;

        private void ShowCustomCardPreview()
        {
            if (txtCustomCardPreview == null) return;
            CustomListItem item = lstCustom.SelectedItem as CustomListItem;
            txtCustomCardPreview.Text = (lstCustom.SelectedItems.Count>1?UiText.T("已选择 ")+lstCustom.SelectedItems.Count+UiText.T(" 张。以下为首张卡牌介绍。\r\n\r\n"):"")+CustomCardPreview.Build(item == null ? null : item.Card);
            txtCustomCardPreview.SelectionStart = 0;
        }

        private void ShowDeckSourcePreview()
        {
            if (txtDeckSourcePreview == null) return;
            DeckSource source = cmbDeckSource.SelectedItem as DeckSource;
            if (source == null) { txtDeckSourcePreview.Text = CustomCardPreview.Build(null); return; }
            txtDeckSourcePreview.Text = CardIntroduction(source.Id,source.Name);
            txtDeckSourcePreview.SelectionStart = 0;
        }
    }
}

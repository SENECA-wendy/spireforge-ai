using System;
using System.Text;
namespace StsCardEditor
{
    public static class NativeCardPreview
    {
        public static string Build(CatalogCard card)
        {
            if(card==null) return UiText.T("选择卡牌，查看基础介绍和升级效果。");
            CatalogCard original=card;card=UiText.NativeCard(card);
            string text=CustomCardPreview.Build(new CustomCardModel {
                id=card.id,name=card.name,description=card.description,upgradeDescription=card.upgradeDescription ?? card.description,
                color=card.color,type=card.type,cost=card.cost,upgradeCost=card.upgradeCost,
                damage=card.damage,upgradeDamage=card.upgradeDamage,block=card.block,upgradeBlock=card.upgradeBlock,
                magicNumber=card.magicNumber,upgradeMagicNumber=card.upgradeMagicNumber });
            text=text.Replace(UiText.T(" · 已启用"),UiText.T(" · 原生卡"));
            if(!string.IsNullOrEmpty(MechanismNote(original))) text+=UiText.T("\r\n\r\n机制说明：")+UiText.T(MechanismNote(original));
            return text;
        }
        public static string MechanismNote(CatalogCard card)
        {
            switch(card.id) {
                case "Body Slam": return UiText.T("伤害由当前格挡计算，修改伤害字段不能替代此机制。");
                case "Heavy Blade": return UiText.T("力量加成倍数由附加数值控制。");
                case "Perfected Strike": return UiText.T("伤害还取决于牌组中的打击数量。");
                case "Searing Blow": return UiText.T("可以多次升级，游戏使用特殊升级公式；此处升级值表示覆盖后的数值。");
                case "Whirlwind": return UiText.T("X费牌的攻击次数由消耗能量决定，伤害是每次攻击数值。");
                default: return card.special ? card.specialNote : null;
            }
        }
    }
}

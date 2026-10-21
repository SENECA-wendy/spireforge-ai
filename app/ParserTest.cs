using System;

namespace StsCardEditor
{
    internal static class ParserTest
    {
        private static void Main()
        {
            string[] texts = new string[] {
                "1 费攻击，造成 9 点伤害，施加 2 层易伤，升级后伤害变为 12。",
                "0 费技能，失去 3 点生命，抽 2 张牌，消耗。",
                "2 费群体攻击，对所有敌人造成 8 点伤害，如果自己有力量，再获得 5 点格挡。",
                "1 费能力，每回合开始获得 2 点格挡，升级后获得 3 点。",
                "1费技能，每当受到伤害时，随机复制手牌中的一张牌并使其费用变为0。"
            };
            for (int i = 0; i < texts.Length; i++)
            {
                CustomCardModel c;
                string err;
                bool ok = LocalParser.TryParse(texts[i], "测试卡" + (i + 1), out c, out err);
                Console.WriteLine("CASE " + (i + 1) + " ok=" + ok);
                if (ok)
                {
                    Console.WriteLine("  name=" + c.name + " cost=" + c.cost + " type=" + c.type + " target=" + c.target);
                    Console.WriteLine("  desc=" + c.description);
                    Console.WriteLine("  up=" + c.upgradeDescription);
                    Console.WriteLine("  json=" + JsonUtil.ToJson(c));
                }
                else
                {
                    Console.WriteLine("  error=" + err);
                }
            }
        }
    }
}

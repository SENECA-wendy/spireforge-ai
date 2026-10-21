package com.megacrit.cardcrawl.cards;
import java.util.UUID;
public class AbstractCard {
    public enum CardColor { RED, GREEN, BLUE, PURPLE, COLORLESS }
    public enum CardType { ATTACK, SKILL, POWER, CURSE, STATUS }
    public enum CardRarity { BASIC, COMMON, UNCOMMON, RARE, SPECIAL, CURSE }
    public CardType type = CardType.ATTACK;
    public CardRarity rarity = CardRarity.BASIC;
    public static int getPrice(CardRarity rarity) { return rarity == CardRarity.BASIC ? 9999 : 50; }
    public int price;
    public float current_x, current_y, target_x, target_y, drawScale, targetDrawScale;
    public String cardID, name;
    public UUID uuid = UUID.randomUUID();
    public int timesUpgraded, misc, cost, damage, baseDamage;
    public boolean isSeen, isLocked, inBottleFlame, inBottleLightning, inBottleTornado;
    public boolean retain, selfRetain, exhaust, exhaustOnUseOnce, exhaustOnFire, isEthereal, isInnate, purgeOnUse, dontTriggerOnUseCard, returnToHand, shuffleBackIntoDrawPile, freeToPlayOnce, ignoreEnergyOnUse;
    public int energyOnUse;
    public int costForTurn = 1;
    public boolean isCostModifiedForTurn;
    public CardColor color = CardColor.RED;
    public AbstractCard(String id) { cardID = id; name = id; baseDamage = damage = 6; cost = 1; }
    public boolean canUpgrade() { return timesUpgraded < 1; }
    public void upgrade() { if (canUpgrade()) { timesUpgraded++; damage += 3; baseDamage += 3; } }
    public AbstractCard makeCopy() { return new AbstractCard(cardID); }
    public AbstractCard makeSameInstanceOf() { AbstractCard c = makeCopy(); c.uuid = uuid; c.misc = misc; c.timesUpgraded = timesUpgraded; c.damage = damage; c.baseDamage = baseDamage; return c; }
}

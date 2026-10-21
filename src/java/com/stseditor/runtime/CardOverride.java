package com.stseditor.runtime;

import com.google.gson.JsonObject;

/** Declarative stat override for an existing game card. */
public final class CardOverride {
    public String id;
    public String name;
    public String description;
    public Integer cost;
    public Integer upgradeCost;
    public Integer damage;
    public Integer upgradeDamage;
    public Integer block;
    public Integer upgradeBlock;
    public Integer magicNumber;
    public Integer upgradeMagicNumber;
    public Integer draw;
    public Integer discard;
    public Integer heal;

    public static CardOverride parse(String id, JsonObject o) {
        CardOverride c = new CardOverride();
        c.id = id;
        c.name = CardSpec.str(o, "name", null);
        c.description = CardSpec.str(o, "description", null);
        c.cost = CardSpec.integerOrNull(o, "cost");
        c.upgradeCost = CardSpec.integerOrNull(o, "upgradeCost");
        c.damage = CardSpec.integerOrNull(o, "damage");
        c.upgradeDamage = CardSpec.integerOrNull(o, "upgradeDamage");
        c.block = CardSpec.integerOrNull(o, "block");
        c.upgradeBlock = CardSpec.integerOrNull(o, "upgradeBlock");
        c.magicNumber = CardSpec.integerOrNull(o, "magicNumber");
        c.upgradeMagicNumber = CardSpec.integerOrNull(o, "upgradeMagicNumber");
        c.draw = CardSpec.integerOrNull(o, "draw");
        c.discard = CardSpec.integerOrNull(o, "discard");
        c.heal = CardSpec.integerOrNull(o, "heal");
        return c;
    }

    public boolean isEmpty() {
        return name == null && description == null && cost == null && upgradeCost == null
                && damage == null && upgradeDamage == null && block == null && upgradeBlock == null
                && magicNumber == null && upgradeMagicNumber == null && draw == null && discard == null && heal == null;
    }
}

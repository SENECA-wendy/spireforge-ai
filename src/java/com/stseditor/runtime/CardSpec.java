package com.stseditor.runtime;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import java.util.ArrayList;
import java.util.List;

/** Versioned, declarative description of one custom card. */
public final class CardSpec {
    public static final int SCHEMA_VERSION = 2;
    public static final int MIN_SCHEMA_VERSION = 1;

    public String id;
    public String name;
    public String description;
    public String upgradeDescription;
    public String image = "red/attack/bash";
    public String color = "RED";
    public String type = "ATTACK";
    public String rarity = "COMMON";
    public String target = "ENEMY";
    public int cost = 1;
    public Integer upgradeCost;
    public Integer damage;
    public Integer upgradeDamage;
    public Integer block;
    public Integer upgradeBlock;
    public Integer magicNumber;
    public Integer upgradeMagicNumber;
    public boolean exhaust;
    public boolean innate;
    public boolean ethereal;
    public boolean retain;
    public final List<String> keywords = new ArrayList<String>();
    public final List<EffectSpec> effects = new ArrayList<EffectSpec>();

    public static CardSpec parse(JsonObject o) {
        CardSpec s = new CardSpec();
        s.id = str(o, "id", null);
        s.name = str(o, "name", null);
        s.description = str(o, "description", "");
        s.upgradeDescription = str(o, "upgradeDescription", null);
        s.image = str(o, "image", "red/attack/bash");
        s.color = str(o, "color", "RED");
        s.type = str(o, "type", "ATTACK");
        s.rarity = str(o, "rarity", "COMMON");
        s.target = str(o, "target", "ENEMY");
        s.cost = integer(o, "cost", 1);
        s.upgradeCost = integerOrNull(o, "upgradeCost");
        s.damage = integerOrNull(o, "damage");
        s.upgradeDamage = integerOrNull(o, "upgradeDamage");
        s.block = integerOrNull(o, "block");
        s.upgradeBlock = integerOrNull(o, "upgradeBlock");
        s.magicNumber = integerOrNull(o, "magicNumber");
        s.upgradeMagicNumber = integerOrNull(o, "upgradeMagicNumber");
        s.exhaust = bool(o, "exhaust", false);
        s.innate = bool(o, "innate", false);
        s.ethereal = bool(o, "ethereal", false);
        s.retain = bool(o, "retain", false);
        JsonArray kw = arr(o, "keywords");
        if (kw != null) {
            for (JsonElement e : kw) {
                if (!e.isJsonNull()) {
                    s.keywords.add(e.getAsString());
                }
            }
        }
        JsonArray fx = arr(o, "effects");
        if (fx != null) {
            for (JsonElement e : fx) {
                if (e.isJsonObject()) {
                    s.effects.add(EffectSpec.parse(e.getAsJsonObject()));
                }
            }
        }
        if (s.damage == null) {
            s.damage = firstAmount(s.effects, "DAMAGE");
        }
        if (s.block == null) {
            s.block = firstAmount(s.effects, "BLOCK");
        }
        if (s.magicNumber == null) {
            s.magicNumber = firstAmount(s.effects, "APPLY_POWER");
        }
        if (s.magicNumber == null) {
            s.magicNumber = firstAmount(s.effects, "START_TURN_BLOCK");
        }
        if (s.magicNumber == null) {
            s.magicNumber = firstAmount(s.effects, "DRAW_ON_PLAY");
        }
        if (s.upgradeDamage == null) {
            s.upgradeDamage = firstUpgradeAmount(s.effects, "DAMAGE");
        }
        if (s.upgradeBlock == null) {
            s.upgradeBlock = firstUpgradeAmount(s.effects, "BLOCK");
        }
        if (s.upgradeMagicNumber == null) {
            s.upgradeMagicNumber = firstUpgradeAmount(s.effects, "APPLY_POWER");
        }
        if (s.upgradeMagicNumber == null) {
            s.upgradeMagicNumber = firstUpgradeAmount(s.effects, "START_TURN_BLOCK");
        }
        if (s.upgradeMagicNumber == null) {
            s.upgradeMagicNumber = firstUpgradeAmount(s.effects, "DRAW_ON_PLAY");
        }
        return s;
    }

    private static Integer firstAmount(List<EffectSpec> effects, String type) {
        for (EffectSpec e : effects) {
            if (type.equals(e.type) && e.amount != null) {
                return e.amount;
            }
        }
        return null;
    }

    private static Integer firstUpgradeAmount(List<EffectSpec> effects, String type) {
        for (EffectSpec e : effects) {
            if (type.equals(e.type) && e.upgradeAmount != null) {
                return e.upgradeAmount;
            }
        }
        return null;
    }

    public int amountFor(EffectSpec e, boolean upgraded) {
        if (upgraded && e.upgradeAmount != null) {
            return e.upgradeAmount;
        }
        return e.amount == null ? 0 : e.amount;
    }

    static String str(JsonObject o, String key, String def) {
        JsonElement e = o.get(key);
        return e == null || e.isJsonNull() ? def : e.getAsString();
    }

    static int integer(JsonObject o, String key, int def) {
        JsonElement e = o.get(key);
        return e == null || e.isJsonNull() ? def : e.getAsInt();
    }

    static Integer integerOrNull(JsonObject o, String key) {
        JsonElement e = o.get(key);
        return e == null || e.isJsonNull() ? null : Integer.valueOf(e.getAsInt());
    }

    static boolean bool(JsonObject o, String key, boolean def) {
        JsonElement e = o.get(key);
        return e == null || e.isJsonNull() ? def : e.getAsBoolean();
    }

    static JsonArray arr(JsonObject o, String key) {
        JsonElement e = o.get(key);
        return e != null && e.isJsonArray() ? e.getAsJsonArray() : null;
    }

    /** One effect node in the whitelist DSL. */
    public static final class EffectSpec {
        public String type;
        public Integer amount;
        public Integer upgradeAmount;
        public Integer hits;
        public Integer upgradeHits;
        public String power;
        public String target = "ENEMY";
        public String cardId;
        public String destination;
        public Boolean cardUpgrade;
        public String extensionId;
        public JsonObject parameters;
        public JsonObject condition;

        public static EffectSpec parse(JsonObject o) {
            EffectSpec e = new EffectSpec();
            e.type = str(o, "type", "").toUpperCase();
            e.amount = integerOrNull(o, "amount");
            e.upgradeAmount = integerOrNull(o, "upgradeAmount");
            e.hits = integerOrNull(o, "hits");
            e.upgradeHits = integerOrNull(o, "upgradeHits");
            e.power = str(o, "power", null);
            e.target = str(o, "target", "ENEMY").toUpperCase();
            e.cardId = str(o, "cardId", null);
            e.extensionId = str(o, "extensionId", null);
            if (o.has("parameters") && o.get("parameters").isJsonObject()) e.parameters = o.getAsJsonObject("parameters");
            e.destination = str(o, "destination", null);
            e.cardUpgrade = o.has("cardUpgrade") && !o.get("cardUpgrade").isJsonNull()
                    ? Boolean.valueOf(o.get("cardUpgrade").getAsBoolean()) : null;
            JsonElement c = o.get("condition");
            if (c != null && c.isJsonObject()) {
                e.condition = c.getAsJsonObject();
            }
            return e;
        }
    }
}

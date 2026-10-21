package com.stseditor.runtime;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.megacrit.cardcrawl.cards.AbstractCard;
import com.megacrit.cardcrawl.core.Settings;
import com.megacrit.cardcrawl.dungeons.AbstractDungeon;
import com.megacrit.cardcrawl.helpers.CardLibrary;
import com.megacrit.cardcrawl.helpers.input.InputHelper;
import com.megacrit.cardcrawl.rooms.AbstractRoom;
import com.megacrit.cardcrawl.ui.buttons.LargeDialogOptionButton;
import com.megacrit.cardcrawl.unlock.UnlockTracker;
import java.io.BufferedReader;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.nio.charset.Charset;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

/**
 * Runtime bridge between the declarative editor data and the game classes.
 *
 * This class is compiled against the retail game jar and is loaded from the
 * patch jar that is placed before desktop-1.0.jar in config.json.
 */
public final class ContentRegistry {
    private static final Object LOCK = new Object();
    private static final Set<UUID> APPLIED_LIVE_CARDS = new HashSet<UUID>();
    private static boolean loaded = false;
    private static boolean registrationDone = false;
    private static final Map<String, CardOverride> OVERRIDES = new HashMap<String, CardOverride>();
    private static final List<CardSpec> CUSTOM_CARDS = new ArrayList<CardSpec>();
    private static long lastDialogLog = 0L;

    private ContentRegistry() {
    }

    /** Called from the patched CardLibrary.initialize(). */
    public static void register() {
        synchronized (LOCK) {
            load();
            if (debugFile().isFile()) {
                Settings.isInfo = true;
                log("debug input flag detected; Settings.isInfo=true");
            }
            int added = 0;
            for (CardSpec spec : CUSTOM_CARDS) {
                if (CardLibrary.cards.containsKey(spec.id)) {
                    continue;
                }
                try {
                    CustomCard card = new CustomCard(spec);
                    CardLibrary.add(card);
                    card.isSeen = true;
                    card.isLocked = false;
                    UnlockTracker.lockedCards.remove(spec.id);
                    added++;
                    log("registered custom card id=" + spec.id + " name=" + spec.name);
                } catch (Throwable t) {
                    log("failed to register custom card id=" + spec.id + ": " + t);
                }
            }
            registrationDone = true;
            log("ContentRegistry.register complete; custom=" + CUSTOM_CARDS.size()
                    + " added=" + added + " overrides=" + OVERRIDES.size());
        }
        try {
            RuntimeBridge.start();
        } catch (Throwable t) {
            log("RuntimeBridge.start failed: " + t);
        }
    }

    /** Called from the patched CardLibrary.add(AbstractCard). */
    public static void applyPrototype(AbstractCard card) {
        if (card == null) {
            return;
        }
        synchronized (LOCK) {
            load();
            CardOverride override = OVERRIDES.get(card.cardID);
            if (override != null && !override.isEmpty()) {
                applyOverride(card, override, false);
            }
        }
    }

    /** Called from the patched CardLibrary.getCopy(...) before returning. */
    public static void applyCopy(AbstractCard card) {
        if (card == null) {
            return;
        }
        synchronized (LOCK) {
            load();
            CardOverride override = OVERRIDES.get(card.cardID);
            if (override != null && !override.isEmpty()) {
                applyOverride(card, override, card.upgraded || card.timesUpgraded > 0);
            }
            APPLIED_LIVE_CARDS.add(card.uuid);
        }
    }

    /** Called from the patched AbstractCard.applyPowers()/calculateCardDamage(). */
    public static void applyLiveCard(AbstractCard card) {
        if (card == null || card.uuid == null) {
            return;
        }
        synchronized (LOCK) {
            if (APPLIED_LIVE_CARDS.contains(card.uuid)) {
                return;
            }
            load();
            CardOverride override = OVERRIDES.get(card.cardID);
            if (override != null && !override.isEmpty()) {
                applyOverride(card, override, card.upgraded || card.timesUpgraded > 0);
            }
            APPLIED_LIVE_CARDS.add(card.uuid);
        }
    }

    public static void afterUpgradeDamage(AbstractCard card) {
        afterUpgrade(card);
    }

    public static void afterUpgradeBlock(AbstractCard card) {
        afterUpgrade(card);
    }

    public static void afterUpgradeMagicNumber(AbstractCard card) {
        afterUpgrade(card);
    }

    public static void afterUpgradeBaseCost(AbstractCard card) {
        afterUpgrade(card);
    }

    private static void afterUpgrade(AbstractCard card) {
        if (card == null) {
            return;
        }
        synchronized (LOCK) {
            load();
            CardOverride override = OVERRIDES.get(card.cardID);
            if (override != null && !override.isEmpty()) {
                applyOverride(card, override, true);
            }
            APPLIED_LIVE_CARDS.add(card.uuid);
        }
    }

    private static void applyOverride(AbstractCard card, CardOverride o, boolean upgraded) {
        boolean changed = false;
        if (o.name != null && o.name.length() > 0) {
            String n = o.name;
            if (upgraded && !n.endsWith("+")) {
                n = n + "+";
            }
            if (!n.equals(card.name)) {
                card.name = n;
                card.originalName = o.name;
                changed = true;
            }
        }
        if (o.description != null) {
            if (!o.description.equals(card.rawDescription)) {
                card.rawDescription = o.description;
                changed = true;
            }
        }
        int oldCost = card.cost;
        int oldCostForTurn = card.costForTurn;
        int oldBaseMagic = card.baseMagicNumber;
        int oldMagic = card.magicNumber;
        if (upgraded) {
            if (o.upgradeCost != null) {
                card.cost = o.upgradeCost.intValue();
                card.costForTurn = LiveCostPolicy.afterBaseCostChange(oldCost, oldCostForTurn, card.cost);
                card.upgradedCost = true;
            }
            if (o.upgradeDamage != null) {
                card.baseDamage = o.upgradeDamage.intValue();
                card.upgradedDamage = true;
            }
            if (o.upgradeBlock != null) {
                card.baseBlock = o.upgradeBlock.intValue();
                card.upgradedBlock = true;
            }
            if (o.upgradeMagicNumber != null) {
                card.baseMagicNumber = o.upgradeMagicNumber.intValue();
                card.magicNumber = card.baseMagicNumber + (oldMagic - oldBaseMagic);
                card.upgradedMagicNumber = true;
            }
        } else {
            if (o.cost != null) {
                card.cost = o.cost.intValue();
                card.costForTurn = LiveCostPolicy.afterBaseCostChange(oldCost, oldCostForTurn, card.cost);
            }
            if (o.damage != null) {
                card.baseDamage = o.damage.intValue();
            }
            if (o.block != null) {
                card.baseBlock = o.block.intValue();
            }
            if (o.magicNumber != null) {
                card.baseMagicNumber = o.magicNumber.intValue();
                card.magicNumber = card.baseMagicNumber + (oldMagic - oldBaseMagic);
            }
        }
        if (o.draw != null) {
            card.baseDraw = o.draw.intValue();
            card.draw = card.baseDraw;
        }
        if (o.discard != null) {
            card.baseDiscard = o.discard.intValue();
            card.discard = card.baseDiscard;
        }
        if (o.heal != null) {
            card.baseHeal = o.heal.intValue();
            card.heal = card.baseHeal;
        }
        if (changed) {
            try {
                card.initializeDescription();
            } catch (Throwable t) {
                log("initializeDescription failed for " + card.cardID + ": " + t);
            }
        }
    }

    public static synchronized CardSpec findCustomSpec(String id) {
        for (CardSpec spec : CUSTOM_CARDS) {
            if (spec.id != null && spec.id.equals(id)) {
                return spec;
            }
        }
        return null;
    }

    public static synchronized List<CardSpec> copyCustomSpecs() {
        return new ArrayList<CardSpec>(CUSTOM_CARDS);
    }

    public static synchronized CardOverride findOverride(String id) {
        return OVERRIDES.get(id);
    }

    /**
     * Replaces the runtime definition of an existing custom card, or registers a
     * new custom card while the process is already running.
     */
    public static synchronized void updateCustomDefinition(CardSpec spec) {
        boolean replaced = false;
        for (int i = 0; i < CUSTOM_CARDS.size(); i++) {
            if (CUSTOM_CARDS.get(i).id.equals(spec.id)) {
                CUSTOM_CARDS.set(i, spec);
                replaced = true;
                break;
            }
        }
        if (!replaced) {
            CUSTOM_CARDS.add(spec);
        }
        AbstractCard proto = CardLibrary.cards.get(spec.id);
        if (proto instanceof CustomCard) {
            ((CustomCard) proto).applySpecLive(spec);
        } else if (proto == null) {
            CustomCard card = new CustomCard(spec);
            CardLibrary.add(card);
            card.isSeen = true;
            card.isLocked = false;
            UnlockTracker.lockedCards.remove(spec.id);
            log("registered new custom card at runtime id=" + spec.id);
        } else {
            throw new IllegalStateException("card id collides with non-custom card: " + spec.id);
        }
        log("updated custom definition id=" + spec.id + " replaced=" + replaced
                + " effects=" + spec.effects.size());
    }

    public static synchronized void applyCustomSpecToInstance(CustomCard card, CardSpec spec) {
        card.applySpecLive(spec);
    }

    public static synchronized void updateOverride(String id, CardOverride override) {
        if (override == null || override.isEmpty()) {
            OVERRIDES.remove(id);
        } else {
            OVERRIDES.put(id, override);
        }
        AbstractCard proto = CardLibrary.cards.get(id);
        if (proto != null && override != null && !override.isEmpty()) {
            applyOverride(proto, override, false);
            refreshCard(proto);
        }
        log("updated override id=" + id + " fields=" + (override == null ? 0 : 1));
    }

    public static synchronized void applyOverrideToInstance(AbstractCard card, CardOverride override) {
        if (card == null || override == null || override.isEmpty()) {
            return;
        }
        boolean upgraded = card.upgraded || card.timesUpgraded > 0;
        applyOverride(card, override, upgraded);
        refreshCard(card);
    }

    public static synchronized void refreshCard(AbstractCard card) {
        if (card == null) {
            return;
        }
        try {
            card.initializeDescription();
        } catch (Throwable t) {
            log("refreshCard initializeDescription failed for " + card.cardID + ": " + t);
        }
        try {
            boolean combat = AbstractDungeon.player != null && AbstractDungeon.getCurrRoom() != null
                    && AbstractDungeon.getCurrRoom().phase == AbstractRoom.RoomPhase.COMBAT;
            if (combat) {
                card.applyPowers();
            } else {
                if (card.baseDamage >= 0) card.damage = card.baseDamage;
                if (card.baseBlock >= 0) card.block = card.baseBlock;
                if (card.baseMagicNumber >= 0) card.magicNumber = card.baseMagicNumber;
            }
            if (AbstractDungeon.player != null && AbstractDungeon.player.hand != null
                    && AbstractDungeon.player.hand.contains(card)) {
                AbstractDungeon.player.hand.refreshHandLayout();
            }
        } catch (Throwable t) {
            log("refreshCard applyPowers failed for " + card.cardID + ": " + t);
        }
    }

    public static List<String> validateSpec(CardSpec s) {
        List<String> errors = new ArrayList<String>();
        if (s == null) {
            errors.add("spec is null");
            return errors;
        }
        if (s.id == null || !s.id.startsWith("stseditor_")) {
            errors.add("id must start with stseditor_");
        }
        if (s.name == null || s.name.length() == 0) {
            errors.add("name is empty");
        }
        if (s.cost < -2 || s.cost > 99) {
            errors.add("cost out of range");
        }
        if (s.description != null && s.description.contains("所有") && (s.description.contains("0费") || s.description.contains("零费"))) {
            boolean matches = false;
            for (CardSpec.EffectSpec effect : s.effects) if ("ALL_CARDS_ZERO_COST".equals(effect.type) || "JAVA_EXTENSION".equals(effect.type)) matches = true;
            if (!matches) errors.add("description promises zero cost but the executable effect is missing");
        }
        if (s.effects == null || s.effects.isEmpty()) {
            errors.add("no effects");
        } else {
            for (CardSpec.EffectSpec e : s.effects) {
                if (!CustomCard.supports(e)) {
                    errors.add("unsupported effect " + e.type);
                    continue;
                }
                if (e.type == null) continue;
                if ("JAVA_EXTENSION".equals(e.type)) {
                    String error = ExtensionRegistry.validate(e.extensionId);
                    if (error != null) errors.add(error);
                }
                if ("DAMAGE".equals(e.type)) {
                    if (e.amount == null || e.amount.intValue() < 0 || e.amount.intValue() > 999) errors.add("DAMAGE amount out of range");
                    if (e.hits != null && (e.hits.intValue() < 1 || e.hits.intValue() > 10)) errors.add("DAMAGE hits out of range");
                    if (e.upgradeHits != null && (e.upgradeHits.intValue() < 1 || e.upgradeHits.intValue() > 10)) errors.add("DAMAGE upgradeHits out of range");
                } else if ("ADD_CARD".equals(e.type)) {
                    if (e.cardId == null || e.cardId.length() == 0) {
                        errors.add("ADD_CARD missing cardId");
                    } else if (e.cardId.equals(s.id)) {
                        errors.add("ADD_CARD cannot reference itself");
                    } else if (CardLibrary.getCard(e.cardId) == null) {
                        errors.add("ADD_CARD unknown cardId " + e.cardId);
                    }
                    if (e.destination != null) {
                        String d = e.destination.toUpperCase();
                        if (!("HAND".equals(d) || "DRAW".equals(d) || "DRAW_PILE".equals(d)
                                || "DISCARD".equals(d) || "DISCARD_PILE".equals(d))) {
                            errors.add("ADD_CARD destination invalid");
                        }
                    }
                } else if ("EXHAUST_CARDS".equals(e.type) || "EXHAUST".equals(e.type)
                        || "DISCARD".equals(e.type) || "DISCARD_CARDS".equals(e.type)) {
                    if (e.amount == null || e.amount.intValue() < 1 || e.amount.intValue() > 10) errors.add(e.type + " amount out of range");
                } else if ("APPLY_POWER".equals(e.type)) {
                    if (!isSupportedPower(e.power)) errors.add("unsupported power " + e.power);
                    if (e.amount == null || e.amount.intValue() < 1 || e.amount.intValue() > 999) errors.add("APPLY_POWER amount out of range");
                }
                if (e.target != null) {
                    String t = e.target.toUpperCase();
                    if (!("ENEMY".equals(t) || "ALL_ENEMY".equals(t) || "SELF".equals(t)
                            || "NONE".equals(t) || "ALL".equals(t) || "SELF_AND_ENEMY".equals(t))) {
                        errors.add("invalid effect target " + e.target);
                    }
                }
                if (e.condition != null) {
                    String ct = CardSpec.str(e.condition, "type", "");
                    if (!("HAS_POWER".equalsIgnoreCase(ct) || "HP_BELOW_PERCENT".equalsIgnoreCase(ct)
                            || "HAS_BLOCK".equalsIgnoreCase(ct) || "HAND_COUNT_AT_LEAST".equalsIgnoreCase(ct)
                            || "ENERGY_AT_LEAST".equalsIgnoreCase(ct) || "ENEMY_COUNT_AT_LEAST".equalsIgnoreCase(ct))) {
                        errors.add("unsupported condition " + ct);
                    }
                }
            }
        }
        if (!("ATTACK".equals(s.type) || "SKILL".equals(s.type) || "POWER".equals(s.type)
                || "STATUS".equals(s.type) || "CURSE".equals(s.type))) {
            errors.add("invalid type " + s.type);
        }
        if (!("RED".equals(s.color) || "GREEN".equals(s.color) || "BLUE".equals(s.color) || "PURPLE".equals(s.color) || "COLORLESS".equals(s.color))) {
            errors.add("invalid color " + s.color);
        }
        if (!("ENEMY".equals(s.target) || "ALL_ENEMY".equals(s.target) || "SELF".equals(s.target)
                || "NONE".equals(s.target) || "SELF_AND_ENEMY".equals(s.target) || "ALL".equals(s.target))) {
            errors.add("invalid target " + s.target);
        }
        return errors;
    }

    public static boolean isSupportedPower(String power) {
        if (power == null) return false;
        return "Strength".equalsIgnoreCase(power) || "Dexterity".equalsIgnoreCase(power)
                || "Vulnerable".equalsIgnoreCase(power) || "Weak".equalsIgnoreCase(power)
                || "Weakened".equalsIgnoreCase(power) || "Frail".equalsIgnoreCase(power)
                || "Poison".equalsIgnoreCase(power) || "Thorns".equalsIgnoreCase(power);
    }

    private static void load() {
        if (loaded) {
            return;
        }
        loaded = true;
        File file = contentFile();
        if (!file.isFile()) {
            log("content file not found: " + file.getAbsolutePath());
            return;
        }
        BufferedReader reader = null;
        try {
            reader = new BufferedReader(new InputStreamReader(new FileInputStream(file), Charset.forName("UTF-8")));
            StringBuilder sb = new StringBuilder();
            char[] buf = new char[8192];
            int n;
            while ((n = reader.read(buf)) > 0) {
                sb.append(buf, 0, n);
            }
            JsonObject root = new JsonParser().parse(sb.toString()).getAsJsonObject();
            int schema = root.has("schemaVersion") ? root.get("schemaVersion").getAsInt() : 0;
            if (schema < CardSpec.MIN_SCHEMA_VERSION || schema > CardSpec.SCHEMA_VERSION) {
                log("unsupported content schemaVersion=" + schema
                        + " supported=" + CardSpec.MIN_SCHEMA_VERSION + ".." + CardSpec.SCHEMA_VERSION);
                return;
            }
            if (schema < CardSpec.SCHEMA_VERSION) {
                log("migrating content schemaVersion " + schema + " -> " + CardSpec.SCHEMA_VERSION
                        + " (new fields default to disabled)");
            }
            JsonElement ov = root.get("overrides");
            if (ov != null && ov.isJsonObject()) {
                for (Map.Entry<String, JsonElement> e : ov.getAsJsonObject().entrySet()) {
                    if (e.getValue().isJsonObject()) {
                        CardOverride o = CardOverride.parse(e.getKey(), e.getValue().getAsJsonObject());
                        if (!o.isEmpty()) {
                            OVERRIDES.put(e.getKey(), o);
                        }
                    }
                }
            }
            JsonElement cards = root.get("customCards");
            if (cards != null && cards.isJsonArray()) {
                for (JsonElement e : cards.getAsJsonArray()) {
                    if (!e.isJsonObject()) {
                        continue;
                    }
                    try {
                        CardSpec spec = CardSpec.parse(e.getAsJsonObject());
                        if (validate(spec)) {
                            CUSTOM_CARDS.add(spec);
                        }
                    } catch (Throwable t) {
                        log("invalid custom card: " + t);
                    }
                }
            }
            log("loaded content: custom=" + CUSTOM_CARDS.size() + " overrides=" + OVERRIDES.size()
                    + " file=" + file.getAbsolutePath());
        } catch (Throwable t) {
            log("failed to load content: " + t);
        } finally {
            if (reader != null) {
                try {
                    reader.close();
                } catch (Throwable ignored) {
                }
            }
        }
    }

    private static boolean validate(CardSpec s) {
        List<String> errors = validateSpec(s);
        if (!errors.isEmpty()) {
            log("reject custom card " + (s == null ? "(null)" : s.id) + ": " + errors);
            return false;
        }
        return true;
    }

    public static void debugDialog(LargeDialogOptionButton button) {
        if (!debugFile().isFile()) {
            return;
        }
        long now = System.currentTimeMillis();
        if (now - lastDialogLog < 300L) {
            return;
        }
        lastDialogLog = now;
        log("dialog hb x=" + button.hb.x + " y=" + button.hb.y
                + " w=" + button.hb.width + " h=" + button.hb.height
                + " cX=" + button.hb.cX + " cY=" + button.hb.cY
                + " hovered=" + button.hb.hovered
                + " mouse=" + InputHelper.mX + "," + InputHelper.mY
                + " down=" + InputHelper.justClickedLeft
                + " up=" + InputHelper.justReleasedClickLeft);
    }

    private static File debugFile() {
        return new File(new File(System.getProperty("user.dir"), "sts-card-editor"), "debug-input.flag");
    }

    private static File contentFile() {
        String custom = System.getProperty("sts.editor.content");
        if (custom != null && custom.length() > 0) {
            return new File(custom);
        }
        return new File(new File(System.getProperty("user.dir"), "sts-card-editor"), "content.json");
    }

    public static synchronized void log(String message) {
        String line = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss.SSS").format(new Date()) + " " + message;
        try {
            File dir = new File(System.getProperty("user.dir"), "sts-card-editor");
            if (!dir.isDirectory() && !dir.mkdirs()) {
                return;
            }
            PrintWriter out = new PrintWriter(new OutputStreamWriter(
                    new FileOutputStream(new File(dir, "runtime.log"), true), Charset.forName("UTF-8")));
            try {
                out.println(line);
            } finally {
                out.close();
            }
        } catch (Throwable ignored) {
        }
        System.out.println("[sts-card-editor] " + line);
    }
}

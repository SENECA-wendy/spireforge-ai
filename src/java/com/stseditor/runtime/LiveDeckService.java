package com.stseditor.runtime;

import com.google.gson.*;
import com.megacrit.cardcrawl.cards.AbstractCard;
import com.megacrit.cardcrawl.cards.CardGroup;
import com.megacrit.cardcrawl.characters.AbstractPlayer;
import com.megacrit.cardcrawl.core.CardCrawlGame;
import com.megacrit.cardcrawl.dungeons.AbstractDungeon;
import com.megacrit.cardcrawl.helpers.CardLibrary;
import com.megacrit.cardcrawl.rooms.AbstractRoom;
import com.megacrit.cardcrawl.relics.AbstractRelic;
import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.security.MessageDigest;
import java.util.*;

/** All methods are called by RuntimeBridge on the game thread. */
public final class LiveDeckService {
    private static AbstractPlayer trackedPlayer;
    private static String runToken = "";
    private static final Gson GSON = new Gson();
    private static final String[] GROUPS = {"masterDeck", "hand", "drawPile", "discardPile", "exhaustPile"};

    public static String runKey() {
        AbstractPlayer p = AbstractDungeon.player;
        if (p != trackedPlayer) {
            trackedPlayer = p;
            runToken = UUID.randomUUID().toString();
        }
        return runToken;
    }

    public static void describe(JsonObject snapshot) {
        if(AbstractDungeon.player!=null){snapshot.addProperty("playerClass",AbstractDungeon.player.chosenClass.name());snapshot.addProperty("cardColor",AbstractDungeon.player.getCardColor().name());}
        snapshot.addProperty("runKey", runKey());
        JsonObject versions = new JsonObject();
        for (String group : GROUPS) versions.addProperty(group, fingerprint(group(group)));
        snapshot.add("groupVersions", versions);
    }

    public static JsonObject catalog() {
        JsonObject result = new JsonObject();
        JsonArray cards = new JsonArray();
        List<String> ids = new ArrayList<String>(CardLibrary.cards.keySet());
        Collections.sort(ids);
        for (String id : ids) {
            AbstractCard c = CardLibrary.cards.get(id);
            if (c == null) continue;
            JsonObject item = new JsonObject();
            item.addProperty("cardId", id);
            item.addProperty("name", c.name);
            item.addProperty("color", c.color.name());
            item.addProperty("type", c.type.name());
            item.addProperty("rarity", c.rarity.name());
            item.addProperty("isCustom", c instanceof CustomCard);
            item.addProperty("obtainable", RunCardPoolService.isObtainable(c));
            cards.add(item);
        }
        result.add("cards", cards);
        return result;
    }

    public static JsonObject edit(JsonObject payload, long revision, long currentRevision) throws Exception {
        if (revision <= currentRevision) fail("修改版本已过期，请刷新本局牌组后重试。");
        AbstractPlayer p = AbstractDungeon.player;
        if (p == null || p.isDead || CardCrawlGame.mode != CardCrawlGame.GameMode.GAMEPLAY)
            fail("当前不在可编辑的游戏局内。");
        if (!runKey().equals(text(payload, "runKey", ""))) fail("游戏局已变化，请刷新本局牌组。");
        String scope = text(payload, "group", "masterDeck");
        CardGroup target = group(scope);
        if (target == null) fail("未知或不可用的牌区。");
        boolean master = "masterDeck".equals(scope);
        boolean combat = AbstractDungeon.getCurrRoom().phase == AbstractRoom.RoomPhase.COMBAT;
        if (!master && !combat) fail("战斗牌区只能在战斗中编辑。");
        if (!fingerprint(target).equals(text(payload, "expectedVersion", "")))
            fail("牌组在快照后已经变化，未执行修改。请刷新后重试。");
        boolean sync = flag(payload, "syncCombat", false);
        if (sync && !master) fail("同步战斗副本只适用于主牌组。");
        String action = text(payload, "action", "");
        if (!("ADD".equals(action) || "REMOVE".equals(action) || "UPGRADE".equals(action)))
            fail("不支持的牌组操作。");

        // Build a complete plan first. No live card group is mutated during validation.
        Map<CardGroup, List<AbstractCard>> before = new LinkedHashMap<CardGroup, List<AbstractCard>>();
        Map<CardGroup, List<AbstractCard>> after = new LinkedHashMap<CardGroup, List<AbstractCard>>();
        for (String name : GROUPS) {
            CardGroup g = group(name);
            if (g != null) {
                before.put(g, new ArrayList<AbstractCard>(g.group));
                after.put(g, new ArrayList<AbstractCard>(g.group));
            }
        }
        List<CardSpec> missing = new ArrayList<CardSpec>();
        List<AbstractCard> additions = new ArrayList<AbstractCard>();
        Set<AbstractCard> affected = Collections.newSetFromMap(new IdentityHashMap<AbstractCard, Boolean>());
        int changed = 0;
        if ("ADD".equals(action)) {
            JsonArray entries = array(payload, "entries");
            if (entries == null || entries.size() < 1 || entries.size() > 100) fail("每次加入 1 至 100 项卡牌。");
            Map<String, CardSpec> supplied = new HashMap<String, CardSpec>();
            JsonArray definitions = array(payload, "customCards");
            if (definitions != null) {
                for (JsonElement e : definitions) {
                    CardSpec spec = CardSpec.parse(e.getAsJsonObject());
                    if (spec.id == null || supplied.put(spec.id, spec) != null) fail("卡牌定义 ID 缺失或重复。");
                }
            }
            Map<String, AbstractCard> templates = new HashMap<String, AbstractCard>();
            int total = 0;
            for (JsonElement e : entries) {
                JsonObject entry = e.getAsJsonObject();
                String id = text(entry, "cardId", "");
                int count = number(entry, "count", 1);
                int upgrades = number(entry, "upgrades", 0);
                int misc = number(entry, "misc", 0);
                if (count < 1 || count > 100 || upgrades < 0 || upgrades > 100 || misc < 0)
                    fail("卡牌数量/升级次数/附加数值超出允许范围。");
                total += count;
                if (total > 100) fail("一次最多加入 100 张牌。");
                AbstractCard proto = templates.get(id);
                if (proto == null) {
                    proto = CardLibrary.cards.get(id);
                    if (proto == null) {
                        CardSpec spec = supplied.get(id);
                        if (spec == null) fail("找不到卡牌定义：" + id);
                        List<String> errors = ContentRegistry.validateSpec(spec);
                        if (!errors.isEmpty()) fail("卡牌定义无效：" + id + " " + errors);
                        proto = new CustomCard(spec);
                        missing.add(spec);
                    }
                    templates.put(id, proto);
                }
                for (int i = 0; i < count; i++) {
                    AbstractCard copy = proto.makeCopy();
                    copy.misc = misc;
                    // getCopy includes the original game's misc-specific initialization.
                    if (CardLibrary.cards.containsKey(id)) copy = CardLibrary.getCopy(id, upgrades, misc);
                    else for (int n = 0; n < upgrades; n++) copy.upgrade();
                    if (copy.timesUpgraded != upgrades) fail("该卡不支持指定的升级次数：" + id);
                    copy.isSeen = true;
                    copy.isLocked = false;
                    additions.add(copy);
                    after.get(target).add(copy);
                    if (master && sync && combat) {
                        AbstractCard battle = copy.makeSameInstanceOf();
                        after.get(p.drawPile).add(battle);
                    }
                }
            }
            changed = total;
        } else {
            JsonArray selection = array(payload, "instanceIds");
            if (selection == null || selection.size() < 1 || selection.size() > 500) fail("请先选择卡牌。");
            Set<String> seen = new HashSet<String>();
            for (JsonElement element : selection) {
                String instance = element.getAsString();
                if (!seen.add(instance)) fail("选择中存在重复实例。");
                AbstractCard card = find(target, scope, instance);
                if (card == null) fail("选中卡牌已移动或不存在，请刷新。");
                if (master && (card.inBottleFlame || card.inBottleLightning || card.inBottleTornado))
                    fail("瓶装牌请使用游戏内升级功能；移除前需解除瓶装遗物关联，避免遗物引用失效。");
                List<CardGroup> destinations = new ArrayList<CardGroup>();
                destinations.add(target);
                if (master && sync && combat) {
                    for (String name : GROUPS) {
                        if (!"masterDeck".equals(name)) {
                            CardGroup g = group(name);
                            if (g != null) destinations.add(g);
                        }
                    }
                }
                boolean didChange = false;
                for (CardGroup g : destinations) {
                    List<AbstractCard> planned = after.get(g);
                    for (int i = planned.size() - 1; i >= 0; i--) {
                        AbstractCard match = planned.get(i);
                        // Synchronize only the corresponding instance, never every card of the same ID.
                        if (g == target ? match != card : !card.uuid.equals(match.uuid)) continue;
                        if ("REMOVE".equals(action)) {
                            planned.remove(i);
                            affected.add(match);
                            didChange = true;
                        } else if (match.canUpgrade()) {
                            AbstractCard upgraded = match.makeSameInstanceOf();
                            preserveBattleFlags(match, upgraded);
                            upgraded.upgrade();
                            if (upgraded.timesUpgraded == match.timesUpgraded) continue;
                            planned.set(i, upgraded);
                            affected.add(match);
                            didChange = true;
                        }
                    }
                }
                if (didChange) changed++;
            }
        }
        if (after.get(p.masterDeck).isEmpty()) fail("主牌组至少保留一张牌。");
        for (Map.Entry<CardGroup, List<AbstractCard>> e : after.entrySet()) {
            if (e.getValue().size() > 500) fail("牌区超过 500 张，已停止修改。");
            if (e.getKey() == p.hand && e.getValue().size() > 10) fail("手牌最多 10 张；请加入抽牌堆或主牌组。");
        }
        if (changed == 0) fail("选中卡牌均无法继续升级，未修改牌组。");

        // Persist all referenced custom definitions before they can enter a saved run.
        // Existing definitions are preserved; adding a preset does not overwrite live designs.
        if (!additions.isEmpty()) persistDefinitions(additions);
        for (CardSpec spec : missing) ContentRegistry.updateCustomDefinition(spec);
        AbstractCard previousHovered = p.hoveredCard;
        try {
            for (Map.Entry<CardGroup, List<AbstractCard>> e : after.entrySet()) {
                e.getKey().group.clear();
                e.getKey().group.addAll(e.getValue());
            }
            if (p.hoveredCard != null && affected.contains(p.hoveredCard)) p.hoveredCard = null;
            if (combat) {
                p.hand.refreshHandLayout();
                p.hand.applyPowers();
            }
        } catch (Throwable failure) {
            for (Map.Entry<CardGroup, List<AbstractCard>> e : before.entrySet()) {
                e.getKey().group.clear();
                e.getKey().group.addAll(e.getValue());
            }
            p.hoveredCard = previousHovered;
            throw new IllegalStateException("牌组修改失败，已恢复原牌组。", failure);
        }
        JsonObject result = new JsonObject();
        // Refresh deck-dependent relic counters, without replaying acquire/remove rewards.
        if (master && p.relics != null) for (AbstractRelic relic : new ArrayList<AbstractRelic>(p.relics)) {
            try { relic.onMasterDeckChange(); }
            catch (Throwable failure) {
                result.addProperty("warning", "牌组已修改，但有遗物刷新失败，请查看日志。");
                ContentRegistry.log("deckEdit relic refresh failed: " + failure);
            }
        }
        result.addProperty("edited", true);
        result.addProperty("action", action);
        result.addProperty("changed", changed);
        result.addProperty("masterCount", p.masterDeck.size());
        result.addProperty("group", scope);
        result.addProperty("syncCombat", sync);
        result.addProperty("saveNote", master ? "已修改本局主牌组，由游戏正常保存；尚未强制写入存档。" : "仅当前战斗牌区生效。");
        ContentRegistry.log("deckEdit action=" + action + " group=" + scope + " changed=" + changed + " revision=" + revision);
        return result;
    }

    private static void preserveBattleFlags(AbstractCard source, AbstractCard copy) {
        copy.retain = source.retain;
        copy.selfRetain = source.selfRetain;
        copy.exhaust = source.exhaust;
        copy.exhaustOnUseOnce = source.exhaustOnUseOnce;
        copy.exhaustOnFire = source.exhaustOnFire;
        copy.isEthereal = source.isEthereal;
        copy.isInnate = source.isInnate;
        copy.purgeOnUse = source.purgeOnUse;
        copy.dontTriggerOnUseCard = source.dontTriggerOnUseCard;
        copy.returnToHand = source.returnToHand;
        copy.shuffleBackIntoDrawPile = source.shuffleBackIntoDrawPile;
        copy.freeToPlayOnce = source.freeToPlayOnce;
        copy.ignoreEnergyOnUse = source.ignoreEnergyOnUse;
        copy.energyOnUse = source.energyOnUse;
    }

    private static void persistDefinitions(List<AbstractCard> additions) throws Exception {
        Map<String, CardSpec> required = new LinkedHashMap<String, CardSpec>();
        for (AbstractCard c : additions) if (c instanceof CustomCard) required.put(c.cardID, ((CustomCard)c).getSpec());
        if (required.isEmpty()) return;
        Path path = new File(new File(System.getProperty("user.dir"), "sts-card-editor"), "content.json").toPath();
        byte[] original = Files.exists(path) ? Files.readAllBytes(path) : null;
        JsonObject root = original == null ? new JsonObject() : new JsonParser().parse(new String(original, StandardCharsets.UTF_8)).getAsJsonObject();
        JsonArray cards = root.has("customCards") ? root.getAsJsonArray("customCards") : new JsonArray();
        for (JsonElement element : cards) required.remove(text(element.getAsJsonObject(), "id", ""));
        if (required.isEmpty()) return;
        for (CardSpec spec : required.values()) cards.add(GSON.toJsonTree(spec));
        root.addProperty("schemaVersion", 2);
        root.add("customCards", cards);
        if (!root.has("overrides")) root.add("overrides", new JsonObject());
        Files.createDirectories(path.getParent());
        if (original != null) Files.copy(path, path.resolveSibling("content-before-deck-" + System.currentTimeMillis() + ".json"));
        Path temp = Files.createTempFile(path.getParent(), "content-", ".tmp");
        try {
            Files.write(temp, GSON.toJson(root).getBytes(StandardCharsets.UTF_8));
            // Detect another editor writing content.json rather than silently overwrite its changes.
            if (original != null && !Arrays.equals(original, Files.readAllBytes(path))) fail("卡牌定义文件同时被修改，请重试。");
            try { Files.move(temp, path, StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING); }
            catch (AtomicMoveNotSupportedException e) { Files.move(temp, path, StandardCopyOption.REPLACE_EXISTING); }
        } finally { Files.deleteIfExists(temp); }
    }

    private static CardGroup group(String name) {
        AbstractPlayer p = AbstractDungeon.player;
        if (p == null) return null;
        if ("masterDeck".equals(name)) return p.masterDeck;
        if ("hand".equals(name)) return p.hand;
        if ("drawPile".equals(name)) return p.drawPile;
        if ("discardPile".equals(name)) return p.discardPile;
        if ("exhaustPile".equals(name)) return p.exhaustPile;
        return null;
    }

    private static AbstractCard find(CardGroup group, String name, String instance) {
        for (AbstractCard c : group.group) if ((name + ":" + c.uuid).equals(instance)) return c;
        return null;
    }

    private static String fingerprint(CardGroup group) {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            if (group != null) for (AbstractCard c : group.group)
                digest.update((c.uuid + "|" + c.cardID + "|" + c.timesUpgraded + "|" + c.misc + ";").getBytes(StandardCharsets.UTF_8));
            StringBuilder hex = new StringBuilder();
            for (byte b : digest.digest()) hex.append(String.format("%02x", b & 255));
            return hex.toString();
        } catch (Exception e) { throw new IllegalStateException(e); }
    }

    private static void fail(String message) { throw new IllegalArgumentException(message); }
    private static String text(JsonObject o, String key, String fallback) {
        JsonElement value = o.get(key);
        return value == null || value.isJsonNull() ? fallback : value.getAsString();
    }
    private static boolean flag(JsonObject o, String key, boolean fallback) {
        return o.has(key) && !o.get(key).isJsonNull() ? o.get(key).getAsBoolean() : fallback;
    }
    private static int number(JsonObject o, String key, int fallback) {
        return o.has(key) && !o.get(key).isJsonNull() ? o.get(key).getAsInt() : fallback;
    }
    private static JsonArray array(JsonObject o, String key) {
        return o.has(key) && o.get(key).isJsonArray() ? o.getAsJsonArray(key) : null;
    }
}

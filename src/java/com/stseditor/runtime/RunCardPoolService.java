package com.stseditor.runtime;

import com.google.gson.*;
import com.megacrit.cardcrawl.cards.AbstractCard;
import com.megacrit.cardcrawl.core.Settings;
import com.megacrit.cardcrawl.dungeons.AbstractDungeon;
import com.megacrit.cardcrawl.helpers.CardLibrary;
import com.megacrit.cardcrawl.shop.ShopScreen;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;

/** Run-scoped acquisition policy; never shrinks the engine's underlying random pools. */
public final class RunCardPoolService {
    private static String identity = "";
    private static boolean enabled;
    private static LinkedHashSet<String> allowed = new LinkedHashSet<String>();
    private static final Gson GSON = new Gson();

    public static boolean isObtainable(AbstractCard card) {
        return card != null && (card.type == AbstractCard.CardType.ATTACK || card.type == AbstractCard.CardType.SKILL || card.type == AbstractCard.CardType.POWER);
    }

    private static String currentIdentity() {
        return AbstractDungeon.player == null || Settings.seed == null ? "" : Settings.seed + ":" + AbstractDungeon.player.chosenClass.name() + ":" + AbstractDungeon.ascensionLevel;
    }

    private static Path stateFile() {
        return Paths.get(System.getProperty("user.dir"), "sts-card-editor", "run-card-pools", identity.replace(':', '_') + ".json");
    }

    private static void ensureRun() {
        String key = currentIdentity();
        if (identity.equals(key)) return;
        identity = key; enabled = false; allowed.clear();
        if (key.length() == 0 || !Files.isRegularFile(stateFile())) return;
        try {
            JsonObject doc = new JsonParser().parse(new String(Files.readAllBytes(stateFile()), StandardCharsets.UTF_8)).getAsJsonObject();
            if (!key.equals(doc.get("identity").getAsString())) return;
            LinkedHashSet<String> ids = parseIds(doc.getAsJsonArray("cardIds"));
            enabled = doc.get("enabled").getAsBoolean() && !ids.isEmpty(); allowed = ids;
        } catch (Exception error) { ContentRegistry.log("run card pool restore failed: " + error); }
    }

    /** Called only from the new-dungeon constructor, not from the save-load constructor. */
    public static void onNewDungeon() {
        if (AbstractDungeon.floorNum != 0) return; // Moving to another act preserves this run.
        identity = currentIdentity(); enabled = false; allowed.clear();
        if (identity.length() == 0) return;
        try { persist(false, allowed); } catch (Exception error) { ContentRegistry.log("run card pool reset failed: " + error); }
    }

    public static void onLoadDungeon() { identity = ""; ensureRun(); }

    public static JsonObject describe() {
        ensureRun();
        JsonObject data = new JsonObject();
        data.addProperty("enabled", enabled);
        if(AbstractDungeon.player!=null){data.addProperty("playerClass",AbstractDungeon.player.chosenClass.name());data.addProperty("cardColor",AbstractDungeon.player.getCardColor().name());}
        data.addProperty("runKey", LiveDeckService.runKey());
        JsonArray ids = new JsonArray(); for (String id : allowed) ids.add(id);
        data.add("cardIds", ids);
        data.addProperty("count", allowed.size());
        data.addProperty("scope", "SHOP_AND_CARD_REWARDS");
        return data;
    }

    public static JsonObject configure(JsonObject payload) throws Exception {
        ensureRun();
        if (AbstractDungeon.player == null || AbstractDungeon.player.isDead) throw new IllegalArgumentException("请先进入一局游戏。");
        if (!payload.has("runKey") || !LiveDeckService.runKey().equals(payload.get("runKey").getAsString()))
            throw new IllegalArgumentException("游戏局已变化，请重新读取本局牌池。");
        boolean nextEnabled = payload.has("enabled") && payload.get("enabled").getAsBoolean();
        LinkedHashSet<String> next = nextEnabled ? parseIds(payload.getAsJsonArray("cardIds")) : new LinkedHashSet<String>();
        if (nextEnabled && next.isEmpty()) throw new IllegalArgumentException("牌池至少保留一种卡牌；如需原版请点恢复原版牌池。");
        persist(nextEnabled, next); // Disk failure must not silently apply half a change.
        enabled = nextEnabled; allowed = next;
        ContentRegistry.log("run card pool configured enabled=" + enabled + " cards=" + allowed.size());
        JsonObject result = describe(); result.addProperty("applied", true); return result;
    }

    private static LinkedHashSet<String> parseIds(JsonArray array) {
        if (array == null || array.size() > 2000) throw new IllegalArgumentException("牌池列表无效或超出2000种。");
        LinkedHashSet<String> result = new LinkedHashSet<String>();
        for (JsonElement item : array) {
            String id = item.getAsString();
            if (!isObtainable(CardLibrary.getCard(id))) throw new IllegalArgumentException("找不到可获得的卡牌定义：" + id + "；自定义卡请先同步定义。");
            result.add(id);
        }
        return result;
    }

    private static void persist(boolean active, Set<String> ids) throws Exception {
        Path file = stateFile(); Files.createDirectories(file.getParent());
        JsonObject doc = new JsonObject(); doc.addProperty("schemaVersion", 1); doc.addProperty("identity", identity); doc.addProperty("enabled", active);
        JsonArray array = new JsonArray(); for (String id : ids) array.add(id); doc.add("cardIds", array);
        Path pending = file.resolveSibling(file.getFileName().toString() + ".pending");
        Files.write(pending, GSON.toJson(doc).getBytes(StandardCharsets.UTF_8));
        try { Files.move(pending, file, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE); }
        catch (AtomicMoveNotSupportedException ignored) { Files.move(pending, file, StandardCopyOption.REPLACE_EXISTING); }
    }

    private static List<String> candidates() {
        ensureRun(); List<String> ids = new ArrayList<String>();
        if (enabled) for (String id : allowed) if (isObtainable(CardLibrary.getCard(id))) ids.add(id);
        return ids;
    }

    /** Preserve slot count and upgrades; uniqueness is relaxed only after exhausting the whitelist. */
    public static ArrayList<AbstractCard> rewards(ArrayList<AbstractCard> originals) { return replace(originals, false); }
    public static ArrayList<AbstractCard> shopOffers(ArrayList<AbstractCard> originals) { return replace(originals, true); }

    private static ArrayList<AbstractCard> replace(ArrayList<AbstractCard> originals, boolean shop) {
        List<String> ids = candidates();
        if (ids.isEmpty() || originals == null) return originals;
        ArrayList<AbstractCard> result = new ArrayList<AbstractCard>();
        List<String> remaining = new ArrayList<String>(ids);
        for (AbstractCard old : originals) {
            if (remaining.isEmpty()) remaining.addAll(ids);
            int index = shop ? AbstractDungeon.merchantRng.random(remaining.size() - 1) : AbstractDungeon.cardRng.random(remaining.size() - 1);
            String id = remaining.remove(index);
            AbstractCard copy = CardLibrary.getCopy(id, !shop && old != null ? old.timesUpgraded : 0, 0);
            result.add(copy);
        }
        return result;
    }

    /** Merchant slot membership, rather than card color, also supports a red card in a colorless slot. */
    public static AbstractCard.CardColor slotColor(AbstractCard card, ShopScreen shop) {
        if (shop.coloredCards.contains(card)) return AbstractDungeon.player==null?AbstractCard.CardColor.RED:AbstractDungeon.player.getCardColor();
        return shop.colorlessCards.contains(card) ? AbstractCard.CardColor.COLORLESS : card.color;
    }

    public static int shopBasePrice(AbstractCard.CardRarity rarity) {
        // Vanilla intentionally prices BASIC at 9999 because it never sells starter cards.
        return rarity == AbstractCard.CardRarity.BASIC || rarity == AbstractCard.CardRarity.SPECIAL ? 50 : AbstractCard.getPrice(rarity);
    }

    /** The Courier's vanilla generation/loops finish before the replacement is installed. */
    public static void afterPurchase(ShopScreen shop) {
        if (candidates().isEmpty()) return;
        refreshRestocks(shop.coloredCards); refreshRestocks(shop.colorlessCards);
    }

    private static void refreshRestocks(ArrayList<AbstractCard> cards) {
        for (int i = 0; i < cards.size(); i++) {
            AbstractCard old = cards.get(i);
            if (allowed.contains(old.cardID)) continue;
            ArrayList<AbstractCard> one = new ArrayList<AbstractCard>(); one.add(old);
            AbstractCard copy = shopOffers(one).get(0);
            copy.price = Math.max(0, Math.round(old.price * (float)shopBasePrice(copy.rarity) / Math.max(1, shopBasePrice(old.rarity))));
            copy.current_x = old.current_x; copy.current_y = old.current_y;
            copy.target_x = old.target_x; copy.target_y = old.target_y; copy.drawScale = old.drawScale; copy.targetDrawScale = old.targetDrawScale;
            for (com.megacrit.cardcrawl.relics.AbstractRelic relic : AbstractDungeon.player.relics) relic.onPreviewObtainCard(copy);
            cards.set(i, copy);
        }
    }
}

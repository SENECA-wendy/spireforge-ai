package com.stseditor.runtime;

import com.google.gson.Gson;
import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.megacrit.cardcrawl.actions.GameActionManager;
import com.megacrit.cardcrawl.cards.AbstractCard;
import com.megacrit.cardcrawl.cards.CardGroup;
import com.megacrit.cardcrawl.characters.AbstractPlayer;
import com.megacrit.cardcrawl.core.CardCrawlGame;
import com.megacrit.cardcrawl.dungeons.AbstractDungeon;
import com.megacrit.cardcrawl.helpers.CardLibrary;
import com.megacrit.cardcrawl.rooms.AbstractRoom;
import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.net.SocketTimeoutException;
import java.nio.charset.Charset;
import java.security.MessageDigest;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Deque;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

/**
 * Local loopback bridge for live card editing.
 *
 * The socket thread only parses/validates transport requests. Every read and
 * write of live game objects happens in tick(), which is injected into
 * CardCrawlGame.render() and therefore runs on the libGDX game thread.
 */
public final class RuntimeBridge {
    public static final int PROTOCOL_VERSION = 1;
    public static final String LAYER_VERSION = "1.6.0";
    public static final int MAX_LINE = 512 * 1024;
    public static final int MAX_REQUEST_ID = 120;

    private static final Object QUEUE_LOCK = new Object();
    private static final Object RESULT_LOCK = new Object();
    private static final Deque<Request> QUEUE = new ArrayDeque<Request>();
    private static final Map<String, JsonObject> RESULTS = new LinkedHashMap<String, JsonObject>();
    private static final Map<String, Request> IN_FLIGHT = new LinkedHashMap<String, Request>();
    private static final Gson GSON = new Gson();

    private static volatile boolean started = false;
    private static volatile boolean running = false;
    private static volatile long currentRevision = 1L;
    private static volatile String lastRequestId = "";
    private static volatile long lastAppliedAt = 0L;
    private static ServerSocket server;
    private static Thread acceptThread;
    private static String token;
    private static int port;

    private RuntimeBridge() {
    }

    public static synchronized void start() {
        if (started) {
            return;
        }
        started = true;
        try {
            token = UUID.randomUUID().toString().replace("-", "")
                    + UUID.randomUUID().toString().replace("-", "");
            server = new ServerSocket(0, 1, InetAddress.getByName("127.0.0.1"));
            server.setReuseAddress(false);
            port = server.getLocalPort();
            running = true;
            writeBridgeFile();
            acceptThread = new Thread(new Runnable() {
                @Override
                public void run() {
                    acceptLoop();
                }
            }, "stseditor-bridge-accept");
            acceptThread.setDaemon(true);
            acceptThread.start();
            Runtime.getRuntime().addShutdownHook(new Thread(new Runnable() {
                @Override
                public void run() {
                    stop();
                }
            }, "stseditor-bridge-shutdown"));
            ContentRegistry.log("RuntimeBridge started on 127.0.0.1:" + port
                    + " protocol=" + PROTOCOL_VERSION + " layer=" + LAYER_VERSION);
        } catch (Throwable t) {
            running = false;
            ContentRegistry.log("RuntimeBridge failed to start: " + t);
        }
    }

    public static synchronized void stop() {
        running = false;
        try {
            if (server != null) server.close();
        } catch (Throwable ignored) {
        }
        server = null;
        try {
            File f = bridgeFile();
            if (f.isFile()) f.delete();
        } catch (Throwable ignored) {
        }
    }

    /**
     * Injected into CardCrawlGame.render() by the patcher. This is the only
     * place where queued live operations touch game objects.
     */
    public static void tick() {
        if (!running) {
            return;
        }
        ZeroCostPower.refreshPlayer();
        List<Request> work = new ArrayList<Request>();
        synchronized (QUEUE_LOCK) {
            while (!QUEUE.isEmpty()) work.add(QUEUE.removeFirst());
        }
        List<Request> retry = new ArrayList<Request>();
        for (Request req : work) {
            if (req.done) continue;
            boolean safe = safeToApply(allowsMap(req));
            if (isMutation(req.op) && !safe) {
                if (System.currentTimeMillis() < req.deadline) {
                    retry.add(req);
                } else {
                    completeError(req, "等待安全时机超时：动作队列/界面/拖牌状态仍在变化");
                }
                continue;
            }
            try {
                JsonObject data = execute(req);
                complete(req, data);
            } catch (Throwable t) {
                completeError(req, t.getClass().getSimpleName() + ": " + t.getMessage());
            }
        }
        synchronized (QUEUE_LOCK) {
            for (Request req : retry) QUEUE.addLast(req);
        }
    }

    private static JsonObject execute(Request req) throws Exception {
        if ("status".equals(req.op)) {
            return statusData();
        }
        if ("capabilities".equals(req.op)) {
            return capabilitiesData();
        }
        if ("snapshot".equals(req.op)) {
            return snapshotData();
        }
        if ("validate".equals(req.op)) {
            return validateData(req.payload);
        }
        if ("apply".equals(req.op)) {
            return applyData(req);
        }
        if ("addCard".equals(req.op)) {
            return addCardData(req);
        }
        if ("deckEdit".equals(req.op)) {
            JsonObject data = LiveDeckService.edit(req.payload, req.revision, currentRevision);
            currentRevision = req.revision;
            lastRequestId = req.requestId;
            lastAppliedAt = System.currentTimeMillis();
            data.addProperty("revision", currentRevision);
            return data;
        }
        if ("cardCatalog".equals(req.op)) return LiveDeckService.catalog();
        if ("cardPool".equals(req.op)) return RunCardPoolService.describe();
        if ("setCardPool".equals(req.op)) {
            if (req.revision <= currentRevision) throw new IllegalArgumentException("牌池修改版本已过期，请刷新重试。");
            JsonObject result = RunCardPoolService.configure(req.payload);
            currentRevision = req.revision; lastRequestId = req.requestId; lastAppliedAt = System.currentTimeMillis();
            return result;
        }
        throw new IllegalArgumentException("unsupported op " + req.op);
    }

    private static JsonObject statusData() {
        JsonObject o = new JsonObject();
        AbstractPlayer p = AbstractDungeon.player;
        AbstractRoom room = safeRoom();
        o.addProperty("inRun", p != null && !p.isDead && CardCrawlGame.mode == CardCrawlGame.GameMode.GAMEPLAY);
        o.addProperty("inCombat", room != null && room.phase == AbstractRoom.RoomPhase.COMBAT);
        o.addProperty("safeToApply", safeToApply());
        o.addProperty("safeToEditDeck", safeToApply(true));
        o.addProperty("screen", AbstractDungeon.screen == null ? "NONE" : AbstractDungeon.screen.name());
        o.addProperty("screenUp", AbstractDungeon.isScreenUp);
        o.addProperty("actionsPending", p == null ? 0 : pendingActionCount());
        o.addProperty("revision", currentRevision);
        o.addProperty("lastRequestId", lastRequestId);
        o.addProperty("lastAppliedAt", lastAppliedAt);
        o.addProperty("layerVersion", LAYER_VERSION);
        o.addProperty("protocolVersion", PROTOCOL_VERSION);
        o.addProperty("gameVersion", CardCrawlGame.VERSION_NUM == null ? "unknown" : CardCrawlGame.VERSION_NUM);
        o.addProperty("pid", currentPid());
        if (p != null) {
            o.addProperty("playerClass", p.chosenClass.name());o.addProperty("cardColor", p.getCardColor().name());
            o.addProperty("hp", p.currentHealth);
            o.addProperty("maxHp", p.maxHealth);
            o.addProperty("block", p.currentBlock);
            o.addProperty("energy", p.energy == null ? 0 : p.energy.energy);
            o.addProperty("handCount", p.hand == null ? 0 : p.hand.size());
            o.addProperty("drawCount", p.drawPile == null ? 0 : p.drawPile.size());
            o.addProperty("discardCount", p.discardPile == null ? 0 : p.discardPile.size());
            o.addProperty("exhaustCount", p.exhaustPile == null ? 0 : p.exhaustPile.size());
            o.addProperty("masterDeckCount", p.masterDeck == null ? 0 : p.masterDeck.size());
        }
        if (room != null) o.addProperty("roomPhase", room.phase == null ? "UNKNOWN" : room.phase.name());
        else o.addProperty("roomPhase", "NONE");
        return o;
    }

    private static JsonObject capabilitiesData() {
        JsonObject o = new JsonObject();
        o.addProperty("protocolVersion", PROTOCOL_VERSION);
        o.addProperty("layerVersion", LAYER_VERSION);
        o.addProperty("gameVersion", CardCrawlGame.VERSION_NUM == null ? "unknown" : CardCrawlGame.VERSION_NUM);
        JsonArray ops = new JsonArray();
        ops.add("status"); ops.add("snapshot"); ops.add("capabilities");
        ops.add("validate"); ops.add("apply"); ops.add("addCard"); ops.add("result");
        ops.add("deckEdit"); ops.add("cardCatalog");
        ops.add("cardPool"); ops.add("setCardPool");
        o.add("ops", ops);
        JsonArray scopes = new JsonArray();
        scopes.add("INSTANCE"); scopes.add("COMBAT_ID"); scopes.add("MASTER_ID"); scopes.add("DEFINITION");
        o.add("scopes", scopes);
        JsonArray effects = new JsonArray();
        String[] supported = new String[] {
            "DAMAGE", "BLOCK", "DRAW", "GAIN_ENERGY", "LOSE_HP", "HEAL",
            "DISCARD", "DISCARD_CARDS", "EXHAUST_CARDS", "EXHAUST",
            "ADD_CARD", "APPLY_POWER", "START_TURN_BLOCK", "DRAW_ON_PLAY", "ALL_CARDS_ZERO_COST", "JAVA_EXTENSION"
        };
        for (String s : supported) effects.add(s);
        o.add("effects", effects);
        o.add("extensions", ExtensionRegistry.describe());
        JsonArray conditions = new JsonArray();
        conditions.add("HAS_POWER"); conditions.add("HP_BELOW_PERCENT"); conditions.add("HAS_BLOCK");
        conditions.add("HAND_COUNT_AT_LEAST"); conditions.add("ENERGY_AT_LEAST"); conditions.add("ENEMY_COUNT_AT_LEAST");
        o.add("conditions", conditions);
        o.add("triggers", new JsonArray());
        o.addProperty("maxRequestBytes", MAX_LINE);
        o.addProperty("liveCombatEdit", true);
        o.addProperty("mapDeckEdit", true);
        o.addProperty("runCardPool", true);
        JsonArray colors=new JsonArray();for(String color:new String[]{"RED","GREEN","BLUE","PURPLE","COLORLESS"})colors.add(color);o.add("customCardColors",colors);
        return o;
    }

    private static JsonObject snapshotData() {
        JsonObject o = new JsonObject();
        AbstractPlayer p = AbstractDungeon.player;
        if (p == null || p.isDead || CardCrawlGame.mode != CardCrawlGame.GameMode.GAMEPLAY) {
            o.addProperty("inRun", false);
            o.add("groups", new JsonObject());
            return o;
        }
        o.addProperty("inRun", true);
        AbstractRoom room = safeRoom();
        o.addProperty("inCombat", room != null && room.phase == AbstractRoom.RoomPhase.COMBAT);
        o.addProperty("safeToApply", safeToApply());
        o.addProperty("revision", currentRevision);
        o.addProperty("layerVersion", LAYER_VERSION);
        o.addProperty("protocolVersion", PROTOCOL_VERSION);
        o.addProperty("gameVersion", CardCrawlGame.VERSION_NUM == null ? "unknown" : CardCrawlGame.VERSION_NUM);
        JsonObject groups = new JsonObject();
        addGroup(groups, "masterDeck", p.masterDeck);
        addGroup(groups, "hand", p.hand);
        addGroup(groups, "drawPile", p.drawPile);
        addGroup(groups, "discardPile", p.discardPile);
        addGroup(groups, "exhaustPile", p.exhaustPile);
        o.add("groups", groups);
        LiveDeckService.describe(o);
        o.add("cardPool", RunCardPoolService.describe());
        AbstractCard hovered = p.hoveredCard;
        if (hovered != null) {
            o.add("currentCard", cardJson(hovered, "current"));
        }
        GameActionManager am = AbstractDungeon.actionManager;
        if (am != null && am.currentAction != null) {
            o.addProperty("currentAction", am.currentAction.getClass().getName());
        }
        return o;
    }

    private static void addGroup(JsonObject groups, String name, CardGroup group) {
        JsonArray arr = new JsonArray();
        if (group != null && group.group != null) {
            for (AbstractCard c : group.group) {
                arr.add(cardJson(c, name));
            }
        }
        groups.add(name, arr);
    }

    private static JsonObject cardJson(AbstractCard c, String group) {
        JsonObject o = new JsonObject();
        String uuid = c.uuid == null ? "" : c.uuid.toString();
        o.addProperty("instanceId", group + ":" + uuid);
        o.addProperty("uuid", uuid);
        o.addProperty("group", group);
        o.addProperty("cardId", c.cardID);
        o.addProperty("name", c.name);
        o.addProperty("upgraded", c.upgraded);
        o.addProperty("timesUpgraded", c.timesUpgraded);
        o.addProperty("misc", c.misc);
        o.addProperty("cost", c.cost);
        o.addProperty("costForTurn", c.costForTurn);
        o.addProperty("baseDamage", c.baseDamage);
        o.addProperty("damage", c.damage);
        o.addProperty("baseBlock", c.baseBlock);
        o.addProperty("block", c.block);
        o.addProperty("baseMagicNumber", c.baseMagicNumber);
        o.addProperty("magicNumber", c.magicNumber);
        o.addProperty("rawDescription", c.rawDescription);
        o.addProperty("isCustom", c instanceof CustomCard);
        if (c instanceof CustomCard) {
            CardSpec spec = ((CustomCard) c).getSpec();
            if (spec != null) o.add("spec", GSON.toJsonTree(spec).getAsJsonObject());
        }
        return o;
    }

    private static JsonObject validateData(JsonObject payload) {
        JsonObject o = new JsonObject();
        List<String> errors = validatePayload(payload);
        JsonArray arr = new JsonArray();
        for (String e : errors) arr.add(e);
        JsonArray scopesCheck = payload.getAsJsonArray("scopes");
        String instanceCheck = str(payload, "instanceId", "");
        if (errors.isEmpty() && AbstractDungeon.player != null
                && hasScope(scopesCheck, "INSTANCE") && instanceCheck.length() > 0
                && findByInstanceId(instanceCheck, AbstractDungeon.player) == null) {
            errors.add("instance not found or moved; refresh snapshot");
            arr.add("instance not found or moved; refresh snapshot");
        }
        o.add("errors", arr);
        o.addProperty("valid", errors.isEmpty());
        if (errors.isEmpty() && AbstractDungeon.player != null) {
            JsonArray scopes = payload.getAsJsonArray("scopes");
            String cardId = str(payload, "cardId", "");
            String instanceId = str(payload, "instanceId", "");
            Map<AbstractCard, String> targets = collectTargets(scopes, instanceId, cardId, AbstractDungeon.player);
            JsonObject counts = new JsonObject();
            int hand = 0, draw = 0, discard = 0, exhaust = 0, master = 0;
            for (String group : targets.values()) {
                if ("hand".equals(group)) hand++;
                else if ("drawPile".equals(group)) draw++;
                else if ("discardPile".equals(group)) discard++;
                else if ("exhaustPile".equals(group)) exhaust++;
                else if ("masterDeck".equals(group)) master++;
            }
            counts.addProperty("hand", hand);
            counts.addProperty("drawPile", draw);
            counts.addProperty("discardPile", discard);
            counts.addProperty("exhaustPile", exhaust);
            counts.addProperty("masterDeck", master);
            counts.addProperty("total", targets.size());
            counts.addProperty("definition", hasScope(scopes, "DEFINITION"));
            o.add("scopeCounts", counts);
        }
        return o;
    }

    private static List<String> validatePayload(JsonObject payload) {
        List<String> errors = new ArrayList<String>();
        String kind = str(payload, "kind", "custom");
        String cardId = str(payload, "cardId", null);
        if (cardId == null || cardId.length() == 0) errors.add("cardId missing");
        if ("custom".equals(kind)) {
            JsonElement specEl = payload.get("spec");
            if (specEl == null || !specEl.isJsonObject()) {
                errors.add("spec missing");
                return errors;
            }
            CardSpec spec;
            try {
                spec = CardSpec.parse(specEl.getAsJsonObject());
            } catch (Throwable t) {
                errors.add("spec parse failed: " + t.getMessage());
                return errors;
            }
            if (cardId != null && spec.id != null && !cardId.equals(spec.id)) errors.add("cardId/spec.id mismatch");
            errors.addAll(ContentRegistry.validateSpec(spec));
        } else if ("override".equals(kind)) {
            JsonElement ov = payload.get("override");
            if (ov == null || !ov.isJsonObject()) {
                errors.add("override missing");
                return errors;
            }
            errors.addAll(validateOverride(ov.getAsJsonObject()));
            if (cardId != null && com.megacrit.cardcrawl.helpers.CardLibrary.getCard(cardId) == null) {
                errors.add("target card not found: " + cardId);
            }
        } else {
            errors.add("unknown kind " + kind);
        }
        JsonElement scopes = payload.get("scopes");
        if (scopes == null || !scopes.isJsonArray() || scopes.getAsJsonArray().size() == 0) {
            errors.add("at least one scope is required");
        } else {
            for (JsonElement e : scopes.getAsJsonArray()) {
                String s = e.getAsString();
                if (!("INSTANCE".equals(s) || "COMBAT_ID".equals(s) || "MASTER_ID".equals(s) || "DEFINITION".equals(s))) {
                    errors.add("invalid scope " + s);
                }
            }
            if (hasScope(scopes.getAsJsonArray(), "INSTANCE") && str(payload, "instanceId", "").length() == 0) {
                errors.add("instanceId is required for INSTANCE scope");
            }
        }
        return errors;
    }

    private static List<String> validateOverride(JsonObject o) {
        List<String> errors = new ArrayList<String>();
        int[] cost = { -2, 99 };
        int[] stat = { 0, 999 };
        checkInt(o, "cost", cost, errors);
        checkInt(o, "upgradeCost", cost, errors);
        checkInt(o, "damage", stat, errors);
        checkInt(o, "upgradeDamage", stat, errors);
        checkInt(o, "block", stat, errors);
        checkInt(o, "upgradeBlock", stat, errors);
        checkInt(o, "magicNumber", stat, errors);
        checkInt(o, "upgradeMagicNumber", stat, errors);
        checkInt(o, "draw", stat, errors);
        checkInt(o, "discard", stat, errors);
        checkInt(o, "heal", stat, errors);
        return errors;
    }

    private static void checkInt(JsonObject o, String key, int[] range, List<String> errors) {
        JsonElement e = o.get(key);
        if (e == null || e.isJsonNull()) return;
        try {
            int v = e.getAsInt();
            if (v < range[0] || v > range[1]) errors.add(key + " out of range");
        } catch (Throwable t) {
            errors.add(key + " is not an integer");
        }
    }

    private static JsonObject addCardData(Request req) {
        JsonObject payload = req.payload;
        JsonObject result = new JsonObject();
        String cardId = str(payload, "cardId", "");
        String dest = str(payload, "destination", "HAND").toUpperCase();
        boolean upgraded = bool(payload, "upgraded", false);
        int amount = intValue(payload, "amount", 1);
        AbstractPlayer p = AbstractDungeon.player;
        if (req.revision <= currentRevision) {
            JsonObject cached = cachedResult(req.requestId);
            if (cached != null) return cached;
            result.addProperty("added", false);
            result.addProperty("error", "stale revision");
            return result;
        }
        if (p == null) {
            result.addProperty("added", false);
            result.addProperty("error", "not in a run");
            return result;
        }
        if (CardLibrary.getCard(cardId) == null) {
            result.addProperty("added", false);
            result.addProperty("error", "card not found: " + cardId);
            return result;
        }
        if (amount < 1 || amount > 10) {
            result.addProperty("added", false);
            result.addProperty("error", "amount must be 1..10");
            return result;
        }
        boolean combat = p != null && safeRoom() != null && safeRoom().phase == AbstractRoom.RoomPhase.COMBAT;
        if (("HAND".equals(dest) || "DRAW".equals(dest) || "DRAW_PILE".equals(dest)) && !combat) {
            result.addProperty("added", false);
            result.addProperty("error", "HAND/DRAW require an active combat");
            return result;
        }
        if (!("HAND".equals(dest) || "DRAW".equals(dest) || "DRAW_PILE".equals(dest) || "MASTER".equals(dest) || "MASTER_DECK".equals(dest))) {
            result.addProperty("added", false);
            result.addProperty("error", "invalid destination");
            return result;
        }
        JsonArray ids = new JsonArray();
        for (int i = 0; i < amount; i++) {
            AbstractCard copy = CardLibrary.getCopy(cardId, upgraded ? 1 : 0, 0);
            if (copy == null) {
                result.addProperty("added", false);
                result.addProperty("error", "copy failed for " + cardId);
                return result;
            }
            copy.isSeen = true;
            copy.isLocked = false;
            if ("HAND".equals(dest)) {
                p.hand.addToTop(copy);
                ids.add("hand:" + (copy.uuid == null ? copy.cardID : copy.uuid.toString()));
            } else if ("DRAW".equals(dest) || "DRAW_PILE".equals(dest)) {
                p.drawPile.addToTop(copy);
                ids.add("drawPile:" + (copy.uuid == null ? copy.cardID : copy.uuid.toString()));
            } else {
                p.masterDeck.addToTop(copy);
                ids.add("masterDeck:" + (copy.uuid == null ? copy.cardID : copy.uuid.toString()));
            }
        }
        if ("HAND".equals(dest)) {
            p.hand.refreshHandLayout();
            p.hand.applyPowers();
        }
        currentRevision = req.revision;
        lastRequestId = req.requestId;
        lastAppliedAt = System.currentTimeMillis();
        result.addProperty("added", true);
        result.addProperty("cardId", cardId);
        result.addProperty("destination", dest);
        result.addProperty("upgraded", upgraded);
        result.addProperty("amount", amount);
        result.addProperty("revision", currentRevision);
        result.add("instanceIds", ids);
        ContentRegistry.log("RuntimeBridge addCard requestId=" + req.requestId + " card=" + cardId
                + " dest=" + dest + " upgraded=" + upgraded + " amount=" + amount);
        return result;
    }

    private static JsonObject applyData(Request req) {
        JsonObject payload = req.payload;
        JsonObject result = new JsonObject();
        List<String> errors = validatePayload(payload);
        if (!errors.isEmpty()) {
            result.addProperty("applied", false);
            JsonArray arr = new JsonArray();
            for (String e : errors) arr.add(e);
            result.add("errors", arr);
            return result;
        }
        String kind = str(payload, "kind", "custom");
        String cardId = str(payload, "cardId", null);
        JsonArray scopes = payload.getAsJsonArray("scopes");
        if (req.revision <= currentRevision) {
            JsonObject cached = cachedResult(req.requestId);
            if (cached != null) return cached;
            result.addProperty("applied", false);
            result.addProperty("error", "stale revision: request=" + req.revision
                    + " current=" + currentRevision + "; query status and retry with a newer revision");
            return result;
        }
        boolean definitionUpdated = false;
        Set<String> affectedIds = new HashSet<String>();
        String instanceId = str(payload, "instanceId", "");
        AbstractPlayer p = AbstractDungeon.player;
        if (p == null) {
            result.addProperty("applied", false);
            result.addProperty("error", "not in a run");
            return result;
        }

        CardSpec spec = null;
        CardOverride override = null;
        if ("custom".equals(kind)) {
            spec = CardSpec.parse(payload.getAsJsonObject("spec"));
            if (!cardId.equals(spec.id)) throw new IllegalArgumentException("cardId/spec.id mismatch");
            if (hasScope(scopes, "DEFINITION")) {
                ContentRegistry.updateCustomDefinition(spec);
                definitionUpdated = true;
            }
            Map<AbstractCard, String> targets = collectTargets(scopes, instanceId, cardId, p);
            if (hasScope(scopes, "INSTANCE") && targets.isEmpty()) {
                errors.add("instance not found or moved; refresh snapshot");
            }
            for (Map.Entry<AbstractCard, String> entry : targets.entrySet()) {
                AbstractCard card = entry.getKey();
                if (!(card instanceof CustomCard)) {
                    errors.add("card " + card.cardID + " is not a custom card");
                    continue;
                }
                ContentRegistry.applyCustomSpecToInstance((CustomCard) card, spec);
                affectedIds.add(entry.getValue() + ":" + (card.uuid == null ? card.cardID : card.uuid.toString()));
            }
        } else {
            override = CardOverride.parse(cardId, payload.getAsJsonObject("override"));
            if (override.isEmpty()) errors.add("override is empty");
            if (hasScope(scopes, "DEFINITION")) {
                ContentRegistry.updateOverride(cardId, override);
                definitionUpdated = true;
            }
            Map<AbstractCard, String> targets = collectTargets(scopes, instanceId, cardId, p);
            if (hasScope(scopes, "INSTANCE") && targets.isEmpty()) {
                errors.add("instance not found or moved; refresh snapshot");
            }
            for (Map.Entry<AbstractCard, String> entry : targets.entrySet()) {
                AbstractCard card = entry.getKey();
                ContentRegistry.applyOverrideToInstance(card, override);
                affectedIds.add(entry.getValue() + ":" + (card.uuid == null ? card.cardID : card.uuid.toString()));
            }
        }
        if (!errors.isEmpty()) {
            result.addProperty("applied", false);
            JsonArray arr = new JsonArray();
            for (String e : errors) arr.add(e);
            result.add("errors", arr);
            return result;
        }
        if (p.hand != null) {
            try { p.hand.refreshHandLayout(); } catch (Throwable ignored) { }
        }
        currentRevision = req.revision;
        lastRequestId = req.requestId;
        lastAppliedAt = System.currentTimeMillis();
        result.addProperty("applied", true);
        result.addProperty("revision", currentRevision);
        result.addProperty("requestId", req.requestId);
        result.addProperty("definitionUpdated", definitionUpdated);
        result.addProperty("affectedInstances", affectedIds.size());
        JsonArray ids = new JsonArray();
        for (String id : affectedIds) ids.add(id);
        result.add("affectedInstanceIds", ids);
        result.addProperty("note", "已经进入结算的动作保持原效果快照；下一次使用采用新定义。");
        ContentRegistry.log("RuntimeBridge applied requestId=" + req.requestId
                + " kind=" + kind + " card=" + cardId + " revision=" + currentRevision
                + " affected=" + affectedIds.size() + " definition=" + definitionUpdated);
        return result;
    }

    private static Map<AbstractCard, String> collectTargets(JsonArray scopes, String instanceId, String cardId, AbstractPlayer p) {
        Map<AbstractCard, String> out = new LinkedHashMap<AbstractCard, String>();
        boolean combat = hasScope(scopes, "COMBAT_ID");
        boolean master = hasScope(scopes, "MASTER_ID");
        if (hasScope(scopes, "INSTANCE") && instanceId.length() > 0) {
            AbstractCard c = findByInstanceId(instanceId, p);
            if (c != null) out.put(c, groupOfInstanceId(instanceId));
        }
        if (combat) addGroupCards(out, p.hand, "hand", cardId);
        if (combat) addGroupCards(out, p.drawPile, "drawPile", cardId);
        if (combat) addGroupCards(out, p.discardPile, "discardPile", cardId);
        if (combat) addGroupCards(out, p.exhaustPile, "exhaustPile", cardId);
        if (master) addGroupCards(out, p.masterDeck, "masterDeck", cardId);
        return out;
    }

    private static void addGroupCards(Map<AbstractCard, String> out, CardGroup group, String groupName, String cardId) {
        if (group == null || group.group == null) return;
        for (AbstractCard c : group.group) {
            if (cardId.equals(c.cardID) && !out.containsKey(c)) out.put(c, groupName);
        }
    }

    private static String groupOfInstanceId(String instanceId) {
        int at = instanceId.indexOf(':');
        return at > 0 ? instanceId.substring(0, at) : "hand";
    }

    private static AbstractCard findByInstanceId(String instanceId, AbstractPlayer p) {
        int at = instanceId.indexOf(':');
        if (at <= 0) {
            // Backwards-compatible lookup by bare UUID; prefer combat groups,
            // because a combat copy can share the source UUID.
            AbstractCard c = findInGroup(p.hand, instanceId);
            if (c != null) return c;
            c = findInGroup(p.drawPile, instanceId);
            if (c != null) return c;
            c = findInGroup(p.discardPile, instanceId);
            if (c != null) return c;
            c = findInGroup(p.exhaustPile, instanceId);
            if (c != null) return c;
            return findInGroup(p.masterDeck, instanceId);
        }
        String group = instanceId.substring(0, at);
        String uuid = instanceId.substring(at + 1);
        if ("masterDeck".equals(group)) return findInGroup(p.masterDeck, uuid);
        if ("hand".equals(group)) return findInGroup(p.hand, uuid);
        if ("drawPile".equals(group)) return findInGroup(p.drawPile, uuid);
        if ("discardPile".equals(group)) return findInGroup(p.discardPile, uuid);
        if ("exhaustPile".equals(group)) return findInGroup(p.exhaustPile, uuid);
        return null;
    }

    private static AbstractCard findInGroup(CardGroup group, String id) {
        if (group == null || group.group == null) return null;
        for (AbstractCard c : group.group) {
            if (c.uuid != null && c.uuid.toString().equals(id)) return c;
        }
        return null;
    }

    private static boolean hasScope(JsonArray scopes, String scope) {
        if (scopes == null) return false;
        for (JsonElement e : scopes) {
            if (scope.equals(e.getAsString())) return true;
        }
        return false;
    }

    private static JsonObject cachedResult(String requestId) {
        synchronized (RESULT_LOCK) {
            JsonObject o = RESULTS.get(requestId);
            if (o == null) return null;
            return o.getAsJsonObject("data");
        }
    }

    private static void complete(Request req, JsonObject data) {
        JsonObject resp = baseResponse(req);
        resp.addProperty("ok", true);
        resp.addProperty("pending", false);
        resp.add("data", data);
        storeResult(req.requestId, resp);
        synchronized (req) {
            req.response = resp;
            req.done = true;
            req.notifyAll();
        }
    }

    private static void completeError(Request req, String error) {
        JsonObject resp = baseResponse(req);
        resp.addProperty("ok", false);
        resp.addProperty("pending", false);
        resp.addProperty("error", error == null ? "unknown error" : error);
        storeResult(req.requestId, resp);
        synchronized (req) {
            req.response = resp;
            req.done = true;
            req.notifyAll();
        }
        ContentRegistry.log("RuntimeBridge error requestId=" + req.requestId + " op=" + req.op + ": " + error);
    }

    private static JsonObject baseResponse(Request req) {
        JsonObject resp = new JsonObject();
        resp.addProperty("protocolVersion", PROTOCOL_VERSION);
        resp.addProperty("requestId", req.requestId);
        resp.addProperty("op", req.op);
        resp.addProperty("revision", currentRevision);
        return resp;
    }

    private static void storeResult(String requestId, JsonObject response) {
        synchronized (RESULT_LOCK) {
            IN_FLIGHT.remove(requestId);
            RESULTS.put(requestId, response);
            while (RESULTS.size() > 100) {
                String first = RESULTS.keySet().iterator().next();
                RESULTS.remove(first);
            }
        }
    }

    private static JsonObject resultData(String requestId) {
        synchronized (RESULT_LOCK) {
            JsonObject o = RESULTS.get(requestId);
            if (o == null) {
                JsonObject pending = new JsonObject();
                pending.addProperty("pending", true);
                pending.addProperty("requestId", requestId);
                return pending;
            }
            return o;
        }
    }

    private static boolean safeToApply() {
        return safeToApply(false);
    }

    private static boolean allowsMap(Request req) {
        if ("setCardPool".equals(req.op)) return true;
        if ("deckEdit".equals(req.op)) return "masterDeck".equals(str(req.payload, "group", "masterDeck"));
        if ("addCard".equals(req.op)) return str(req.payload, "destination", "HAND").startsWith("MASTER");
        if ("apply".equals(req.op)) {
            JsonArray scopes = req.payload.has("scopes") ? req.payload.getAsJsonArray("scopes") : null;
            if (scopes == null || scopes.size() == 0) return false;
            for (JsonElement scope : scopes) {
                String value = scope.getAsString();
                if ("COMBAT_ID".equals(value)) return false;
                if ("INSTANCE".equals(value) && !str(req.payload, "instanceId", "").startsWith("masterDeck:")) return false;
            }
            return true;
        }
        return false;
    }

    private static boolean safeToApply(boolean permanentEdit) {
        AbstractPlayer p = AbstractDungeon.player;
        if (p == null) return false;
        if (CardCrawlGame.mode != CardCrawlGame.GameMode.GAMEPLAY || p.isDead) return false;
        AbstractRoom room = safeRoom();
        if (!LiveEditSafety.overlayAllowed(AbstractDungeon.isScreenUp,
                AbstractDungeon.screen == null ? "NONE" : AbstractDungeon.screen.name(), AbstractDungeon.screenSwap,
                room != null && room.phase == AbstractRoom.RoomPhase.COMBAT, permanentEdit)) return false;
        if (AbstractDungeon.isFadingIn || AbstractDungeon.isFadingOut) return false;
        if (room == null || p.isDraggingCard) return false;
        // A completed fight can leave an inactive action queue behind while the route map is open.
        if (permanentEdit && room.phase != AbstractRoom.RoomPhase.COMBAT && AbstractDungeon.screen == AbstractDungeon.CurrentScreen.MAP) return true;
        GameActionManager am = AbstractDungeon.actionManager;
        if (am != null && !am.isEmpty()) return false;
        if (am != null && (am.currentAction != null || am.turnStartCurrentAction != null || am.usingCard
                || (am.cardQueue != null && !am.cardQueue.isEmpty()))) return false;
        if (p.isDraggingCard) return false;
        if (room == null) return false;
        return true;
    }

    private static AbstractRoom safeRoom() {
        try {
            return AbstractDungeon.getCurrRoom();
        } catch (Throwable t) {
            return null;
        }
    }

    private static int pendingActionCount() {
        try {
            GameActionManager am = AbstractDungeon.actionManager;
            if (am == null || am.actions == null) return 0;
            return am.actions.size();
        } catch (Throwable t) {
            return 0;
        }
    }

    private static int currentPid() {
        try {
            String n = java.lang.management.ManagementFactory.getRuntimeMXBean().getName();
            int at = n.indexOf('@');
            return at > 0 ? Integer.parseInt(n.substring(0, at)) : 0;
        } catch (Throwable t) {
            return 0;
        }
    }

    private static void acceptLoop() {
        while (running) {
            Socket socket = null;
            try {
                socket = server.accept();
                socket.setSoTimeout(60000);
                socket.setTcpNoDelay(true);
                final Socket clientSocket = socket;
                Thread t = new Thread(new Runnable() {
                    @Override
                    public void run() {
                        try {
                            handleClient(clientSocket);
                        } catch (Throwable e) {
                            ContentRegistry.log("RuntimeBridge client error: " + e);
                        } finally {
                            try { clientSocket.close(); } catch (Throwable ignored) { }
                        }
                    }
                }, "stseditor-bridge-client");
                t.setDaemon(true);
                t.start();
            } catch (Throwable t) {
                if (running) ContentRegistry.log("RuntimeBridge accept error: " + t);
                if (socket != null) {
                    try { socket.close(); } catch (Throwable ignored) { }
                }
            }
        }
    }

    private static void handleClient(Socket socket) throws IOException {
        BufferedReader reader = new BufferedReader(new InputStreamReader(socket.getInputStream(), Charset.forName("UTF-8")));
        BufferedWriter writer = new BufferedWriter(new OutputStreamWriter(socket.getOutputStream(), Charset.forName("UTF-8")));
        ContentRegistry.log("RuntimeBridge client connected");
        String line;
        while (running && (line = readLineLimited(reader, MAX_LINE)) != null) {
            JsonObject response;
            try {
                response = handleRequest(line);
            } catch (Throwable t) {
                response = new JsonObject();
                response.addProperty("ok", false);
                response.addProperty("error", t.getClass().getSimpleName() + ": " + t.getMessage());
            }
            writer.write(GSON.toJson(response));
            writer.write("\n");
            writer.flush();
        }
    }

    private static JsonObject handleRequest(String line) {
        JsonObject req;
        try {
            req = new JsonParser().parse(line).getAsJsonObject();
        } catch (Throwable t) {
            return errorResponse("bad json");
        }
        int protocol = intValue(req, "protocolVersion", 0);
        if (protocol != PROTOCOL_VERSION) {
            return errorResponse("protocol mismatch: client=" + protocol + " server=" + PROTOCOL_VERSION);
        }
        String supplied = str(req, "token", "");
        if (!constantTimeEquals(token, supplied)) {
            return errorResponse("unauthorized");
        }
        String requestId = str(req, "requestId", "");
        String op = str(req, "op", "");
        if (requestId.length() == 0 || requestId.length() > MAX_REQUEST_ID) {
            return errorResponse("invalid requestId");
        }
        if (op.length() == 0 || op.length() > 32) {
            return errorResponse("invalid op");
        }
        if ("result".equals(op)) {
            return resultData(requestId);
        }
        if ("capabilities".equals(op)) {
            JsonObject resp = new JsonObject();
            resp.addProperty("protocolVersion", PROTOCOL_VERSION);
            resp.addProperty("requestId", requestId);
            resp.addProperty("op", op);
            resp.addProperty("ok", true);
            resp.addProperty("pending", false);
            resp.addProperty("revision", currentRevision);
            resp.add("data", capabilitiesData());
            return resp;
        }
        long revision = longValue(req, "revision", 0L);
        JsonElement payloadEl = req.get("payload");
        JsonObject payload = payloadEl != null && payloadEl.isJsonObject()
                ? payloadEl.getAsJsonObject() : new JsonObject();
        Request request = new Request();
        request.requestId = requestId;
        request.op = op;
        request.revision = revision;
        request.payload = payload;
        request.deadline = System.currentTimeMillis() + (isMutation(op) ? 10000L : 5000L);
        synchronized (RESULT_LOCK) {
            if (isMutation(op) && RESULTS.containsKey(requestId)) {
                JsonObject cached = RESULTS.get(requestId);
                if (cached != null) return cached;
            }
            if (isMutation(op) && IN_FLIGHT.containsKey(requestId)) {
                JsonObject pending = baseResponse(request);
                pending.addProperty("ok", true);
                pending.addProperty("pending", true);
                return pending;
            }
            if (isMutation(op)) IN_FLIGHT.put(requestId, request);
        }
        synchronized (QUEUE_LOCK) {
            QUEUE.addLast(request);
        }
        long waitUntil = System.currentTimeMillis() + (isMutation(op) ? 1500L : 5000L);
        synchronized (request) {
            while (!request.done) {
                long left = waitUntil - System.currentTimeMillis();
                if (left <= 0) break;
                try {
                    request.wait(left);
                } catch (InterruptedException e) {
                    Thread.currentThread().interrupt();
                    break;
                }
            }
            if (request.done && request.response != null) {
                return request.response;
            }
        }
        JsonObject resp = new JsonObject();
        resp.addProperty("protocolVersion", PROTOCOL_VERSION);
        resp.addProperty("requestId", requestId);
        resp.addProperty("op", op);
        resp.addProperty("ok", true);
        resp.addProperty("pending", true);
        resp.addProperty("revision", currentRevision);
        return resp;
    }

    private static JsonObject errorResponse(String message) {
        JsonObject resp = new JsonObject();
        resp.addProperty("ok", false);
        resp.addProperty("pending", false);
        resp.addProperty("error", message);
        return resp;
    }

    private static boolean isMutation(String op) {
        return "apply".equals(op) || "addCard".equals(op) || "deckEdit".equals(op) || "setCardPool".equals(op);
    }

    private static String readLineLimited(BufferedReader reader, int max) throws IOException {
        StringBuilder sb = new StringBuilder();
        int c;
        while ((c = reader.read()) >= 0) {
            if (c == '\n') return sb.toString();
            if (c != '\r') sb.append((char) c);
            if (sb.length() > max) throw new IOException("request too large");
        }
        if (sb.length() == 0) return null;
        return sb.toString();
    }

    private static File bridgeFile() {
        return new File(new File(System.getProperty("user.dir"), "sts-card-editor"), "bridge.json");
    }

    private static void writeBridgeFile() throws IOException {
        File f = bridgeFile();
        File dir = f.getParentFile();
        if (!dir.isDirectory() && !dir.mkdirs()) throw new IOException("cannot create " + dir);
        JsonObject o = new JsonObject();
        o.addProperty("protocolVersion", PROTOCOL_VERSION);
        o.addProperty("layerVersion", LAYER_VERSION);
        o.addProperty("gameVersion", CardCrawlGame.VERSION_NUM == null ? "unknown" : CardCrawlGame.VERSION_NUM);
        o.addProperty("port", port);
        o.addProperty("token", token);
        o.addProperty("pid", currentPid());
        o.addProperty("startedAt", System.currentTimeMillis());
        o.addProperty("host", "127.0.0.1");
        FileOutputStream out = new FileOutputStream(f);
        try {
            out.write(GSON.toJson(o).getBytes(Charset.forName("UTF-8")));
        } finally {
            out.close();
        }
    }

    private static boolean constantTimeEquals(String a, String b) {
        if (a == null || b == null) return false;
        try {
            return MessageDigest.isEqual(a.getBytes(Charset.forName("UTF-8")), b.getBytes(Charset.forName("UTF-8")));
        } catch (Throwable t) {
            return false;
        }
    }

    private static String str(JsonObject o, String key, String def) {
        JsonElement e = o == null ? null : o.get(key);
        return e == null || e.isJsonNull() ? def : e.getAsString();
    }

    private static boolean bool(JsonObject o, String key, boolean def) {
        JsonElement e = o == null ? null : o.get(key);
        return e == null || e.isJsonNull() ? def : e.getAsBoolean();
    }

    private static int intValue(JsonObject o, String key, int def) {
        JsonElement e = o == null ? null : o.get(key);
        return e == null || e.isJsonNull() ? def : e.getAsInt();
    }

    private static long longValue(JsonObject o, String key, long def) {
        JsonElement e = o == null ? null : o.get(key);
        return e == null || e.isJsonNull() ? def : e.getAsLong();
    }

    private static final class Request {
        String requestId;
        String op;
        long revision;
        JsonObject payload;
        JsonObject response;
        boolean done;
        long deadline;
    }
}

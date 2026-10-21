import com.google.gson.*;
import com.megacrit.cardcrawl.cards.*;
import com.megacrit.cardcrawl.characters.*;
import com.megacrit.cardcrawl.core.*;
import com.megacrit.cardcrawl.dungeons.*;
import com.megacrit.cardcrawl.helpers.*;
import com.stseditor.runtime.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;

/** Behavioral tests with isolated engine fixtures; not a claim of in-game verification. */
public class LiveDeckTest {
    private static int passed;
    private static AbstractPlayer p;
    private static JsonObject payload(String action, String group) {
        JsonObject snapshot = new JsonObject(); LiveDeckService.describe(snapshot);
        JsonObject payload = new JsonObject();
        payload.addProperty("action", action); payload.addProperty("group", group);
        payload.add("runKey", snapshot.get("runKey"));
        payload.add("expectedVersion", snapshot.getAsJsonObject("groupVersions").get(group));
        return payload;
    }
    private static void reset() {
        p = new AbstractPlayer(); AbstractDungeon.player = p; CardCrawlGame.mode = CardCrawlGame.GameMode.GAMEPLAY;
        CardLibrary.cards.clear(); CardLibrary.cards.put("Strike_R", new AbstractCard("Strike_R"));
        CardLibrary.cards.put("Defend_R", new AbstractCard("Defend_R"));
        p.masterDeck.group.add(new AbstractCard("Strike_R"));
        p.masterDeck.group.add(new AbstractCard("Strike_R"));
        p.hand.group.add(p.masterDeck.group.get(0).makeSameInstanceOf());
        p.drawPile.group.add(p.masterDeck.group.get(1).makeSameInstanceOf());
    }
    private static JsonArray entries(String id, int count, int upgrades) {
        JsonArray entries = new JsonArray(); JsonObject entry = new JsonObject();
        entry.addProperty("cardId", id); entry.addProperty("count", count); entry.addProperty("upgrades", upgrades); entries.add(entry); return entries;
    }
    private static void select(JsonObject payload, AbstractCard... cards) {
        JsonArray ids = new JsonArray(); String group = payload.get("group").getAsString();
        for (AbstractCard card : cards) ids.add(group + ":" + card.uuid); payload.add("instanceIds", ids);
    }
    private static void check(boolean condition, String name) {
        if (!condition) throw new AssertionError(name); passed++; System.out.println("PASS " + name);
    }
    private static void rejects(JsonObject payload, String name) throws Exception {
        String before = state(); boolean rejected = false;
        try { LiveDeckService.edit(payload, 2, 1); } catch (IllegalArgumentException e) { rejected = true; }
        check(rejected && before.equals(state()), name);
    }
    private static String state() {
        JsonObject d = new JsonObject(); LiveDeckService.describe(d); return d.toString();
    }
    public static void main(String[] args) throws Exception {
        reset(); JsonObject add = payload("ADD", "masterDeck"); add.add("entries", entries("Defend_R", 2, 1)); add.addProperty("syncCombat", true);
        LiveDeckService.edit(add, 2, 1);
        check(p.masterDeck.size() == 4 && p.drawPile.size() == 3, "add full preset to master and current draw pile");
        check(p.masterDeck.group.get(2).uuid.equals(p.drawPile.group.get(1).uuid) && p.masterDeck.group.get(2).timesUpgraded == 1, "added combat copies retain matching identity and upgrade");

        reset(); JsonObject invalid = payload("ADD", "masterDeck"); JsonArray batch = entries("Defend_R", 2, 0); batch.add(entries("Missing", 1, 0).get(0)); invalid.add("entries", batch);
        rejects(invalid, "invalid card in batch leaves entire deck unchanged");
        reset(); JsonObject stale = payload("ADD", "masterDeck"); stale.add("entries", entries("Strike_R", 1, 0)); p.masterDeck.group.add(new AbstractCard("Defend_R"));
        rejects(stale, "outdated snapshot cannot change another deck state");
        reset(); JsonObject wrongRun = payload("ADD", "masterDeck"); wrongRun.add("entries", entries("Strike_R", 1, 0)); AbstractDungeon.player = new AbstractPlayer(); AbstractDungeon.player.masterDeck.group.add(new AbstractCard("Strike_R"));
        rejects(wrongRun, "old run token cannot edit a new run");

        reset(); JsonObject remove = payload("REMOVE", "masterDeck"); select(remove, p.masterDeck.group.get(0)); remove.addProperty("syncCombat", true);
        LiveDeckService.edit(remove, 2, 1);
        check(p.masterDeck.size() == 1 && p.hand.size() == 0 && p.drawPile.size() == 1, "remove only selected same-name instance and corresponding battle copy");
        reset(); JsonObject masterOnly = payload("REMOVE", "masterDeck"); select(masterOnly, p.masterDeck.group.get(0)); LiveDeckService.edit(masterOnly, 2, 1);
        check(p.masterDeck.size() == 1 && p.hand.size() == 1, "master-only removal preserves this combat copy");
        reset(); JsonObject handOnly = payload("REMOVE", "hand"); select(handOnly, p.hand.group.get(0)); LiveDeckService.edit(handOnly, 2, 1);
        check(p.masterDeck.size() == 2 && p.hand.size() == 0, "combat removal preserves master deck");

        reset(); JsonObject upgrade = payload("UPGRADE", "masterDeck"); select(upgrade, p.masterDeck.group.get(0)); upgrade.addProperty("syncCombat", true); LiveDeckService.edit(upgrade, 2, 1);
        check(p.masterDeck.group.get(0).timesUpgraded == 1 && p.hand.group.get(0).timesUpgraded == 1 && p.masterDeck.group.get(1).timesUpgraded == 0, "upgrade selected instance with precise battle sync");
        reset(); p.hand.group.get(0).retain = true; p.hand.group.get(0).exhaustOnUseOnce = true;
        JsonObject flags = payload("UPGRADE", "hand"); select(flags, p.hand.group.get(0)); LiveDeckService.edit(flags, 2, 1);
        check(p.hand.group.get(0).retain && p.hand.group.get(0).exhaustOnUseOnce, "combat upgrade preserves retain and one-use exhaust flags");
        reset(); upgrade = payload("UPGRADE", "masterDeck"); select(upgrade, p.masterDeck.group.get(0)); LiveDeckService.edit(upgrade, 2, 1);
        JsonObject repeat = payload("UPGRADE", "masterDeck"); select(repeat, p.masterDeck.group.get(0)); rejects(repeat, "non-upgradable selection is reported without changes");
        reset(); JsonObject empty = payload("REMOVE", "masterDeck"); select(empty, p.masterDeck.group.get(0), p.masterDeck.group.get(1)); rejects(empty, "last master card cannot be removed");
        reset(); p.masterDeck.group.get(0).inBottleFlame = true; JsonObject bottle = payload("REMOVE", "masterDeck"); select(bottle, p.masterDeck.group.get(0)); rejects(bottle, "bottled card cannot leave a broken relic reference");
        reset(); JsonObject overfull = payload("ADD", "hand"); overfull.add("entries", entries("Defend_R", 10, 0)); rejects(overfull, "hand limit rejects whole batch");
        reset(); JsonObject unsupportedUpgrades = payload("ADD", "masterDeck"); unsupportedUpgrades.add("entries", entries("Defend_R", 1, 2)); rejects(unsupportedUpgrades, "unsupported upgrade count is not silently reduced");
        reset(); JsonObject duplicate = payload("REMOVE", "masterDeck"); select(duplicate, p.masterDeck.group.get(0), p.masterDeck.group.get(0)); rejects(duplicate, "duplicate selected instance cannot remove twice");
        reset(); JsonObject oldRevision = payload("REMOVE", "hand"); select(oldRevision, p.hand.group.get(0)); boolean oldRejected = false;
        try { LiveDeckService.edit(oldRevision, 1, 1); } catch (IllegalArgumentException expected) { oldRejected = true; }
        check(oldRejected && p.hand.size() == 1, "stale revision rejected");
        reset(); JsonObject rollback = payload("REMOVE", "masterDeck"); select(rollback, p.masterDeck.group.get(0)); p.hand.failRefresh = true; String before = state();
        boolean failed = false; try { LiveDeckService.edit(rollback, 2, 1); } catch (IllegalStateException e) { failed = true; }
        check(failed && before.equals(state()), "layout failure restores all original card groups");

        reset(); JsonObject custom = payload("ADD", "masterDeck"); custom.add("entries", entries("stseditor_test", 2, 0));
        JsonArray specs = new JsonArray(); JsonObject spec = new JsonObject(); spec.addProperty("id", "stseditor_test"); spec.addProperty("name", "Test"); specs.add(spec); custom.add("customCards", specs);
        Path content = Paths.get("sts-card-editor", "content.json"); Files.createDirectories(content.getParent()); Files.write(content, "{\"schemaVersion\":2,\"customCards\":[],\"overrides\":{\"old\":{\"damage\":17}}}".getBytes(StandardCharsets.UTF_8));
        LiveDeckService.edit(custom, 2, 1);
        JsonObject persisted = new JsonParser().parse(new String(Files.readAllBytes(content), StandardCharsets.UTF_8)).getAsJsonObject();
        check(p.masterDeck.size() == 4 && CardLibrary.cards.containsKey("stseditor_test"), "missing custom definition is registered with full preset");
        check(persisted.getAsJsonArray("customCards").size() == 1 && persisted.getAsJsonObject("overrides").has("old"), "custom definition persisted without dropping existing overrides");
        check(Files.newDirectoryStream(content.getParent(), "content-before-deck-*.json").iterator().hasNext(), "definition persistence creates recovery backup");
        reset(); com.megacrit.cardcrawl.relics.AbstractRelic relic = new com.megacrit.cardcrawl.relics.AbstractRelic(); p.relics.add(relic);
        JsonObject notify = payload("ADD", "masterDeck"); notify.add("entries", entries("Defend_R", 2, 0)); LiveDeckService.edit(notify, 2, 1);
        check(relic.notifications == 1, "batch refreshes deck-dependent relic once");
        reset(); AbstractDungeon.room.phase = com.megacrit.cardcrawl.rooms.AbstractRoom.RoomPhase.COMPLETE;
        JsonObject mapAdd=payload("ADD","masterDeck"); mapAdd.add("entries",entries("Defend_R",2,0)); LiveDeckService.edit(mapAdd,2,1);
        check(p.masterDeck.size()==4 && p.hand.size()==1 && p.drawPile.size()==1,"outside combat adding a deck changes the permanent run deck only");
        JsonObject mapUpgrade=payload("UPGRADE","masterDeck"); select(mapUpgrade,p.masterDeck.group.get(2)); LiveDeckService.edit(mapUpgrade,3,2);
        check(p.masterDeck.group.get(2).timesUpgraded==1,"outside combat upgrade edits the selected owned card");
        JsonObject mapRemove=payload("REMOVE","masterDeck"); select(mapRemove,p.masterDeck.group.get(2),p.masterDeck.group.get(3)); LiveDeckService.edit(mapRemove,4,3);
        check(p.masterDeck.size()==2 && p.hand.size()==1,"outside combat removal restores the original run deck without touching temporary zones");
        System.out.println("TOTAL " + passed + " behavioral checks passed (isolated engine fixtures)");
    }
}

import com.megacrit.cardcrawl.cards.*;
import com.megacrit.cardcrawl.characters.*;
import com.megacrit.cardcrawl.dungeons.*;
import com.stseditor.runtime.*;
public class ZeroCostTest {
    private static int passed;
    private static void check(boolean condition, String name) { if (!condition) throw new AssertionError(name); passed++; System.out.println("PASS " + name); }
    public static void main(String[] args) {
        AbstractPlayer player = new AbstractPlayer(); AbstractDungeon.player = player;
        AbstractCard master = new AbstractCard("master"); player.masterDeck.group.add(master);
        AbstractCard hand = new AbstractCard("hand"); player.hand.group.add(hand);
        AbstractCard draw = new AbstractCard("draw"); draw.cost = draw.costForTurn = 3; player.drawPile.group.add(draw);
        AbstractCard discard = new AbstractCard("discard"); player.discardPile.group.add(discard);
        AbstractCard exhaust = new AbstractCard("exhaust"); player.exhaustPile.group.add(exhaust);
        AbstractCard x = new AbstractCard("x"); x.cost = x.costForTurn = -1; player.hand.group.add(x);
        AbstractCard unplayable = new AbstractCard("curse"); unplayable.cost = unplayable.costForTurn = -2; player.hand.group.add(unplayable);
        ZeroCostPower power = new ZeroCostPower(player); player.powers.add(power); power.onInitialApplication();
        check(hand.costForTurn == 0 && draw.costForTurn == 0 && discard.costForTurn == 0 && exhaust.costForTurn == 0, "zero cost covers all current combat zones");
        check(master.costForTurn == 1 && master.cost == 1 && draw.cost == 3, "master deck and permanent base costs stay unchanged");
        check(x.costForTurn == -1 && unplayable.costForTurn == -2, "X-cost and unplayable cards retain their defined rules");
        AbstractCard generated = new AbstractCard("generated"); generated.cost = generated.costForTurn = 2; player.hand.group.add(generated); ZeroCostPower.refreshPlayer();
        check(generated.costForTurn == 0, "newly generated cards become free on next game tick");
        AbstractCard newlyDrawn = new AbstractCard("newly-drawn"); power.onCardDraw(newlyDrawn);
        check(newlyDrawn.costForTurn == 0, "draw callback applies zero cost before use");
        draw.costForTurn = 3; power.atStartOfTurnPostDraw();
        check(draw.costForTurn == 0, "turn reset cannot remove combat-long discount");
        hand.costForTurn = 2; hand.isCostModifiedForTurn = true; ZeroCostPower.refreshPlayer(); power.onRemove();
        check(hand.costForTurn == 2 && hand.isCostModifiedForTurn && draw.costForTurn == 3 && generated.costForTurn == 2, "removal restores the most recent nonzero cost states");
        player.powers.clear(); ZeroCostPower.refreshPlayer(); check(hand.costForTurn == 2, "no discount remains after power removal");
        ZeroCostPower second = new ZeroCostPower(player); player.powers.add(second); second.onInitialApplication(); second.onVictory(); ZeroCostPower.refreshPlayer();
        check(hand.costForTurn == 2 && draw.costForTurn == 3, "victory restoration cannot be re-applied during transition");
        player.powers.clear(); ZeroCostPower third = new ZeroCostPower(player); player.powers.add(third); third.onInitialApplication();
        generated.cost = 0; ZeroCostPower.refreshPlayer(); third.onRemove();
        check(generated.costForTurn == 0 && generated.cost == 0, "permanent cost changes made by another effect survive discount removal");
        System.out.println("TOTAL " + passed + " zero-cost checks passed (isolated engine fixtures)");
    }
}

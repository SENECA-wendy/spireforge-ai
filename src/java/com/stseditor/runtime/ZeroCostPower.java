package com.stseditor.runtime;

import com.megacrit.cardcrawl.cards.AbstractCard;
import com.megacrit.cardcrawl.cards.CardGroup;
import com.megacrit.cardcrawl.characters.AbstractPlayer;
import com.megacrit.cardcrawl.dungeons.AbstractDungeon;
import com.megacrit.cardcrawl.core.AbstractCreature;
import com.megacrit.cardcrawl.powers.AbstractPower;
import com.megacrit.cardcrawl.rooms.AbstractRoom;
import java.util.IdentityHashMap;
import java.util.Map;

/** Combat-local cost override. Never changes cost or the master deck. */
public final class ZeroCostPower extends AbstractPower {
    public static final String POWER_ID = "STSEDITOR_ALL_CARDS_ZERO_COST";
    private final Map<AbstractCard, CostState> originals = new IdentityHashMap<AbstractCard, CostState>();
    private boolean finished;

    public ZeroCostPower(AbstractPlayer player) {
        ID = POWER_ID;
        name = "空城之策";
        owner = player;
        amount = -1;
        type = PowerType.BUFF;
        isTurnBased = false;
        description = "本场战斗中，所有固定费用卡牌的能量消耗为0，包括之后抽到和生成的牌。X费与不可打出的牌除外。";
        loadRegion("corruption");
    }

    @Override public void onInitialApplication() { refresh(); }
    @Override public void atStartOfTurnPostDraw() { refresh(); }
    @Override public void onCardDraw(AbstractCard card) { apply(card); }
    @Override public void onAfterCardPlayed(AbstractCard card) { refresh(); }
    @Override public void stackPower(int ignored) { refresh(); }
    @Override public void onRemove() { finished = true; restore(); }
    @Override public void onVictory() { finished = true; restore(); }

    public static void refreshPlayer() {
        AbstractPlayer player = AbstractDungeon.player;
        if (player == null) return;
        AbstractPower power = player.getPower(POWER_ID);
        if (power instanceof ZeroCostPower) ((ZeroCostPower)power).refresh();
    }

    private void refresh() {
        if (finished) return;
        if (!(owner instanceof AbstractPlayer)) return;
        AbstractRoom room;
        try { room = AbstractDungeon.getCurrRoom(); } catch (Throwable ignored) { return; }
        if (room == null || room.phase != AbstractRoom.RoomPhase.COMBAT) return;
        AbstractPlayer player = (AbstractPlayer)owner;
        apply(player.hand); apply(player.drawPile); apply(player.discardPile); apply(player.exhaustPile);
    }

    private void apply(CardGroup group) {
        if (group == null) return;
        for (AbstractCard card : group.group) apply(card);
    }

    private void apply(AbstractCard card) {
        if (finished || card == null || card.cost < 0) return;
        CostState state = originals.get(card);
        if (state == null) {
            state = new CostState(); originals.put(card, state);
            state.costForTurn = card.costForTurn; state.modified = card.isCostModifiedForTurn;
            state.baseCost = card.cost;
        } else if (card.costForTurn != 0) {
            // Preserve a later turn reset or another cost modifier for removal.
            state.costForTurn = card.costForTurn; state.modified = card.isCostModifiedForTurn;
            state.baseCost = card.cost;
        } else if (card.cost != state.baseCost) {
            state.costForTurn = Math.max(0, state.costForTurn + card.cost - state.baseCost);
            state.baseCost = card.cost;
        }
        card.costForTurn = 0;
        card.isCostModifiedForTurn = card.cost != 0;
    }

    private void restore() {
        for (Map.Entry<AbstractCard, CostState> entry : originals.entrySet()) {
            AbstractCard card = entry.getKey();
            if (card.costForTurn == 0) {
                card.costForTurn = entry.getValue().costForTurn;
                card.isCostModifiedForTurn = entry.getValue().modified;
            }
        }
        originals.clear();
    }

    private static final class CostState { int costForTurn; int baseCost; boolean modified; }
}

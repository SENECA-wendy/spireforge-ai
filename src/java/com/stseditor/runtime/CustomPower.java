package com.stseditor.runtime;

import com.megacrit.cardcrawl.actions.common.DrawCardAction;
import com.megacrit.cardcrawl.actions.common.GainBlockAction;
import com.megacrit.cardcrawl.cards.AbstractCard;
import com.megacrit.cardcrawl.characters.AbstractPlayer;
import com.megacrit.cardcrawl.core.AbstractCreature;
import com.megacrit.cardcrawl.powers.AbstractPower;

/** A small non-turn-based buff used by generated cards. */
public final class CustomPower extends AbstractPower {
    private final String baseDescription;
    private final boolean blockAtStartOfTurn;
    private final boolean drawOnCardPlay;

    public CustomPower(AbstractCreature owner, int amount, String id, String name,
                       String description, boolean blockAtStartOfTurn, boolean drawOnCardPlay) {
        this.name = name;
        this.ID = id;
        this.owner = owner;
        this.amount = amount;
        this.baseDescription = description;
        this.blockAtStartOfTurn = blockAtStartOfTurn;
        this.drawOnCardPlay = drawOnCardPlay;
        this.type = AbstractPower.PowerType.BUFF;
        this.isTurnBased = false;
        this.priority = 50;
        this.updateDescription();
        this.loadRegion("armor");
    }

    @Override
    public void updateDescription() {
        this.description = this.baseDescription.replace("!M!", Integer.toString(this.amount));
    }

    @Override
    public void atStartOfTurn() {
        if (this.blockAtStartOfTurn && this.owner != null && !this.owner.isDying) {
            this.flash();
            this.addToBot(new GainBlockAction(this.owner, this.owner, this.amount));
        }
    }

    @Override
    public void onAfterCardPlayed(AbstractCard card) {
        if (this.drawOnCardPlay && this.owner instanceof AbstractPlayer && !this.owner.isDying) {
            int draw = this.amount < 1 ? 1 : this.amount;
            this.flash();
            this.addToBot(new DrawCardAction((AbstractPlayer) this.owner, draw));
        }
    }

    @Override
    public void stackPower(int stackAmount) {
        this.amount += stackAmount;
        this.updateDescription();
    }
}

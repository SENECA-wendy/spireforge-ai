package com.megacrit.cardcrawl.powers;
import com.megacrit.cardcrawl.core.AbstractCreature;
import com.megacrit.cardcrawl.cards.AbstractCard;
public class AbstractPower {
    public enum PowerType { BUFF, DEBUFF }
    public String ID, name, description;
    public AbstractCreature owner;
    public int amount;
    public PowerType type;
    public boolean isTurnBased;
    public void loadRegion(String region) { }
    public void onInitialApplication() { }
    public void atStartOfTurnPostDraw() { }
    public void onCardDraw(AbstractCard card) { }
    public void onAfterCardPlayed(AbstractCard card) { }
    public void stackPower(int amount) { }
    public void onRemove() { }
    public void onVictory() { }
}

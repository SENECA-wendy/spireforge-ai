package com.megacrit.cardcrawl.characters;
import com.megacrit.cardcrawl.cards.*;
public class AbstractPlayer extends com.megacrit.cardcrawl.core.AbstractCreature {
    public enum PlayerClass { IRONCLAD, THE_SILENT, DEFECT, WATCHER }
    public PlayerClass chosenClass = PlayerClass.IRONCLAD;
    public AbstractCard.CardColor getCardColor(){return chosenClass==PlayerClass.THE_SILENT?AbstractCard.CardColor.GREEN:chosenClass==PlayerClass.DEFECT?AbstractCard.CardColor.BLUE:chosenClass==PlayerClass.WATCHER?AbstractCard.CardColor.PURPLE:AbstractCard.CardColor.RED;}
    public CardGroup masterDeck = new CardGroup(), hand = new CardGroup(), drawPile = new CardGroup(), discardPile = new CardGroup(), exhaustPile = new CardGroup();
    public AbstractCard hoveredCard;
    public boolean isDead;
    public java.util.ArrayList<com.megacrit.cardcrawl.powers.AbstractPower> powers = new java.util.ArrayList<com.megacrit.cardcrawl.powers.AbstractPower>();
    public com.megacrit.cardcrawl.powers.AbstractPower getPower(String id) { for (com.megacrit.cardcrawl.powers.AbstractPower power : powers) if (id.equals(power.ID)) return power; return null; }
    public java.util.ArrayList<com.megacrit.cardcrawl.relics.AbstractRelic> relics = new java.util.ArrayList<com.megacrit.cardcrawl.relics.AbstractRelic>();
}

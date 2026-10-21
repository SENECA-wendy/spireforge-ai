package com.megacrit.cardcrawl.characters;
import com.megacrit.cardcrawl.cards.CardGroup;
public class AbstractPlayer extends com.megacrit.cardcrawl.core.AbstractCreature {
 public CardGroup hand=new CardGroup(),drawPile=new CardGroup(),discardPile=new CardGroup(),exhaustPile=new CardGroup(),limbo=new CardGroup(),masterDeck=new CardGroup();
 public com.megacrit.cardcrawl.core.EnergyManager energy=new com.megacrit.cardcrawl.core.EnergyManager();
 public void gainEnergy(int n){energy.energy+=n;}public void loseEnergy(int n){energy.energy-=n;}
}

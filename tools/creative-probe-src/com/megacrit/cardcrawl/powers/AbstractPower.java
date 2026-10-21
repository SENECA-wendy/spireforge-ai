package com.megacrit.cardcrawl.powers;
public class AbstractPower {
 public enum PowerType {BUFF,DEBUFF}
 public String ID,name,description;public com.megacrit.cardcrawl.core.AbstractCreature owner;public int amount;public boolean isTurnBased;public PowerType type;
 public void loadRegion(String s){}public void updateDescription(){}public void stackPower(int n){amount+=n;}
 public void atStartOfTurn(){}public void atEndOfTurn(boolean p){}public void onCardDraw(com.megacrit.cardcrawl.cards.AbstractCard c){}public void onAfterCardPlayed(com.megacrit.cardcrawl.cards.AbstractCard c){}
}

package com.megacrit.cardcrawl.actions.common;
public class DrawCardAction extends com.megacrit.cardcrawl.actions.AbstractGameAction {
 public DrawCardAction(int n){amount=n;}public DrawCardAction(int n,boolean b){this(n);}public DrawCardAction(com.megacrit.cardcrawl.core.AbstractCreature p,int n){this(n);}public DrawCardAction(com.megacrit.cardcrawl.core.AbstractCreature p,int n,boolean b){this(n);}
 public void update(){com.megacrit.cardcrawl.characters.AbstractPlayer p=com.megacrit.cardcrawl.dungeons.AbstractDungeon.player;for(int i=0;i<amount&&p.hand.size()<10&&!p.drawPile.isEmpty();i++){p.hand.group.add(p.drawPile.group.remove(p.drawPile.size()-1));probe.Observations.add("cardsDrawn",1);}isDone=true;}
}

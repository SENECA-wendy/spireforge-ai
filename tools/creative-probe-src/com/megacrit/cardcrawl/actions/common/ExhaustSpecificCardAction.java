package com.megacrit.cardcrawl.actions.common;
public class ExhaustSpecificCardAction extends com.megacrit.cardcrawl.actions.AbstractGameAction {
 private com.megacrit.cardcrawl.cards.AbstractCard card;private com.megacrit.cardcrawl.cards.CardGroup group;
 public ExhaustSpecificCardAction(com.megacrit.cardcrawl.cards.AbstractCard c,com.megacrit.cardcrawl.cards.CardGroup g){card=c;group=g;}
 public void update(){if(group.group.remove(card))com.megacrit.cardcrawl.dungeons.AbstractDungeon.player.exhaustPile.group.add(card);isDone=true;}
}

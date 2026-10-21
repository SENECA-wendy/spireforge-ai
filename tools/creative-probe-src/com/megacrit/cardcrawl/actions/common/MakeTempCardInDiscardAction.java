package com.megacrit.cardcrawl.actions.common;
import com.megacrit.cardcrawl.cards.AbstractCard;
public class MakeTempCardInDiscardAction extends com.megacrit.cardcrawl.actions.AbstractGameAction {
 private AbstractCard card;private int count;
 public MakeTempCardInDiscardAction(AbstractCard c,int n){card=c;count=n;}
 public void update(){if(count<0||count>1000)throw new IllegalStateException("invalid generated count");for(int i=0;i<count;i++){AbstractCard c=card.makeStatEquivalentCopy();com.megacrit.cardcrawl.dungeons.AbstractDungeon.player.discardPile.group.add(c);probe.Observations.card("cardsToDiscard",c);}isDone=true;}
}

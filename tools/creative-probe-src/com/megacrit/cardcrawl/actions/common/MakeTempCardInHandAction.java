package com.megacrit.cardcrawl.actions.common;
import com.megacrit.cardcrawl.cards.AbstractCard;
public class MakeTempCardInHandAction extends com.megacrit.cardcrawl.actions.AbstractGameAction {
 private AbstractCard card;private int count;
 public MakeTempCardInHandAction(AbstractCard c){this(c,1);}public MakeTempCardInHandAction(AbstractCard c,int n){card=c;count=n;}
 public MakeTempCardInHandAction(AbstractCard c,int n,boolean b){this(c,n);}public MakeTempCardInHandAction(AbstractCard c,int n,boolean b,boolean d){this(c,n);}
 public void update(){if(count<0||count>1000)throw new IllegalStateException("invalid generated count");for(int i=0;i<count;i++){AbstractCard c=card.makeStatEquivalentCopy();if(com.megacrit.cardcrawl.dungeons.AbstractDungeon.player.hand.size()<10){com.megacrit.cardcrawl.dungeons.AbstractDungeon.player.hand.group.add(c);probe.Observations.card("cardsToHand",c);}else com.megacrit.cardcrawl.dungeons.AbstractDungeon.player.discardPile.group.add(c);}isDone=true;}
}

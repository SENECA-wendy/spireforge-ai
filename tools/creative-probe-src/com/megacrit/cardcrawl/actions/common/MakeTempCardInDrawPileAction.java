package com.megacrit.cardcrawl.actions.common;
import com.megacrit.cardcrawl.cards.AbstractCard;
public class MakeTempCardInDrawPileAction extends com.megacrit.cardcrawl.actions.AbstractGameAction {
 private AbstractCard card;private int count;
 public MakeTempCardInDrawPileAction(AbstractCard c,int n,boolean a,boolean b){card=c;count=n;}public MakeTempCardInDrawPileAction(AbstractCard c,int n,boolean a,boolean b,boolean d){this(c,n,a,b);}
 public MakeTempCardInDrawPileAction(AbstractCard c,int n,boolean a,boolean b,boolean d,float x,float y){this(c,n,a,b);}
 public void update(){if(count<0||count>1000)throw new IllegalStateException("invalid generated count");for(int i=0;i<count;i++){AbstractCard c=card.makeStatEquivalentCopy();com.megacrit.cardcrawl.dungeons.AbstractDungeon.player.drawPile.group.add(c);probe.Observations.card("cardsToDrawPile",c);}isDone=true;}
}

package com.megacrit.cardcrawl.cards;
public class CardGroup {
 public java.util.ArrayList<AbstractCard> group=new java.util.ArrayList<AbstractCard>();
 public int size(){return group.size();}public boolean isEmpty(){return group.isEmpty();}
 public void clear(){group.clear();}public void addToTop(AbstractCard c){group.add(c);}public void addToBottom(AbstractCard c){group.add(0,c);}
 public void removeCard(AbstractCard c){group.remove(c);}public AbstractCard getTopCard(){return group.get(group.size()-1);}
}

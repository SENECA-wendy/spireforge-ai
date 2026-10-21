package com.megacrit.cardcrawl.actions;
public class GameActionManager {
 public java.util.ArrayDeque<AbstractGameAction> actions=new java.util.ArrayDeque<AbstractGameAction>();
 public void addToBottom(AbstractGameAction a){actions.addLast(a);}public void addToTop(AbstractGameAction a){actions.addFirst(a);}
 public void drain(){int steps=0;while(!actions.isEmpty()){if(++steps>2000)throw new IllegalStateException("action limit exceeded");AbstractGameAction a=actions.removeFirst();a.update();if(!a.isDone)throw new UnsupportedOperationException("multi-frame action needs real-game validation: "+a.getClass().getName());}}
}

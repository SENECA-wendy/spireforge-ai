package com.megacrit.cardcrawl.actions.common;
public class GainBlockAction extends com.megacrit.cardcrawl.actions.AbstractGameAction {
 private com.megacrit.cardcrawl.core.AbstractCreature target;
 public GainBlockAction(com.megacrit.cardcrawl.core.AbstractCreature t,int n){target=t;amount=n;}public GainBlockAction(com.megacrit.cardcrawl.core.AbstractCreature t,com.megacrit.cardcrawl.core.AbstractCreature s,int n){this(t,n);}
 public GainBlockAction(com.megacrit.cardcrawl.core.AbstractCreature t,int n,boolean b){this(t,n);}public GainBlockAction(com.megacrit.cardcrawl.core.AbstractCreature t,com.megacrit.cardcrawl.core.AbstractCreature s,int n,boolean b){this(t,n);}
 public void update(){target.currentBlock+=amount;probe.Observations.add("blockGained",amount);isDone=true;}
}

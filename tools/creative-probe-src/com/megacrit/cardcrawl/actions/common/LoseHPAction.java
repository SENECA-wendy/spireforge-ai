package com.megacrit.cardcrawl.actions.common;
public class LoseHPAction extends com.megacrit.cardcrawl.actions.AbstractGameAction {
 private com.megacrit.cardcrawl.core.AbstractCreature target;
 public LoseHPAction(com.megacrit.cardcrawl.core.AbstractCreature t,com.megacrit.cardcrawl.core.AbstractCreature s,int n){target=t;amount=n;}
 public void update(){target.currentHealth=Math.max(0,target.currentHealth-amount);probe.Observations.add("hpLost",amount);isDone=true;}
}

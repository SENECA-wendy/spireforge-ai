package com.megacrit.cardcrawl.actions.common;
public class ApplyPowerAction extends com.megacrit.cardcrawl.actions.AbstractGameAction {
 private com.megacrit.cardcrawl.core.AbstractCreature target;private com.megacrit.cardcrawl.powers.AbstractPower power;
 public ApplyPowerAction(com.megacrit.cardcrawl.core.AbstractCreature t,com.megacrit.cardcrawl.core.AbstractCreature s,com.megacrit.cardcrawl.powers.AbstractPower p,int n){target=t;power=p;amount=n;}
 public void update(){target.powers.add(power);probe.Observations.add("powerApplications",1);isDone=true;}
}

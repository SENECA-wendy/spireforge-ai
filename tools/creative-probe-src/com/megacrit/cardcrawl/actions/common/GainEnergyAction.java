package com.megacrit.cardcrawl.actions.common;
public class GainEnergyAction extends com.megacrit.cardcrawl.actions.AbstractGameAction {
 public GainEnergyAction(int n){amount=n;}public void update(){com.megacrit.cardcrawl.dungeons.AbstractDungeon.player.gainEnergy(amount);probe.Observations.add("energyGained",amount);isDone=true;}
}

package com.megacrit.cardcrawl.core;
public class AbstractCreature {
 public int currentHealth=50,maxHealth=50,currentBlock;public boolean isDead,isDying;
 public java.util.ArrayList<com.megacrit.cardcrawl.powers.AbstractPower> powers=new java.util.ArrayList<com.megacrit.cardcrawl.powers.AbstractPower>();
 public com.megacrit.cardcrawl.powers.AbstractPower getPower(String id){for(com.megacrit.cardcrawl.powers.AbstractPower p:powers)if(id.equals(p.ID))return p;return null;}
 public boolean hasPower(String id){return getPower(id)!=null;}
}

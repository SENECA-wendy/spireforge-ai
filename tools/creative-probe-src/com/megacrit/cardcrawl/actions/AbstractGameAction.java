package com.megacrit.cardcrawl.actions;
public abstract class AbstractGameAction {
 public enum AttackEffect {NONE,BLUNT_LIGHT,BLUNT_HEAVY,SLASH_DIAGONAL,SLASH_HORIZONTAL,SLASH_VERTICAL,FIRE,POISON}
 public enum ActionType {DAMAGE,BLOCK,POWER,CARD_MANIPULATION,SPECIAL,DRAW,ENERGY,EXHAUST}
 public boolean isDone;public int amount;public float duration;public ActionType actionType;
 public abstract void update();protected void tickDuration(){isDone=true;}
}

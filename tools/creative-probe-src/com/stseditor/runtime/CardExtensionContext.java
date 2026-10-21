package com.stseditor.runtime;
public final class CardExtensionContext {
 public final CustomCard card;public final com.megacrit.cardcrawl.characters.AbstractPlayer player;public final com.megacrit.cardcrawl.monsters.AbstractMonster target;
 public final int amount;public final com.google.gson.JsonObject parameters;
 public CardExtensionContext(CustomCard c,com.megacrit.cardcrawl.characters.AbstractPlayer p,com.megacrit.cardcrawl.monsters.AbstractMonster m,int amount,com.google.gson.JsonObject parameters){this.card=c;this.player=p;this.target=m;this.amount=amount;this.parameters=parameters;}
}

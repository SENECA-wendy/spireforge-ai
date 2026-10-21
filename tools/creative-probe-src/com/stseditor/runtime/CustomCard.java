package com.stseditor.runtime;
public class CustomCard extends com.megacrit.cardcrawl.cards.AbstractCard {
 public CustomCard(boolean upgraded){super("probe","测试卡","",0,"",CardType.SKILL,CardColor.RED,CardRarity.RARE,CardTarget.SELF);this.upgraded=upgraded;}
 public CustomCard(boolean upgraded,com.google.gson.JsonObject s){this(upgraded);cardID=s.get("id").getAsString();name=s.get("name").getAsString()+(upgraded?"+":"");color=CardColor.valueOf(s.get("color").getAsString());type=CardType.valueOf(s.get("type").getAsString());rarity=CardRarity.valueOf(s.get("rarity").getAsString());target=CardTarget.valueOf(s.get("target").getAsString());cost=costForTurn=value(s,"cost","upgradeCost",upgraded,0);baseDamage=damage=value(s,"damage","upgradeDamage",upgraded,-1);baseBlock=block=value(s,"block","upgradeBlock",upgraded,-1);baseMagicNumber=magicNumber=value(s,"magicNumber","upgradeMagicNumber",upgraded,-1);exhaust=flag(s,"exhaust");isInnate=flag(s,"innate");isEthereal=flag(s,"ethereal");selfRetain=flag(s,"retain");}
 private static boolean flag(com.google.gson.JsonObject s,String k){return s.has(k)&&!s.get(k).isJsonNull()&&s.get(k).getAsBoolean();}
 private static int value(com.google.gson.JsonObject s,String k,String u,boolean up,int fallback){if(up&&s.has(u)&&!s.get(u).isJsonNull())return s.get(u).getAsInt();return s.has(k)&&!s.get(k).isJsonNull()?s.get(k).getAsInt():fallback;}
}

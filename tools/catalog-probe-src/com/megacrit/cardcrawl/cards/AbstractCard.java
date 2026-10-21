package com.megacrit.cardcrawl.cards;
import java.util.ArrayList;
public class AbstractCard {
 public enum CardType{ATTACK,SKILL,POWER,CURSE,STATUS}public enum CardColor{RED,GREEN,BLUE,PURPLE,COLORLESS,CURSE}public enum CardRarity{BASIC,COMMON,UNCOMMON,RARE,SPECIAL,CURSE}public enum CardTarget{SELF,ENEMY,ALL_ENEMY,NONE,SELF_AND_ENEMY,ALL}public enum CardTags{HEALING,STRIKE,EMPTY,STARTER_DEFEND,STARTER_STRIKE}
 public String cardID,name,originalName,rawDescription;public CardType type;public CardColor color;public CardRarity rarity;public CardTarget target;
 public int cost,costForTurn,baseDamage=-1,damage=-1,baseBlock=-1,block=-1,baseMagicNumber=-1,magicNumber=-1,timesUpgraded,misc;
 public boolean upgraded,upgradedCost,upgradedDamage,upgradedBlock,upgradedMagicNumber,exhaust,isEthereal,isInnate,selfRetain,retain,isMultiDamage,shuffleBackIntoDrawPile,returnToHand,isLocked;
 public boolean showEvokeValue;public int showEvokeOrbCount;public AbstractCard cardsToPreview;public ArrayList<CardTags> tags=new ArrayList<CardTags>();public ArrayList<String> keywords=new ArrayList<String>();
 public AbstractCard(String id,String name,String image,int cost,String description,CardType type,CardColor color,CardRarity rarity,CardTarget target){this.cardID=id;this.name=this.originalName=name;this.rawDescription=description;this.cost=this.costForTurn=cost;this.type=type;this.color=color;this.rarity=rarity;this.target=target;}
 public void upgrade(){}protected void upgradeName(){timesUpgraded++;upgraded=true;name=originalName+"+";}
 protected void upgradeDamage(int n){upgradedDamage=true;baseDamage+=n;damage=baseDamage;}protected void upgradeBlock(int n){upgradedBlock=true;baseBlock+=n;block=baseBlock;}protected void upgradeMagicNumber(int n){upgradedMagicNumber=true;baseMagicNumber+=n;magicNumber=baseMagicNumber;}protected void upgradeBaseCost(int n){upgradedCost=true;cost=costForTurn=n;}
 public void initializeDescription(){}public void initializeTitle(){}public void loadCardImage(String path){}public void displayUpgrades(){}
 public boolean hasTag(CardTags tag){return tags.contains(tag);}public void applyPowers(){}public void calculateCardDamage(com.megacrit.cardcrawl.monsters.AbstractMonster m){}
 public AbstractCard makeCopy(){throw new UnsupportedOperationException();}
}

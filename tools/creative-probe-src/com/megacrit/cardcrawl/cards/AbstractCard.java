package com.megacrit.cardcrawl.cards;
import com.megacrit.cardcrawl.characters.AbstractPlayer;
import com.megacrit.cardcrawl.monsters.AbstractMonster;
public class AbstractCard {
 public enum CardType {ATTACK,SKILL,POWER,CURSE,STATUS}
 public enum CardColor {RED,GREEN,BLUE,PURPLE,COLORLESS,CURSE}
 public enum CardRarity {BASIC,COMMON,UNCOMMON,RARE,SPECIAL,CURSE}
 public enum CardTarget {SELF,ENEMY,ALL_ENEMY,NONE,SELF_AND_ENEMY,ALL}
 public String cardID,name,rawDescription;public boolean upgraded,exhaust,isInnate,isEthereal,retain,selfRetain,purgeOnUse,freeToPlayOnce;
 public int baseDamage,damage,baseBlock,block,baseMagicNumber,magicNumber,cost,costForTurn,timesUpgraded;public CardType type;public CardColor color;public CardRarity rarity;public CardTarget target;
 public AbstractCard(String id,String name,String img,int cost,String desc,CardType type,CardColor color,CardRarity rarity,CardTarget target){this.cardID=id;this.name=name;this.cost=cost;this.costForTurn=cost;this.rawDescription=desc;this.type=type;this.color=color;this.rarity=rarity;this.target=target;}
 public void use(AbstractPlayer p,AbstractMonster m){}
 public AbstractCard makeCopy(){return new AbstractCard(cardID,name,"",cost,rawDescription,type,color,rarity,target);}
 public AbstractCard makeStatEquivalentCopy(){AbstractCard c=makeCopy();if(upgraded)c.upgrade();return c;}
 public void upgrade(){}protected void upgradeName(){upgraded=true;timesUpgraded++;name+="+";}
 protected void upgradeDamage(int n){baseDamage+=n;damage=baseDamage;}protected void upgradeBlock(int n){baseBlock+=n;block=baseBlock;}
 protected void upgradeMagicNumber(int n){baseMagicNumber+=n;magicNumber=baseMagicNumber;}protected void upgradeBaseCost(int n){cost=costForTurn=n;}
 public void initializeDescription(){}public void applyPowers(){}public void calculateCardDamage(AbstractMonster m){}
}

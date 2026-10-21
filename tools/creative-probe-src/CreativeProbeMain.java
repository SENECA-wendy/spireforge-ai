import com.google.gson.*;
import com.megacrit.cardcrawl.cards.*;
import com.megacrit.cardcrawl.characters.AbstractPlayer;
import com.megacrit.cardcrawl.dungeons.AbstractDungeon;
import com.stseditor.runtime.*;
import java.nio.file.*;
public final class CreativeProbeMain {
 public static void main(String[] args) throws Exception {
  int passed=0;boolean unsupported=false;
  JsonObject config=new JsonParser().parse(new String(Files.readAllBytes(Paths.get(args[0])),"UTF-8")).getAsJsonObject();
  for(boolean upgraded:new boolean[]{false,true}) {
   try {
    probe.Observations.reset();AbstractDungeon.player=new AbstractPlayer();AbstractDungeon.actionManager=new com.megacrit.cardcrawl.actions.GameActionManager();AbstractPlayer p=AbstractDungeon.player;
    for(CardGroup g:new CardGroup[]{p.hand,p.drawPile,p.discardPile,p.masterDeck})for(int i=0;i<2;i++)g.group.add(new AbstractCard("fixture","测试牌","",1,"",AbstractCard.CardType.ATTACK,AbstractCard.CardColor.RED,AbstractCard.CardRarity.BASIC,AbstractCard.CardTarget.ENEMY));
    java.util.ArrayList<AbstractCard> masterBefore=new java.util.ArrayList<AbstractCard>(p.masterDeck.group);
    CardExtension logic=(CardExtension)Class.forName(config.get("className").getAsString()).getDeclaredConstructor().newInstance();
    logic.onUse(new CardExtensionContext(new CustomCard(upgraded,config.getAsJsonObject("card")),p,new com.megacrit.cardcrawl.monsters.AbstractMonster(),config.get(upgraded?"upgradedAmount":"baseAmount").getAsInt(),config.getAsJsonObject("parameters")));
    AbstractDungeon.actionManager.drain();
    if(!config.get("allowMasterDeckChange").getAsBoolean() && !p.masterDeck.group.equals(masterBefore))throw new AssertionError("masterDeck changed without permission");
    for(JsonElement raw:config.getAsJsonArray("probes")) {
     JsonObject test=raw.getAsJsonObject();String metric=test.get("metric").getAsString();String name=test.has("cardName")&&!test.get("cardName").isJsonNull()?test.get("cardName").getAsString():null;
     if(upgraded&&test.has("upgradedCardName")&&!test.get("upgradedCardName").isJsonNull())name=test.get("upgradedCardName").getAsString();
     int actual=metric.equals("masterDeckSize")?p.masterDeck.size():metric.equals("handSize")?p.hand.size():metric.equals("drawPileSize")?p.drawPile.size():metric.equals("discardPileSize")?p.discardPile.size():metric.startsWith("cardsTo")?count(metric.equals("cardsToHand")?p.hand:metric.equals("cardsToDrawPile")?p.drawPile:p.discardPile,name):probe.Observations.get(metric);
     int expected=test.get(upgraded?"upgradedExpected":"baseExpected").getAsInt();
     if(actual!=expected)throw new AssertionError((upgraded?"upgraded":"base")+" "+metric+" "+(name==null?"":name)+": expected="+expected+", actual="+actual);
     passed++;
    }
   } catch(AssertionError failure){System.out.println(failure.getMessage());System.exit(1);}
   catch(LinkageError failure){unsupported=true;System.out.println("未运行完成 "+(upgraded?"升级":"基础")+"：简化环境不支持 "+failure.getClass().getSimpleName()+" "+failure.getMessage());}
   catch(UnsupportedOperationException failure){unsupported=true;System.out.println("未运行完成："+failure.getMessage());}
   catch(Throwable failure){System.out.println("执行失败："+failure.getClass().getSimpleName()+" "+failure.getMessage());System.exit(1);}
  }
  System.out.println("简化环境行为检查："+passed+" 项通过"+(unsupported?"；存在未验证项，须实战检查":"（基础及升级分别执行）"));
  System.exit(unsupported?2:0);
 }
 private static int count(CardGroup group,String name){int n=0;for(AbstractCard card:group.group)if(!"fixture".equals(card.cardID)&&(name==null||name.equals(card.name)))n++;return n;}
}

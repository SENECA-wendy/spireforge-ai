import com.google.gson.*;
import com.stseditor.runtime.*;
import com.megacrit.cardcrawl.cards.*;
import com.megacrit.cardcrawl.characters.*;
import com.megacrit.cardcrawl.core.*;
import com.megacrit.cardcrawl.dungeons.*;
import com.megacrit.cardcrawl.helpers.*;
import com.megacrit.cardcrawl.shop.*;
import java.util.*;

public class RunCardPoolTest {
    private static int passed;
    private static void check(boolean c,String name) { if(!c) throw new AssertionError(name); passed++; System.out.println("PASS "+name); }
    private static JsonObject policy(String... ids) { JsonObject p=new JsonObject(); p.addProperty("runKey",LiveDeckService.runKey()); p.addProperty("enabled",true); JsonArray a=new JsonArray(); for(String id:ids) a.add(id); p.add("cardIds",a); return p; }
    private static ArrayList<AbstractCard> offers(int count) { ArrayList<AbstractCard> a=new ArrayList<AbstractCard>(); for(int i=0;i<count;i++) a.add(new AbstractCard("Defend_R")); return a; }
    private static boolean only(List<AbstractCard> cards,String id) { for(AbstractCard c:cards) if(!id.equals(c.cardID)) return false; return true; }
    public static void main(String[] args) throws Exception {
        AbstractDungeon.player=new AbstractPlayer(); AbstractDungeon.floorNum=0; RunCardPoolService.onNewDungeon();
        CardLibrary.cards.put("Strike_R",new AbstractCard("Strike_R")); CardLibrary.cards.put("Defend_R",new AbstractCard("Defend_R"));
        AbstractCard curse=new AbstractCard("Curse"); curse.type=AbstractCard.CardType.CURSE; CardLibrary.cards.put("Curse",curse);
        ArrayList<AbstractCard> originals=offers(3);
        check(RunCardPoolService.rewards(originals)==originals,"disabled policy preserves original reward objects and RNG path");
        RunCardPoolService.configure(policy("Strike_R"));
        ArrayList<AbstractCard> rewards=RunCardPoolService.rewards(originals);
        check(rewards.size()==3 && only(rewards,"Strike_R"),"singleton pool fills every reward slot without an unbounded uniqueness loop");
        check(rewards.get(0)!=rewards.get(1) && rewards.get(0).uuid!=rewards.get(1).uuid,"repeated choices are independent card instances");
        check(only(RunCardPoolService.shopOffers(offers(5)),"Strike_R") && only(RunCardPoolService.shopOffers(offers(2)),"Strike_R"),"colored and colorless merchant slots use the same whitelist");
        check(only(originals,"Defend_R"),"reward generation leaves vanilla source objects untouched");
        originals.get(0).upgrade(); check(RunCardPoolService.rewards(originals).get(0).timesUpgraded==1,"reward replacement preserves an upgraded offer");
        int master=AbstractDungeon.player.masterDeck.size(); RunCardPoolService.rewards(originals); check(master==AbstractDungeon.player.masterDeck.size(),"acquisition pool does not add or remove owned cards");
        boolean reject=false; try{RunCardPoolService.configure(policy());}catch(IllegalArgumentException e){reject=true;} check(reject && RunCardPoolService.describe().get("count").getAsInt()==1,"empty enabled pool is rejected without discarding active policy");
        reject=false; try{RunCardPoolService.configure(policy("Missing"));}catch(IllegalArgumentException e){reject=true;} check(reject,"missing card cannot silently leak vanilla fallback rewards");
        reject=false; try{RunCardPoolService.configure(policy("Curse"));}catch(IllegalArgumentException e){reject=true;} check(reject,"curse and status definitions cannot enter normal acquisition pool");
        JsonObject stale=policy("Strike_R"); stale.addProperty("runKey","another-run"); reject=false; try{RunCardPoolService.configure(stale);}catch(IllegalArgumentException e){reject=true;} check(reject,"policy from another run is rejected");
        RunCardPoolService.onLoadDungeon(); check(RunCardPoolService.describe().get("enabled").getAsBoolean(),"saved policy reloads for the same seed character and ascension");
        AbstractDungeon.floorNum=18; RunCardPoolService.onNewDungeon(); check(RunCardPoolService.describe().get("enabled").getAsBoolean(),"advancing to another act preserves the run policy");
        Settings.seed=456L; check(!RunCardPoolService.describe().get("enabled").getAsBoolean(),"different seed cannot inherit the previous policy");
        Settings.seed=123L; AbstractDungeon.floorNum=0; RunCardPoolService.onNewDungeon(); check(!RunCardPoolService.describe().get("enabled").getAsBoolean(),"new run with the same seed resets the previous policy");
        RunCardPoolService.configure(policy("Strike_R","Defend_R")); rewards=RunCardPoolService.rewards(offers(3)); check(!rewards.get(0).cardID.equals(rewards.get(1).cardID),"whitelist choices are distinct until available IDs are exhausted");
        RunCardPoolService.configure(policy("Strike_R")); ShopScreen shop=new ShopScreen(); AbstractCard red=new AbstractCard("Strike_R"); red.color=AbstractCard.CardColor.COLORLESS; shop.coloredCards.add(red); AbstractCard gray=new AbstractCard("Strike_R"); shop.colorlessCards.add(gray);
        check(RunCardPoolService.slotColor(red,shop)==AbstractCard.CardColor.RED && RunCardPoolService.slotColor(gray,shop)==AbstractCard.CardColor.COLORLESS,"purchase routes by actual merchant slot even across card colors");
        check(RunCardPoolService.shopBasePrice(AbstractCard.CardRarity.BASIC)==50 && RunCardPoolService.shopBasePrice(AbstractCard.CardRarity.SPECIAL)==50,"starter and special cards are buyable instead of vanilla 9999 pricing");
        AbstractCard restock=new AbstractCard("Defend_R"); restock.price=42; restock.current_x=200; shop.coloredCards.add(restock); RunCardPoolService.afterPurchase(shop);
        check(only(shop.coloredCards,"Strike_R") && shop.coloredCards.get(1).price==42 && shop.coloredCards.get(1).current_x==200,"Courier restock is restricted while preserving price and position");
        JsonObject off=policy(); off.addProperty("enabled",false); RunCardPoolService.configure(off); check(RunCardPoolService.rewards(originals)==originals,"restore original policy stops rewriting future rewards");
        check(LiveEditSafety.overlayAllowed(true,"MAP",false,false,true),"route map permits permanent run deck edits");
        check(!LiveEditSafety.overlayAllowed(true,"MAP",false,true,true),"opening map during combat does not bypass action protection");
        check(!LiveEditSafety.overlayAllowed(true,"GRID",false,false,true) && !LiveEditSafety.overlayAllowed(true,"HAND_SELECT",false,false,true),"card choice and removal screens remain protected");
        check(!LiveEditSafety.overlayAllowed(true,"MAP",true,false,true) && !LiveEditSafety.overlayAllowed(true,"MAP",false,false,false),"transitions and temporary combat edits remain blocked on map");
        String[] strikes={"Strike_R","Strike_G","Strike_B","Strike_P"};AbstractPlayer.PlayerClass[] classes={AbstractPlayer.PlayerClass.IRONCLAD,AbstractPlayer.PlayerClass.THE_SILENT,AbstractPlayer.PlayerClass.DEFECT,AbstractPlayer.PlayerClass.WATCHER};
        for(int i=0;i<classes.length;i++){
            AbstractDungeon.player=new AbstractPlayer();AbstractDungeon.player.chosenClass=classes[i];Settings.seed=999L;AbstractDungeon.floorNum=0;RunCardPoolService.onNewDungeon();AbstractCard card=new AbstractCard(strikes[i]);card.color=AbstractDungeon.player.getCardColor();CardLibrary.cards.put(strikes[i],card);RunCardPoolService.configure(policy(strikes[i]));
            check(only(RunCardPoolService.rewards(offers(3)),strikes[i])&&only(RunCardPoolService.shopOffers(offers(5)),strikes[i]),"rewards and shops obey singleton whitelist for "+classes[i]);
            ShopScreen merchant=new ShopScreen();AbstractCard foreign=new AbstractCard(strikes[i]);foreign.color=AbstractCard.CardColor.COLORLESS;merchant.coloredCards.add(foreign);check(RunCardPoolService.slotColor(foreign,merchant)==AbstractDungeon.player.getCardColor(),"merchant slot routing uses actual profession for "+classes[i]);
            JsonObject snapshot=new JsonObject();LiveDeckService.describe(snapshot);check(snapshot.get("cardColor").getAsString().equals(AbstractDungeon.player.getCardColor().name())&&RunCardPoolService.describe().get("playerClass").getAsString().equals(classes[i].name()),"deck and acquisition snapshots expose current profession for "+classes[i]);
        }
        RunCardPoolService.configure(policy("Strike_R","Strike_P"));ArrayList<AbstractCard> mixed=RunCardPoolService.rewards(offers(3));check(mixed.size()==3&&mixed.stream().allMatch(c->c.cardID.equals("Strike_R")||c.cardID.equals("Strike_P")),"mixed-profession acquisition pool remains valid in another character's run");
        System.out.println("TOTAL "+passed+" acquisition-pool/map checks passed (isolated engine fixtures)");
    }
}

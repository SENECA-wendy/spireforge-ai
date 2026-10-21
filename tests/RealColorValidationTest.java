import com.stseditor.runtime.*;
import com.megacrit.cardcrawl.cards.AbstractCard;
public class RealColorValidationTest {
 static int passed;static void check(boolean ok,String text){if(!ok)throw new AssertionError(text);passed++;System.out.println("PASS "+text);}
 public static void main(String[] args)throws Exception{
  for(String color:new String[]{"RED","GREEN","BLUE","PURPLE","COLORLESS"}) {
   CardSpec spec=new CardSpec();spec.id="stseditor_color_"+color;spec.name="color probe";spec.color=color;spec.type="SKILL";spec.target="SELF";spec.rarity="COMMON";spec.description="gain energy";spec.upgradeDescription="gain energy";spec.cost=1;spec.upgradeCost=0;CardSpec.EffectSpec effect=new CardSpec.EffectSpec();effect.type="GAIN_ENERGY";effect.amount=1;spec.effects.add(effect);
   check(ContentRegistry.validateSpec(spec).isEmpty(),"actual runtime validator accepts "+color);
   CustomCard card=new CustomCard(spec);card.upgrade();check(card.color.name().equals(color)&&card.makeCopy().color.name().equals(color)&&card.cost==0,"actual custom card constructor/upgrade/copy preserves "+color);
  }
  CardSpec bad=new CardSpec();bad.id="stseditor_invalid_color";bad.name="bad";bad.color="NOT_A_COLOR";check(ContentRegistry.validateSpec(bad).toString().contains("invalid color"),"actual runtime validator still rejects unknown colors");
  System.out.println("TOTAL "+passed+" real-runtime color checks passed (non-graphical base card)");
 }
}

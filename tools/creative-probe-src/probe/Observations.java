package probe;
import com.megacrit.cardcrawl.cards.AbstractCard;
public final class Observations {
 public static java.util.Map<String,Integer> values=new java.util.HashMap<String,Integer>();
 public static java.util.Map<String,java.util.List<String>> created=new java.util.HashMap<String,java.util.List<String>>();
 public static void add(String k,int n){values.put(k,get(k)+n);}public static int get(String k){return values.containsKey(k)?values.get(k):0;}
 public static void card(String k,AbstractCard c){if(!created.containsKey(k))created.put(k,new java.util.ArrayList<String>());created.get(k).add(c.name);add(k,1);}
 public static int named(String k,String name,boolean upgraded){if(name==null||name.length()==0)return get(k);int n=0;if(created.containsKey(k))for(String s:created.get(k))if(name.equals(s)||(upgraded&&(name+"+").equals(s)))n++;return n;}
 public static void reset(){values.clear();created.clear();}
}

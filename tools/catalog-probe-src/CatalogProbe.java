import com.google.gson.*;
import com.megacrit.cardcrawl.cards.AbstractCard;
import java.io.*;import java.nio.file.*;import java.util.*;import java.util.jar.*;import java.security.*;
import org.objectweb.asm.*;
/** Runs the game's real card constructors and upgrade methods against an isolated, non-graphical base card. Never loads installed editor patches. */
public final class CatalogProbe {
 static final Map<String,String> notes=new HashMap<String,String>();
 static {
  notes.put("Bane","目标中毒时追加一次攻击，伤害为每次攻击数值。");notes.put("Skewer","X费攻击次数由消耗能量决定，伤害为每次攻击数值。");
  notes.put("Malaise","减力量与虚弱层数按消耗能量及升级状态计算，并非普通伤害字段。");notes.put("Grand Finale","只在抽牌堆为空时可以打出。");notes.put("Catalyst","附加数值是中毒层数的乘数，不是固定施毒量。");notes.put("Poisoned Stab","附加数值控制中毒层数，伤害独立计算。");
  notes.put("Flechettes","攻击次数取决于手牌中的技能牌数量。");notes.put("Finisher","攻击次数取决于本回合已打出的攻击牌数量。");notes.put("Eviscerate","费用随本回合弃牌数量变化。");notes.put("Well Laid Plans","保留数量由附加数值决定，升级会改变保留数量。");
  notes.put("Barrage","攻击次数取决于充能球数量，伤害为每次攻击数值。");notes.put("Blizzard","伤害按本场战斗生成的冰霜球数量计算。");notes.put("Thunder Strike","攻击次数取决于本场战斗生成的闪电球数量。");notes.put("Streamline","本场战斗中每次打出后费用下降。");notes.put("Claw","伤害会在战斗中随利爪效果增长，修改后仍会受该机制影响。");notes.put("Genetic Algorithm","格挡从该实例积累的misc值计算，不只由目录基础格挡决定。");notes.put("Reinforced Body","X费牌，格挡次数由消耗能量决定。");notes.put("Multi-Cast","X费牌，激发次数由消耗能量及升级状态决定。");notes.put("Meteor Strike","伤害与生成等离子球是两个机制，球类型与数量不由伤害字段控制。");notes.put("Dualcast","激发球效果取决于球类型与集中，并非普通卡牌伤害。");
  notes.put("Force Field","费用随本场战斗已打出的能力牌数量降低，目录显示无战斗修正的基础费用。");
  notes.put("Eruption","进入愤怒后攻击伤害会受到姿态倍率影响。");notes.put("Vigilance","获得格挡并进入平静；姿态机制不由格挡数值决定。");notes.put("Wallop","获得的格挡取决于实际造成的未被格挡伤害。");notes.put("Pressure Points","附加数值是印记层数，实际失去生命取决于印记机制。");notes.put("Ragnarok","随机多段攻击，伤害和段数分别由伤害及附加数值控制。");notes.put("Conjure Blade","X费技能生成特殊牌，攻击次数取决于能量；子卡数值不等同于本牌伤害字段。");notes.put("Blasphemy","进入神格并在下回合触发死亡机制；数值字段不代表免除代价。");notes.put("Sands of Time","费用会因保留而下降。");notes.put("WindmillStrike","伤害会因保留而增长。");notes.put("Perseverance","格挡会因保留而增长。");notes.put("LessonLearned","击杀时升级主牌组的一张牌，伤害字段不会改变升级机制。");
 }
 public static void main(String[] args)throws Exception{
  String lang=args.length>3?args[3]:"zhs";if(!lang.equals("eng")&&!lang.equals("zhs"))throw new IllegalArgumentException("unsupported localization");
  com.megacrit.cardcrawl.core.Settings.language=lang.equals("eng")?com.megacrit.cardcrawl.core.Settings.GameLanguage.ENG:com.megacrit.cardcrawl.core.Settings.GameLanguage.ZHS;
  File jarFile=new File(args[0]);JsonObject prior=args.length>2&&Files.isRegularFile(Paths.get(args[2]))?new JsonParser().parse(new String(Files.readAllBytes(Paths.get(args[2])),"UTF-8")).getAsJsonObject():null;
  if(prior!=null)for(JsonElement e:prior.getAsJsonArray("cards")){JsonObject c=e.getAsJsonObject();if(c.has("specialNote"))notes.put(c.get("id").getAsString(),c.get("specialNote").getAsString());}
  final Set<String> registered=new TreeSet<String>();JsonArray result=new JsonArray();int failures=0;Map<String,Integer> counts=new TreeMap<String,Integer>();
  try(JarFile jar=new JarFile(jarFile)){
   com.megacrit.cardcrawl.localization.LocalizedStrings.cards=new JsonParser().parse(new String(read(jar.getInputStream(jar.getJarEntry("localization/"+lang+"/cards.json"))),"UTF-8")).getAsJsonObject();
   com.megacrit.cardcrawl.localization.LocalizedStrings.keywords=new JsonParser().parse(new String(read(jar.getInputStream(jar.getJarEntry("localization/"+lang+"/keywords.json"))),"UTF-8")).getAsJsonObject();
   new ClassReader(read(jar.getInputStream(jar.getJarEntry("com/megacrit/cardcrawl/helpers/CardLibrary.class")))).accept(new ClassVisitor(Opcodes.ASM9){public MethodVisitor visitMethod(int a,String name,String desc,String sig,String[] ex){if(!name.matches("add(Red|Green|Blue|Purple)Cards"))return null;return new MethodVisitor(Opcodes.ASM9){public void visitTypeInsn(int opcode,String type){if(opcode==Opcodes.NEW&&type.matches("com/megacrit/cardcrawl/cards/(red|green|blue|purple)/[^/$]+"))registered.add(type);}};}},0);
   for(String type:registered)try{
    AbstractCard base=(AbstractCard)Class.forName(type.replace('/','.')).getDeclaredConstructor().newInstance();AbstractCard up=(AbstractCard)Class.forName(type.replace('/','.')).getDeclaredConstructor().newInstance();up.upgrade();
    if(base.name==null||base.name.length()==0||base.color==null||base.type==null)throw new IllegalStateException("incomplete identity");
    JsonObject c=new JsonObject();c.addProperty("id",base.cardID);c.addProperty("className",type.substring(type.lastIndexOf('/')+1));c.addProperty("name",base.name);c.addProperty("description",base.rawDescription);c.addProperty("upgradeDescription",up.rawDescription);c.addProperty("cost",base.cost);c.addProperty("upgradeCost",up.cost);c.addProperty("type",base.type.name());c.addProperty("color",base.color.name());c.addProperty("rarity",base.rarity.name());c.addProperty("target",base.target.name());
    stat(c,"damage",base.baseDamage);stat(c,"upgradeDamage",up.baseDamage);stat(c,"block",base.baseBlock);stat(c,"upgradeBlock",up.baseBlock);stat(c,"magicNumber",base.baseMagicNumber);stat(c,"upgradeMagicNumber",up.baseMagicNumber);
    String note=notes.get(base.cardID);if(note==null){String simple=type.substring(type.lastIndexOf('/')+1);for(Map.Entry<String,String> entry:notes.entrySet())if(entry.getKey().replace(" ","").replace("-","").equals(simple)){note=entry.getValue();break;}}final boolean[] dynamic={false};new ClassReader(read(jar.getInputStream(jar.getJarEntry(type+".class")))).accept(new ClassVisitor(Opcodes.ASM9){public MethodVisitor visitMethod(int a,String n,String d,String s,String[] ex){if(n.equals("applyPowers")||n.equals("calculateCardDamage"))dynamic[0]=true;return null;}},0);
    if(note==null&&dynamic[0])note="数值会根据战场状态重新计算；修改基础字段后仍遵守原生动态机制。";if(note==null&&base.cost==-1)note="X费牌，效果次数或层数由消耗的能量决定。";
    c.addProperty("special",note!=null);if(note!=null)c.addProperty("specialNote",note);result.add(c);counts.put(base.color.name(),counts.containsKey(base.color.name())?counts.get(base.color.name())+1:1);
   }catch(Throwable error){failures++;Throwable cause=error;while(cause.getCause()!=null)cause=cause.getCause();System.err.println(type+": "+cause);}
  }
  if(failures>0||registered.size()!=300)throw new IllegalStateException("catalog incomplete: "+failures+" failures, registered="+registered.size()+"; output not written");
  JsonObject root=new JsonObject();root.addProperty("schemaVersion",1);root.addProperty("gameVersion","2.3.4");root.addProperty("jarSha256",hex(MessageDigest.getInstance("SHA-256").digest(Files.readAllBytes(jarFile.toPath()))));root.addProperty("source","original CardLibrary registrations + actual card constructors/upgrade with isolated non-graphical base + localization/zhs/cards.json");root.add("cards",result);
  if(lang.equals("eng"))root.addProperty("source","original CardLibrary + constructors/upgrade + localization/eng/cards.json");
  Files.write(Paths.get(args[1]),new GsonBuilder().setPrettyPrinting().serializeNulls().disableHtmlEscaping().create().toJson(root).getBytes("UTF-8"));System.out.println("registered="+registered.size()+" counts="+counts+" output="+args[1]);
 }
 static void stat(JsonObject o,String key,int value){if(value<0)o.add(key,JsonNull.INSTANCE);else o.addProperty(key,value);}static byte[] read(InputStream in)throws Exception{try(InputStream stream=in;ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] b=new byte[8192];int n;while((n=stream.read(b))!=-1)out.write(b,0,n);return out.toByteArray();}}static String hex(byte[] b){StringBuilder s=new StringBuilder();for(byte v:b)s.append(String.format("%02x",v&255));return s.toString();}
}

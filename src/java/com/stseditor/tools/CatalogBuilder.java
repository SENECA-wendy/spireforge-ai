package com.stseditor.tools;

import com.google.gson.GsonBuilder;
import com.google.gson.JsonArray;
import com.google.gson.JsonObject;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStreamWriter;
import java.io.Writer;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.jar.JarEntry;
import java.util.jar.JarFile;
import org.objectweb.asm.ClassReader;
import org.objectweb.asm.ClassVisitor;
import org.objectweb.asm.FieldVisitor;
import org.objectweb.asm.MethodVisitor;
import org.objectweb.asm.Opcodes;
import org.objectweb.asm.Type;

/**
 * Extracts the Ironclad card catalog from the user's own game jar.
 * Usage: CatalogBuilder <desktop-1.0.jar> <zhs-cards.json> <out.json>
 */
public final class CatalogBuilder {
    private static final Map<String, String> SPECIAL = new HashMap<String, String>();

    static {
        SPECIAL.put("Body Slam", "伤害来自当前格挡，基础伤害字段不会单独决定结果。");
        SPECIAL.put("Heavy Blade", "力量加成受 !M! 倍率影响。");
        SPECIAL.put("Perfected Strike", "伤害随牌组中的打击牌数量增加。");
        SPECIAL.put("Searing Blow", "可多次升级，升级次数会影响伤害。");
        SPECIAL.put("Fiend Fire", "消耗手牌并按消耗数量结算伤害。");
        SPECIAL.put("Whirlwind", "X 费群体攻击，伤害与能量相关。");
        SPECIAL.put("Reaper", "按未被格挡的伤害回复生命。");
        SPECIAL.put("Feed", "击杀时提高最大生命。");
        SPECIAL.put("Blood for Blood", "费用会随受到伤害降低。");
        SPECIAL.put("Limit Break", "使力量翻倍，不使用伤害字段。");
        SPECIAL.put("Double Tap", "使下一张攻击牌打出两次。");
        SPECIAL.put("Corruption", "改变技能牌的费用与消耗规则。");
    }

    public static void main(String[] args) throws Exception {
        if (args.length != 3) {
            System.err.println("Usage: CatalogBuilder <desktop-1.0.jar> <zhs-cards.json> <out.json>");
            System.exit(2);
        }
        File jarFile = new File(args[0]);
        File locFile = new File(args[1]);
        File outFile = new File(args[2]);
        Map<String, JsonObject> loc = readLocalization(locFile);
        List<CardInfo> cards = new ArrayList<CardInfo>();
        try (JarFile jar = new JarFile(jarFile)) {
            List<String> names = new ArrayList<String>();
            for (JarEntry e : Collections.list(jar.entries())) {
                String n = e.getName();
                if (n.startsWith("com/megacrit/cardcrawl/cards/red/") && n.endsWith(".class")
                        && n.indexOf('$') < 0) {
                    names.add(n);
                }
            }
            Collections.sort(names);
            for (String n : names) {
                try (InputStream in = jar.getInputStream(jar.getJarEntry(n))) {
                    CardInfo c = parseCard(readAll(in), n);
                    if (c != null) {
                        c.name = locName(loc, c.id, "NAME");
                        c.description = locName(loc, c.id, "DESCRIPTION");
                        cards.add(c);
                    }
                }
            }
        }
        Collections.sort(cards, new Comparator<CardInfo>() {
            @Override
            public int compare(CardInfo a, CardInfo b) {
                int x = a.name == null ? 0 : a.name.compareTo(b.name == null ? "" : b.name);
                return x != 0 ? x : a.id.compareTo(b.id);
            }
        });
        JsonObject root = new JsonObject();
        root.addProperty("schemaVersion", 1);
        root.addProperty("gameVersion", "2.3.4");
        root.addProperty("jarSha256", sha256(jarFile));
        root.addProperty("source", "desktop-1.0.jar + localization/zhs/cards.json");
        JsonArray arr = new JsonArray();
        for (CardInfo c : cards) {
            arr.add(c.toJson());
        }
        root.add("cards", arr);
        try (Writer w = new OutputStreamWriter(new FileOutputStream(outFile), StandardCharsets.UTF_8)) {
            new GsonBuilder().setPrettyPrinting().disableHtmlEscaping().create().toJson(root, w);
        }
        System.out.println("cards=" + cards.size() + " out=" + outFile.getAbsolutePath());
    }

    private static Map<String, JsonObject> readLocalization(File file) throws Exception {
        Map<String, JsonObject> out = new HashMap<String, JsonObject>();
        try (InputStream in = new FileInputStream(file)) {
            String text = new String(readAll(in), StandardCharsets.UTF_8);
            JsonObject root = new com.google.gson.JsonParser().parse(text).getAsJsonObject();
            for (Map.Entry<String, com.google.gson.JsonElement> e : root.entrySet()) {
                if (e.getValue().isJsonObject()) {
                    out.put(e.getKey(), e.getValue().getAsJsonObject());
                }
            }
        }
        return out;
    }

    private static String locName(Map<String, JsonObject> loc, String id, String field) {
        JsonObject o = loc.get(id);
        if (o == null || !o.has(field)) {
            return "";
        }
        return o.get(field).getAsString();
    }

    private static CardInfo parseCard(byte[] bytes, String entry) {
        final CardInfo info = new CardInfo();
        info.className = entry.substring(entry.lastIndexOf('/') + 1, entry.length() - 6);
        info.specialNote = SPECIAL.get(info.className);
        ClassReader reader = new ClassReader(bytes);
        reader.accept(new ClassVisitor(Opcodes.ASM9) {
            @Override
            public FieldVisitor visitField(int access, String name, String descriptor, String signature, Object value) {
                if ("ID".equals(name) && "Ljava/lang/String;".equals(descriptor) && value instanceof String) {
                    info.id = (String) value;
                }
                return null;
            }

            @Override
            public MethodVisitor visitMethod(int access, String name, String descriptor, String signature, String[] exceptions) {
                if ("<init>".equals(name)) {
                    return new ConstructorVisitor(info);
                }
                if ("upgrade".equals(name) && "()V".equals(descriptor)) {
                    return new UpgradeVisitor(info);
                }
                return null;
            }
        }, 0);
        if (info.id == null || info.id.length() == 0) {
            return null;
        }
        return info;
    }

    private static final class ConstructorVisitor extends MethodVisitor {
        private final CardInfo info;
        private final List<Object> stack = new ArrayList<Object>();
        private int lastInt = Integer.MIN_VALUE;

        ConstructorVisitor(CardInfo info) {
            super(Opcodes.ASM9);
            this.info = info;
        }

        @Override
        public void visitInsn(int opcode) {
            if (opcode >= Opcodes.ICONST_M1 && opcode <= Opcodes.ICONST_5) {
                int value = opcode - Opcodes.ICONST_0;
                stack.add(Integer.valueOf(value));
                lastInt = value;
            } else if (opcode == Opcodes.DUP) {
                if (!stack.isEmpty()) {
                    stack.add(stack.get(stack.size() - 1));
                }
            } else if (opcode == Opcodes.DUP_X1) {
                if (stack.size() >= 2) {
                    Object a = stack.remove(stack.size() - 1);
                    Object b = stack.remove(stack.size() - 1);
                    stack.add(a);
                    stack.add(b);
                    stack.add(a);
                }
            }
        }

        @Override
        public void visitIntInsn(int opcode, int operand) {
            if (opcode == Opcodes.BIPUSH || opcode == Opcodes.SIPUSH) {
                stack.add(Integer.valueOf(operand));
                lastInt = operand;
            }
        }

        @Override
        public void visitLdcInsn(Object value) {
            stack.add(value);
        }

        @Override
        public void visitFieldInsn(int opcode, String owner, String name, String descriptor) {
            if (opcode == Opcodes.GETSTATIC) {
                stack.add("FIELD:" + owner + "." + name);
            } else if (opcode == Opcodes.PUTFIELD) {
                Object value = stack.isEmpty() ? null : stack.remove(stack.size() - 1);
                if ("baseDamage".equals(name)) {
                    info.damage = lastInt == Integer.MIN_VALUE ? null : Integer.valueOf(lastInt);
                } else if ("baseBlock".equals(name)) {
                    info.block = lastInt == Integer.MIN_VALUE ? null : Integer.valueOf(lastInt);
                } else if ("baseMagicNumber".equals(name)) {
                    info.magicNumber = lastInt == Integer.MIN_VALUE ? null : Integer.valueOf(lastInt);
                }
            }
        }

        @Override
        public void visitVarInsn(int opcode, int var) {
            if (opcode == Opcodes.ALOAD && var == 0) {
                stack.add("THIS");
            }
        }

        @Override
        public void visitMethodInsn(int opcode, String owner, String name, String descriptor, boolean itf) {
            if (opcode == Opcodes.INVOKESPECIAL && "com/megacrit/cardcrawl/cards/AbstractCard".equals(owner)
                    && "<init>".equals(name)) {
                Type[] args = Type.getArgumentTypes(descriptor);
                Object[] values = new Object[args.length];
                for (int i = args.length - 1; i >= 0; --i) {
                    values[i] = stack.isEmpty() ? null : stack.remove(stack.size() - 1);
                }
                for (Object v : values) {
                    if (v instanceof Integer) {
                        info.cost = (Integer) v;
                    } else if (v instanceof String) {
                        String s = (String) v;
                        if (s.startsWith("FIELD:") && s.contains("CardType.")) {
                            info.type = s.substring(s.lastIndexOf('.') + 1);
                        } else if (s.startsWith("FIELD:") && s.contains("CardColor.")) {
                            info.color = s.substring(s.lastIndexOf('.') + 1);
                        } else if (s.startsWith("FIELD:") && s.contains("CardRarity.")) {
                            info.rarity = s.substring(s.lastIndexOf('.') + 1);
                        } else if (s.startsWith("FIELD:") && s.contains("CardTarget.")) {
                            info.target = s.substring(s.lastIndexOf('.') + 1);
                        } else if (s.startsWith("FIELD:") && s.contains("CardStrings.NAME")) {
                            info.nameKey = s;
                        } else if (s.startsWith("FIELD:") && s.contains("CardStrings.DESCRIPTION")) {
                            info.descriptionKey = s;
                        }
                    }
                }
            }
        }
    }

    private static final class UpgradeVisitor extends MethodVisitor {
        private final CardInfo info;
        private int lastInt = Integer.MIN_VALUE;

        UpgradeVisitor(CardInfo info) {
            super(Opcodes.ASM9);
            this.info = info;
        }

        @Override
        public void visitInsn(int opcode) {
            if (opcode >= Opcodes.ICONST_M1 && opcode <= Opcodes.ICONST_5) {
                lastInt = opcode - Opcodes.ICONST_0;
            }
        }

        @Override
        public void visitIntInsn(int opcode, int operand) {
            if (opcode == Opcodes.BIPUSH || opcode == Opcodes.SIPUSH) {
                lastInt = operand;
            }
        }

        @Override
        public void visitMethodInsn(int opcode, String owner, String name, String descriptor, boolean itf) {
            if ("upgradeDamage".equals(name)) {
                info.upgradeDamage = lastInt == Integer.MIN_VALUE ? null : Integer.valueOf(lastInt);
            } else if ("upgradeBlock".equals(name)) {
                info.upgradeBlock = lastInt == Integer.MIN_VALUE ? null : Integer.valueOf(lastInt);
            } else if ("upgradeMagicNumber".equals(name)) {
                info.upgradeMagicNumber = lastInt == Integer.MIN_VALUE ? null : Integer.valueOf(lastInt);
            } else if ("upgradeBaseCost".equals(name)) {
                info.upgradeCost = lastInt == Integer.MIN_VALUE ? null : Integer.valueOf(lastInt);
            }
        }
    }

    private static final class CardInfo {
        String id;
        String className;
        String name;
        String nameKey;
        String description;
        String descriptionKey;
        Integer cost;
        String type = "ATTACK";
        String color = "RED";
        String rarity = "COMMON";
        String target = "ENEMY";
        Integer damage;
        Integer block;
        Integer magicNumber;
        Integer upgradeDamage;
        Integer upgradeBlock;
        Integer upgradeMagicNumber;
        Integer upgradeCost;
        String specialNote;

        JsonObject toJson() {
            JsonObject o = new JsonObject();
            o.addProperty("id", id);
            o.addProperty("className", className);
            o.addProperty("name", name == null ? "" : name);
            o.addProperty("description", description == null ? "" : description);
            o.addProperty("cost", cost == null ? 0 : cost.intValue());
            o.addProperty("type", type);
            o.addProperty("color", color);
            o.addProperty("rarity", rarity);
            o.addProperty("target", target);
            addInt(o, "damage", damage);
            addInt(o, "upgradeDamage", upgradeDamage == null || damage == null ? null : Integer.valueOf(damage + upgradeDamage));
            addInt(o, "block", block);
            addInt(o, "upgradeBlock", upgradeBlock == null || block == null ? null : Integer.valueOf(block + upgradeBlock));
            addInt(o, "magicNumber", magicNumber);
            addInt(o, "upgradeMagicNumber", upgradeMagicNumber == null || magicNumber == null ? null : Integer.valueOf(magicNumber + upgradeMagicNumber));
            addInt(o, "upgradeCost", upgradeCost);
            o.addProperty("special", specialNote != null);
            if (specialNote != null) {
                o.addProperty("specialNote", specialNote);
            }
            return o;
        }

        private static void addInt(JsonObject o, String key, Integer value) {
            if (value == null) {
                o.add(key, com.google.gson.JsonNull.INSTANCE);
            } else {
                o.addProperty(key, value.intValue());
            }
        }
    }

    private static byte[] readAll(InputStream in) throws Exception {
        java.io.ByteArrayOutputStream out = new java.io.ByteArrayOutputStream();
        byte[] buffer = new byte[8192];
        int n;
        while ((n = in.read(buffer)) > 0) {
            out.write(buffer, 0, n);
        }
        return out.toByteArray();
    }

    private static String sha256(File file) throws Exception {
        MessageDigest md = MessageDigest.getInstance("SHA-256");
        try (InputStream in = new FileInputStream(file)) {
            byte[] buffer = new byte[8192];
            int n;
            while ((n = in.read(buffer)) > 0) {
                md.update(buffer, 0, n);
            }
        }
        StringBuilder sb = new StringBuilder();
        for (byte b : md.digest()) {
            sb.append(String.format("%02x", b & 0xff));
        }
        return sb.toString();
    }
}

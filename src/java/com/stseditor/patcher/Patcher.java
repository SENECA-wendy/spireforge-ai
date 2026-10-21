package com.stseditor.patcher;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.security.MessageDigest;
import java.util.jar.JarEntry;
import java.util.jar.JarFile;
import java.util.jar.JarOutputStream;
import org.objectweb.asm.ClassReader;
import org.objectweb.asm.ClassVisitor;
import org.objectweb.asm.ClassWriter;
import org.objectweb.asm.MethodVisitor;
import org.objectweb.asm.Opcodes;

/**
 * Builds the class-shadowing patch jar from the user's own desktop-1.0.jar.
 *
 * Usage: Patcher <desktop-1.0.jar> <runtime-classes-dir> <out.jar>
 */
public final class Patcher {
    private static final String REGISTRY = "com/stseditor/runtime/ContentRegistry";
    private static final String BRIDGE = "com/stseditor/runtime/RuntimeBridge";
    private static final String CARD = "com/megacrit/cardcrawl/cards/AbstractCard";
    private static final String CARD_CRAWL_GAME = "com/megacrit/cardcrawl/core/CardCrawlGame";
    private static final String POOL = "com/stseditor/runtime/RunCardPoolService";
    private static final String DUNGEON = "com/megacrit/cardcrawl/dungeons/AbstractDungeon";
    private static final String SHOP = "com/megacrit/cardcrawl/shop/ShopScreen";

    public static void main(String[] args) throws Exception {
        if (args.length != 3) {
            System.err.println("Usage: Patcher <desktop-1.0.jar> <runtime-classes-dir> <out.jar>");
            System.exit(2);
        }
        File gameJar = new File(args[0]);
        File runtimeDir = new File(args[1]);
        File outJar = new File(args[2]);
        if (!gameJar.isFile()) {
            throw new IllegalArgumentException("game jar not found: " + gameJar);
        }
        if (!runtimeDir.isDirectory()) {
            throw new IllegalArgumentException("runtime classes dir not found: " + runtimeDir);
        }

        byte[] cardLibrary = readEntry(gameJar, "com/megacrit/cardcrawl/helpers/CardLibrary.class");
        byte[] abstractCard = readEntry(gameJar, CARD + ".class");
        byte[] cardCrawlGame = readEntry(gameJar, CARD_CRAWL_GAME + ".class");
        byte[] dialogButton = readEntry(gameJar, "com/megacrit/cardcrawl/ui/buttons/LargeDialogOptionButton.class");
        byte[] patchedLibrary = patchCardLibrary(cardLibrary);
        byte[] patchedCard = patchAbstractCard(abstractCard);
        byte[] patchedGame = patchCardCrawlGame(cardCrawlGame);
        byte[] patchedDialog = patchDialogButton(dialogButton);
        byte[] patchedDungeon = patchDungeon(readEntry(gameJar, DUNGEON + ".class"));
        byte[] patchedShop = patchShop(readEntry(gameJar, SHOP + ".class"));

        File parent = outJar.getParentFile();
        if (parent != null) {
            parent.mkdirs();
        }
        try (JarOutputStream out = new JarOutputStream(new FileOutputStream(outJar))) {
            out.putNextEntry(new JarEntry("META-INF/MANIFEST.MF"));
            out.write("Manifest-Version: 1.0\r\nCreated-By: STS Card Editor\r\n\r\n".getBytes("UTF-8"));
            out.closeEntry();
            put(out, "com/megacrit/cardcrawl/helpers/CardLibrary.class", patchedLibrary);
            put(out, CARD + ".class", patchedCard);
            put(out, CARD_CRAWL_GAME + ".class", patchedGame);
            put(out, "com/megacrit/cardcrawl/ui/buttons/LargeDialogOptionButton.class", patchedDialog);
            put(out, DUNGEON + ".class", patchedDungeon);
            put(out, SHOP + ".class", patchedShop);
            addTree(out, runtimeDir, runtimeDir);
        }
        System.out.println("input_sha256=" + sha256(gameJar));
        System.out.println("output_sha256=" + sha256(outJar));
        System.out.println("output=" + outJar.getAbsolutePath());
    }

    private static byte[] patchCardLibrary(byte[] bytes) {
        ClassReader reader = new ClassReader(bytes);
        ClassWriter writer = new ClassWriter(reader, ClassWriter.COMPUTE_MAXS);
        ClassVisitor cv = new ClassVisitor(Opcodes.ASM9, writer) {
            @Override
            public MethodVisitor visitMethod(int access, String name, String descriptor, String signature, String[] exceptions) {
                MethodVisitor mv = super.visitMethod(access, name, descriptor, signature, exceptions);
                if ("initialize".equals(name) && "()V".equals(descriptor)) {
                    return new MethodVisitor(Opcodes.ASM9, mv) {
                        @Override
                        public void visitInsn(int opcode) {
                            if (opcode == Opcodes.RETURN) {
                                super.visitMethodInsn(Opcodes.INVOKESTATIC, REGISTRY, "register", "()V", false);
                            }
                            super.visitInsn(opcode);
                        }
                    };
                }
                if ("add".equals(name) && "(Lcom/megacrit/cardcrawl/cards/AbstractCard;)V".equals(descriptor)) {
                    return new MethodVisitor(Opcodes.ASM9, mv) {
                        @Override
                        public void visitCode() {
                            super.visitCode();
                            super.visitVarInsn(Opcodes.ALOAD, 0);
                            super.visitMethodInsn(Opcodes.INVOKESTATIC, REGISTRY, "applyPrototype",
                                    "(Lcom/megacrit/cardcrawl/cards/AbstractCard;)V", false);
                        }
                    };
                }
                if ("getCopy".equals(name) && descriptor.startsWith("(Ljava/lang/String;")) {
                    return new MethodVisitor(Opcodes.ASM9, mv) {
                        @Override
                        public void visitInsn(int opcode) {
                            if (opcode == Opcodes.ARETURN) {
                                super.visitInsn(Opcodes.DUP);
                                super.visitMethodInsn(Opcodes.INVOKESTATIC, REGISTRY, "applyCopy",
                                        "(Lcom/megacrit/cardcrawl/cards/AbstractCard;)V", false);
                            }
                            super.visitInsn(opcode);
                        }
                    };
                }
                return mv;
            }
        };
        reader.accept(cv, 0);
        return writer.toByteArray();
    }

    private static byte[] patchAbstractCard(byte[] bytes) {
        ClassReader reader = new ClassReader(bytes);
        ClassWriter writer = new ClassWriter(reader, ClassWriter.COMPUTE_MAXS);
        ClassVisitor cv = new ClassVisitor(Opcodes.ASM9, writer) {
            @Override
            public MethodVisitor visitMethod(int access, String name, String descriptor, String signature, String[] exceptions) {
                MethodVisitor mv = super.visitMethod(access, name, descriptor, signature, exceptions);
                if (("upgradeDamage".equals(name) && "(I)V".equals(descriptor))
                        || ("upgradeBlock".equals(name) && "(I)V".equals(descriptor))
                        || ("upgradeMagicNumber".equals(name) && "(I)V".equals(descriptor))
                        || ("upgradeBaseCost".equals(name) && "(I)V".equals(descriptor))) {
                    final String callback;
                    if ("upgradeDamage".equals(name)) {
                        callback = "afterUpgradeDamage";
                    } else if ("upgradeBlock".equals(name)) {
                        callback = "afterUpgradeBlock";
                    } else if ("upgradeMagicNumber".equals(name)) {
                        callback = "afterUpgradeMagicNumber";
                    } else {
                        callback = "afterUpgradeBaseCost";
                    }
                    return new MethodVisitor(Opcodes.ASM9, mv) {
                        @Override
                        public void visitInsn(int opcode) {
                            if (opcode == Opcodes.RETURN) {
                                super.visitVarInsn(Opcodes.ALOAD, 0);
                                super.visitMethodInsn(Opcodes.INVOKESTATIC, REGISTRY, callback,
                                        "(Lcom/megacrit/cardcrawl/cards/AbstractCard;)V", false);
                            }
                            super.visitInsn(opcode);
                        }
                    };
                }
                if (("applyPowers".equals(name) && "()V".equals(descriptor))
                        || ("calculateCardDamage".equals(name)
                        && "(Lcom/megacrit/cardcrawl/monsters/AbstractMonster;)V".equals(descriptor))) {
                    return new MethodVisitor(Opcodes.ASM9, mv) {
                        @Override
                        public void visitCode() {
                            super.visitCode();
                            super.visitVarInsn(Opcodes.ALOAD, 0);
                            super.visitMethodInsn(Opcodes.INVOKESTATIC, REGISTRY, "applyLiveCard",
                                    "(Lcom/megacrit/cardcrawl/cards/AbstractCard;)V", false);
                        }
                    };
                }
                return mv;
            }
        };
        reader.accept(cv, 0);
        return writer.toByteArray();
    }

    private static byte[] patchCardCrawlGame(byte[] bytes) {
        ClassReader reader = new ClassReader(bytes);
        ClassWriter writer = new ClassWriter(reader, ClassWriter.COMPUTE_MAXS);
        ClassVisitor cv = new ClassVisitor(Opcodes.ASM9, writer) {
            @Override
            public MethodVisitor visitMethod(int access, String name, String descriptor, String signature, String[] exceptions) {
                MethodVisitor mv = super.visitMethod(access, name, descriptor, signature, exceptions);
                if ("render".equals(name) && "()V".equals(descriptor)) {
                    return new MethodVisitor(Opcodes.ASM9, mv) {
                        @Override
                        public void visitCode() {
                            super.visitCode();
                            super.visitMethodInsn(Opcodes.INVOKESTATIC, BRIDGE, "tick", "()V", false);
                        }
                    };
                }
                return mv;
            }
        };
        reader.accept(cv, 0);
        return writer.toByteArray();
    }

    private static byte[] patchDialogButton(byte[] bytes) {
        ClassReader reader = new ClassReader(bytes);
        ClassWriter writer = new ClassWriter(reader, ClassWriter.COMPUTE_MAXS);
        ClassVisitor cv = new ClassVisitor(Opcodes.ASM9, writer) {
            @Override
            public MethodVisitor visitMethod(int access, String name, String descriptor, String signature, String[] exceptions) {
                MethodVisitor mv = super.visitMethod(access, name, descriptor, signature, exceptions);
                if ("update".equals(name) && "(I)V".equals(descriptor)) {
                    return new MethodVisitor(Opcodes.ASM9, mv) {
                        @Override
                        public void visitCode() {
                            super.visitCode();
                            super.visitVarInsn(Opcodes.ALOAD, 0);
                            super.visitMethodInsn(Opcodes.INVOKESTATIC, REGISTRY, "debugDialog",
                                    "(Lcom/megacrit/cardcrawl/ui/buttons/LargeDialogOptionButton;)V", false);
                        }
                    };
                }
                return mv;
            }
        };
        reader.accept(cv, 0);
        return writer.toByteArray();
    }

    private static byte[] patchDungeon(byte[] bytes) {
        ClassReader reader = new ClassReader(bytes);
        ClassWriter writer = new ClassWriter(reader, ClassWriter.COMPUTE_MAXS);
        final int[] matches = new int[4];
        reader.accept(new ClassVisitor(Opcodes.ASM9, writer) {
            @Override public MethodVisitor visitMethod(int access, final String name, final String descriptor, String signature, String[] exceptions) {
                MethodVisitor mv = super.visitMethod(access, name, descriptor, signature, exceptions);
                if (("getRewardCards".equals(name) || "getColorlessRewardCards".equals(name)) && "()Ljava/util/ArrayList;".equals(descriptor)) {
                    matches[0]++;
                    return new MethodVisitor(Opcodes.ASM9, mv) {
                        private int iterators;
                        @Override public void visitMethodInsn(int opcode, String owner, String method, String desc, boolean itf) {
                            // The fourth iterator walks final copies for upgrade/egg previews.
                            if ("getRewardCards".equals(name) && "java/util/ArrayList".equals(owner) && "iterator".equals(method) && ++iterators == 4) {
                                super.visitMethodInsn(Opcodes.INVOKESTATIC, POOL, "rewards", "(Ljava/util/ArrayList;)Ljava/util/ArrayList;", false);
                                super.visitInsn(Opcodes.DUP); super.visitVarInsn(Opcodes.ASTORE, 2); matches[3]++;
                            }
                            super.visitMethodInsn(opcode, owner, method, desc, itf);
                        }
                        @Override public void visitInsn(int opcode) {
                            if (opcode == Opcodes.ARETURN && "getColorlessRewardCards".equals(name)) super.visitMethodInsn(Opcodes.INVOKESTATIC, POOL, "rewards", "(Ljava/util/ArrayList;)Ljava/util/ArrayList;", false);
                            super.visitInsn(opcode);
                        }
                    };
                }
                if ("<init>".equals(name)) {
                    final String hook;
                    if (descriptor.equals("(Ljava/lang/String;Ljava/lang/String;Lcom/megacrit/cardcrawl/characters/AbstractPlayer;Ljava/util/ArrayList;)V")) { matches[1]++; hook = "onNewDungeon"; }
                    else if (descriptor.equals("(Ljava/lang/String;Lcom/megacrit/cardcrawl/characters/AbstractPlayer;Lcom/megacrit/cardcrawl/saveAndContinue/SaveFile;)V")) { matches[2]++; hook = "onLoadDungeon"; }
                    else return mv;
                    return new MethodVisitor(Opcodes.ASM9, mv) {
                        @Override public void visitInsn(int opcode) {
                            if (opcode == Opcodes.RETURN) super.visitMethodInsn(Opcodes.INVOKESTATIC, POOL, hook, "()V", false);
                            super.visitInsn(opcode);
                        }
                    };
                }
                return mv;
            }
        }, 0);
        if (matches[0] != 2 || matches[1] != 1 || matches[2] != 1 || matches[3] != 1) throw new IllegalArgumentException("unsupported dungeon reward/run lifecycle signatures");
        return writer.toByteArray();
    }

    private static byte[] patchShop(byte[] bytes) {
        ClassReader reader = new ClassReader(bytes);
        ClassWriter writer = new ClassWriter(reader, ClassWriter.COMPUTE_MAXS);
        final int[] matches = new int[3];
        reader.accept(new ClassVisitor(Opcodes.ASM9, writer) {
            @Override public MethodVisitor visitMethod(int access, String name, String descriptor, String signature, String[] exceptions) {
                MethodVisitor mv = super.visitMethod(access, name, descriptor, signature, exceptions);
                if ("init".equals(name) && "(Ljava/util/ArrayList;Ljava/util/ArrayList;)V".equals(descriptor)) {
                    matches[0]++;
                    return new MethodVisitor(Opcodes.ASM9, mv) {
                        @Override public void visitCode() {
                            super.visitCode();
                            for (int i = 1; i <= 2; i++) {
                                super.visitVarInsn(Opcodes.ALOAD, i);
                                super.visitMethodInsn(Opcodes.INVOKESTATIC, POOL, "shopOffers", "(Ljava/util/ArrayList;)Ljava/util/ArrayList;", false);
                                super.visitVarInsn(Opcodes.ASTORE, i);
                            }
                        }
                    };
                }
                if ("purchaseCard".equals(name) && ("(L" + CARD + ";)V").equals(descriptor)) {
                    matches[1]++;
                    return new MethodVisitor(Opcodes.ASM9, mv) {
                        private boolean firstColor = true;
                        @Override public void visitFieldInsn(int opcode, String owner, String field, String desc) {
                            if (firstColor && opcode == Opcodes.GETFIELD && CARD.equals(owner) && "color".equals(field)) {
                                firstColor = false; matches[2]++;
                                super.visitVarInsn(Opcodes.ALOAD, 0);
                                super.visitMethodInsn(Opcodes.INVOKESTATIC, POOL, "slotColor", "(L" + CARD + ";L" + SHOP + ";)L" + CARD + "$CardColor;", false);
                            } else super.visitFieldInsn(opcode, owner, field, desc);
                        }
                        @Override public void visitInsn(int opcode) {
                            if (opcode == Opcodes.RETURN) {
                                super.visitVarInsn(Opcodes.ALOAD, 0);
                                super.visitMethodInsn(Opcodes.INVOKESTATIC, POOL, "afterPurchase", "(L" + SHOP + ";)V", false);
                            }
                            super.visitInsn(opcode);
                        }
                    };
                }
                if ("initCards".equals(name) || "setPrice".equals(name)) {
                    return new MethodVisitor(Opcodes.ASM9, mv) {
                        @Override public void visitMethodInsn(int opcode, String owner, String method, String desc, boolean itf) {
                            if (opcode == Opcodes.INVOKESTATIC && CARD.equals(owner) && "getPrice".equals(method))
                                super.visitMethodInsn(opcode, POOL, "shopBasePrice", desc, false);
                            else super.visitMethodInsn(opcode, owner, method, desc, itf);
                        }
                    };
                }
                return mv;
            }
        }, 0);
        if (matches[0] != 1 || matches[1] != 1 || matches[2] != 1) throw new IllegalArgumentException("unsupported merchant card signatures");
        return writer.toByteArray();
    }

    private static byte[] readEntry(File jar, String name) throws Exception {
        try (JarFile jf = new JarFile(jar)) {
            JarEntry entry = jf.getJarEntry(name);
            if (entry == null) {
                throw new IllegalArgumentException("entry not found: " + name + " in " + jar);
            }
            try (InputStream in = jf.getInputStream(entry)) {
                return readAll(in);
            }
        }
    }

    private static void put(JarOutputStream out, String name, byte[] data) throws Exception {
        out.putNextEntry(new JarEntry(name));
        out.write(data);
        out.closeEntry();
    }

    private static void addTree(JarOutputStream out, File root, File file) throws Exception {
        if (file.isDirectory()) {
            File[] children = file.listFiles();
            if (children != null) {
                for (File child : children) {
                    addTree(out, root, child);
                }
            }
            return;
        }
        String name = root.toURI().relativize(file.toURI()).getPath();
        out.putNextEntry(new JarEntry(name));
        try (InputStream in = new FileInputStream(file)) {
            byte[] buffer = new byte[8192];
            int n;
            while ((n = in.read(buffer)) > 0) {
                out.write(buffer, 0, n);
            }
        }
        out.closeEntry();
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

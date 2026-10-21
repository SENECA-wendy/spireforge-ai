package com.stseditor.patcher;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.util.jar.JarEntry;
import java.util.jar.JarFile;
import java.util.jar.JarOutputStream;
import org.objectweb.asm.ClassReader;
import org.objectweb.asm.ClassVisitor;
import org.objectweb.asm.ClassWriter;
import org.objectweb.asm.MethodVisitor;
import org.objectweb.asm.Opcodes;

/** One-off smoke test: inject Probe.mark at the end of CardLibrary.initialize(). */
public final class PatchProbe {
    public static void main(String[] args) throws Exception {
        File gameJar = new File(args[0]);
        File runtimeDir = new File(args[1]);
        File outJar = new File(args[2]);

        byte[] original;
        try (JarFile jar = new JarFile(gameJar)) {
            JarEntry entry = jar.getJarEntry("com/megacrit/cardcrawl/helpers/CardLibrary.class");
            try (InputStream in = jar.getInputStream(entry)) {
                original = readAll(in);
            }
        }

        ClassReader reader = new ClassReader(original);
        ClassWriter writer = new ClassWriter(reader, ClassWriter.COMPUTE_MAXS);
        ClassVisitor visitor = new ClassVisitor(Opcodes.ASM9, writer) {
            @Override
            public MethodVisitor visitMethod(int access, String name, String descriptor, String signature, String[] exceptions) {
                MethodVisitor mv = super.visitMethod(access, name, descriptor, signature, exceptions);
                if (!"initialize".equals(name) || !"()V".equals(descriptor)) {
                    return mv;
                }
                return new MethodVisitor(Opcodes.ASM9, mv) {
                    @Override
                    public void visitInsn(int opcode) {
                        if (opcode == Opcodes.RETURN) {
                            super.visitLdcInsn("CardLibrary.initialize");
                            super.visitMethodInsn(Opcodes.INVOKESTATIC, "com/stseditor/runtime/Probe", "mark", "(Ljava/lang/String;)V", false);
                        }
                        super.visitInsn(opcode);
                    }
                };
            }
        };
        reader.accept(visitor, 0);
        byte[] patched = writer.toByteArray();

        try (JarOutputStream out = new JarOutputStream(new FileOutputStream(outJar))) {
            out.putNextEntry(new JarEntry("com/megacrit/cardcrawl/helpers/CardLibrary.class"));
            out.write(patched);
            out.closeEntry();
            addTree(out, runtimeDir, runtimeDir);
        }
        System.out.println("wrote " + outJar.getAbsolutePath());
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
        try (InputStream in = new java.io.FileInputStream(file)) {
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
}

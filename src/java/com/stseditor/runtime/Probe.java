package com.stseditor.runtime;

import java.io.File;
import java.io.FileWriter;
import java.io.PrintWriter;

/** Temporary smoke-test probe used while validating class shadowing. */
public final class Probe {
    private Probe() {
    }

    public static void mark(String what) {
        try (PrintWriter out = new PrintWriter(new FileWriter(new File(System.getProperty("user.dir"), "sts-card-editor-probe.txt"), true))) {
            out.println(what + " " + System.currentTimeMillis());
        } catch (Throwable ignored) {
        }
    }
}

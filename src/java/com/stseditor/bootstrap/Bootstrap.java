package com.stseditor.bootstrap;

import java.io.File;
import java.io.FileWriter;
import java.io.PrintWriter;

/**
 * Steam's normal java -jar launcher wrapper delegates to the original game.
 * Its manifest loads the local adapter before desktop-1.0-original.jar.
 */
public final class Bootstrap {
    private Bootstrap() {
    }

    public static void main(String[] args) throws Exception {
        File marker = new File(System.getProperty("user.dir"), "sts-card-editor-bootstrap.txt");
        try (PrintWriter out = new PrintWriter(new FileWriter(marker, true))) {
            out.println("bootstrap-ok " + System.currentTimeMillis() + " cwd=" + System.getProperty("user.dir"));
        }
        com.megacrit.cardcrawl.desktop.DesktopLauncher.main(args);
    }
}

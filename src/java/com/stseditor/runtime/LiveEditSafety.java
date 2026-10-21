package com.stseditor.runtime;

/** Only the route map may be left open for edits to the permanent run deck. */
public final class LiveEditSafety {
    public static boolean overlayAllowed(boolean screenUp, String screen, boolean screenSwap, boolean combat, boolean permanentEdit) {
        if (screenSwap) return false;
        return !screenUp || (permanentEdit && !combat && "MAP".equals(screen));
    }
}

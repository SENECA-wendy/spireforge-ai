package com.stseditor.runtime;
import java.util.*;
import com.megacrit.cardcrawl.helpers.CardLibrary;
public class ContentRegistry {
    public static List<String> validateSpec(CardSpec spec) { return new ArrayList<String>(); }
    public static void updateCustomDefinition(CardSpec spec) { CardLibrary.cards.put(spec.id, new CustomCard(spec)); }
    public static void log(String message) { }
}

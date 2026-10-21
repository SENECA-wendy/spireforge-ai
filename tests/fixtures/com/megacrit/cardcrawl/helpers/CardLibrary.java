package com.megacrit.cardcrawl.helpers;
import java.util.HashMap;
import com.megacrit.cardcrawl.cards.AbstractCard;
public class CardLibrary {
    public static HashMap<String, AbstractCard> cards = new HashMap<String, AbstractCard>();
    public static AbstractCard getCard(String id) { return cards.get(id); }
    public static AbstractCard getCopy(String id, int upgrades, int misc) {
        AbstractCard c = cards.get(id).makeCopy(); c.misc = misc;
        for (int i = 0; i < upgrades; i++) c.upgrade();
        return c;
    }
}

package com.megacrit.cardcrawl.cards;
import java.util.ArrayList;
public class CardGroup {
    public ArrayList<AbstractCard> group = new ArrayList<AbstractCard>();
    public boolean failRefresh;
    public int size() { return group.size(); }
    public void refreshHandLayout() { if (failRefresh) throw new IllegalStateException("simulated layout failure"); }
    public void applyPowers() { }
}

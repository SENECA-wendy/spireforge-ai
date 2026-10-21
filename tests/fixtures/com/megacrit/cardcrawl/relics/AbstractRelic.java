package com.megacrit.cardcrawl.relics;
public class AbstractRelic {
    public int notifications;
    public void onMasterDeckChange() { notifications++; }
    public void onPreviewObtainCard(com.megacrit.cardcrawl.cards.AbstractCard card) { }
}

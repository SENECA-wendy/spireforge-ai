package com.stseditor.runtime;

import com.google.gson.JsonObject;
import com.megacrit.cardcrawl.characters.AbstractPlayer;
import com.megacrit.cardcrawl.monsters.AbstractMonster;

public final class CardExtensionContext {
    public final CustomCard card;
    public final AbstractPlayer player;
    public final AbstractMonster target;
    public final int amount;
    public final JsonObject parameters;
    public CardExtensionContext(CustomCard card, AbstractPlayer player, AbstractMonster target, int amount, JsonObject parameters) {
        this.card = card; this.player = player; this.target = target; this.amount = amount;
        this.parameters = parameters == null ? new JsonObject() : parameters;
    }
}

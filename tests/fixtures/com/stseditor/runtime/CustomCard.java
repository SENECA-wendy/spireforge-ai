package com.stseditor.runtime;
import com.megacrit.cardcrawl.cards.AbstractCard;
public class CustomCard extends AbstractCard {
    private final CardSpec spec;
    public CustomCard(CardSpec spec) { super(spec.id); this.spec = spec; }
    public CardSpec getSpec() { return spec; }
    public AbstractCard makeCopy() { return new CustomCard(spec); }
}

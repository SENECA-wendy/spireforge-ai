package com.stseditor.runtime;

import com.megacrit.cardcrawl.actions.AbstractGameAction;
import com.megacrit.cardcrawl.actions.common.ApplyPowerAction;
import com.megacrit.cardcrawl.actions.common.DamageAction;
import com.megacrit.cardcrawl.actions.common.DamageAllEnemiesAction;
import com.megacrit.cardcrawl.actions.common.DiscardAction;
import com.megacrit.cardcrawl.actions.common.DrawCardAction;
import com.megacrit.cardcrawl.actions.common.ExhaustAction;
import com.megacrit.cardcrawl.actions.common.GainBlockAction;
import com.megacrit.cardcrawl.actions.common.GainEnergyAction;
import com.megacrit.cardcrawl.actions.common.HealAction;
import com.megacrit.cardcrawl.actions.common.LoseHPAction;
import com.megacrit.cardcrawl.actions.common.MakeTempCardInDiscardAction;
import com.megacrit.cardcrawl.actions.common.MakeTempCardInDrawPileAction;
import com.megacrit.cardcrawl.actions.common.MakeTempCardInHandAction;
import com.megacrit.cardcrawl.cards.AbstractCard;
import com.megacrit.cardcrawl.cards.DamageInfo;
import com.megacrit.cardcrawl.characters.AbstractPlayer;
import com.megacrit.cardcrawl.core.AbstractCreature;
import com.megacrit.cardcrawl.dungeons.AbstractDungeon;
import com.megacrit.cardcrawl.helpers.CardLibrary;
import com.megacrit.cardcrawl.monsters.AbstractMonster;
import com.megacrit.cardcrawl.powers.AbstractPower;
import com.megacrit.cardcrawl.powers.DexterityPower;
import com.megacrit.cardcrawl.powers.FrailPower;
import com.megacrit.cardcrawl.powers.PoisonPower;
import com.megacrit.cardcrawl.powers.StrengthPower;
import com.megacrit.cardcrawl.powers.ThornsPower;
import com.megacrit.cardcrawl.powers.VulnerablePower;
import com.megacrit.cardcrawl.powers.WeakPower;
import com.megacrit.cardcrawl.rooms.AbstractRoom;
import java.util.ArrayList;
import java.util.List;

/** A data-driven card whose behaviour is limited to the supported effect whitelist. */
public final class CustomCard extends AbstractCard {
    private volatile CardSpec spec;

    public CustomCard(CardSpec spec) {
        super(spec.id, spec.name, spec.image, spec.cost, spec.description,
                CardType.valueOf(spec.type), CardColor.valueOf(spec.color),
                CardRarity.valueOf(spec.rarity), CardTarget.valueOf(spec.target));
        this.spec = spec;
        this.exhaust = spec.exhaust;
        this.isInnate = spec.innate;
        this.isEthereal = spec.ethereal;
        this.selfRetain = spec.retain;
        this.isMultiDamage = hasAoeDamage(spec);
        applySpec(false);
    }

    public CardSpec getSpec() {
        return this.spec;
    }

    /**
     * Applies a new definition to this concrete instance. The method is called
     * on the game thread from RuntimeBridge, never from the socket thread.
     */
    public void applySpecLive(CardSpec newSpec) {
        if (newSpec == null || this.cardID == null || !this.cardID.equals(newSpec.id)) {
            throw new IllegalArgumentException("card id mismatch");
        }
        int oldCost = this.cost;
        int oldCostForTurn = this.costForTurn;
        this.spec = newSpec;
        this.originalName = newSpec.name;
        this.name = newSpec.name + (this.upgraded ? "+" : "");
        try { this.initializeTitle(); } catch (Throwable ignored) { }
        this.type = CardType.valueOf(newSpec.type);
        this.color = CardColor.valueOf(newSpec.color);
        this.rarity = CardRarity.valueOf(newSpec.rarity);
        this.target = CardTarget.valueOf(newSpec.target);
        this.exhaust = newSpec.exhaust;
        this.isInnate = newSpec.innate;
        this.isEthereal = newSpec.ethereal;
        this.selfRetain = newSpec.retain;
        this.isMultiDamage = hasAoeDamage(newSpec);
        int newCost = this.upgraded && newSpec.upgradeCost != null ? newSpec.upgradeCost.intValue() : newSpec.cost;
        this.cost = newCost;
        this.costForTurn = newCost + (oldCostForTurn - oldCost);
        this.upgradedCost = this.upgraded && newSpec.upgradeCost != null;
        this.baseDamage = valueFor(newSpec.damage, newSpec.upgradeDamage, this.upgraded);
        this.baseBlock = valueFor(newSpec.block, newSpec.upgradeBlock, this.upgraded);
        this.baseMagicNumber = valueFor(newSpec.magicNumber, newSpec.upgradeMagicNumber, this.upgraded);
        this.rawDescription = this.upgraded && newSpec.upgradeDescription != null
                ? newSpec.upgradeDescription : newSpec.description;
        this.initializeDescription();
        refreshAfterDefinitionChange();
        ContentRegistry.log("CustomCard.applySpecLive id=" + this.cardID + " upgraded=" + this.upgraded
                + " cost=" + this.cost + " damage=" + this.damage + " block=" + this.block);
    }

    private static int valueFor(Integer base, Integer upgradedValue, boolean upgraded) {
        if (upgraded && upgradedValue != null) return upgradedValue.intValue();
        return base == null ? -1 : base.intValue();
    }

    private void refreshAfterDefinitionChange() {
        try {
            if (AbstractDungeon.player != null && AbstractDungeon.getCurrRoom() != null
                    && AbstractDungeon.getCurrRoom().phase == AbstractRoom.RoomPhase.COMBAT) {
                this.applyPowers();
            } else {
                this.damage = this.baseDamage;
                this.block = this.baseBlock;
                this.magicNumber = this.baseMagicNumber;
            }
            if (AbstractDungeon.player != null && AbstractDungeon.player.hand != null
                    && AbstractDungeon.player.hand.contains(this)) {
                AbstractDungeon.player.hand.refreshHandLayout();
            }
        } catch (Throwable t) {
            ContentRegistry.log("CustomCard.refreshAfterDefinitionChange failed: " + t);
        }
    }

    private static boolean hasAoeDamage(CardSpec spec) {
        for (CardSpec.EffectSpec e : spec.effects) {
            if ("DAMAGE".equals(e.type) && ("ALL_ENEMY".equals(e.target) || "ALL".equals(e.target))) {
                return true;
            }
        }
        return false;
    }

    private void applySpec(boolean up) {
        Integer d = up && spec.upgradeDamage != null ? spec.upgradeDamage : spec.damage;
        if (d != null) {
            this.baseDamage = d.intValue();
            this.damage = this.baseDamage;
        }
        Integer b = up && spec.upgradeBlock != null ? spec.upgradeBlock : spec.block;
        if (b != null) {
            this.baseBlock = b.intValue();
            this.block = this.baseBlock;
        }
        Integer m = up && spec.upgradeMagicNumber != null ? spec.upgradeMagicNumber : spec.magicNumber;
        if (m != null) {
            this.baseMagicNumber = m.intValue();
            this.magicNumber = this.baseMagicNumber;
        }
        if (up && spec.upgradeCost != null) {
            this.cost = spec.upgradeCost.intValue();
            this.costForTurn = this.cost;
            this.upgradedCost = true;
        }
        String text = up && spec.upgradeDescription != null ? spec.upgradeDescription : spec.description;
        this.rawDescription = text;
    }

    @Override
    public void use(AbstractPlayer p, AbstractMonster m) {
        // Snapshot the definition for this resolution. A hot update that arrives
        // while this card is being resolved must not change half of its effects.
        final CardSpec useSpec = this.spec;
        ContentRegistry.log("CustomCard.use id=" + this.cardID + " upgraded=" + this.upgraded
                + " damage=" + this.damage + " block=" + this.block + " magic=" + this.magicNumber
                + " effects=" + useSpec.effects.size());
        for (CardSpec.EffectSpec e : useSpec.effects) {
            if (!conditionMet(e, p)) {
                ContentRegistry.log("CustomCard.use condition skipped: " + e.type);
                continue;
            }
            int amount = useSpec.amountFor(e, this.upgraded);
            if ("DAMAGE".equals(e.type)) {
                int hits = hitsFor(e, this.upgraded);
                for (int i = 0; i < hits; i++) {
                    doDamage(e, p, m, amount);
                }
            } else if ("BLOCK".equals(e.type)) {
                int block = this.block >= 0 ? this.block : amount;
                this.addToBot(new GainBlockAction(p, p, block));
            } else if ("DRAW".equals(e.type)) {
                this.addToBot(new DrawCardAction(p, amount));
            } else if ("GAIN_ENERGY".equals(e.type)) {
                this.addToBot(new GainEnergyAction(amount));
            } else if ("LOSE_HP".equals(e.type)) {
                this.addToBot(new LoseHPAction(p, p, amount));
            } else if ("HEAL".equals(e.type)) {
                this.addToBot(new HealAction(p, p, amount));
            } else if ("DISCARD".equals(e.type) || "DISCARD_CARDS".equals(e.type)) {
                this.addToBot(new DiscardAction(p, p, amount, false));
            } else if ("EXHAUST_CARDS".equals(e.type) || "EXHAUST".equals(e.type)) {
                this.addToBot(new ExhaustAction(p, p, amount, false));
            } else if ("ADD_CARD".equals(e.type)) {
                addCard(e, amount);
            } else if ("APPLY_POWER".equals(e.type)) {
                applyPower(e, p, m, amount);
            } else if ("START_TURN_BLOCK".equals(e.type)) {
                String id = "STSEDITOR_" + this.cardID;
                String desc = "At the start of your turn, gain " + amount + " Block.";
                this.addToBot(new ApplyPowerAction(p, p,
                        new CustomPower(p, amount, id, this.name, desc, true, false), amount));
            } else if ("DRAW_ON_PLAY".equals(e.type)) {
                String id = "STSEDITOR_DRAW_" + this.cardID;
                String desc = "Whenever you play a card, draw !M! card(s).";
                this.addToBot(new ApplyPowerAction(p, p,
                        new CustomPower(p, amount, id, this.name, desc, false, true), amount));
            } else if ("ALL_CARDS_ZERO_COST".equals(e.type)) {
                this.addToBot(new ApplyPowerAction(p, p, new ZeroCostPower(p), 1));
            } else if ("JAVA_EXTENSION".equals(e.type)) {
                ExtensionRegistry.execute(e.extensionId, new CardExtensionContext(this, p, m, amount, e.parameters));
            }
        }
    }

    private static int hitsFor(CardSpec.EffectSpec e, boolean upgraded) {
        int hits = e.hits == null ? 1 : e.hits.intValue();
        if (upgraded && e.upgradeHits != null) hits = e.upgradeHits.intValue();
        if (hits < 1) hits = 1;
        if (hits > 10) hits = 10;
        return hits;
    }

    private void addCard(CardSpec.EffectSpec e, int amount) {
        if (e.cardId == null || e.cardId.length() == 0) {
            ContentRegistry.log("CustomCard.use ADD_CARD missing cardId");
            return;
        }
        int upgrade = e.cardUpgrade != null && e.cardUpgrade.booleanValue() ? 1 : 0;
        AbstractCard copy = CardLibrary.getCopy(e.cardId, upgrade, 0);
        if (copy == null) {
            ContentRegistry.log("CustomCard.use ADD_CARD unknown cardId=" + e.cardId);
            return;
        }
        int count = amount < 1 ? 1 : (amount > 10 ? 10 : amount);
        String dest = e.destination == null ? "HAND" : e.destination.toUpperCase();
        if ("DRAW".equals(dest) || "DRAW_PILE".equals(dest)) {
            this.addToBot(new MakeTempCardInDrawPileAction(copy, count, true, true, false));
        } else if ("DISCARD".equals(dest) || "DISCARD_PILE".equals(dest)) {
            this.addToBot(new MakeTempCardInDiscardAction(copy, count));
        } else {
            this.addToBot(new MakeTempCardInHandAction(copy, count));
        }
    }

    private void doDamage(CardSpec.EffectSpec e, AbstractPlayer p, AbstractMonster m, int fallback) {
        boolean all = "ALL_ENEMY".equals(e.target) || "ALL".equals(e.target);
        int damage = this.damage >= 0 ? this.damage : fallback;
        if (all) {
            int[] multi = this.multiDamage;
            if (multi == null) {
                multi = DamageInfo.createDamageMatrix(damage);
            }
            this.addToBot(new DamageAllEnemiesAction(p, multi, this.damageTypeForTurn,
                    AbstractGameAction.AttackEffect.BLUNT_HEAVY));
        } else {
            AbstractMonster target = m != null ? m : firstAliveMonster();
            if (target != null) {
                this.addToBot(new DamageAction(target,
                        new DamageInfo(p, damage, this.damageTypeForTurn),
                        AbstractGameAction.AttackEffect.SLASH_DIAGONAL));
            }
        }
    }

    private void applyPower(CardSpec.EffectSpec e, AbstractPlayer p, AbstractMonster m, int amount) {
        List<AbstractCreature> targets = targetsFor(e.target, p, m);
        for (AbstractCreature target : targets) {
            AbstractPower power = makePower(e.power, target, p, amount);
            if (power != null) {
                this.addToBot(new ApplyPowerAction(target, p, power, amount));
            } else {
                ContentRegistry.log("CustomCard.use unknown power: " + e.power);
            }
        }
    }

    private static AbstractPower makePower(String name, AbstractCreature target, AbstractPlayer source, int amount) {
        if (name == null) {
            return null;
        }
        if ("Strength".equalsIgnoreCase(name)) {
            return new StrengthPower(target, amount);
        }
        if ("Dexterity".equalsIgnoreCase(name)) {
            return new DexterityPower(target, amount);
        }
        if ("Vulnerable".equalsIgnoreCase(name)) {
            return new VulnerablePower(target, amount, false);
        }
        if ("Weak".equalsIgnoreCase(name) || "Weakened".equalsIgnoreCase(name)) {
            return new WeakPower(target, amount, false);
        }
        if ("Frail".equalsIgnoreCase(name)) {
            return new FrailPower(target, amount, false);
        }
        if ("Poison".equalsIgnoreCase(name)) {
            return new PoisonPower(target, source, amount);
        }
        if ("Thorns".equalsIgnoreCase(name)) {
            return new ThornsPower(target, amount);
        }
        return null;
    }

    private static List<AbstractCreature> targetsFor(String target, AbstractPlayer p, AbstractMonster m) {
        List<AbstractCreature> out = new ArrayList<AbstractCreature>();
        if ("SELF".equals(target) || "NONE".equals(target)) {
            out.add(p);
        } else if ("ALL_ENEMY".equals(target) || "ALL".equals(target)) {
            for (AbstractMonster monster : AbstractDungeon.getCurrRoom().monsters.monsters) {
                if (!monster.isDeadOrEscaped()) {
                    out.add(monster);
                }
            }
        } else if ("SELF_AND_ENEMY".equals(target)) {
            out.add(p);
            if (m != null) {
                out.add(m);
            }
        } else {
            if (m != null) {
                out.add(m);
            } else {
                AbstractMonster first = firstAliveMonster();
                if (first != null) {
                    out.add(first);
                }
            }
        }
        return out;
    }

    private static AbstractMonster firstAliveMonster() {
        if (AbstractDungeon.getCurrRoom() == null || AbstractDungeon.getCurrRoom().monsters == null) {
            return null;
        }
        for (AbstractMonster monster : AbstractDungeon.getCurrRoom().monsters.monsters) {
            if (!monster.isDeadOrEscaped()) {
                return monster;
            }
        }
        return null;
    }

    private static boolean conditionMet(CardSpec.EffectSpec e, AbstractPlayer p) {
        if (e.condition == null) {
            return true;
        }
        String type = CardSpec.str(e.condition, "type", "");
        if ("HAS_POWER".equalsIgnoreCase(type)) {
            String power = CardSpec.str(e.condition, "power", null);
            int min = CardSpec.integer(e.condition, "minAmount", 1);
            AbstractPower have = p.getPower(power);
            return have != null && have.amount >= min;
        }
        if ("HP_BELOW_PERCENT".equalsIgnoreCase(type)) {
            int percent = CardSpec.integer(e.condition, "percent", 50);
            return p.currentHealth * 100 <= p.maxHealth * percent;
        }
        if ("HAS_BLOCK".equalsIgnoreCase(type)) {
            int min = CardSpec.integer(e.condition, "minAmount", 1);
            return p.currentBlock >= min;
        }
        if ("HAND_COUNT_AT_LEAST".equalsIgnoreCase(type)) {
            int min = CardSpec.integer(e.condition, "minAmount", 1);
            return p.hand != null && p.hand.size() >= min;
        }
        if ("ENERGY_AT_LEAST".equalsIgnoreCase(type)) {
            int min = CardSpec.integer(e.condition, "minAmount", 1);
            return p.energy != null && p.energy.energy >= min;
        }
        if ("ENEMY_COUNT_AT_LEAST".equalsIgnoreCase(type)) {
            int min = CardSpec.integer(e.condition, "minAmount", 1);
            int count = 0;
            if (AbstractDungeon.getCurrRoom() != null && AbstractDungeon.getCurrRoom().monsters != null) {
                for (AbstractMonster monster : AbstractDungeon.getCurrRoom().monsters.monsters) {
                    if (!monster.isDeadOrEscaped()) count++;
                }
            }
            return count >= min;
        }
        ContentRegistry.log("CustomCard.use unsupported condition: " + type);
        return false;
    }

    @Override
    public void upgrade() {
        if (!this.upgraded) {
            this.upgradeName();
            applySpec(true);
            this.initializeDescription();
            ContentRegistry.log("CustomCard.upgrade id=" + this.cardID);
        }
    }

    @Override
    public AbstractCard makeCopy() {
        return new CustomCard(this.spec);
    }

    public static boolean supports(CardSpec.EffectSpec e) {
        String t = e.type;
        return "DAMAGE".equals(t) || "BLOCK".equals(t) || "DRAW".equals(t)
                || "GAIN_ENERGY".equals(t) || "LOSE_HP".equals(t) || "HEAL".equals(t)
                || "DISCARD".equals(t) || "DISCARD_CARDS".equals(t)
                || "EXHAUST_CARDS".equals(t) || "EXHAUST".equals(t)
                || "ADD_CARD".equals(t)
                || "APPLY_POWER".equals(t) || "START_TURN_BLOCK".equals(t)
                  || "DRAW_ON_PLAY".equals(t)
                  || "ALL_CARDS_ZERO_COST".equals(t) || "JAVA_EXTENSION".equals(t);
    }
}

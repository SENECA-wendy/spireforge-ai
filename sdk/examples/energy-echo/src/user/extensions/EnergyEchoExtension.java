package user.extensions;

import com.stseditor.runtime.CardExtension;
import com.stseditor.runtime.CardExtensionContext;
import com.megacrit.cardcrawl.actions.common.GainEnergyAction;
import com.megacrit.cardcrawl.dungeons.AbstractDungeon;

/** Example of dynamic code instead of a fixed numeric effect node. */
public final class EnergyEchoExtension implements CardExtension {
    @Override public void onUse(CardExtensionContext context) {
        int multiplier = context.parameters.has("multiplier") ? context.parameters.get("multiplier").getAsInt() : 1;
        int energy = Math.max(context.amount, context.player.energy.energy * multiplier);
        AbstractDungeon.actionManager.addToBottom(new GainEnergyAction(energy));
    }
}

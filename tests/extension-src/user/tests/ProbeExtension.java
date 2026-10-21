package user.tests;
import com.stseditor.runtime.*;
public final class ProbeExtension implements CardExtension {
    public void onUse(CardExtensionContext context) {
        if (context.parameters.has("fail")) throw new IllegalStateException("intentional extension test error");
        context.parameters.addProperty("executedAmount", context.amount);
    }
}

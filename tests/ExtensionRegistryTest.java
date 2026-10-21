import com.google.gson.*;
import com.stseditor.runtime.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
public class ExtensionRegistryTest {
    private static int passed;
    private static void check(boolean c, String name) { if (!c) throw new AssertionError(name); passed++; System.out.println("PASS " + name); }
    public static void main(String[] args) throws Exception {
        Path folder = Paths.get("sts-card-editor", "extensions"); Files.createDirectories(folder);
        Path jar = folder.resolve("probe.jar"); Files.copy(Paths.get(args[0]), jar, StandardCopyOption.REPLACE_EXISTING);
        StringBuilder sha = new StringBuilder(); for (byte b : MessageDigest.getInstance("SHA-256").digest(Files.readAllBytes(jar))) sha.append(String.format("%02x", b & 255));
        JsonObject manifest = new JsonObject(); manifest.addProperty("apiVersion",1); manifest.addProperty("id","test.probe"); manifest.addProperty("className","user.tests.ProbeExtension"); manifest.addProperty("jar","probe.jar"); manifest.addProperty("sha256",sha.toString()); manifest.addProperty("enabled",true);
        Files.write(folder.resolve("probe.extension.json"), manifest.toString().getBytes(StandardCharsets.UTF_8));
        check(ExtensionRegistry.validate("test.probe") == null, "explicit enabled hash-verified extension loads");
        JsonObject parameters = new JsonObject(); ExtensionRegistry.execute("test.probe", new CardExtensionContext(null,null,null,7,parameters));
        check(parameters.get("executedAmount").getAsInt() == 7, "compiled extension executes arbitrary implementation with card context");
        check(ExtensionRegistry.validate("not.installed") != null, "missing extension is rejected rather than silently replaced");
        manifest.addProperty("id","test.disabled"); manifest.addProperty("enabled",false);
        Files.write(folder.resolve("disabled.extension.json"), manifest.toString().getBytes(StandardCharsets.UTF_8));
        check(ExtensionRegistry.validate("test.disabled") != null, "disabled manifest is not executable");
        manifest.addProperty("id","test.bad_hash"); manifest.addProperty("enabled",true); manifest.addProperty("sha256","0000");
        Files.write(folder.resolve("bad-hash.extension.json"), manifest.toString().getBytes(StandardCharsets.UTF_8));
        check(ExtensionRegistry.validate("test.bad_hash").contains("hash mismatch"), "modified jar is rejected before class execution");
        parameters.addProperty("fail",true); ExtensionRegistry.execute("test.probe", new CardExtensionContext(null,null,null,1,parameters));
        check(ExtensionRegistry.describe().toString().contains("intentional extension test error"), "execution exception is contained and exposed in diagnostics");
        System.out.println("TOTAL " + passed + " compiled-extension checks passed");
    }
}

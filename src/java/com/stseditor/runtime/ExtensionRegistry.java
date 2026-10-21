package com.stseditor.runtime;

import com.google.gson.*;
import java.io.File;
import java.net.URLClassLoader;
import java.net.URL;
import java.nio.file.Files;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.*;

/** Native game API escape hatch; no source from a card or model is evaluated. */
public final class ExtensionRegistry {
    private static final Map<String, Loaded> extensions = new LinkedHashMap<String, Loaded>();
    private static final Map<String, String> errors = new LinkedHashMap<String, String>();

    private static synchronized void scan() {
        File folder = new File(new File(System.getProperty("user.dir"), "sts-card-editor"), "extensions");
        File[] manifests = folder.listFiles();
        if (manifests == null) return;
        Arrays.sort(manifests);
        for (File file : manifests) {
            if (!file.getName().endsWith(".extension.json")) continue;
            String id = file.getName();
            try {
                JsonObject manifest = new JsonParser().parse(new String(Files.readAllBytes(file.toPath()), StandardCharsets.UTF_8)).getAsJsonObject();
                if (!manifest.has("enabled") || !manifest.get("enabled").getAsBoolean()) continue;
                id = required(manifest, "id");
                if (!id.matches("[A-Za-z0-9_.-]{1,120}")) throw new IllegalArgumentException("invalid extension ID");
                if (extensions.containsKey(id)) continue; // Replacing existing code requires restart.
                if (!manifest.has("apiVersion") || manifest.get("apiVersion").getAsInt() != 1) throw new IllegalArgumentException("extension API version must be 1");
                String jarName = required(manifest, "jar");
                File jar = new File(folder, jarName).getCanonicalFile();
                if (!jar.getParentFile().equals(folder.getCanonicalFile()) || !jarName.endsWith(".jar") || !jar.isFile())
                    throw new IllegalArgumentException("jar must be a file directly inside extensions");
                String expected = required(manifest, "sha256");
                byte[] digest = MessageDigest.getInstance("SHA-256").digest(Files.readAllBytes(jar.toPath()));
                StringBuilder hash = new StringBuilder();
                for (byte b : digest) hash.append(String.format("%02x", b & 255));
                if (!hash.toString().equalsIgnoreCase(expected)) throw new IllegalArgumentException("extension jar hash mismatch");
                String className = required(manifest, "className");
                if (!className.matches("[A-Za-z_$][A-Za-z0-9_$.]*")) throw new IllegalArgumentException("invalid class name");
                URLClassLoader loader = new URLClassLoader(new URL[] { jar.toURI().toURL() }, CardExtension.class.getClassLoader());
                try {
                    Class<?> type = Class.forName(className, false, loader);
                    if (!CardExtension.class.isAssignableFrom(type)) throw new IllegalArgumentException("class must implement CardExtension");
                    Loaded loaded = new Loaded();
                    loaded.loader = loader;
                    loaded.implementation = (CardExtension)type.getDeclaredConstructor().newInstance();
                    loaded.name = manifest.has("name") ? manifest.get("name").getAsString() : id;
                    loaded.hash = hash.toString();
                    extensions.put(id, loaded); errors.remove(id);
                    ContentRegistry.log("loaded Java extension id=" + id + " class=" + className + " sha256=" + hash);
                } catch (Throwable failure) { loader.close(); throw failure; }
            } catch (Throwable failure) {
                String error = failure.getClass().getSimpleName() + ": " + failure.getMessage();
                if (!error.equals(errors.get(id))) ContentRegistry.log("Java extension load failed " + id + ": " + error);
                errors.put(id, error);
            }
        }
    }

    public static synchronized String validate(String id) {
        if (id == null || id.length() == 0) return "JAVA_EXTENSION missing extensionId";
        if (!extensions.containsKey(id)) scan();
        return extensions.containsKey(id) ? null : "extension is not installed/enabled: " + id + (errors.containsKey(id) ? "; " + errors.get(id) : "");
    }

    public static void execute(String id, CardExtensionContext context) {
        String error = validate(id);
        if (error != null) { ContentRegistry.log(error); return; }
        try { extensions.get(id).implementation.onUse(context); }
        catch (Throwable failure) {
            String message = failure.getClass().getSimpleName() + ": " + failure.getMessage();
            synchronized (ExtensionRegistry.class) { errors.put(id, message); }
            ContentRegistry.log("Java extension execution failed id=" + id + ": " + message);
        }
    }

    public static synchronized JsonArray describe() {
        JsonArray result = new JsonArray();
        for (Map.Entry<String, Loaded> item : extensions.entrySet()) {
            JsonObject entry = new JsonObject();
            entry.addProperty("id", item.getKey()); entry.addProperty("name", item.getValue().name);
            entry.addProperty("sha256", item.getValue().hash);
            if (errors.containsKey(item.getKey())) entry.addProperty("lastError", errors.get(item.getKey()));
            result.add(entry);
        }
        for (Map.Entry<String,String> item : errors.entrySet()) if (!extensions.containsKey(item.getKey())) {
            JsonObject entry = new JsonObject(); entry.addProperty("id", item.getKey()); entry.addProperty("error", item.getValue()); result.add(entry);
        }
        return result;
    }

    private static String required(JsonObject object, String key) {
        if (!object.has(key) || object.get(key).isJsonNull()) throw new IllegalArgumentException("manifest missing " + key);
        return object.get(key).getAsString();
    }
    private static final class Loaded { URLClassLoader loader; CardExtension implementation; String name; String hash; }
}

package com.stseditor.runtime;

/** Implemented by explicitly installed, compiled Java extensions. */
public interface CardExtension {
    void onUse(CardExtensionContext context) throws Exception;
}

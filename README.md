# SpireForge AI · 尖塔铸卡

> **v1.7.0 distribution status:** source preview. The clean build compiled, but Windows Defender blocked the new executable as `Trojan:MSIL/Tedy.LVN!MTB` during packaging on the development machine. A usable portable binary is not published yet; the cause has not been established. Do not disable protection to install it. The 309-check result below predates this packaging block.

**Describe a card. Forge its mechanics. Play it in Slay the Spire.**

[简体中文](README.zh-CN.md) · [Releases / status](https://github.com/SENECA-wendy/spireforge-ai/releases) · [Report an issue](https://github.com/SENECA-wendy/spireforge-ai/issues)

SpireForge AI turns natural-language ideas into custom cards for **Slay the Spire 1**. Inspect the generated rules and implementation, refine them with manual edits or another AI instruction, then bring your cards into a run. Native-card, deck, and reward/shop-pool editing are included.

Formerly STSCardEditor. Existing data folders and internal `stseditor` identifiers remain compatible.

## Features

- **AI creation and revision:** describe effects, triggers, generated cards, and upgrades; revise an existing card instead of starting over.
- **Structured effects or generated Java:** free design checks a rule contract, compilation, behavior probes, and consistency before accepting a result.
- **Editable results:** change numeric parameters, properties, names, and descriptions; compare revisions before saving under the existing card ID.
- **Live tools:** edit the current deck on the map and at supported safe points in combat; manage deck presets and future reward/shop card pools.
- **Four characters:** 300 native cards across Ironclad, Silent, Defect, and Watcher, with base/upgraded descriptions, character filters, and multi-selection.
- **Chinese / English interface:** switch instantly using **Language** at the top right. Unsaved inputs and selections remain; the preference survives restart.

UI language does not change the game's language or translate your custom card names, descriptions, or prompts. English native-card descriptions are supplied separately.

## Quick start

1. Download and extract the complete portable ZIP from [Releases](https://github.com/SENECA-wendy/spireforge-ai/releases). Keep its EXE, `tools`, `sdk`, catalogs, and `data` together.
2. Run **SpireForgeAI-v1.7.0.exe** on Windows 10/11 with .NET Framework 4.8. Select your own Steam installation of Slay the Spire 1.
3. Exit the game for the **first adapter installation or adapter upgrade**, then install/apply through the editor. Backups are created. Launch **Play Slay the Spire** normally from Steam.
4. For AI creation, configure your model provider's compatible API endpoint, model, and key. Free Java design also requires a JDK; JDK 17 is recommended and can be detected through `JAVA_HOME`. A limited offline parser is available, primarily for Chinese structured-effect prompts.
5. Create a card, review it, synchronize definitions, then add it to the current deck or obtainable card pool.

Example:

> Create a 1-energy Silent skill named Spare Blades. Add exactly 3 temporary 0-cost attack cards to my hand. Each deals 4 damage and exhausts. Upgrading the parent creates 4 cards. Keep the displayed description consistent with these numbers.

Your model provider may charge for requests. Generation can involve several planning, validation, and repair requests. No API key or model subscription is included. Saved keys use Windows DPAPI.

## Live-editing boundaries

| Action | Behavior |
| --- | --- |
| Native numeric edit | Updates supported fields on live instances; special native mechanics can recalculate values. Read the mechanism notes. |
| Current deck editing | Available on the map and at supported combat safe points; transitions or selection screens can defer/reject requests. |
| New custom definition | Can synchronize without restarting when its mechanics are supported by the installed adapter. |
| Replace loaded Java code | A restart can be required for an already-loaded extension with the same ID. |
| Reward/shop pool | Controls cards generated afterward, not offers already on screen. A one-card pool may repeat that card. |
| Description-only edit | Changes text, not behavior. Change effect parameters or regenerate the implementation to alter mechanics. |

Free design is not unlimited engine rewriting: unsupported lifecycle hooks need adapter work. Compilation and probes cannot prove every real-run interaction; review and test generated cards in game. Existing card art is reused; image import is not implemented.

The adapter uses a local launcher wrapper and patched classes without ModTheSpire/BaseMod. It modifies the local installation; Steam file verification can replace it. Steam initialization and achievement call paths remain, but unlocking a previously locked achievement has **not** been independently verified here and is not guaranteed. Use the restore action to return to the original launcher. Keep save backups, especially before removing custom cards/extensions used by a saved run.

## Build

Requirements: Windows, PowerShell 7, .NET Framework 4.8 compiler, JDK 17, and your original installed game. Game binaries/assets are not included.

```powershell
pwsh .\build-portable.ps1 -GameDir "D:\Steam\steamapps\common\SlayTheSpire" -Jdk "C:\Java\jdk-17"
```

This verifies/downloads ASM 9.7.1, builds the editor and adapter, extracts Chinese/English catalogs from your game, and packages `dist/SpireForgeAI-v1.7.0-portable.zip`. Building does not install the adapter into the game. Editor: **1.7.0**; adapter: **1.6.0**.

After a full build, editor-only changes can use `pwsh .\build-editor.ps1`. For translations, edit `localization/en.json`, run `py -3 tools/prepare-localization.py`, then rebuild. Do not use the historical `--initialize` migration mode.

After building, run isolated integration checks:

```powershell
pwsh .\tests\run.ps1 -Jdk "C:\Java\jdk-17" -GameJar "D:\Steam\steamapps\common\SlayTheSpire\desktop-1.0.jar"
```

If the adapter is installed, pass `desktop-1.0-original.jar` instead. The local suite passed **309 checks**, including 21 localization/state-preservation checks. This is not a paid-model or complete in-game playtest.

## Source map

- `app/`: WinForms UI, model clients, validation, library, live editing.
- `src/java/`: bootstrap, patcher, runtime, extension hooks.
- `localization/`: editable English UI dictionary; Chinese is the source language.
- `tools/catalog-probe-src/`: local game catalog extraction.
- `tools/creative-probe-src/`: isolated generated-card behavior probes.
- `sdk/`: extension example and build helper.
- `tests/`: regression checks and fixtures.

Public packages start with an empty library. Personal cards, API settings, saves, generated user code, and backups are excluded. See [third-party notices](THIRD_PARTY_NOTICES.md). Unofficial project; not affiliated with Mega Crit.

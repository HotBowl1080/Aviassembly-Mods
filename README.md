# Aviassembly Mods

Mods and development tools for Aviassembly. Current downloads live on `main`, organized by project.

## Downloads

| Download | What it contains | Status |
| --- | --- | --- |
| [Flap Mod DLL](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/AviassemblyFlaps.dll) | Flaps for eight wings, plus Big Wing spoilers | v0.24 candidate; cached models and saved-aircraft pricing |
| [Editor Tools DLL](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/EditorTools/Aviassembly.EditorTools.dll) | Camera focus, precision editing, multi-selection, part hiding and propeller clearance | v0.7.0 |
| [Wing viewer ZIP](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/aviassembly-wing-preview.zip) | One HTML viewer with all 16 native wing parts | Optional preview; not a game plugin |

Download a DLL to install a mod. The viewer ZIP is for inspecting the models in a browser.

## Install

1. Close Aviassembly. The mods require BepInEx 5 installed and initialized in the game folder.
2. Back up the existing copy of the plugin you are replacing.
3. Copy the downloaded DLL into `Aviassembly/BepInEx/plugins/`. Keep only one installed copy of each plugin.
4. Restart the game. Flap Mod options are purchased and removed in each wing's settings menu. See [Flap Mod](Flaps/README.md) or [Editor Tools](EditorTools/README.md) for controls and setup.

For the viewer, extract the ZIP and open `aviassembly-wing-preview.html` in Chrome or Edge. Internet access is needed for its rendering library.

## Projects

| Folder | Contents |
| --- | --- |
| [Flaps](Flaps/) | Current combined plugin, wing viewer, source, geometry tests and model tools |
| [EditorTools](EditorTools/) | Current editor plugin, source, tests and installer |
| [WingInspector](WingInspector/) | Source-only development helper for inspecting loaded wing geometry |

Older wing viewer versions are preserved in [Flaps/archive](Flaps/archive/). Development notes and validation results are linked from each project's README.

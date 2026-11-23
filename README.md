# Aviassembly Mods

Mods and development tools for Aviassembly. Current downloads live on `main`, organized by project.

## Downloads

| Download | What it contains | Status |
| --- | --- | --- |
| [Flap Mod ZIP](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/releases/1.0.0/Flap-Mod-1.0.0.zip) | 13 flap-capable wings, Big Wing spoilers, saved purchases and controller input | 1.0.0 |
| [Editor Tools DLL](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/EditorTools/Aviassembly.EditorTools.dll) | Camera focus, precision editing, multi-selection, part hiding and propeller clearance | v0.7.15 |
| [Atmosphere DLL](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Atmosphere/Aviassembly.Atmosphere.dll) | Dynamic weather, flight instruments, terrain checked approaches, climb and turn guidance, and optional landing scores | v0.26.1 |
| [Weather Edition DLL](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/AtmosphereWeather/Aviassembly.AtmosphereWeather.dll) | Dynamic weather through Marginal VFR, day and night cycle, runway lights, PAPI, and precise altitude | v1.0.16 |
| [BackCountry Operations DLL](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/BackCountryOperations/BackCountryOperations.dll) | Forest Island earthquake recovery, damaged runway handling, surface upgrades and airfield repairs | v0.29.1 |
| [Performance 240 DLL](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Performance240/Aviassembly.Performance240.dll) | Adaptive rendering settings for a 240 FPS target | v1.0.1 |
| [Yaw Fix DLL](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/YawFix/YawFix.dll) | Centers the rudder immediately when yaw input is released | v1.0.1 |
| [Wing viewer ZIP](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/aviassembly-wing-preview.zip) | One HTML viewer with all 16 native wing parts | Optional preview; not a game plugin |

For Flap Mod, download the mod ZIP and follow its included install instructions. Other projects offer DLL downloads. The viewer ZIP is for inspecting the models in a browser.

The earlier [Flap Mod 0.23.1 ZIP](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/releases/0.23.1/Flap_Mod_0.23.1.zip) is also available.

## Install

1. Close Aviassembly. The mods require BepInEx 5 installed and initialized in the game folder.
2. Back up the existing copy of the plugin you are replacing.
3. Copy the downloaded DLL into `Aviassembly/BepInEx/plugins/`. Keep only one installed copy of each plugin.
4. Restart the game. Flap Mod options are purchased and removed in each wing's settings menu. See [Flap Mod](Flaps/README.md) or [Editor Tools](EditorTools/README.md) for controls and setup.

For the viewer, extract the ZIP and open `aviassembly-wing-preview.html` in Chrome or Edge. The viewer works offline.

## Projects

| Folder | Contents |
| --- | --- |
| [Flaps](Flaps/) | Current combined plugin, wing viewer, source, geometry tests and model tools |
| [EditorTools](EditorTools/) | Editor plugin, matching source and tests, plus the [Editor Tools changelog](EditorTools/CHANGELOG.md) |
| [Atmosphere](Atmosphere/) | Weather and HUD plugin download and checksum |
| [Weather Edition](AtmosphereWeather/) | Standalone weather and airport lighting plugin download and checksum |
| [BackCountry Operations](BackCountryOperations/) | Forest Island career mod, installation instructions and checksum |
| [Performance 240](Performance240/) | Performance plugin download and checksum |
| [Yaw Fix](YawFix/) | Rudder release plugin, source and installation instructions |
| [WingInspector](WingInspector/) | Source-only development helper for inspecting loaded wing geometry |

Older wing viewer versions are preserved in [Flaps/archive](Flaps/archive/). Development notes and validation results are linked from each project's README.

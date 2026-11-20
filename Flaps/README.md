# Flap Mod 1.0.0 for Aviassembly

[Download Flap-Mod-1.0.0.zip](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/releases/1.0.0/Flap-Mod-1.0.0.zip) · [Optional offline wing viewer](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/aviassembly-wing-preview.zip)

## Install or update

1. Close Aviassembly. This Windows build requires BepInEx 5 for the Mono game; launch the game once with BepInEx installed.
2. If updating, back up your campaign, aircraft designs and previous AviassemblyFlaps.dll. Put the DLL backup outside BepInEx/plugins.
3. Extract the mod ZIP. For a new install, copy its BepInEx folder into the game folder beside Aviassembly.exe. For an update, replace the existing AviassemblyFlaps.dll in its current location with the one from the ZIP. Keep exactly one copy anywhere under BepInEx/plugins.
4. Start the game. Open a supported wing's settings to buy or remove Flaps. If needed, check BepInEx/LogOutput.log for “Flap Mod 1.0.0”. To uninstall, close the game and remove that DLL. Keep a backup before opening a modded save with an older plugin.

## Wings and controls

| Parts | Upgrade |
| --- | --- |
| Big Wing | Dedicated flaps and a separate spoiler purchase. The updated model is visible before purchase. |
| Straight Wing, Wing, Glider Wing, Electron Wing, Small Jet Wing, Small Recreational Wing, Delta Wing 02 | Dedicated flap; the stock aileron remains independent. |
| Delta Wing 01, Tail Wing, Tailwing 02, Tailwing 03, Tailwing 04 | Shared surface: flap deployment is added to the stock steering angle. |
| Biplane Wing, Small Canard, Wood Wing | Stock, with no flap purchase. |

Use the lever or press **]** to extend and **[** to retract. On a controller, push the right stick down to extend or up to retract; center it before the next step. Settings are 0°, 10°, 20° and 30°. Big Wing spoilers use the game's throttle-down input at idle or reverse. No overspeed limits, damage or automatic retraction.

Each purchased option costs 25% of the wing’s original price. Removing it refunds that amount on a placed wing. Native mirror mode applies a paired change in one undo step. Campaign, design and undo records retain per-wing choices; creative mode doesn't charge. Hover over an unlocked saved design for the value of its flap and spoiler upgrades.

## What's included in 1.0

Combines the v0.25 upgrade pricing, saves, default Big Wing model and cached geometry with Rylan's controller input and shared-surface implementation. The eight dedicated wing models keep their separate control surfaces. Big Wing forces follow actual deployment, and conventional flaps share one aircraft weight calculation per physics tick. Vertical shared surfaces add drag without artificial sideways lift; the stock steering forces still operate.

The optional viewer is a separate download. Extract it and open aviassembly-wing-preview.html in Chrome or Edge: all 16 models are embedded, 13 have animated flaps, and no internet connection is needed. The 0.23.1 package is historical; don't install it alongside 1.0.

## Build and verification

Build from Flaps with ./build.ps1 -GameDir '<Aviassembly installation>'. The repository contains source, geometry tools, tests and the ZIP packager. The released DLL was built against the installed game and checked in isolated Unity players. Tests cover native aircraft loading, receipts, refunds, undo, mirrored steering, all flap detents and simulated controller input. Physical controller feel and full-flight handling on player-built aircraft have not been manually verified.

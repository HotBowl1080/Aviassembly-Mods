# Flap Mod

Eight wings support paid flaps. Big Wing always shows the updated model; its fixed center plate no longer overlaps an extra moving panel. Wing geometry is prepared once and reused on upgrade and undo.

**v0.24 in-game verification is pending.** The combined DLL compiles, and transaction/save, geometry and force checks pass. Manual campaign and menu testing is still required.

## Paid per-wing upgrades in v0.24

- Buy or remove **Flaps** from the existing wing settings menu: see the wing list below.
- **Spoilers** are a separate purchase on Big Wing. They can be used without buying flaps.
- Each option costs **25% of the unmodified wing price**. Deselecting an owned option refunds its price.
- With the editor's mirror mode on, settings apply to both wings. The pair price is shown and checked before either wing changes; the pair is one undo step.
- Choices save per wing in campaign saves, aircraft designs and undo snapshots. Reloading restores them without another charge.
- Upgraded part prices feed the game's aircraft value, placement and resale. Creative mode follows native free building.

See [paid upgrades and saving](docs/UPGRADES.md) for eligibility, pricing and verification details. No speed limits or warnings are included.

## Retained geometry and force changes from v0.22

- Straight Wing has a visible 6 mm hinge seam and 4 mm end seams, with both mesh bodies closed. Native ailerons are unchanged.
- Straight Wing added forces follow the animated flap angle and the measured flap area (about 22.6% of the stock planform).
- Added lift uses the game's balanced lift origin; drag acts through the rigidbody centre of mass and uses the native drag coefficient.
- Shared limits cap total added Straight Wing lift at 0.25 g and drag at 0.04 g across the active aircraft's wings. The stock wing forces are unchanged.
- The combined plugin display name is now **Flap Mod**. The DLL filename and plugin ID stay the same for replacement installs.

![Actual Straight Wing stowed and deployed flap geometry](docs/straight-wing-v0.22.png)

## Downloads

- **[Download AviassemblyFlaps.dll](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/AviassemblyFlaps.dll)** — the combined game plugin.
- [Download the wing viewer ZIP](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/aviassembly-wing-preview.zip) — optional browser preview with all 16 native wing parts.
- [Older viewer versions](archive/) — archived previews for reference.

The current DLL checksum is recorded in [SHA256.txt](SHA256.txt).

## Install

1. Close Aviassembly. BepInEx 5 must already be installed and initialized.
2. Back up your installed `AviassemblyFlaps.dll`.
3. Copy the downloaded DLL into `Aviassembly/BepInEx/plugins/`, replacing the existing copy. Keep only one copy installed.
4. Restart the game and open a supported wing's settings menu to purchase or remove Flaps and eligible Spoilers. The selections now save with the aircraft.

## Wing support

| Wing | Flaps | Preview |
| --- | --- | --- |
| Big Wing | Separate paid flaps/spoilers; updated stowed model without purchase | Modified model and animation |
| Straight Wing | Paid flaps; v0.22 geometry/forces retained | Actual cut mesh and C# poses, 0–30 degrees |
| Small Recreational Wing, Glider Wing, Electron Wing, Small Jet Wing, Delta Wing 02, Wing | Paid inboard flaps; native ailerons retained | Actual cut mesh and C# poses, 0–30 degrees |
| Other 8 native wing parts | Stock controls retained | Native meshes with child transforms baked in |

## Controls

Use the shared lever or `[` / `]` to select 0, 10, 20 or 30 degrees.

| Setting | Main flap angle | Big Wing slats | Big Wing leading-edge flaps |
| --- | --- | --- | --- |
| 1 | 0 degrees | Retracted | Retracted |
| 2 | 10 degrees | Retracted | Retracted |
| 3 | 20 degrees | Extended | Retracted |
| 4 | 30 degrees | Extended | Extended |

On Big Wing with the Spoilers upgrade purchased, hold Control at idle or reverse for spoilers and ground braking. The winglet and native aileron remain separate from flap motion.

Straight Wing uses one rigid inboard trailing-edge flap and retains its native aileron. Between 20 and 30 degrees, the last setting smoothly adds 25.4 mm aft and 12.7 mm down travel.

## Wing viewer

Extract the viewer ZIP, then open `aviassembly-wing-preview.html` in Chrome or Edge. Internet access is needed for the rendering library; all model data is embedded in the HTML.

Choose a wing from the dropdown, drag to rotate, scroll to zoom, and choose Whole wing or an available device view. Native ailerons are shown at neutral. Modified wings use their generated mod geometry and animation.

## Development

| Path | Purpose |
| --- | --- |
| [src](src/) | Plugin source |
| [build.ps1](build.ps1) | Build against installed game, Unity, BepInEx and Harmony assemblies |
| [tests](tests/) | Upgrade transaction/save tests, geometry runner and validation results |
| [tools](tools/) | Native wing capture and embedded viewer updates |
| [STRAIGHT-WING-DEVELOPMENT.md](STRAIGHT-WING-DEVELOPMENT.md) | Straight Wing checks and remaining in-game test steps |
| [MODEL-UPDATES.md](MODEL-UPDATES.md) | Shared viewer export and update workflow |
| [Big Wing v0.20 notes](docs/BIG-WING-v0.20.md) | Preserved geometry and validation history |
| [archive](archive/) | Older viewer ZIPs |

Build from this folder with `./build.ps1 -GameDir '<Aviassembly installation>'`. Publish matching source, compiled DLL and modified previews together.

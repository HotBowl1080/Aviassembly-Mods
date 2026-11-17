# Aviassembly Wing Flaps

This is the shared flap project for the game's wings. The v0.21 candidate DLL on this branch adds Straight Wing hinged flaps and preserves the Big Wing v0.20 geometry. It compiles against the installed game, Unity, BepInEx and Harmony assemblies. In-game testing remains pending; this candidate has not been installed in the game folder.

| Wing | Released in-game flaps | Embedded preview |
| --- | --- | --- |
| Big Wing | v0.20 behavior retained | Current modified model |
| Straight Wing | v0.21 candidate; in-game test pending | Actual cut mesh and C# poses, 0–30 degrees |
| Other 14 native Wing parts | Stock controls retained | Actual native meshes and baked child transforms |

## Straight Wing v0.21 candidate

Enable flaps on Straight Wing with the same setting used for Big Wing. Use the shared lever or `[ / ]` for 0, 10, 20 and 30 degrees. The inboard trailing edge rotates as one rigid flap. The last setting adds 25.4 mm aft and 12.7 mm down travel, smoothly introduced between 20 and 30 degrees. The native aileron remains separate. Straight Wing does not acquire Big Wing's slats, leading-edge flaps or spoilers.

The cutter checks the installed native mesh signature before modifying it. Both the flap and fixed shell are closed. Checks on actual source geometry cover 62 poses in both deployment directions, rigid motion, hinge-plane clearance, stowed closure, retraction, fixed-vertex preservation and last-setting-only translation. Those checks do not substitute for in-game testing of enablement, flight forces or aircraft loading. Finish that check before implementing the next wing.

Build with `./build.ps1 -GameDir '<Aviassembly installation>'`. To install manually, close the game, back up the existing plugin and replace `BepInEx/plugins/AviassemblyFlaps.dll` with this branch's DLL. Enablement still needs to be selected after loading an aircraft.

## Big Wing v0.20

Install AviassemblyFlaps.dll in Aviassembly/BepInEx/plugins and restart the game. Enable flaps on each Big Wing. Use the lever or [ / ].

| Setting | Main flaps | Slats | LE flaps |
| --- | --- | --- | --- |
| 1 | 0 degrees | Retracted | Retracted |
| 2 | 10 degrees | Retracted | Retracted |
| 3 | 20 degrees | Extended | Retracted |
| 4 | 30 degrees | Extended | Extended |

The LE flaps keep the broad bowed profile from v0.18. The final 50.8 mm (2 inches) of the curved free edge is trimmed away, leaving the mounting region and ribs unchanged. Full deployment now uses 115 degrees of hinge travel, 20 degrees more than v0.19. The panel remains rigid and retains the existing hinge and stowed fit.

The three slat panels follow section-specific tracks fitted against the actual fixed wing mesh, including separator and root geometry. Their aft upper rail has a roughly 12 mm opening at full extension, checked at 102 points along the span. Cross sections keep their rounded shape, with closed fixed wing noses behind the slot. The tracks now keep every section on its own span plane, so slat ends no longer drift sideways into the fixed tip collars.

Both trailing-edge flap members connect beneath the stationary cover. The main connector carries a rounded airfoil nose matching its neighboring flaps. Its previous 4 mm sag has been removed, keeping the surface straight between the flap attachment edges. Hold Control at idle or reverse for spoilers and ground braking. The winglet and native aileron remain separate from flap motion.

## Model preview

[Download the wing viewer ZIP](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/straight-wing-flaps/Flaps/aviassembly-wing-preview.zip). Extract All, then open aviassembly-wing-preview.html in Chrome or Edge. Its Wing dropdown includes all 16 native Wing prefabs found across the installed game's asset files, including tail and canard parts. All model data is embedded. No separate catalog or model files are needed. Internet access is still needed for the rendering library.

Select a wing, drag to rotate, scroll to zoom, and choose Whole wing or an available device view. The native aileron is shown at neutral. The Flaps highlight includes both moving connectors. The viewer uses the generated mod mesh for every degree of travel.

Validation: the v0.20 DLL is a targeted managed-code patch of the previously compiled build, with matching source. The new profile argument mapping was checked against the source at 109 inputs; the original profile calculation body is retained. The trim measures 1.99999999 inches along the quadratic curve. Checks against the viewer mesh confirm that the minimum stowed clearance is unchanged on both LE panels. The viewer applies the same trimmed profile and 115-degree travel, with corrected surface normals. New browser rendering and in-game runtime checks could not be run for this update.

Enablement must currently be selected again after loading an aircraft. Source is in src. Build against installed Unity, game, BepInEx and Harmony assemblies.


## Updating embedded models

The canonical viewer is aviassembly-wing-preview.html. Future wing exports replace or add entries in its embedded registry, keeping every available wing in this same file. The build helper also rebuilds the single-file download ZIP. See [MODEL-UPDATES.md](MODEL-UPDATES.md). Pending models are explicitly marked; the viewer does not substitute a guessed wing model.

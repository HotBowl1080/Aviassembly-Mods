# Big Wing Flaps v0.19.0

Install AviassemblyFlaps.dll in Aviassembly/BepInEx/plugins and restart the game. Enable flaps on each Big Wing. Use the lever or [ / ].

| Setting | Main flaps | Slats | LE flaps |
| --- | --- | --- | --- |
| 1 | 0 degrees | Retracted | Retracted |
| 2 | 10 degrees | Retracted | Retracted |
| 3 | 20 degrees | Extended | Retracted |
| 4 | 30 degrees | Extended | Extended |

The LE flaps restore the original broad bowed profile and returning lip from v0.13, reflected fore/aft at its original width and depth. Full deployment now adds 45 degrees of rotation to the v0.18 position: 95 degrees of hinge travel from the unchanged stowed pose. The curved profile, hinge, slats and trailing-flap geometry are unchanged. This same rigid curve folds into the wing and swings down about an attached hinge without morphing or sliding forward.

The three slat panels follow section-specific tracks fitted against the actual fixed wing mesh, including separator and root geometry. Their aft upper rail has a roughly 12 mm opening at full extension, checked at 102 points along the span. Cross sections keep their rounded shape, with closed fixed wing noses behind the slot. The tracks now keep every section on its own span plane, so slat ends no longer drift sideways into the fixed tip collars.

Both trailing-edge flap members connect beneath the stationary cover. The main connector carries a rounded airfoil nose matching its neighboring flaps. Its previous 4 mm sag has been removed, keeping the surface straight between the flap attachment edges. Hold Control at idle or reverse for spoilers and ground braking. The winglet and native aileron remain separate from flap motion.

## Model preview

[Download the v0.19 viewer ZIP](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/aviassembly-wing-preview-v0.19.zip). Extract All, then open aviassembly-wing-preview-v0.19.html in Chrome or Edge. Check that the heading says v0.19. Internet access is needed to load the rendering library.

Drag to rotate, scroll to zoom, and choose Whole wing, Flaps, Aileron, Slats or LE flaps. The native aileron is shown at neutral. The Flaps highlight includes both moving connectors. The viewer uses the generated mod mesh for every degree of travel.

Validation: v0.19 is an angle-only patch to the previously compiled v0.18 DLL. Its two LE rotation constants change from +/-50 to +/-95 degrees, with the plugin version updated; every other byte is preserved. The matching source uses the same values. The viewer applies the additional hinge rotation to both LE panels and their normals, verified against the 95-degree transform across 6,556 vertex/angle comparisons within 0.3 mm (mesh quantization). Prior v0.18 geometry checks covered stowed fit, slat slots, flap connections and rigid hinge motion. New browser and in-game runtime checks could not be run for this update.

Enablement must currently be selected again after loading an aircraft. Source is in src. Build against installed Unity, game, BepInEx and Harmony assemblies.

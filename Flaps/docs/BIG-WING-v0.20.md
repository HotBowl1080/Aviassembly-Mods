# Big Wing v0.20 geometry and validation history

[Back to Wing Flaps](../README.md)

These notes describe the preserved Big Wing v0.20 behavior. The current download is the combined v0.23 candidate; see the project README for its status and installation.

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

## Historical validation

the v0.20 DLL is a targeted managed-code patch of the previously compiled build, with matching source. The new profile argument mapping was checked against the source at 109 inputs; the original profile calculation body is retained. The trim measures 1.99999999 inches along the quadratic curve. Checks against the viewer mesh confirm that the minimum stowed clearance is unchanged on both LE panels. The viewer applies the same trimmed profile and 115-degree travel, with corrected surface normals. New browser rendering and in-game runtime checks could not be run for this update.

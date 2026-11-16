# Big Wing Flaps v0.18.0

Install AviassemblyFlaps.dll in Aviassembly/BepInEx/plugins and restart the game. Enable flaps on each Big Wing. Use the lever or [ / ].

| Setting | Main flaps | Slats | LE flaps |
| --- | --- | --- | --- |
| 1 | 0 degrees | Retracted | Retracted |
| 2 | 10 degrees | Retracted | Retracted |
| 3 | 20 degrees | Extended | Retracted |
| 4 | 30 degrees | Extended | Extended |

The LE flaps restore the original broad bowed profile and returning lip from v0.13, reflected fore/aft at its original width and depth. A five-degree downward mounting angle keeps the returning lip clear. This same rigid curve folds into the wing and swings down about an attached hinge without morphing or sliding forward.

The three slat panels follow section-specific tracks fitted against the actual fixed wing mesh, including separator and root geometry. Their aft upper rail has a roughly 12 mm opening at full extension, checked at 102 points along the span. Cross sections keep their rounded shape, with closed fixed wing noses behind the slot. The tracks now keep every section on its own span plane, so slat ends no longer drift sideways into the fixed tip collars.

Both trailing-edge flap members connect beneath the stationary cover. The main connector carries a rounded airfoil nose matching its neighboring flaps. Its previous 4 mm sag has been removed, keeping the surface straight between the flap attachment edges. Hold Control at idle or reverse for spoilers and ground braking. The winglet and native aileron remain separate from flap motion.

## Model preview

[Download the v0.18 viewer ZIP](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/aviassembly-wing-preview-v0.18.zip). Extract All, then open aviassembly-wing-preview-v0.18.html in Chrome or Edge. Check that the heading says v0.18. Internet access is needed to load the rendering library.

Drag to rotate, scroll to zoom, and choose Whole wing, Flaps, Aileron, Slats or LE flaps. The native aileron is shown at neutral. The Flaps highlight includes both moving connectors. The viewer uses the generated mod mesh for every degree of travel.

Validation: compiled against the installed game. Geometry checks cover rigid LE rotation, numerical landmarks from the original mirrored curve and aft curvature, stowed fit, actual fixed-surface slat clearance, non-folding tracks, preserved slat sections, rounded connector attachment, root seams, winglet preservation, both wing orientations, all deployment angles and retraction. Browser checks cover all four settings, component highlighting, native aileron visibility and narrow-screen layout. Preview lighting differs from the game; in-game visual verification is still needed.

Enablement must currently be selected again after loading an aircraft. Source is in src. Build against installed Unity, game, BepInEx and Harmony assemblies.

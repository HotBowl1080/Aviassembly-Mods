# Big Wing Flaps v0.16.0

Install AviassemblyFlaps.dll in Aviassembly/BepInEx/plugins and restart the game. Enable flaps on each Big Wing. Use the lever or [ / ].

| Setting | Flaps | Slats | Inboard LE flaps |
|---|---|---|---|
| 1 | 0 degrees | Retracted | Retracted |
| 2 | 10 degrees | Retracted | Retracted |
| 3 | 20 degrees | Extended | Retracted |
| 4 | 30 degrees | Extended | Extended |

Slat travel is now fitted to the visible stationary nose contour, rather than the original buried cut line. Each sampled span section clears that visible edge by the same outward distance; the three separate panels keep their rounded cross sections.

The LE flaps retain the previous mirrored scoop curve, positioned forward so their aft curl clears the wing. The rib roots remain attached to the fixed wing as the panels fold out. Both trailing-edge flap members connect beneath the stationary cover. Hold Control at idle or reverse for spoilers and ground braking.

## Model preview

[Download the v0.16 viewer ZIP](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/aviassembly-wing-preview-v0.16.zip). Extract All, then open aviassembly-wing-preview-v0.16.html in Chrome or Edge. The heading must say v0.16. Internet access is needed to load the rendering library.

The viewer now includes the game's native aileron mesh in its original child-transform position. The Flaps view highlights both main flap and slot-vane bridges; they no longer fade with the fixed wing. Dedicated Whole wing, Flaps, Aileron, Slats and LE flaps views are available. The aileron is shown at neutral and stays independent of flap settings.

Validation: compiled against the installed game. Geometry checks cover gap to the actual visible nose, non-folding slat tracks, unchanged cross sections, LE-to-wing clearance, stowed fit, flap/vane bridge attachments, both orientations, all angles and retraction, shading and track engagement. Browser checks cover all four settings, native aileron visibility, full flap highlighting and narrow-screen layout. Preview lighting differs from the game; in-game visual verification is still needed.

Enablement must currently be selected again after loading an aircraft. Source is in src. Build against installed Unity, game, BepInEx and Harmony assemblies.

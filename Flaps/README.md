# Big Wing Flaps v0.13.0

Install AviassemblyFlaps.dll in Aviassembly/BepInEx/plugins and restart the game. Enable flaps on each Big Wing. Use the lever or [ / ].

| Setting | Flaps | Slats | Inboard LE flaps |
|---|---|---|---|
| 1 | 0 degrees | Retracted | Retracted |
| 2 | 10 degrees | Retracted | Retracted |
| 3 | 20 degrees | Extended | Retracted |
| 4 | 30 degrees | Extended | Extended |

The slats are three separate panels along the span, with 5 cm physical breaks between their moving meshes. A closed wing nose remains behind the gaps. Rounded shading, the fixed lower leading edge and the original winglet bend are retained.

Two wider, bowed Krueger panels replace the hooked LE strip. They fold forward and down, rise gently at the forward edge, and stow inside the wing in the first three settings. Their span leaves clearance at the fuselage end.

Both trailing-edge flap members connect beneath the stationary cover. Hold Control (throttle-decrease) at idle or reverse for spoilers and ground braking. The rounded wing-root cap and native skin joints remain closed.

Validation: compiled against the installed game. Regression checks cover exactly three separated slats, two bowed LE panels, stowed clearance, shading, both wing orientations, 0 through 30 degrees and retraction, root seams, flap bridges, slot clearance and guide-track engagement. Mesh previews inspected; in-game visual verification is still needed.

Enablement must currently be selected again after loading an aircraft. Source is in src. Build against installed Unity, game, BepInEx and Harmony assemblies.

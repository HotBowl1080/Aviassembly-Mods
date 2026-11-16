# Big Wing Flaps v0.14.0

Install AviassemblyFlaps.dll in Aviassembly/BepInEx/plugins and restart the game. Enable flaps on each Big Wing. Use the lever or [ / ].

| Setting | Flaps | Slats | Inboard LE flaps |
|---|---|---|---|
| 1 | 0 degrees | Retracted | Retracted |
| 2 | 10 degrees | Retracted | Retracted |
| 3 | 20 degrees | Extended | Retracted |
| 4 | 30 degrees | Extended | Extended |

Each of the three slat panels now translates independently in its own wing-span frame. The outward opening is 6 cm along every panel, with 1.8 cm downward travel. Removing the shared chordwise rotation prevents the slot tapering from one end to the other. The three panels retain 5 cm spanwise breaks and a closed wing nose behind the slot.

The two inboard Krueger panels now bow forward and turn down/back at the nose. Their convex outer faces point forward and their support webs remain behind them, instead of presenting open scoops. They stow inside the wing in the first three settings and leave clearance at the fuselage.

Both trailing-edge flap members connect beneath the stationary cover. Hold Control (throttle-decrease) at idle or reverse for spoilers and ground braking. The rounded wing-root cap, native skin joints, and original winglet bend are preserved.

Validation: compiled against the installed game. Geometry regression checks cover uniform clearance at every slat vertex through all angles in both wing orientations, rigid panel translation, panel separation, LE orientation and stowed clearance, root seams, flap bridges, slot clearance, shading, guide-track engagement and retraction. Mesh previews inspected; in-game visual verification is still needed.

Enablement must currently be selected again after loading an aircraft. Source is in src. Build against installed Unity, game, BepInEx and Harmony assemblies.

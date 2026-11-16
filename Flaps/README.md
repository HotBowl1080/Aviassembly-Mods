# Big Wing Flaps v0.15.0

Install AviassemblyFlaps.dll in Aviassembly/BepInEx/plugins and restart the game. Enable flaps on each Big Wing. Use the lever or [ / ].

| Setting | Flaps | Slats | Inboard LE flaps |
|---|---|---|---|
| 1 | 0 degrees | Retracted | Retracted |
| 2 | 10 degrees | Retracted | Retracted |
| 3 | 20 degrees | Extended | Retracted |
| 4 | 30 degrees | Extended | Extended |

The LE flaps restore the v0.13 scoop curve, mirrored fore/aft about their attachment so the curl points back toward the wing. The original depth, curvature, two panels and support ribs are retained. They stow inside the wing in settings 1 through 3.

Each of the three slat panels translates independently in its own wing-span frame, retaining a uniform 6 cm outward opening and 1.8 cm downward travel along the panel. The three panels have 5 cm spanwise breaks, with a closed wing nose behind the slot.

Both trailing-edge flap members connect beneath the stationary cover. Hold Control (throttle-decrease) at idle or reverse for spoilers and ground braking. The rounded wing-root cap, native skin joints, and original winglet bend are preserved.

Validation: compiled against the installed game. Geometry checks cover the restored aft-curving LE profile, all deployment angles and retraction in both wing orientations, stowed clearance, uniform rigid slat travel, root seams, flap bridges, slot clearance, shading and guide-track engagement. The in-conversation preview uses the actual generated mesh at every degree of flap travel. Preview lighting differs from the game; in-game visual verification is still needed.

Enablement must currently be selected again after loading an aircraft. Source is in src. Build against installed Unity, game, BepInEx and Harmony assemblies.

## Model preview

[Download the viewer ZIP](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Flaps/aviassembly-wing-preview.zip). Right-click the downloaded ZIP, choose Extract All, and open aviassembly-wing-preview.html in Chrome or Edge. Keep internet access available to load the rendering library.

Drag to rotate, scroll to zoom, select a close-up, and use the four flap settings. The viewer contains the actual v0.15 wing geometry; lighting differs from the game. The ZIP avoids GitHub displaying raw HTML as plain text.

# Big Wing Flaps v0.11.0

Install AviassemblyFlaps.dll in Aviassembly/BepInEx/plugins and restart the game. Enable flaps on each Big Wing.

| Setting | Flaps | Slats | Inboard LE flap |
|---|---|---|---|
| 1 | 0 degrees | Retracted | Retracted |
| 2 | 10 degrees | Retracted | Retracted |
| 3 | 20 degrees | Extended | Retracted |
| 4 | 30 degrees | Extended | Extended |

Use the lever or [ / ]. The rounded main-wing leading edge uses six short facets with shared shading normals. Its upper four facets move together as each slat, with a closed fixed nose and lower wing underneath the slot. The original upturned winglet and its bend are preserved.

The inboard LE flap is a wider thin underside panel with two hinge webs. At the last setting it folds forward and down while its forward lip rolls upward. It stows inside the wing at the other settings.

The trailing-edge flap connects under its stationary cover. Hold Control (throttle-decrease) at idle or reverse for spoilers and ground braking.

Validation: compiled against the installed game; checked all deployment angles and retraction, both wing orientations, leading-device seams, stowed LE clearances, original winglet triangles, flap connections, and track engagement. In-game visual verification is still needed.

Enablement must currently be selected again after loading an aircraft. Source is in src. Build against installed Unity, game, BepInEx and Harmony assemblies.

# Big Wing Flaps v0.9.1

Install AviassemblyFlaps.dll in Aviassembly/BepInEx/plugins. Enable flaps on each Big Wing.

| Setting | Flaps | Slats | Inboard LE flap |
|---|---|---|---|
| 1 | 0 degrees | Retracted | Retracted |
| 2 | 10 degrees | Retracted | Retracted |
| 3 | 20 degrees | Extended | Retracted |
| 4 | 30 degrees | Extended | Extended |

Use the lever or [ / ]. Enabled wings use a consistent three-facet leading-edge model in the builder and flight. Only the upper 70-degree nose segment moves as a slat. The lower nose and closed wing structure remain fixed. Underside LE panels deploy at the final setting.

The trailing-edge flap connects under the stationary cover. Hold Control (throttle-decrease) at idle or reverse for spoilers and ground braking.

Flap enablement must currently be selected again after loading an aircraft. Source is in src. Build against installed Unity, game, BepInEx and Harmony assemblies.

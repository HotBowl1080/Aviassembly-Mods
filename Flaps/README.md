# Big Wing Flaps v0.7.2

Install AviassemblyFlaps.dll in Aviassembly/BepInEx/plugins. Enable flaps on each Big Wing.

| Lever | Flaps | Slats | Inboard LE flap |
|---|---|---|---|
| First | 0 degrees | Retracted | Retracted |
| Second | 10 degrees | Retracted | Retracted |
| Third | 20 degrees | Extended | Retracted |
| Fourth | 30 degrees | Extended | Extended |

The handle or [ / ] selects a detent. Devices animate smoothly between detents. Slats have one extended position shared by the third and fourth settings.

Thin slats are cut from the stock wing skin, moving forward and down. Two upper-wing spoilers sit ahead of the trailing-edge flaps. Hold Control (throttle-decrease) at idle or reverse to deploy spoilers and brake on the ground. Release to retract.

Flap enablement must currently be selected again after loading an aircraft.

Source files are under src. Build against the installed Assembly-CSharp, Unity, BepInEx and Harmony assemblies. Game assemblies are not included.

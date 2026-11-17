# Straight Wing development — v0.21 candidate

Implemented on the actual installed StraightWing mesh: a rigid inboard trailing-edge flap, closed cut surfaces, native aileron preserved, shared enable toggle/selector/physics integration, and 25.4 mm aft plus 12.7 mm down travel introduced only between 20 and 30 degrees. The offset hinge follows the stock taper and clears the skin in both mirrored deployment directions. Big Wing v0.20 geometry source and embedded preview payload remain unchanged.

The combined DLL compiles against the installed game, Unity, BepInEx and Harmony assemblies. It has not been installed into the game folder. This is a candidate for in-game testing, not a claim of completed flight validation.

## Completed checks

- Captured all 16 native Wing prefabs across the installed asset files, including tail and canard parts, with child transforms baked into the models.
- Both flap and fixed shell have zero boundary/nonmanifold edges after seam welding.
- Checked 62 deployment poses, 0 through 30 degrees with both local-down directions: rigid distances, fixed vertices, downward trailing-edge motion, clearance of the fixed hinge plane, normals/positions, stowed closure and retraction.
- Checked that extra translation is absent through 20 degrees and reaches the specified small travel at 30 degrees.
- Exported the actual C# positions/normals at every degree into the single canonical HTML. Other wings use native stock meshes; Big Wing retains its existing v0.20 adapter.
- Verified unique dropdown entries, matching one-file ZIP contents, JavaScript syntax, and unchanged Big Wing source/payload.

## Remaining verification

Browser inspection was unavailable because the local computer-use app-server helper could not launch. No browser rendering or in-game enablement/flight-force test is claimed. The game installation is readable but outside this task's writable directories; the DLL was built in the project workspace.

Close the game, back up the installed plugin, copy this branch's AviassemblyFlaps.dll into BepInEx/plugins, restart, enable Straight Wing flaps, and check both sides at all settings, selector visibility, ailerons, retraction, disabling flaps and Big Wing behavior. Finish those checks before starting another wing.

Further wing expansion is authorized one wing at a time after Straight Wing verification. Keep using the shared viewer and replace each wing entry from its actual generated mod poses as it changes.

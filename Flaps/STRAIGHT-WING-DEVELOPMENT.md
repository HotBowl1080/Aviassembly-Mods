# Straight Wing development

This branch retains the latest shared wing viewer from main and the released Big Wing v0.20 DLL.

Prepared source: StraightWingFlapMotion.cs provides rigid hinge rotation and a final-setting-only small aft/down shift. WingCatalogExporter.cs captures loaded native wing meshes with child ailerons and baked transforms.

The only active viewer is aviassembly-wing-preview.html. It uses an embedded model registry and a Wing dropdown. Do not use a separate catalog-loading viewer. See MODEL-UPDATES.md for the build-time export/embed workflow.

Still blocked by the unavailable computer environment: capture the actual Straight Wing and remaining native wing assets; calibrate the user's marked inboard flap region; cut and close the flap mesh; register it with the shared enable toggle, selector and physics; export actual modified poses into the shared HTML; compile and verify the combined mod. Finish and verify Straight Wing before enabling modifications on the next suitable wing; expansion to additional wings is now authorized.

This is not a Straight Wing release. Its entry remains explicitly pending until actual native mesh data is available. The current Big Wing model and released DLL remain v0.20.

## Resume checkpoint — 2026-11-16

A command runner is now available in a Linux workspace, but the Windows game installation at `E:\SteamLibrary\steamapps\common\Aviassembly` is not mounted. No native wing captures or game assemblies were found in the available workspace, temporary files or home directory. Neither `dotnet` nor `mcs` is on PATH. Repository access alone does not provide the missing native geometry or permit in-game validation.

The released DLL and viewer geometry are unchanged. No Straight Wing mesh cut, runtime integration, combined compilation or visual/flight validation has been completed by this checkpoint.

Resume in an environment with the installed game assets, Unity/game assembly references and a C# compiler:

1. Check for newer work and capture the actual complete game wing inventory before enabling modifiers. Include native child control surfaces and baked transforms; loaded objects alone may omit catalog parts.
2. Fit and cut the marked Straight Wing inboard flap, close all cut surfaces, and use the prepared hinge motion with small aft/down travel at the final setting only.
3. Integrate the flap with the shared enable toggle, selector and physics. Preserve Big Wing v0.20 and native controls.
4. Export the actual modified poses into the canonical viewer. Add every actual game wing to its dropdown using real native captures, keeping stock entries without flaps and preserving existing entries. Regenerate the matching ZIP.
5. Compile the combined DLL and verify mirrored orientations, stowed closure, selector settings, retraction, highlighting, wing selection and regressions. Keep source, released binary and preview geometry synchronized.
6. Only after Straight Wing passes those checks, implement and verify the next suitable wing, repeating the same capture/build/preview workflow.

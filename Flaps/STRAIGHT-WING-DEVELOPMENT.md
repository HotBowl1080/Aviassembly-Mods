# Straight Wing geometry v0.22 / Flap Mod v0.23

Flap Mod combines Straight Wing and Big Wing flaps in AviassemblyFlaps.dll. The v0.23 plugin adds paid, saved per-wing options and mirrored settings; see [UPGRADES.md](docs/UPGRADES.md). The geometry and force model described below remain the v0.22 implementation. Big Wing v0.20 geometry, animation and existing force branch are preserved. Straight Wing uses the captured native mesh, its separate native aileron, and actual generated C# preview poses.

## Reported v0.21 problem

The user reported severe pitch-up and speed loss when deploying Straight Wing flaps, plus a flap outline that was hard to distinguish from the fixed wing. The installed game's Wing.ApplyLift, Wing.ApplyDrag and PlaneContainer.GetLiftOrigin were inspected from Assembly-CSharp.dll.

The old added force branch applied lift and drag at each wing's orientation pivot rather than the game's balanced lift origin/centre of mass. It used the lift coefficient for drag, covered the full wing instead of the flap's area, and immediately applied the selected setting rather than the animated deployment.

## v0.22 changes

- Add 6 mm hinge and 4 mm end clearances by affinely insetting the already closed flap body. Transform normals with the inverse transpose; retain native UVs and all fixed-shell geometry. Each body stays watertight.
- Preserve the rigid 0/10/20/30 degree motion and its final-setting-only 25.4 mm aft / 12.7 mm down travel.
- Measure the moving panel's projected skin area: 0.22576015 of the stock wing planform.
- Separate Straight Wing aerodynamics from the unchanged Big Wing force branch. Follow the actual animated angle, measured flap area, native coefficient units and wing orientation/mirror sign. Fade the increment in vertical, sideways and reverse airflow.
- Apply lift at PlaneContainer.GetLiftOrigin() and drag via Rigidbody.AddForce(), so added drag does not introduce a wing-pivot pitching moment.
- Allocate conservative 0.25 g lift and 0.04 g drag increment limits by area-times-lift coefficient across all active aircraft wings sharing the rigidbody. These bound only the added Straight Wing forces, not the game's stock lift or drag.
- Rename the combined plugin display name to Flap Mod, keeping its plugin ID and DLL filename unchanged.

## Verification

The geometry runner checks both closed bodies, stowed seam clearance, all 62 mirrored deployment poses, rigid motion, fixed vertices, downward travel, hinge-plane clearance and retraction. It exports the actual 0-30 degree C# poses for the canonical viewer.

Force regressions cover zero added force at rest/retracted, gradual deployment, dynamic pressure scaling, independent native drag coefficients, mirrored lift, reverse/cross-flow fade, low-speed support and shared force limits with eight wings at six speeds.

The combined DLL compiles against the installed game, Unity, BepInEx and Harmony assemblies. All 16 viewer entries are retained. Only Straight Wing poses change; the Big Wing source/model and the other 14 stock entries are preserved. The viewer ZIP must contain byte-identical canonical HTML. A static render of the actual exported geometry has been inspected. The browser security policy blocked opening the local viewer; no browser interaction check is claimed.

## Remaining in-game verification

The v0.22 candidate has not been installed or flight-tested in-game by this task. Geometry and force regression checks do not prove handling for the user's aircraft.

Close the game, back up the installed plugin, replace AviassemblyFlaps.dll, restart, and retest the aircraft that exposed v0.21's pitch/drag problem. Check gradual deployment and retraction at each setting, normal trim and speed loss, mirrored wings, native ailerons, enable/disable, selector visibility and unchanged Big Wing behavior. Verify multiple aircraft weights and wing arrangements before tuning coefficients further or adding another wing.

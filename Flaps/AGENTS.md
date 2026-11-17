# Wing model and viewer maintenance

- Keep one canonical viewer: Flaps/aviassembly-wing-preview.html. Its Wing dropdown must use its embedded model registry. Do not introduce runtime catalog uploads, external model JSON requests, or separate per-wing HTML files.
- When changing a wing's mesh, flap geometry or animation, regenerate that wing's actual preview poses and update its registry entry in the same change. Update Flaps/aviassembly-wing-preview.zip from the same HTML. Preserve the other wing entries.
- Use native game meshes and actual generated mod geometry. Do not present a guessed reconstruction or another wing as the requested model. If assets are unavailable, retain an explicit pending entry and report the blocker.
- Released DLL, source and each modified preview must describe the same geometry and movement. The current released Big Wing model is v0.20. Straight Wing is not released until its mesh cut, closure, runtime registration and combined build are complete.
- Complete and verify Straight Wing first. After that, add and verify flaps on one additional suitable native wing at a time, as authorized by the user. Preserve the released Big Wing behavior and each wing's native control surfaces.
- The canonical viewer's Wing dropdown must cover every wing in the actual game part catalog, including stock wings without added flaps. Discover the inventory from game assets; do not invent names or claim loaded-object captures are necessarily the complete catalog.
- Capture stock wings before enabling flap modifiers. Include native child ailerons and bake their transforms correctly.
- Validate the actual mesh and both mirrored orientations, stowed closure, hinge motion, last-setting travel, retraction, device highlighting and wing selection. If compilation or rendering is unavailable, state that limitation and do not claim those checks passed.

## Publishing and naming

- Publish current deliverables and user-facing download links on main, organized in the existing project folders. Keep AviassemblyFlaps.dll and the canonical viewer paths stable.
- Keep historical viewer ZIPs in Flaps/archive; link current downloads from the repository and project READMEs.
- The user-requested display name is "Flap Mod". Big Wing and Straight Wing flaps share one download. Preserve the existing plugin ID and AviassemblyFlaps.dll filename when updating.

## Upgrade settings, money and saves

- Do not add flap speed limits, overspeed warnings, speed-triggered damage or automatic retraction. The user only allows considering slower extension at higher speed; this update leaves extension speed unchanged.
- Flaps and eligible spoilers are separate per-wing purchases in the existing wing settings menu. Each option costs 25% of that wing's base price, without compounding. Removing a placed option refunds its price. Big Wing supports both; Straight Wing supports flaps only until actual spoiler geometry is implemented and verified.
- Mirror settings when the native editor mirror mode is enabled. Quote both wings, validate the total before charging, apply/remove atomically, and create one undo step. Handle centreline/missing partners once and charge only missing purchases.
- Keep purchased options in native part prices for placement, aircraft value, resale and undo. Creative mode uses native free building. Never charge on restore, mirror-copy creation or loading; new copies are paid through native placement.
- Persist per-wing options in campaign/design files and undo snapshots using the bounded optional EOF extension. Do not insert bytes into the unframed native wing/part records. Keep native reader cursors and campaign money loading unchanged.
- Run tests/UpgradeRunner for transaction, mirroring or save changes and build against the installed game. Distinguish adapter tests from actual in-game testing.

# Wing model and viewer maintenance

- Keep one canonical viewer: Flaps/aviassembly-wing-preview.html. Its Wing dropdown must use its embedded model registry. Do not introduce runtime catalog uploads, external model JSON requests, or separate per-wing HTML files.
- When changing a wing's mesh, flap geometry or animation, regenerate that wing's actual preview poses and update its registry entry in the same change. Update Flaps/aviassembly-wing-preview.zip from the same HTML. Preserve the other wing entries.
- Use native game meshes and actual generated mod geometry. Do not present a guessed reconstruction or another wing as the requested model. If assets are unavailable, retain an explicit pending entry and report the blocker.
- Released DLL, source and each modified preview must describe the same geometry and movement. The current released Big Wing model is v0.20. Straight Wing is not released until its mesh cut, closure, runtime registration and combined build are complete.
- Flap modifications in the current expansion are limited to Straight Wing. Preserve the released Big Wing behavior and all other wings' native control surfaces.
- Capture stock wings before enabling flap modifiers. Include native child ailerons and bake their transforms correctly.
- Validate the actual mesh and both mirrored orientations, stowed closure, hinge motion, last-setting travel, retraction, device highlighting and wing selection. If compilation or rendering is unavailable, state that limitation and do not claim those checks passed.

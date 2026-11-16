# Straight Wing development

This branch retains the latest shared wing viewer from main and the released Big Wing v0.20 DLL.

Prepared source: StraightWingFlapMotion.cs provides rigid hinge rotation and a final-setting-only small aft/down shift. WingCatalogExporter.cs captures loaded native wing meshes with child ailerons and baked transforms.

The only active viewer is aviassembly-wing-preview.html. It uses an embedded model registry and a Wing dropdown. Do not use a separate catalog-loading viewer. See MODEL-UPDATES.md for the build-time export/embed workflow.

Still blocked by the unavailable computer environment: capture the actual Straight Wing and remaining native wing assets; calibrate the user's marked inboard flap region; cut and close the flap mesh; register it with the shared enable toggle, selector and physics; export actual modified poses into the shared HTML; compile and verify the combined mod. Do not enable flap modifications on other wing types.

This is not a Straight Wing release. Its entry remains explicitly pending until actual native mesh data is available. The current Big Wing model and released DLL remain v0.20.

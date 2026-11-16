# Straight Wing flaps - development status

This branch starts from the released Big Wing v0.20 code and DLL. It is not a Straight Wing release. The release DLL and the main branch downloads are unchanged.

## Prepared

- StraightWingFlapMotion.cs: one rigid flap hinge with 0/10/20/30 selector positions. No extra translation through setting 3. During the final transition, a smooth ramp adds 25.4 mm aft and 12.7 mm down. The movement direction is derived from the wing's local down vector, including mirrored wings. Positions and normals are regenerated from the immutable rest mesh, avoiding accumulated drift.
- WingCatalogExporter.cs: captures the real loaded Wing meshes, their material submesh indices, and child control surfaces. Child transforms and mirrored triangle winding are baked into Wing-local coordinates. Export before enabling flap modifiers. Capture coverage must be checked against the game's full part catalog.
- aviassembly-wing-preview-catalog-dev.html: retains the complete modified Big Wing v0.20 preview. Adds a Wing dropdown and a JSON catalog loader. Captured stock wings can be viewed, including their native ailerons. Stock wing entries do not offer unimplemented flap controls.

## Needed before release

1. Reconnect the local computer environment so the installed game assets and compiler are available.
2. Capture the Straight Wing and all remaining wing assets. Confirm prefab identity, local axes, the native aileron boundary, and the exact inboard trailing-edge section marked in the user's screenshot.
3. Cut and close the Straight Wing flap mesh, register it with the shared enable toggle, selector, and physics. Feed the actual flap vertex set and hinge endpoints into StraightWingFlapMotion. Do not enable flap modifications on other wing types.
4. Export the actual modified Straight Wing poses into the viewer, populate all stock wing entries, and remove the development catalog-loading step from the final user flow.
5. Compile the combined mod from the latest Big Wing v0.20 sources and the new Straight Wing implementation. Check stowed seams, hinge motion, final-setting offsets, native aileron independence, mirrored wings, retraction, lift/drag and viewer controls.

## Validation available this turn

The viewer module parses as JavaScript. Catalog validation accepts a valid mesh fixture and rejects unsupported schema versions, duplicate IDs and invalid mesh data. Motion math fixtures check detents, final-only translation, rigid rotation and retraction. No new C# compile, native Straight Wing mesh check, browser rendering or game runtime test was possible with the computer environment unavailable.

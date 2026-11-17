# One-file wing viewer updates

The stable deliverables are:

- aviassembly-wing-preview.html: the only active viewer HTML.
- aviassembly-wing-preview.zip: a download wrapper containing only that HTML.

The viewer embeds an application/json registry in its data-wing-models script. The existing Big Wing adapter retains the complete v0.20 model and poses. A raw entry embeds one wing's native meshes and, where implemented, its generated flap poses. Pending entries display an unavailable message, never a substitute model.

## Publish a model update

1. Capture the actual wing and its native child control surfaces from the game. For a modified wing, export the final generated mesh and normals for the shared selector's poses.
2. Create a build-time JSON manifest with schemaVersion: 1 and a wings array. Use stable IDs; big-wing and straight-wing are reserved for those entries. Each real model entry uses format: raw, name, version, hasFlaps, and meshes. Each mesh contains flat positions and normals, submeshes with triangle index arrays, and role: wing, flap, aileron, slat, le-flap or spoiler. Moving meshes also need frames with increasing angle values from 0 to 30 and flat positions/normals for every frame. Static wings must have hasFlaps: false.
3. Run node Flaps/tools/update-wing-preview.mjs exported-models.json. This validates the export, replaces matching entries, preserves other models, embeds the data in the canonical HTML, and rebuilds the ZIP.
4. Verify the new geometry, all selector settings, mirrored wings, native controls and dropdown entries. Commit the matching model source, compiled mod when behavior changed, canonical HTML and ZIP together.

The export manifest is a build input. Users never load it into the viewer and do not need it beside the HTML.

## Current coverage

All 16 native Wing prefabs are embedded: Big Wing retains its v0.20 modified model, Straight Wing contains its v0.22 candidate cut geometry and actual C# poses, and the other 14 parts retain stock meshes and controls. Tail and canard parts are included. Child transforms are baked into Wing-local coordinates.

Use `tools/capture-native-wings.py --game-dir <game> --output native-wings.json --straight-output straight-native.json` with UnityPy and numpy installed. An optional `--dependencies <directory>` supplies those Python packages without installing them globally. Capture reads all installed `.assets` files and identifies actual Wing components.

Run `dotnet run --project tests/GeometryRunner -- straight-native.json straight-poses.json` from Flaps to validate and export the actual modified Straight Wing poses. Append the stock Straight Wing child meshes from the native capture to that modified entry, replace its entry in the stock manifest, and pass the combined manifest to `tools/update-wing-preview.mjs`. Big Wing is deliberately excluded from native capture updates so its released adapter remains intact.

Compile with `build.ps1 -GameDir <game>`. The v0.22 candidate has passed geometry and force regression checks and static render inspection. Browser interaction and in-game flight retesting remain pending. See STRAIGHT-WING-DEVELOPMENT.md for exact completed checks and the remaining handoff.

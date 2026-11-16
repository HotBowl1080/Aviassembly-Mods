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

Big Wing v0.20 is embedded and available. Straight Wing is listed but has no captured model yet. Other native wing assets must be captured before their real entries can be added. The computer environment and installed game assets were unavailable during this viewer organization change.

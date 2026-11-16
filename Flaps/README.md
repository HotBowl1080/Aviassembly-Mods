# Big Wing Flaps v0.12.0

Install AviassemblyFlaps.dll in Aviassembly/BepInEx/plugins and restart the game. Enable flaps on each Big Wing.

| Setting | Flaps | Slats | Inboard LE flap |
|---|---|---|---|
| 1 | 0 degrees | Retracted | Retracted |
| 2 | 10 degrees | Retracted | Retracted |
| 3 | 20 degrees | Extended | Retracted |
| 4 | 30 degrees | Extended | Extended |

Use the lever or [ / ]. Three spanwise slat panels each move the upper three faces of the six-facet rounded nose. The lower nose stays with the wing to remove the hanging slat lip. Shared normals keep the edge shading smooth; the original winglet bend is preserved.

The inboard LE flap folds forward and down into a compact curved profile with a returning curled nose and two hinge webs. It remains inside the wing at the other settings.

Both the main trailing-edge flap and its narrow slot vane now connect across the join beneath the stationary cover. The vane has a longer chord and closer fit to the main flap, with clearance throughout the movement. Hold Control (throttle-decrease) at idle or reverse for spoilers and ground braking.

The rounded nose follows the original wing skin's spanwise bends and triangle crossings. A solid end cap meets the original tilted wing-root plane, closing the root openings. Tiny-face shading normals are normalized without Unity's small-vector cutoff.

Validation: compiled against the installed game. Geometry checks cover 0 through 30 degrees and retraction in both wing orientations, slot clearance, both flap bridges, root seam alignment, leading-device seams, stowed LE clearances, curled deployed LE shape, original winglet triangles, shading normals and track engagement. In-game visual verification is still needed.

Enablement must currently be selected again after loading an aircraft. Source is in src. Build against installed Unity, game, BepInEx and Harmony assemblies.

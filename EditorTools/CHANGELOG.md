# Editor Tools changelog

## 0.7.15

- Added draggable Move squares for XY, XZ, and YZ. A square follows the two local axes between its arrows and holds the third coordinate fixed.
- Press G and then tap two axis keys in sequence or press them together to move in that world plane. For example, G, X, Y locks movement to XY.
- Plane moves support Tab to enter the second numeric offset. Drag tests cover a rotated part, confirmation on release, and restoration on Escape.
- Passed 616 automated Unity checks. Verified that the installer places the matching 0.7.15 DLL.

## 0.7.14

- Made Hide and Show All compact icon buttons. The crossed eye hides the selection and the open eye restores all hidden parts. V and I perform the same actions.

## 0.7.13

- Fixed horizontal fuselage offset sensitivity. The horizontal and vertical offset arrows now track the grabbed point in perspective, matching the length arrow.
- Added geometry snapping to all three fuselage offset arrows. Turn on the magnet and drag over another part's edge or corner. The edited fuselage end's center aligns only along the dragged axis, and a yellow marker identifies the target. Shift switches to fine adjustment, and Ctrl selects grid snapping.
- Passed 594 automated Unity checks, including both ends at different zoom levels, snapping to a corner, and committing the snapped position.

## 0.7.12

Rotation snapping defaults to **1°**. Click the rotation angle button to cycle through **1°, 5°, 15°, 30°, 45°, 60°, 75°, 90°, and Free**. The selected increment applies to mouse rotation with R, axis constraints, trackball, and dragged rotation rings. Typed angles stay exact. Shift still slows mouse motion, and Ctrl still snaps movement to the grid.

This update brings the GitHub download from 0.7.3 to 0.7.12. Source, tests, and the DLL checksum are included. The build passed 559 isolated Unity checks and 345 camera math checks. Installer tests verified fresh installation, replacement and backups, and rejection of invalid packages.

## 0.7.11

- The bottom-left magnet opens **Center**, **Edge**, and **Off**. Center aligns the visible midpoint of the moving geometry. Edge uses the end nearest the target along a constrained axis, or a mesh edge or corner during a free move. The mod remembers the chosen reference.
- Normal mirrored dragging ignores attachment points and surfaces on both moving copies and their attached children, so a fixed part behind them can still receive the snap. Picking up a centerline part also avoids claiming a nearby placed part as its mirror.

## 0.7.10

- Drag a move arrow over a visible edge or corner on another part to snap along that axis. A yellow marker shows the target. Move the cursor away to resume ordinary dragging. This replaced the two-click V/E snapping workflow.
- X, Y, and Z start an axis edit directly from the Move or Rotate menu. Repeating the axis switches between world and local constraints and then clears the constraint.
- Backspace and Delete remove the active part while idle. Backspace erases numeric input during a precision edit. V hides the selection.

## 0.7.9

- A confirmed resize in Focused mode recenters the camera on the edited fuselage or group, including when the edit started by hovering over an unselected part. Unfocused mode keeps its framing.
- Added snapping to real mesh vertices and edges with optional axis or plane constraints. The first version used V/E and separate source and destination clicks.

## 0.7.8

- Fixed grouped transforms so selected parents and children move or scale once, automatic fuselage mirrors resize in place, and group moves preserve each part's saved rotation offset. An unlocked group member can still rotate when the active member's rotation is locked.
- Fixed numeric readjustment after a fuselage pivot recenters. Reentering the same length or offset preserves the geometry and budget. A different value applies only the difference. Switching tools clears the previous shape readout, and copy feedback remains visible after an edit.
- Fixed numeric component handling during axis and trackball changes, including Backspace on an empty component and retyping a grouped ring rotation after release.
- Fixed paste cancellation so it preserves an existing opposite-side part. Checked that a placed copy returns to the native edit workflow and that disabling and reenabling Editor Tools restores the intended hooks.

## 0.7.7

- Length offset follows the grabbed point on the fuselage handle as it moves in perspective.
- Corrected the crossed red and green rotation axes. Mouse rotation, axis labels, and typed angles correspond to the clicked ring.
- Reduced corner roundness sensitivity to 38 percentage points per 100 pixels at 1080p. Hollowness remains at 45 percentage points.

## 0.7.6

- Kept the camera fixed during a normal part drag or a fuselage edit, even at the minimum size. The point grabbed on a placed part follows the cursor, and only nearby attachment points can snap. Focused mode centers a new placement after release and leaves the view in place after repositioning an existing part.
- Added **Ctrl+C** to copy the active part and **Ctrl+V** to paste into normal placement. Copies retain shape, paint, rotation offset, and saved part settings. **Ctrl+D** duplicates in place and immediately opens advanced move, with confirm, cancel, cost checks, and native undo.
- Added a centered **Hotkeys** panel, accessible with the toolbar button or K. The first Shift-click highlights the part immediately, and the group count moved to the right.
- Added X as an idle delete shortcut in this version. It moved to Backspace and Delete in 0.7.10.

## 0.7.4–0.7.5

- Made the camera start Unfocused and preserve the chosen mode through placement, undo, aircraft changes, and returning from flight. F and the camera button can change the mode during edits without confirming them.
- Fixed fuselage handle tracking and increased drag sensitivity. Drag along the projected handle direction, or vertically when a handle points toward the camera. Shift slows the adjustment to one tenth without jumping when pressed or released.
- Clamped roundness to 0–100% and hollowness to 0–90%. Reversing an overshot drag responds immediately. The shared roundness handle preserves mixed corner values during dragging, while typing a percentage applies it to every corner.
- Applied group shape changes to matching fuselage ends and native mirrors. Previews stay outside the save and undo history, and confirmed changes use the native geometry and affordability checks.

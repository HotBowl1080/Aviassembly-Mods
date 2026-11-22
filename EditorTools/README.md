# Aviassembly Editor Tools 0.7.13

## Download and install

[Download Aviassembly.EditorTools.dll](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/EditorTools/Aviassembly.EditorTools.dll). With Aviassembly closed and BepInEx 5 installed, place the DLL in `Aviassembly/BepInEx/plugins/Aviassembly.EditorTools/`. Back up the previous version and keep one installed copy of the plugin.

If you have the project folder, [Install.ps1](Install.ps1) installs the DLL with a backup and checksum verification. The full list of changes since 0.7.3 is in the [changelog](CHANGELOG.md).


Focus Selected, multi-selection, Precision Edit, procedural shape precision, Hide Parts with X-ray, and propeller clearance are included in one plugin

The readout is a prominent bold text line below the top-right controls, with a black outline and no background box: Move by (X, local): 0.35 m or Rotate by (Z, local): 33.5 degrees; final shape values use = and scale multipliers use the multiplication symbol

Hide and Show All use a matching row above the native toolbar, leaving the game's CG and Drag controls clear; both buttons remain enabled during a valid mouse press so the click completes on release

## Controls

| Action | Control |
| --- | --- |
| Toggle Focused / Unfocused | F or camera mode button |
| Open the shortcuts menu | K or the Hotkeys button while idle |
| Close the shortcuts menu | K, Escape, or Close |
| Copy the active part | Ctrl+C |
| Pick up a copy for placement | Ctrl+V |
| Duplicate the active part in place and start advanced move | Ctrl+D |
| Match a wing to the top of a tailfin | In Move, drag the vertical arrow over the fin's top corner or edge. Release when the yellow marker appears. |
| Choose which part of the wing meets the fin | Click the magnet at the bottom left. Center aligns the visible midpoint, Edge aligns the facing edge, and Off disables snapping. |
| Start an axis edit from the Move or Rotate menu | Press X, Y, or Z, then move the mouse or type an amount. Click or press Enter to apply. |
| Delete the active part | Backspace or Delete while idle |
| Add or remove a selected part | Shift-click |
| Select all visible placed parts | A |
| Clear selection | Alt+A |
| Box selection | B then drag; Shift adds, Ctrl subtracts, Escape cancels |
| Uniform scale of editable fuselages | S, then drag or type a multiplier |
| Exact size, corner roundness, or hollowness | Drag its native shape handle, then type a value |
| Fine fuselage adjustment | Hold Shift while dragging any shape handle |
| Snap a fuselage end along its offset arrow | Turn on the magnet, then drag the horizontal, vertical, or length arrow over another part's edge or corner |
| Precision movement or rotation | Click and drag a native arrow or ring |
| Set the exact total drag adjustment | Type a number during or just after dragging, then Enter |
| Move selected placed part | G, then move mouse or type an offset |
| Rotate selected placed part | R, then move mouse or type degrees |
| Trackball rotation | R again during rotation |
| Axis constraint | X, Y, or Z |
| Local axis | Press the same axis again |
| Remove constraint | Press the same axis a third time |
| Exclude an axis while moving | Shift with X, Y, or Z |
| Choose axis from mouse direction | Middle mouse during a transform |
| Fine mouse adjustment | Hold Shift |
| Snap movement to grid | Hold Ctrl |
| Fine movement grid | Hold Ctrl and Shift |
| Confirm typed adjustment | Enter or left click |
| Confirm mouse handle drag | Release the mouse |
| Cancel active transform | Escape or right click |
| Hide selected parts | V or crossed-eye Hide button |
| Restore every hidden part | I or open-eye Show All button |
| Ghost view for hidden parts | Always enabled for hidden parts; no toggle |
| Global propeller clearance setting | All Props / Check On or Off inside a selected propeller's floating menu |

Controls are guarded during text entry, blocking menus, placement, and other exclusive operations

## Camera

The first editor visit starts Unfocused. F and the camera button switch modes, and your choice carries through placements, undo, aircraft changes, and returning from flight during the game session.

Dragging keeps the camera position, angle, and zoom fixed in both modes, including at fuselage shape limits. The point where you grabbed a placed part tracks the pointer. Nearby attachment endpoints can still snap.

Focused mode centers a part when you click it or finish resizing it, including when you grab a fuselage handle without selecting the part first. A group resize centers the group. Repositioning a placed part leaves the view in place after release. Press F or use the camera button to switch modes. If a drag is active, the framing change starts when the edit ends.

The Hotkeys button and K open the centered shortcuts panel while the editor is idle. K, Escape, and Close dismiss it.

## Precision Edit

Precision input is automatically available on the game's movement arrows and rotation rings; no mode switch or panel is required, and normal camera and selection controls remain available while idle

Drag a move arrow over another part's visible corner or edge to align along that arrow. Moving off the target resumes ordinary dragging. Shift gives finer motion and Ctrl snaps to grid increments. Rotation starts at 1 degree. Click the rotation angle button to cycle through 1, 5, 15, 30, 45, 60, 75, 90 degrees and Free. Typed angles stay exact.

Type a signed number while dragging or after releasing to replace the total adjustment measured from the operation's original pose; for example, dragging 0.37 m then typing 0.35 sets the total movement to 0.35 m, and rotating 90 degrees then typing 33.5 sets the total rotation to 33.5 degrees

Release commits a mouse-only drag and immediately returns to normal idle interaction; if a number has been typed, Enter confirms or Escape/right click cancels; cancelling a later numeric readjustment restores the already committed drag

The recent handle operation stays available for typing until another selection, tool, world click, external transform, or editor exit invalidates it; clicking a handle without dragging also selects its axis for subsequent numeric input

Keyboard and handle actions share the game's axes and rotation signs: X right, Y up, Z forward; G/R start with global axes, repeating an axis selects local orientation, and native handles use local orientation

G X 0.35 moves 0.35 m, and R Y -33.5 rotates by negative 33.5 degrees; these keyboard operations confirm with Enter or left click and cancel with Escape/right click

Ctrl snaps movement to 1 m increments, and Ctrl with Shift uses 0.1 m. Rotation follows the angle button. Shift reduces mouse sensitivity to one tenth.

During modal numeric input, minus toggles the sign, slash toggles the reciprocal, Backspace removes input, and Tab cycles components where multiple values are available

Native attachment and mirror operations are retained; attached children and gizmos follow the preview; previews do not enter saves or undo history, and confirming schedules one native undo step; numerically readjusting a previously committed drag is a separate undo step

This implements the listed Blender selection, move, rotate, and uniform procedural scale interactions; it does not reproduce every Blender feature, including advanced expressions, unit expressions, alternate pivot modes, arbitrary mesh scaling, or cursor wrapping

## Multi-selection

Shift-click toggles individual placed parts without picking them up; the last selected part is active, A selects all visible parts, and Alt+A clears selection

B starts box selection by part origins, including visible parts behind other parts; drag to replace the selection, hold Shift to add or Ctrl to subtract, and press Escape to cancel; parts hidden with V are excluded

Adding or removing parts with Shift-click, A, or box selection leaves the camera pivot in place; explicitly toggling Focused back on targets the selection center, while an ordinary click still centers its part when Focused

The first Shift-click shows the orange selection highlight. Groups show their count on the right. G, R, and native movement or rotation handles affect the group around its average selected origin. Attached children transform once even when their parent is also selected.

V and the Hide button hide the selection, and I restores every hidden part. Backspace and Delete remove the active part. Backspace erases a digit during numeric editing.

Ctrl+C copies the active part's shape, paint color, rotation offset, and saved part settings. Ctrl+V picks up that copy for normal placement with the game's snapping, symmetry, and cost checks. The clipboard holds one part for the game session.

Ctrl+D starts advanced move with a copy at the active part's position and angle. Move the copy with the mouse or enter an exact offset. Enter or left click confirms the placement and charges the part's price. Escape or right click cancels the copy.

## Shape precision

Click the existing fuselage size, width, height, offset, roundness, individual corner, or hollowness handles and type the exact value; the outlined readout shows only the active setting and its units, with no new panel

Shape handles stay with the fuselage during the preview. Drag along a size or offset handle, or drag vertically when the readout says Drag up/down. Pull the roundness handles inward to round a corner or the whole end. Pull the scale and hollowness handles outward to increase their values.

Horizontal, vertical, and length offset arrows track the grabbed point at different view angles and zoom levels. With the magnet on, hover over an edge or corner of another part to align the center of the edited fuselage end along the arrow's axis. A yellow marker shows the target. Hold Shift for fine adjustment or Ctrl for grid snapping.

At 1080p, dragging 100 pixels changes corner roundness by 38 percentage points or hollowness by 45. Length offset tracks the grabbed point along the fuselage axis at the current zoom and viewing angle. Rotation follows the plane of the clicked ring. Hold Shift to reduce subsequent motion to one tenth. Pressing or releasing Shift during a shape drag keeps the current value. Ctrl snaps dimensions to 0.1 m, percentages to 10 points, and scale to 0.1. Ctrl with Shift uses 0.01 m, 1 percentage point, and 0.01.

Width and height are full dimensions in metres; length and end offsets also use metres, while roundness and hollowness use percentages; for example, type 1.237 for a width of 1.237 m or 33.5 for 33.5% corner roundness

Drag values stop at the game's limits, including 0% to 100% for roundness and 0% to 90% for hollowness. Reverse the drag to move back from the limit. Typed values outside the range show the permitted range. Dragging the overall roundness handle adjusts the corners together from their existing values.

S scales editable fuselage geometry and selected part spacing uniformly around the selection center; type 1.25 for 125% size, and use the native width or height handle for a particular end dimension; the end scale handle uses a multiplier too

All selected parts must be editable procedural fuselages for shape operations; arbitrary scaling of fixed parts is not supported because native saves do not preserve generic transform scale

Enter confirms typed values. Escape or right click cancels. Release the mouse to confirm a drag without typed values.

Absolute dimensions and percentages can be typed immediately after releasing a handle to readjust them; multipliers begin a fresh operation with S or another handle click

Group shape edits apply to the same end of each selected fuselage and include native mirrored counterparts. Entering an exact value sets that value on each part. Preview geometry stays out of saves, and the game checks affordability before committing changes.

## Hide Parts

V hides each selected part's own visuals and decals so you can click visible parts behind it; attached and nested parts stay visible unless selected too

Hidden parts always have a faint blue X-ray view through surrounding geometry; they remain excluded from selection, paint, and decal picking so you can edit inside the aircraft; there is no X-ray toggle or UI control

The overlay draws existing geometry with a separate material and never replaces original materials; renderers that were already disabled before hiding stay absent; I removes all ghosts and restores each original renderer state

I restores every part hidden by this mod; Show All displays the count and has an open-eye icon, while Hide has a crossed-eye icon

Hiding preserves colliders, layers, transforms, attachment lists, money, saves, and undo history; only editor picking and highlights ignore hidden geometry

Original renderer states are restored exactly; leaving the editor, entering flight, changing aircraft, and unloading the mod restore hidden objects automatically

## Propeller clearance

Select or place a propeller engine to see its swept perimeter and clearance readout; ordinary parts, jet engines without a native propeller, and helicopter rotors do not receive the overlay; the All Props / Check On or Off button enables or disables this inspection globally and is on by default; changing the selected propeller does not reset the setting

The All Props check button sits inside the game's floating selected-part menu alongside move, rotate, delete, and settings; it appears only for a selected propeller and follows the native menu's position and visibility, with no separate bottom toolbar control

The sweep uses the installed game's RotorCollider center, radius, local axis, scale, and orientation; overlapping aircraft parts receive a red overlay and the sweep turns red, while estimated gaps below 0.1 m turn the ring amber

Hidden parts still count as obstructions; the engine's own geometry is excluded, while independently attached child parts are checked; editor mode, camera, selection, placement, and save data are not changed by checking

This is an inspection aid, not a guarantee of flight clearance; a 48-sided convex sweep has a 0.01 m margin on each side of the rotation plane and slightly conservatively encloses the native radius; non-convex meshes additionally receive 192 radial checks in both directions

The gap is explicitly approximate: samples cover six radial bands and the center, non-convex meshes use conservative bounds for distance, and the readout stops at greater than 2 m; collision geometry can differ from visible geometry, and very thin or fully enclosing non-convex geometry may need visual inspection

Checks update up to ten times per second for the selected propeller; the query collider sits outside the aircraft and is enabled only during synchronous collision queries, with no physics simulation or input handling between enable and disable; it is never saved or left active between frames

## Build from source

With the game and BepInEx 5 installed, run `./build.ps1` from this folder. The source is in [src](src/), and validation is in [tests](tests/).


## Verification

Built against local Assembly-CSharp SHA256 1E831DB844376E4EC0E96B81DCB5FB2B40AB85757D9A8A7AD20B3F50620D159A, BepInEx 5.4.23.6, and Unity 6000.0.69f1; other game builds remain unverified

```powershell
.\tests\RunUnityTests.ps1
```

The runner uses a separate disposable game copy under .work\UnityTestHost, suppressing unrelated startup callbacks; it loads actual Building scene assets and a synthetic aircraft without launching the installed game, reading real saves, or starting Steam/cloud synchronization

The standalone Hide Parts worker used a different isolated host; its 60 passing checks are recorded in tests\hide-parts-standalone-results.txt

The combined build passed 594 isolated Unity checks and compiled with zero warnings or errors. The full run is in [runtime-results.txt](tests/runtime-results.txt). The separate camera math suite passed 345 checks.

Geometry snapping uses visible mesh corners and edges. The magnet remembers Center or Edge for advanced moves. On an axis, Center uses the midpoint of the selected geometry along that axis, and Edge uses the end nearest the hovered target. In a free move, Center uses the middle of the selection in the active part's orientation. Edge uses the starting corner or edge under the cursor, or the nearest source edge if the move started in empty space. Typed amounts take precedence.

During ordinary mirrored placement, both copies ignore their own snap points and surfaces, including attached children that move with them. The cursor can still reach a fixed fuselage behind either copy.

The correctness review also covers attached fuselages selected in either order, automatic mirror spacing, editing a displayed offset after the game recenters the fuselage pivot, grouped rotation with a locked part, and cancelling a pasted part near an existing mirror. Input tests cover switching tools, editing individual numeric components, and disabling and reenabling the plugin.

The length tests picked each stock handle, then checked the grabbed point against the cursor through forward and reverse drags at three camera positions. Rotation tests used all three stock rings on a rotated part and checked the mouse drag, the local axis readout, and a typed angle.

The tests check cursor tracking and nearby snapping, fixed camera pose at the fuselage width limit in both modes, the first Shift-click highlight, centered shortcut layout, and duplicate confirmation, cancellation, save isolation, cost, and undo history.

The regression checks cover native placement in both camera modes, focus retention on aircraft reset and reentry, selection and precision input, real roundness and hollowness handles, clamping and reversal, Shift fine control, and gizmo positions during preview and cancel.

Multi-selection and shape coverage includes A/Alt+A/B/G/S input routing, native shape handle collider picking, group median rotation, selected parent/child handling, group hiding, exact dimensions and corner percentages on actual procedural mesh assets, idle precision preservation, saved geometry scaling, affordability, cancellation, and preview isolation from native save bytes

Coverage also includes camera modes with no selection, manual F overrides during transforms, exact numeric and mouse transforms, mirrors, attached parts, typing during and after drag, Hide and Show All pointer handling, text focus, and toolbar bounds at 1904 by 984.

These are headless runtime fixtures, not a visual playtest of the complete editor or physical keyboard testing; native undo snapshots are checked without exercising the complete aircraft reload path of an actual Undo

Inspection coverage includes native propeller discovery, repeated overlap queries, approximate axial and tip gaps, non-convex colliders, mirrored and resized props, hidden and attached obstructions, original material preservation, permanent X-ray draw-command cleanup, the native part-menu Prop Check control through pointer press/release, and flight cleanup; actual visual appearance still requires a visual playtest

## Independent module task

[The Hide Parts development brief](docs/Hide_Parts_Task_Prompt.txt) contains the original separate worker prompt; reusable hiding sources now also require src\HiddenXray.cs and src\EditorToolRow.cs alongside src\HideParts.cs and src\HidePartsUI.cs

No game binaries or copied native assets are included

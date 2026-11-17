# Aviassembly Editor Tools 0.7.0

## Download and install

[Download Aviassembly.EditorTools.dll](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/EditorTools/Aviassembly.EditorTools.dll) from main. Close Aviassembly, back up the previous plugin, and place the DLL in `Aviassembly/BepInEx/plugins/Aviassembly.EditorTools/`. BepInEx 5 must already be installed and initialized. Keep only one installed copy of this plugin.

If you download this project folder, [Install.ps1](Install.ps1) can install it with backups and checksum checks: `./Install.ps1 -GameDir "<Aviassembly installation>"`.

Source is in [src](src/), validation is in [tests](tests/), and the original development brief is in [docs](docs/).

Focus Selected, multi-selection, Precision Edit, procedural shape precision, Hide Parts with X-ray, and propeller clearance are included in one plugin

The readout is a prominent bold text line below the top-right controls, with a black outline and no background box: Direction (X): 0.35 m or Rotation (Z): 33.5 degrees

Hide and Show All use a matching row above the native toolbar, leaving the game's CG and Drag controls clear; both buttons remain enabled during a valid mouse press so the click completes on release

## Controls

| Action | Control |
| --- | --- |
| Toggle Focused / Unfocused | F or camera mode button |
| Open or close shortcuts menu | K while idle; Escape also closes |
| Add or remove a selected part | Shift-click |
| Select all visible placed parts | A |
| Clear selection | Alt+A |
| Box selection | B then drag; Shift adds, Ctrl subtracts, Escape cancels |
| Uniform scale of editable fuselages | S, then drag or type a multiplier |
| Exact size, corner roundness, or hollowness | Drag its native shape handle, then type a value |
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
| Snap mouse adjustment | Hold Ctrl |
| Fine snapping | Hold Ctrl and Shift |
| Confirm typed adjustment | Enter or left click |
| Confirm mouse handle drag | Release the mouse |
| Cancel active transform | Escape or right click |
| Hide selected parts | V or crossed-eye Hide button |
| Restore every hidden part | I or open-eye Show All button |
| Ghost view for hidden parts | X-ray On/Off toolbar button |
| Selected propeller clearance | Prop Check On/Off toolbar button |

Controls are guarded during text entry, blocking menus, placement, and other exclusive operations

## Camera

The editor starts Unfocused with the default aircraft camera; F or the toolbar button toggles Focused and Unfocused even when nothing is selected or no editing tool is active

Completing placement of a new part focuses its final position while retaining the viewing angle and zoom; picking up existing parts, moving, and rotating do not activate focus or make the camera follow the part

F can also toggle the mode during placement or precision editing; choosing Unfocused returns to the native aircraft center, and moving existing parts preserves that choice

Input projection is captured at the start of a drag so explicitly toggling F cannot push the part farther away on each frame; dropping the part restores native placement projection

Focused mode remains available without a selection; completing a new placement keeps its final position focused even when the game clears its selection, and subsequent movement leaves that pivot stationary

K opens the shortcuts menu while idle; K or Escape closes it, and editor actions are blocked while it is open; typing in native text fields, active transforms, placement, and blocking game menus prevent it from opening

## Precision Edit

Precision input is automatically available on the game's movement arrows and rotation rings; no mode switch or panel is required, and normal camera and selection controls remain available while idle

Click an arrow or ring and drag; movement is unsnapped by default, with Ctrl for snapping and Shift for finer motion; the top-right line shows only Direction or Rotation, the chosen axis, the number, and m or a degree symbol

Type a signed number while dragging or after releasing to replace the total adjustment measured from the operation's original pose; for example, dragging 0.37 m then typing 0.35 sets the total movement to 0.35 m, and rotating 90 degrees then typing 33.5 sets the total rotation to 33.5 degrees

Release commits a mouse-only drag and immediately returns to normal idle interaction; if a number has been typed, Enter confirms or Escape/right click cancels; cancelling a later numeric readjustment restores the already committed drag

The recent handle operation stays available for typing until another selection, tool, world click, external transform, or editor exit invalidates it; clicking a handle without dragging also selects its axis for subsequent numeric input

Handle actions follow the game's own axis directions and local orientation; optional G/R shortcuts retain Blender axes, X right, Y forward, Z up, and their existing constraints and trackball controls

G X 0.35 moves 0.35 m, and R Z -33.5 rotates by negative 33.5 degrees; these keyboard operations confirm with Enter or left click and cancel with Escape/right click

Ctrl mouse movement uses 1 m increments and Ctrl rotation uses 5 degrees; Ctrl with Shift uses 0.1 m and 1 degree; Shift alone reduces mouse sensitivity to one tenth

During modal numeric input, minus toggles the sign, slash toggles the reciprocal, Backspace removes input, and Tab cycles components where multiple values are available

Native attachment and mirror operations are retained; attached children and gizmos follow the preview; previews do not enter saves or undo history, and confirming schedules one native undo step; numerically readjusting a previously committed drag is a separate undo step

This implements the listed Blender selection, move, rotate, and uniform procedural scale interactions; it does not reproduce every Blender feature, including advanced expressions, unit expressions, alternate pivot modes, arbitrary mesh scaling, or cursor wrapping

## Multi-selection

Shift-click toggles individual placed parts without picking them up; the last selected part is active, A selects all visible parts, and Alt+A clears selection

B starts box selection by part origins, including visible parts behind other parts; drag to replace the selection, hold Shift to add or Ctrl to subtract, and press Escape to cancel; parts hidden with V are excluded

Selected groups have an orange overlay and a count; G, R, and native movement or rotation handles affect the group around the average selected origin, with attached children transformed once even if their parent is also selected

V and the Hide button hide the entire selection; unselected child parts stay visible, and I restores every hidden part; other native actions such as delete and copy still act on the active part

## Shape precision

Click the existing fuselage size, width, height, offset, roundness, individual corner, or hollowness handles and type the exact value; the outlined readout shows only the active setting and its units, with no new panel

Width and height are full dimensions in metres; length and end offsets also use metres, while roundness and hollowness use percentages; for example, type 1.237 for a width of 1.237 m or 33.5 for 33.5% corner roundness

Corner values use the game's roundness parameter rather than a geometric fillet radius; native shape limits remain enforced, including a maximum hollowness of 90%

S scales editable fuselage geometry and selected part spacing uniformly around the selection center; type 1.25 for 125% size, and use the native width or height handle for a particular end dimension; the end scale handle uses a multiplier too

All selected parts must be editable procedural fuselages for shape operations; arbitrary scaling of fixed parts is not supported because native saves do not preserve generic transform scale

Mouse editing is unsnapped by default, Shift slows it to one tenth, and Ctrl snaps dimensions and multipliers to 0.1 or percentages to 10%; Enter confirms typed values, Escape or right click cancels, and mouse release confirms a drag without numbers

Absolute dimensions and percentages can be typed immediately after releasing a handle to readjust them; multipliers begin a fresh operation with S or another handle click

Group shape edits apply the same value to the same end of each selected fuselage and include native mirrored counterparts; geometry, collisions, attachment points, weight, price, and save data use the native procedural system, with previews excluded from saves and affordability checked before commit

## Hide Parts

V hides each selected part's own visuals and decals so you can click visible parts behind it; attached and nested parts stay visible unless selected too

Hidden parts default to a faint blue X-ray view through surrounding geometry; they remain excluded from selection, paint, and decal picking so you can edit inside the aircraft; X-ray Off removes the ghost view without restoring those parts

The overlay draws existing geometry with a separate material and never replaces original materials; renderers that were already disabled before hiding stay absent; I removes all ghosts and restores each original renderer state

I restores every part hidden by this mod; Show All displays the count and has an open-eye icon, while Hide has a crossed-eye icon

Hiding preserves colliders, layers, transforms, attachment lists, money, saves, and undo history; only editor picking and highlights ignore hidden geometry

Original renderer states are restored exactly; leaving the editor, entering flight, changing aircraft, and unloading the mod restore hidden objects automatically

## Propeller clearance

Select or place a propeller engine to see its swept perimeter and clearance readout; ordinary parts, jet engines without a native propeller, and helicopter rotors do not receive the overlay; Prop Check toggles this inspection and is on by default

The sweep uses the installed game's RotorCollider center, radius, local axis, scale, and orientation; overlapping aircraft parts receive a red overlay and the sweep turns red, while estimated gaps below 0.1 m turn the ring amber

Hidden parts still count as obstructions; the engine's own geometry is excluded, while independently attached child parts are checked; editor mode, camera, selection, placement, and save data are not changed by checking

This is an inspection aid, not a guarantee of flight clearance; a 48-sided convex sweep has a 0.01 m margin on each side of the rotation plane and slightly conservatively encloses the native radius; non-convex meshes additionally receive 192 radial checks in both directions

The gap is explicitly approximate: samples cover six radial bands and the center, non-convex meshes use conservative bounds for distance, and the readout stops at greater than 2 m; collision geometry can differ from visible geometry, and very thin or fully enclosing non-convex geometry may need visual inspection

Checks update up to ten times per second for the selected propeller; the query collider sits outside the aircraft and is enabled only during synchronous collision queries, with no physics simulation or input handling between enable and disable; it is never saved or left active between frames

## Build and install

Building from source requires a .NET SDK, locally installed game assemblies, and BepInEx 5; the prebuilt DLL only needs the game and BepInEx 5

The prebuilt plugin is kept at EditorTools/Aviassembly.EditorTools.dll in [HotBowl1080/Aviassembly-Mods](https://github.com/HotBowl1080/Aviassembly-Mods/tree/main/EditorTools); updates use that same filename

To install the prebuilt plugin, copy it into BepInEx/plugins/Aviassembly.EditorTools in your game directory, replacing the existing copy while the game is closed; compiling from source requires the SDK

```powershell
.\build.ps1
.\Install.ps1
```

Both scripts accept a GameDir parameter; close Aviassembly before installation, and run the installer as administrator if Windows blocks writes to Program Files

The installer backs up the previous DLL, checks for duplicate copies, and verifies the installed checksum

For manual installation, copy dist\BepInEx into the game directory; install only Aviassembly.EditorTools.dll, never a runtime test DLL

Existing Focus Selected settings remain in BepInEx\config\rylan.aviassembly.editortools.cfg; Shortcut defaults to F, ShowPanel controls the camera button, and TransitionSeconds controls the transition

## Verification

Built against local Assembly-CSharp SHA256 1E831DB844376E4EC0E96B81DCB5FB2B40AB85757D9A8A7AD20B3F50620D159A, BepInEx 5.4.23.6, and Unity 6000.0.69f1; other game builds remain unverified

```powershell
.\tests\RunUnityTests.ps1
```

The runner uses a separate disposable game copy under .work\UnityTestHost, suppressing unrelated startup callbacks; it loads actual Building scene assets and a synthetic aircraft without launching the installed game, reading real saves, or starting Steam/cloud synchronization

The standalone Hide Parts worker used a different isolated host; its 60 passing checks are recorded in tests\hide-parts-standalone-results.txt

The combined build passes 286 checks with zero failures and compiles with zero warnings or errors; the report is tests\runtime-results.txt

Multi-selection and shape coverage includes A/Alt+A/B/G/S input routing, native shape handle collider picking, group median rotation, selected parent/child handling, group hiding, exact dimensions and corner percentages on actual procedural mesh assets, idle precision preservation, saved geometry scaling, affordability, cancellation, and preview isolation from native save bytes

Coverage includes camera modes with no selection, focus after new placement, no automatic focus or following during movement, stable native placement rays, manual F overrides during transforms, outlined and lowered readout geometry without a background, K menu input blocking and dismissal, exact numeric and mouse transforms, snapping, mirrors, attached parts, cancellation, save isolation, undo snapshots, typing during and after drag, complete Hide/Show All pointer press and release, V/I routing, text focus, and toolbar bounds at 1904 by 984

These are headless runtime fixtures, not a visual playtest of the complete editor or physical keyboard testing; native undo snapshots are checked without exercising the complete aircraft reload path of an actual Undo

Inspection coverage includes native propeller discovery, repeated overlap queries, approximate axial and tip gaps, non-convex colliders, mirrored and resized props, hidden and attached obstructions, original material preservation, X-ray draw-command cleanup, both new toolbar buttons through pointer press/release, and flight cleanup; the actual X-ray appearance still requires a visual playtest

## Independent module task

Hide_Parts_Task_Prompt.txt contains the original separate worker prompt; reusable hiding sources now also require src\HiddenXray.cs and src\EditorToolRow.cs alongside src\HideParts.cs and src\HidePartsUI.cs

No game binaries or copied native assets are included

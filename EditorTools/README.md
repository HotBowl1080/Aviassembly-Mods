# Aviassembly Editor Tools 0.5.0

Focus Selected, Precision Edit, and Hide Parts are included in one plugin

The readout is one small text line in the top right, with no background box: Direction (X): 0.35 m or Rotation (Z): 33.5 degrees

Hide and Show All use a matching row above the native toolbar, leaving the game's CG and Drag controls clear; both buttons remain enabled during a valid mouse press so the click completes on release

## Controls

| Action | Control |
| --- | --- |
| Toggle Focused / Unfocused | F or camera mode button |
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
| Hide selected part | V or crossed-eye Hide button |
| Restore every hidden part | I or open-eye Show All button |

Controls are guarded during text entry, blocking menus, placement, and other exclusive operations

## Camera

The editor starts Unfocused with the default aircraft camera; F or the toolbar button toggles Focused and Unfocused even when nothing is selected or no editing tool is active

Starting placement, picking up a part, or beginning a precision transform automatically focuses that part; the camera follows its current position while retaining the viewing angle and zoom, including before a new part has been placed

F can also toggle the mode during placement or precision editing; choosing Unfocused remains in effect for the rest of that operation, returning to the native aircraft center; starting another placement or transform focuses the new operation

Input projection is captured at the start of a drag so camera following cannot push the part farther away on each frame; the visible camera is independent from the fixed input projection, and dropping the part restores native placement projection

Focused mode remains available without a selection, and the next selected part becomes its target; completing a placement keeps the newly placed part focused even when the game clears its selection

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

This implements the listed Blender move and rotate interactions; it does not reproduce every Blender feature, including advanced expressions, unit expressions, alternate pivot modes, scale, or cursor wrapping

## Hide Parts

V hides the selected part's own visuals and decals so you can click visible parts behind it; attached and nested parts stay visible unless separately hidden

I restores every part hidden by this mod; Show All displays the count and has an open-eye icon, while Hide has a crossed-eye icon

Hiding preserves colliders, layers, transforms, attachment lists, money, saves, and undo history; only editor picking and highlights ignore hidden geometry

Original renderer states are restored exactly; leaving the editor, entering flight, changing aircraft, and unloading the mod restore hidden objects automatically

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

The combined build passes 205 checks with zero failures and compiles with zero warnings or errors; the report is tests\runtime-results.txt

Coverage includes camera modes with no selection, focus on unplaced and moving parts, stable native placement rays while the camera follows, manual F overrides during transforms, top-right text geometry without a background, exact numeric and mouse transforms, snapping, mirrors, attached parts, cancellation, save isolation, undo snapshots, typing during and after drag, complete Hide/Show All pointer press and release, V/I routing, text focus, and toolbar bounds at 1904 by 984

These are headless runtime fixtures, not a visual playtest of the complete editor or physical keyboard testing; native undo snapshots are checked without exercising the complete aircraft reload path of an actual Undo

## Independent module task

Hide_Parts_Task_Prompt.txt contains the separate worker prompt updated with V, I, and both icons; reusable module sources are src\HideParts.cs and src\HidePartsUI.cs

No game binaries or copied native assets are included

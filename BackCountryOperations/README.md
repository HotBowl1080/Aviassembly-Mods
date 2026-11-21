# BackCountry Operations

BackCountry Operations adds bush wings, flap kits, steerable and adjustable
landing gear, piston engine options, and the Wind Farm runway repair to
Aviassembly. Version 0.21.2 keeps the campaign on the existing airfields.

## Build

Use the .NET SDK, the Windows Steam installation of Aviassembly, and BepInEx
5.4.23.6. The recorded baseline is Steam build 26689425. Clone the whole
repository so the sibling `Flaps` folder is available. From this folder, run:

```powershell
dotnet build .\BackCountryOperations.csproj -c Release
```

Pass `-p:AviassemblyDir="path to Aviassembly"` if the game is installed
outside the default Steam library configured in the project file. The DLL is
at `bin/Release/netstandard2.1/BackCountryOperations.dll`.

The build embeds `../Flaps/AviassemblyFlaps.dll` from the shared repository.
In a standalone checkout, put that DLL at `Vendor/AviassemblyFlaps.dll` or set
`-p:FlapModDll="path to AviassemblyFlaps.dll"`. If the separate plugin is present,
BackCountry Operations shares its flap selector and applies no duplicate
wing patches. Stock wing upgrade prices and saved fittings use the
original Flap Mod format. Fitted Cub and 172 wings share the same
cockpit lever, keyboard, and gamepad controls.

### Where to make changes

- `BushUtilityWings.cs` owns wing profiles, flap fittings, and the editor controls.
- `BushLandingGear.cs`, `BushWheelPart.cs`, and `BushGroundHandling.cs` own gear,
  wheel contact, braking, and powered tail lift.
- `CampaignProgress.cs`, `RoughStripScout.cs`, and `MapJobPanel.cs` own mission
  progress, delivery checks, and the stock-style airport briefings.
- `WindFarmWorks.cs` and `InlandClearance.cs` own the existing runway's visuals
  and nearby scenery. The patch kit's editable SVG is in `Assets`.

## In-game check

1. Use an isolated test install with BepInEx 5 and a disposable save. Put the
   DLL in its `BepInEx/plugins` folder.
2. Start Aviassembly and quit normally. In `BepInEx/LogOutput.log`, look for
   one startup message containing both versions. Check for plugin errors.
3. In `BepInEx/config/com.machworks.backcountryoperations.cfg`, set
   `EnableDiagnostics = true` under `[Developer]`. Relaunch and quit.
   Check for the Unity version and the quit event, then turn diagnostics off.

## Stock runway baseline

Survey the stock runway in a disposable save. Mark the usable surface and
thresholds, then record any scenery that intrudes into that footprint.
Compare those obstacles after returning from the editor, resetting the airport,
flying out far enough to unload the area and returning, and relaunching.

For flight tests, use a modest early Career design, the same design carrying a
representative legal payload, and a wider utility design for ground clearance.
Record the design, payload, fuel, weather, takeoff direction, and start point.
Fly the early design in both directions at each load and record liftoff and
climb-out. Use the utility design to check taxi and turnaround space. Match
each setup when comparing stock and modded runs.

Use the measured footprint and obstacle bounds to scope the scenery patch.
If a reference aircraft still runs out of usable surface after clearance,
record which setup it was before evaluating a runway extension.

### First visual survey

- The overhead and both runway-end views show a visibly clear paved surface.
  Cones and airport structures sit along the sides. Actual collider overlap
  remains to be checked.
- Looking inland from the shoreline end, trees and rising terrain are visible
  beyond the far threshold. Check the immediate area past that threshold for
  scenery on or close to the extended centerline.
  A close-up shows a large tree nearly in line with the runway beyond the
  inland threshold. Its exact offset and collider still need measurement.
- Looking toward the shoreline, the terrain beyond the far end appears to
  give way to open water. The usable turnaround and overrun distance at
  each end still need ground measurements.
- To get the opposite view, the light taildragger taxied the length of the
  runway, went completely off the pavement, turned right, then turned left
  and taxied back onto the centerline. Fuel decreased during this maneuver.
  This is the turnaround baseline for this aircraft with current steering.
  Repeat with the same design and a matched fuel load when tailwheel steering
  is available in Flight Controls. Use that result before deciding whether
  the field needs a larger turnaround area.

## Scenery hook findings

Read-only inspection of the installed Steam build 26689425 confirmed these
entry points for the Stage 01 snapshot probe:

| Purpose | Confirmed game behavior |
| --- | --- |
| World identity | `AirportSpawner.Load(GameDataReader)` selects the newest compatible `WorldAsset` for the save version and passes it to `ContinentManager.SetWorldAsset`. |
| Chunk setup | `ChunckManager.CreateChunk(Vector3)` registers the chunk and calls `Chunk.Init(float, ObjectPool)`. The chunk subscribes `NewSurfaceDecor()` to `Terrain.TerrainComplete`. |
| Mesh and scenery | `Terrain.DataReceived(HeightMap)` builds the visual mesh synchronously and then raises `TerrainComplete`. The terrain collider mesh is assigned separately in `Terrain.Update()`. |
| Snapshot | A postfix on `Chunk.NewSurfaceDecor()` can read `Chunk.position` and the completed `Chunk.trees` list. Each `Tree` exposes the placed decoration type and holds LOD0 and LOD1 mesh filters and a capsule collider. |
| Unload and reuse | `Chunk.OnDestroy()` returns trees through `ObjectPool.ReleaseObject(Tree)`. Snapshot values at capture time because the same tree object can be reused by another chunk. |

## Capture and compare

1. After the startup smoke test, set `Enabled = true` under `[SceneryProbe]`
   in the plugin configuration file and restart in the disposable Career
   save. The probe centers on the airport identified by the stock initial
   spawn logic. For a custom area, set `CenterOnStartingAirport = false` and
   enter `CenterX` and `CenterZ` in world coordinates.
2. Check the BepInEx log for the capture location and starting-airport
   message. `Radius` (default 1000 world units) filters chunk origins.
   `MaxCaptures` (default 64) limits the number of files per launch. Use the
   logged timestamped folder under `BepInEx/BackCountryOperations/captures`.
3. Repeat the editor exit, airport reset, stream-out and return, and full
   relaunch individually. Keep track of which files correspond to each
   action and use captures with the same chunk origin and world asset.

The first two live runs of 0.2.0 reported five chunks with matching decoration
counts, but the exporter omitted the individual records. The live 0.2.1
run `20261120-170215-faea214dffae40c08f76d9fafeee536a` verified the exporter
fix. It captured 677 records across five chunks in `LocalizedVersion` (world
version 24), including 176 active decoration colliders in the origin chunk
at Base. Use this run as the reference for the next full relaunch.

The next 0.2.1 launch matched all 677 positions and transforms, with model
differences at 235 placements across the five chunks. Physics bounds from
that capture point were stale. Some appeared kilometers from their matching
trees. Starting in 0.2.2 (capture schema 2), the probe calculates capsule
bounds from the current transform and collider dimensions. Use schema 2
captures for runway screening and for comparing bounds across runs.

The first fly-away-and-return test produced repeated snapshots for all five
chunks on 0.2.1. Comparing the first and last snapshot for each origin showed
no position or transform changes. The Base chunk had 72 model changes.
Carry the remaining collider and load-path checks into normal flights while
testing the first runway change. The next implementation checkpoint is to
mark both thresholds and the usable width, then preview the runway footprint.

```powershell
.\Compare-SceneryCapture.ps1 -Before 'path/to/first.json' -After 'path/to/second.json'
```

The output shows exact differences in positions, mesh variants, transforms,
and collider bounds. A moved decoration appears under positions and in each
category keyed to that position. The two stored mesh names describe the
assigned LOD meshes. Compare screenshots at matching camera distances to
check which mesh the game actually draws. Repeat with Atmosphere only after
capturing the standalone result.

## Screen a proposed runway footprint

Version 0.3.1 automatically measures and previews the starting runway when
its pavement mesh loads. With the game closed, install
the DLL and set `Enabled = true` under `[RunwayPreview]` in the plugin config.
If that section is missing, launch once with the new DLL to generate it.
At Base, look for the cyan outline and the “Preview saved” message. The
plugin saves the pavement measurements under
`BepInEx/BackCountryOperations/runways`. **F5** reruns the automatic survey.

The first live 0.3.1 survey measured the Base pavement at 450 × 30 world
units, centered on X = 0 between Z = −225 and Z = +225. Screening five
schema 2 captures found zero generated decoration colliders overlapping
the pavement. Beyond the positive-Z end, the two nearest tree collider
bounds in the extended 30-unit-wide runway strip begin about 93 and
142 world units past the threshold. These are candidates for the first
departure clearance preview.

If the automatic outline does not appear or needs correction, switch to an
elevated view with the pavement visible and use the mouse pointer to mark
three points:

1. **F6** marks the center of the pavement at the first runway end.
2. **F7** marks the center at the opposite end and draws the yellow centerline.
3. **F8** marks either side edge of the pavement, draws the cyan footprint,
   and saves the measurements under `BepInEx/BackCountryOperations/runways`.

Aim at visible pavement clear of the aircraft and props. The preview samples
the first solid surface under the pointer. Use **F6** to start over, **F8**
to refine the width, or **F9** to clear the current preview. The snapshot
probe can stay enabled for the same flight. Check that the cyan outline
follows the pavement before using the saved runway file for screening.

The preview output can be passed to the screening tool below. To author a
record by hand, use the world identity from the capture, the surveyed
threshold centers, and the full usable width. These coordinates are examples.

```json
{
  "worldAsset": "ExampleWorld",
  "worldVersionStartingFrom": 1,
  "thresholdA": { "x": 0, "z": 0 },
  "thresholdB": { "x": 100, "z": 0 },
  "width": 20
}
```

```powershell
.\Find-RunwayObstacles.ps1 -Runway 'path/to/runway.json' -Capture @('chunk1.json', 'chunk2.json')
```

Supply the captures for all chunks covering the rectangle. The report includes
active decoration collider bounds that touch or cross the rectangle in X/Z,
including obstacles whose centers fall outside. Review candidates in-game for
height and clearance. The tool works on captures and makes no game changes.

The Unity callback, generated captures, and runway measurements still need
live validation before the first clearance patch.

## Inland clearance flight test

Version 0.4.0 adds the first scenery change. Close the game, install the DLL,
and add or enable this section in the plugin configuration:

```ini
[InlandClearance]
Enabled = true
```

Load the same Career save at Base and depart inland normally. The log should
report `Base inland clearance released 2 of 2 surveyed trees.` on initial load
and each time the Base chunk regenerates. Keep the scenery probe enabled so
the same flight produces evidence for comparison with the stock capture.
The test targets the two surveyed tree positions in `LocalizedVersion`
(world version 24). Disable the setting and relaunch to restore stock scenery.

The first 0.4.0 flight confirmed that both target trees cleared at initial
load and again when the Base chunk regenerated. The Base chunk went from
176 stock placements to 174. Comparing the two modded captures showed that
all remaining positions and transforms matched. Some remaining models and
collider bounds varied across the reload.

## Rough-strip survey flight

Version 0.5.0 adds an automatic rough-strip survey for the inland region.
It looks for a 130 × 24 world-unit footprint on loaded terrain using
longitudinal grade, cross-strip samples, and a collision check. It marks the
first candidate with a green outline and a distance cue. The survey logs the
threshold coordinates and grade, or reports when no site meets the checks.
The candidate is a preview for a future authored strip.

Version 0.6.0 adds the first playable job. Install the DLL and load the same
save with scenery capture and inland clearance enabled. The scout is enabled
by default. Depart inland and press **F10** when the survey offer appears.
Land inside the green outline and hold the aircraft stopped for three
seconds. The marker then points back to Base. Take off, return, and hold
stopped on the Base runway for three seconds to complete the survey.

The mod uses the stock aircraft's ground check and measured velocity.
Acceptance, the verified strip stop, and completion are logged. A crash,
aircraft change, or position reset interrupts the attempt. The job is
session-only and does not award currency. The same flight also captures the
cleared trees at departure and return. If a survey offer never appears, the
log shows whether the scout found a candidate on the loaded terrain.

Starting in 0.6.1, a search with no candidate offers an inland flyover.
Press **F10** and fly through the waypoint 1,000 world units inland at
world elevation 350, then stop at Base to finish. The offer switches to
a rough-strip survey if a strip is found before acceptance. An accepted
flight keeps its original objective.

The first completed live 0.6.1 flyover logged acceptance, the inland
checkpoint, and completion at Base. Earlier interrupted attempts reset
and allowed reacceptance. This verifies the flyover loop. The rough-strip
landing variant still needs a surveyed site and a live landing test.

Version 0.6.2 expands the inland search and checks both 130 × 24 and
100 × 24 footprints. The log distinguishes footprints that pass the
terrain checks from those rejected by the obstacle check. The scout
continues after a completed flyover and offers a landing survey if it
finds a site. During an accepted flight it keeps the current objective.

### Combined 0.6.2 playtest

1. Load Base in the same test save and use an aircraft you already know.
   Depart inland and note how the cleared path and remaining hillside look
   from the cockpit.
2. Make a scouting pass inland and one lateral pass across the nearby
   valley. Leave the flyover offer unaccepted so the scout can keep
   evaluating newly loaded ground.
3. If a rough-strip offer appears, make a low pass to judge visibility and
   approach space. Accept with F10 and land only if the strip looks suitable
   for that aircraft. Stop for three seconds to test the checkpoint.
4. Return to Base. If you accepted the landing survey, stop on the paved
   runway for three seconds to test completion. Note any scenery that
   interferes with taxiing or turning around.

Use the game's log for launch, clearance, search results, and job transitions.
Record only pilot observations that the game cannot capture directly.

## BackCountry jobs

In Career, find the repair requirements beneath **The Wind Turbines** on
the flight map. Open **Load Cargo** while parked at Base and take one
**Runway Patch Kit**. Deliver it by stopping in the marked service
bay at the Wind Farm. The crate weighs three times as much as the medicine
and uses the same cargo space. Delivery automatically starts the runway
repair. The runway upgrade is saved with the Career.

The stock **Delivering Batteries** job follows the opening. **Gear Fit
Check** is listed under The Farm after that delivery. Land with wheeled gear,
taxi slowly, then come to a full stop. **Backspace** abandons an active bush
mission while the map is closed.

Valley Strip, its contracts, and the checkpoint routes are paused. Utility
and High-Lift flap kits remain selectable on compatible wings.

### Workshop and flight

Select either bush main strut and drag the triangle by the wheel to adjust
its length. The airframe mount stays in place. Both bush main struts are
fixed gear, including when older aircraft load with the former retraction
option.

Climb propeller and turbocharger appear as checkboxes next to the regular
engine settings. A climb propeller gives more pull at low speed and lowers
top speed. Reverse throttle stops at idle on piston engines.

Near the ground, wing drag drops gradually and returns to normal as the
aircraft climbs. The change applies through the game's wing physics, so
approach speed and the timing of the flare still matter.

Hold **B** to apply light wheel brakes. With a tailwheel and an elevator
fitted, nose-down input gets a small boost during a powered takeoff roll.

### Runway visuals and turnarounds

Version 0.21.0 gives the damaged Wind Farm runway worn threshold paint
alongside its existing cracks and repairs. Completing the runway repair
job reveals fresh threshold bars and aiming marks at both ends. The
finished runway attempts to fit a connected gravel turnaround beside each
end. It tries either side and fits the pad only where the pavement
transition, grade, and obstacle clearance pass.

Check the Wind Farm before and after repair, then taxi a test aircraft
through both turns.

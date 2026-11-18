# Aviassembly Atmosphere 0.15.0

[Download Aviassembly.Atmosphere.dll](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Atmosphere/Aviassembly.Atmosphere.dll) · [SHA256](SHA256.txt)

With the game closed, copy the DLL to `BepInEx/plugins/Aviassembly.Atmosphere/`. Press **O** for weather and HUD customization, or **F7** for gauges. A map pin or tracked objective activates GPS. On final approach, two white and two red slope lights indicate the nominal 3° path.


Manual weather takes effect when you press **Apply**. In heavy fog the ocean fades with distance, and water close to the aircraft remains visible.

Each paved runway end has a widening corridor cleared of generated trees and rocks. The end requiring less terrain work normally has a full MALSR with five outer flashers running toward the threshold. The opposite end normally has steady bars, and long runways with two clear approaches can have full systems in both directions. Lamp housings sit on crossarms or posts anchored to terrain or seabed.

The end markers sit outside the shoulders. They show green on arrival and red from the runway, with at least 60 game units clear down the center beyond each end. PAPI units have a solid hooded cabinet, a recessed lens and three ground-mounted footplates. The mod checks the four sightlines for fences and buildings, preferring the left shoulder and using the right when needed. Two white and two red indicate a 3° approach. More red means low, and more white means high.

A parked aircraft holds position in heavy crosswinds, and the ocean fog respects foreground depth. The GPS and approach cue give fuel warnings room to appear.

When you fly toward an approach gate from the wrong side, follow the **Rejoin** gates back to its inbound course. Guidance checks the turn against terrain and keeps your chosen runway and remaining gates. Nearby airport icons on the map show their runway numbers.

## Time of day

Press **O**, then **Time of day**. A new clock starts at noon. At **1×**, one real second advances the clock one game minute, so a full day takes 24 minutes of active play.

Choose **Dawn** (06:00), **Noon** (12:00), **Dusk** (18:00) or **Midnight** (00:00), or step an hour earlier or later. **Lock time** holds the current lighting while you fly. You can still select another hour while locked. **Unlock time** resumes from that hour. **Slower** and **Faster** select 0.25×, 0.5×, 1×, 2×, 4× or 8×. The panel shows how many real minutes a day will take at the selected speed.

Clock controls take effect immediately. The speed changes the clock independently of aircraft physics and weather. Time carries across flights and game restarts, and pauses during loading or a game pause. Flight lighting follows the clock, including the sun, terrain, water, clouds and fog. The lit runway fixtures remain visible at night.

At night the runway markings, aircraft and nearby terrain pick up soft moonlight. Rain and fog still obscure distant scenery, and grass by the runway darkens with the surrounding ground.

[Compare the same runway at midnight in heavy rain](Screenshots/night-runway-comparison.jpg).

## Flying the approach

The top cue shows your ground track (**TRK**), the bearing to the next gate (**BRG**), and the altitude for that gate. **Fly left 12 u** means move 12 game units left to regain the course. **High** and **low** compare your current altitude with the descent line at your position between gates. When you are outside the checked corridor, the cue shows the course to rejoin before giving vertical guidance again.

If you miss a gate, it stays active and the cue shows the heading to cross it. Open the arrow for **Go around** to climb and turn back toward the entry, or **Change approach** to select another arrival. Cancelling a search keeps your present gate. The return and the arrival each have their own gate count. At the starting Base, the return uses wider gates and favors gentler climbs and wider turns when terrain allows.

After crossing the threshold on the selected approach, the cue shows runway distance remaining. Touching down in the selected direction clears guidance once the aircraft settles, even if you skipped a gate. Flying beyond the runway leaves **Go around** available.

## Sandbox map and runway approaches

Click the stock **Map** button in Sandbox to see the full chart, including unexplored land. Pan or zoom to inspect it. Reopening the map brings the whole chart back into view. Career keeps its exploration settings.

In flight, click the small arrow below the upper right controls. Select an airport within 14 km, choose the runway by its inbound heading, then pick **Straight in**, **RNAV left** or **RNAV right**. In IFR weather, follow the bright gate through its face. A faint box previews the next gate, and the compass cue points toward an entry outside your view. Outer gates are about 55 to 58 units tall away from the starting Base. The last two tighten around the 3° final. A missed gate stays active so you can circle back. Choose **Go Around** for a terrain-checked climb and return, or **Clear** to stop guidance.

Airport icons appear within 300 units and custom markers appear in the flight view within 200 units. Nearby gates remain visible in IFR when boxes are enabled. Runway lights carry farther in fog and terrain still blocks their glow.

## Fog and instrument approaches

Approach boxes appear only in local IFR weather. They fade in as visibility drops and grow easier to see as you approach each gate. In clear air, the instrument cue gives the bearing and horizontal range to the next gate, its target altitude on the stock altitude scale, and your ground track. At low speed the cue switches to heading.

Open the approach dropdown and select **Advanced guidance**, then enable **Instruments only** to hide the boxes even in heavy fog. The choice is saved. Gate crossings, go-arounds and landing completion work in both modes.

Ocean tiles now join the fog pass as they stream into view. Rain renders in front of the fogged water, and the shoreline uses the native water surface height.

## Choosing an approach

For a first landing, choose the starting **Base**, pick the runway marked **Easiest**, and select **Straight in**. The Base has the largest boxes, with an outer gate about 72 units tall. Its entries limit changes of track to 17°. In open terrain the gates on its final are about 810 units apart.

Other airports use smaller boxes and can ask for more precise turns. Runway length, the terrain on each side and the runway direction shape the route. A ridge can shorten the straight final or send an RNAV entry through an adjacent valley. The planner checks the width between checkpoints and the boxes themselves, and the final two gates stay aligned with the runway on a 3° descent.

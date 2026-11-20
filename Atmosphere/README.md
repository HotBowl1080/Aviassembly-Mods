# Aviassembly Atmosphere 0.26.1

[Download Aviassembly.Atmosphere.dll](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Atmosphere/Aviassembly.Atmosphere.dll) · [SHA256](SHA256.txt)

With the game closed, copy the DLL to `BepInEx/plugins/Aviassembly.Atmosphere/`. Press **O** for weather and HUD customization, or **F7** for gauges. A map pin or tracked objective activates GPS. On final approach, two white and two red slope lights indicate the nominal 3° path.


## Feature overview

| Feature | What you can do |
| --- | --- |
| Weather and flight physics | Press **O** to choose from eight weather presets or enable automatic changes. Wind, gusts, air density and wet runway grip affect how the aircraft flies and handles. |
| Day and night | A day takes 24 minutes at **1×**. Change the speed, jump to a time of day or lock the clock. |
| Instruments and GPS | Press **F7** for attitude, airspeed, altitude, vertical speed, heading and ground track. Follow a map pin or mission objective and save custom HUD layouts. |
| Airport approaches | Choose a runway in Landing, or track a mission for automatic selection. Follow the checked approach, or choose **Go around** to climb and rejoin. |
| Approach boxes and lights | Fly through the bright box and use the faint one to anticipate the turn. Boxes default to IFR and can be enabled in clear weather. The final box lines up with the runway’s 3° PAPI slope. |
| Map | Open the full geographic chart in Sandbox. Airport icons are selectable across the chart, including unexplored areas in Career. Paved airports show their runway numbers. |


Manual weather takes effect when you press **Apply**. In heavy fog the ocean fades with distance, and water close to the aircraft remains visible.

Each paved runway end has a widening corridor cleared of generated trees and rocks. The end with more level ground normally has a full MALSR with five outer flashers running toward the threshold. The opposite end normally has steady bars, and long runways with two clear approaches can have full systems in both directions. Lamp housings sit on crossarms or posts anchored to terrain or seabed.

The end markers sit outside the shoulders. They show green on arrival and red from the runway, with at least 60 game units clear down the center beyond each end. PAPI units have a solid hooded cabinet, a recessed lens and three ground-mounted footplates. The mod checks the four sightlines for fences and buildings, preferring the left shoulder and using the right when needed. Two white and two red indicate a 3° approach. More red means low, and more white means high.

A parked aircraft holds position in heavy crosswinds, and the ocean fog respects foreground depth. The GPS and approach cue give fuel warnings room to appear.

If you miss an approach gate or cross it the wrong way, return to its entry side and cross it inbound. Paved airports show their runway numbers on the map.

Track a mission on the map. When its current objective is a runway airport within 14 km, guidance automatically chooses a runway and verifies the entry against terrain. Cargo missions follow the pickup and then the delivery airport. Arrival direction and wind influence the choice, and a player map pin still works independently.

Choose a runway manually to override the mission. **Clear** dismisses the current leg, and **Advanced guidance → Mission approaches** lets you turn automatic selection off.

The final box, threshold reference and four PAPI lights share a 3° line to the same aim point inside the runway. Two white and two red means on slope, more white means high and more red means low. The handoff distance varies with the terrain. A short final can use one larger box followed by PAPI guidance.

The planner checks the current world's terrain when selecting a runway. It can search for a different course or a higher entry if the direct course crosses terrain.

The native landscape keeps its generated elevations. Approach bars follow the ground surface, and rows that would project into the glide path are omitted.

## Time of day

Press **O**, then **Time of day**. A new clock starts at noon. At **1×**, one real second advances the clock one game minute, so a full day takes 24 minutes of active play.

Choose **Dawn** (06:00), **Noon** (12:00), **Dusk** (18:00) or **Midnight** (00:00), or step an hour earlier or later. **Lock time** holds the current lighting while you fly. You can still select another hour while locked. **Unlock time** resumes from that hour. **Slower** and **Faster** select 0.25×, 0.5×, 1×, 2×, 4× or 8×. The panel shows how many real minutes a day will take at the selected speed.

Clock controls take effect immediately. The speed changes the clock independently of aircraft physics and weather. Time carries across flights and game restarts, and pauses during loading or a game pause. Flight lighting follows the clock, including the sun, terrain, water, clouds and fog. The lit runway fixtures remain visible at night.

At night the runway markings, aircraft and nearby terrain pick up soft moonlight. Rain and fog still obscure distant scenery, and grass by the runway darkens with the surrounding ground.

[Compare the same runway at midnight in heavy rain](Screenshots/night-runway-comparison.jpg).

## Flying the approach

Open **Landing**, choose an airport and runway, then select **Prepare landing**. To select the V2 planner, open **Landing → Advanced guidance**. In V2, **Go around** checks a climb course and arms the same runway for rejoining after the climb.

On a low departure toward The Hospital, **Climb before joining** means a higher join has cleared the terrain survey. Use the altitude cue while the planner checks a join from your new position. Follow the turn cue once a join is ready.

The approach card shows your current altitude under **NOW** and the altitude to aim for along the current leg under **TARGET**. **GATE ALT** gives the next checkpoint's altitude. Turn instructions use your actual ground track. Near touchdown, the card gives runway height and vertical speed.

Open **Landing → Advanced guidance** and switch **Landing scoring** off to hide touchdown scores. The choice is saved. Press **F6** or **R3** to open the approach GPS. If F6 also starts your Performance 240 benchmark, use **Show GPS** in Landing.

## Sandbox map and runway approaches

Click the stock **Map** button in Sandbox to see the full chart, including unexplored land. Pan or zoom to inspect it. Reopening the map brings the whole chart back into view. Career keeps its exploration settings.

Open the Landing menu below the upper right controls. Select an airport and runway to prepare an approach. In IFR weather, fly through the bright gate inbound. A faint box previews the next gate when it is in range. The final gate leads to the runway's 3° PAPI slope.

Hover over any charted airport for mission details, then click it to track an available objective. In the flight view, airport markers appear within 300 units and custom markers within 200. Nearby gates remain visible in IFR when boxes are enabled. Runway lights carry farther in fog and terrain still blocks their glow.

## Fog and instrument approaches

By default, approach boxes appear in local IFR weather. To see them in any weather, open the top-right approach arrow, choose **Advanced guidance** and switch **Clear-weather boxes** on. They fade in as visibility drops and grow easier to see as you approach each gate. With boxes hidden, follow the bearing, altitude and steering cues on the approach card.

Open the approach dropdown and select **Advanced guidance**, then enable **Instruments only** to hide the boxes even in heavy fog. **Instruments only** takes priority in every weather condition. Both settings are saved, and switching modes keeps your current route and gate. Gate crossings, go-arounds and landing completion work with either display.

Ocean tiles now join the fog pass as they stream into view. Rain renders in front of the fogged water, and the shoreline uses the native water surface height.

## Choosing an approach

For a first landing, choose **Base**, pick the runway marked **Easiest**, and select **Prepare landing**. The Base has the largest boxes, with an outer gate about 72 units tall. Its entries limit changes of track to 15°. On the open Base RWY 36 final, the gates are 1,750 game units apart.

Other airports use smaller boxes and can ask for more precise turns. Runway length, the terrain on each side and the runway direction shape the route. A ridge can shorten the straight final or send an RNAV entry through an adjacent valley. The planner checks the width between checkpoints and the boxes themselves, and the last gate leads to the PAPI aim point on a 3° descent.

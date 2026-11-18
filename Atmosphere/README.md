# Aviassembly Atmosphere 0.12.1

[Download Aviassembly.Atmosphere.dll](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Atmosphere/Aviassembly.Atmosphere.dll) · [SHA256](SHA256.txt)

With the game closed, copy the DLL to `BepInEx/plugins/Aviassembly.Atmosphere/`. Press **O** for weather and HUD customization, or **F7** for gauges. A map pin or tracked objective activates GPS. On final approach, two white and two red slope lights indicate the nominal 3° path.


Manual weather takes effect when you press **Apply**. In heavy fog the ocean fades with distance, and water close to the aircraft remains visible.

Each paved runway end has a widening corridor cleared of generated trees and rocks. The end requiring less terrain work normally has a full MALSR with five outer flashers running toward the threshold. The opposite end normally has steady bars, and long runways with two clear approaches can have full systems in both directions. Lamp housings sit on crossarms or posts anchored to terrain or seabed.

The end markers sit outside the shoulders. They show green on arrival and red from the runway, with at least 60 game units clear down the center beyond each end. PAPI units have a solid hooded cabinet, a recessed lens and three ground-mounted footplates. The mod checks the four sightlines for fences and buildings, preferring the left shoulder and using the right when needed. Two white and two red indicate a 3° approach. More red means low, and more white means high.

## Sandbox map and runway approaches

Click the stock **Map** button in Sandbox to see the full chart, including unexplored land. Pan or zoom to inspect it. Reopening the map brings the whole chart back into view. Career keeps its exploration settings.

In flight, click the small arrow below the upper right controls. Select an airport within 14 km, choose the runway by its inbound heading, then pick **Straight in**, **RNAV left** or **RNAV right**. In IFR weather, follow the bright gate through its face. A faint box previews the next gate, and the compass cue points toward an entry outside your view. The outer gate is 50 units tall. The last two tighten around the 3° final. A missed gate stays active so you can circle back. Choose **Go Around** for a terrain-checked climb and return, or **Clear** to stop guidance.

Airport icons appear within 300 units and custom markers appear in the flight view within 200 units. Nearby gates remain visible in IFR when boxes are enabled. Runway lights carry farther in fog and terrain still blocks their glow.

## Fog and instrument approaches

Approach boxes appear only in local IFR weather. They fade in as visibility drops and grow easier to see as you approach each gate. In clear air, the instrument cue gives the bearing and horizontal range to the next gate, its target altitude on the stock altitude scale, and your ground track. At low speed the cue switches to heading.

Open the approach dropdown and select **Advanced guidance**, then enable **Instruments only** to hide the boxes even in heavy fog. The choice is saved. Gate crossings, go-arounds and landing completion work in both modes.

Ocean tiles now join the fog pass as they stream into view. Rain renders in front of the fogged water, and the shoreline uses the native water surface height.

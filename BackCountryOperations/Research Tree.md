# BackCountry Operations research tree

Draft 0.5 · November 21, 2026

## Career progression

Start with a light aircraft. **Parts research** spends scrap on airframes and hardware. **Technology** is a separate tree. Completing a named mission awards its blueprint and a one-time coin bonus. Fitting that blueprint to compatible equipment still costs coins. The cash below is a draft bonus on top of any normal mission payout.

| Mission milestone | Equipment needed to finish the job | Award | Bonus |
| --- | --- | --- | ---: |
| First Delivery | Starter aircraft and a paved destination | Tailwheel Steering | 35 coins |
| Short Strip Supply | Starter aircraft at a forgiving, compact strip | Utility Flaps | 90 coins |
| Loaded Short-Field Run | Utility Flaps and a representative load | High-Lift Flaps | 100 coins |
| Gear Fit Check | Stock gear, taxi and stop at The Farm | Adjustable Struts | 40 coins |
| Dirt Strip Resupply | Starter gear on a graded dirt strip | Big Tires | 90 coins |
| Remote Strip Supply | Big Tires on compatible fixed gear at a rougher strip | Heavy-Duty Shocks | 100 coins |
| Piston Performance Check | Fly a baseline leg to The Wind Turbines with an unmodified eligible piston engine | Turbocharger | 70 coins |
| Shoreline Supply | Wheeled landing at a coastal airstrip | Small pontoons become available in Parts research | — |
| Water Freight | Small pontoons on a light aircraft | Pontoons become available in Parts research | — |

First Delivery opens the initial branch missions. The follow-on routes lead from Short Strip Supply to Loaded Short-Field Run and from Dirt Strip Resupply to Remote Strip Supply. A mission only asks for hardware available before its reward.

## Technology

The mission awards each blueprint directly. Utility Flaps and High-Lift Flaps are two settings in the wing fitting menu on eligible wings. High-Lift replaces Utility on that wing and charges the difference in installed value. Fit availability depends on the wing's supported geometry and flap behavior. Verify which light stock wings the installed Flaps mod supports before making any mission depend on them.

| Technology | Blueprint mission | Fitting price |
| --- | --- | ---: |
| Tailwheel Steering | First Delivery | 25 per gear |
| Utility Flaps | Short Strip Supply | 40 per wing |
| High-Lift Flaps | Loaded Short-Field Run | 75 per wing |
| Adjustable Struts | Gear Fit Check | 10 shortened / 15 extended per gear |
| Big Tires | Dirt Strip Resupply | 40 per gear |
| Heavy-Duty Shocks | Remote Strip Supply | 65 per gear |
| Turbocharger | Piston Performance Check | 60 per engine |

The **Turbocharger** is a first-pass candidate for the Engine and Small propeller engine. Its role is to retain more thrust at altitude and during a loaded climb, with added mass and higher fuel burn under boost. Keep the baseline engine useful at low altitude. Verify performance on matched routes and keep this as an optional branch while missions are being balanced.

## Stock parts in the proposed tree

Each table groups parts by their proposed branch. Prefab names identify the stock parts. Prices are the serialized base prices in coins per unit. Research values are shown as regular scrap / advanced scrap. The stock column shows the installed values when the proposal changes them. **Starter** means unlocked on a new Career. **Starter research** means immediately available to research. Where the stock tree lists multiple parent links, the table records them as stock links and carries forward the game's eligibility rule. A plus sign in a changed prerequisite means both conditions are required.

### Starter airframe

| Part | Proposed prerequisite | Scrap / advanced | Coins | Stock difference |
| --- | --- | ---: | ---: | --- |
| WoodEngine | Starter | 0 / 0 | 150 | — |
| BiplaneWing | Starter | 0 / 0 | 75 | — |
| TailWing | BiplaneWing | 0 / 0 | 15 | — |
| BiplaneCockpit | Starter | 0 / 0 | 150 | — |
| BikeWheel | Starter | 0 / 0 | 25 | — |
| SmallRearWheel | Starter | 0 / 0 | 25 | — |

### Utility propulsion

| Part | Proposed prerequisite | Scrap / advanced | Coins | Stock difference |
| --- | --- | ---: | ---: | --- |
| Engine | WoodEngine | 10 / 0 | 300 | — |
| SmallPropEngine | Engine | 10 / 0 | 400 | 15 / 0 scrap |
| TurboProp | SmallPropEngine | 25 / 5 | 650 | — |
| PropfanEngine | TurboProp | 25 / 0 | 1000 | — |

### Wings and flight controls

| Part | Proposed prerequisite | Scrap / advanced | Coins | Stock difference |
| --- | --- | ---: | ---: | --- |
| Wing | BiplaneWing | 10 / 0 | 150 | — |
| ElectronWing | Wing | 15 / 0 | 500 | — |
| StraightWing | ElectronWing | 20 / 0 | 500 | — |
| GliderWing | Wing | 10 / 0 | 750 | Prerequisite Wing / SmallBalloon, 15 / 0 scrap, 1000 coins |
| SmallRecreationalWing | GliderWing | 15 / 0 | 500 | — |
| Tailwing03 | TailWing | 5 / 0 | 75 | — |
| Tailwing04 | Tailwing03 | 10 / 0 | 125 | — |
| Tailwing02 | Tailwing04 | 15 / 0 | 250 | 15 / 1 scrap |
| SmallCanard | Tailwing02 | 15 / 0 | 125 | — |
| Airbreak | Starter research | 5 / 0 | 50 | — |
| Parachute | Airbreak | 10 / 0 | 200 | — |

### Cabins and fuel

| Part | Proposed prerequisite | Scrap / advanced | Coins | Stock difference |
| --- | --- | ---: | ---: | --- |
| Cockpit | BiplaneCockpit | 5 / 0 | 175 | — |
| PrivateJetCockpit | Cockpit | 10 / 0 | 250 | — |
| BigCockpit | PrivateJetCockpit | 25 / 5 | 750 | — |
| JetlineCockpit | BigCockpit | 50 / 0 | 1000 | — |
| HugeCockpit | JetlineCockpit | 50 / 0 | 1200 | — |
| GliderCockpit | Cockpit | 5 / 0 | 75 | — |
| SmallPlaneCockpit | GliderCockpit | 5 / 0 | 200 | — |
| SmallFuelTank | Starter | 0 / 0 | 100 | Prerequisite Starter research, 5 / 0 scrap |
| ExternalFuelTank | SmallFuelTank | 10 / 0 | 200 | — |
| BigExternalFuelTank | ExternalFuelTank | 15 / 5 | 800 | — |
| FuelTank | BigExternalFuelTank | 25 / 0 | 1000 | — |

### Landing gear and floats

| Part | Proposed prerequisite | Scrap / advanced | Coins | Stock difference |
| --- | --- | ---: | ---: | --- |
| OldSteelWheel | BikeWheel / SmallRearWheel | 5 / 0 | 50 | — |
| BigOldWheel | OldSteelWheel | 10 / 0 | 75 | — |
| RearWheel | SmallRearWheel | 5 / 0 | 75 | 10 / 0 scrap, 100 coins |
| AeroWheel | RearWheel | 15 / 0 | 75 | — |
| AeroWheelLong | BigOldWheel / AeroWheel | 15 / 0 | 75 | — |
| TinyRetractableGear | OldSteelWheel | 5 / 0 | 150 | Prerequisite BigOldWheel, 15 / 0 scrap |
| SmallRetractableGear | TinyRetractableGear | 10 / 0 | 200 | 15 / 0 scrap |
| Wheel03 | SmallRetractableGear | 15 / 0 | 300 | 30 / 0 scrap |
| BigRetractableGear | Wheel03 | 20 / 0 | 500 | 40 / 0 scrap |
| Small pontoons | SmallRearWheel + Shoreline Supply | 5 / 0 | 100 | Prerequisite SmallRearWheel, 10 / 0 scrap |
| Pontoons | Small pontoons + Water Freight | 10 / 0 | 150 | Prerequisite Small pontoons, 15 / 0 scrap |

#### Gear upgrades

Select a wheel and drag a **stock-style axial editor handle** under the mounting point, using the same selection, axis projection, preview, and apply behavior as the fuselage tools. Drag up to shorten the gear and down to extend it along the local strut axis. Keep the airframe attachment fixed. Move the axle, tire contact point, and supporting geometry together. Use the **Adjustable Struts** blueprint to unlock the handle.

Use stock length as the zero-cost detent. The provisional range is **−35% to +12% of stock deployed strut length**. Any shortened setting costs 10 coins per gear and any extended setting costs 15. Quote the mirrored-set price during the drag and apply it once on release. Undo should restore the geometry and money together. Keep the gear settings panel for tires, shocks, and steering.

Big Tires add about 25% to tire radius and increase mass and drag. Heavy-Duty Shocks improve touchdown tolerance and load capacity with a mass penalty. Tune both through loaded landings and taxi tests.

| Gear family | Length | Big Tires | Heavy-Duty Shocks |
| --- | --- | --- | --- |
| Bike Wheel | Stock | — | — |
| Small Rear Wheel | Adjustable | — | — |
| Rear Wheel | Adjustable | — | — |
| Steel wheels, both Light wheel variants, and Big Wheel | Adjustable | Yes | Yes |

Use Steel wheels as the early utility chassis. Heavy-Duty Shocks follow the Big Tires mission. Bush gear stays fixed. The four stock retractable wheels keep their built-in action in the normal parts branch. Floats keep their own mission branch.

Test tire clearance, contact alignment, and full compression before fitting an adjusted strut.

Follow the installed Flaps mod's editor pattern. Apply confirmed upgrades to a mirrored set as one transaction. Charge the difference between old and new installed upgrade values in Career, with a credit on removal capped at the amount paid. Preview edits and apply them only after the entire set passes compatibility and balance checks. Store the options and paid values with each part for undo, mirror, duplication, save, and load. Older designs start with stock-length gear, stock tires and shocks, and their original retraction behavior. Show any incompatible saved or imported configuration before placement without silently removing paid options.

Prototype against the native wheel suspension, contact, and failure behavior. Test clearance through full travel, braking, side-loads, mirrored edits, and reloads before opening the upgrades in Career.

### High-speed and experimental aircraft

| Part | Proposed prerequisite | Scrap / advanced | Coins | Stock difference |
| --- | --- | ---: | ---: | --- |
| SmallJetEngine | Engine | 15 / 5 | 1000 | — |
| JetEngine | SmallJetEngine | 30 / 0 | 3000 | — |
| FighterJetEngine | JetEngine | 40 / 0 | 3500 | — |
| RamJet | FighterJetEngine | 40 / 0 | 6000 | — |
| RocketEngine | RamJet | 45 / 0 | 5000 | — |
| SmallJetWing | StraightWing | 25 / 5 | 800 | — |
| BigWing | SmallJetWing | 50 / 0 | 2500 | — |
| DeltaWing01 | StraightWing | 15 / 0 | 250 | — |
| DeltaWing02 | DeltaWing01 | 20 / 0 | 250 | — |
| JetCockpit | SmallPlaneCockpit | 5 / 0 | 150 | — |
| SmallLargeCockpit | JetCockpit | 5 / 0 | 100 | — |
| JetCockpit02 | SmallLargeCockpit | 5 / 0 | 200 | — |
| AreoDynamicCockpit | JetCockpit02 | 20 / 0 | 250 | — |

### Electrical and specialty aircraft

| Part | Proposed prerequisite | Scrap / advanced | Coins | Stock difference |
| --- | --- | ---: | ---: | --- |
| SmallBatteries | Starter research | 5 / 0 | 250 | 0 / 5 scrap |
| BigBatteries | SmallBatteries | 5 / 0 | 500 | — |
| ElectricEngine | Engine + SmallBatteries | 10 / 0 | 400 | Prerequisite Engine, 15 / 5 scrap |
| BigElectricEngine | ElectricEngine | 20 / 0 | 1000 | — |
| SolarPanel | BigBatteries | 5 / 0 | 200 | — |
| AirTurbine | SolarPanel | 5 / 0 | 100 | — |
| RotationJoint | Starter research | 5 / 0 | 50 | Prerequisite SmallBatteries |
| Decoupler | RotationJoint | 5 / 0 | 50 | — |
| SmallBalloon | Starter research | 5 / 0 | 100 | — |
| BigBalloon | SmallBalloon | 10 / 0 | 200 | — |
| HelicopterEngineSmall | Engine | 15 / 5 | 1000 | Prerequisite Engine / SmallBalloon, 10 / 5 scrap |
| HelicopterEngine | HelicopterEngineSmall | 20 / 0 | 1500 | — |
| HugePropEngine | HelicopterEngine | 20 / 5 | 1000 | — |
| SmallHelicopterCockpit | PrivateJetCockpit | 10 / 0 | 250 | — |
| HelicopterCockpit | SmallHelicopterCockpit | 10 / 0 | 350 | — |


## Next parts and fittings to review

| Proposed part | Placement | Job in Career |
| --- | --- | --- |
| J-3 Cub cockpit | Early cabin alongside the Cub Utility Wing | Narrow, light tandem cabin for surveys and one-crate work. Attach the stock procedural body sections behind it. |
| A-65-style piston engine | Early propulsion after Wood Engine | Light, economical Cub engine with decent low-speed pull and less loaded climb and cruise performance than the stock Engine. Support the existing Climb propeller fitting. |
| 172 cabin | Utility cabins after the stock Cockpit | Wider cabin with more usable cargo room. Pair it with the 172 Utility Wing and a stock Engine or Small propeller engine for loaded Valley runs. |
| Fixed utility nose strut | Utility landing gear near the 172 cabin | Steerable nose gear for tricycle builds. Give it less rough-field capacity than the bush main struts. |

| Proposed fitting | Compatible parts | Payoff and trade-off |
| --- | --- | --- |
| Constant-speed propeller | Stock Engine and Small propeller engine | Later option for loaded climbs and longer cruise legs. Replace the climb prop when fitted and add cost and mass. |

Build the Cub cockpit and light engine first. Test a complete Cub against the one-crate Valley Supply Trial and a 172 against the two-crate follow-up before setting research and purchase prices.

### Valley Strip and workshop fittings

| Technology | Eligible parts | Draft unlock | Fitting price |
| --- | --- | --- | ---: |
| Vortex Generators | Cub Utility Wing and 172 Utility Wing | Complete a short handling check at Valley Strip after the first supply run. The check uses a standard aircraft and awards the blueprint plus 35 coins. | 15 per wing |
| Fixed Leading-Edge Slot | Cub Utility Wing and 172 Utility Wing | Complete the existing two-crate Loaded Short-Field Run. Award the slot and High-Lift Flaps blueprints with one shared completion bonus, currently drafted at 100 coins. | 50 per wing |
| Upgraded Brakes | Covered Bush Gear Strut and Bush Gear Strut with Shocks | Finish the existing Gear Fit Check taxi and stop at The Farm. Use its proposed 40-coin bonus for the brake pair. | 20 per main strut |

The handling check reuses the Valley approach and cargo bay. Ask for a stable approach, landing, and stop with generous limits tested on both reference aircraft. The two-crate Valley run and Farm taxi check must also be completable with the fittings the player has before the reward. Keep the new blueprints optional for later work until flight tests identify a job that benefits from them.

**Wing fittings.** Add a Leading Edge choice in the existing Cub and 172 wing settings, with Stock, Vortex Generators, and Fixed Slot. Keep the trailing-edge flap control separate. Allow one leading-edge choice per wing and test it with both flap kits. Use small paired fins on the upper wing for vortex generators. Model the fixed slot as a stationary piece with a visible gap ahead of the wing and no cockpit control. Give vortex generators a modest improvement in near-stall behavior. The fixed slot should support a lower usable approach speed under load and carry the larger cruise penalty. The FAA's fixed-slot diagram is the geometry reference. [FAA Flight Controls handbook](https://www.faa.gov/sites/faa.gov/files/pilots/pilot_handbook.pdf)

Aviassembly's `Wing.ApplyLift` uses a sine-based lift response, so there is no single stock stall-angle field to tune. Start with a bounded change to the high-angle lift response for fitted wings. Keep the low-angle lift slope close to stock and give the slot measurable cruise drag. Use the same response for flight and any lift preview that reports the fitted wing's performance. Keep existing flap incidence, flap drag, and ground effect working together without restoring or multiplying the same value twice. Judge vortex generators by the approach and recovery behavior in test flights before adding any separate roll effect.

**Brakes.** Add an Upgraded Brakes checkbox beside Balloon tires and Rudder steering on the two fixed main struts. Keep B as the pedal input. Expand the existing contact-only braking force for fitted main gear, bounded by tire grip and wheel load. Leave rolling friction, rudder steering, and tailwheel behavior alone. Use stock grip or measured surface behavior to tune paved and grass rollout separately. Keep weak braking on an unloaded wheel, and let weight transfer preserve the risk of a nose-over.

**Fitting and saves.** Extend the existing wing and wheel variants so the leading-edge selection survives alongside flap type and brake state survives alongside length, tire, and steering settings. Confirm a mirrored edit as one transaction, with the total cost shown before applying it. Save named blueprint rewards once per Career, restore settings when a design is loaded, and preserve stock settings on older designs. Test a load/import with a fitting the Career has yet to earn, and test an unaffordable mirrored change before releasing the editor UI.

**Flight checks.** Hold aircraft, fuel, payload, weather, and route constant. Compare clean wings, vortex generators, and fixed slots with flaps retracted, then repeat with Utility and High-Lift flaps. Record minimum controllable approach speed, descent recovery, runway use, and cruise performance. Compare baseline and upgraded brakes from the same touchdown speed on pavement and grass. Record stopping distance, straight tracking, and nose-over tendency with and without balloon tires. Verify that B still works on the stock setup.

### Cargo capacity

Use the stock fuselage volume for cargo space. Aviassembly already recalculates that space from the procedural body and checks it against each cargo unit in inventory. The proposed 172 cabin should contribute a normal amount of native cargo volume based on its size. Keep the Cub narrow and design the one-crate and two-crate jobs around their actual volume, loading, and takeoff performance.

Improve the Valley briefing first. Show units required, free space, and cargo weight before the player leaves Base, plus the space shortfall if the load will not fit. If later missions introduce fragile freight as a recurring problem, consider a tie-down fitting that reduces damage from ordinary bumps. Give it a small weight or space cost and preserve damage from hard impacts. Seat removal and a generic capacity multiplier can stay out of this pass.

## Balance against actual earnings

- Career opens with **650 coins, zero scrap, and zero advanced scrap**. One stock wood engine, one biplane cockpit, two biplane wings, one tailwing, two bike wheels, and one small rear wheel total **540 coins** at listed prices. That leaves **110 coins before the body and any separate fuel or cargo hardware**. Fly and price a complete starter build before making the small fuel tank essential. Its draft change removes the research cost.
- The starting island contains **25 pickup scrap**, at 5 per pickup. Authored rewards include **10 + 15 scrap** for the first two Hospital contracts, **5 scrap** for the Farm's tools job, and **20 scrap each** at Spring Hills and Maplewood. Weather Station awards **25 advanced scrap**. Keep advanced scrap out of the first backcountry branches.
- Check actual net coins from each early job on Normal and Hard. Authored delivery pay scales to 85% on Hard and depends on cargo and nearest-supplier distance. Aim for First Delivery to cover the 25-coin Tailwheel Steering upgrade plus the next flight's costs. Aim for the short-field route to afford the 80-coin flap pair. The rough-field route needs 180 coins and 5 scrap for a player buying the Steel wheels and fitting Big Tires from scratch. Verify that players with a compatible gear pair can buy the 80-coin tire upgrade after Dirt Strip Resupply.
- Run a fresh Career through each branch and test a partially completed delivery, a reload, and an existing save with purchased stock floats. Partial deliveries should leave the gate locked until the mission is complete. Carry forward existing unlocks and installed parts on older saves. Use named completion records so generated jobs cannot satisfy these gates.
- Revisit prices when cargo requirements, route payouts, or the starter build change. If an early job requires upgrading first, adjust the cargo or runway requirement, the payout, or the branch order based on that flight test.

Stock baseline comes from the installed Career scene, research tree, part prefabs, and LocalizedVersion mission assets. The stock tables cover the 71 serialized research nodes. Parts outside those nodes, including procedural pieces, are outside this pricing pass. The proposed mission payouts, technologies, and prices are for review and have not been applied to the game.

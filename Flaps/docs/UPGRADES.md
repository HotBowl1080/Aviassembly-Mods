# Paid wing upgrades

Flap Mod 1.0.0 adds separate Flaps and Spoilers purchases to the wing settings menu.

## Prices and eligibility

| Wing | Flaps | Spoilers |
| --- | --- | --- |
| Big Wing | 25% of the wing's base price | 25% of the wing's base price |
| Straight Wing, Small Recreational Wing, Glider Wing, Electron Wing, Small Jet Wing, Delta Wing 02, Wing | 25% of the wing's base price | Not offered |
| Delta Wing 01, Tail Wing, Tailwing 02, Tailwing 03, Tailwing 04 | 25% (stock steering surface also deploys as flap) | Not offered |
| Biplane Wing, Small Canard, Wood Wing | Not offered | Not offered |

Each option uses the unmodified wing price. Buying a second option does not increase its price by charging a percentage of an already upgraded wing.

For a $1,000 Big Wing, flaps cost $250 and spoilers cost $250. Both together make that wing worth $1,500. A matching pair costs $500 for flaps and another $500 for spoilers.

## Existing wing menu

Open the same wing settings menu previously used to enable flaps. Its Flaps and, on eligible wings, Spoilers rows show the cost to add the option or the refund for removing it. Deselect the row to remove that upgrade. A spoiler-only Big Wing retains independent spoiler operation; purchasing flaps does not grant free spoilers.

When the editor's mirror mode is on, changing a setting also applies it to the corresponding wing. The row shows the combined pair price. The entire pair is checked before charging: insufficient funds or an unsupported mesh leave both wings unchanged. With mirror mode off, only the selected wing changes. A centreline wing is handled once.

## Campaign money and aircraft value

- Placed wings are charged immediately when an upgrade is added and refunded its original upgrade price when removed.
- An unplaced wing includes the options in its placement quote; cancelling an unpaid option does not produce money.
- The wing's native part price includes its purchased options. Aircraft design prices, native placement, resale and undo therefore include the upgrades.
- Repeatedly selecting an already enabled option does not charge again.
- Mirrored changes create one undo step. Existing options on a partner are not bought twice.
- Creative mode keeps the upgraded aircraft value while following the game's no-charge/no-refund behavior.
- Mirrored copies inherit the options and price; the game's normal placement charges for the new copy.

## Saved options

Flaps and spoilers are saved per wing in campaign saves, aircraft designs and undo snapshots. Loading restores the selected options and upgraded part values without charging twice. When replacing an aircraft design, the campaign balance changes by the difference between the two aircraft values, including upgrades. Old saves without upgrade records load with stock options off.

The extension is appended after the native save data. Native part and money records retain their original layout, and reading the upgrade extension restores the native reader's position before the game continues loading.

No flap speed limits, overspeed warnings, damage or automatic retraction are added. Extension speed remains unchanged in this update.

## Verification

The upgrade runner exercises the production transaction and persistence classes using small in-memory game adapters. Its checks cover exact affordability, separate options, mixed ownership, atomic mirrored purchases/refunds, creative mode, unpaid previews, copy prices, native-style resale and undo, legacy saves, campaign/design snapshot round-trips, unchanged native prefixes, reader cursor restoration and malformed records.

Hover over an unlocked saved aircraft in the load menu to see the flap and spoiler counts and the value of each. This reads the design without changing it. Stock designs show no extra tooltip. The game still calculates the amount due when replacing an aircraft.

The 1.0.0 build passed 168 transaction and save checks. The packaged DLL passed 77 checks in each of two isolated game installations, a fresh install and an upgrade from 0.25. These exercise repeat aircraft loads, rollback of an unaffordable design, the saved-aircraft tooltip, purchases and refunds on 13 wings, mirrored undo and forces during flap travel. The surface suite passed 496 checks. Separate compatibility runs restored designs written by the released 0.25 and 0.23.1 plugins. Run `Flaps/tests/Runtime/Run.ps1 -GameDir '<game folder>'` and `Flaps/tests/SurfaceRuntime/Run.ps1 -GameDir '<game folder>'` from the repository to repeat the game checks. The isolated players and generated designs stay under `Flaps/.work/`.

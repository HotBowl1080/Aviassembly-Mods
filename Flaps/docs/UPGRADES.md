# Paid wing upgrades

Flap Mod v0.24 provides separate paid Flaps and Spoilers options to the existing wing settings menu.

## Prices and eligibility

| Wing | Flaps | Spoilers |
| --- | --- | --- |
| Big Wing | 25% of the wing's base price | 25% of the wing's base price |
| Straight Wing, Small Recreational Wing, Glider Wing, Electron Wing, Small Jet Wing, Delta Wing 02, Wing | 25% of the wing's base price | Not offered |
| Other wings | Not offered yet | Not offered yet |

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

The combined DLL also compiles against the installed game, and its 13 Harmony targets and injected fields match the installed assemblies. Geometry and force regression checks still pass. This is not an in-game campaign/UI test: verify the menu, native mirror matching, purchases, refunds, undo and reload on a test aircraft before treating the candidate as fully validated in-game.

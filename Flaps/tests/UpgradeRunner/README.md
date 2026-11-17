# Upgrade transaction and persistence checks

From the repository root:

```text
dotnet run --project Flaps/tests/UpgradeRunner
```

This runner compiles the production economy, transaction, mirror-group and save classes with small in-memory game adapters. It does not change a campaign balance or user save. Temporary file checks use a newly created test file and remove it afterward.

The checks cover independent 25% upgrades, exact affordability, atomic paired changes and rollbacks, existing/missing/centreline mirror partners, partial ownership, placed/unplaced copies, refunds, creative mode, resale and undo value changes, campaign/design snapshot records, legacy saves, native byte-prefix and reader-cursor preservation, and malformed metadata.

These adapters do not validate Unity UI rendering, native geometric mirror matching, actual campaign loading, or runtime mesh setup. Build against the installed game and test those paths in-game separately. The geometry runner covers the actual generated Straight Wing mesh and force model.

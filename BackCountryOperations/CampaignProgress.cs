using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MachWorks.BackCountryOperations
{
    [Flags]
    internal enum ValleyProgress
    {
        None = 0,
        FirstSupply = 1,
        LoadedSupply = 2,
        WindRunwayRepair = 16,
        FarmGearCheck = 32,
        MaplewoodDrains = 64,
        PumpReturn = 128,
        All = FirstSupply | LoadedSupply | WindRunwayRepair | FarmGearCheck | MaplewoodDrains | PumpReturn,
    }

    internal sealed class CampaignProgress : IDisposable
    {
        internal const string RepairCargoName = "Runway Patch Kit";
        // Keep these bits above the milestone flags so older mod versions can still read progress.
        private const int CarryingPatchKit = 1 << 8;
        private const int CarryingDrainageKit = 1 << 9;
        private const int CarryingWornPump = 1 << 10;
        private static readonly AccessTools.FieldRef<CargoInventory, Dictionary<CargoType, int>> InventoryEntries =
            AccessTools.FieldRefAccess<CargoInventory, Dictionary<CargoType, int>>("currentCargo");
        private static readonly AccessTools.FieldRef<CargoInventory, float> InventoryVolume =
            AccessTools.FieldRefAccess<CargoInventory, float>("<CurrentVolume>k__BackingField");
        private readonly Harmony harmony = new Harmony(BackCountryOperationsPlugin.PluginId + ".valleycareer");
        private readonly ManualLogSource log;
        private static CampaignProgress current;
        private ValleyProgress completed;
        private CargoType valleyCargo, repairSupplies, drainageKit, wornPump;
        private Texture2D repairIcon;

        internal CampaignProgress(ManualLogSource log)
        {
            this.log = log;
            try
            {
                harmony.Patch(AccessTools.DeclaredMethod(typeof(GameManager), nameof(GameManager.StartGame)),
                    prefix: new HarmonyMethod(typeof(CampaignProgress), nameof(NewCareer)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(PersistentStorage), nameof(PersistentStorage.Save),
                    new[] { typeof(string) }), postfix: new HarmonyMethod(typeof(CampaignProgress), nameof(Save)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(PersistentStorage), nameof(PersistentStorage.Load),
                    new[] { typeof(string) }), postfix: new HarmonyMethod(typeof(CampaignProgress), nameof(Load)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(CargoInventory), nameof(CargoInventory.Save)),
                    prefix: new HarmonyMethod(typeof(CampaignProgress), nameof(SaveStockCargo)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.CheckForCompletion)),
                    prefix: new HarmonyMethod(typeof(CampaignProgress), nameof(AllowStockCompletion)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(StoryState), nameof(StoryState.QuestAvailable)),
                    prefix: new HarmonyMethod(typeof(CampaignProgress), nameof(AllowStockOffer)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(QuestFeedbackManager), nameof(QuestFeedbackManager.SelectQuest)),
                    prefix: new HarmonyMethod(typeof(CampaignProgress), nameof(AllowStockSelection)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(CargoInventoryUI), "Update"),
                    prefix: new HarmonyMethod(typeof(CampaignProgress), nameof(ShowCargoOffers)),
                    finalizer: new HarmonyMethod(typeof(CampaignProgress), nameof(RestoreAirportCargo)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(AirportInspector), nameof(AirportInspector.SetAirport)),
                    prefix: new HarmonyMethod(typeof(CampaignProgress), nameof(ShowMissionCargoButton)),
                    finalizer: new HarmonyMethod(typeof(CampaignProgress), nameof(RestoreAirportCargo)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(CargoInventory), nameof(CargoInventory.AddCargo)),
                    prefix: new HarmonyMethod(typeof(CampaignProgress), nameof(AllowMissionPickup)),
                    postfix: new HarmonyMethod(typeof(CampaignProgress), nameof(TrackTownPickup)));
                current = this;
            }
            catch (Exception error)
            {
                harmony.UnpatchSelf();
                log.LogWarning($"Campaign integration could not initialize: {error.Message}");
            }
        }

        internal static bool IsDone(ValleyProgress milestone) =>
            current != null && milestone != ValleyProgress.None && (current.completed & milestone) == milestone;

        internal static bool Available => current != null;

        private static bool AllowStockCompletion(Quest __instance) => !OpeningAirfields.HoldStockQuest(__instance);
        private static bool AllowStockOffer(Quest __0) => !OpeningAirfields.HoldStockQuest(__0);
        private static bool AllowStockSelection(Quest __0) => !OpeningAirfields.HoldStockQuest(__0);

        private static void ShowCargoOffers(bool ___airportUI, out (Airport Airport, CargoType[] Stock) __state)
        {
            __state = default;
            if (!___airportUI || !OpeningAirfields.CareerOpening)
                return;

            var manager = AirportManager.Instance;
            var plane = PlaneContainer.Instance;
            if (!manager || !plane || !plane.IsAtAirport())
                return;
            var airport = manager.GetClosestAirport(plane.transform.position);
            if (airport != null && OpeningAirfields.AtAirport(airport, plane.transform.position))
                __state = AddMissionOffer(manager, airport);
        }

        private static void ShowMissionCargoButton(Airport __0, out (Airport Airport, CargoType[] Stock) __state)
        {
            var manager = AirportManager.Instance;
            __state = manager && __0 != null && OpeningAirfields.CareerOpening
                ? AddMissionOffer(manager, __0) : default;
        }

        private static (Airport Airport, CargoType[] Stock) AddMissionOffer(AirportManager manager, Airport airport)
        {
            CargoType cargo = null;
            if (airport == manager.GetHomeBase())
            {
                if (!IsDone(ValleyProgress.WindRunwayRepair))
                    cargo = OpeningAirfields.RepairCargo(manager);
                else if (TownContracts.IsOffered(RoughStripScout.MaplewoodDrainsJob))
                    cargo = TownContracts.Cargo(manager, RoughStripScout.MaplewoodDrainsJob);
            }
            else if (OpeningAirfields.Matches(airport, TownContracts.MaplewoodPosition) &&
                TownContracts.IsOffered(RoughStripScout.PumpReturnJob))
                cargo = TownContracts.Cargo(manager, RoughStripScout.PumpReturnJob);

            var inventory = CargoInventory.Instance;
            if (!cargo || !inventory || inventory.GetCargoCount(cargo) != 0)
                return default;

            var stock = airport.cargoType;
            if (stock != null && Array.IndexOf(stock, cargo) >= 0)
                return default;
            var offers = new CargoType[(stock?.Length ?? 0) + 1];
            offers[0] = cargo;
            if (stock != null)
                Array.Copy(stock, 0, offers, 1, stock.Length);
            airport.cargoType = offers;
            return (airport, stock);
        }

        private static void RestoreAirportCargo((Airport Airport, CargoType[] Stock) __state)
        {
            if (__state.Airport != null)
                __state.Airport.cargoType = __state.Stock;
        }

        private static bool AllowMissionPickup(CargoInventory __instance, CargoType __0)
        {
            if (!__0 || current == null)
                return true;
            bool isRepair = __0 == current.repairSupplies;
            int townIndex = TownPickupIndex(__0);
            if (!isRepair && townIndex < 0)
                return true;
            if (!OpeningAirfields.CareerOpening || __instance.GetCargoCount(__0) != 0 ||
                (isRepair ? IsDone(ValleyProgress.WindRunwayRepair) : !TownContracts.IsOffered(townIndex)))
                return false;
            var plane = PlaneContainer.Instance;
            var manager = AirportManager.Instance;
            var pickup = manager ? isRepair ? manager.GetHomeBase() : TownContracts.Pickup(manager, townIndex) : null;
            return pickup != null && plane && plane.IsAtAirport() && OpeningAirfields.AtAirport(pickup, plane.transform.position);
        }

        private static int TownPickupIndex(CargoType cargo) => current == null || !cargo ? -1 :
            cargo == current.drainageKit ? RoughStripScout.MaplewoodDrainsJob :
            cargo == current.wornPump ? RoughStripScout.PumpReturnJob : -1;

        private static void TrackTownPickup(CargoInventory __instance, CargoType __0, bool __runOriginal)
        {
            int index = TownPickupIndex(__0);
            if (__runOriginal && index >= 0 && __instance.GetCargoCount(__0) == 1 && TownContracts.IsOffered(index))
                TownContracts.Track(AirportManager.Instance, index);
        }

        internal static void Complete(ValleyProgress milestone)
        {
            if (current != null)
                current.completed |= milestone;
        }

        internal static CargoType ValleyCargo(CargoType template)
        {
            if (current == null || !template)
                return null;

            if (!current.valleyCargo)
                current.valleyCargo = MakeCargo(template, "Valley supplies", 1f);

            return current.valleyCargo;
        }

        internal static CargoType RepairCargo(CargoType template)
        {
            if (current == null || !template)
                return null;
            if (!current.repairSupplies)
            {
                current.repairSupplies = MakeCargo(template, RepairCargoName, 3f);
                current.repairSupplies.name = RepairCargoName;
                current.repairIcon = LoadRepairIcon();
                if (current.repairIcon)
                    current.repairSupplies.icon = current.repairIcon;
            }
            return current.repairSupplies;
        }

        internal static CargoType TownCargo(CargoType template, bool drainage)
        {
            if (current == null || !template)
                return null;
            ref CargoType cargo = ref (drainage ? ref current.drainageKit : ref current.wornPump);
            if (!cargo)
            {
                string name = drainage ? "Drainage Kit" : "Worn Pump";
                cargo = MakeCargo(template, name, drainage ? .8f : 2.5f);
                cargo.name = name;
                cargo.cargoSpace = 15;
            }
            return cargo;
        }

        private static Texture2D LoadRepairIcon()
        {
            using var stream = typeof(CampaignProgress).Assembly.GetManifestResourceStream("BackCountryOperations.RunwayPatchKit.png");
            if (stream == null)
                return null;
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            var icon = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                { name = RepairCargoName, hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            if (icon.LoadImage(bytes.ToArray(), true))
                return icon;
            UnityEngine.Object.Destroy(icon);
            return null;
        }

        private static CargoType MakeCargo(CargoType template, string name, float mass)
        {
            var cargo = UnityEngine.Object.Instantiate(template);
            cargo.name = "BackCountry " + name;
            cargo.cargoName = name;
            cargo.weight *= mass;
            cargo.basePrice = 0f;
            cargo.fragile = false;
            cargo.expires = false;
            cargo.hideFlags = HideFlags.DontSave;
            return cargo;
        }

        // Keep the stock inventory format readable without the mod. The sidecar records mission cargo.
        private static bool SaveStockCargo(CargoInventory __instance, GameDataWriter __0)
        {
            if (current == null)
                return true;

            var stock = __instance.GetCurrentCargo();
            float volume = 0f;
            bool RemoveReserved(CargoType cargo)
            {
                if (!cargo || !stock.TryGetValue(cargo, out int units))
                    return false;
                stock.Remove(cargo);
                volume += units * cargo.cargoSpace;
                return true;
            }
            bool removed = RemoveReserved(current.valleyCargo);
            removed |= RemoveReserved(current.repairSupplies);
            removed |= RemoveReserved(current.drainageKit);
            removed |= RemoveReserved(current.wornPump);
            if (!removed)
                return true;

            __0.Write(stock.Count);
            __0.Write(Mathf.Max(0f, __instance.CurrentVolume - volume));
            foreach (var pair in stock)
            {
                __0.Write(AirportManager.Instance.CargoTypeToIndex(pair.Key));
                __0.Write(pair.Value);
            }
            return false;
        }

        internal static bool CanFit(BushFlapKit kit)
        {
            if (kit == BushFlapKit.None || !UsesBlueprints())
                return true;
            return kit == BushFlapKit.Utility
                ? IsDone(ValleyProgress.MaplewoodDrains) || IsDone(ValleyProgress.FirstSupply)
                : IsDone(ValleyProgress.PumpReturn) || IsDone(ValleyProgress.LoadedSupply);
        }

        internal static bool CanFitBalloon => !UsesBlueprints() || IsDone(ValleyProgress.FarmGearCheck);

        private static bool UsesBlueprints()
        {
            var mode = GameManager.Instance?.gameModeData;
            var world = ContinentManager.Instance?.worldAsset;
            return current != null && mode != null && !mode.creativeMode && !mode.disableAirports && !mode.unlockAll &&
                (!world || OpeningAirfields.IsSupported(world));
        }

        private static void NewCareer() => current.completed = ValleyProgress.None;

        // Store the mission bits beside the native save so vanilla and Flap Mod read the same aircraft/campaign file.
        private static string Sidecar(PersistentStorage storage, string name) =>
            Path.Combine(storage.SaveFilePath, name + ".plane.backcountry");

        private static void Save(PersistentStorage __instance, string __0)
        {
            if (string.IsNullOrEmpty(__0))
                return;
            var path = Sidecar(__instance, __0);
            var temporary = path + ".tmp";
            try
            {
                int flags = (int)current.completed;
                var inventory = CargoInventory.Instance;
                if (inventory)
                {
                    if (inventory.GetCargoCount(current.repairSupplies) > 0)
                        flags |= CarryingPatchKit;
                    if (inventory.GetCargoCount(current.drainageKit) > 0)
                        flags |= CarryingDrainageKit;
                    if (inventory.GetCargoCount(current.wornPump) > 0)
                        flags |= CarryingWornPump;
                }
                if (flags == 0 && !File.Exists(path))
                    return;
                File.WriteAllText(temporary, flags.ToString(CultureInfo.InvariantCulture));
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                current.log.LogWarning($"Could not save BackCountry mission progress: {error.Message}");
            }
        }

        private static void Load(PersistentStorage __instance, string __0)
        {
            current.completed = ValleyProgress.None;
            if (string.IsNullOrEmpty(__0))
                return;

            try
            {
                var path = Sidecar(__instance, __0);
                if (File.Exists(path) && int.TryParse(File.ReadAllText(path), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int flags))
                {
                    current.completed = (ValleyProgress)flags & ValleyProgress.All;
                    RestoreCargo(flags);
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                current.log.LogWarning($"Could not load BackCountry mission progress: {error.Message}");
            }
        }

        private static void RestoreCargo(int flags)
        {
            const int cargoFlags = CarryingPatchKit | CarryingDrainageKit | CarryingWornPump;
            var inventory = CargoInventory.Instance;
            var airports = AirportManager.Instance;
            if ((flags & cargoFlags) == 0 || !inventory || !airports)
                return;

            CargoType cargo = null;
            if ((flags & CarryingPatchKit) != 0 && !IsDone(ValleyProgress.WindRunwayRepair))
                cargo = OpeningAirfields.RepairCargo(airports);
            else if ((flags & CarryingDrainageKit) != 0 && !IsDone(ValleyProgress.MaplewoodDrains))
                cargo = TownContracts.Cargo(airports, RoughStripScout.MaplewoodDrainsJob);
            else if ((flags & CarryingWornPump) != 0 && !IsDone(ValleyProgress.PumpReturn))
                cargo = TownContracts.Cargo(airports, RoughStripScout.PumpReturnJob);
            if (!cargo || inventory.GetCargoCount(cargo) > 0)
                return;
            InventoryEntries(inventory)[cargo] = 1;
            InventoryVolume(inventory) += cargo.cargoSpace;
            // Native flight initialization applies the restored inventory's mass.
        }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            if (valleyCargo)
                UnityEngine.Object.Destroy(valleyCargo);
            if (repairSupplies)
                UnityEngine.Object.Destroy(repairSupplies);
            if (drainageKit)
                UnityEngine.Object.Destroy(drainageKit);
            if (wornPump)
                UnityEngine.Object.Destroy(wornPump);
            if (repairIcon)
                UnityEngine.Object.Destroy(repairIcon);
            if (current == this)
                current = null;
        }
    }
}


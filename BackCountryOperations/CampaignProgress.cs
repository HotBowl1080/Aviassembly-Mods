using System;
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
        All = FirstSupply | LoadedSupply | WindRunwayRepair,
    }

    internal sealed class CampaignProgress : IDisposable
    {
        internal const string RepairCargoName = "Runway Patch Kit";
        private readonly Harmony harmony = new Harmony(BackCountryOperationsPlugin.PluginId + ".valleycareer");
        private readonly ManualLogSource log;
        private readonly bool valleyJobs;
        private static CampaignProgress current;
        private ValleyProgress completed;
        private CargoType valleyCargo, repairSupplies;
        private Texture2D repairIcon;

        internal CampaignProgress(ManualLogSource log, bool valleyJobs = true)
        {
            this.log = log;
            this.valleyJobs = valleyJobs;
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
                    prefix: new HarmonyMethod(typeof(CampaignProgress), nameof(OfferRepairCargo)),
                    finalizer: new HarmonyMethod(typeof(CampaignProgress), nameof(RestoreAirportCargo)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(AirportInspector), nameof(AirportInspector.SetAirport)),
                    prefix: new HarmonyMethod(typeof(CampaignProgress), nameof(ShowBaseCargoButton)),
                    finalizer: new HarmonyMethod(typeof(CampaignProgress), nameof(RestoreAirportCargo)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(CargoInventory), nameof(CargoInventory.AddCargo)),
                    prefix: new HarmonyMethod(typeof(CampaignProgress), nameof(AllowRepairPickup)));
                current = this;
            }
            catch (Exception error)
            {
                harmony.UnpatchSelf();
                log.LogWarning($"Valley mission progress could not hook the native save: {error.Message}");
            }
        }

        internal static bool IsDone(ValleyProgress milestone) =>
            current != null && milestone != ValleyProgress.None && (current.completed & milestone) == milestone;

        internal static bool Available => current != null;

        private static bool AllowStockCompletion(Quest __instance) => !OpeningAirfields.HoldStockQuest(__instance);
        private static bool AllowStockOffer(Quest __0) => !OpeningAirfields.HoldStockQuest(__0);
        private static bool AllowStockSelection(Quest __0) => !OpeningAirfields.HoldStockQuest(__0);

        private static void OfferRepairCargo(bool ___airportUI, out (Airport Airport, CargoType[] Stock) __state)
        {
            __state = default;
            if (!___airportUI || !OpeningAirfields.CareerOpening || IsDone(ValleyProgress.WindRunwayRepair))
                return;

            var manager = AirportManager.Instance;
            var plane = PlaneContainer.Instance;
            var home = manager ? manager.GetHomeBase() : null;
            if (home == null || !plane || !plane.IsAtAirport() || !OpeningAirfields.AtAirport(home, plane.transform.position))
                return;
            __state = AddRepairOffer(manager, home);
        }

        private static void ShowBaseCargoButton(Airport __0, out (Airport Airport, CargoType[] Stock) __state)
        {
            var manager = AirportManager.Instance;
            __state = manager && __0 != null && __0 == manager.GetHomeBase() && OpeningAirfields.CareerOpening &&
                !IsDone(ValleyProgress.WindRunwayRepair) ? AddRepairOffer(manager, __0) : default;
        }

        private static (Airport Airport, CargoType[] Stock) AddRepairOffer(AirportManager manager, Airport home)
        {
            var cargo = OpeningAirfields.RepairCargo(manager);
            var inventory = CargoInventory.Instance;
            if (!cargo || !inventory || inventory.GetCargoCount(cargo) != 0)
                return default;

            var stock = home.cargoType;
            if (stock != null && Array.IndexOf(stock, cargo) >= 0)
                return default;
            var offers = new CargoType[(stock?.Length ?? 0) + 1];
            offers[0] = cargo;
            if (stock != null)
                Array.Copy(stock, 0, offers, 1, stock.Length);
            home.cargoType = offers;
            return (home, stock);
        }

        private static void RestoreAirportCargo((Airport Airport, CargoType[] Stock) __state)
        {
            if (__state.Airport != null)
                __state.Airport.cargoType = __state.Stock;
        }

        private static bool AllowRepairPickup(CargoInventory __instance, CargoType __0)
        {
            if (!__0 || current?.repairSupplies != __0)
                return true;
            var plane = PlaneContainer.Instance;
            var manager = AirportManager.Instance;
            var home = manager ? manager.GetHomeBase() : null;
            return home != null && plane && plane.IsAtAirport() && !IsDone(ValleyProgress.WindRunwayRepair) &&
                __instance.GetCargoCount(__0) == 0 && OpeningAirfields.AtAirport(home, plane.transform.position);
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

        // Active jobs reset when a save is loaded. Write the native inventory format with only
        // stock cargo records while leaving the aircraft's live cargo, space and weight untouched.
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
            if (kit == BushFlapKit.None || current == null || !current.valleyJobs)
                return true;
            var mode = GameManager.Instance?.gameModeData;
            var world = ContinentManager.Instance?.worldAsset;
            if (mode == null || mode.creativeMode || mode.disableAirports || mode.unlockAll ||
                (world && (world.name != "LocalizedVersion" || world.versionStartingFrom != 24)))
                return true;

            // In the workshop the world can be unloaded, but the current Career's saved flags still apply.
            return IsDone(kit == BushFlapKit.Utility ? ValleyProgress.FirstSupply : ValleyProgress.LoadedSupply);
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
                if (current.completed == ValleyProgress.None && !File.Exists(path))
                    return;
                File.WriteAllText(temporary, ((int)current.completed).ToString(CultureInfo.InvariantCulture));
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                current.log.LogWarning($"Could not save Valley mission progress: {error.Message}");
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
                    current.completed = (ValleyProgress)flags & ValleyProgress.All;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                current.log.LogWarning($"Could not load Valley mission progress: {error.Message}");
            }
        }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            if (valleyCargo)
                UnityEngine.Object.Destroy(valleyCargo);
            if (repairSupplies)
                UnityEngine.Object.Destroy(repairSupplies);
            if (repairIcon)
                UnityEngine.Object.Destroy(repairIcon);
            if (current == this)
                current = null;
        }
    }
}


using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MachWorks.BackCountryOperations
{
    [Flags]
    internal enum PistonFittings
    {
        None = 0,
        Turbo = 1,
        ClimbProp = 2
    }

    internal sealed class PistonEngineAddons : IDisposable
    {
        private const string TurboSuffix = "_BCOTurbo";
        private const string PropSuffix = "_BCOClimbProp";
        private const string CloneSuffix = "(Clone)";

        private readonly Harmony harmony = new Harmony(BackCountryOperationsPlugin.PluginId + ".pistonaddons");
        private readonly Dictionary<string, GameObject> variants = new Dictionary<string, GameObject>();
        private static PistonEngineAddons current;
        private ManualLogSource log;
        private GameObject templates;

        public static PistonEngineAddons Install(ManualLogSource log)
        {
            var addons = new PistonEngineAddons { log = log };
            try
            {
                current = addons;
                addons.Patch(typeof(PartPrefabs), "Awake", postfix: nameof(CreateVariants));
                addons.Patch(typeof(PartPrefabs), nameof(PartPrefabs.GetPartPrefab), postfix: nameof(ResolveVariant));
                addons.Patch(typeof(ResearchManager), nameof(ResearchManager.FindPart), prefix: nameof(ResolveResearchPart));
                addons.Patch(typeof(PartSettingsPanel), nameof(PartSettingsPanel.OpenSettings), postfix: nameof(AddSettingsPage));
                addons.Patch(typeof(Engine), "set_currentThrottle", prefix: nameof(LimitPistonThrottle));
                addons.Patch(typeof(Engine), nameof(Engine.UpdatePart), prefix: nameof(ApplyPerformance),
                    finalizer: nameof(RestorePerformance));
                addons.Patch(typeof(Engine), nameof(Engine.GetThrustAtSpeed), postfix: nameof(PreviewThrust));
                return addons;
            }
            catch (Exception error)
            {
                addons.Dispose();
                log.LogError($"Could not install piston engine add-ons: {error}");
                return null;
            }
        }

        private void Patch(Type type, string target, string prefix = null, string postfix = null, string finalizer = null)
        {
            harmony.Patch(AccessTools.DeclaredMethod(type, target),
                prefix: prefix == null ? null : new HarmonyMethod(typeof(PistonEngineAddons), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(PistonEngineAddons), postfix),
                finalizer: finalizer == null ? null : new HarmonyMethod(typeof(PistonEngineAddons), finalizer));
        }

        private static void CreateVariants(List<GameObject> ___allPartPrefabs)
        {
            if (current.templates)
                return;

            var templateRoot = new GameObject("BackCountry Piston Engine Templates");
            templateRoot.SetActive(false);
            Object.DontDestroyOnLoad(templateRoot);
            current.templates = templateRoot;

            try
            {
                foreach (var stock in ___allPartPrefabs)
                {
                    if (!stock || Supported(stock.name) == PistonFittings.None)
                        continue;

                    var engine = stock.GetComponentInChildren<Engine>(true);
                    if (!IsPiston(engine))
                        continue;

                    var part = stock.GetComponent<BuildingPart>();
                    current.CreateVariant(part, PistonFittings.ClimbProp);
                    if ((Supported(stock.name) & PistonFittings.Turbo) != 0)
                    {
                        current.CreateVariant(part, PistonFittings.Turbo);
                        current.CreateVariant(part, PistonFittings.ClimbProp | PistonFittings.Turbo);
                    }
                }
                current.log.LogInfo($"Registered {current.variants.Count} piston engine add-on configurations.");
            }
            catch (Exception error)
            {
                current.variants.Clear();
                Object.Destroy(templateRoot);
                current.templates = null;
                current.log.LogError($"Could not build piston engine add-ons: {error}");
            }
        }

        private void CreateVariant(BuildingPart stock, PistonFittings fittings)
        {
            string id = VariantId(stock.name, fittings);
            if (variants.ContainsKey(id))
                return;

            var clone = Object.Instantiate(stock.gameObject, templates.transform, false);
            clone.name = id;
            var part = clone.GetComponent<BuildingPart>();
            var engine = clone.GetComponentInChildren<Engine>(true);
            AttachFittings(part, engine, stock.name, fittings);
            variants.Add(id, clone);
        }

        private static PistonFittings Supported(string baseId)
        {
            switch (baseId)
            {
                case "WoodEngine":
                    return PistonFittings.ClimbProp;
                case "Engine":
                case "SmallPropEngine":
                    return PistonFittings.ClimbProp | PistonFittings.Turbo;
                default:
                    return PistonFittings.None;
            }
        }

        private static bool IsPiston(Engine engine) =>
            engine && !engine.electricEngine && !engine.helicopterRotor && !engine.useAfterburner;

        private static void LimitPistonThrottle(Engine __instance, ref float __0)
        {
            if (__0 >= 0f || !IsPiston(__instance))
                return;

            var part = __instance.GetComponentInParent<BuildingPart>();
            if (part && Supported(BaseId(part.name)) != PistonFittings.None)
                __0 = 0f;
        }

        internal static string VariantId(string baseId, PistonFittings fittings)
        {
            return baseId + ((fittings & PistonFittings.Turbo) != 0 ? TurboSuffix : "") +
                ((fittings & PistonFittings.ClimbProp) != 0 ? PropSuffix : "");
        }

        internal static string BaseId(string currentId)
        {
            string id = currentId.Replace(CloneSuffix, "");
            if (id.EndsWith(PropSuffix, StringComparison.Ordinal))
                id = id.Substring(0, id.Length - PropSuffix.Length);
            if (id.EndsWith(TurboSuffix, StringComparison.Ordinal))
                id = id.Substring(0, id.Length - TurboSuffix.Length);
            return id;
        }

        private static void ResolveVariant(string __0, ref GameObject __result)
        {
            if (!__result && current.variants.TryGetValue(__0.Replace(CloneSuffix, ""), out var variant))
                __result = variant;
        }

        private static void ResolveResearchPart(ref string __0)
        {
            if (!string.IsNullOrEmpty(__0) && current.variants.ContainsKey(__0.Replace(CloneSuffix, "")))
                __0 = BaseId(__0);
        }

        private static void AddSettingsPage(PartSettingsPanel __instance, PlanePart __0, bool __1)
        {
            if (__1 || !__instance.content || !(__0 is Engine engine) || !IsPiston(engine))
                return;

            var part = engine.GetComponentInParent<BuildingPart>();
            if (!part)
                return;

            string baseId = BaseId(part.name);
            var supported = Supported(baseId);
            if (supported == PistonFittings.None || !current.variants.ContainsKey(VariantId(baseId, PistonFittings.ClimbProp)))
                return;

            PistonEngineAddonPanel.Attach(__instance, part, engine, baseId, supported);
        }

        internal static PistonFittings GetFittings(Engine engine) =>
            engine.TryGetComponent<PistonEngineFitting>(out var state) ? state.Fittings : PistonFittings.None;

        internal static void SetFittings(BuildingPart part, Engine engine, string baseId, PistonFittings fittings)
        {
            fittings &= Supported(baseId);
            if (fittings != PistonFittings.None && !current.variants.ContainsKey(VariantId(baseId, fittings)))
                return;

            AttachFittings(part, engine, baseId, fittings);
        }

        private static void AttachFittings(BuildingPart part, Engine engine, string baseId, PistonFittings fittings)
        {
            var cloneSuffix = part.name.EndsWith(CloneSuffix, StringComparison.Ordinal) ? CloneSuffix : "";
            if (!engine.TryGetComponent<PistonEngineFitting>(out var state))
                state = engine.gameObject.AddComponent<PistonEngineFitting>();
            state.Apply(engine, fittings);
            part.gameObject.name = VariantId(baseId, fittings) + cloneSuffix;
        }

        private readonly struct EngineSnapshot
        {
            internal readonly bool Changed;
            internal readonly float Thrust;
            internal readonly float FuelConsumption;

            internal EngineSnapshot(Engine engine)
            {
                Changed = true;
                Thrust = engine.thrust;
                FuelConsumption = engine.fuelConsumption;
            }
        }

        private static void ApplyPerformance(Engine __instance, out EngineSnapshot __state)
        {
            __state = default;
            if (!__instance.rb || !__instance.TryGetComponent<PistonEngineFitting>(out var fitting) ||
                fitting.Fittings == PistonFittings.None)
                return;

            var performance = fitting.GetPerformance(__instance.transform.position.y, __instance.rb.linearVelocity.magnitude);
            __state = new EngineSnapshot(__instance);
            __instance.thrust *= performance.x;
            __instance.fuelConsumption *= performance.y;
        }

        private static void RestorePerformance(Engine __instance, EngineSnapshot __state)
        {
            if (__state.Changed)
            {
                __instance.thrust = __state.Thrust;
                __instance.fuelConsumption = __state.FuelConsumption;
            }
        }

        private static void PreviewThrust(Engine __instance, float __0, ref float __result)
        {
            if (__instance.TryGetComponent<PistonEngineFitting>(out var fitting))
                __result *= fitting.GetPerformance(__instance.rb ? __instance.transform.position.y : 0f, __0).x;
        }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            if (templates)
                Object.Destroy(templates);
            variants.Clear();
            current = null;
        }
    }

    internal sealed class PistonEngineFitting : MonoBehaviour
    {
        [SerializeField] private PistonFittings fittings;
        [SerializeField] private float originalWeight;
        [SerializeField] private float propReferenceSpeed;
        [SerializeField] private bool initialized;

        internal PistonFittings Fittings => fittings;
        internal float OriginalWeight => originalWeight;

        internal void Apply(Engine engine, PistonFittings value)
        {
            if (!initialized)
            {
                originalWeight = engine.weight;
                // Stock prop engines expose the best speed in the second key of the thrust curve.
                propReferenceSpeed = engine.maxSpeed != null && engine.maxSpeed.length > 2
                    ? Mathf.Max(24f, engine.maxSpeed[1].time)
                    : 45f;
                initialized = true;
            }

            fittings = value;
            float extraWeight = (value & PistonFittings.Turbo) != 0 ? .12f : 0f;
            if ((value & PistonFittings.ClimbProp) != 0)
                extraWeight += .025f;
            engine.weight = originalWeight * (1f + extraWeight);
        }

        internal Vector2 GetPerformance(float altitude, float speed)
        {
            float thrust = 1f;
            float fuel = 1f;
            if ((fittings & PistonFittings.Turbo) != 0)
            {
                float boost = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(160f, 1150f, altitude));
                thrust += .24f * boost;
                fuel += .22f * boost;
            }
            if ((fittings & PistonFittings.ClimbProp) != 0)
            {
                float cruise = Mathf.InverseLerp(propReferenceSpeed * .40f, propReferenceSpeed, speed);
                thrust *= Mathf.Lerp(1.16f, .84f, cruise);
            }
            return new Vector2(thrust, fuel);
        }
    }
}

namespace MachWorks.BackCountryOperations
{
    using UnityEngine.UI;

    internal sealed class PistonEngineAddonPanel : MonoBehaviour
    {
        private RectTransform stockContent, addonContent, layout;
        private PartSettingsPanel panel;
        private BuildingPart part;
        private Engine engine;
        private string baseId;
        private Toggle climbPropToggle, turboToggle;

        internal static void Attach(PartSettingsPanel panel, BuildingPart part, Engine engine, string baseId,
            PistonFittings supported)
        {
            var stock = panel.content;
            LayoutRebuilder.ForceRebuildLayoutImmediate(stock);
            var stockSize = stock.rect.size;
            var root = new GameObject("Piston engine settings", typeof(RectTransform));
            root.transform.SetParent(stock.parent, false);
            var addon = root.AddComponent<PistonEngineAddonPanel>();
            addon.panel = panel;
            addon.part = part;
            addon.engine = engine;
            addon.baseId = baseId;
            addon.layout = (RectTransform)root.transform;
            addon.stockContent = stock;
            addon.stockContent.SetParent(addon.layout, false);
            panel.content = addon.layout;
            addon.Build(supported, stockSize);
        }

        private void Build(PistonFittings supported, Vector2 stockSize)
        {
            float stockHeight = Mathf.Max(40f, stockSize.y);
            bool supportsTurbo = (supported & PistonFittings.Turbo) != 0;
            float addonHeight = supportsTurbo ? 157f : 79f;
            layout.sizeDelta = new Vector2(Mathf.Max(WorkshopSettingsUi.Width, stockSize.x), stockHeight + addonHeight);
            stockContent.anchorMin = stockContent.anchorMax = stockContent.pivot = new Vector2(.5f, .5f);
            stockContent.sizeDelta = stockSize;
            stockContent.anchoredPosition = Vector2.up * (addonHeight * .5f);
            var page = new GameObject("Engine add-ons", typeof(RectTransform));
            page.transform.SetParent(layout, false);
            addonContent = (RectTransform)page.transform;
            WorkshopSettingsUi.Place(addonContent, 0f, stockHeight + 8f, WorkshopSettingsUi.Width, addonHeight);
            climbPropToggle = WorkshopSettingsUi.Checkbox(addonContent, "Climb propeller", 0f,
                enabled => SetFitting(PistonFittings.ClimbProp, enabled));
            WorkshopSettingsUi.Label(addonContent, "More pull at low speed. Lower top speed.", 35f, 36f, detail: true);
            if (supportsTurbo)
            {
                turboToggle = WorkshopSettingsUi.Checkbox(addonContent, "Turbocharger", 78f,
                    enabled => SetFitting(PistonFittings.Turbo, enabled));
                WorkshopSettingsUi.Label(addonContent, "More power at altitude. Uses more fuel.", 113f, 36f, detail: true);
            }
            Refresh();
        }

        private void SetFitting(PistonFittings fitting, bool enabled)
        {
            var state = PistonEngineAddons.GetFittings(engine);
            var mirror = panel.partPlacer ? panel.partPlacer.GetMirrorPart(part) : null;
            var mirrorEngine = mirror && PistonEngineAddons.BaseId(mirror.name) == baseId
                ? mirror.GetComponentInChildren<Engine>(true) : null;
            PistonEngineAddons.SetFittings(part, engine, baseId, enabled ? state | fitting : state & ~fitting);
            if (mirrorEngine)
            {
                var mirrorState = PistonEngineAddons.GetFittings(mirrorEngine);
                var updated = enabled ? mirrorState | fitting : mirrorState & ~fitting;
                PistonEngineAddons.SetFittings(mirror, mirrorEngine, baseId, updated);
            }
            if (PlaneStorage.Instance)
                PlaneStorage.Instance.UpdateHistory();
            Refresh();
        }

        private void Refresh()
        {
            var fittings = PistonEngineAddons.GetFittings(engine);
            WorkshopSettingsUi.SetCheckbox(climbPropToggle, (fittings & PistonFittings.ClimbProp) != 0);
            if (turboToggle)
                WorkshopSettingsUi.SetCheckbox(turboToggle, (fittings & PistonFittings.Turbo) != 0);
        }
    }
}


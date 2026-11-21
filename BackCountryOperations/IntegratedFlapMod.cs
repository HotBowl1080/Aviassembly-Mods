using System;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MachWorks.BackCountryOperations
{
    internal sealed class IntegratedFlapMod : IDisposable
    {
        internal const string PluginId = "com.andrewstack.aviassembly.flaps";
        private const string Resource = "BackCountryOperations.AviassemblyFlaps.dll";
        private readonly Harmony bridge = new Harmony(BackCountryOperationsPlugin.PluginId + ".flapcontrols");
        private Component bundledComponent;
        private static Func<int> readStage;

        internal static bool HasSharedControls => readStage != null;
        internal static int Stage => readStage();

        internal static IntegratedFlapMod Install(GameObject host, ManualLogSource log)
        {
            var integration = new IntegratedFlapMod();
            try
            {
                Type pluginType;
                bool standalone = Chainloader.PluginInfos.TryGetValue(PluginId, out var plugin) && plugin.Instance;
                if (standalone)
                {
                    pluginType = plugin.Instance.GetType();
                }
                else
                {
                    using var source = typeof(IntegratedFlapMod).Assembly.GetManifestResourceStream(Resource)
                        ?? throw new InvalidOperationException("The bundled Flap Mod resource is missing.");
                    using var payload = new MemoryStream();
                    source.CopyTo(payload);
                    pluginType = Assembly.Load(payload.ToArray()).GetType("FlapsPlugin", throwOnError: true);
                    integration.bundledComponent = host.AddComponent(pluginType);
                    if (Harmony.GetPatchInfo(AccessTools.DeclaredMethod(typeof(Wing), nameof(Wing.UpdatePart)))
                        ?.Owners.Contains(PluginId) != true)
                        throw new InvalidOperationException("The bundled Flap Mod did not initialize its wing physics.");
                }

                var stage = AccessTools.PropertyGetter(pluginType, "stage");
                var selector = AccessTools.PropertyGetter(pluginType, "ShowSelector");
                if (stage != null && selector != null)
                {
                    integration.bridge.Patch(selector,
                        postfix: new HarmonyMethod(typeof(IntegratedFlapMod), nameof(ShowSelectorForBushWings)));
                    readStage = (Func<int>)stage.CreateDelegate(typeof(Func<int>));
                }
                log.LogInfo(standalone ? "Sharing flap controls with the installed Flap Mod." :
                    "Loaded the bundled Flap Mod for stock wing devices and shared flap controls.");
                return integration;
            }
            catch (Exception error)
            {
                integration.Dispose();
                log.LogError($"Could not initialize integrated Flap Mod: {error}");
                return null;
            }
        }

        private static void ShowSelectorForBushWings(PlaneContainer ___activePlane, FlyingUIController ___flightUI,
            ref bool __result)
        {
            if (!__result && ___activePlane && ___activePlane.FlightModeInitialized &&
                (!___flightUI || !___flightUI.UIOpen))
                __result = BushFlapControls.HasFlapsFor(___activePlane);
        }

        public void Dispose()
        {
            bridge.UnpatchSelf();
            readStage = null;
            if (bundledComponent)
            {
                new Harmony(PluginId).UnpatchSelf();
                Object.Destroy(bundledComponent);
            }
        }
    }
}

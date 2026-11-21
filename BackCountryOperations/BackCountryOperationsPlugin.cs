using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace MachWorks.BackCountryOperations
{
    [BepInPlugin(PluginId, PluginName, PluginVersion)]
    [BepInDependency(IntegratedFlapMod.PluginId, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class BackCountryOperationsPlugin : BaseUnityPlugin
    {
        public const string PluginId = "com.machworks.backcountryoperations";
        public const string PluginName = "BackCountry Operations";
        public const string PluginVersion = "0.21.2";

        private ConfigEntry<bool> diagnosticsEnabled;
        private SceneryProbe sceneryProbe;
        private InlandClearance inlandClearance;
        private TitleScreenBrand titleScreenBrand;
        private BushLandingGear bushLandingGear;
        private IDisposable bushWings;
        private IDisposable pistonAddons;
        private IntegratedFlapMod integratedFlaps;
        private CampaignProgress campaignProgress;

        private void Awake()
        {
            diagnosticsEnabled = Config.Bind(
                "Developer",
                "EnableDiagnostics",
                false,
                "Log additional startup and shutdown information.");

            Logger.LogInfo($"{PluginName} {Info.Metadata.Version} started. Aviassembly version {Application.version}.");
            titleScreenBrand = TitleScreenBrand.Install(Logger);
            integratedFlaps = IntegratedFlapMod.Install(gameObject, Logger);
            bushLandingGear = BushLandingGear.Install(Logger);
            bushWings = BushUtilityWings.Install(Logger);
            pistonAddons = PistonEngineAddons.Install(Logger);

            if (diagnosticsEnabled.Value)
            {
                Logger.LogInfo($"Diagnostics enabled. Unity version {Application.unityVersion}.");
            }

            sceneryProbe = SceneryProbe.Start(Config, Logger);
            var scoutEnabled = Config.Bind("RoughStripScout", "Enabled", true, "Enable BackCountry jobs and surveys in flight.").Value;
            inlandClearance = InlandClearance.Start(Config, Logger, valleyEnabled: false, windEnabled: scoutEnabled);
            if (Config.Bind("RunwayPreview", "Enabled", false, "Enable the runway marking tools. Restart to apply.").Value)
                gameObject.AddComponent<RunwayPreview>().Initialize(Logger);

            if (scoutEnabled)
            {
                campaignProgress = new CampaignProgress(Logger, valleyJobs: false);
                gameObject.AddComponent<RoughStripScout>().Initialize(Logger, valleyEnabled: false);
            }
        }

        private void OnDestroy()
        {
            sceneryProbe?.Dispose();
            inlandClearance?.Dispose();
            titleScreenBrand?.Dispose();
            bushLandingGear?.Dispose();
            bushWings?.Dispose();
            pistonAddons?.Dispose();
            integratedFlaps?.Dispose();
            campaignProgress?.Dispose();
        }

        private void OnApplicationQuit()
        {
            if (diagnosticsEnabled?.Value == true)
            {
                Logger.LogInfo($"{PluginName} received the application quit event.");
            }
        }
    }
}

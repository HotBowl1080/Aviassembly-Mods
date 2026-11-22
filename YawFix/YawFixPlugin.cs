using BepInEx;
using HarmonyLib;

namespace MachWorks.YawFix
{
    [BepInPlugin(PluginId, "Yaw Fix", "1.0.1")]
    public sealed class YawFixPlugin : BaseUnityPlugin
    {
        private const string PluginId = "com.machworks.yawfix";
        private Harmony harmony;

        private void Awake()
        {
            var updateThrottle = AccessTools.Method(typeof(PlaneController), "UpdateThrottle");
            var rudderInput = AccessTools.Field(typeof(PlaneController), "rudderInput");
            var smoothedInput = AccessTools.Field(typeof(PlaneController), "rudderInputSmooth");
            var inputVelocity = AccessTools.Field(typeof(PlaneController), "rudderInputVelocity");
            if (updateThrottle == null || rudderInput?.FieldType != typeof(float) ||
                smoothedInput?.FieldType != typeof(float) || inputVelocity?.FieldType != typeof(float))
            {
                Logger.LogWarning("Yaw Fix found an unsupported flight controls layout.");
                return;
            }

            harmony = new Harmony(PluginId);
            harmony.Patch(updateThrottle, postfix: new HarmonyMethod(
                typeof(YawFixPlugin), nameof(ClearReleasedRudder)));
            Logger.LogInfo("Yaw Fix enabled immediate rudder release.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        private static void ClearReleasedRudder(
            float ___rudderInput, ref float ___rudderInputSmooth, ref float ___rudderInputVelocity)
        {
            // Stock applies nonzero input directly, then eases it back to center on release.
            if (___rudderInput == 0f)
            {
                ___rudderInputSmooth = 0f;
                ___rudderInputVelocity = 0f;
            }
        }
    }
}

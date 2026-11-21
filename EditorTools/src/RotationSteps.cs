using System;
using HarmonyLib;
using UnityEngine;

namespace Aviassembly.EditorTools
{
    [HarmonyPatch(typeof(RotationGizmo))]
    internal static class RotationSteps
    {
        private static readonly int[] Steps = { 1, 5, 15, 30, 45, 60, 75, 90, 0 };
        private static readonly AccessTools.FieldRef<RotationGizmo, float> Increment =
            AccessTools.FieldRefAccess<RotationGizmo, float>("<snapIncrement>k__BackingField");
        private static readonly AccessTools.FieldRef<RotationGizmo, bool> Enabled =
            AccessTools.FieldRefAccess<RotationGizmo, bool>("snapping");

        [HarmonyPostfix, HarmonyPatch("Awake")]
        private static void Initialize(RotationGizmo __instance) => Set(__instance, 1);

        [HarmonyPrefix, HarmonyPatch(nameof(RotationGizmo.SetSnap))]
        private static bool Cycle(RotationGizmo __instance)
        {
            int index = Array.IndexOf(Steps, Mathf.RoundToInt(__instance.snapIncrement));
            Set(__instance, Steps[(index + 1) % Steps.Length]);
            return false;
        }

        private static void Set(RotationGizmo gizmo, int step)
        {
            Increment(gizmo) = step;
            Enabled(gizmo) = step > 0;
        }

        internal static float Apply(float angle, RotationGizmo gizmo)
        {
            int step = gizmo == null ? 1 : Mathf.RoundToInt(gizmo.snapIncrement);
            return step > 0 ? TransformMath.Snap(angle, step) : angle;
        }
    }

    [HarmonyPatch(typeof(RotationSnapButton))]
    internal static class RotationStepLabel
    {
        [HarmonyPostfix, HarmonyPatch("Start")] private static void StartLabel(RotationSnapButton __instance) => Refresh(__instance);
        [HarmonyPostfix, HarmonyPatch(nameof(RotationSnapButton.Press))]
        private static void Refresh(RotationSnapButton __instance)
        {
            int step = Mathf.RoundToInt(__instance.gizmo.snapIncrement);
            __instance.snapAngleText.text = step == 0 ? "Free" : step + "\u00b0";
            __instance.icon.texture = step == 0 ? __instance.angle360 : step == 90 ? __instance.angle90 : __instance.angle45;
        }
    }
}

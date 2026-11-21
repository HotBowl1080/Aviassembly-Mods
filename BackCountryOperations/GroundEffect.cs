using HarmonyLib;
using UnityEngine;

namespace MachWorks.BackCountryOperations
{
    internal static class GroundEffect
    {
        private const float MaximumHeight = 5f;
        private const float MaximumDragReduction = .18f;

        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.DeclaredMethod(typeof(Wing), "ApplyDrag"),
                prefix: new HarmonyMethod(typeof(GroundEffect), nameof(Apply)),
                finalizer: new HarmonyMethod(typeof(GroundEffect), nameof(Restore)));
        }

        private static void Apply(Wing __instance, Vector3 __1, out float? __state)
        {
            __state = null;
            var orientation = __instance.orientation;
            if (!orientation || !__instance.rb || __1.sqrMagnitude < 1f ||
                !__instance.rb.TryGetComponent<PlaneContainer>(out var aircraft))
                return;

            // Only the wing drag scales. Keep the stock gravity, lift, flap loads and wheel friction.
            if (!Physics.Raycast(orientation.position, Vector3.down, out var ground, MaximumHeight,
                    aircraft.groundedCheckLayerMask, QueryTriggerInteraction.Ignore) ||
                ground.rigidbody == __instance.rb || ground.normal.y < .65f)
                return;

            float reduction = Reduction(ground.distance, orientation.up.y);
            if (reduction <= 0f)
                return;

            __state = __instance.dragForce;
            __instance.dragForce *= 1f - reduction;
        }

        internal static float Reduction(float height, float verticalAlignment)
        {
            float proximity = 1f - Mathf.InverseLerp(.4f, MaximumHeight, height);
            float alignment = Mathf.Abs(verticalAlignment);
            return MaximumDragReduction * Mathf.SmoothStep(0f, 1f, proximity) * alignment * alignment;
        }

        private static void Restore(Wing __instance, float? __state)
        {
            if (__state.HasValue)
                __instance.dragForce = __state.Value;
        }
    }
}

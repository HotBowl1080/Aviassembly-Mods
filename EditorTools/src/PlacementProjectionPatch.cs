using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Aviassembly.EditorTools
{
    [HarmonyPatch]
    internal static class PlacementProjectionPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PartPlacer), "GetMouseWorldPosition");
            yield return AccessTools.Method(typeof(PartPlacer), "MovePart", new[] { typeof(BuildingPart) });
            yield return AccessTools.Method(typeof(PartPlacer), "MoveMirrorPart");
        }
        private static Camera InputCamera() => Plugin.Instance?.PlacementInputCamera ?? BuildingCamera.cam;
        internal static bool InMovingAssembly(BuildingPart owner, PartPlacer placer, BuildingPart solving)
        {
            if (owner == null) return false;
            bool Includes(BuildingPart root) => root != null && (owner == root || owner.transform.IsChildOf(root.transform) || root.IsChild(owner));
            return Includes(solving) || (placer.currentMovingPart != solving && Includes(placer.currentMovingPart)) ||
                (placer.currentMirrorPart != solving && Includes(placer.currentMirrorPart));
        }

        private static bool PlacementRay(Ray ray, out RaycastHit hit, float distance, int layerMask, PartPlacer placer, BuildingPart solving)
        {
            if (!Physics.Raycast(ray, out hit, distance, layerMask)) return false;
            if (!InMovingAssembly(hit.collider.GetComponentInParent<BuildingPart>(), placer, solving)) return true;
            // A moving collider can cover the valid center fuselage in the same view.
            hit = default;
            float nearest = float.PositiveInfinity;
            foreach (var next in Physics.RaycastAll(ray, distance, layerMask))
            {
                if (next.distance >= nearest || InMovingAssembly(next.collider.GetComponentInParent<BuildingPart>(), placer, solving)) continue;
                hit = next;
                nearest = next.distance;
            }
            return hit.collider != null;
        }

        private static bool RaycastNearPart(Ray ray, out RaycastHit hit, float distance, int layerMask, PartPlacer placer, BuildingPart solving)
        {
            if (Plugin.Instance?.MovingPlacedPart != true) return PlacementRay(ray, out hit, distance, layerMask, placer, solving);
            hit = default;
            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var original = AccessTools.PropertyGetter(typeof(BuildingCamera), "cam");
            var raycast = AccessTools.Method(typeof(Physics), nameof(Physics.Raycast),
                new[] { typeof(Ray), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int) });
            bool isMove = __originalMethod.Name == "MovePart";
            int raycasts = 0;
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(original)) { instruction.operand = AccessTools.Method(typeof(PlacementProjectionPatch), "InputCamera"); replaced++; }
                // In MovePart the first ray starts at the mouse. The second starts at
                // the part itself and can pull it away even when the mouse misses.
                if (instruction.Calls(raycast) && (isMove || __originalMethod.Name == "MoveMirrorPart"))
                {
                    raycasts++;
                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    instruction.operand = AccessTools.Method(typeof(PlacementProjectionPatch),
                        isMove && raycasts == 2 ? nameof(RaycastNearPart) : nameof(PlacementRay));
                }
                yield return instruction;
            }
            if (replaced != 1 || ((isMove || __originalMethod.Name == "MoveMirrorPart") && raycasts != 2)) throw new InvalidOperationException("Unsupported native placement camera layout");
        }
    }

    [HarmonyPatch(typeof(PartPlacer), nameof(PartPlacer.GetMirrorPart))]
    internal static class CenterPartMirrorPatch
    {
        private static void Postfix(PartPlacer __instance, BuildingPart __0, ref BuildingPart __result)
        {
            if (__result == null || __0 == null || __instance.currentMovingPart != __0) return;
            // A centerline part is its own reflection. Never pick up a nearby placed part as its partner.
            if (__0.IsChild(__result) || __result.IsChild(__0) ||
                Mathf.Abs(__instance.symmetryPlane.DistanceFromPlane(CustomMath.GetObjectCenter(__0.gameObject))) <= 0.025f)
                __result = null;
        }
    }

    [HarmonyPatch(typeof(PartPlacer), "SolveSnapping")]
    internal static class PlacedPartSnapPatch
    {
        private static bool CanAttach(BuildingPart owner, PartPlacer placer, BuildingPart solving) =>
            owner != null && !PlacementProjectionPatch.InMovingAssembly(owner, placer, solving) && owner.IsAttatched();

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var attached = AccessTools.Method(typeof(BuildingPart), nameof(BuildingPart.IsAttatched));
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(attached))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(PlacedPartSnapPatch), nameof(CanAttach));
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1) throw new InvalidOperationException("Unsupported native attachment search");
        }
        // Existing parts can still snap to nearby endpoints. Measuring from the
        // infinite mouse ray also finds endpoints far beyond the part's drag plane.
        private static void Prefix(ref bool __1)
        { if (Plugin.Instance?.MovingPlacedPart == true) __1 = false; }
    }
}

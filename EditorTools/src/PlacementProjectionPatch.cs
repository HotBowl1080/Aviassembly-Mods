using System;
using System.Collections.Generic;
using System.Reflection;
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
        }
        private static Camera InputCamera() => Plugin.Instance?.PlacementInputCamera ?? BuildingCamera.cam;
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = AccessTools.PropertyGetter(typeof(BuildingCamera), "cam");
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(original)) { instruction.operand = AccessTools.Method(typeof(PlacementProjectionPatch), "InputCamera"); replaced++; }
                yield return instruction;
            }
            if (replaced != 1) throw new InvalidOperationException("Unsupported native placement camera layout");
        }
    }
}

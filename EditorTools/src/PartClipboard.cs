using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Aviassembly.EditorTools
{
    internal sealed class PartClipboard
    {
        private sealed class PartData
        {
            internal GameObject Prefab;
            internal byte[] Settings;
            internal Color Color;
            internal Vector3 Scale;
            internal Quaternion RotationOffset;
            internal ProceduralFuselageTransform? Shape;
            internal string Name;
        }

        private PartData copied;
        private BuildingPart placingCopy;
        private string message;
        private float messageExpiry;

        internal string Message => Time.unscaledTime < messageExpiry ? message : "";

        internal void Copy(BuildingPart part)
        {
            if (part == null || !part.hasBeenPlaced) { Show("Select a placed part to copy"); return; }
            var data = Capture(part);
            if (data == null) { Show("This part cannot be copied"); return; }
            copied = data;
            Show("Copied " + copied.Name);
        }

        internal void Paste(PartPlacer placer)
        {
            if (copied == null) { Show("Copy a part with Ctrl+C first"); return; }
            var plane = PlaneContainer.Instance;
            if (placer.partContainer == null || placer.currentMovingPart != null || plane == null) return;

            var part = Create(copied, plane.transform);
            try
            {
                placingCopy = part;
                placer.SelectPart(part, false);
                Show("Place " + copied.Name);
            }
            catch
            {
                if (placer.currentMovingPart == part) placer.DestroySelectedPart();
                else UnityEngine.Object.Destroy(part.gameObject);
                throw;
            }
        }

        internal void Duplicate(PartPlacer placer, PrecisionController precision)
        {
            var source = placer.currentSelectedPart;
            if (source == null || !source.hasBeenPlaced) { Show("Select a placed part to duplicate"); return; }
            if (!precision.CanStart) return;
            var data = Capture(source);
            if (data == null) { Show("This part cannot be duplicated"); return; }

            // Native saves enumerate the aircraft's children, including unplaced parts.
            // Keep the preview outside the aircraft until the move is confirmed.
            var part = Create(data, null);
            try
            {
                part.transform.localScale = source.transform.lossyScale;
                part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                part.baseRotation = source.baseRotation;
                part.EnableColliders(false);
                if (precision.BeginDuplicate(placer, part)) return;
                UnityEngine.Object.Destroy(part.gameObject);
            }
            catch
            {
                if (precision.Session.Active && precision.Session.Target == part) precision.Cancel();
                else UnityEngine.Object.Destroy(part.gameObject);
                throw;
            }
        }

        private static PartData Capture(BuildingPart part)
        {
            var prefab = PartPrefabs.GetPartPrefab(part.gameObject.name);
            var planePart = part.GetComponentInChildren<PlanePart>();
            if (prefab == null || planePart == null) return null;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                planePart.Save(new GameDataWriter(writer));
                writer.Flush();
                return new PartData
                {
                    Prefab = prefab,
                    Settings = stream.ToArray(),
                    Color = part.currentColor,
                    Scale = part.transform.localScale,
                    RotationOffset = part.rotationOffset,
                    Shape = part.GetComponent<ProceduralFuselage>()?.AppliedTransform,
                    Name = part.partName
                };
            }
        }

        private static BuildingPart Create(PartData data, Transform parent)
        {
            var clone = UnityEngine.Object.Instantiate(data.Prefab, parent, true);
            try
            {
                var part = PartPlacer.GetBuildingPartComponent(clone);
                part.hasBeenPlaced = false;
                using (var stream = new MemoryStream(data.Settings, false))
                using (var reader = new BinaryReader(stream))
                    part.GetComponentInChildren<PlanePart>().Load(new GameDataReader(reader) { version = PersistentStorage.SaveVersion });
                part.SetColor(data.Color, true);
                part.transform.localScale = data.Scale;
                part.rotationOffset = data.RotationOffset;
                if (data.Shape.HasValue) part.GetComponent<ProceduralFuselage>().ApplyTransformation(data.Shape.Value, true);
                return part;
            }
            catch
            {
                UnityEngine.Object.Destroy(clone);
                throw;
            }
        }

        internal bool IsUnplacedPaste(BuildingPart part) => part != null && part == placingCopy && !part.hasBeenPlaced;
        internal bool KeepPastedShape(PartPlacer placer, BuildingPart part) => placer != null && IsUnplacedPaste(part) && part == placer.currentMovingPart;
        internal void Show(string value) { message = value; messageExpiry = Time.unscaledTime + 3f; }
    }

    [HarmonyPatch(typeof(PartPlacer), nameof(PartPlacer.GetMirrorPart))]
    internal static class ClipboardMirrorPatch
    {
        // A paste owns only the mirror created with it. Cancellation also performs this lookup.
        private static bool Prefix(BuildingPart __0) => Plugin.Instance?.Clipboard.IsUnplacedPaste(__0) != true;
    }

    [HarmonyPatch(typeof(ProceduralPartManager), "CalculateOverrideRadius")]
    internal static class ClipboardFuselagePatch
    {
        // Catalogue fuselages take their size from the attachment. Pasted fuselages already have a shape.
        private static bool Prefix(ProceduralPartManager __instance, BuildingPart __0) =>
            Plugin.Instance?.Clipboard.KeepPastedShape(__instance.partPlacer, __0) != true;
    }
}

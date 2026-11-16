using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Aviassembly.EditorTools
{
    internal sealed class TransformSnapshot
    {
        private struct Pose
        {
            internal Transform transform, parent;
            internal Vector3 position, scale;
            internal Quaternion rotation;
            internal BuildingPart part;
            internal BuildingPart logicalParent;
            internal Quaternion offset, baseRotation;
        }
        private readonly List<Pose> poses = new List<Pose>();
        private readonly HashSet<Transform> seen = new HashSet<Transform>();
        private readonly HashSet<BuildingPart> seenParts = new HashSet<BuildingPart>();
        internal TransformSnapshot(BuildingPart target, BuildingPart mirror) { AddPart(target); AddPart(mirror); }
        private void AddPart(BuildingPart part)
        {
            if (part == null || !seenParts.Add(part)) return;
            AddTree(part.transform);
            foreach (var child in part.children) AddPart(child);
            foreach (var decal in part.decals) if (decal != null) AddTree(decal.transform);
        }
        private void AddTree(Transform t)
        {
            if (!seen.Add(t)) return;
            var p = t.GetComponent<BuildingPart>();
            poses.Add(new Pose { transform = t, parent = t.parent, position = t.localPosition, rotation = t.localRotation,
                scale = t.localScale, part = p, offset = p != null ? p.rotationOffset : Quaternion.identity,
                baseRotation = p != null ? p.baseRotation : Quaternion.identity, logicalParent = p != null ? p.parent : null });
            foreach (Transform child in t) AddTree(child);
        }
        internal void Restore()
        {
            foreach (var p in poses)
            {
                if (p.transform == null) continue;
                if (p.transform.parent != p.parent) p.transform.SetParent(p.parent, false);
                p.transform.localPosition = p.position;
                p.transform.localRotation = p.rotation;
                p.transform.localScale = p.scale;
                if (p.part != null)
                {
                    p.part.rotationOffset = p.offset; p.part.baseRotation = p.baseRotation;
                    if (p.part.parent != p.logicalParent) p.part.SetParent(p.logicalParent != null ? p.logicalParent.gameObject : null);
                }
            }
        }
    }

    internal sealed class TransformSession
    {
        internal bool Active { get; private set; }
        internal BuildingPart Target { get; private set; }
        internal Vector3 StartPosition { get; private set; }
        internal Quaternion StartRotation { get; private set; }
        internal bool Changed { get; private set; }
        private PartPlacer placer;
        private BuildingPart mirror;
        private TransformSnapshot original;
        private Vector3 previewPosition;
        private Quaternion mirrorStartRotation;
        private static readonly MethodInfo Flush = AccessTools.Method(typeof(PlaneStorage), "ExecuteHistoryUpdate");
        internal bool Begin(PartPlacer source)
        {
            if (Active || source == null || source.currentSelectedPart == null || !source.currentSelectedPart.hasBeenPlaced ||
                source.currentMovingPart != null || source.partContainer == null || PlaneStorage.Instance == null || DecalContainer.Instance == null) return false;
            placer = source;
            Target = source.currentSelectedPart;
            mirror = source.GetMirrorPart(Target);
            if (mirror == Target || (mirror != null && (Target.IsChild(mirror) || mirror.IsChild(Target)))) mirror = null;
            // Flush a pending native edit BEFORE any preview. Native history deduplicates.
            Flush.Invoke(PlaneStorage.Instance, null);
            original = new TransformSnapshot(Target, mirror);
            mirrorStartRotation = mirror != null ? mirror.transform.rotation : Quaternion.identity;
            StartPosition = Target.transform.position;
            StartRotation = Target.transform.rotation;
            previewPosition = StartPosition;
            Active = true; Changed = false;
            return true;
        }
        internal void Preview(Vector3 position, Quaternion rotation)
        {
            if (!Active || Target == null || !TransformMath.Finite(position) ||
                !TransformMath.Finite(new Vector3(rotation.x, rotation.y, rotation.z)) || !TransformMath.Finite(rotation.w)) return;
            if (Target.lockRotation) rotation = StartRotation;
            original.Restore();
            Target.SetPositionAndRotation(position, rotation);
            bool rotationChanged = Quaternion.Angle(rotation, StartRotation) > 0.00001f;
            if (rotationChanged) Target.rotationOffset = Quaternion.Inverse(Target.baseRotation) * rotation;
            if (mirror != null)
            {
                mirror.SetPositionAndRotation(placer.symmetryPlane.GetMirroredPosition(position),
                    rotationChanged ? placer.symmetryPlane.GetMirroredRotation(Target.transform) : mirrorStartRotation);
                if (rotationChanged) mirror.rotationOffset = Quaternion.Inverse(mirror.baseRotation) * mirror.transform.rotation;
            }
            previewPosition = position;
            Changed = (position - StartPosition).sqrMagnitude > 0.0000000001f || Quaternion.Angle(rotation, StartRotation) > 0.00001f;
            SyncGizmos();
        }
        internal bool Commit()
        {
            if (!Active || Target == null) { Cancel(); return false; }
            if (!Changed) { Cancel(); return true; }
            try
            {
            // Attachment recalculation matches native movement and runs only at commit.
            Physics.SyncTransforms();
            if ((previewPosition - StartPosition).sqrMagnitude > 0.0000000001f)
            {
                placer.partContainer.UpdateAttachment(Target);
                if (mirror != null) placer.partContainer.UpdateAttachment(mirror);
                placer.partContainer.UpdateUsedSnapPoints();
            }
            SyncGizmos();
            if (placer.decalPlacer != null) placer.decalPlacer.RecalculateDecals(null, null);
            Active = false;
            PlaneStorage.Instance.UpdateHistory();
            original = null;
            return true;
            }
            catch { Active = true; Cancel(); throw; }
        }
        internal void Cancel()
        {
            if (!Active) return;
            original?.Restore();
            Active = false; Changed = false;
            SyncGizmos();
            original = null;
        }
        private void SyncGizmos()
        {
            if (Target == null || placer == null || placer.currentSelectedPart != Target) return;
            if (placer.moveGizmo != null) placer.moveGizmo.transform.SetPositionAndRotation(Target.transform.position, Target.transform.rotation);
            if (placer.rotationGizmo != null)
            {
                placer.rotationGizmo.transform.SetPositionAndRotation(Target.transform.position, Target.transform.rotation);
                placer.rotationGizmo.openRotation = Target.transform.rotation;
                AccessTools.Field(typeof(RotationGizmo), "startRotation").SetValue(placer.rotationGizmo, Target.transform.rotation);
            }
        }
        internal TransformSnapshot BeforeSave()
        {
            if (!Active) return null;
            var preview = new TransformSnapshot(Target, mirror);
            original.Restore();
            return preview;
        }
    }

    [HarmonyPatch(typeof(PlaneStorage), "Save")]
    internal static class PrecisionSavePatch
    {
        private static void Prefix(out TransformSnapshot __state) { __state = Plugin.Instance?.Precision?.Session.BeforeSave(); }
        private static Exception Finalizer(Exception __exception, TransformSnapshot __state) { __state?.Restore(); return __exception; }
    }
    [HarmonyPatch]
    internal static class PrecisionHistoryPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PlaneStorage), "UpdateHistory");
            yield return AccessTools.Method(typeof(PlaneStorage), "ExecuteHistoryUpdate");
        }
        private static bool Prefix() => Plugin.Instance?.Precision?.Session.Active != true;
    }
    [HarmonyPatch]
    internal static class PrecisionCancelBeforeMutationPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PlaneStorage), "ResetPlane");
            yield return AccessTools.Method(typeof(PlaneStorage), "Undo");
            yield return AccessTools.Method(typeof(PlaneStorage), "Load");
            yield return AccessTools.Method(typeof(GameManager), "StartFlyMode");
            yield return AccessTools.Method(typeof(GameManager), "StartMenu");
            yield return AccessTools.Method(typeof(PartPlacer), "DestroySelectedPart");
        }
        private static void Prefix() { Plugin.Instance?.Precision?.Cancel(); }
    }
}

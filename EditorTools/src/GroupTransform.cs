using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Aviassembly.EditorTools
{
    internal sealed class GroupTransform
    {
        private sealed class Entry
        {
            internal BuildingPart part, mirror;
            internal Vector3 position;
            internal Quaternion rotation, mirrorRotation;
        }
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<BuildingPart> affected = new List<BuildingPart>();
        private readonly TransformSnapshot snapshot;
        private readonly PartPlacer placer;
        internal Vector3 Pivot { get; }
        internal GroupTransform(PartPlacer placer, BuildingPart[] selection)
        {
            this.placer = placer;
            Pivot = selection.Aggregate(Vector3.zero, (v, p) => v + p.transform.position) / selection.Length;
            var roots = selection.Where(p => !selection.Any(q => q != p && (q.IsChild(p) || p.transform.IsChildOf(q.transform)))).ToArray();
            foreach (var part in roots)
            {
                var mirror = placer.GetMirrorPart(part);
                if (mirror != null && (selection.Contains(mirror) || roots.Any(p => p == mirror || p.IsChild(mirror) || mirror.IsChild(p)) || affected.Contains(mirror))) mirror = null;
                entries.Add(new Entry { part = part, position = part.transform.position, rotation = part.transform.rotation,
                    mirror = mirror, mirrorRotation = mirror != null ? mirror.transform.rotation : Quaternion.identity });
                affected.Add(part); if (mirror != null) affected.Add(mirror);
            }
            snapshot = new TransformSnapshot(affected);
        }
        internal void Preview(Vector3 pivot, Quaternion delta)
        {
            snapshot.Restore();
            foreach (var e in entries)
            {
                if (e.part == null) continue;
                var rotation = e.part.lockRotation ? e.rotation : delta * e.rotation;
                e.part.SetPositionAndRotation(pivot + delta * (e.position - Pivot), rotation);
                e.part.rotationOffset = Quaternion.Inverse(e.part.baseRotation) * rotation;
                if (e.mirror != null)
                {
                    var mirroredRotation = Quaternion.Angle(rotation, e.rotation) > 0.00001f ? placer.symmetryPlane.GetMirroredRotation(e.part.transform) : e.mirrorRotation;
                    e.mirror.SetPositionAndRotation(placer.symmetryPlane.GetMirroredPosition(e.part.transform.position), mirroredRotation);
                    e.mirror.rotationOffset = Quaternion.Inverse(e.mirror.baseRotation) * mirroredRotation;
                }
            }
        }
        internal void Restore() => snapshot.Restore();
        internal TransformSnapshot BeforeSave() { var preview = new TransformSnapshot(affected); snapshot.Restore(); return preview; }
        internal void Commit()
        {
            foreach (var part in affected) if (part != null) placer.partContainer.UpdateAttachment(part);
            placer.partContainer.UpdateUsedSnapPoints();
            placer.decalPlacer?.RecalculateDecals(null, null);
        }
    }
}

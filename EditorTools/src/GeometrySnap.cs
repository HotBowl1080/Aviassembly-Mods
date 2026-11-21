using System.Collections.Generic;
using UnityEngine;

namespace Aviassembly.EditorTools
{
    internal sealed partial class PrecisionController
    {
        private GeometrySnap geometry;
        private Vector3? snapHit, moveAnchor;

        private bool TryHoverTarget(Vector2 mouse, out Vector3 hit)
        {
            hit = default;
            // The mod reports PointerIsOverUI to the game throughout a drag to suppress native clicks.
            // Ask the native hit test about actual panels before picking the mesh underneath.
            if (geometry == null || (mouse - startMouse).sqrMagnitude < 9 || host.PointerOverPanel() ||
                host.PointerOverToolUI() || host.PointerOverClearanceUI() ||
                (UnityEngine.EventSystems.EventSystem.current != null && MouseInput.Instance?.GetPointerIsOverUI(null) == true)) return false;
            snapHit = geometry.Pick(mouse, true);
            if (!snapHit.HasValue) return false;
            hit = snapHit.Value;
            return true;
        }

        private float HoverDistance(Vector3 hit, Vector3 axis)
        {
            float destination = Vector3.Dot(hit - Session.StartPosition, axis);
            var span = geometry.Extent(Session.StartPosition, axis);
            float middle = (span.x + span.y) * 0.5f;
            float source = host.Reference == SnapReference.Center ? middle : destination <= middle ? span.x : span.y;
            return destination - source;
        }

        private Vector3 FreeSnapAnchor(Vector3 hit)
        {
            if (moveAnchor.HasValue) return moveAnchor.Value;
            if (host.Reference == SnapReference.Center)
            {
                var center = Session.StartPosition;
                for (int i = 0; i < 3; i++)
                {
                    var axis = Session.StartRotation * TransformMath.Axis(i);
                    var span = geometry.Extent(Session.StartPosition, axis);
                    center += axis * ((span.x + span.y) * 0.5f);
                }
                moveAnchor = center;
            }
            else moveAnchor = geometry.Pick(startMouse, false);
            return moveAnchor ?? geometry.ClosestEdge(hit, Session.StartPosition);
        }

        internal void DrawSnapMarker()
        {
            if (snapHit.HasValue && Event.current.type == EventType.Repaint) DrawPoint(snapHit.Value, Color.yellow, 11);
        }

        private void DrawPoint(Vector3 world, Color color, float size)
        {
            var point = ProjectionCamera.WorldToScreenPoint(world);
            if (point.z <= 0) return;
            var previous = GUI.color;
            GUI.color = Color.black;
            var rect = new Rect(point.x - size / 2, Screen.height - point.y - size / 2, size, size);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, size - 4, size - 4), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }

    // Freeze the visible geometry at the original pose. A preview must never become its own destination.
    internal sealed class GeometrySnap
    {
        private sealed class Surface
        {
            internal Mesh mesh;
            internal Matrix4x4 matrix;
            internal Bounds bounds;
            internal Rect screenBounds;
            internal bool moves, source;
            internal Vector3[] world, screen;
            internal int[] triangles, corners;
            internal List<Vector2Int> edges;
        }

        private readonly Camera camera;
        private readonly List<Surface> surfaces = new List<Surface>();
        private readonly float radius;
        private bool cached, lastTarget;
        private Vector2 lastMouse;
        private Vector3? lastPoint;
        private Vector3 lastAxis;
        private Vector2 extent;

        internal GeometrySnap(Camera camera, TransformSession session, Plugin host)
        {
            this.camera = camera;
            if (camera == null) return;
            radius = Mathf.Clamp(camera.pixelHeight / 80f, 8f, 20f);
            var seen = new HashSet<MeshFilter>();
            var selection = session.IsGroup ? host.Selection.Parts : new[] { session.Target };
            void Collect(Component root)
            {
                if (root == null) return;
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (!seen.Add(filter) || renderer == null || !renderer.enabled || renderer.forceRenderingOff ||
                        (camera.cullingMask & (1 << filter.gameObject.layer)) == 0) continue;
                    var part = filter.GetComponentInParent<BuildingPart>();
                    if (part == null || host.HideParts?.IsHidden(part) == true) continue;
                    var mesh = filter.sharedMesh;
                    if (mesh == null || !mesh.isReadable) continue;
                    var surface = new Surface { mesh = mesh, bounds = renderer.bounds, matrix = filter.transform.localToWorldMatrix,
                        moves = session.Moves(part) };
                    if (!surface.moves && !part.hasBeenPlaced) continue;
                    foreach (var selected in selection)
                        if (part == selected) { surface.source = true; break; }
                    surface.screenBounds = ScreenBounds(surface.bounds);
                    surfaces.Add(surface);
                }
            }
            Collect(PlaneContainer.Instance);
            Collect(session.Target);
        }

        private Rect ScreenBounds(Bounds bounds)
        {
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var point = camera.WorldToScreenPoint(new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z));
                if (point.z <= camera.nearClipPlane) return camera.pixelRect;
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x - radius, min.y - radius, max.x + radius, max.y + radius);
        }

        private void Load(Surface s)
        {
            if (s.world != null) return;
            var vertices = s.mesh.vertices;
            s.world = new Vector3[vertices.Length]; s.screen = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                s.world[i] = s.matrix.MultiplyPoint3x4(vertices[i]);
                s.screen[i] = camera.WorldToScreenPoint(s.world[i]);
            }
            s.triangles = s.mesh.triangles;
            var corners = new HashSet<int>(s.triangles);
            s.corners = new int[corners.Count];
            corners.CopyTo(s.corners);
        }

        // Weld coincident UV/normal seams and omit coplanar triangulation diagonals.
        private static List<Vector2Int> Edges(Surface s)
        {
            var welded = new Dictionary<Vector3, int>();
            var canonical = new int[s.world.Length];
            for (int i = 0; i < canonical.Length; i++)
            {
                if (!welded.TryGetValue(s.world[i], out int index)) welded.Add(s.world[i], index = i);
                canonical[i] = index;
            }
            var normals = new Dictionary<Vector2Int, Vector3>();
            var interior = new HashSet<Vector2Int>();
            void Add(int a, int b, Vector3 normal)
            {
                var edge = new Vector2Int(Mathf.Min(a, b), Mathf.Max(a, b));
                if (!normals.TryGetValue(edge, out Vector3 previous)) normals.Add(edge, normal);
                else if (Vector3.Dot(previous, normal) > 0.999999f) interior.Add(edge);
            }
            for (int i = 0; i < s.triangles.Length; i += 3)
            {
                int a = canonical[s.triangles[i]], b = canonical[s.triangles[i + 1]], c = canonical[s.triangles[i + 2]];
                var normal = Vector3.Cross(s.world[b] - s.world[a], s.world[c] - s.world[a]).normalized;
                if (normal == Vector3.zero) continue;
                Add(a, b, normal); Add(b, c, normal); Add(c, a, normal);
            }
            var edges = new List<Vector2Int>();
            foreach (var edge in normals.Keys) if (!interior.Contains(edge)) edges.Add(edge);
            return edges;
        }

        internal Vector2 Extent(Vector3 origin, Vector3 axis)
        {
            if (axis.Equals(lastAxis)) return extent;
            lastAxis = axis;
            extent = new Vector2(float.PositiveInfinity, float.NegativeInfinity);
            foreach (var s in surfaces)
            {
                if (!s.source) continue;
                Load(s);
                foreach (int i in s.corners)
                {
                    float distance = Vector3.Dot(s.world[i] - origin, axis);
                    extent.x = Mathf.Min(extent.x, distance);
                    extent.y = Mathf.Max(extent.y, distance);
                }
            }
            if (extent.x > extent.y) extent = Vector2.zero;
            return extent;
        }

        internal Vector3? Pick(Vector2 mouse, bool target)
        {
            if (cached && mouse.Equals(lastMouse) && target == lastTarget) return lastPoint;
            cached = true;
            lastMouse = mouse; lastTarget = target;
            lastPoint = PickFeature(mouse, target, false) ?? PickFeature(mouse, target, true);
            return lastPoint;
        }

        internal Vector3 ClosestEdge(Vector3 target, Vector3 fallback)
        {
            float best = float.PositiveInfinity;
            foreach (var s in surfaces)
            {
                if (!s.source) continue;
                Load(s);
                if (s.edges == null) s.edges = Edges(s);
                foreach (var edge in s.edges)
                {
                    var start = s.world[edge.x];
                    var line = s.world[edge.y] - start;
                    if (line.sqrMagnitude < 1e-12f) continue;
                    var point = start + line * Mathf.Clamp01(Vector3.Dot(target - start, line) / line.sqrMagnitude);
                    float distance = (target - point).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    fallback = point;
                }
            }
            return fallback;
        }

        private Vector3? PickFeature(Vector2 mouse, bool target, bool edges)
        {
            Vector3? result = null;
            float best = radius * radius * (edges ? 1f : 0.25f), depth = float.PositiveInfinity;
            void Consider(Vector3 world, Vector3 screen)
            {
                if (screen.z < camera.nearClipPlane || screen.z > camera.farClipPlane) return;
                float distance = ((Vector2)screen - mouse).sqrMagnitude;
                if (distance > best || (Mathf.Abs(distance - best) < 0.0001f && screen.z >= depth)) return;
                if (Occluded(world, screen, target)) return;
                result = world; best = distance; depth = screen.z;
            }
            foreach (var s in surfaces)
            {
                if ((target ? s.moves : !s.source) || !s.screenBounds.Contains(mouse)) continue;
                Load(s);
                if (!edges)
                {
                    foreach (int i in s.corners) Consider(s.world[i], s.screen[i]);
                    continue;
                }
                if (s.edges == null) s.edges = Edges(s);
                foreach (var edge in s.edges)
                {
                    var start = s.world[edge.x]; var end = s.world[edge.y];
                    var a = s.screen[edge.x]; var b = s.screen[edge.y];
                    float near = camera.nearClipPlane;
                    if (a.z < near && b.z < near) continue;
                    if (a.z < near || b.z < near)
                    {
                        var clipped = Vector3.Lerp(start, end, (near - a.z) / (b.z - a.z));
                        if (a.z < near) { start = clipped; a = camera.WorldToScreenPoint(start); }
                        else { end = clipped; b = camera.WorldToScreenPoint(end); }
                    }
                    var line = (Vector2)(b - a);
                    float t = line.sqrMagnitude < 0.00001f ? (a.z < b.z ? 0 : 1) :
                        Mathf.Clamp01(Vector2.Dot(mouse - (Vector2)a, line) / line.sqrMagnitude);
                    if (((Vector2)a + t * line - mouse).sqrMagnitude > best) continue;
                    if (!camera.orthographic) t = t * a.z / Mathf.Lerp(b.z, a.z, t);
                    var world = Vector3.Lerp(start, end, t);
                    Consider(world, camera.WorldToScreenPoint(world));
                }
            }
            return result;
        }

        private bool Occluded(Vector3 world, Vector3 screen, bool target)
        {
            var ray = camera.ScreenPointToRay(screen);
            float limit = Vector3.Dot(world - ray.origin, ray.direction) - Mathf.Max(0.0001f, screen.z * 0.00001f);
            foreach (var s in surfaces)
            {
                if ((target && s.moves) || !s.bounds.IntersectRay(ray, out float start) || start > limit) continue;
                Load(s);
                for (int i = 0; i < s.triangles.Length; i += 3)
                {
                    var a = s.world[s.triangles[i]];
                    var ab = s.world[s.triangles[i + 1]] - a;
                    var ac = s.world[s.triangles[i + 2]] - a;
                    var h = Vector3.Cross(ray.direction, ac);
                    float det = Vector3.Dot(ab, h);
                    if (Mathf.Abs(det) < 1e-10f) continue;
                    var offset = ray.origin - a;
                    float u = Vector3.Dot(offset, h) / det;
                    if (u < 0 || u > 1) continue;
                    var q = Vector3.Cross(offset, ab);
                    float v = Vector3.Dot(ray.direction, q) / det;
                    if (v < 0 || u + v > 1) continue;
                    float d = Vector3.Dot(ac, q) / det;
                    if (d >= 0 && d < limit) return true;
                }
            }
            return false;
        }
    }
}

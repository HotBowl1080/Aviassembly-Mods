using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Aviassembly.EditorTools
{
    internal sealed partial class PrecisionController
    {
        private bool planeHandle;
        private Vector3 planeGrabPoint;
        private Material planeHandleMaterial;

        private MoveGizmo PlaneGizmo
        {
            get
            {
                if (!host.CanStartHandle || placer == null || placer.currentSelectedPart == null ||
                    placer.currentMovingPart != null || (Session.Active && !planeHandle)) return null;
                var gizmo = placer.moveGizmo;
                return gizmo != null && gizmo.isActiveAndEnabled ? gizmo : null;
            }
        }

        private struct MovePlane
        {
            internal Vector3 origin, first, second;
            internal Vector2 a, b, c, d;

            internal bool Hit(Ray ray, out float distance)
            {
                var plane = new Plane(Vector3.Cross(first, second).normalized, origin);
                if (!plane.Raycast(ray, out distance)) return false;
                var offset = ray.GetPoint(distance) - origin;
                float u = Vector3.Dot(offset, first) / first.sqrMagnitude;
                float v = Vector3.Dot(offset, second) / second.sqrMagnitude;
                return u >= 0.16f && u <= 0.43f && v >= 0.16f && v <= 0.43f;
            }
        }

        private static bool TryGetPlane(MoveGizmo gizmo, Camera camera, int normal, out MovePlane plane)
        {
            plane = default;
            var firstArrow = normal == 0 ? gizmo.Y : gizmo.X;
            var secondArrow = normal == 2 ? gizmo.Y : gizmo.Z;
            if (firstArrow == null || secondArrow == null || !firstArrow.isActiveAndEnabled || !secondArrow.isActiveAndEnabled ||
                firstArrow.direction == null || secondArrow.direction == null) return false;

            plane.origin = gizmo.transform.position;
            var first = firstArrow.direction.forward;
            var second = secondArrow.direction.forward;
            var firstCollider = firstArrow.GetComponentInChildren<Collider>() ?? firstArrow.GetComponentInParent<Collider>();
            var secondCollider = secondArrow.GetComponentInChildren<Collider>() ?? secondArrow.GetComponentInParent<Collider>();
            if (firstCollider == null || secondCollider == null || !firstCollider.enabled || !secondCollider.enabled) return false;
            plane.first = Vector3.Project(firstCollider.bounds.center - plane.origin, first);
            plane.second = Vector3.Project(secondCollider.bounds.center - plane.origin, second);
            if (plane.first.sqrMagnitude < 0.000001f || plane.second.sqrMagnitude < 0.000001f) return false;
            var a = camera.WorldToScreenPoint(plane.origin + plane.first * 0.16f + plane.second * 0.16f);
            var b = camera.WorldToScreenPoint(plane.origin + plane.first * 0.43f + plane.second * 0.16f);
            var c = camera.WorldToScreenPoint(plane.origin + plane.first * 0.43f + plane.second * 0.43f);
            var d = camera.WorldToScreenPoint(plane.origin + plane.first * 0.16f + plane.second * 0.43f);
            float twiceArea = Vector3.Cross(b - a, d - a).z + Vector3.Cross(c - b, d - b).z;
            if (Mathf.Min(Mathf.Min(a.z, b.z), Mathf.Min(c.z, d.z)) <= camera.nearClipPlane || Mathf.Abs(twiceArea) < 50f) return false;
            plane.a = a; plane.b = b; plane.c = c; plane.d = d;
            return true;
        }

        private static int PickMovePlane(MoveGizmo gizmo, Camera camera, Ray ray, float distance, out Vector3 point)
        {
            int normal = -1;
            point = default;
            for (int axis = 0; axis < 3; axis++)
            {
                if (!TryGetPlane(gizmo, camera, axis, out var plane) || !plane.Hit(ray, out float hitDistance) || hitDistance >= distance) continue;
                distance = hitDistance;
                normal = axis;
            }
            if (normal >= 0) point = ray.GetPoint(distance);
            return normal;
        }

        private bool BeginPlaneHandle(int normal, Vector3 point)
        {
            if (!Begin(false, true, true)) return false;
            planeHandle = handleDragging = PlaneLock = Local = true;
            planeGrabPoint = point;
            Axis = normal;
            return true;
        }

        internal void DrawPlaneHandles()
        {
            if (Event.current.type != EventType.Repaint) return;
            var gizmo = PlaneGizmo;
            var camera = BuildingCamera.cam;
            if (gizmo == null || camera == null) return;
            if (planeHandleMaterial == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return;
                planeHandleMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                planeHandleMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                planeHandleMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                planeHandleMaterial.SetInt("_Cull", (int)CullMode.Off);
                planeHandleMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
                planeHandleMaterial.SetInt("_ZWrite", 0);
            }
            if (!planeHandleMaterial.SetPass(0)) return;
            var mouse = Mouse.current;
            int hover = planeHandle ? Axis : mouse != null && MouseInput.Instance?.PointerIsOverUI != true
                ? PickMovePlane(gizmo, camera, camera.ScreenPointToRay(mouse.position.ReadValue()), float.PositiveInfinity, out _) : -1;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, 0, Screen.height);
            for (int axis = 0; axis < 3; axis++)
            {
                if (planeHandle && axis != Axis || !TryGetPlane(gizmo, camera, axis, out var plane)) continue;
                Color color = axis == 0 ? new Color(0.31f, 0.9f, 0.37f) : axis == 1 ? new Color(0.33f, 0.55f, 1f) : new Color(1f, 0.32f, 0.31f);
                color.a = axis == hover ? 0.86f : 0.52f;
                GL.Begin(GL.TRIANGLES);
                GL.Color(color);
                GL.Vertex(plane.a); GL.Vertex(plane.b); GL.Vertex(plane.c);
                GL.Vertex(plane.a); GL.Vertex(plane.c); GL.Vertex(plane.d);
                GL.End();
                color.a = 1;
                GL.Begin(GL.LINE_STRIP);
                GL.Color(color);
                GL.Vertex(plane.a); GL.Vertex(plane.b); GL.Vertex(plane.c); GL.Vertex(plane.d); GL.Vertex(plane.a);
                GL.End();
            }
            GL.PopMatrix();
        }
    }
}

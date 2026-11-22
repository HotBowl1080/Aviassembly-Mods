using System;
using UnityEngine;

namespace Aviassembly.EditorTools
{
    // Keep the starting projection for the full gesture.
    internal sealed class DragProjection : IDisposable
    {
        internal Camera Camera { get; private set; }
        internal void Capture(Camera source)
        {
            if (source == null) return;
            if (Camera == null)
            {
                var go = new GameObject("EditorTools drag projection");
                go.hideFlags = HideFlags.HideAndDontSave;
                Camera = go.AddComponent<Camera>();
            }
            Camera.CopyFrom(source);
            Camera.enabled = false;
            Camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            Camera.pixelRect = source.pixelRect;
            Camera.projectionMatrix = source.projectionMatrix;
        }
        // Invert perspective along a world axis through the point the player picked.
        internal static float AxisDisplacement(Camera cam, Vector3 anchor, Vector3 direction, Vector2 motion,
            float minimumRateFraction, out bool endOn)
        {
            endOn = false;
            var origin = cam.WorldToScreenPoint(anchor);
            if (origin.z <= cam.nearClipPlane) return 0;
            float step = origin.z * 0.05f;
            var axisPoint = cam.WorldToScreenPoint(anchor + direction * step);
            var slope = ((Vector2)axisPoint - (Vector2)origin) / step;
            float pixelsPerMetre = Vector2.Distance(cam.WorldToScreenPoint(anchor + cam.transform.right * step), origin) / step;
            if (pixelsPerMetre <= 0.0001f) return 0;
            if (!cam.orthographic) slope *= axisPoint.z / origin.z;
            float rate = slope.magnitude;
            endOn = rate <= pixelsPerMetre * 0.02f;
            if (endOn) return motion.y / pixelsPerMetre;
            float pixels = Vector2.Dot(motion, slope / rate);
            if (!cam.orthographic) rate -= pixels * Vector3.Dot(direction, cam.transform.forward) / origin.z;
            return pixels / Mathf.Max(rate, pixelsPerMetre * minimumRateFraction);
        }

        internal static Vector3 PlaneDisplacement(Camera camera, Vector3 anchor, Vector3 normal, Vector2 start, Vector2 mouse)
        {
            var plane = new Plane(normal, anchor);
            var startRay = camera.ScreenPointToRay(start);
            var endRay = camera.ScreenPointToRay(mouse);
            float facing = Vector3.Dot(normal, startRay.direction);
            if (Mathf.Abs(facing) >= 0.06f && Vector3.Dot(normal, endRay.direction) * Mathf.Sign(facing) >= 0.06f &&
                plane.Raycast(startRay, out float startDistance) && plane.Raycast(endRay, out float endDistance))
                return Vector3.ProjectOnPlane(endRay.GetPoint(endDistance) - startRay.GetPoint(startDistance), normal);

            // An edge-on plane has no usable ray intersection. Follow visible screen motion at the anchor depth.
            float depth = Mathf.Max(camera.nearClipPlane, camera.WorldToScreenPoint(anchor).z);
            var from = camera.ScreenToWorldPoint(new Vector3(start.x, start.y, depth));
            var to = camera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, depth));
            return Vector3.ProjectOnPlane(to - from, normal);
        }
        public void Dispose() { if (Camera != null) UnityEngine.Object.Destroy(Camera.gameObject); Camera = null; }
    }
}

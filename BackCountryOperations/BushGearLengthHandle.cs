using UnityEngine;
using UnityEngine.EventSystems;

namespace MachWorks.BackCountryOperations
{
    internal sealed class BushGearLengthHandle : MonoBehaviour
    {
        private const float HandlePixels = 34f;
        private const float GrabRadius = 23f;
        private PartPlacer placer;
        private BushWheelPart wheel, mirror;
        private Transform handle;
        private MeshRenderer handleRenderer;
        private MaterialPropertyBlock handleStyle;
        private ProceduralGizmoManager visualSource;
        private float meshSize;
        private Vector2 startMouse, screenTravel;
        private int initialLength, mirrorLength;
        private bool dragging;

        internal static void UpdateFor(PartPlacer placer)
        {
            var selected = placer.currentSelectedPart;
            var wheel = selected && !placer.currentMovingPart && selected.hasBeenPlaced
                ? selected.GetComponentInChildren<BushWheelPart>() : null;
            if (!placer.TryGetComponent<BushGearLengthHandle>(out var editor))
            {
                if (!wheel || !wheel.SupportsLength)
                    return;
                editor = placer.gameObject.AddComponent<BushGearLengthHandle>();
                editor.placer = placer;
            }

            if (editor.wheel != wheel && editor.dragging)
                editor.Finish();
            editor.wheel = wheel;
            editor.PositionHandle();
        }

        private void PositionHandle()
        {
            bool visible = wheel && wheel.SupportsLength && !wheel.LegacyFitted && BuildingCamera.cam;
            if (visible && !handle)
            {
                var manager = placer.procduralPartController ? placer.procduralPartController.gizmoManager : null;
                var source = manager && manager.gizmo1 ? manager.gizmo1.sizeGizmo : null;
                var filter = source ? source.GetComponentInChildren<MeshFilter>(true) : null;
                if (!filter || !filter.sharedMesh || !filter.TryGetComponent<MeshRenderer>(out var renderer))
                    return;
                var model = new GameObject("Strut length handle", typeof(MeshFilter), typeof(MeshRenderer));
                handle = model.transform;
                handle.SetParent(placer.transform, false);
                model.layer = filter.gameObject.layer;
                model.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                handleRenderer = model.GetComponent<MeshRenderer>();
                handleRenderer.sharedMaterials = renderer.sharedMaterials;
                handleStyle = new MaterialPropertyBlock();
                meshSize = Mathf.Max(.01f, filter.sharedMesh.bounds.size.magnitude);
                visualSource = manager;
            }

            if (!handle)
                return;
            handle.gameObject.SetActive(visible);
            if (visible)
            {
                var camera = BuildingCamera.cam;
                var point = wheel.LengthHandlePosition;
                var projection = camera.WorldToScreenPoint(point);
                handle.gameObject.SetActive(projection.z > 0f);
                handle.SetPositionAndRotation(point,
                    Quaternion.LookRotation(wheel.LengthTravel.normalized, wheel.Part.transform.forward));
                float pixelsPerUnit = (camera.WorldToScreenPoint(point + camera.transform.up) - projection).magnitude;
                handle.localScale = Vector3.one * (Mathf.Clamp(HandlePixels / Mathf.Max(1f, pixelsPerUnit), .12f, .68f) / meshSize);
                if (visualSource)
                {
                    bool hovered = (MouseInput.GetMousePosition() - new Vector3(projection.x, projection.y)).sqrMagnitude <= GrabRadius * GrabRadius;
                    handleStyle.SetColor("_Color", dragging || hovered ? visualSource.white : visualSource.black);
                    handleRenderer.SetPropertyBlock(handleStyle);
                }
            }
        }

        internal static bool BeforeSelection(PartPlacer __instance)
        {
            return !__instance.TryGetComponent<BushGearLengthHandle>(out var editor) || !editor.ReadDrag();
        }

        private bool ReadDrag()
        {
            if (!wheel || !handle || !handle.gameObject.activeInHierarchy || !BuildingCamera.cam)
            {
                Finish();
                return false;
            }

            var mouse = (Vector2)MouseInput.GetMousePosition();
            if (!dragging)
            {
                if (!MouseInput.GetMouseButtonDown(0) || (EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
                    return false;

                var camera = BuildingCamera.cam;
                var point = camera.WorldToScreenPoint(handle.position);
                if (point.z <= 0f || (mouse - (Vector2)point).sqrMagnitude > GrabRadius * GrabRadius)
                    return false;
                screenTravel = (Vector2)(camera.WorldToScreenPoint(handle.position + wheel.LengthTravel) - point);
                if (screenTravel.sqrMagnitude < 25f)
                    return false;
                mirror = BushTireSettings.FindMatchingWheel(placer, wheel);
                if (mirror && mirror.LegacyFitted)
                    return false;
                startMouse = mouse;
                initialLength = wheel.LengthPercent;
                mirrorLength = mirror ? mirror.LengthPercent : initialLength;
                dragging = true;
            }

            if (!MouseInput.GetMouseButton(0))
            {
                Finish();
                return true;
            }

            float displacement = Vector2.Dot(mouse - startMouse, screenTravel) / screenTravel.sqrMagnitude;
            int percent = Mathf.Clamp(initialLength + Mathf.RoundToInt(displacement * 100f), 65, 112);
            if (Mathf.Abs(percent - 100) <= 1)
                percent = 100;
            wheel.SetLengthPercent(percent);
            if (mirror && percent != initialLength)
                mirror.SetLengthPercent(percent);
            else if (mirror)
                mirror.SetLengthPercent(mirrorLength);
            return true;
        }

        private void Finish()
        {
            if (dragging && ((wheel && wheel.LengthPercent != initialLength) ||
                (mirror && mirror.LengthPercent != mirrorLength)) && PlaneStorage.Instance)
                PlaneStorage.Instance.UpdateHistory();
            dragging = false;
            mirror = null;
        }

        private void OnDisable()
        {
            Finish();
            if (handle)
                handle.gameObject.SetActive(false);
        }
    }
}

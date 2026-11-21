using System;
using System.Globalization;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aviassembly.EditorTools
{
    internal sealed partial class PrecisionController
    {
        private sealed class RecentAction
        {
            internal BuildingPart part;
            internal bool rotation;
            internal int axis;
            internal Vector3 origin, direction, finalPosition;
            internal Quaternion startRotation, finalRotation;
            internal float value;
        }
        private RecentAction recent;
        private bool handleOperation, handleDragging;
        private Vector3 handleDirection, handleOrigin, handleRadial, handleGrabPoint;
        private Quaternion handleStartRotation;
        private Vector2 handleTangent;
        private float handleValue, handleLastAngle, handleAngle;
        private string toolHint;
        private BuildingPart toolTarget;
        private BuildingPart lastMenuPart;
        private bool lastMenuRotation;
        private Vector3 readoutNumbers;

        internal string ReadoutLine
        {
            get
            {
                if (host.ShapeEditor != null && host.ShapeEditor.Readout.Length > 0) return host.ShapeEditor.Readout;
                bool rotation = Session.Active ? Rotating : recent != null ? recent.rotation : toolHint == "Rotation";
                int axis = Session.Active ? Axis : recent != null ? recent.axis : -1;
                string axes = axis >= 0 ? "XYZ"[axis].ToString() : "X,Y,Z";
                if (Session.Active && PlaneLock) axes = axis == 0 ? "Y,Z" : axis == 1 ? "X,Z" : "X,Y";
                if (Session.Active && Trackball) axes = "X,Y";
                string number;
                if (!Session.Active) number = Display(recent != null ? recent.value : 0);
                else if (handleOperation) number = numeric && !inverse[0] ? numbers[0] : Display(handleValue);
                else
                {
                    int count = ComponentCount;
                    var values = new string[count];
                    for (int i = 0; i < count; i++) values[i] = numeric && numbers[i].Length > 0 && !inverse[i] ? numbers[i] : Display(readoutNumbers[i]);
                    number = string.Join(", ", values);
                }
                string space = Session.Active ? (handleOperation || Local ? "local" : "global") : recent != null ? "local" : "";
                  if (Session.Active && rotation && !handleOperation && (Trackball || axis < 0))
                  { axes = Trackball ? "horizontal, vertical" : "view axis"; space = ""; }
                return (rotation ? "Rotate by" : Session.Duplicating ? "Move copy by" : "Move by") +
                    " (" + axes + (space.Length > 0 ? ", " + space : "") + "): " + number + (rotation ? "\u00b0" : " m") +
                    (snapHit.HasValue ? "  |  Snapped" : "");
            }
        }

        internal bool HasShapeReadout => host.ShapeEditor?.Readout.Length > 0;
        internal bool Editing => Session.Active || host.ShapeEditor?.Active == true;
        internal bool HasReadout => Session.Active || recent != null || toolHint != null || HasShapeReadout;
        internal string ReadoutTitle => Session.Active
            ? (Rotating ? "Rotation" : "Movement") + (Axis >= 0 ? "  " + "XYZ"[Axis] + (handleOperation ? " handle" : Local ? " Local" : " Global") : Trackball ? "  Trackball" : "  View")
            : recent != null ? (recent.rotation ? "Rotation" : "Movement") + "  " + "XYZ"[recent.axis] + " handle" : toolHint;
        private static string Display(float value) => (Mathf.Abs(value) < 0.0000005f ? 0 : value).ToString("0.######", CultureInfo.InvariantCulture);
        internal string ReadoutValue => !ValidInput && Session.Active ? "Enter a valid number" :
            Session.Active && handleOperation ? (numeric && !inverse[0] ? numbers[0] : Display(handleValue)) + (Rotating ? " deg" : " m") :
            Session.Active ? Status : recent != null ? Display(recent.value) + (recent.rotation ? " deg" : " m") : "Choose an axis handle";

        internal void NativeToolOpened(PartPlacer source, bool rotation)
        {
            if (source != placer || source.currentSelectedPart == null) return;
            lastMenuPart = source.currentSelectedPart;
            lastMenuRotation = rotation;
            host.ShapeEditor?.ClearHint();
            Cancel(); toolTarget = source.currentSelectedPart; toolHint = rotation ? "Rotation" : "Movement";
        }
        internal void ClearRecentAction() { recent = null; toolHint = null; toolTarget = null; queuedText = ""; }
        private void ValidateRecentAction()
        {
            if (!host.InEditor || placer == null || placer.currentMovingPart != null) { ClearRecentAction(); return; }
            if (toolTarget != null && toolTarget != placer.currentSelectedPart) { toolTarget = null; toolHint = null; }
            if (recent != null && (recent.part == null || recent.part != placer.currentSelectedPart ||
                Vector3.Distance(recent.part.transform.position, recent.finalPosition) > 0.0001f ||
                Quaternion.Angle(recent.part.transform.rotation, recent.finalRotation) > 0.01f)) recent = null;
        }

        internal bool TryBeginHandleFromPointer()
        {
            if (Session.Active || !host.CanStartHandle || placer == null || placer.currentSelectedPart == null ||
                placer.currentMovingPart != null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame ||
                (MouseInput.Instance != null && MouseInput.Instance.PointerIsOverUI) || BuildingCamera.cam == null) return false;
            var ray = BuildingCamera.cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Component closest = null; Vector3 point = Vector3.zero; float distance = float.PositiveInfinity;
            void Check(Component gizmo)
            {
                if (gizmo == null || !gizmo.gameObject.activeInHierarchy) return;
                foreach (var collider in gizmo.GetComponentsInChildren<Collider>())
                {
                    if (!collider.enabled || !collider.Raycast(ray, out var hit, distance)) continue;
                    Component element = collider.GetComponentInChildren<MoveGizmoElement>();
                    if (element == null) element = collider.GetComponentInParent<MoveGizmoElement>();
                    if (element == null) element = collider.GetComponentInChildren<RotationGizmoElement>();
                    if (element == null) element = collider.GetComponentInParent<RotationGizmoElement>();
                    if (element == null) continue;
                    closest = element; distance = hit.distance; point = hit.point;
                }
            }
            Check(placer.moveGizmo); Check(placer.rotationGizmo);
            if (closest is MoveGizmoElement move)
            {
                var gizmo = placer.moveGizmo;
                int axis = move == gizmo.X ? 0 : move == gizmo.Y ? 1 : move == gizmo.Z ? 2 : -1;
                return axis >= 0 && move.direction != null && BeginHandle(false, axis, move.direction.forward, point);
            }
            if (closest is RotationGizmoElement ring)
            {
                var gizmo = placer.rotationGizmo;
                if (ring != gizmo.X && ring != gizmo.Y && ring != gizmo.Z) return false;
                // Native rings rotate around their own forward vector. The prefab slots
                // do not necessarily name the matching axis of the selected part.
                var normal = ring.transform.forward;
                var local = placer.currentSelectedPart.transform.InverseTransformDirection(normal);
                int axis = Mathf.Abs(local.x) >= Mathf.Abs(local.y) ? 0 : 1;
                if (Mathf.Abs(local.z) > Mathf.Abs(local[axis])) axis = 2;
                return BeginHandle(true, axis, normal * Mathf.Sign(local[axis]), point);
            }
            return false;
        }
        internal bool BeginHandle(bool rotation, int axis, Vector3 direction, Vector3 hitPoint)
        {
            if (axis < 0 || axis > 2 || direction.sqrMagnitude < 0.5f || !Begin(rotation, true, true)) return false;
            handleOperation = true; handleDragging = true; Axis = axis;
            handleDirection = direction.normalized; handleOrigin = Session.StartPosition; handleStartRotation = Session.StartRotation;
            handleGrabPoint = handleOrigin + Vector3.Project(hitPoint - handleOrigin, handleDirection);
            handleValue = handleLastAngle = handleAngle = 0;
            handleRadial = Vector3.ProjectOnPlane(PlanePoint(startMouse, handleDirection) - handleOrigin, handleDirection);
            if (handleRadial.sqrMagnitude < 0.0001f) handleRadial = Vector3.ProjectOnPlane(hitPoint - handleOrigin, handleDirection);
            if (handleRadial.sqrMagnitude < 0.0001f) handleRadial = Vector3.Cross(handleDirection, ProjectionCamera.transform.forward);
            if (handleRadial.sqrMagnitude < 0.0001f) handleRadial = Vector3.Cross(handleDirection, ProjectionCamera.transform.up);
            var cam = ProjectionCamera;
            var tangent = Vector3.Cross(handleDirection, handleRadial).normalized;
            handleTangent = ((Vector2)cam.WorldToScreenPoint(handleOrigin + handleRadial + tangent) -
                (Vector2)cam.WorldToScreenPoint(handleOrigin + handleRadial)).normalized;
            ClearHandleSelection();
            return true;
        }
        internal void UpdateHandle()
        {
            if (!Session.Active || !handleOperation || BuildingCamera.cam == null) return;
            ValidInput = true;
            snapHit = null;
            float amount = handleValue;
            if (numeric)
            {
                if (!TransformMath.Number(numbers[0], out amount)) ValidInput = false;
                if (inverse[0]) { if (Mathf.Abs(amount) < 0.0000001f) ValidInput = false; else amount = 1 / amount; }
            }
            else if (handleDragging)
            {
                var mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : startMouse;
                bool fine = keyboard != null && keyboard.shiftKey.isPressed;
                bool snap = keyboard != null && keyboard.ctrlKey.isPressed;
                if (!Rotating) amount = AxisDrag(mouse, handleDirection, handleGrabPoint);
                else if (Mathf.Abs(Vector3.Dot(handleDirection, ProjectionCamera.transform.forward)) > 0.15f)
                {
                    var radial = Vector3.ProjectOnPlane(PlanePoint(mouse, handleDirection) - handleOrigin, handleDirection);
                    if (radial.sqrMagnitude > 0.0001f)
                    {
                        float angle = Vector3.SignedAngle(handleRadial, radial, handleDirection);
                        handleAngle += Mathf.DeltaAngle(handleLastAngle, angle); handleLastAngle = angle;
                    }
                    amount = handleAngle;
                }
                else amount = Vector2.Dot(mouse - startMouse, handleTangent) * 0.5f;
                if (fine) amount *= 0.1f;
                if (Rotating) amount = RotationSteps.Apply(amount, placer.rotationGizmo);
                else if (snap) amount = TransformMath.Snap(amount, fine ? 0.1f : 1);
                else if (!Rotating && !fine && TryHoverTarget(mouse, out var hit)) amount = HoverDistance(hit, handleDirection);
            }
            if (!ValidInput || !TransformMath.Finite(amount)) { ValidInput = false; return; }
            var position = Rotating ? handleOrigin : handleOrigin + handleDirection * amount;
            if (!TransformMath.Finite(position) || position.magnitude > 1000000) { ValidInput = false; return; }
            handleValue = amount;
            Session.Preview(position, Rotating ? Quaternion.AngleAxis(amount, handleDirection) * handleStartRotation : handleStartRotation);
            Status = ReadoutTitle + "  " + Display(amount) + (Rotating ? " deg" : " m");
        }
        private RecentAction CaptureRecentAction() => new RecentAction { part = Session.Target, rotation = Rotating, axis = Axis,
            origin = handleOrigin, startRotation = handleStartRotation, direction = handleDirection, value = handleValue,
            finalPosition = Session.Target.transform.position, finalRotation = Session.Target.transform.rotation };
        internal bool BeginRecentAdjustment()
        {
            ValidateRecentAction();
            var previous = recent;
            if (previous == null || !Begin(previous.rotation, true)) return false;
            handleOperation = true; handleDragging = false; Axis = previous.axis;
            handleOrigin = previous.origin; handleStartRotation = previous.startRotation;
            handleDirection = previous.direction; handleValue = previous.value;
            if (Session.IsGroup && previous.rotation)
                handleStartRotation = Quaternion.AngleAxis(-previous.value, handleDirection) * Session.StartRotation;
            return true;
        }
        private void ClearHandleSelection()
        {
            if (placer == null) return;
            if (placer.moveGizmo != null)
                foreach (var element in placer.moveGizmo.GetComponentsInChildren<MoveGizmoElement>(true)) { element.selected = false; element.highlighted = false; }
            if (placer.rotationGizmo != null)
                foreach (var element in placer.rotationGizmo.GetComponentsInChildren<RotationGizmoElement>(true)) { element.selected = false; element.highlighted = false; }
        }
    }
    [HarmonyPatch(typeof(PartPlacer), "RotatePart")]
    internal static class PrecisionRotationToolPatch
    {
        private static void Postfix(PartPlacer __instance) { Plugin.Instance?.Precision?.NativeToolOpened(__instance, true); }
    }
    [HarmonyPatch(typeof(PartPlacer), "MovePart", new Type[0])]
    internal static class PrecisionMovementToolPatch
    {
        private static void Postfix(PartPlacer __instance) { Plugin.Instance?.Precision?.NativeToolOpened(__instance, false); }
    }
}

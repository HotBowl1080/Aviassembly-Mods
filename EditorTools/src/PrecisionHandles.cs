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
        private Vector3 handleDirection, handleOrigin, handleRadial;
        private Quaternion handleStartRotation;
        private Vector2 handleTangent;
        private float handleValue, handleLastAngle, handleAngle;
        private string toolHint;
        private BuildingPart toolTarget;
        private Vector3 readoutNumbers;

        internal string ReadoutLine
        {
            get
            {
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
                    int count = Trackball || PlaneLock ? 2 : axis >= 0 || rotation ? 1 : 3;
                    var values = new string[count];
                    for (int i = 0; i < count; i++) values[i] = numeric && numbers[i].Length > 0 && !inverse[i] ? numbers[i] : Display(readoutNumbers[i]);
                    number = string.Join(", ", values);
                }
                return (rotation ? "Rotation" : "Direction") + " (" + axes + "): " + number + (rotation ? "\u00b0" : " m");
            }
        }

        internal bool HasReadout => Session.Active || recent != null || toolHint != null;
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
            Cancel(); toolTarget = source.currentSelectedPart; toolHint = rotation ? "Rotation" : "Movement";
        }
        private void ClearRecentAction() { recent = null; toolHint = null; toolTarget = null; queuedText = ""; }
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
                int axis = ring == gizmo.X ? 0 : ring == gizmo.Y ? 1 : ring == gizmo.Z ? 2 : -1;
                var basis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
                return axis >= 0 && BeginHandle(true, axis, gizmo.transform.rotation * basis, point);
            }
            return false;
        }
        internal bool BeginHandle(bool rotation, int axis, Vector3 direction, Vector3 hitPoint)
        {
            if (axis < 0 || axis > 2 || direction.sqrMagnitude < 0.5f || !Begin(rotation, true, true)) return false;
            handleOperation = true; handleDragging = true; Axis = axis;
            handleDirection = direction.normalized; handleOrigin = Session.StartPosition; handleStartRotation = Session.StartRotation;
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
                if (!Rotating) amount = AxisPoint(mouse, handleDirection) - AxisPoint(startMouse, handleDirection);
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
                if (snap) amount = TransformMath.Snap(amount, Rotating ? (fine ? 1 : 5) : (fine ? 0.1f : 1));
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

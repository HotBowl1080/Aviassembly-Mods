using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aviassembly.EditorTools
{
    internal enum ShapeField { Width, Height, EndScale, Length, OffsetX, OffsetY, Roundness, Corner0, Corner1, Corner2, Corner3, Hollowness, Scale }
    internal sealed class ShapePrecision : IDisposable
    {
        private sealed class Entry
        {
            internal BuildingPart part;
            internal ProceduralFuselage fuselage;
            internal ProceduralFuselageTransform before, preview;
            internal Vector3 position;
            internal float price;
        }
        private readonly Plugin host;
        private readonly List<Entry> entries = new List<Entry>();
        private PartPlacer placer;
        private BuildingPart target;
        private Keyboard keyboard;
        private PrecisionInputBlocker blocker;
        private TransformSnapshot snapshot;
        private ShapeField field;
        private int side, tickFrame = -1, consumed = -1, beginFrame;
        private bool mouseDrag, numeric, suspended;
        private string typed = "", queued = "";
        private Vector2 startMouse;
        private float initialValue, value;
        private Vector3 pivot;
        private ShapeField recentField;
        private int recentSide;
        private BuildingPart recentTarget;
        private ProceduralFuselageTransform recentTransform;
        internal bool Active { get; private set; }
        internal bool BlockInput => Active || consumed == Time.frameCount;
        internal bool Valid { get; private set; } = true;
        internal string Readout { get; private set; } = "";
        internal bool OwnsBlocker(InputBlocker b) => b != null && b == blocker;
        internal ShapePrecision(Plugin host) { this.host = host; }
        internal void ClearHint() { if (!Active) { Readout = ""; recentTarget = null; queued = ""; } }
        internal void ObserveSelection(BuildingPart next)
        { if (target != next) { Cancel(); Readout = ""; recentTarget = null; } }
        internal void Tick(PartPlacer source)
        {
            placer = source;
            if (tickFrame == Time.frameCount) return; tickFrame = Time.frameCount;
            if (!Active && consumed != Time.frameCount) ReleaseBlocker();
            if (keyboard != Keyboard.current)
            { if (keyboard != null) keyboard.onTextInput -= OnText; keyboard = Keyboard.current; if (keyboard != null) keyboard.onTextInput += OnText; }
            if (Active && (!host.PrecisionContextValid || target == null || placer.currentSelectedPart != target || placer.currentMovingPart != null || !Application.isFocused)) { Cancel(); return; }
            if (Application.isFocused) ProcessInput();
        }
        private void OnText(char c)
        {
            if ((Active || recentTarget != null) && !host.TextEntryActive && keyboard != null && !keyboard.ctrlKey.isPressed && !keyboard.altKey.isPressed &&
                (char.IsDigit(c) || c == '.' || c == '-')) queued += c;
        }
        internal void ProcessInput()
        {
            if (keyboard == null) keyboard = Keyboard.current;
            if (keyboard == null || placer == null) return;
            var mouse = Mouse.current;
            if (!Active)
            {
                if (!host.PrecisionContextValid || host.TextEntryActive || host.Precision.BlockGameInput || host.Selection?.BlockInput == true || placer.currentMovingPart != null) { queued = ""; return; }
                if (queued.Length > 0 && recentTarget != null && placer.currentSelectedPart == recentTarget)
                {
                    var proc = recentTarget.GetComponent<ProceduralFuselage>();
                    if (proc != null && proc.AppliedTransform.Equals(recentTransform, false)) Begin(recentField, recentSide, false);
                    if (!Active) queued = "";
                }
                if (!Active && keyboard.sKey.wasPressedThisFrame && !keyboard.ctrlKey.isPressed && !keyboard.altKey.isPressed) Begin(ShapeField.Scale, 0, false);
                if (!Active && mouse != null && mouse.leftButton.wasPressedThisFrame)
                { recentTarget = null; Readout = ""; TryHandle(); }
                if (!Active) return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame)) { Cancel(); return; }
            if (keyboard.backspaceKey.wasPressedThisFrame && typed.Length > 0) { typed = typed.Substring(0, typed.Length - 1); numeric = true; }
            foreach (var c in queued) Type(c); queued = "";
            if (numeric)
            {
                if (!float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !TransformMath.Finite(value))
                { Valid = false; Readout = Name(field) + ": " + typed + " | Enter a valid number"; return; }
            }
            else if (mouse != null)
            {
                float factor = keyboard.shiftKey.isPressed ? 0.1f : 1f;
                float change = ((mouse.position.ReadValue().x - startMouse.x) + (mouse.position.ReadValue().y - startMouse.y)) * factor;
                value = initialValue + change * (Percent(field) ? 0.2f : 0.005f);
                if (keyboard.ctrlKey.isPressed) value = TransformMath.Snap(value, Percent(field) ? 10 : 0.1f);
            }
            Preview(value);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                (!mouseDrag && Time.frameCount != beginFrame && mouse != null && mouse.leftButton.wasPressedThisFrame)) Apply();
            else if (mouseDrag && mouse != null && mouse.leftButton.wasReleasedThisFrame)
            { mouseDrag = false; if (!numeric) Apply(); }
        }
        internal void Type(char c)
        {
            if (typed.Length >= 24) return;
            if (char.IsDigit(c) || (c == '.' && !typed.Contains("."))) typed += c;
            else if (c == '-') typed = typed.StartsWith("-") ? typed.Substring(1) : "-" + typed;
            numeric = true;
        }
        private void TryHandle()
        {
            if (MouseInput.Instance != null && MouseInput.Instance.PointerIsOverUI) return;
            var gm = placer.procduralPartController?.gizmoManager;
            if (gm == null || BuildingCamera.cam == null) return;
            var ray = BuildingCamera.cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            float distance = float.PositiveInfinity; ShapeField found = default; int foundSide = -1;
            void Check(Component component, ShapeField kind, int end)
            {
                if (component == null || !component.gameObject.activeInHierarchy) return;
                foreach (var collider in component.GetComponentsInChildren<Collider>())
                    if (collider.enabled && collider.Raycast(ray, out var hit, distance)) { distance = hit.distance; found = kind; foundSide = end; }
            }
            var gizmos = new[] { gm.gizmo1, gm.gizmo2 };
            for (int i = 0; i < 2; i++)
            {
                var g = gizmos[i]; if (g == null) continue;
                Check(g.sizeGizmo, ShapeField.Length, i); Check(g.xOffset, ShapeField.OffsetX, i); Check(g.yOffset, ShapeField.OffsetY, i);
                Check(g.scaleGizmo, ShapeField.EndScale, i); Check(g.xScaleGizmo, ShapeField.Width, i); Check(g.yScaleGizmo, ShapeField.Height, i);
                Check(g.roundnessGizmo, ShapeField.Roundness, i); Check(g.hallownesGizmo, ShapeField.Hollowness, i);
                if (g.corners != null) for (int corner = 0; corner < g.corners.Length && corner < 4; corner++)
                    Check(g.corners[corner], (ShapeField)((int)ShapeField.Corner0 + ((corner + 1) % 4)), i);
            }
            if (foundSide >= 0) Begin(found, foundSide, true);
        }
        internal bool Begin(ShapeField kind, int end, bool drag, PartPlacer source = null)
        {
            if (source != null) placer = source;
            if (Active || !host.PrecisionContextValid || host.TextEntryActive || host.Precision.BlockGameInput || placer?.currentSelectedPart == null || placer.currentMovingPart != null) return false;
            var selected = host.Selection?.IsGroup == true ? host.Selection.Parts : new[] { placer.currentSelectedPart };
            if (selected.Any(p => !p.hasBeenPlaced || p.GetComponent<ProceduralFuselage>() == null))
            { Readout = "Precision shape: select only editable fuselage parts"; return false; }
            if (PlaneStorage.Instance == null || MoneyManager.Instance == null) return false;
            AccessTools.Method(typeof(PlaneStorage), "ExecuteHistoryUpdate").Invoke(PlaneStorage.Instance, null);
            entries.Clear();
            var affected = selected.ToList();
            foreach (var p in selected)
            {
                var m = placer.GetMirrorPart(p);
                if (m != null && !affected.Contains(m) && m.GetComponent<ProceduralFuselage>() != null) affected.Add(m);
            }
            foreach (var p in affected.OrderBy(p => Depth(p)))
            {
                var f = p.GetComponent<ProceduralFuselage>(); f.UpdateChildPosition();
                entries.Add(new Entry { part = p, fuselage = f, before = f.AppliedTransform, preview = f.AppliedTransform, position = p.transform.position, price = p.price });
            }
            snapshot = new TransformSnapshot(affected);
            target = placer.currentSelectedPart; field = kind; side = Mathf.Clamp(end, 0, 1);
            pivot = selected.Aggregate(Vector3.zero, (v, p) => v + p.transform.position) / selected.Length;
            initialValue = GetValue(target.GetComponent<ProceduralFuselage>().AppliedTransform, field, side);
            value = initialValue; startMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            typed = ""; numeric = false; mouseDrag = drag; beginFrame = Time.frameCount; recentTarget = null;
            Active = true; Valid = true;
            if (blocker == null) blocker = new GameObject("EditorTools shape input").AddComponent<PrecisionInputBlocker>();
            InputManager.Instance.currentInputBlocker = blocker;
            host.StopFocusTransition(); Readout = Format(value); return true;
        }
        private static int Depth(BuildingPart p) { int depth = 0; for (var t = p.transform.parent; t != null; t = t.parent) depth++; return depth; }
        internal static float GetValue(ProceduralFuselageTransform t, ShapeField field, int side)
        {
            var s = side == 0 ? t.side1 : t.side2;
            if (field == ShapeField.Width) return s.radius.x * 2;
            if (field == ShapeField.Height) return s.radius.y * 2;
            if (field == ShapeField.Length) return s.lengthOffset.z;
            if (field == ShapeField.OffsetX) return s.lengthOffset.x;
            if (field == ShapeField.OffsetY) return s.lengthOffset.y;
            if (field == ShapeField.Roundness) return s.roundness.x * 100;
            if (field >= ShapeField.Corner0 && field <= ShapeField.Corner3) return s.roundness[(int)field - (int)ShapeField.Corner0] * 100;
            if (field == ShapeField.Hollowness) return s.hallowness * 100;
            return 1;
        }
        internal static bool Change(ProceduralFuselageTransform original, ShapeField field, int side, float value, out ProceduralFuselageTransform result)
        {
            result = original; if (!TransformMath.Finite(value) || Mathf.Abs(value) > 1000) return false;
            if (field == ShapeField.Scale)
            {
                if (value <= 0) return false;
                result.side1.radius *= value; result.side2.radius *= value;
                result.side1.lengthOffset *= value; result.side2.lengthOffset *= value;
            }
            else
            {
                var s = side == 0 ? original.side1 : original.side2;
                if (field == ShapeField.Width) s.radius.x = value * 0.5f;
                else if (field == ShapeField.Height) s.radius.y = value * 0.5f;
                else if (field == ShapeField.EndScale) { if (value <= 0) return false; s.radius *= value; }
                else if (field == ShapeField.Length) s.lengthOffset.z = value;
                else if (field == ShapeField.OffsetX) s.lengthOffset.x = value;
                else if (field == ShapeField.OffsetY) s.lengthOffset.y = value;
                else if (field == ShapeField.Roundness) s.roundness = Vector4.one * (value / 100);
                else if (field >= ShapeField.Corner0 && field <= ShapeField.Corner3) s.roundness[(int)field - (int)ShapeField.Corner0] = value / 100;
                else if (field == ShapeField.Hollowness) s.hallowness = value / 100;
                if (side == 0) result.side1 = s; else result.side2 = s;
            }
            foreach (var s in new[] { result.side1, result.side2 })
            {
                if (s.radius.x < 0.035f || s.radius.y < 0.035f || s.radius.x > 1.5f || s.radius.y > 1.5f || s.hallowness < 0 || s.hallowness > 0.9f) return false;
                for (int i = 0; i < 4; i++) if (s.roundness[i] < 0 || s.roundness[i] > 1) return false;
            }
            return result.side1.lengthOffset.z + result.side2.lengthOffset.z >= 0.1f;
        }
        internal bool Preview(float number)
        {
            if (!Active || suspended) return false;
            value = number;
            foreach (var e in entries)
                if (e.part == null || !Change(e.before, field, side, number, out e.preview))
                { Valid = false; Readout = Name(field) + ": value outside native limits"; return false; }
            Valid = true; snapshot.Restore();
            foreach (var e in entries) e.fuselage.PreviewTransformation(e.preview);
            if (field == ShapeField.Scale)
                foreach (var e in entries) e.part.SetPosition(pivot + (e.position - pivot) * number);
            Readout = Format(number); return true;
        }
        private string Format(float v) => Name(field) + (field == ShapeField.Scale ? "" : " (End " + (side + 1) + ")") + ": " + v.ToString("0.######", CultureInfo.InvariantCulture) +
            (Percent(field) ? "%" : field == ShapeField.Scale || field == ShapeField.EndScale ? "x" : " m");
        private static bool Percent(ShapeField f) => f >= ShapeField.Roundness && f <= ShapeField.Hollowness;
        private static string Name(ShapeField f) => f >= ShapeField.Corner0 && f <= ShapeField.Corner3 ? "Corner " + ((int)f - (int)ShapeField.Corner0 + 1) + " roundness" :
            f == ShapeField.EndScale ? "End scale" : f == ShapeField.OffsetX ? "Offset X" : f == ShapeField.OffsetY ? "Offset Y" : f == ShapeField.Length ? "Length offset" : f.ToString();
        internal bool Apply()
        {
            if (!Active || !Valid) return false;
            float cost = entries.Sum(e => e.fuselage.GetPrice(e.preview) - e.fuselage.GetPrice(e.before));
            if (!MoneyManager.Instance.HasEnoughMoney(cost)) { Readout = "Precision shape: not enough money"; return false; }
            float moneyBefore = MoneyManager.Instance.money;
            try
            {
                foreach (var e in entries) e.fuselage.ApplyTransformation(e.preview, true);
                foreach (var e in entries) placer.partContainer.UpdateAttachment(e.part);
                placer.partContainer.UpdateUsedSnapPoints(); placer.decalPlacer?.RecalculateDecals(null, null);
            }
            catch
            {
                foreach (var e in entries) if (e.fuselage != null) e.fuselage.ApplyTransformation(e.before, true);
                MoneyManager.Instance.money = moneyBefore; snapshot.Restore(); End(); throw;
            }
            var remembered = target;
            End(); PlaneStorage.Instance.UpdateHistory(); ResetGizmos();
            // Absolute dimensions and percentages can be readjusted after release.
            // Multipliers intentionally start a new operation with S or another click.
            if (field != ShapeField.Scale && field != ShapeField.EndScale)
            { recentTarget = remembered; recentField = field; recentSide = side; recentTransform = remembered.GetComponent<ProceduralFuselage>().AppliedTransform; }
            return true;
        }
        internal void Cancel()
        {
            recentTarget = null;
            if (!Active) return;
            RestoreOriginal(); End(); Readout = ""; ResetGizmos();
        }
        private void RestoreOriginal()
        {
            foreach (var e in entries) if (e.fuselage != null) { e.fuselage.PreviewTransformation(e.before); e.part.price = e.price; }
            snapshot?.Restore();
        }
        internal bool BeforeSave() { if (!Active || suspended) return false; suspended = true; RestoreOriginal(); return true; }
        internal void AfterSave() { suspended = false; if (Active) Preview(value); }
        private void ResetGizmos()
        {
            var manager = placer?.procduralPartController;
            var f = target != null ? target.GetComponent<ProceduralFuselage>() : null;
            if (manager == null || f == null) return;
            AccessTools.Field(typeof(ProceduralPartManager), "currentTransform").SetValue(manager, f.AppliedTransform);
            if (manager.gizmoManager?.gizmo1 != null && manager.gizmoManager.gizmo2 != null) manager.gizmoManager.ResetGizmos(f);
        }
        private void End() { Active = false; consumed = Time.frameCount; queued = ""; mouseDrag = false; }
        private void ReleaseBlocker()
        { if (blocker == null) return; if (InputManager.Instance != null && InputManager.Instance.currentInputBlocker == blocker) InputManager.Instance.currentInputBlocker = null; UnityEngine.Object.Destroy(blocker.gameObject); blocker = null; }
        public void Dispose() { Cancel(); ReleaseBlocker(); if (keyboard != null) keyboard.onTextInput -= OnText; }
    }
    [HarmonyPatch(typeof(ProceduralPartManager), "UpdateProcduralPartController")]
    internal static class ShapeNativeUpdatePatch
    { private static bool Prefix() => Plugin.Instance?.ShapeEditor?.BlockInput != true; }
    // All native procedural handles are routed through ShapePrecision. Native idle
    // updates otherwise quantize the committed dimensions and corners every frame.
    [HarmonyPatch(typeof(ProceduralGizmo), "GetUpdatedFuselageSide")]
    internal static class ShapePreserveExactPatch
    {
        private static bool Prefix(ProceduralFuselageSide __0, ref ProceduralFuselageSide __result)
        {
            if (Plugin.Instance?.InEditor != true || Plugin.Instance.ShapeEditor == null) return true;
            __result = __0; return false;
        }
    }
    [HarmonyPatch(typeof(PlaneStorage), "Save")]
    internal static class ShapeSavePatch
    {
        private static void Prefix(out bool __state) { __state = Plugin.Instance?.ShapeEditor?.BeforeSave() == true; }
        private static Exception Finalizer(Exception __exception, bool __state) { if (__state) Plugin.Instance?.ShapeEditor?.AfterSave(); return __exception; }
    }
    [HarmonyPatch]
    internal static class ShapeHandleUpdatePatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        { yield return AccessTools.Method(typeof(ProceduralAxisScaleGizmo), "LateUpdate"); yield return AccessTools.Method(typeof(ProceduralScaleGizmos), "LateUpdate"); yield return AccessTools.Method(typeof(ProceduralSizeGizmo), "LateUpdate"); }
        private static bool Prefix() => Plugin.Instance?.ShapeEditor?.BlockInput != true;
    }
}

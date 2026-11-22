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
        private const float MinRadius = 0.035f, MaxRadius = 1.5f, MinLength = 0.1f;
        private const float MaxRoundness = 100f, MaxHollowness = 90f, MaxNumber = 1000f;
        private const float FineSensitivity = 0.1f;
        private static readonly System.Reflection.FieldInfo NativeSelectedPart = AccessTools.Field(typeof(ProceduralPartManager), "selectedPart");
        private static readonly System.Reflection.FieldInfo NativeSnapPoint = AccessTools.Field(typeof(ProceduralPartManager), "selectedSnapPoint");
        private static readonly System.Reflection.FieldInfo NativeTransform = AccessTools.Field(typeof(ProceduralPartManager), "currentTransform");

        private sealed class Entry
        {
            internal BuildingPart part;
            internal ProceduralFuselage fuselage;
            internal ProceduralFuselageTransform before, preview;
            internal Vector3 position, scaleOffset;
            internal float price, displayedOffset;
        }
        private readonly Plugin host;
        private readonly List<Entry> entries = new List<Entry>();
        private PartPlacer placer;
        private BuildingPart target;
        private BuildingPart selectionAtStart;
        private Keyboard keyboard;
        private PrecisionInputBlocker blocker;
        private TransformSnapshot snapshot;
        private ShapeField field;
        private int side, tickFrame = -1, consumed = -1, beginFrame;
        private bool mouseDrag, numeric, suspended, previewFromMouse, readjustOffset;
        private string typed = "", queued = "";
        private Vector2 lastMouse;
        private Vector2 dragDirection = Vector2.right;
        private float unitsPerPixel = 0.005f;
        private bool endOnDrag;
        private Camera offsetCamera;
        private Vector3 offsetGrabPoint, offsetAxis, offsetCenter;
        private Vector2 offsetStartMouse;
        private GeometrySnap offsetSnap;
        private Vector3? offsetSnapHit;
        private float initialValue, previewValue, minimum, maximum;
        private float dragValue, dragMinimum, dragMaximum;
        private ShapeField recentField;
        private int recentSide;
        private BuildingPart recentTarget;
        internal bool Active { get; private set; }
        internal bool BlockInput => Active || consumed == Time.frameCount;
        internal bool Valid { get; private set; } = true;
        internal string Readout { get; private set; } = "";
        internal bool OwnsBlocker(InputBlocker b) => b != null && b == blocker;
        internal ShapePrecision(Plugin host) { this.host = host; }
        internal void ClearHint() { if (!Active) Cancel(); }
        internal void ObserveSelection(BuildingPart next)
        { if (!Active || selectionAtStart != next) Cancel(); }
        internal void Tick(PartPlacer source)
        {
            placer = source;
            if (tickFrame == Time.frameCount) return; tickFrame = Time.frameCount;
            if (!Active && consumed != Time.frameCount) ReleaseBlocker();
            if (keyboard != Keyboard.current)
            { if (keyboard != null) keyboard.onTextInput -= OnText; keyboard = Keyboard.current; if (keyboard != null) keyboard.onTextInput += OnText; }
            if (Active && (!host.PrecisionContextValid || host.TextEntryActive || target == null || placer.currentSelectedPart != selectionAtStart || placer.currentMovingPart != null || !Application.isFocused)) { Cancel(); return; }
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
                if (BlockInput || !host.PrecisionContextValid || host.TextEntryActive || host.Precision.BlockGameInput || host.Selection?.BlockInput == true || placer.currentMovingPart != null) { queued = ""; return; }
                if (queued.Length > 0 && recentTarget != null && placer.currentSelectedPart == selectionAtStart)
                {
                    if (entries.All(e => e.fuselage != null && SameTransform(e.fuselage.AppliedTransform, e.before)))
                        Begin(recentField, recentSide, false, handlePart: recentTarget, readjust: true);
                    if (!Active) ClearHint();
                }
                if (!Active && keyboard.sKey.wasPressedThisFrame && !keyboard.ctrlKey.isPressed && !keyboard.altKey.isPressed) Begin(ShapeField.Scale, 0, false);
                if (!Active && mouse != null && mouse.leftButton.wasPressedThisFrame)
                { ClearHint(); TryHandle(); }
                if (!Active) return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame)) { Cancel(); return; }
            if (keyboard.backspaceKey.wasPressedThisFrame && typed.Length > 0) { typed = typed.Substring(0, typed.Length - 1); numeric = true; }
            foreach (var c in queued) Type(c); queued = "";
            bool released = mouseDrag && mouse != null && !mouse.leftButton.isPressed;
            if (released) mouseDrag = false;
            if (numeric)
            {
                if (!float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || !TransformMath.Finite(number))
                { Valid = false; Readout = Name(field) + ": " + typed + " | Enter a valid number"; return; }
                Preview(number);
            }
            else if (mouse != null)
            {
                UpdateDrag(mouse.position.ReadValue(), keyboard.shiftKey.isPressed, keyboard.ctrlKey.isPressed);
            }
            if (released && !numeric)
            {
                if (!Apply()) Cancel(keepReadout: true);
            }
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                (!mouseDrag && Time.frameCount != beginFrame && mouse != null && mouse.leftButton.wasPressedThisFrame && !host.PointerOverPanel())) Apply();
        }
        private void UpdateDrag(Vector2 mouse, bool fine, bool snap)
        {
            var motion = mouse - lastMouse;
            float delta = offsetCamera != null
                ? DragProjection.AxisDisplacement(offsetCamera, offsetGrabPoint + offsetAxis * (dragValue - initialValue),
                    offsetAxis, motion, 0.02f, out endOnDrag)
                : Vector2.Dot(motion, dragDirection) * unitsPerPixel;
            lastMouse = mouse;
            // Accumulate only new motion so pressing Shift never rescales the drag so far.
            if (delta != 0 && dragMinimum <= dragMaximum)
                dragValue = Mathf.Clamp(dragValue + delta * (fine ? FineSensitivity : 1f), dragMinimum, dragMaximum);
            float number = dragValue;
            offsetSnapHit = null;
            if (number != initialValue && snap && dragMinimum <= dragMaximum)
            {
                float step = Percent(field) ? (fine ? 1 : 10) : (fine ? 0.01f : 0.1f);
                number = Mathf.Clamp(TransformMath.Snap(number, step), dragMinimum, dragMaximum);
            }
            else if (!snap && !fine && offsetSnap != null && (mouse - offsetStartMouse).sqrMagnitude >= 9 &&
                PartPlacer.snapMode && !offsetSnap.PointerOverUI(host))
            {
                var hit = offsetSnap.Pick(mouse, true);
                if (hit.HasValue)
                {
                    float aligned = initialValue + Vector3.Dot(hit.Value - offsetCenter, offsetAxis);
                    if (aligned >= dragMinimum && aligned <= dragMaximum)
                    {
                        number = aligned;
                        offsetSnapHit = hit;
                    }
                }
            }
            Preview(number, true);
        }
        internal void DrawSnapMarker()
        {
            if (Active && !numeric && offsetSnapHit.HasValue) GeometrySnap.DrawMarker(offsetCamera, offsetSnapHit.Value);
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
            var manager = placer.procduralPartController;
            var gm = manager?.gizmoManager;
            if (gm == null || BuildingCamera.cam == null) return;
            var selected = (BuildingPart)NativeSelectedPart.GetValue(manager);
            if (selected == null) return;
            Physics.SyncTransforms();
            var ray = BuildingCamera.cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            float distance = float.PositiveInfinity; ShapeField found = default; int foundSide = -1;
            Behaviour foundHandle = null; Vector3 foundPoint = Vector3.zero;
            void Check(Behaviour component, ShapeField kind, int end)
            {
                if (component == null || !component.isActiveAndEnabled) return;
                foreach (var collider in component.GetComponentsInChildren<Collider>())
                    if (collider.enabled && collider.Raycast(ray, out var hit, distance)) { distance = hit.distance; found = kind; foundSide = end; foundHandle = component; foundPoint = hit.point; }
            }
            var gizmos = new[] { gm.gizmo1, gm.gizmo2 };
            for (int i = 0; i < 2; i++)
            {
                var g = gizmos[i]; if (g == null || !g.isActiveAndEnabled) continue;
                Check(g.sizeGizmo, ShapeField.Length, i); Check(g.xOffset, ShapeField.OffsetX, i); Check(g.yOffset, ShapeField.OffsetY, i);
                Check(g.scaleGizmo, ShapeField.EndScale, i); Check(g.xScaleGizmo, ShapeField.Width, i); Check(g.yScaleGizmo, ShapeField.Height, i);
                Check(g.roundnessGizmo, ShapeField.Roundness, i); Check(g.hallownesGizmo, ShapeField.Hollowness, i);
                if (g.corners != null) for (int corner = 0; corner < g.corners.Length && corner < 4; corner++)
                    Check(g.corners[corner], (ShapeField)((int)ShapeField.Corner0 + ((corner + 1) % 4)), i);
            }
            if (foundSide >= 0 && Begin(found, foundSide, true, handlePart: selected))
            {
                var shape = selected.GetComponent<ProceduralFuselage>().AppliedTransform;
                var center = foundSide == 0 ? shape.GetBaseOrigin1(selected.transform) : shape.GetBaseOrigin2(selected.transform);
                ConfigureHandleDrag(foundHandle, foundPoint, center);
            }
        }
        internal void SetDragAxis(Camera camera, Vector3 origin, Vector3 direction, float unitsPerWorld)
        {
            var a = camera.WorldToScreenPoint(origin);
            var b = camera.WorldToScreenPoint(origin + direction.normalized);
            var projected = (Vector2)(b - a);
            endOnDrag = a.z <= 0 || b.z <= 0 || projected.magnitude < 4;
            dragDirection = endOnDrag ? Vector2.up : projected.normalized;
            unitsPerPixel = endOnDrag ? unitsPerWorld / 200f : unitsPerWorld / projected.magnitude;
        }
        private void ConfigureHandleDrag(Component handle, Vector3 hit, Vector3 center)
        {
            var camera = BuildingCamera.cam;
            if (field == ShapeField.Length || field == ShapeField.OffsetX || field == ShapeField.OffsetY)
            {
                offsetCamera = camera;
                offsetGrabPoint = hit;
                offsetCenter = center;
                offsetStartMouse = lastMouse;
                var transform = target.transform;
                if (field == ShapeField.OffsetX) offsetAxis = transform.right;
                else if (field == ShapeField.OffsetY) offsetAxis = transform.up * Mathf.Sign(transform.localScale.y);
                else offsetAxis = side == 0 ? transform.forward : -transform.forward;
                if (PartPlacer.snapMode) offsetSnap = new GeometrySnap(camera, host, snapshot.Contains);
                return;
            }
            if (handle is ProceduralScaleGizmos)
            {
                // Pull corners inward to round them. Pull outward to enlarge a ring or an end.
                SetRadialDrag(camera, center);
                return;
            }
            var direction = handle.transform.forward;
            if (handle is ProceduralSizeGizmo size && size.transformDirection.sqrMagnitude > 0.001f) direction = size.transformDirection;
            if (handle is ProceduralAxisScaleGizmo axis && axis.sensitivity < 0) direction = -direction;
            SetDragAxis(camera, hit, direction, field == ShapeField.Width || field == ShapeField.Height ? 5f : 2.5f);
            float pixelScale = PixelScale(camera);
            unitsPerPixel = Mathf.Clamp(unitsPerPixel, 0.005f * pixelScale, 0.06f * pixelScale);
        }
        private static float PixelScale(Camera camera) => 1080f / Mathf.Max(480, camera.pixelHeight);
        private static float PercentPerPixel(ShapeField field) => field == ShapeField.Hollowness ? 0.45f : 0.38f;

        private void SetRadialDrag(Camera camera, Vector3 center)
        {
            var radial = lastMouse - (Vector2)camera.WorldToScreenPoint(center);
            dragDirection = radial.sqrMagnitude < 16 ? Vector2.right : radial.normalized;
            if (field >= ShapeField.Roundness && field <= ShapeField.Corner3) dragDirection = -dragDirection;
            // Percent handles use percentage points, while scale uses a multiplier.
            unitsPerPixel = (Percent(field) ? PercentPerPixel(field) : 0.0125f) * PixelScale(camera);
            endOnDrag = false;
        }
        internal bool Begin(ShapeField kind, int end, bool drag, PartPlacer source = null, BuildingPart handlePart = null, bool readjust = false)
        {
            if (source != null) placer = source;
            if (Active || !host.PrecisionContextValid || host.TextEntryActive || host.Precision.BlockGameInput || placer == null || placer.currentMovingPart != null) return false;
            var part = handlePart != null ? handlePart : placer.currentSelectedPart;
            if (part == null) return false;
            var selected = host.Selection?.IsGroup == true ? host.Selection.Parts : new[] { part };
            if (!selected.Contains(part)) selected = new[] { part };
            if (selected.Any(p => !p.hasBeenPlaced || p.GetComponent<ProceduralFuselage>() == null))
            { Readout = "Precision shape: select only editable fuselage parts"; return false; }
            if (PlaneStorage.Instance == null || MoneyManager.Instance == null) return false;
            AccessTools.Method(typeof(PlaneStorage), "ExecuteHistoryUpdate").Invoke(PlaneStorage.Instance, null);
            // Applying a shape can move the stock pivot. Keep the player's last
            // displayed coordinate as the reference for subsequent numeric edits.
            readjustOffset = readjust && (kind == ShapeField.Length || kind == ShapeField.OffsetX || kind == ShapeField.OffsetY);
            var previousOffsets = readjustOffset ? entries.ToDictionary(e => e.part, e => e.displayedOffset) : null;
            entries.Clear();
            var pivot = selected.Aggregate(Vector3.zero, (v, p) => v + p.transform.position) / selected.Length;
            var offsets = selected.ToDictionary(p => p, p => p.transform.position - pivot);
            foreach (var p in selected)
            {
                var m = placer.GetMirrorPart(p);
                if (m != null && !offsets.ContainsKey(m) && m.GetComponent<ProceduralFuselage>() != null)
                    offsets[m] = placer.symmetryPlane.GetMirroredPosition(p.transform.position) - placer.symmetryPlane.GetMirroredPosition(pivot);
            }
            foreach (var p in offsets.Keys.OrderBy(Depth))
            {
                var f = p.GetComponent<ProceduralFuselage>(); f.UpdateChildPosition();
                entries.Add(new Entry { part = p, fuselage = f, before = f.AppliedTransform, preview = f.AppliedTransform,
                    position = p.transform.position, scaleOffset = offsets[p], price = p.price,
                    displayedOffset = previousOffsets != null && previousOffsets.TryGetValue(p, out float previous) ? previous : GetValue(f.AppliedTransform, kind, end) });
            }
            snapshot = new TransformSnapshot(offsets.Keys);
            target = part; selectionAtStart = placer.currentSelectedPart; field = kind; side = Mathf.Clamp(end, 0, 1);
            initialValue = entries.First(e => e.part == target).displayedOffset;
            GetLimits();
            SetDragLimits();
            dragValue = initialValue;
            previewValue = initialValue; previewFromMouse = true;
            lastMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            offsetCamera = null;
            dragDirection = Vector2.right; unitsPerPixel = Percent(field) ? PercentPerPixel(field) : 0.01f; endOnDrag = false;
            if (kind == ShapeField.Scale && BuildingCamera.cam != null) SetRadialDrag(BuildingCamera.cam, pivot);
            typed = ""; numeric = false; mouseDrag = drag; beginFrame = Time.frameCount; recentTarget = null;
            Active = true; Valid = true;
            if (blocker == null) blocker = new GameObject("EditorTools shape input").AddComponent<PrecisionInputBlocker>();
            if (InputManager.Instance != null) InputManager.Instance.currentInputBlocker = blocker;
            host.Precision.ClearRecentAction();
            ClearNativePress();
            host.HoldCameraPosition(); Readout = FormatPreview(); return true;
        }
        private void SetDragLimits()
        {
            dragMinimum = minimum;
            dragMaximum = maximum;
            if (field != ShapeField.Roundness) return;
            float lowestCorner = 1, highestCorner = 0;
            foreach (var entry in entries)
            {
                var corners = (side == 0 ? entry.before.side1 : entry.before.side2).roundness;
                for (int corner = 0; corner < 4; corner++)
                {
                    lowestCorner = Mathf.Min(lowestCorner, corners[corner]);
                    highestCorner = Mathf.Max(highestCorner, corners[corner]);
                }
            }
            dragMinimum = initialValue - highestCorner * MaxRoundness;
            dragMaximum = initialValue + (1 - lowestCorner) * MaxRoundness;
        }
        private static int Depth(BuildingPart part)
        {
            // Native attachments usually share a Unity parent. SetPosition moves their logical children too.
            int depth = 0;
            while (part != null)
            {
                var parent = part.transform.parent;
                part = part.parent != null ? part.parent : parent != null ? parent.GetComponentInParent<BuildingPart>() : null;
                depth++;
            }
            return depth;
        }
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
        private void GetLimits()
        {
            minimum = -MaxNumber;
            maximum = MaxNumber;
            switch (field)
            {
                case ShapeField.Width:
                case ShapeField.Height:
                    minimum = 2 * MinRadius;
                    maximum = 2 * MaxRadius;
                    break;
                case ShapeField.Roundness:
                case ShapeField.Corner0:
                case ShapeField.Corner1:
                case ShapeField.Corner2:
                case ShapeField.Corner3:
                case ShapeField.Hollowness:
                    minimum = 0;
                    maximum = field == ShapeField.Hollowness ? MaxHollowness : MaxRoundness;
                    break;
                case ShapeField.OffsetX:
                case ShapeField.OffsetY:
                case ShapeField.Length:
                    minimum = entries.Max(e => DisplayedOffset(e, field == ShapeField.Length
                        ? MinLength - (side == 0 ? e.before.side2 : e.before.side1).lengthOffset.z : -MaxNumber));
                    maximum = entries.Min(e => DisplayedOffset(e, MaxNumber));
                    break;
                case ShapeField.EndScale:
                case ShapeField.Scale:
                    minimum = 0;
                    foreach (var e in entries)
                    {
                        if (field == ShapeField.EndScale)
                        {
                            LimitScale(side == 0 ? e.before.side1 : e.before.side2);
                            continue;
                        }
                        LimitScale(e.before.side1);
                        LimitScale(e.before.side2);
                        float length = e.before.side1.lengthOffset.z + e.before.side2.lengthOffset.z;
                        minimum = Mathf.Max(minimum, MinLength / length);
                        if (length <= 0) maximum = 0;
                    }
                    break;
            }
        }
        private float DisplayedOffset(Entry entry, float native) => readjustOffset
            ? entry.displayedOffset + (native - GetValue(entry.before, field, side)) : native;
        private float NativeValue(Entry entry, float displayed) => readjustOffset
            ? GetValue(entry.before, field, side) + (displayed - entry.displayedOffset) : displayed;
        private void LimitScale(ProceduralFuselageSide end)
        {
            float smaller = Mathf.Min(end.radius.x, end.radius.y);
            float larger = Mathf.Max(end.radius.x, end.radius.y);
            if (smaller <= 0 || !TransformMath.Finite(smaller) || !TransformMath.Finite(larger))
            {
                maximum = 0;
                return;
            }
            minimum = Mathf.Max(minimum, MinRadius / smaller);
            maximum = Mathf.Min(maximum, MaxRadius / larger);
        }
        private static Vector2 ScaledRadius(Vector2 radius, float factor) =>
            new Vector2(Mathf.Clamp(radius.x * factor, MinRadius, MaxRadius), Mathf.Clamp(radius.y * factor, MinRadius, MaxRadius));

        private ProceduralFuselageTransform Change(ProceduralFuselageTransform result, float number, bool relativeRoundness)
        {
            if (field == ShapeField.Scale)
            {
                result.side1.radius = ScaledRadius(result.side1.radius, number);
                result.side2.radius = ScaledRadius(result.side2.radius, number);
                result.side1.lengthOffset *= number;
                result.side2.lengthOffset *= number;
                return result;
            }
            var s = side == 0 ? result.side1 : result.side2;
            if (field == ShapeField.Width) s.radius.x = number * 0.5f;
            else if (field == ShapeField.Height) s.radius.y = number * 0.5f;
            else if (field == ShapeField.EndScale) s.radius = ScaledRadius(s.radius, number);
            else if (field == ShapeField.Length) s.lengthOffset.z = number;
            else if (field == ShapeField.OffsetX) s.lengthOffset.x = number;
            else if (field == ShapeField.OffsetY) s.lengthOffset.y = number;
            else if (field == ShapeField.Roundness && relativeRoundness)
            {
                float delta = Mathf.Clamp((number - initialValue) / MaxRoundness, -1, 1);
                for (int corner = 0; corner < 4; corner++) s.roundness[corner] = Mathf.Clamp01(s.roundness[corner] + delta);
            }
            else if (field == ShapeField.Roundness) s.roundness = Vector4.one * (number / MaxRoundness);
            else if (field >= ShapeField.Corner0 && field <= ShapeField.Corner3) s.roundness[(int)field - (int)ShapeField.Corner0] = number / MaxRoundness;
            else if (field == ShapeField.Hollowness) s.hallowness = number / 100f;
            if (side == 0) result.side1 = s; else result.side2 = s;
            return result;
        }
        internal bool Preview(float number) => Preview(number, false);
        private bool Preview(float number, bool fromMouse)
        {
            if (!Active || suspended || target == null) return false;
            bool original = fromMouse && number == initialValue;
            bool relativeRoundness = fromMouse && field == ShapeField.Roundness;
            if (!original && (!TransformMath.Finite(number) || !TransformMath.Finite(minimum) || !TransformMath.Finite(maximum) ||
                minimum > maximum || (IsScale(field) && maximum <= 0) || (!fromMouse && (number < minimum || number > maximum))))
            {
                Valid = false;
                Readout = Name(field) + (minimum > maximum || (IsScale(field) && maximum <= 0) ? " | No common range for this edit" :
                    " | Enter " + Display(minimum) + " to " + Display(maximum) + Units(field));
                return false;
            }
            if (fromMouse && !original && !relativeRoundness) number = Mathf.Clamp(number, minimum, maximum);
            original = fromMouse && number == initialValue;
            if (number != previewValue || fromMouse != previewFromMouse)
            {
                foreach (var e in entries) e.preview = original ? e.before : Change(e.before, NativeValue(e, number), relativeRoundness);
                previewValue = number;
                previewFromMouse = fromMouse;
                RenderPreview();
            }
            Valid = true;
            Readout = FormatPreview();
            return true;
        }
        private void RenderPreview()
        {
            snapshot.Restore();
            foreach (var e in entries)
            {
                if (e.fuselage == null) continue;
                e.fuselage.PreviewTransformation(e.preview);
                if (SameTransform(e.before, e.preview)) e.part.price = e.price;
            }
            if (field == ShapeField.Scale)
                foreach (var e in entries)
                    if (e.part != null) e.part.SetPosition(e.position + e.scaleOffset * (previewValue - 1));
            SyncGizmos(false);
        }
        private string FormatPreview()
        {
            string amount = Display(previewValue);
            if (field == ShapeField.Roundness)
            {
                var current = target.GetComponent<ProceduralFuselage>().CurrentTransform;
                var corners = (side == 0 ? current.side1 : current.side2).roundness;
                float low = Mathf.Min(Mathf.Min(corners.x, corners.y), Mathf.Min(corners.z, corners.w));
                float high = Mathf.Max(Mathf.Max(corners.x, corners.y), Mathf.Max(corners.z, corners.w));
                amount = Display(low * MaxRoundness);
                if (!Mathf.Approximately(low, high)) amount += " to " + Display(high * MaxRoundness);
            }
            return Name(field) + (field == ShapeField.Scale ? "" : " (End " + (side + 1) + ")") +
                (IsScale(field) ? " \u00d7 " : " = ") + amount + Units(field) +
                (offsetSnapHit.HasValue && !numeric ? " | Snapped" : endOnDrag && !numeric ? " | Drag up/down" : "");
        }
        private static string Display(float v) => v.ToString("0.######", CultureInfo.InvariantCulture);
        private static string Units(ShapeField f) => Percent(f) ? "%" : IsScale(f) ? "" : " m";
        private static bool IsScale(ShapeField f) => f == ShapeField.Scale || f == ShapeField.EndScale;
        private static bool Percent(ShapeField f) => f >= ShapeField.Roundness && f <= ShapeField.Hollowness;
        private static string Name(ShapeField f) => f >= ShapeField.Corner0 && f <= ShapeField.Corner3 ? "Corner " + ((int)f - (int)ShapeField.Corner0 + 1) + " roundness" :
            f == ShapeField.EndScale ? "End scale" : f == ShapeField.OffsetX ? "Offset X" : f == ShapeField.OffsetY ? "Offset Y" : f == ShapeField.Length ? "Length offset" : f.ToString();
        private static bool SameSide(ProceduralFuselageSide a, ProceduralFuselageSide b) =>
            a.radius.Equals(b.radius) && a.lengthOffset.Equals(b.lengthOffset) && a.roundness.Equals(b.roundness) && a.hallowness.Equals(b.hallowness);
        private static bool SameTransform(ProceduralFuselageTransform a, ProceduralFuselageTransform b) => SameSide(a.side1, b.side1) && SameSide(a.side2, b.side2);
        internal bool Apply()
        {
            if (!Active || !Valid) return false;
            bool changed = field == ShapeField.Scale && previewValue != 1 || entries.Any(e => !SameTransform(e.before, e.preview));
            if (changed)
            {
                float cost = entries.Sum(e => e.fuselage.GetPrice(e.preview) - e.fuselage.GetPrice(e.before));
                if (!MoneyManager.Instance.HasEnoughMoney(cost)) { Readout = "Precision shape: not enough money"; return false; }
                float moneyBefore = MoneyManager.Instance.money;
                try
                {
                    foreach (var e in entries)
                    {
                        if (field == ShapeField.Scale) e.part.SetPosition(e.position + e.scaleOffset * (previewValue - 1));
                        e.fuselage.ApplyTransformation(e.preview, true);
                    }
                    Physics.SyncTransforms();
                    foreach (var e in entries) placer.partContainer.UpdateAttachment(e.part);
                    placer.partContainer.UpdateUsedSnapPoints(); placer.decalPlacer?.RecalculateDecals(null, null);
                }
                catch
                {
                    snapshot.Restore();
                    foreach (var e in entries) if (e.fuselage != null) e.fuselage.ApplyTransformation(e.before, true);
                    RestoreOriginal(); MoneyManager.Instance.money = moneyBefore; End(); SyncGizmos(true); throw;
                }
            }
            End();
            if (changed) PlaneStorage.Instance.UpdateHistory();
            SyncGizmos(true);
            if (changed && !Percent(field)) host.ResizeCompleted(target);
            // Absolute dimensions and percentages can be readjusted after release.
            // Multipliers intentionally start a new operation with S or another click.
            if (!IsScale(field))
            {
                recentTarget = target; recentField = field; recentSide = side;
                foreach (var e in entries)
                {
                    if (!readjustOffset) e.displayedOffset = GetValue(e.preview, field, side);
                    else if (!previewFromMouse || previewValue != initialValue) e.displayedOffset = previewValue;
                    e.before = e.fuselage.AppliedTransform;
                }
            }
            return true;
        }
        internal void Cancel(bool keepReadout = false)
        {
            recentTarget = null; queued = "";
            if (Active) { RestoreOriginal(); End(); SyncGizmos(true); }
            entries.Clear(); target = selectionAtStart = null;
            if (!keepReadout) Readout = "";
        }
        private void RestoreOriginal()
        {
            foreach (var e in entries) if (e.fuselage != null) { e.fuselage.PreviewTransformation(e.before); e.part.price = e.price; }
            snapshot?.Restore();
        }
        internal bool BeforeSave() { if (!Active || suspended) return false; suspended = true; RestoreOriginal(); return true; }
        internal void AfterSave() { suspended = false; if (Active) RenderPreview(); }
        internal bool NativeHoverDrag => !BlockInput && placer != null && placer.currentSelectedPart == null;
        private void ClearNativePress()
        {
            var gizmos = placer.procduralPartController?.gizmoManager;
            if (gizmos == null) return;
            foreach (var end in new[] { gizmos.gizmo1, gizmos.gizmo2 })
            {
                if (end == null) continue;
                foreach (var handle in end.GetComponentsInChildren<ProceduralScaleGizmos>(true)) handle.OnDisable();
                foreach (var handle in end.GetComponentsInChildren<ProceduralAxisScaleGizmo>(true)) handle.OnDisable();
                foreach (var handle in end.GetComponentsInChildren<ProceduralSizeGizmo>(true)) handle.OnDisable();
            }
        }
        private void SyncGizmos(bool finished)
        {
            var manager = placer?.procduralPartController;
            var gizmos = manager?.gizmoManager;
            if (target == null || gizmos?.gizmo1 == null || gizmos.gizmo2 == null ||
                (BuildingPart)NativeSelectedPart.GetValue(manager) != target) return;
            var f = target.GetComponent<ProceduralFuselage>();
            if (finished)
            {
                NativeTransform.SetValue(manager, f.AppliedTransform);
                gizmos.ResetGizmos(f);
            }
            if (gizmos.gizmo1.gameObject.activeInHierarchy || gizmos.gizmo2.gameObject.activeInHierarchy)
                gizmos.UpdateGizmos(target, (Transform)NativeSnapPoint.GetValue(manager), f.CurrentTransform);
        }
        private void End()
        {
            Active = false; consumed = Time.frameCount; queued = ""; mouseDrag = false; snapshot = null;
            offsetCamera = null; offsetSnap = null; offsetSnapHit = null;
        }
        private void ReleaseBlocker()
        { if (blocker == null) return; if (InputManager.Instance != null && InputManager.Instance.currentInputBlocker == blocker) InputManager.Instance.currentInputBlocker = null; UnityEngine.Object.Destroy(blocker.gameObject); blocker = null; }
        public void Dispose() { Cancel(); ReleaseBlocker(); if (keyboard != null) keyboard.onTextInput -= OnText; }
    }
    [HarmonyPatch(typeof(ProceduralPartManager), "ApplyTransformation")]
    internal static class NativeResizeFocusPatch
    {
        private static void Prefix(ProceduralFuselage __0, ProceduralFuselageTransform __2, out bool __state)
        {
            __state = false;
            if (Plugin.Instance?.IsFocused != true || __0 == null) return;
            var before = __0.AppliedTransform;
            __state = before.side1.radius != __2.side1.radius || before.side2.radius != __2.side2.radius ||
                before.side1.lengthOffset != __2.side1.lengthOffset || before.side2.lengthOffset != __2.side2.lengthOffset;
        }
        private static void Postfix(ProceduralFuselage __0, bool __state)
        {
            if (__state) Plugin.Instance?.ResizeCompleted(__0.GetComponent<BuildingPart>());
        }
    }

    [HarmonyPatch(typeof(ProceduralPartManager), "UpdateProcduralPartController")]
    internal static class ShapeNativeUpdatePatch
    { private static bool Prefix() => Plugin.Instance?.ShapeEditor?.BlockInput != true; }
    // Idle stock updates quantize exact values. Keep the two stock handles used
    // when hovering an unselected fuselage, and only take their actual edit.
    [HarmonyPatch(typeof(ProceduralGizmo), "GetUpdatedFuselageSide")]
    internal static class ShapePreserveExactPatch
    {
        private static bool Prefix(ProceduralGizmo __instance, ProceduralFuselageSide __0, ref ProceduralFuselageSide __result,
            out (ProceduralFuselageSide original, ShapeField? field) __state)
        {
            __state = (__0, null);
            var mod = Plugin.Instance;
            if (mod?.InEditor != true || mod.ShapeEditor == null) return true;
            var mouse = MouseInput.currentMouse;
            if (mod.ShapeEditor.NativeHoverDrag && __instance.isActiveAndEnabled && __instance.currentMode == GizmoMode.Default &&
                mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame))
            {
                var length = __instance.sizeGizmo;
                var scale = __instance.scaleGizmo;
                if (length != null && length.isActiveAndEnabled && length.IsHighlighted()) __state.field = ShapeField.Length;
                else if (scale != null && scale.isActiveAndEnabled && scale.IsHighlighted()) __state.field = ShapeField.EndScale;
                if (__state.field.HasValue) return true;
            }
            __result = __0; return false;
        }
        private static void Postfix(ref ProceduralFuselageSide __result, (ProceduralFuselageSide original, ShapeField? field) __state)
        {
            if (!__state.field.HasValue) return;
            var edited = __result;
            // The stock method overwrites its first argument as it works through the handles.
            __result = __state.original;
            if (__state.field == ShapeField.Length) __result.lengthOffset.z = edited.lengthOffset.z;
            else __result.radius = edited.radius;
        }
    }
    [HarmonyPatch(typeof(PlaneStorage), "Save")]
    internal static class ShapeSavePatch
    {
        private static void Prefix(out bool __state) { __state = Plugin.Instance?.ShapeEditor?.BeforeSave() == true; }
        private static Exception Finalizer(Exception __exception, bool __state) { if (__state) Plugin.Instance?.ShapeEditor?.AfterSave(); return __exception; }
    }
    [HarmonyPatch(typeof(ProceduralGizmoManager), "LateUpdate")]
    internal static class ShapeGizmoModePatch
    {
        private static bool Prefix() => Plugin.Instance?.ShapeEditor?.BlockInput != true;
    }
}

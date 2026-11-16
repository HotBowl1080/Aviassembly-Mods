using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aviassembly.EditorTools
{
    internal sealed class MultiSelection : IDisposable
    {
        private readonly Plugin host;
        private readonly List<BuildingPart> parts = new List<BuildingPart>();
        private readonly HiddenXray highlight = new HiddenXray(new Color(1f, 0.65f, 0.1f, 0.16f), "EditorTools selected group");
        private readonly List<Renderer> renderers = new List<Renderer>();
        private PartPlacer placer;
        private PlaneContainer plane;
        private bool assigning, box, dragging;
        private Vector2 boxStart;
        private int consumed = -1, inputFrame = -1;
        private GameObject labelRoot, boxRoot;
        private TextMeshProUGUI label;
        private Canvas canvas;
        internal int Count { get { Prune(); return parts.Count; } }
        internal bool BlockInput => box || consumed == Time.frameCount;
        internal bool IsGroup => Count > 1;
        internal BuildingPart[] Parts { get { Prune(); return parts.ToArray(); } }
        internal Vector3 Pivot => parts.Count == 0 ? Vector3.zero : parts.Aggregate(Vector3.zero, (v, p) => v + p.transform.position) / parts.Count;
        internal MultiSelection(Plugin host) { this.host = host; }
        private bool Eligible(BuildingPart p) => p != null && p.hasBeenPlaced && p.gameObject.activeInHierarchy && !host.HideParts.IsHidden(p);
        private void Prune() { parts.RemoveAll(p => !Eligible(p)); }
        internal void Observe(PartPlacer source)
        {
            if (placer != source || plane != PlaneContainer.Instance) { Clear(false); placer = source; plane = PlaneContainer.Instance; }
            if (!host.InEditor) { Clear(false); return; }
            Prune();
            if (parts.Count == 0 && Eligible(source.currentSelectedPart)) parts.Add(source.currentSelectedPart);
            if (Application.isFocused) ProcessInput();
        }
        internal void SelectionAssigned(BuildingPart part)
        {
            if (assigning) return;
            parts.Clear(); if (Eligible(part)) parts.Add(part);
        }
        internal void Set(IEnumerable<BuildingPart> selection)
        {
            if (host.Precision.Session.Active || host.ShapeEditor?.Active == true) return;
            parts.Clear(); foreach (var p in selection) if (Eligible(p) && !parts.Contains(p)) parts.Add(p);
            AssignActive(parts.LastOrDefault());
        }
        internal void Toggle(BuildingPart part)
        {
            if (!Eligible(part)) return;
            var next = Parts.ToList(); if (!next.Remove(part)) next.Add(part); Set(next);
        }
        private void AssignActive(BuildingPart part)
        {
            if (placer == null) return;
            assigning = true;
            try
            {
                AccessTools.PropertySetter(typeof(PartPlacer), "currentSelectedPart").Invoke(placer, new object[] { part });
                if (part != null && placer.moveGizmo != null && placer.rotationGizmo != null)
                    AccessTools.Method(typeof(PartPlacer), "UpdateGizmos").Invoke(placer, new object[] { part });
                if (part == null)
                {
                    placer.moveGizmo?.gameObject.SetActive(false); placer.rotationGizmo?.gameObject.SetActive(false);
                }
            }
            finally { assigning = false; }
            Refresh();
        }
        internal void Clear(bool native = true)
        {
            parts.Clear(); box = dragging = false; highlight.Clear();
            if (native) AssignActive(null);
            if (labelRoot != null) labelRoot.SetActive(false);
            if (boxRoot != null) boxRoot.SetActive(false);
        }
        internal void ProcessInput()
        {
            var k = Keyboard.current; var mouse = Mouse.current;
            if (inputFrame == Time.frameCount || k == null || mouse == null) return;
            inputFrame = Time.frameCount;
            if (!host.PrecisionContextValid || host.TextEntryActive || host.Precision.BlockGameInput || host.ShapeEditor?.Active == true || placer.currentMovingPart != null)
            { box = dragging = false; return; }
            if (box)
            {
                if (k.escapeKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) { box = dragging = false; consumed = Time.frameCount; return; }
                if (mouse.leftButton.wasPressedThisFrame) { dragging = true; boxStart = mouse.position.ReadValue(); }
                if (dragging && mouse.leftButton.wasReleasedThisFrame)
                {
                    SelectBox(boxStart, mouse.position.ReadValue(), k.shiftKey.isPressed, k.ctrlKey.isPressed);
                    box = dragging = false; consumed = Time.frameCount;
                }
                return;
            }
            if (MouseInput.Instance != null && MouseInput.Instance.PointerIsOverUI) return;
            if (k.aKey.wasPressedThisFrame && !k.ctrlKey.isPressed)
            {
                if (k.altKey.isPressed) Clear(); else Set(plane.GetComponentsInChildren<BuildingPart>().Where(Eligible));
                consumed = Time.frameCount; return;
            }
            if (k.bKey.wasPressedThisFrame && !k.ctrlKey.isPressed && !k.altKey.isPressed)
            { box = true; dragging = false; consumed = Time.frameCount; return; }
            if (!mouse.leftButton.wasPressedThisFrame || (!k.shiftKey.isPressed && !IsGroup) || BuildingCamera.cam == null) return;
            var ray = BuildingCamera.cam.ScreenPointToRay(mouse.position.ReadValue());
            if (OverHandle(ray)) return;
            var hit = Pick(ray);
            if (k.shiftKey.isPressed) { if (hit != null) Toggle(hit); }
            else Set(hit == null ? new BuildingPart[0] : new[] { hit });
            consumed = Time.frameCount;
        }
        internal BuildingPart Pick(Ray ray)
        {
            if (!host.HideParts.SelectionRaycast(ray, out var hit, 10000, placer.selectionMask)) return null;
            var part = PartPlacer.GetBuildingPartComponent(hit.collider.gameObject);
            return Eligible(part) ? part : null;
        }
        private bool OverHandle(Ray ray)
        {
            var roots = new Component[] { placer.moveGizmo, placer.rotationGizmo, placer.procduralPartController?.gizmoManager };
            foreach (var root in roots)
                if (root != null && root.gameObject.activeInHierarchy)
                    foreach (var c in root.GetComponentsInChildren<Collider>()) if (c.enabled && c.Raycast(ray, out var hit, 10000)) return true;
            return false;
        }
        internal void SelectBox(Vector2 a, Vector2 b, bool add, bool subtract)
        {
            if (plane == null || BuildingCamera.cam == null) return;
            var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            var next = add || subtract ? Parts.ToList() : new List<BuildingPart>();
            foreach (var part in plane.GetComponentsInChildren<BuildingPart>())
            {
                if (!Eligible(part)) continue;
                var p = BuildingCamera.cam.WorldToScreenPoint(part.transform.position);
                if (p.z <= 0 || !rect.Contains(p)) continue;
                if (subtract) next.Remove(part); else if (!next.Contains(part)) next.Add(part);
            }
            Set(next);
        }
        internal void Refresh()
        {
            Prune();
            if (!host.InEditor || host.ShortcutsOpen) { highlight.Clear(); if (labelRoot != null) labelRoot.SetActive(false); if (boxRoot != null) boxRoot.SetActive(false); return; }
            renderers.Clear();
            if (parts.Count > 1)
                foreach (var p in parts)
                    foreach (var r in p.GetComponentsInChildren<Renderer>())
                        if (r.GetComponentInParent<BuildingPart>() == p && r.enabled) renderers.Add(r);
            highlight.Render(BuildingCamera.cam, renderers, renderers.Count > 0);
            if (IsGroup && !host.Precision.Session.Active)
            {
                if (placer.moveGizmo != null) placer.moveGizmo.transform.position = Pivot;
                if (placer.rotationGizmo != null) placer.rotationGizmo.transform.position = Pivot;
            }
            EnsureUI();
            if (labelRoot == null) return;
            labelRoot.SetActive(IsGroup || box);
            label.text = box ? "Box select: drag | Shift add | Ctrl subtract | Esc cancel" : parts.Count + " selected | Shift-click toggle | G move | R rotate";
            boxRoot.SetActive(box && dragging);
            if (box && dragging)
            {
                var parent = (RectTransform)canvas.transform;
                var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, boxStart, cam, out var a);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Mouse.current.position.ReadValue(), cam, out var b);
                var rect = (RectTransform)boxRoot.transform; rect.anchoredPosition = (a + b) * 0.5f;
                rect.sizeDelta = new Vector2(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
            }
        }
        private void EnsureUI()
        {
            if (labelRoot != null) return;
            foreach (var native in UnityEngine.Object.FindObjectsByType<MirrorButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var c = native.GetComponentInParent<Canvas>(); if (c == null || !c.isActiveAndEnabled || native.text?.font == null) continue;
                canvas = c.rootCanvas;
                labelRoot = new GameObject("EditorTools selection count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                var rect = (RectTransform)labelRoot.transform; rect.SetParent(canvas.transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(24, -120); rect.sizeDelta = new Vector2(850, 48);
                label = labelRoot.GetComponent<TextMeshProUGUI>(); label.font = native.text.font; label.fontSharedMaterial = native.text.font.material;
                label.fontSize = 24; label.color = new Color(1f, 0.8f, 0.3f); label.raycastTarget = false;
                label.fontMaterial.SetFloat("_OutlineWidth", 0.25f); label.fontMaterial.SetColor("_OutlineColor", Color.black); label.UpdateMeshPadding();
                boxRoot = new GameObject("EditorTools selection rectangle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
                boxRoot.transform.SetParent(canvas.transform, false);
                boxRoot.GetComponent<Image>().color = new Color(0.3f, 0.8f, 1, 0.12f); boxRoot.GetComponent<Image>().raycastTarget = false;
                boxRoot.GetComponent<Outline>().effectColor = new Color(0.4f, 0.9f, 1); boxRoot.GetComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
                boxRoot.SetActive(false); return;
            }
        }
        public void Dispose() { Clear(false); highlight.Dispose(); if (labelRoot != null) UnityEngine.Object.Destroy(labelRoot); if (boxRoot != null) UnityEngine.Object.Destroy(boxRoot); }
    }

    [HarmonyPatch]
    internal static class GroupNativeGizmoPatch
    {
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        { yield return AccessTools.Method(typeof(PartPlacer), "UpdateMove"); yield return AccessTools.Method(typeof(PartPlacer), "UpdateRotation"); }
        private static bool Prefix() => Plugin.Instance?.Selection?.IsGroup != true;
    }
}

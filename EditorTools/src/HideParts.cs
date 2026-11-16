using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aviassembly.EditorTools
{
    /// <summary>Editor-only visual hiding. Physics components and saved aircraft state are never changed.</summary>
    public sealed class HideParts : IDisposable
    {
        private sealed class HiddenPart
        {
            internal BuildingPart Part;
            internal readonly Dictionary<Renderer, bool> Renderers = new Dictionary<Renderer, bool>();
        }
        private readonly Dictionary<BuildingPart, HiddenPart> hidden = new Dictionary<BuildingPart, HiddenPart>();
        private readonly List<BuildingPart> dead = new List<BuildingPart>();
        private readonly Func<bool> interactionBlocked;
        private readonly List<KeyValuePair<MethodBase, MethodInfo>> installed = new List<KeyValuePair<MethodBase, MethodInfo>>();
        private Harmony harmony;
        private PartPlacer placer;
        private PlaneContainer aircraft;
        private HidePartsUI ui;
        private float nextUIAttempt;
        private int shortcutFrame = -1;
        private bool disposed;
        private readonly HiddenXray xray = new HiddenXray();
        private readonly List<Renderer> xrayRenderers = new List<Renderer>();
        public bool XrayEnabled { get; private set; } = true;
        internal int XrayDrawCount => xray.DrawCount;
        internal bool WasVisible(Renderer renderer)
        {
            var owner = renderer != null ? renderer.GetComponentInParent<BuildingPart>() : null;
            return owner != null && hidden.TryGetValue(owner, out var entry) && entry.Renderers.TryGetValue(renderer, out var visible) ? visible : renderer != null && renderer.enabled;
        }
        public void ToggleXray() { if (CanInteract) { XrayEnabled = !XrayEnabled; RefreshXray(); RefreshUI(); } }
        internal void RefreshXray()
        {
            xrayRenderers.Clear();
            foreach (var entry in hidden.Values)
                foreach (var pair in entry.Renderers)
                    if (pair.Value && pair.Key != null) xrayRenderers.Add(pair.Key);
            xray.Render(BuildingCamera.cam, xrayRenderers, XrayEnabled && InEditor && !GameManager.Instance.inMenu);
        }
        private static HideParts current;
        private static readonly MethodInfo SetSelected = AccessTools.PropertySetter(typeof(PartPlacer), "currentSelectedPart");

        /// <param name="interactionBlocked">Return true during the host's transform preview, text entry, or other exclusive action.</param>
        public HideParts(Func<bool> interactionBlocked)
        {
            this.interactionBlocked = interactionBlocked ?? (() => false);
        }

        public int HiddenCount { get { Prune(); return hidden.Count; } }
        public bool IsHidden(BuildingPart part) => part != null && hidden.ContainsKey(part);
        public bool InEditor => !disposed && GameManager.gameMode == GameMode.Building && GameManager.Instance != null &&
            !GameManager.Instance.Loading && placer != null && placer.isActiveAndEnabled;
        public bool CanInteract => InEditor && !GameManager.Instance.inMenu && !GameManager.Instance.modalsOpen &&
            !interactionBlocked() && !IsTyping() && placer.currentMovingPart == null && !DraggingHandle();
        public bool CanHide => CanInteract && placer.currentSelectedPart != null && placer.currentSelectedPart.hasBeenPlaced &&
            !IsHidden(placer.currentSelectedPart) && (placer.decalPlacer == null || !placer.decalPlacer.IsPlacingDecal);

        /// <summary>Use a unique Harmony ID; this method only patches this module's specific methods, never PatchAll.</summary>
        public void RegisterPatches(Harmony owner)
        {
            if (disposed) throw new ObjectDisposedException(nameof(HideParts));
            if (harmony != null) return;
            if (current != null && current != this) throw new InvalidOperationException("Only one HideParts module may be registered");
            harmony = owner ?? throw new ArgumentNullException(nameof(owner));
            current = this;
            try
            {
                Patch(typeof(PartPlacer), "UpdateSelection", "FilterRaycasts", true);
                Patch(typeof(PartPainter), "GetHoverPart", "FilterRaycasts", true);
                Patch(typeof(DecalPlacer), "GetHoverDecal", "FilterRaycasts", true);
                Patch(typeof(HighlightRenderer), "HighlightBuildingPart", "SkipHiddenHighlight");
                Patch(typeof(HighlightRenderer), "RenderOutlineObjects", "FilterHighlightGeometry", true);
                Patch(typeof(HighlightRenderer), "RenderHighlightObjects", "FilterHighlightGeometry", true);
                Patch(typeof(GameManager), "StartFlyMode", "RestoreBeforeTransition");
                Patch(typeof(GameManager), "StartMenu", "RestoreBeforeTransition");
                Patch(typeof(PlaneStorage), "ResetPlane", "RestoreBeforeTransition");
                Patch(typeof(PlaneStorage), "Load", "RestoreBeforeTransition");
            }
            catch { UnregisterPatches(); throw; }
        }

        private void Patch(Type type, string method, string hook, bool transpiler = false)
        {
            var original = AccessTools.Method(type, method);
            var replacement = AccessTools.Method(typeof(HideParts), hook);
            if (original == null || replacement == null) throw new MissingMethodException(type.Name, method);
            var patch = new HarmonyMethod(replacement);
            installed.Add(new KeyValuePair<MethodBase, MethodInfo>(original, replacement));
            harmony.Patch(original, transpiler ? null : patch, null, transpiler ? patch : null);
        }

        /// <summary>Call before/after PartPlacer.Update and once from the host Update while the editor may be absent.</summary>
        public void Observe(PartPlacer source, bool pollShortcut = true)
        {
            if (disposed) return;
            var nextAircraft = PlaneContainer.Instance;
            if (placer != source || aircraft != nextAircraft)
            {
                ShowAll();
                placer = source;
                aircraft = nextAircraft;
            }
            if (!InEditor) { ShowAll(); ui?.Refresh(false, false, 0); return; }
            Prune();
            foreach (var entry in hidden.Values)
                foreach (var pair in entry.Renderers)
                    if (pair.Key != null) pair.Key.enabled = false;
            RefreshXray();
            if (pollShortcut && CanInteract && Application.isFocused && Time.frameCount != shortcutFrame)
            {
                if (Input.GetKeyDown(KeyCode.I)) { shortcutFrame = Time.frameCount; HandleShortcut(KeyCode.I); }
                else if (Input.GetKeyDown(KeyCode.V)) { shortcutFrame = Time.frameCount; HandleShortcut(KeyCode.V); }
            }
        }

        /// <summary>Only V and I are handled; useful for a host input router and isolated tests.</summary>
        public bool HandleShortcut(KeyCode key)
        {
            if (!CanInteract) return false;
            if (key == KeyCode.V) return HideSelected();
            if (key == KeyCode.I) { ShowAll(); RefreshUI(); return true; }
            return false;
        }

        public void RefreshUI()
        {
            if (disposed) return;
            bool visible = InEditor && !GameManager.Instance.inMenu && !GameManager.Instance.modalsOpen && !IsTyping() && !interactionBlocked();
            if (visible && (ui == null || !ui.IsAlive) && Time.unscaledTime >= nextUIAttempt)
            {
                nextUIAttempt = Time.unscaledTime + 1;
                ui = HidePartsUI.TryCreate(this);
            }
            ui?.Refresh(visible, CanHide, HiddenCount, CanInteract);
        }

        public bool PointerOverUI(Vector2 position) => ui != null && ui.Contains(position);

        public bool HideSelected()
        {
            if (!CanHide || SetSelected == null) return false;
            var selection = Plugin.Instance?.Selection;
            var targets = selection?.IsGroup == true ? selection.Parts : new[] { placer.currentSelectedPart };
            foreach (var part in targets)
            {
            if (part == null || IsHidden(part)) continue;
            var entry = new HiddenPart { Part = part };
            foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
                if (renderer.GetComponentInParent<BuildingPart>() == part) Capture(entry, renderer);
            // Decals live in a separate container; their explicit owner is authoritative.
            if (part.decals != null)
                foreach (var decal in part.decals)
                    if (decal != null && decal.parentBuildingPart == part)
                        foreach (var renderer in decal.GetComponentsInChildren<Renderer>(true)) Capture(entry, renderer);
            hidden.Add(part, entry);
            foreach (var pair in entry.Renderers) if (pair.Key != null) pair.Key.enabled = false;
            }
            SetSelected.Invoke(placer, new object[] { null });
            if (placer.moveGizmo != null) placer.moveGizmo.gameObject.SetActive(false);
            if (placer.rotationGizmo != null) placer.rotationGizmo.gameObject.SetActive(false);
            var procedural = placer.procduralPartController != null ? placer.procduralPartController.gizmoManager : null;
            if (procedural != null)
            {
                if (procedural.gizmo1 != null) procedural.gizmo1.SetActive(false);
                if (procedural.gizmo2 != null) procedural.gizmo2.SetActive(false);
                procedural.currentGizmoMode = (GizmoMode)0;
            }
            RefreshUI();
            RefreshXray();
            return true;
        }

        private static void Capture(HiddenPart entry, Renderer renderer)
        {
            if (renderer != null && !entry.Renderers.ContainsKey(renderer)) entry.Renderers.Add(renderer, renderer.enabled);
        }

        /// <summary>Unconditional cleanup action; safe even after leaving the editor, during load, or on plugin disable.</summary>
        public void ShowAll()
        {
            foreach (var entry in hidden.Values)
                foreach (var pair in entry.Renderers)
                    if (pair.Key != null) pair.Key.enabled = pair.Value;
            hidden.Clear();
            xray.Clear();
        }

        public void ShowAllFromUI() { if (CanInteract) { ShowAll(); RefreshUI(); } }

        private void Prune()
        {
            dead.Clear();
            foreach (var pair in hidden) if (pair.Key == null) dead.Add(pair.Key);
            foreach (var key in dead)
            {
                foreach (var pair in hidden[key].Renderers) if (pair.Key != null) pair.Key.enabled = pair.Value;
                hidden.Remove(key);
            }
        }

        private bool DraggingHandle()
        {
            var mouse = MouseInput.currentMouse;
            if (mouse == null || !mouse.leftButton.isPressed) return false;
            // Keep a toolbar button interactable throughout pointer-down/up; disabling
            // it mid-press cancels Unity's click. World presses still block hiding.
            return !PointerOverUI(mouse.position.ReadValue());
        }

        private static bool IsTyping()
        {
            if (InputManager.Instance != null && InputManager.Instance.currentInputBlocker != null) return true;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            var input = selected.GetComponentInParent<InputField>();
            var tmp = selected.GetComponentInParent<TMP_InputField>();
            return (input != null && input.isFocused) || (tmp != null && tmp.isFocused);
        }

        public bool IsHiddenCollider(Collider collider)
        {
            if (collider == null) return false;
            return IsHiddenObject(collider.gameObject);
        }

        private bool IsHiddenObject(GameObject gameObject)
        {
            var decal = gameObject.GetComponentInParent<Decal>();
            if (decal != null && IsHidden(decal.parentBuildingPart)) return true;
            return IsHidden(gameObject.GetComponentInParent<BuildingPart>());
        }

        public bool SelectionRaycast(Ray ray, out RaycastHit hit, float maxDistance, int mask)
        {
            if (!InEditor || hidden.Count == 0) return Physics.Raycast(ray, out hit, maxDistance, mask);
            hit = default(RaycastHit);
            float closest = float.PositiveInfinity;
            bool found = false;
            foreach (var candidate in Physics.RaycastAll(ray, maxDistance, mask, QueryTriggerInteraction.UseGlobal))
            {
                if (IsHiddenCollider(candidate.collider) || candidate.distance >= closest) continue;
                hit = candidate;
                closest = candidate.distance;
                found = true;
            }
            return found;
        }

        private static bool RaycastFiltered(Ray ray, out RaycastHit hit, float distance, int mask) => current != null
            ? current.SelectionRaycast(ray, out hit, distance, mask) : Physics.Raycast(ray, out hit, distance, mask);
        private static bool RaycastFilteredDefault(Ray ray, out RaycastHit hit) => RaycastFiltered(ray, out hit, float.PositiveInfinity, Physics.DefaultRaycastLayers);
        private static RaycastHit[] RaycastAllFiltered(Ray ray, float distance, int mask)
        {
            var hits = Physics.RaycastAll(ray, distance, mask);
            if (current == null || !current.InEditor || current.hidden.Count == 0) return hits;
            return Array.FindAll(hits, hit => !current.IsHiddenCollider(hit.collider));
        }

        private static IEnumerable<CodeInstruction> FilterRaycasts(IEnumerable<CodeInstruction> source, MethodBase __originalMethod)
        {
            var ray = AccessTools.Method(typeof(Physics), "Raycast", new[] { typeof(Ray), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int) });
            var basic = AccessTools.Method(typeof(Physics), "Raycast", new[] { typeof(Ray), typeof(RaycastHit).MakeByRefType() });
            var all = AccessTools.Method(typeof(Physics), "RaycastAll", new[] { typeof(Ray), typeof(float), typeof(int) });
            int count = 0;
            foreach (var instruction in source)
            {
                if (instruction.Calls(ray)) { instruction.operand = AccessTools.Method(typeof(HideParts), "RaycastFiltered"); count++; }
                else if (instruction.Calls(basic)) { instruction.operand = AccessTools.Method(typeof(HideParts), "RaycastFilteredDefault"); count++; }
                else if (instruction.Calls(all)) { instruction.operand = AccessTools.Method(typeof(HideParts), "RaycastAllFiltered"); count++; }
                yield return instruction;
            }
            if (count == 0) throw new InvalidOperationException("Unsupported selection raycast layout: " + __originalMethod);
        }

        private static bool SkipHiddenHighlight(BuildingPart __0) => current == null || !current.InEditor || !current.IsHidden(__0);
        private static void RestoreBeforeTransition() { current?.ShowAll(); }

        // Native highlights use CommandBuffer.DrawMesh and ignore Renderer.enabled.
        // Filter both parallel arrays at their actual render sites, including queued
        // objects and hidden nested meshes beneath an otherwise visible ancestor.
        private static Renderer[] HighlightRenderers(GameObject root)
        {
            var components = root.GetComponentsInChildren<Renderer>();
            return current == null || !current.InEditor || current.hidden.Count == 0 ? components :
                Array.FindAll(components, c => !current.IsHiddenObject(c.gameObject));
        }
        private static MeshFilter[] HighlightMeshes(GameObject root)
        {
            var components = root.GetComponentsInChildren<MeshFilter>();
            return current == null || !current.InEditor || current.hidden.Count == 0 ? components :
                Array.FindAll(components, c => !current.IsHiddenObject(c.gameObject));
        }
        private static IEnumerable<CodeInstruction> FilterHighlightGeometry(IEnumerable<CodeInstruction> source, MethodBase __originalMethod)
        {
            int count = 0;
            foreach (var instruction in source)
            {
                var method = instruction.operand as MethodInfo;
                if (method != null && method.DeclaringType == typeof(GameObject) && method.Name == "GetComponentsInChildren" &&
                    method.IsGenericMethod && method.GetParameters().Length == 0)
                {
                    var type = method.GetGenericArguments()[0];
                    string replacement = type == typeof(Renderer) ? "HighlightRenderers" : type == typeof(MeshFilter) ? "HighlightMeshes" : null;
                    if (replacement != null)
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = AccessTools.Method(typeof(HideParts), replacement);
                        count++;
                    }
                }
                yield return instruction;
            }
            if (count != 2) throw new InvalidOperationException("Unsupported highlight renderer layout: " + __originalMethod);
        }

        private void UnregisterPatches()
        {
            if (harmony != null) foreach (var patch in installed) harmony.Unpatch(patch.Key, patch.Value);
            installed.Clear();
            harmony = null;
            if (current == this) current = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            ShowAll();
            xray.Dispose();
            ui?.Dispose();
            ui = null;
            UnregisterPatches();
            disposed = true;
        }
    }
}

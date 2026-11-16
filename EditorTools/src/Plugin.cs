using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aviassembly.EditorTools
{
    [BepInPlugin(Id, "Aviassembly Editor Tools", Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "rylan.aviassembly.editortools";
        public const string Version = "0.6.0";
        internal static Plugin Instance;
        private Harmony harmony;
        private ConfigEntry<KeyCode> focusKey;
        private ConfigEntry<bool> showPanel;
        private ConfigEntry<float> transitionSeconds;
        private PartPlacer placer;
        private BuildingCamera cameraController;
        private BuildingPart selected;
        private BuildingPart focusTarget;
        private bool anchored;
        private bool transitioning;
        private bool pendingFocus;
        private Vector3 defaultOrigin;
        private Vector3 orbitOrigin;
        private Vector3 initialVisualOffset;
        private Vector3 visualOffset;
        private float startDistance;
        private float transitionStart;
        private int lastShortcutFrame = -1;
        private FocusUI focusUI;
        private ShortcutsUI shortcutsUI;
        private int lastHelpFrame = -1;
        internal bool ShortcutsOpen => shortcutsUI?.IsOpen == true;
        private float nextUIAttempt;
        internal PrecisionController Precision { get; private set; }
        internal HideParts HideParts { get; private set; }
        internal PropClearance Clearance { get; private set; }
        private BuildingPart placementTarget;
        private readonly DragProjection placementProjection = new DragProjection();
        internal Camera PlacementInputCamera => placementTarget != null && placer != null && placer.currentMovingPart == placementTarget
            ? placementProjection.Camera ?? BuildingCamera.cam : BuildingCamera.cam;

        // These exact fields were inspected in the installed game's assembly.
        private static readonly FieldInfo Origin = AccessTools.Field(typeof(BuildingCamera), "currentOrigin");
        private static readonly FieldInfo OriginVelocity = AccessTools.Field(typeof(BuildingCamera), "refOrigin");
        private static readonly FieldInfo SavedOrigin = AccessTools.Field(typeof(BuildingCamera), "buildCameraOrigin");
        private static readonly FieldInfo SavedPosition = AccessTools.Field(typeof(BuildingCamera), "buildCameraPosition");
        private static readonly MethodInfo PlaneCenter = AccessTools.Method(typeof(BuildingCamera), "GetPlaneCenter");

        private void Awake()
        {
            focusKey = Config.Bind("Focus Selected", "Shortcut", KeyCode.F, "Toggle Focused and Unfocused even without a selection. Newly placed parts focus after placement; moving does not focus or follow.");
            showPanel = Config.Bind("Focus Selected", "ShowPanel", true, "Show the Focused / Unfocused camera button in the aircraft editor.");
            transitionSeconds = Config.Bind("Focus Selected", "TransitionSeconds", 0.28f,
                new ConfigDescription("Camera transition duration", new AcceptableValueRange<float>(0.05f, 1.5f)));
            if (Origin == null || OriginVelocity == null || SavedOrigin == null || SavedPosition == null || PlaneCenter == null)
            {
                Logger.LogError("Unsupported BuildingCamera layout; Focus Selected has not been enabled.");
                enabled = false;
                return;
            }
            Instance = this;
            Precision = new PrecisionController(this);
            try
            {
                harmony = new Harmony(Id);
                harmony.PatchAll(typeof(Plugin).Assembly);
                HideParts = new HideParts(() => Precision?.BlockGameInput == true || TextEntryActive || ShortcutsOpen);
                HideParts.RegisterPatches(new Harmony(Id + ".hideparts"));
                Clearance = new PropClearance(this);
                Logger.LogInfo("Editor Tools " + Version + " ready. Click arrows/rings and type; G move; R rotate; F focus; V hide; I show all.");
            }
            catch (Exception ex)
            {
                harmony?.UnpatchSelf();
                HideParts?.Dispose();
                Instance = null;
                enabled = false;
                Logger.LogError("Could not install Editor Tools patches: " + ex);
            }
        }

        // Menus disable interaction, but must not discard the established orbit origin.
        private bool HasEditor => GameManager.gameMode == GameMode.Building &&
            GameManager.Instance != null && !GameManager.Instance.Loading &&
            placer != null && placer.isActiveAndEnabled && cameraController != null && cameraController.isActiveAndEnabled;

        internal bool InEditor => HasEditor && !GameManager.Instance.inMenu;
        internal bool TextEntryActive
        {
            get
            {
                var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (go == null) return false;
                var textField = go.GetComponentInParent<TMP_InputField>();
                var legacy = go.GetComponentInParent<InputField>();
                return (textField != null && textField.isFocused) || (legacy != null && legacy.isFocused);
            }
        }
        internal bool PrecisionContextValid => InEditor && !ShortcutsOpen && !GameManager.Instance.modalsOpen &&
            (InputManager.Instance == null || InputManager.Instance.currentInputBlocker == null ||
             (Precision != null && Precision.OwnsBlocker(InputManager.Instance.currentInputBlocker)));
        internal bool CanStartPrecision => PrecisionContextValid && !NativeHandleDragging() && !TextEntryActive;
        internal bool CanStartHandle => PrecisionContextValid && !TextEntryActive;

        private bool BusyUI()
        {
            if (GameManager.Instance == null || GameManager.Instance.modalsOpen) return true;
            if (InputManager.Instance != null && InputManager.Instance.currentInputBlocker != null) return true;
            var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (go == null) return false;
            var input = go.GetComponentInParent<InputField>();
            var tmpInput = go.GetComponentInParent<TMP_InputField>();
            return (input != null && input.isFocused) || (tmpInput != null && tmpInput.isFocused);
        }

        private bool CanUseCamera => InEditor && !BusyUI() && placer.currentMovingPart == null && !NativeHandleDragging() && Precision?.BlockGameInput != true;
        private bool CanToggleFocus => InEditor && !ShortcutsOpen && !GameManager.Instance.modalsOpen && !TextEntryActive &&
            (InputManager.Instance == null || InputManager.Instance.currentInputBlocker == null ||
             (Precision != null && Precision.OwnsBlocker(InputManager.Instance.currentInputBlocker)));
        private bool CanFocus => CanUseCamera && selected != null && selected.hasBeenPlaced;
        internal bool IsFocused => anchored;

        private bool NativeHandleDragging()
        {
            if (placer == null) return false;
            if (MouseInput.currentMouse == null || !MouseInput.currentMouse.leftButton.isPressed) return false;
            var m = placer.moveGizmo;
            var r = placer.rotationGizmo;
            return (m != null && m.gameObject.activeInHierarchy && (m.X.selected || m.Y.selected || m.Z.selected)) ||
                (r != null && r.gameObject.activeInHierarchy && (r.X.selected || r.Y.selected || r.Z.selected));
        }

        internal void Observe(PartPlacer source)
        {
            if (placer != source || cameraController != source.buildingCamera)
            {
                Precision?.Cancel();
                ResetFocus();
                placer = source;
                cameraController = source.buildingCamera;
            }
            if (!HasEditor) { HideParts?.Observe(source); ResetFocus(); return; }
            if (Application.isFocused) ProcessHelpInput();
            MovingPartChanged(source);
            Precision?.Tick(source);
            HideParts?.Observe(source);
            SyncSelection();
            if (CanToggleFocus && Application.isFocused && Time.frameCount != lastShortcutFrame && Input.GetKeyDown(focusKey.Value))
            {
                lastShortcutFrame = Time.frameCount;
                ToggleFocus();
            }
        }

        internal void MovingPartChanged(PartPlacer source)
        {
            if (source != placer || !InEditor || source.currentMovingPart == placementTarget) return;
            placementTarget = source.currentMovingPart;
            if (placementTarget == null) return;
            placementProjection.Capture(BuildingCamera.cam);
            StopFocusTransition();
        }

        internal void StopFocusTransition()
        {
            pendingFocus = false;
            if (!transitioning) return;
            orbitOrigin = cameraController.transform.position + cameraController.transform.forward * cameraController.currentDistance;
            transitioning = false; visualOffset = Vector3.zero;
            if (anchored) SetOrigin(orbitOrigin);
        }

        internal void ProcessHelpInput()
        {
            if (Time.frameCount == lastHelpFrame || Keyboard.current == null) return;
            if (ShortcutsOpen && (Keyboard.current.kKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame))
            { lastHelpFrame = Time.frameCount; shortcutsUI.SetOpen(false); return; }
            if (!Keyboard.current.kKey.wasPressedThisFrame || !CanToggleFocus || Precision?.BlockGameInput == true ||
                placer.currentMovingPart != null || NativeHandleDragging()) return;
            lastHelpFrame = Time.frameCount;
            if (shortcutsUI == null || !shortcutsUI.IsAlive) shortcutsUI = ShortcutsUI.TryCreate(focusKey.Value.ToString());
            shortcutsUI?.SetOpen(true);
        }

        internal void FocusOperation(BuildingPart target)
        {
            if (!InEditor || target == null) return;
            if (!anchored && !transitioning) defaultOrigin = (Vector3)Origin.GetValue(cameraController);
            selected = placer.currentSelectedPart;
            focusTarget = target; anchored = true; pendingFocus = false;
            BeginTransition(target.transform.position);
        }

        // The setter is also called when the player clicks the same part again.
        internal void SelectionAssigned(PartPlacer source)
        {
            if (source != placer) return;
            selected = source.currentSelectedPart;
            pendingFocus = anchored && selected != null && selected.hasBeenPlaced && source.currentMovingPart == null;
        }

        private void SyncSelection()
        {
            if (selected != placer.currentSelectedPart) SelectionAssigned(placer);
            // Native selection temporarily picks a part up, so wait for its placement
            // to settle before moving the camera and changing mouse placement rays.
            if (pendingFocus && CanFocus) { pendingFocus = false; Focus(); }
        }

        private void Update()
        {
            if (Application.isFocused) ProcessHelpInput();
            if (!InEditor || GameManager.Instance.modalsOpen) shortcutsUI?.SetOpen(false);
            if (placer != null) Precision?.Tick(placer);
            HideParts?.Observe(placer);
            if (placer != null && !HasEditor) ResetFocus();
        }

        private void LateUpdate()
        {
            bool visible = showPanel.Value && CanToggleFocus;
            if (visible && (focusUI == null || !focusUI.IsAlive) && Time.unscaledTime >= nextUIAttempt)
            {
                nextUIAttempt = Time.unscaledTime + 1;
                focusUI = FocusUI.TryCreate(ToggleFocus);
            }
            focusUI?.Refresh(visible, IsFocused, CanToggleFocus, focusKey.Value.ToString());
            if (InEditor && (Precision.UI == null || !Precision.UI.IsAlive)) Precision.UI = PrecisionUI.TryCreate(Precision);
            Precision.UI?.Refresh(PrecisionContextValid, selected, Precision.CanStart);
            HideParts?.RefreshUI();
            Clearance?.Tick(placer);
            UpdateFollowingCamera();
        }

        internal void BeforeCameraUpdate(BuildingCamera source)
        {
            if (!HasEditor || source != cameraController) return;
            SyncSelection();
            if (!anchored && !transitioning) return;
            var mouse = MouseInput.currentMouse;
            bool cameraInput = mouse != null && (mouse.rightButton.isPressed || mouse.scroll.ReadValue().sqrMagnitude > 0.01f);
            if (cameraInput)
            {
                // Camera takeover ends the visual easing, not the new native orbit point.
                transitioning = false;
                visualOffset = Vector3.zero;
            }
            if (transitioning)
            {
                float eased = FocusMath.Ease(Time.unscaledTime - transitionStart, transitionSeconds.Value);
                visualOffset = initialVisualOffset * (1 - eased);
                if (eased >= 1) { transitioning = false; visualOffset = Vector3.zero; }
            }
            SetOrigin(orbitOrigin);
        }

        internal void UpdateFollowingCamera()
        {
            if (!InEditor || (!anchored && !transitioning)) return;
            BeforeCameraUpdate(cameraController);
            cameraController.transform.position = orbitOrigin - cameraController.transform.forward * cameraController.currentDistance + visualOffset;
            SavedPosition.SetValue(null, cameraController.transform.position);
        }

        internal void AfterCameraUpdate(BuildingCamera source)
        {
            if (!HasEditor || source != cameraController || (!anchored && !transitioning)) return;
            // Native controls always orbit around orbitOrigin. Only the rendered camera
            // position eases in; an interrupted animation can never strand the orbit pivot.
            if (transitioning) source.transform.position += visualOffset;
            SavedPosition.SetValue(null, source.transform.position);
        }

        private void SetOrigin(Vector3 value)
        {
            Origin.SetValue(cameraController, value);
            OriginVelocity.SetValue(cameraController, Vector3.zero);
            SavedOrigin.SetValue(null, value);
        }

        private void Focus()
        {
            if (!CanFocus) return;
            pendingFocus = false;
            if (!anchored && !transitioning) defaultOrigin = (Vector3)Origin.GetValue(cameraController);
            focusTarget = selected;
            anchored = true;
            BeginTransition(selected.transform.position);
        }

        internal void ToggleFocus()
        {
            if (!CanToggleFocus) return;
            if (anchored) ReturnToDefault();
            else
            {
                var target = placer.currentMovingPart != null ? placer.currentMovingPart : placer.currentSelectedPart;
                if (target != null) FocusOperation(target);
                else
                {
                    defaultOrigin = (Vector3)Origin.GetValue(cameraController);
                    orbitOrigin = defaultOrigin; anchored = true; focusTarget = null; pendingFocus = false;
                    transitioning = false; visualOffset = Vector3.zero;
                }
            }
            focusUI?.Refresh(showPanel.Value && InEditor, IsFocused, CanToggleFocus, focusKey.Value.ToString());
        }

        internal void ReturnToDefault()
        {
            if (!CanToggleFocus) return;
            pendingFocus = false;
            if (anchored) BeginDefault();
        }

        private void BeginDefault()
        {
            var center = (Vector3)PlaneCenter.Invoke(cameraController, null);
            // An empty plane has no bounds; native GetPlaneCenter can return its
            // previously saved focused pivot, or NaN when there are no renderers.
            var plane = PlaneContainer.Instance;
            if (plane == null || plane.transform.childCount == 0 || !Finite(center)) center = defaultOrigin;
            anchored = false;
            focusTarget = null;
            BeginTransition(center);
        }

        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        private void BeginTransition(Vector3 target)
        {
            startDistance = cameraController.currentDistance;
            orbitOrigin = target;
            initialVisualOffset = cameraController.transform.position -
                (orbitOrigin - cameraController.transform.forward * startDistance);
            visualOffset = initialVisualOffset;
            SetOrigin(orbitOrigin);
            transitioning = true;
            transitionStart = Time.unscaledTime;
        }

        internal void ResetFocus()
        {
            placementTarget = null;
            anchored = false;
            transitioning = false;
            visualOffset = Vector3.zero;
            pendingFocus = false;
            selected = null;
            focusTarget = null;
        }

        internal bool OwnsOrigin(BuildingCamera source) => HasEditor && source == cameraController && (anchored || transitioning);

        internal bool BlockPanelClick => ShortcutsOpen || Precision?.BlockGameInput == true || TextEntryActive ||
            ((PointerOverPanel() || PointerOverToolUI() || PointerOverClearanceUI()) &&
                placer != null && placer.currentMovingPart == null && !NativeHandleDragging());

        internal bool PointerOverToolUI() => Mouse.current != null &&
            ((Precision?.UI?.Contains(Mouse.current.position.ReadValue()) ?? false) ||
             (HideParts?.PointerOverUI(Mouse.current.position.ReadValue()) ?? false));
        internal bool PointerOverClearanceUI() => Mouse.current != null && (Clearance?.Contains(Mouse.current.position.ReadValue()) ?? false);

        internal bool PointerOverPanel()
        {
            if (!showPanel.Value || !InEditor || BusyUI()) return false;
            var mouse = MouseInput.currentMouse;
            if (mouse == null) return false;
            return focusUI != null && focusUI.Contains(mouse.position.ReadValue());
        }

        private void OnDisable() { Clearance?.Dispose(); shortcutsUI?.Dispose(); placementProjection.Dispose(); HideParts?.Dispose(); Precision?.Dispose(); ResetFocus(); focusUI?.Dispose(); focusUI = null; }
        private void OnDestroy()
        {
            ResetFocus();
            shortcutsUI?.Dispose();
            Clearance?.Dispose();
            placementProjection.Dispose();
            HideParts?.Dispose();
            Precision?.Dispose();
            focusUI?.Dispose();
            harmony?.UnpatchSelf();
            if (Instance == this) Instance = null;
        }
    }

    [HarmonyPatch(typeof(PartPlacer), "set_currentSelectedPart")]
    internal static class SelectionAssignedPatch
    {
        private static void Prefix(PartPlacer __instance, BuildingPart __0) { Plugin.Instance?.Precision?.ObserveSelection(__instance, __0); }
        private static void Postfix(PartPlacer __instance) { Plugin.Instance?.SelectionAssigned(__instance); }
    }

    [HarmonyPatch(typeof(PartPlacer), "set_currentMovingPart")]
    internal static class FocusMovingPartPatch
    {
        private static void Postfix(PartPlacer __instance) { Plugin.Instance?.MovingPartChanged(__instance); }
    }

    [HarmonyPatch(typeof(PartPlacer), "Update")]
    internal static class ObserveEditorPatch
    {
        private static bool Prefix(PartPlacer __instance)
        {
            Plugin.Instance?.Observe(__instance);
            return Plugin.Instance == null || !Plugin.Instance.BlockPanelClick;
        }
        private static void Postfix(PartPlacer __instance) { Plugin.Instance?.Observe(__instance); }
    }

    [HarmonyPatch(typeof(PartPlacer), "PlaceSelectedPart")]
    internal static class FocusNewPlacementPatch
    {
        internal static void Prefix(PartPlacer __instance, out BuildingPart __state)
        {
            var part = __instance.currentSelectedPart;
            __state = part != null && !part.hasBeenPlaced ? part : null;
        }
        internal static void Postfix(BuildingPart __state)
        {
            if (__state != null && __state.hasBeenPlaced) Plugin.Instance?.FocusOperation(__state);
        }
    }

    [HarmonyPatch(typeof(GizmoRaycaster), "Update")]
    internal static class PanelGizmoHoverPatch
    {
        private static bool Prefix() => Plugin.Instance == null || !Plugin.Instance.BlockPanelClick;
    }

    [HarmonyPatch(typeof(RotationGizmo), "LateUpdate")]
    internal static class PanelGizmoClickPatch
    {
        private static bool Prefix() => Plugin.Instance == null || !Plugin.Instance.BlockPanelClick;
    }

    [HarmonyPatch(typeof(BuildingCamera), "UpdateOrigin")]
    internal static class KeepOrbitPatch
    {
        private static bool Prefix(BuildingCamera __instance) => Plugin.Instance == null || !Plugin.Instance.OwnsOrigin(__instance);
    }

    [HarmonyPatch(typeof(BuildingCamera), "Update")]
    internal static class AnimateFocusPatch
    {
        private static bool Prefix(BuildingCamera __instance)
        {
            if (Plugin.Instance?.ShortcutsOpen == true) return false;
            if (Plugin.Instance?.Precision?.BlockGameInput == true) { Plugin.Instance.UpdateFollowingCamera(); return false; }
            if (Plugin.Instance?.TextEntryActive == true) return false;
            Plugin.Instance?.BeforeCameraUpdate(__instance); return true;
        }
        private static void Postfix(BuildingCamera __instance) { if (Plugin.Instance?.Precision?.BlockGameInput != true && Plugin.Instance?.TextEntryActive != true) Plugin.Instance?.AfterCameraUpdate(__instance); }
    }

    [HarmonyPatch(typeof(MouseInput), "get_PointerIsOverUI")]
    internal static class FocusPanelInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (Plugin.Instance != null && (Plugin.Instance.ShortcutsOpen || Plugin.Instance.PointerOverPanel() || Plugin.Instance.Precision?.BlockGameInput == true ||
                Plugin.Instance.PointerOverToolUI() || Plugin.Instance.PointerOverClearanceUI())) __result = true;
        }
    }

    [HarmonyPatch(typeof(PlaneStorage), "ResetPlane")]
    internal static class ResetAircraftPatch
    {
        private static void Prefix() { Plugin.Instance?.ResetFocus(); }
    }
}

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
        public const string Version = "0.7.12";
        internal static Plugin Instance;
        private Harmony harmony;
        private ConfigEntry<KeyCode> focusKey;
        private ConfigEntry<bool> showPanel;
        private ConfigEntry<SnapReference> snapReference;
        private SnapReferenceMenu snapMenu;
        private int snapMenuClosedFrame = -1;
        internal SnapReference Reference => snapReference.Value;
        private ConfigEntry<float> transitionSeconds;
        private PartPlacer placer;
        private BuildingCamera cameraController;
        private BuildingPart selected;
        private bool focused;
        private bool orbitInitialized;
        private bool transitioning;
        private bool pendingFocus;
        private BuildingPart resizedPart;
        private bool? focusBeforeDeferredToggle;
        private Vector3 defaultOrigin;
        private Vector3 orbitOrigin;
        private Vector3 initialVisualOffset;
        private Vector3 visualOffset;
        private float transitionStart;
        private int lastShortcutFrame = -1;
        private FocusUI focusUI;
        private ShortcutsUI shortcutsUI;
        private int lastHelpFrame = -1;
        private int lastPartShortcutFrame = -1;
        internal PartClipboard Clipboard { get; } = new PartClipboard();
        internal bool EditorMenuOpen => ShortcutsOpen || snapMenu != null || snapMenuClosedFrame == Time.frameCount;
        internal bool ShortcutsOpen => shortcutsUI?.IsOpen == true;
        private float nextUIAttempt;
        internal PrecisionController Precision { get; private set; }
        internal HideParts HideParts { get; private set; }
        internal PropClearance Clearance { get; private set; }
        internal MultiSelection Selection { get; private set; }
        internal ShapePrecision ShapeEditor { get; private set; }
        private BuildingPart placementTarget;
        private bool movingPlacedPart;
        private Vector3 pickupPosition;
        private readonly DragProjection placementProjection = new DragProjection();
        internal Camera PlacementInputCamera => placementTarget != null && placer != null && placer.currentMovingPart == placementTarget
            ? placementProjection.Camera ?? BuildingCamera.cam : BuildingCamera.cam;

        // These exact fields were inspected in the installed game's assembly.
        private static readonly FieldInfo Origin = AccessTools.Field(typeof(BuildingCamera), "currentOrigin");
        private static readonly FieldInfo OriginVelocity = AccessTools.Field(typeof(BuildingCamera), "refOrigin");
        private static readonly FieldInfo SavedOrigin = AccessTools.Field(typeof(BuildingCamera), "buildCameraOrigin");
        private static readonly FieldInfo SavedPosition = AccessTools.Field(typeof(BuildingCamera), "buildCameraPosition");
        private static readonly MethodInfo PlaneCenter = AccessTools.Method(typeof(BuildingCamera), "GetPlaneCenter");
        private static readonly MethodInfo SetCameraRotated = AccessTools.PropertySetter(typeof(BuildingCamera), "Rotated");
        private static readonly FieldInfo PlacementDepth = AccessTools.Field(typeof(PartPlacer), "currentDistance");
        private static readonly FieldInfo PlacementOffset = AccessTools.Field(typeof(PartPlacer), "positionOffset");

        private void Awake()
        {
            snapReference = Config.Bind("Precision Edit", "SnapReference", SnapReference.Edge, "Moving part geometry aligned to the hovered point.");
            focusKey = Config.Bind("Focus Selected", "Shortcut", KeyCode.F, "Toggle camera focus. Focused mode centers clicked parts and new placements after release. The view stays fixed while editing.");
            showPanel = Config.Bind("Focus Selected", "ShowPanel", true, "Show the Focused / Unfocused camera button in the aircraft editor.");
            transitionSeconds = Config.Bind("Focus Selected", "TransitionSeconds", 0.28f,
                new ConfigDescription("Camera transition duration", new AcceptableValueRange<float>(0.05f, 1.5f)));
        }

        private void OnEnable()
        {
            if (Origin == null || OriginVelocity == null || SavedOrigin == null || SavedPosition == null || PlaneCenter == null ||
                SetCameraRotated == null || PlacementDepth == null || PlacementOffset == null)
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
                HideParts = new HideParts(() => Precision?.BlockGameInput == true || ShapeEditor?.BlockInput == true || Selection?.BlockInput == true || TextEntryActive || EditorMenuOpen);
                HideParts.RegisterPatches(new Harmony(Id + ".hideparts"));
                Clearance = new PropClearance(this);
                Selection = new MultiSelection(this);
                ShapeEditor = new ShapePrecision(this);
                Logger.LogInfo("Editor Tools " + Version + " ready. Click arrows/rings and type; G move; R rotate; F focus; V hide; I show all.");
            }
            catch (Exception ex)
            {
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
        internal bool PrecisionContextValid => InEditor && !EditorMenuOpen && !GameManager.Instance.modalsOpen &&
            (InputManager.Instance == null || InputManager.Instance.currentInputBlocker == null ||
             (Precision != null && Precision.OwnsBlocker(InputManager.Instance.currentInputBlocker)) ||
             (ShapeEditor != null && ShapeEditor.OwnsBlocker(InputManager.Instance.currentInputBlocker)));
        internal bool CanStartPrecision => PrecisionContextValid && !NativeHandleDragging() && !TextEntryActive && ShapeEditor?.BlockInput != true && Selection?.BlockInput != true;
        internal bool CanStartHandle => PrecisionContextValid && !TextEntryActive && ShapeEditor?.BlockInput != true && Selection?.BlockInput != true;

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

        private bool CameraHeld => placer != null && (placer.currentMovingPart != null || NativeHandleDragging() ||
            Precision?.BlockGameInput == true || ShapeEditor?.BlockInput == true);
        private bool CanUseCamera => InEditor && !BusyUI() && !CameraHeld;
        private bool CanToggleFocus => InEditor && !EditorMenuOpen && !GameManager.Instance.modalsOpen && !TextEntryActive &&
            (InputManager.Instance == null || InputManager.Instance.currentInputBlocker == null ||
             (Precision != null && Precision.OwnsBlocker(InputManager.Instance.currentInputBlocker)) ||
             (ShapeEditor != null && ShapeEditor.OwnsBlocker(InputManager.Instance.currentInputBlocker)));
        private bool CanFocus => CanUseCamera && selected != null && selected.hasBeenPlaced;
        private bool CanUseIdleShortcuts => CanToggleFocus && !CameraHeld && Selection?.BlockInput != true;
        internal bool IsFocused => focused;

        private bool NativeHandleDragging()
        {
            if (placer == null) return false;
            if (MouseInput.currentMouse == null || !MouseInput.currentMouse.leftButton.isPressed) return false;
            var m = placer.moveGizmo;
            var r = placer.rotationGizmo;
            return (m != null && m.gameObject.activeInHierarchy && (m.X?.selected == true || m.Y?.selected == true || m.Z?.selected == true)) ||
                (r != null && r.gameObject.activeInHierarchy && (r.X?.selected == true || r.Y?.selected == true || r.Z?.selected == true));
        }

        internal void Observe(PartPlacer source)
        {
            if (placer != source || cameraController != source.buildingCamera)
            {
                ShapeEditor?.Cancel();
                Precision?.Cancel();
                ReleaseCamera();
                placer = source;
                cameraController = source.buildingCamera;
            }
            if (!HasEditor) { ShapeEditor?.Cancel(); Selection?.Clear(false); HideParts?.Observe(source); ReleaseCamera(); return; }
            InitializeOrbit();
            if (Application.isFocused) ProcessHelpInput();
            MovingPartChanged(source);
            Selection?.Observe(source);
            ShapeEditor?.Tick(source);
            Precision?.Tick(source);
            if (Application.isFocused) ProcessPartShortcuts();
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
            movingPlacedPart = placementTarget != null && placementTarget.hasBeenPlaced;
            if (placementTarget == null) return;
            pickupPosition = placementTarget.transform.position;
            HoldCameraPosition();
            var camera = BuildingCamera.cam;
            placementProjection.Capture(camera);
            // The catalogue can select a part before the native idle update resets
            // the last drag's depth and grab offset.
            if (!movingPlacedPart && camera != null)
            {
                PlacementDepth.SetValue(source, Mathf.Max(camera.nearClipPlane, Mathf.Abs(cameraController.currentDistance)));
                PlacementOffset.SetValue(source, Vector3.zero);
            }
        }

        internal bool MovingPlacedPart => movingPlacedPart && placementTarget != null && placer.currentMovingPart == placementTarget;

        internal void HoldCameraPosition()
        {
            resizedPart = null;
            pendingFocus = false;
            if (cameraController == null) return;
            orbitOrigin = cameraController.transform.position + cameraController.transform.forward * Mathf.Abs(cameraController.currentDistance);
            transitioning = false;
            visualOffset = Vector3.zero;
            SetOrigin(orbitOrigin);
        }

        internal void ProcessHelpInput()
        {
            if (snapMenu != null)
            {
                if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) CloseSnapMenu();
                return;
            }
            if (Time.frameCount == lastHelpFrame || Keyboard.current == null) return;
            if (ShortcutsOpen && (Keyboard.current.kKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame))
            { lastHelpFrame = Time.frameCount; shortcutsUI.SetOpen(false); return; }
            if (!Keyboard.current.kKey.wasPressedThisFrame || !CanUseIdleShortcuts) return;
            lastHelpFrame = Time.frameCount;
            OpenShortcuts();
        }

        private void EnsureShortcutsUI()
        {
            if (shortcutsUI == null || !shortcutsUI.IsAlive) shortcutsUI = ShortcutsUI.TryCreate(focusKey.Value.ToString(), OpenShortcuts);
        }

        private void OpenShortcuts()
        {
            if (!CanUseIdleShortcuts) return;
            EnsureShortcutsUI();
            shortcutsUI?.SetOpen(true);
        }

        internal bool OpenSnapMenu(SnappingButton magnet)
        {
            if (!CanUseIdleShortcuts || magnet == null) return false;
            snapMenu = SnapReferenceMenu.Create(magnet, this);
            return snapMenu != null;
        }

        internal void SelectSnapReference(SnappingButton magnet, SnapReference? choice)
        {
            if (choice.HasValue) snapReference.Value = choice.Value;
            // Let the game's own Press method update its state, color, and disabled icon.
            if (PartPlacer.snapMode != choice.HasValue) magnet.Press();
            CloseSnapMenu();
        }

        internal void CloseSnapMenu()
        {
            snapMenu?.Dispose();
            snapMenu = null;
            snapMenuClosedFrame = Time.frameCount;
        }

        internal void ProcessPartShortcuts()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || keyboard.altKey.isPressed || keyboard.shiftKey.isPressed ||
                Time.frameCount == lastPartShortcutFrame || !CanUseIdleShortcuts) return;
            bool ctrl = keyboard.ctrlKey.isPressed;
            bool copy = ctrl && keyboard.cKey.wasPressedThisFrame;
            bool paste = ctrl && keyboard.vKey.wasPressedThisFrame;
            bool duplicate = ctrl && keyboard.dKey.wasPressedThisFrame;
            if (!copy && !paste && !duplicate) return;
            lastPartShortcutFrame = Time.frameCount;
            try
            {
                if (copy) Clipboard.Copy(placer.currentSelectedPart);
                else if (paste) Clipboard.Paste(placer);
                else Clipboard.Duplicate(placer, Precision);
            }
            catch (Exception ex)
            {
                Clipboard.Show(copy ? "Could not copy part" : paste ? "Could not paste part" : "Could not duplicate part");
                Logger.LogWarning(ex);
            }
        }

        internal BuildingPart PlacementFocusCandidate(PartPlacer source)
        {
            var part = source.currentMovingPart;
            if (source != placer || part == null) return null;
            if (!part.hasBeenPlaced) return part;
            // Native selection briefly picks up a part even for a simple click.
            // A release that moved it belongs to the drag and keeps the view fixed.
            return MovingPlacedPart && source.movementSinceClick <= 5f && (part.transform.position - pickupPosition).sqrMagnitude < 0.000001f
                ? part : null;
        }

        internal void PlacementCompleted(PartPlacer source, BuildingPart part)
        {
            if (!InEditor || source != placer || source.currentMovingPart != null) return;
            if (ApplyDeferredFocus(part) || !focused || part == null || !part.hasBeenPlaced) return;
            pendingFocus = false;
            BeginTransition(part.transform.position);
        }

        // The setter is also called when the player clicks the same part again.
        internal void SelectionAssigned(PartPlacer source)
        {
            if (source != placer) return;
            resizedPart = null;
            selected = source.currentSelectedPart;
            Selection?.SelectionAssigned(selected);
            ShapeEditor?.ObserveSelection(selected);
            // Native clicks can temporarily pick a part up. Center only after that
            // transaction settles, and only while the player has Focused enabled.
            pendingFocus = Selection?.AssigningSelection != true && focused && selected != null && selected.hasBeenPlaced && source.currentMovingPart == null;
        }

        private void SyncSelection()
        {
            if (selected != placer.currentSelectedPart) SelectionAssigned(placer);
            if (ApplyDeferredFocus(resizedPart)) { resizedPart = null; return; }
            if (focused && resizedPart != null && CanUseCamera)
            {
                if (resizedPart.hasBeenPlaced)
                {
                    var pivot = Selection?.IsGroup == true && Array.IndexOf(Selection.Parts, resizedPart) >= 0 ?
                        Selection.Pivot : resizedPart.transform.position;
                    BeginTransition(pivot);
                }
                resizedPart = null;
                pendingFocus = false;
                return;
            }
            // Native selection temporarily picks a part up, so wait for its placement
            // to settle before moving the camera and changing mouse placement rays.
            if (focused && pendingFocus && CanFocus)
            {
                pendingFocus = false;
                BeginTransition(selected.transform.position);
            }
        }

        internal void ResizeCompleted(BuildingPart part) { resizedPart = focused ? part : null; }

        private void OnGUI() { Precision?.DrawSnapMarker(); }

        private void Update()
        {
            if (snapMenu != null && (!InEditor || !snapMenu.IsAlive || GameManager.Instance.modalsOpen || !Application.isFocused)) CloseSnapMenu();
            if (Application.isFocused) ProcessHelpInput();
            if (!InEditor || GameManager.Instance.modalsOpen) shortcutsUI?.SetOpen(false);
            if (placer != null) { ShapeEditor?.Tick(placer); Precision?.Tick(placer); }
            HideParts?.Observe(placer);
            if (placer != null && !HasEditor) ReleaseCamera();
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
            if (InEditor) EnsureShortcutsUI();
            shortcutsUI?.RefreshButton(InEditor, CanUseIdleShortcuts);
            if (InEditor && (Precision.UI == null || !Precision.UI.IsAlive)) Precision.UI = PrecisionUI.TryCreate(Precision);
            Precision.UI?.Refresh(PrecisionContextValid, selected, Precision.CanStart, Clipboard.Message);
            HideParts?.RefreshUI();
            Clearance?.Tick(placer);
            Selection?.Refresh();
            UpdateFollowingCamera();
        }

        internal void BeforeCameraUpdate(BuildingCamera source)
        {
            if (!HasEditor || source != cameraController) return;
            InitializeOrbit();
            SyncSelection();
            if (!focused && !transitioning) return;
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
            if (!InEditor || CameraHeld || EditorMenuOpen || Selection?.BlockInput == true || TextEntryActive) return;
            BeforeCameraUpdate(cameraController);
            if (!focused && !transitioning) return;
            cameraController.transform.position = orbitOrigin - cameraController.transform.forward * Mathf.Abs(cameraController.currentDistance) + visualOffset;
            SavedPosition.SetValue(null, cameraController.transform.position);
        }

        internal void AfterCameraUpdate(BuildingCamera source)
        {
            if (!HasEditor || source != cameraController || (!focused && !transitioning)) return;
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

        internal void ToggleFocus()
        {
            if (!CanToggleFocus) return;
            InitializeOrbit();
            bool wasFocused = focused;
            focused = !focused;
            resizedPart = null;
            pendingFocus = false;
            if (CameraHeld)
            {
                if (!focusBeforeDeferredToggle.HasValue) focusBeforeDeferredToggle = wasFocused;
                if (focusBeforeDeferredToggle == focused) focusBeforeDeferredToggle = null;
            }
            else
            {
                focusBeforeDeferredToggle = null;
                Reframe();
            }
            focusUI?.Refresh(showPanel.Value && InEditor, IsFocused, CanToggleFocus, focusKey.Value.ToString());
        }

        private bool ApplyDeferredFocus(BuildingPart placed = null)
        {
            if (!focusBeforeDeferredToggle.HasValue || !CanUseCamera) return false;
            focusBeforeDeferredToggle = null;
            pendingFocus = false;
            Reframe(placed);
            return true;
        }

        private void Reframe(BuildingPart placed = null)
        {
            if (!focused) BeginDefault();
            else if (Selection?.IsGroup == true) BeginTransition(Selection.Pivot);
            else if (placed != null && placed.hasBeenPlaced) BeginTransition(placed.transform.position);
            else if (placer.currentSelectedPart != null) BeginTransition(placer.currentSelectedPart.transform.position);
            else HoldCameraPosition();
        }

        private void InitializeOrbit()
        {
            if (orbitInitialized) return;
            defaultOrigin = orbitOrigin = (Vector3)Origin.GetValue(cameraController);
            orbitInitialized = true;
        }

        private void BeginDefault()
        {
            var center = (Vector3)PlaneCenter.Invoke(cameraController, null);
            // An empty plane has no bounds; native GetPlaneCenter can return its
            // previously saved focused pivot, or NaN when there are no renderers.
            var plane = PlaneContainer.Instance;
            if (plane == null || plane.transform.childCount == 0 || !Finite(center)) center = defaultOrigin;
            BeginTransition(center);
        }

        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        private void BeginTransition(Vector3 target)
        {
            orbitOrigin = target;
            initialVisualOffset = cameraController.transform.position -
                (orbitOrigin - cameraController.transform.forward * Mathf.Abs(cameraController.currentDistance));
            visualOffset = initialVisualOffset;
            SetOrigin(orbitOrigin);
            transitioning = true;
            transitionStart = Time.unscaledTime;
        }

        private void ReleaseCamera()
        {
            resizedPart = null;
            placementTarget = null;
            movingPlacedPart = false;
            orbitInitialized = false;
            transitioning = false;
            visualOffset = Vector3.zero;
            pendingFocus = false;
            focusBeforeDeferredToggle = null;
            selected = null;
        }

        internal void AircraftReset()
        {
            ShapeEditor?.Cancel();
            Precision?.Cancel();
            if (HasEditor) HoldCameraPosition();
            placementTarget = null;
            movingPlacedPart = false;
            pendingFocus = false;
            focusBeforeDeferredToggle = null;
            selected = null;
        }

        // Native per-frame recentering tracks changing aircraft bounds, including
        // a new part as soon as it is dropped. Keep the chosen orbit in both modes.
        internal bool OwnsOrigin(BuildingCamera source, bool instant) => HasEditor && source == cameraController &&
            (!instant || focused || transitioning || CameraHeld);

        internal bool HoldCameraUpdate(BuildingCamera source)
        {
            if (!HasEditor || source != cameraController || !CameraHeld) return false;
            // Keep the native right click cancel gesture available while placement
            // freezes the stock camera update that normally resets this flag.
            if (placer.currentMovingPart != null && MouseInput.currentMouse?.rightButton.wasPressedThisFrame == true)
                SetCameraRotated.Invoke(source, new object[] { false });
            return true;
        }

        internal bool BlockPanelClick => EditorMenuOpen || ShapeEditor?.BlockInput == true || Selection?.BlockInput == true || Precision?.BlockGameInput == true || TextEntryActive ||
            ((PointerOverPanel() || PointerOverToolUI() || PointerOverClearanceUI()) &&
                placer != null && placer.currentMovingPart == null && !NativeHandleDragging());

        internal bool PointerOverToolUI() => Mouse.current != null &&
            ((Precision?.UI?.Contains(Mouse.current.position.ReadValue()) ?? false) ||
             (shortcutsUI?.ContainsButton(Mouse.current.position.ReadValue()) ?? false) ||
             (HideParts?.PointerOverUI(Mouse.current.position.ReadValue()) ?? false));
        internal bool PointerOverClearanceUI() => Mouse.current != null && (Clearance?.Contains(Mouse.current.position.ReadValue()) ?? false);

        internal bool PointerOverPanel()
        {
            if (!showPanel.Value || !CanToggleFocus) return false;
            var mouse = MouseInput.currentMouse;
            if (mouse == null) return false;
            return focusUI != null && focusUI.Contains(mouse.position.ReadValue());
        }

        private void OnDisable()
        {
            if (Instance != this) return;
            try
            {
                if (snapMenu != null) CloseSnapMenu();
                ShapeEditor?.Dispose(); Precision?.Dispose(); Selection?.Dispose();
                HideParts?.Dispose(); Clearance?.Dispose(); placementProjection.Dispose();
                shortcutsUI?.Dispose(); focusUI?.Dispose();
            }
            finally
            {
                harmony?.UnpatchSelf();
                Instance = null;
                harmony = null; shortcutsUI = null; focusUI = null;
                ShapeEditor = null; Precision = null; Selection = null; HideParts = null; Clearance = null;
                placer = null; cameraController = null; nextUIAttempt = 0;
                ReleaseCamera();
            }
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
    internal static class FocusCompletedPlacementPatch
    {
        internal static void Prefix(PartPlacer __instance, out BuildingPart __state)
        { __state = Plugin.Instance?.PlacementFocusCandidate(__instance); }
        internal static void Postfix(PartPlacer __instance, BuildingPart __state)
        { Plugin.Instance?.PlacementCompleted(__instance, __state); }
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
        private static bool Prefix(BuildingCamera __instance, bool __0) => Plugin.Instance?.OwnsOrigin(__instance, __0) != true;
    }

    [HarmonyPatch(typeof(BuildingCamera), "Update")]
    internal static class AnimateFocusPatch
    {
        private static bool Prefix(BuildingCamera __instance)
        {
            var plugin = Plugin.Instance;
            if (plugin == null) return true;
            if (plugin.HoldCameraUpdate(__instance) || plugin.EditorMenuOpen || plugin.Selection?.BlockInput == true || plugin.TextEntryActive) return false;
            plugin.BeforeCameraUpdate(__instance);
            return true;
        }
        private static void Postfix(BuildingCamera __instance, bool __runOriginal)
        { if (__runOriginal) Plugin.Instance?.AfterCameraUpdate(__instance); }
    }

    [HarmonyPatch(typeof(MouseInput), "get_PointerIsOverUI")]
    internal static class FocusPanelInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (Plugin.Instance != null && (Plugin.Instance.EditorMenuOpen || Plugin.Instance.ShapeEditor?.BlockInput == true || Plugin.Instance.Selection?.BlockInput == true || Plugin.Instance.PointerOverPanel() || Plugin.Instance.Precision?.BlockGameInput == true ||
                Plugin.Instance.PointerOverToolUI() || Plugin.Instance.PointerOverClearanceUI())) __result = true;
        }
    }

    [HarmonyPatch(typeof(PlaneStorage), "ResetPlane")]
    internal static class ResetAircraftPatch
    {
        private static void Prefix() { Plugin.Instance?.AircraftReset(); }
    }
}

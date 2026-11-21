using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using Aviassembly.EditorTools;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using TMPro;

public sealed class ClipboardFixturePart : PlanePart
{
    public int Setting;
    public override PartStat[] GetPartStats() => new PartStat[0];
    public override void UpdatePart(PlaneContainer container) { }
    public override void Save(GameDataWriter writer) => writer.Write(Setting);
    public override void Load(GameDataReader reader) => Setting = reader.ReadInt();
}

[BepInPlugin("rylan.aviassembly.focusruntimetests", "Focus Runtime Tests", "1.0.0")]
[BepInDependency(Plugin.Id)]
public sealed class UnityFocusTests : BaseUnityPlugin
{
    private int passes;
    private int failures;
    private readonly System.Collections.Generic.List<string> results = new System.Collections.Generic.List<string>();
    private static object Call(object target, string name, params object[] args) => AccessTools.Method(target.GetType(), name).Invoke(target, args);
    private static void Put(object target, string name, object value) => AccessTools.Field(target.GetType(), name).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)AccessTools.Field(target.GetType(), name).GetValue(target);
    private static void Singleton<T>(T component) where T : MonoBehaviour =>
        AccessTools.Field(typeof(Singleton<>).MakeGenericType(typeof(T)), "m_Instance").SetValue(null, component);
    private void Check(bool result, string label)
    {
        results.Add((result ? "PASS " : "FAIL ") + label);
        if (result) passes++; else failures++;
    }
    private void Near(Vector3 a, Vector3 b, string label) => Check((a - b).sqrMagnitude < 0.00001f, label + " actual=" + a.ToString("F4") + " expected=" + b.ToString("F4"));

    private IEnumerator Start()
    {
        // Only execute in the disposable test host prepared by PrepareUnityTests.ps1.
        if (!File.Exists(Path.Combine(Paths.GameRootPath, "FOCUS_TEST_HOST_ONLY"))) yield break;
        yield return null;
        // Load only the editor scene assets into the isolated, callback-suppressed host.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += DisableSceneCameras;
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Building", UnityEngine.SceneManagement.LoadSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= DisableSceneCameras;
        BuildingCamera.intialized = false;
        AccessTools.Field(typeof(Singleton<BuildingCamera>), "m_Instance").SetValue(null, null);
        try { Run(); }
        catch (Exception ex) { failures++; results.Add("ERROR " + ex); }
        string report = string.Join(Environment.NewLine, results.ToArray()) + Environment.NewLine + "RESULT " + passes + " passed, " + failures + " failed";
        File.WriteAllText(Path.Combine(Paths.GameRootPath, "focus-test-results.txt"), report);
        Logger.LogInfo(report);
        Application.Quit(failures == 0 ? 0 : 1);
    }

    private static void DisableSceneCameras(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var camera in root.GetComponentsInChildren<BuildingCamera>(true)) camera.enabled = false;
    }

    private void Run()
    {
        var plugin = UnityEngine.Object.FindFirstObjectByType<Plugin>();
        Check(plugin != null, "Release plugin loaded in Unity");
        if (plugin == null) return;
        var manager = new GameObject("Fixture manager").AddComponent<GameManager>();
        Singleton(manager);
        manager.inMenu = false;
        manager.modalsOpen = false;
        GameManager.gameMode = GameMode.Building;
        var input = new GameObject("Fixture input").AddComponent<InputManager>();
        Singleton(input);
        var mouseInput = new GameObject("Fixture mouse").AddComponent<MouseInput>();
        Singleton(mouseInput);
        MouseInput.currentMouse = InputSystem.AddDevice<Mouse>();
        var plane = new GameObject("Fixture aircraft").AddComponent<PlaneContainer>();
        Singleton(plane);
        var cameraGo = new GameObject("Fixture camera");
        var camera = cameraGo.AddComponent<Camera>();
        camera.fieldOfView = 60;
        camera.aspect = 16f / 9;
        cameraGo.transform.rotation = Quaternion.Euler(18, 37, 0);
        var controller = cameraGo.AddComponent<BuildingCamera>();
        Put(controller, "planeContainer", plane);
        Put(controller, "currentOrigin", Vector3.zero);
        controller.currentDistance = 15;
        cameraGo.transform.position = -cameraGo.transform.forward * controller.currentDistance;
        var placer = new GameObject("Fixture placer").AddComponent<PartPlacer>();
        placer.buildingCamera = controller;
        var partGo = new GameObject("Selected offset mesh");
        partGo.transform.SetParent(plane.transform);
        partGo.transform.position = new Vector3(7, 2, -4);
        var part = partGo.AddComponent<BuildingPart>();
        part.hasBeenPlaced = true;
        part.partName = "Offset mesh part";
        var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh.transform.SetParent(partGo.transform, false);
        mesh.transform.localPosition = new Vector3(3, 0, 0);
        mesh.transform.localScale = new Vector3(2, 1, 1);
        Put(placer, "<currentSelectedPart>k__BackingField", part);
        var beforePlane = plane.transform.position;
        var beforePart = part.transform.position;
        var beforeRotation = camera.transform.rotation;
        Call(plugin, "Observe", placer);
        Check(!plugin.IsFocused, "Starts Unfocused; selecting a part does not change mode");
        Near(Get<Vector3>(controller, "currentOrigin"), Vector3.zero, "Unfocused selection leaves native camera alone");
        Call(plugin, "ToggleFocus");
        Near(Get<Vector3>(controller, "currentOrigin"), part.transform.position, "Focus toggle assigns the real part pivot as native orbit origin");
        Check(Mathf.Abs(controller.currentDistance - 15) < 0.0001f, "First focus preserves zoom distance");
        Check(Quaternion.Angle(beforeRotation, camera.transform.rotation) < 0.001f, "First focus preserves camera angle");
        Near(plane.transform.position, beforePlane, "Focus does not move aircraft");
        Near(part.transform.position, beforePart, "Focus does not move selected part");
        controller.UpdateOrigin(false);
        Near(Get<Vector3>(controller, "currentOrigin"), part.transform.position, "Native automatic recenter cannot overwrite focused origin");

        Put(plugin, "transitionStart", Time.unscaledTime - 0.14f);
        controller.Update();
        Near(Get<Vector3>(controller, "currentOrigin"), part.transform.position, "Visual easing never interpolates the native orbit origin");
        Near(camera.transform.position, -camera.transform.forward * 15 + part.transform.position * 0.5f,
            "First focus visually eases halfway while preserving angle and distance");

        // Invoke the real game's camera Update with an explicit orbit angle.
        Put(controller, "pitch", 12f);
        Put(controller, "yaw", 91f);
        InputSystem.QueueStateEvent(MouseInput.currentMouse, new MouseState().WithButton(MouseButton.Right));
        InputSystem.Update();
        controller.Update();
        Near(camera.transform.position + camera.transform.forward * controller.currentDistance,
            part.transform.position, "Native orbit uses the selected pivot after animation interruption");
        InputSystem.QueueStateEvent(MouseInput.currentMouse, new MouseState());
        InputSystem.Update();

        // Second toggle restores the aircraft center as the orbit point.
        var defaultCenter = (Vector3)Call(controller, "GetPlaneCenter");
        float retainedDistance = controller.currentDistance;
        Call(plugin, "ToggleFocus");
        Check(!plugin.IsFocused, "Second toggle changes state to Unfocused immediately");
        Near(Get<Vector3>(controller, "currentOrigin"), defaultCenter, "Unfocused uses the actual native aircraft center");
        Put(plugin, "transitionStart", Time.unscaledTime - 2);
        controller.Update();
        Check(plugin.OwnsOrigin(controller, false), "Unfocused prevents per-frame native recentering after transition");
        Near(camera.transform.position + camera.transform.forward * controller.currentDistance, defaultCenter, "Unfocused returns to the default orbit center");
        Check(Mathf.Abs(controller.currentDistance - retainedDistance) < 0.0001f, "Reset retains zoom instead of applying old close framing");
        Call(plugin, "Observe", placer);
        Check(!plugin.IsFocused, "Current selection cannot immediately reactivate focus after toggling off");
        var other = new GameObject("Other selected part").AddComponent<BuildingPart>();
        other.transform.SetParent(plane.transform);
        other.transform.position = new Vector3(-6, 5, 2);
        other.hasBeenPlaced = true;
        Vector3 viewBefore = camera.transform.position;
        Call(placer, "set_currentSelectedPart", other);
        Call(plugin, "Observe", placer);
        controller.Update();
        Near(camera.transform.position, viewBefore, "Selecting another part while Unfocused leaves camera in place");
        Check(!plugin.IsFocused, "Selection preserves Unfocused mode");
        Call(plugin, "ToggleFocus");
        Near(Get<Vector3>(controller, "currentOrigin"), other.transform.position, "Toggle after changing selection uses the new part pivot");
        Call(placer, "set_currentSelectedPart", part);
        Call(plugin, "Observe", placer);
        Near(Get<Vector3>(controller, "currentOrigin"), part.transform.position, "Focused click centers the selected placed part");
        Check(plugin.IsFocused, "Selection preserves Focused mode");
        Check(Mathf.Abs(controller.currentDistance - retainedDistance) < 0.0001f, "Changing focused selection retains zoom");

        Put(placer, "<currentMovingPart>k__BackingField", other);
        Call(placer, "set_currentSelectedPart", other);
        Call(plugin, "Observe", placer);
        var pickupOrigin = Get<Vector3>(controller, "currentOrigin");
        other.transform.position += Vector3.up;
        plugin.UpdateFollowingCamera();
        Near(Get<Vector3>(controller, "currentOrigin"), pickupOrigin, "Picking up and moving a part does not retarget focused camera");
        Call(plugin, "ToggleFocus");
        Check(!plugin.IsFocused, "F can switch to Unfocused during placement");
        Put(placer, "<currentMovingPart>k__BackingField", null);
        Call(plugin, "Observe", placer);
        Check(!plugin.IsFocused, "Finishing placement respects manual Unfocused override");
        Call(plugin, "ToggleFocus");
        Near(Get<Vector3>(controller, "currentOrigin"), other.transform.position, "F can restore focus after placement");
        manager.inMenu = true;
        Call(plugin, "Observe", placer);
        Check(plugin.OwnsOrigin(controller, false), "Opening a menu retains focused orbit origin");
        Call(plugin, "ToggleFocus");
        Check(plugin.IsFocused, "Menus block mode toggling");
        manager.inMenu = false;
        Call(plugin, "Observe", placer);
        Near(Get<Vector3>(controller, "currentOrigin"), other.transform.position, "Closing a menu retains focused orbit origin");
        manager.modalsOpen = true;
        Call(plugin, "ToggleFocus");
        Check(plugin.IsFocused, "Modal blocks mode toggling");
        manager.modalsOpen = false;

        // Build native UI from the real loaded visual assets when available.
        Call(plugin, "LateUpdate");
        var ui = Get<object>(plugin, "focusUI");
        Check(ui != null, "Native camera button is created from a loaded game MirrorButton template");
        if (ui != null)
        {
            var label = Get<TextMeshProUGUI>(ui, "label");
            var button = Get<Button>(ui, "button");
            var root = Get<GameObject>(ui, "root");
            Canvas.ForceUpdateCanvases();
            var row = root.transform.parent as RectTransform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            Check(row.GetComponent<HorizontalLayoutGroup>() != null, "Mode button joins the native toolbar layout");
            var ourCorners = new Vector3[4];
            ((RectTransform)root.transform).GetWorldCorners(ourCorners);
            bool overlaps = false;
            foreach (Transform sibling in row)
            {
                if (sibling == root.transform || !sibling.gameObject.activeInHierarchy) continue;
                var siblingRect = sibling as RectTransform;
                if (siblingRect == null) continue;
                var corners = new Vector3[4];
                siblingRect.GetWorldCorners(corners);
                overlaps |= corners[0].x < ourCorners[2].x - 0.1f && corners[2].x > ourCorners[0].x + 0.1f;
            }
            Check(!overlaps, "Native layout keeps mode button clear of the existing toolbar buttons");
            var layout = new System.Collections.Generic.List<string>();
            var nativeCanvas = root.GetComponentInParent<Canvas>();
            layout.Add("Canvas " + nativeCanvas.name + " " + ((RectTransform)nativeCanvas.transform).rect);
            foreach (var native in nativeCanvas.GetComponentsInChildren<MirrorButton>(true))
            {
                layout.Add("MIRROR image size=" + native.buttonImage.rectTransform.rect + " colors=" + native.selectedColor + "/" + native.deselectedColor);
                foreach (var component in native.transform.parent.GetComponents<Component>()) layout.Add("PARENT COMPONENT " + component.GetType().Name);
                foreach (Transform sibling in native.transform.parent)
                {
                    var r = sibling as RectTransform;
                    if (r == null) continue;
                    var points = new Vector3[4];
                    r.GetWorldCorners(points);
                    layout.Add("SIBLING " + r.name + " " + points[0] + " " + points[2]);
                }
            }
            foreach (var text in nativeCanvas.GetComponentsInChildren<TMP_Text>(false))
            {
                if (string.IsNullOrEmpty(text.text)) continue;
                var corners = new Vector3[4];
                text.rectTransform.GetWorldCorners(corners);
                layout.Add(text.name + " | " + text.text.Replace("\n", " ") + " | " + corners[0] + " " + corners[2] + " | font=" + text.font.name + " size=" + text.fontSize);
            }
            File.WriteAllLines(Path.Combine(Paths.GameRootPath, "ui-layout.txt"), layout.ToArray());
            Check(label.text == "Focused  [F]", "UI displays Focused and shortcut");
            Check(label.font != null && Get<Image>(ui, "background").sprite != null, "UI uses native font and background sprite");
            Check(root.GetComponents<MonoBehaviour>().Length == 3, "UI contains no copied gameplay controllers");
            button.onClick.Invoke();
            Check(label.text == "Unfocused  [F]" && !plugin.IsFocused, "UI button resets camera and immediately displays Unfocused");
            Call(plugin, "Observe", placer);
            Check(!plugin.IsFocused, "UI reset stays Unfocused despite existing selection");
            button.onClick.Invoke();
            Check(label.text == "Focused  [F]" && plugin.IsFocused, "UI button toggles back to Focused");
            manager.inMenu = true;
            Call(plugin, "LateUpdate");
            Check(!root.activeSelf, "Mode button hides in menus");
            manager.inMenu = false;
            Call(plugin, "LateUpdate");
            Check(root.activeSelf, "Mode button returns after closing menu");
        }

        UnityEngine.Object.DestroyImmediate(other.gameObject);
        Call(plugin, "Observe", placer);
        Put(plugin, "transitionStart", Time.unscaledTime - 2);
        controller.Update();
        Check(plugin.IsFocused, "Deleting a target retains Focused mode for the next selection");
        Call(plugin, "ToggleFocus");
        Check(!plugin.IsFocused, "Toggle works even after the focused part is deleted");
        Call(plugin, "ToggleFocus");
        Check(plugin.IsFocused, "F enters Focused mode without a selected target");
        Call(plugin, "ToggleFocus");
        Call(placer, "set_currentSelectedPart", part);
        Call(plugin, "Observe", placer);
        Check(!plugin.IsFocused, "New selection after deletion preserves Unfocused mode");
        Call(plugin, "ToggleFocus");
        Call(plugin, "ToggleFocus");
        Call(placer, "set_currentSelectedPart", part);
        Call(plugin, "Observe", placer);
        Check(!plugin.IsFocused, "Clicking the same part while Unfocused stays Unfocused");
        Put(plugin, "transitionStart", Time.unscaledTime - 2);
        controller.Update();
        part.transform.position += Vector3.right * 4;
        var oldNativeOrigin = Get<Vector3>(controller, "currentOrigin");
        controller.UpdateOrigin(false);
        Near(Get<Vector3>(controller, "currentOrigin"), oldNativeOrigin, "Unfocused orbit stays put when aircraft bounds change");
        controller.UpdateOrigin(true);
        var nextNativeOrigin = Get<Vector3>(controller, "currentOrigin");
        var changedCenter = (Vector3)Call(controller, "GetPlaneCenter");
        Check((nextNativeOrigin - changedCenter).sqrMagnitude < (oldNativeOrigin - changedCenter).sqrMagnitude,
            "Unfocused accepts the game's explicit reframe after loading an aircraft");
        Call(plugin, "ToggleFocus");
        GameManager.gameMode = GameMode.Flying;
        Call(plugin, "Observe", placer);
        Check(!plugin.OwnsOrigin(controller, false) && plugin.IsFocused, "Entering flight releases the editor camera and remembers the mode");
        GameManager.gameMode = GameMode.Building;
        Put(placer, "<currentSelectedPart>k__BackingField", part);
        Call(plugin, "Observe", placer);
        Check(plugin.IsFocused, "Reentering the editor remembers Focused mode");
        plugin.AircraftReset();
        Check(plugin.IsFocused && plugin.OwnsOrigin(controller, false), "Aircraft reset preserves Focused mode and keeps its orbit");
        plugin.ToggleFocus();
        Put(plugin, "transitionStart", Time.unscaledTime - 2);
        controller.Update();
        var pending = new GameObject("Unplaced focus fixture").AddComponent<BuildingPart>();
        pending.transform.SetParent(plane.transform); pending.transform.position = new Vector3(-2, 1, 3);
        pending.hasBeenPlaced = false;
        Call(placer, "set_currentMovingPart", pending);
        Call(placer, "set_currentSelectedPart", pending);
        Check(!plugin.IsFocused, "Placement: moving a new part does not enter Focused mode");
        var placementOrigin = Get<Vector3>(controller, "currentOrigin");
        Put(placer, "currentDistance", 6f);
        InputSystem.QueueStateEvent(MouseInput.currentMouse, new MouseState { position = new Vector2(500, 450) }); InputSystem.Update();
        var placementPoint = (Vector3)Call(placer, "GetMouseWorldPosition");
        var renderBeforeFollow = camera.transform.position;
        pending.transform.position += Vector3.right * 2;
        Put(plugin, "transitionStart", Time.unscaledTime - 2); plugin.UpdateFollowingCamera();
        Near(Get<Vector3>(controller, "currentOrigin"), placementOrigin, "Placement: moving a new part leaves orbit origin unchanged");
        Near(camera.transform.position, renderBeforeFollow, "Placement: moving a new part leaves rendered camera unchanged");
        Near((Vector3)Call(placer, "GetMouseWorldPosition"), placementPoint,
            "Placement: real native mouse projection stays stable while dragging");
        controller.UpdateOrigin(true);
        Near(Get<Vector3>(controller, "currentOrigin"), placementOrigin, "Placement: native recenter cannot follow a dragged part while Unfocused");
        Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(PartPlacer), "PlaceSelectedPart")).Postfixes.Any(p => p.PatchMethod.DeclaringType == typeof(FocusCompletedPlacementPatch)),
            "Placement: conditional completed-placement focus hook is installed");
        Check(typeof(Plugin).Assembly.GetType("Aviassembly.EditorTools.NamedSelections") == null,
            "Release: cancelled named selections feature is excluded");
        plugin.ToggleFocus(); plugin.ToggleFocus(); Call(plugin, "Observe", placer);
        Check(!plugin.IsFocused, "Placement: F override is not reactivated by continued dragging");
        var beforeDropOrigin = Get<Vector3>(controller, "currentOrigin");
        FocusCompletedPlacementPatch.Prefix(placer, out var unfocusedDrop);
        Call(placer, "set_currentMovingPart", (object)null); pending.hasBeenPlaced = true;
        Call(placer, "set_currentSelectedPart", (object)null); Call(plugin, "Observe", placer);
        FocusCompletedPlacementPatch.Postfix(placer, unfocusedDrop);
        for (int frame = 0; frame < 12; frame++) { controller.UpdateOrigin(false); controller.Update(); }
        Check(!plugin.IsFocused && plugin.PlacementInputCamera == camera,
            "Placement: dropping a new part stays Unfocused and releases frozen projection");
        Near(Get<Vector3>(controller, "currentOrigin"), beforeDropOrigin, "Placement: dropping a new part leaves the Unfocused orbit fixed through native updates");
        Call(placer, "set_currentSelectedPart", pending); plugin.ToggleFocus();
        // A new part dragged in while Focused must stay still until native placement completes.
        pending.hasBeenPlaced = false;
        Call(placer, "set_currentMovingPart", pending); Call(placer, "set_currentSelectedPart", pending);
        var focusedDragOrigin = Get<Vector3>(controller, "currentOrigin");
        pending.transform.position += Vector3.right * 7;
        plugin.UpdateFollowingCamera();
        Near(Get<Vector3>(controller, "currentOrigin"), focusedDragOrigin, "Focused new-part drag does not follow the moving part");
        FocusCompletedPlacementPatch.Prefix(placer, out var focusedDrop);
        Call(placer, "set_currentMovingPart", (object)null); pending.hasBeenPlaced = true;
        Call(placer, "set_currentSelectedPart", (object)null);
        FocusCompletedPlacementPatch.Postfix(placer, focusedDrop);
        Near(Get<Vector3>(controller, "currentOrigin"), pending.transform.position, "Focused new-part drop centers its final placement even after selection clears");
        Put(plugin, "transitionStart", Time.unscaledTime - 2); plugin.UpdateFollowingCamera();
        Call(placer, "set_currentMovingPart", pending); Call(placer, "set_currentSelectedPart", pending);
        Check(plugin.IsFocused, "Placement: dragging preserves manually selected Focused mode");
        var existingOrbit = Get<Vector3>(controller, "currentOrigin");
        var existingView = camera.transform.position;
        pending.transform.position += Vector3.up;
        Put(placer, "<movementSinceClick>k__BackingField", 90f);
        FocusCompletedPlacementPatch.Prefix(placer, out var existingDrop);
        Call(placer, "set_currentMovingPart", (object)null); Call(placer, "set_currentSelectedPart", (object)null);
        FocusCompletedPlacementPatch.Postfix(placer, existingDrop);
        Put(plugin, "transitionStart", Time.unscaledTime - 2); plugin.UpdateFollowingCamera();
        Near(Get<Vector3>(controller, "currentOrigin"), existingOrbit, "Placement: moving an existing part preserves the focused pivot on release");
        Near(camera.transform.position, existingView, "Placement: moving an existing part preserves the focused view on release");
        Put(placer, "<movementSinceClick>k__BackingField", 0f);
        Call(placer, "set_currentMovingPart", pending); Call(placer, "set_currentSelectedPart", pending);
        FocusCompletedPlacementPatch.Prefix(placer, out var clickedPart);
        Call(placer, "set_currentMovingPart", (object)null);
        FocusCompletedPlacementPatch.Postfix(placer, clickedPart);
        Near(Get<Vector3>(controller, "currentOrigin"), pending.transform.position, "Placement: a simple click still centers the part in Focused mode");
        Call(placer, "set_currentMovingPart", pending); Call(placer, "set_currentSelectedPart", pending);
        Put(placer, "<movementSinceClick>k__BackingField", 30f);
        FocusCompletedPlacementPatch.Prefix(placer, out var overriddenDrop);
        var overrideView = camera.transform.position;
        plugin.ToggleFocus();
        controller.Update(); plugin.UpdateFollowingCamera();
        Near(camera.transform.position, overrideView, "Placement: an explicit focus toggle keeps the projection steady until release");
        Call(placer, "set_currentMovingPart", (object)null); Call(placer, "set_currentSelectedPart", (object)null);
        pending.transform.position += Vector3.right;
        FocusCompletedPlacementPatch.Postfix(placer, overriddenDrop);
        Check(!plugin.IsFocused, "Placement: F Unfocused override wins even after a Focused placement begins");
        Near(Get<Vector3>(controller, "currentOrigin"), (Vector3)Call(controller, "GetPlaneCenter"),
            "Placement: the explicit Unfocused reframe begins after the drag");
        Put(placer, "<movementSinceClick>k__BackingField", 0f);
        plugin.ToggleFocus();
        plugin.ToggleFocus(); plugin.ToggleFocus(); Call(plugin, "LateUpdate");
        Check(plugin.IsFocused && Get<Button>(ui, "button").interactable,
            "Placement: F and UI can cycle modes with no selected part or active tool");
        plugin.ToggleFocus();
        UnityEngine.Object.DestroyImmediate(pending.gameObject);
        Call(placer, "set_currentSelectedPart", part); Call(plugin, "Observe", placer);
        InputSystem.QueueStateEvent(MouseInput.currentMouse, new MouseState()); InputSystem.Update();
        UnityPrecisionTests.Run(plugin, placer, plane, part, Check);
        UnityHandleTests.Run(plugin, placer, part, Check);
        TestShortcuts(plugin, placer);
        TestPartShortcuts(plugin, placer, plane);
        UnityInspectionTests.Run(plugin, placer, plane, Check);
        UnityHidePartsIntegrationTests.Run(plugin, placer, plane, part, Check);
        UnityGroupShapeTests.Run(plugin, placer, plane, Check);
        TestNativePlacement(plugin, placer, controller, plane, camera);
        TestNativeDrag(plugin, placer, controller, plane, camera);
        plugin.enabled = false;
        Check(Plugin.Instance == null && !Harmony.HasAnyPatches(Plugin.Id) && !Harmony.HasAnyPatches(Plugin.Id + ".hideparts"),
            "Audit: disabling the plugin detaches all editor hooks");
        plugin.enabled = true;
        plugin.Observe(placer);
        Check(Plugin.Instance == plugin && Harmony.HasAnyPatches(Plugin.Id) && Harmony.HasAnyPatches(Plugin.Id + ".hideparts") && plugin.HideParts.InEditor,
            "Audit: reenabling restores editor tools and hidden-part picking");
    }

    private void TestNativePlacement(Plugin plugin, PartPlacer placer, BuildingCamera controller, PlaneContainer plane, Camera camera)
    {
        var selected = placer.currentSelectedPart;
        var oldDecalPlacer = placer.decalPlacer;
        var decals = new GameObject("Placement decal fixture").AddComponent<DecalPlacer>();
        placer.decalPlacer = decals;
        try
        {
            foreach (bool focused in new[] { false, true })
            {
                if (plugin.IsFocused != focused) plugin.ToggleFocus();
                Put(plugin, "transitionStart", Time.unscaledTime - 2);
                controller.Update();
                var origin = Get<Vector3>(controller, "currentOrigin");
                var view = camera.transform.position;
                var newPart = GameObject.CreatePrimitive(PrimitiveType.Cube).AddComponent<BuildingPart>();
                newPart.transform.SetParent(plane.transform);
                newPart.transform.position = new Vector3(-47, 8, 11);
                newPart.isBasePart = true;
                newPart.snapPoints = new Transform[0];
                Call(placer, "set_currentSelectedPart", newPart);
                Call(placer, "set_currentMovingPart", newPart);
                try
                {
                    placer.PlaceSelectedPart();
                    for (int frame = 0; frame < 12; frame++) { controller.UpdateOrigin(false); controller.Update(); }
                    Check(newPart.hasBeenPlaced && placer.currentMovingPart == null && plugin.IsFocused == focused,
                        "Native placement completes and preserves " + (focused ? "Focused" : "Unfocused") + " mode");
                    Near(Get<Vector3>(controller, "currentOrigin"), focused ? newPart.transform.position : origin,
                        "Native placement uses the " + (focused ? "placed part" : "existing camera") + " pivot");
                    if (!focused) Near(camera.transform.position, view, "Unfocused native placement leaves the rendered view in place");
                }
                finally
                {
                    placer.partContainer.RemovePart(newPart);
                    UnityEngine.Object.DestroyImmediate(newPart.gameObject);
                }
            }
        }
        finally
        {
            placer.decalPlacer = oldDecalPlacer;
            UnityEngine.Object.DestroyImmediate(decals.gameObject);
            Call(placer, "set_currentSelectedPart", selected);
        }
    }

    private void TestNativeDrag(Plugin plugin, PartPlacer placer, BuildingCamera controller, PlaneContainer plane, Camera camera)
    {
        var selected = placer.currentSelectedPart;
        var oldDecals = placer.decalPlacer;
        var oldMask = placer.selectionMask;
        var oldSnap = PartPlacer.snapMode;
        var oldSnapDistance = placer.snapDistance;
        var decals = new GameObject("Drag decal fixture").AddComponent<DecalPlacer>();
        placer.decalPlacer = decals;
        placer.selectionMask = 0;
        placer.snapDistance = 0.4f;
        PartPlacer.snapMode = true;
        void Pointer(Vector2 position, Vector2 scroll)
        {
            InputSystem.QueueStateEvent(MouseInput.currentMouse, new MouseState { position = position, scroll = scroll });
            InputSystem.Update();
        }
        try
        {
            foreach (bool focused in new[] { false, true })
            {
                if (plugin.IsFocused != focused) plugin.ToggleFocus();
                Put(plugin, "transitionStart", Time.unscaledTime - 0.12f);
                controller.Update();
                var view = camera.transform.position;
                var angle = camera.transform.rotation;
                float zoom = controller.currentDistance;
                const float depth = 9;
                var grab = camera.ViewportToWorldPoint(new Vector3(0.35f, 0.4f, depth));
                var offset = camera.transform.right * -0.16f;
                Vector2 newMouse = (Vector2)camera.WorldToScreenPoint(grab) + new Vector2(98, 46);
                var cursorPoint = camera.ScreenToWorldPoint(new Vector3(newMouse.x, newMouse.y, depth));
                var anchor = new GameObject("Snap endpoint on distant mouse ray").AddComponent<BuildingPart>();
                anchor.transform.SetParent(plane.transform);
                anchor.transform.position = camera.ScreenPointToRay(newMouse).GetPoint(45);
                anchor.snapPoints = new[] { anchor.transform };
                anchor.isBasePart = anchor.hasBeenPlaced = true;
                placer.partContainer.AddPart(anchor);
                var dragged = new GameObject("Placed drag fixture").AddComponent<BuildingPart>();
                dragged.transform.SetParent(plane.transform);
                dragged.transform.position = grab + offset;
                dragged.snapPoints = new[] { dragged.transform };
                dragged.hasBeenPlaced = true;
                try
                {
                    Call(placer, "set_currentMovingPart", dragged);
                    Call(placer, "set_currentSelectedPart", dragged);
                    Put(placer, "currentDistance", depth);
                    Put(placer, "positionOffset", offset);
                    Put(placer, "currentRotation", Quaternion.identity);
                    Pointer(newMouse, Vector2.up * 120);
                    controller.Update();
                    plugin.UpdateFollowingCamera();
                    var move = AccessTools.Method(typeof(PartPlacer), "MovePart", new[] { typeof(BuildingPart) });
                    var attachment = (GameObject)move.Invoke(placer, new object[] { dragged });
                    Near(dragged.transform.position, cursorPoint + offset, "Native drag: the grab point follows the mouse with fixed depth");
                    Check(attachment == null && plugin.IsFocused == focused, "Native drag: ignores a distant endpoint along the mouse ray");
                    anchor.transform.position = cursorPoint + offset + camera.transform.up * 0.12f;
                    attachment = (GameObject)move.Invoke(placer, new object[] { dragged });
                    Check(attachment == anchor.gameObject, "Native drag: nearby endpoints still snap");
                    var mirror = new GameObject("Mirror drag preview").AddComponent<BuildingPart>();
                    mirror.transform.SetParent(plane.transform);
                    mirror.transform.position = cursorPoint + offset;
                    mirror.snapPoints = new[] { mirror.transform };
                    mirror.isBasePart = mirror.hasBeenPlaced = true;
                    var child = new GameObject("Moving mirror child").AddComponent<BuildingPart>();
                    child.transform.position = mirror.transform.position;
                    child.snapPoints = new[] { child.transform };
                    child.isBasePart = true;
                    child.SetParent(mirror.gameObject);
                    placer.partContainer.AddPart(mirror);
                    placer.partContainer.AddPart(child);
                    Call(placer, "set_currentMirrorPart", mirror);
                    try
                    {
                        attachment = (GameObject)move.Invoke(placer, new object[] { dragged });
                        Check(attachment == anchor.gameObject, "Mirror drag: closer points on the moving mirror and its child cannot steal the fixed attachment");
                        child.SetPosition(child.transform.position + camera.transform.up * 0.5f);
                        var mirrorMove = AccessTools.Method(typeof(PartPlacer), "MoveMirrorPart");
                        var oldAnchor = anchor.transform.position;
                        anchor.transform.position = placer.symmetryPlane.GetMirroredPosition(dragged.transform.position);
                        var parent = (GameObject)mirrorMove.Invoke(placer, new object[] { mirror, dragged });
                        Check(parent == anchor.gameObject, "Mirror drag: the reflected copy accepts a fixed attachment while excluding itself");
                        var pointerRay = camera.ScreenPointToRay(newMouse);
                        var origin = placer.symmetryPlane.GetMirroredPosition(pointerRay.origin);
                        var direction = (placer.symmetryPlane.GetMirroredPosition(pointerRay.GetPoint(1)) - origin).normalized;
                        child.gameObject.layer = anchor.gameObject.layer = 28;
                        var childCollider = child.gameObject.AddComponent<BoxCollider>();
                        var anchorCollider = anchor.gameObject.AddComponent<BoxCollider>();
                        child.transform.position = origin + direction * 4;
                        anchor.transform.position = origin + direction * 8;
                        placer.selectionMask = 1 << 28; PartPlacer.snapMode = false; Physics.SyncTransforms();
                        parent = (GameObject)mirrorMove.Invoke(placer, new object[] { mirror, dragged });
                        Check(parent == anchor.gameObject, "Mirror drag: the reflected cursor ray reaches a fixed surface behind a moving child");
                        anchorCollider.enabled = false;
                        parent = (GameObject)mirrorMove.Invoke(placer, new object[] { mirror, dragged });
                        Check(parent == null, "Mirror drag: a moving child alone cannot become the reflected part's parent");
                        childCollider.enabled = false; placer.selectionMask = 0; PartPlacer.snapMode = true;
                        anchor.transform.position = oldAnchor;
                        var oldMirrorMode = PartPlacer.mirrorMode;
                        var oldPosition = dragged.transform.position;
                        try
                        {
                            PartPlacer.mirrorMode = true;
                            var center = (oldPosition + placer.symmetryPlane.GetMirroredPosition(oldPosition)) * 0.5f;
                            var normal = (oldPosition - center).normalized;
                            dragged.transform.position = center + normal * 0.015f;
                            mirror.name = dragged.name; mirror.transform.position = center - normal * 0.1f;
                            Check(placer.partContainer.GetMirrorPart(dragged, placer) == mirror && placer.GetMirrorPart(dragged) == null,
                                "Mirror drag: picking up a centerline part cannot claim a nearby placed part as its reflection");
                        }
                        finally { PartPlacer.mirrorMode = oldMirrorMode; dragged.transform.position = oldPosition; }
                    }
                    finally
                    {
                        Call(placer, "set_currentMirrorPart", (object)null);
                        child.SetParent(null); placer.partContainer.RemovePart(child); placer.partContainer.RemovePart(mirror);
                        UnityEngine.Object.DestroyImmediate(child.gameObject); UnityEngine.Object.DestroyImmediate(mirror.gameObject);
                    }
                    Near(camera.transform.position, view, "Native drag: rendered view stays put in " + (focused ? "Focused" : "Unfocused") + " mode");
                    Check(Quaternion.Angle(angle, camera.transform.rotation) < 0.001f && controller.currentDistance == zoom,
                        "Native drag: an active edit preserves camera angle and zoom");
                    Pointer(newMouse, Vector2.zero);
                    Put(placer, "<movementSinceClick>k__BackingField", 80f);
                    placer.PlaceSelectedPart();
                    controller.Update(); plugin.UpdateFollowingCamera();
                    Near(camera.transform.position, view, "Native drag: dropping an existing part keeps the rendered view");
                }
                finally
                {
                    Call(placer, "set_currentMovingPart", (object)null);
                    Call(placer, "set_currentSelectedPart", selected);
                    placer.partContainer.RemovePart(dragged);
                    placer.partContainer.RemovePart(anchor);
                    UnityEngine.Object.DestroyImmediate(dragged.gameObject);
                    UnityEngine.Object.DestroyImmediate(anchor.gameObject);
                    Put(placer, "<movementSinceClick>k__BackingField", 0f);
                }
            }
        }
        finally
        {
            Pointer(Vector2.zero, Vector2.zero);
            placer.selectionMask = oldMask;
            PartPlacer.snapMode = oldSnap;
            placer.snapDistance = oldSnapDistance;
            placer.decalPlacer = oldDecals;
            UnityEngine.Object.DestroyImmediate(decals.gameObject);
        }
    }

    private void TestPartShortcuts(Plugin plugin, PartPlacer placer, PlaneContainer plane)
    {
        var precision = plugin.Precision;
        var keyboard = Keyboard.current;
        var storage = PlaneStorage.Instance;
        var money = MoneyManager.Instance;
        var oldMoney = money.money;
        var oldMode = GameManager.Instance.gameModeData;
        var oldSelected = placer.currentSelectedPart;
        var oldDecals = placer.decalPlacer;
        var oldFeedback = placer.priceFeedbackManager;
        var oldProcedural = placer.procduralPartController;
        var oldMoveGizmo = placer.moveGizmo;
        var oldRotateGizmo = placer.rotationGizmo;
        var oldClipboard = Get<object>(plugin.Clipboard, "copied");
        var store = AccessTools.Field(typeof(PartPrefabs), "instance");
        var oldStore = store.GetValue(null);
        var fixtures = new GameObject("Part shortcut fixture");
        BuildingPart source = null, committed = null;
        var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
        template.name = "Shortcut test part";
        template.transform.SetParent(fixtures.transform, false);
        template.transform.position = Vector3.one * 1000;
        var templatePart = template.AddComponent<BuildingPart>();
        templatePart.price = 37;
        templatePart.snapPoints = Array.Empty<Transform>();
        template.AddComponent<ClipboardFixturePart>();

        void Input(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
            Put(plugin, "lastPartShortcutFrame", -1);
            plugin.ProcessPartShortcuts();
        }
        void Idle()
        {
            Put(precision, "consumedFrame", -1);
            Call(precision, "ReleaseBlocker");
            Input();
        }

        try
        {
            var prefabs = fixtures.AddComponent<PartPrefabs>();
            Put(prefabs, "allPartPrefabs", new System.Collections.Generic.List<GameObject> { template });
            store.SetValue(null, prefabs);
            placer.decalPlacer = fixtures.AddComponent<DecalPlacer>();
            placer.priceFeedbackManager = fixtures.AddComponent<PriceFeedbackManager>();
            placer.procduralPartController = fixtures.AddComponent<ProceduralPartManager>();
            placer.moveGizmo = UnityEngine.Object.Instantiate(Resources.FindObjectsOfTypeAll<MoveGizmo>().First(g => g.X != null), fixtures.transform);
            placer.rotationGizmo = UnityEngine.Object.Instantiate(Resources.FindObjectsOfTypeAll<RotationGizmo>().First(g => g.X != null), fixtures.transform);
            placer.moveGizmo.gameObject.SetActive(false); placer.rotationGizmo.gameObject.SetActive(false);
            GameManager.Instance.gameModeData = new GameModeData();
            money.money = 500;
            source = UnityEngine.Object.Instantiate(template, plane.transform, false).GetComponent<BuildingPart>();
            source.transform.SetPositionAndRotation(new Vector3(108, 3, -2), Quaternion.Euler(11, 35, 5));
            source.rotationOffset = Quaternion.Euler(4, 10, 2);
            source.baseRotation = source.transform.rotation * Quaternion.Inverse(source.rotationOffset);
            source.hasBeenPlaced = source.isBasePart = true;
            source.GetComponent<ClipboardFixturePart>().Setting = 27;
            placer.partContainer.AddPart(source);
            plugin.Selection.Set(new[] { source }, highlightSelection: false);
            Idle();
            Input(Key.LeftCtrl, Key.C);
            var copied = Get<object>(plugin.Clipboard, "copied");
            Check(copied != null && plugin.Clipboard.Message.StartsWith("Copied"), "Clipboard: Ctrl+C captures the active part");
            bool mirrorBefore = PartPlacer.mirrorMode;
            PartPlacer.mirrorMode = false;
            Input(Key.LeftCtrl, Key.V);
            var pasted = placer.currentMovingPart;
            Check(pasted != null && pasted != source && pasted.GetComponent<ClipboardFixturePart>().Setting == 27,
                "Audit: paste creates a separate part with the copied settings");
            if (pasted != null)
            {
                pasted.transform.position = placer.symmetryPlane.GetMirroredPosition(source.transform.position);
                PartPlacer.mirrorMode = true;
                Check(placer.partContainer.GetMirrorPart(pasted, placer) == source && placer.GetMirrorPart(pasted) == null,
                    "Audit: cancelling an unplaced paste cannot pick an existing part as its mirror");
                pasted.hasBeenPlaced = true;
                Check(!plugin.Clipboard.KeepPastedShape(placer, pasted), "Audit: a previously pasted part returns to native editing after placement");
                pasted.hasBeenPlaced = false;
                placer.DestroySelectedPart();
                Check(Get<System.Collections.Generic.List<BuildingPart>>(placer.partContainer, "parts").Contains(source) && source.gameObject.activeInHierarchy,
                    "Audit: native paste cancellation preserves the existing opposite part");
                if (pasted != null) UnityEngine.Object.DestroyImmediate(pasted.gameObject);
            }
            PartPlacer.mirrorMode = mirrorBefore;
            plugin.Selection.Set(new[] { source }, highlightSelection: false); Idle();
            source.GetComponent<ClipboardFixturePart>().Setting = 83;
            var before = storage.SaveToStream().ToArray();
            Call(storage, "ExecuteHistoryUpdate");
            var history = Get<System.Collections.IList>(storage, "history");
            int historyCount = history.Count;
            int partCount = plane.transform.childCount;
            var sourcePosition = source.transform.position;
            var sourceRotation = source.transform.rotation;
            bool focused = plugin.IsFocused;
            Input(Key.LeftCtrl, Key.D);
            if (!precision.Session.Duplicating)
            {
                Check(false, "Duplicate: Ctrl+D opens the advanced move session with a live copy (" + plugin.Clipboard.Message + ")");
                return;
            }
            var preview = precision.Session.Target;
            Check(precision.Modal && !precision.Rotating && preview != source && !preview.hasBeenPlaced,
                "Duplicate: Ctrl+D opens advanced move with a private copy");
            Near(preview.transform.position, sourcePosition, "Duplicate: preview begins in place");
            Check(Quaternion.Angle(preview.transform.rotation, sourceRotation) < 0.001f &&
                Quaternion.Angle(preview.rotationOffset, source.rotationOffset) < 0.001f &&
                preview.GetComponent<ClipboardFixturePart>().Setting == 83 && ReferenceEquals(copied, Get<object>(plugin.Clipboard, "copied")),
                "Duplicate: preview preserves part rotation and saved settings without replacing the clipboard");
            Check(before.SequenceEqual(storage.SaveToStream().ToArray()) && money.money == 500 && history.Count == historyCount,
                "Duplicate: preview stays outside saves, undo and the budget");
            Input(Key.X); precision.ProcessInput();
            Check(precision.Axis == 0 && source != null && preview != null, "Part shortcuts: X constrains the axis during advanced move");
            Input(Key.Escape); precision.ProcessInput();
            Check(!precision.Session.Active && !preview.gameObject.activeSelf && placer.currentSelectedPart == source &&
                plane.transform.childCount == partCount && money.money == 500, "Duplicate: Escape cancels the preview and keeps the source selected");
            Idle();
            Input(Key.LeftCtrl, Key.D);
            committed = precision.Session.Target;
            precision.Constrain(0, false);
            precision.Type('3');
            precision.UpdateMouse();
            Input(Key.Enter); precision.ProcessInput();
            Check(!precision.Session.Active && committed.hasBeenPlaced && placer.currentSelectedPart == committed &&
                committed.transform.parent == plane.transform && plane.transform.childCount == partCount + 1 &&
                money.money == 463 && plugin.IsFocused == focused, "Duplicate: confirming commits exactly one selected part and charges its price");
            Near(committed.transform.position, sourcePosition + Vector3.right * 3, "Duplicate: exact movement applies to the copy");
            Near(source.transform.position, sourcePosition, "Duplicate: moving the copy leaves the source in place");
            Call(storage, "ExecuteHistoryUpdate");
            Check(history.Count == historyCount + 1, "Duplicate: confirmation creates one native undo snapshot");
            Idle();
            money.money = 0;
            Input(Key.LeftCtrl, Key.D);
            Input(Key.Enter); precision.ProcessInput();
            Check(!precision.Session.Active && plane.transform.childCount == partCount + 1 && money.money == 0 &&
                plugin.Clipboard.Message.Contains("Not enough money"), "Duplicate: unaffordable confirmation leaves the aircraft intact");
            Idle();
            GameManager.Instance.gameModeData.creativeMode = true;
            Input(Key.X);
            Check(placer.currentSelectedPart == committed, "Part shortcuts: idle X keeps the active part");
            placer.DestroySelectedPart();
            Check(placer.currentSelectedPart == null && !Get<System.Collections.Generic.List<BuildingPart>>(placer.partContainer, "parts").Contains(committed) && source != null,
                "Part shortcuts: native deletion removes the active part");
        }
        finally
        {
            precision.Cancel();
            Idle();
            foreach (var part in new[] { source, committed })
                if (part != null) { placer.partContainer.RemovePart(part); UnityEngine.Object.DestroyImmediate(part.gameObject); }
            Call(placer, "set_currentSelectedPart", oldSelected);
            placer.decalPlacer = oldDecals;
            placer.priceFeedbackManager = oldFeedback;
            placer.procduralPartController = oldProcedural;
            placer.moveGizmo = oldMoveGizmo; placer.rotationGizmo = oldRotateGizmo;
            store.SetValue(null, oldStore);
            Put(plugin.Clipboard, "copied", oldClipboard);
            GameManager.Instance.gameModeData = oldMode;
            money.money = oldMoney;
            UnityEngine.Object.DestroyImmediate(fixtures);
        }
    }

    private void TestShortcuts(Plugin plugin, PartPlacer placer)
    {
        var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        void Keypress(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); InputSystem.Update();
            Put(plugin, "lastHelpFrame", -1); plugin.ProcessHelpInput();
        }
        Keypress(Key.K);
        Check(plugin.ShortcutsOpen && plugin.BlockPanelClick, "Shortcuts: K opens menu and blocks world clicks");
        Check(InputManager.Instance.currentInputBlocker != null && !plugin.CanStartPrecision,
            "Shortcuts: open menu blocks native shortcuts and precision editing");
        var help = Get<object>(plugin, "shortcutsUI");
        var root = Get<GameObject>(help, "root");
        var card = Get<RectTransform>(help, "card");
        var columns = card.GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.name.EndsWith("shortcuts") && t.name != "Hotkeys title").ToArray();
        var layoutCenter = ((RectTransform)root.transform).rect.center;
        Canvas.ForceUpdateCanvases();
        foreach (var column in columns) column.ForceMeshUpdate();
        Check(card.anchorMin == Vector2.one * 0.5f && card.anchorMax == card.anchorMin &&
            (Vector2)card.localPosition == layoutCenter, "Shortcuts: the card is centered on the native canvas");
        Check(columns.Length == 2 && columns.All(t => !t.isTextOverflowing && t.fontSize >= 16) &&
                columns.Any(t => t.text.Contains("Ctrl + D") && t.text.Contains("Backspace / Delete") && t.text.Contains("Ctrl + V")) &&
            columns.Any(t => t.text.Contains("Ctrl + Tab") && t.text.Contains("X / Y / Z")),
            "Shortcuts: labeled columns fit the card and list duplicate, delete and transform controls");
        Keypress(Key.K);
        Check(!plugin.ShortcutsOpen && InputManager.Instance.currentInputBlocker == null, "Shortcuts: second K closes and releases input");
        Keypress(Key.K); Keypress(Key.Escape);
        Check(!plugin.ShortcutsOpen && !plugin.BlockPanelClick, "Shortcuts: Escape closes and restores editor interaction");
        GameManager.Instance.modalsOpen = true; Keypress(Key.K);
        Check(!plugin.ShortcutsOpen, "Shortcuts: native modal prevents opening help");
        GameManager.Instance.modalsOpen = false;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        Call(plugin, "LateUpdate");
        var openButton = Get<Button>(help, "openButton");
        Check(openButton.IsInteractable() && openButton.gameObject.activeInHierarchy, "Shortcuts: Hotkeys button is available in the editor toolbar");
        openButton.onClick.Invoke();
        Check(plugin.ShortcutsOpen && plugin.BlockPanelClick, "Shortcuts: clicking Hotkeys opens the same input-blocking menu");
        card.Find("EditorTools Close Hotkeys").GetComponent<Button>().onClick.Invoke();
        Check(!plugin.ShortcutsOpen && !plugin.BlockPanelClick, "Shortcuts: clicking Close returns to the editor");

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.V)); InputSystem.Update();
        Put(plugin, "lastPartShortcutFrame", -1); plugin.ProcessPartShortcuts();
        Check(plugin.Clipboard.Message.Contains("Ctrl+C") && placer.currentMovingPart == null && plugin.HideParts.HiddenCount == 0,
            "Clipboard: pasting without a copied part explains how to copy and leaves the aircraft alone");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
    }
}

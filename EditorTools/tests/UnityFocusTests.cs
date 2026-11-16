using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Aviassembly.EditorTools;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using TMPro;

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
        Check(!Get<bool>(plugin, "anchored"), "Starts Unfocused; selecting a part does not change mode");
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

        // Second toggle restores the game's center and releases its normal camera logic.
        var defaultCenter = (Vector3)Call(controller, "GetPlaneCenter");
        float retainedDistance = controller.currentDistance;
        Call(plugin, "ToggleFocus");
        Check(!Get<bool>(plugin, "anchored"), "Second toggle changes state to Unfocused immediately");
        Near(Get<Vector3>(controller, "currentOrigin"), defaultCenter, "Unfocused uses the actual native aircraft center");
        Put(plugin, "transitionStart", Time.unscaledTime - 2);
        controller.Update();
        Check(!(bool)Call(plugin, "OwnsOrigin", controller), "Unfocused releases the game's UpdateOrigin after transition");
        Near(camera.transform.position + camera.transform.forward * controller.currentDistance, defaultCenter, "Unfocused returns to the default orbit center");
        Check(Mathf.Abs(controller.currentDistance - retainedDistance) < 0.0001f, "Reset retains zoom instead of applying old close framing");
        Call(plugin, "Observe", placer);
        Check(!Get<bool>(plugin, "anchored"), "Current selection cannot immediately reactivate focus after reset");
        var other = new GameObject("Other selected part").AddComponent<BuildingPart>();
        other.transform.SetParent(plane.transform);
        other.transform.position = new Vector3(-6, 5, 2);
        other.hasBeenPlaced = true;
        Vector3 viewBefore = camera.transform.position;
        Call(placer, "set_currentSelectedPart", other);
        Call(plugin, "Observe", placer);
        controller.Update();
        Near(camera.transform.position, viewBefore, "Selecting another part while Unfocused leaves camera in place");
        Check(!Get<bool>(plugin, "anchored"), "Selection preserves Unfocused mode");
        Call(plugin, "ToggleFocus");
        Near(Get<Vector3>(controller, "currentOrigin"), other.transform.position, "Toggle after changing selection uses the new part pivot");
        Call(placer, "set_currentSelectedPart", part);
        Call(plugin, "Observe", placer);
        Near(Get<Vector3>(controller, "currentOrigin"), part.transform.position, "Focused selection follows the newly selected part");
        Check(Get<bool>(plugin, "anchored"), "Selection preserves Focused mode");
        Check(Mathf.Abs(controller.currentDistance - retainedDistance) < 0.0001f, "Changing focused selection retains zoom");

        Put(placer, "<currentMovingPart>k__BackingField", other);
        Call(placer, "set_currentSelectedPart", other);
        Call(plugin, "Observe", placer);
        var pickupOrigin = Get<Vector3>(controller, "currentOrigin");
        other.transform.position += Vector3.up;
        plugin.UpdateFollowingCamera();
        Near(Get<Vector3>(controller, "currentOrigin"), pickupOrigin, "Picking up and moving a part does not retarget focused camera");
        Call(plugin, "ToggleFocus");
        Check(!Get<bool>(plugin, "anchored"), "F can switch to Unfocused during placement");
        Put(placer, "<currentMovingPart>k__BackingField", null);
        Call(plugin, "Observe", placer);
        Check(!Get<bool>(plugin, "anchored"), "Finishing placement respects manual Unfocused override");
        Call(plugin, "ToggleFocus");
        Near(Get<Vector3>(controller, "currentOrigin"), other.transform.position, "F can restore focus after placement");
        manager.inMenu = true;
        Call(plugin, "Observe", placer);
        Check((bool)Call(plugin, "OwnsOrigin", controller), "Opening a menu retains focused orbit origin");
        Call(plugin, "ToggleFocus");
        Check(Get<bool>(plugin, "anchored"), "Menus block mode toggling");
        manager.inMenu = false;
        Call(plugin, "Observe", placer);
        Near(Get<Vector3>(controller, "currentOrigin"), other.transform.position, "Closing a menu retains focused orbit origin");
        manager.modalsOpen = true;
        Call(plugin, "ToggleFocus");
        Check(Get<bool>(plugin, "anchored"), "Modal blocks mode toggling");
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
            Check(label.text == "Unfocused  [F]" && !Get<bool>(plugin, "anchored"), "UI button resets camera and immediately displays Unfocused");
            Call(plugin, "Observe", placer);
            Check(!Get<bool>(plugin, "anchored"), "UI reset stays Unfocused despite existing selection");
            button.onClick.Invoke();
            Check(label.text == "Focused  [F]" && Get<bool>(plugin, "anchored"), "UI button toggles back to Focused");
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
        Check(Get<bool>(plugin, "anchored"), "Deleting a target retains Focused mode for the next selection");
        Call(plugin, "ToggleFocus");
        Check(!Get<bool>(plugin, "anchored"), "Reset works even after the focused part is deleted");
        Call(plugin, "ToggleFocus");
        Check(Get<bool>(plugin, "anchored"), "F enters Focused mode without a selected target");
        Call(plugin, "ToggleFocus");
        Call(placer, "set_currentSelectedPart", part);
        Call(plugin, "Observe", placer);
        Check(!Get<bool>(plugin, "anchored"), "New selection after deletion preserves Unfocused mode");
        Call(plugin, "ToggleFocus");
        Call(plugin, "ToggleFocus");
        Call(placer, "set_currentSelectedPart", part);
        Call(plugin, "Observe", placer);
        Check(!Get<bool>(plugin, "anchored"), "Clicking the same part while Unfocused stays Unfocused");
        Put(plugin, "transitionStart", Time.unscaledTime - 2);
        controller.Update();
        part.transform.position += Vector3.right * 4;
        var oldNativeOrigin = Get<Vector3>(controller, "currentOrigin");
        controller.UpdateOrigin(true);
        var nextNativeOrigin = Get<Vector3>(controller, "currentOrigin");
        var changedCenter = (Vector3)Call(controller, "GetPlaneCenter");
        Check((nextNativeOrigin - changedCenter).sqrMagnitude < (oldNativeOrigin - changedCenter).sqrMagnitude,
            "Unfocused native camera follows later aircraft center changes");
        Call(plugin, "ToggleFocus");
        GameManager.gameMode = GameMode.Flying;
        Call(plugin, "Observe", placer);
        Check(!(bool)Call(plugin, "OwnsOrigin", controller), "Entering flight releases editor camera ownership");
        GameManager.gameMode = GameMode.Building;
        Put(placer, "<currentSelectedPart>k__BackingField", part);
        Call(plugin, "Observe", placer);
        Call(plugin, "ToggleFocus");
        Call(plugin, "ResetFocus");
        Check(!(bool)Call(plugin, "OwnsOrigin", controller), "Aircraft reset releases orbit ownership");
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
        plugin.ToggleFocus(); plugin.ToggleFocus(); Call(plugin, "Observe", placer);
        Check(!plugin.IsFocused, "Placement: F override is not reactivated by continued dragging");
        FocusNewPlacementPatch.Prefix(placer, out var newlyPlaced);
        Call(placer, "set_currentMovingPart", (object)null); pending.hasBeenPlaced = true;
        Call(placer, "set_currentSelectedPart", (object)null); Call(plugin, "Observe", placer);
        FocusNewPlacementPatch.Postfix(newlyPlaced);
        Check(plugin.IsFocused && plugin.PlacementInputCamera == camera,
            "Placement: completion focuses a new part and releases frozen projection");
        Near(Get<Vector3>(controller, "currentOrigin"), pending.transform.position, "Placement: new completed placement becomes orbit target");
        Put(plugin, "transitionStart", Time.unscaledTime - 2); plugin.UpdateFollowingCamera();
        var completedOrigin = Get<Vector3>(controller, "currentOrigin");
        Call(placer, "set_currentMovingPart", pending); Call(placer, "set_currentSelectedPart", pending);
        FocusNewPlacementPatch.Prefix(placer, out var existingPlaced);
        Check(existingPlaced == null, "Placement: moving an existing part is excluded from automatic focus");
        Call(placer, "set_currentMovingPart", (object)null); Call(placer, "set_currentSelectedPart", (object)null);
        pending.transform.position += Vector3.up;
        FocusNewPlacementPatch.Postfix(existingPlaced);
        Put(plugin, "transitionStart", Time.unscaledTime - 2); plugin.UpdateFollowingCamera();
        Near(Get<Vector3>(controller, "currentOrigin"), completedOrigin,
            "Placement: existing-part movement does not move the focused pivot");
        plugin.ToggleFocus(); plugin.ToggleFocus(); Call(plugin, "LateUpdate");
        Check(plugin.IsFocused && Get<Button>(ui, "button").interactable,
            "Placement: F and UI can cycle modes with no selected part or active tool");
        plugin.ToggleFocus();
        UnityEngine.Object.DestroyImmediate(pending.gameObject);
        Call(placer, "set_currentSelectedPart", part); Call(plugin, "ResetFocus"); Call(plugin, "Observe", placer);
        InputSystem.QueueStateEvent(MouseInput.currentMouse, new MouseState()); InputSystem.Update();
        UnityPrecisionTests.Run(plugin, placer, plane, part, Check);
        UnityHandleTests.Run(plugin, placer, part, Check);
        TestShortcuts(plugin, placer);
        UnityHidePartsIntegrationTests.Run(plugin, placer, plane, part, Check);
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
        var label = root.GetComponentInChildren<TextMeshProUGUI>();
        Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
        Check(label.text.Contains("V    Hide") && label.text.Contains("Ctrl + Tab") && label.textInfo.characterCount > 200 && !label.isTextOverflowing,
            "Shortcuts: complete shortcut text fits native canvas");
        Keypress(Key.K);
        Check(!plugin.ShortcutsOpen && InputManager.Instance.currentInputBlocker == null, "Shortcuts: second K closes and releases input");
        Keypress(Key.K); Keypress(Key.Escape);
        Check(!plugin.ShortcutsOpen && !plugin.BlockPanelClick, "Shortcuts: Escape closes and restores editor interaction");
        GameManager.Instance.modalsOpen = true; Keypress(Key.K);
        Check(!plugin.ShortcutsOpen, "Shortcuts: native modal prevents opening help");
        GameManager.Instance.modalsOpen = false;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
    }
}

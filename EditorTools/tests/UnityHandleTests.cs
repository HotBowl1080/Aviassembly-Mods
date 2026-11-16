using System;
using System.Linq;
using Aviassembly.EditorTools;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class UnityHandleTests
{
    private static object Call(object o, string name, params object[] args) => AccessTools.Method(o.GetType(), name).Invoke(o, args);
    private static void Put(object o, string name, object value) => AccessTools.Field(o.GetType(), name).SetValue(o, value);
    private static T Get<T>(object o, string name) => (T)AccessTools.Field(o.GetType(), name).GetValue(o);
    public static void Run(Plugin plugin, PartPlacer placer, BuildingPart part, Action<bool, string> check)
    {
        var precision = plugin.Precision;
        var cam = BuildingCamera.cam;
        var originalPosition = part.transform.position; var originalRotation = part.transform.rotation;
        var originalCameraPosition = cam.transform.position; var originalCameraRotation = cam.transform.rotation;
        var originalMove = placer.moveGizmo; var originalRotate = placer.rotationGizmo;
        var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>(); Put(precision, "keyboard", keyboard);
        var move = new GameObject("Handle fixture move").AddComponent<MoveGizmo>();
        var rotate = new GameObject("Handle fixture rotate").AddComponent<RotationGizmo>();
        MoveGizmoElement Arrow(string name, Vector3 offset, Vector3 direction)
        {
            var go = new GameObject(name, typeof(BoxCollider), typeof(MoveGizmoElement));
            go.transform.SetParent(move.transform, false); go.transform.localPosition = offset;
            go.GetComponent<BoxCollider>().size = Vector3.one * 0.3f;
            var arrow = go.GetComponent<MoveGizmoElement>();
            arrow.direction = new GameObject(name + " direction").transform; arrow.direction.SetParent(go.transform, false);
            arrow.direction.rotation = Quaternion.LookRotation(direction); arrow.origin = move.transform; return arrow;
        }
        RotationGizmoElement Ring(string name, Vector3 offset)
        {
            var go = new GameObject(name, typeof(BoxCollider), typeof(RotationGizmoElement));
            go.transform.SetParent(rotate.transform, false); go.transform.localPosition = offset;
            go.GetComponent<BoxCollider>().size = Vector3.one * 0.3f; return go.GetComponent<RotationGizmoElement>();
        }
        void NextFrame()
        {
            Put(precision, "consumedFrame", -1); Call(precision, "ReleaseBlocker");
        }
        void InputAt(Vector2 point, bool held = false, params Key[] keys)
        {
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point, buttons = (ushort)(held ? 1 : 0) });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update();
        }
        void Type(string text) { foreach (var c in text) Call(precision, "OnText", c); precision.ProcessInput(); }
        void ResetPose()
        {
            precision.Cancel(); NextFrame(); part.SetPositionAndRotation(originalPosition, Quaternion.identity);
            move.transform.SetPositionAndRotation(originalPosition, Quaternion.identity);
            rotate.transform.SetPositionAndRotation(originalPosition, Quaternion.identity);
            InputAt(Vector2.zero); Physics.SyncTransforms();
        }
        try
        {
            placer.moveGizmo = move; placer.rotationGizmo = rotate;
            move.X = Arrow("X", Vector3.right * 2, Vector3.right);
            move.Y = Arrow("Y", Vector3.up * 2, Vector3.up);
            move.Z = Arrow("Z", Vector3.left * 2, Vector3.forward);
            rotate.X = Ring("X", Vector3.left * 2); rotate.Y = Ring("Y", Vector3.down * 2); rotate.Z = Ring("Z", Vector3.right * 2);
            cam.transform.SetPositionAndRotation(originalPosition - Vector3.forward * 15, Quaternion.identity);
            ResetPose(); rotate.gameObject.SetActive(false);
            InputAt(cam.WorldToScreenPoint(move.X.transform.position), true);
            precision.ProcessInput();
            check(precision.Session.Active && !precision.Rotating, "Handles: clicking actual arrow collider starts precision without G");
            check(precision.ReadoutTitle == "Movement  X handle", "Handles: arrow readout shows only movement and clicked axis");
            Vector2 moveStart = cam.WorldToScreenPoint(move.X.transform.position);
            InputAt(cam.WorldToScreenPoint(move.X.transform.position + Vector3.right * 0.37f), true); precision.ProcessInput();
            check(Vector3.Distance(part.transform.position, originalPosition + Vector3.right * 0.37f) < 0.001f, "Handles: mouse movement is precise by default without snap");
            check(Vector3.Distance(move.transform.position, part.transform.position) < 0.0001f, "Handles: native arrows follow live movement preview");
            check(plugin.IsFocused, "Following: beginning a precision move automatically focuses its part");
            Put(plugin, "transitionStart", Time.unscaledTime - 2);
            plugin.UpdateFollowingCamera();
            check(Vector3.Distance(cam.transform.position + cam.transform.forward * placer.buildingCamera.currentDistance, part.transform.position) < 0.0001f,
                "Following: rendered camera tracks live movement preview");
            precision.ProcessInput(); plugin.UpdateFollowingCamera(); precision.ProcessInput();
            check(Vector3.Distance(part.transform.position, originalPosition + Vector3.right * 0.37f) < 0.001f,
                "Following: stationary cursor cannot feed camera motion back into movement");
            plugin.ToggleFocus();
            check(!plugin.IsFocused && precision.Session.Active, "Following: F toggles mode during precision without cancelling edit");
            plugin.UpdateFollowingCamera(); precision.ProcessInput();
            check(!plugin.IsFocused && Vector3.Distance(part.transform.position, originalPosition + Vector3.right * 0.37f) < 0.001f,
                "Following: manual Unfocused stays active without changing drag value");
            InputAt(Mouse.current.position.ReadValue(), false); precision.ProcessInput(); NextFrame();
            check(!precision.Session.Active && !precision.BlockGameInput && InputManager.Instance.currentInputBlocker == null,
                "Handles: release commits and restores normal idle controls");
            var committed = PlaneStorage.Instance.SaveToStream().ToArray();
            Type("0.35");
            check(Vector3.Distance(part.transform.position, originalPosition + Vector3.right * 0.35f) < 0.001f,
                "Handles: typing after release replaces total drag offset rather than adding to it");
            check(committed.SequenceEqual(PlaneStorage.Instance.SaveToStream().ToArray()), "Handles: numeric readjustment preview leaves committed save unchanged");
            InputAt(Vector2.zero, false, Key.Enter); precision.ProcessInput(); NextFrame(); InputAt(Vector2.zero);
            check(!precision.Session.Active && Vector3.Distance(part.transform.position, originalPosition + Vector3.right * 0.35f) < 0.001f,
                "Handles: Enter confirms post-drag exact value");
            Type("-");
            check(precision.Session.Active && !precision.ValidInput, "Handles: incomplete post-drag numeric input remains uncommitted");
            InputAt(Vector2.zero, false, Key.Escape); precision.ProcessInput(); NextFrame(); InputAt(Vector2.zero);
            check(Vector3.Distance(part.transform.position, originalPosition + Vector3.right * 0.35f) < 0.001f,
                "Handles: cancelling a readjustment preserves last committed drag");

            ResetPose(); move.gameObject.SetActive(false); rotate.gameObject.SetActive(true); Physics.SyncTransforms();
            InputAt(cam.WorldToScreenPoint(rotate.Z.transform.position), true); precision.ProcessInput();
            check(precision.Session.Active && precision.Rotating && precision.ReadoutTitle == "Rotation  Z handle",
                "Handles: clicking actual ring collider selects only its rotation axis");
            InputAt(cam.WorldToScreenPoint(originalPosition + Vector3.up * 2), true); precision.ProcessInput();
            check(Quaternion.Angle(part.transform.rotation, Quaternion.AngleAxis(90, Vector3.forward)) < 0.05f,
                "Handles: ring mouse drag previews rotation from start pose");
            check(Quaternion.Angle(rotate.transform.rotation, part.transform.rotation) < 0.02f, "Handles: native rings follow live rotation preview");
            Type("33.5");
            check(Quaternion.Angle(part.transform.rotation, Quaternion.AngleAxis(33.5f, Vector3.forward)) < 0.02f,
                "Handles: typing during ring drag replaces mouse angle exactly");
            InputAt(Mouse.current.position.ReadValue(), false); precision.ProcessInput();
            check(precision.Session.Active, "Handles: typed rotation waits for confirmation after release");
            InputAt(Vector2.zero, false, Key.Enter); precision.ProcessInput(); NextFrame(); InputAt(Vector2.zero);
            Type("-12.25");
            check(Quaternion.Angle(part.transform.rotation, Quaternion.AngleAxis(-12.25f, Vector3.forward)) < 0.02f,
                "Handles: typed angle after commit still uses original operation start");
            precision.Cancel(); NextFrame();
            check(Quaternion.Angle(part.transform.rotation, Quaternion.AngleAxis(33.5f, Vector3.forward)) < 0.02f,
                "Handles: Escape restores committed rotation when readjusting");
            ResetPose();
            placer.RotatePart();
            check(!precision.Session.Active && precision.ReadoutTitle == "Rotation", "Handles: native Rotate button only shows rotation hint without capturing input");
            placer.MovePart();
            check(!precision.Session.Active && precision.ReadoutTitle == "Movement", "Handles: native Move button replaces rotation hint with movement hint");
            precision.UI.Refresh(true, part, true);
            var uiRoot = Get<GameObject>(precision.UI, "root");
            check(uiRoot.GetComponentsInChildren<TMP_InputField>(true).Length == 0 && uiRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length == 0,
                "Handles: contextual readout contains no old panel fields or buttons");
            var label = Get<TMP_Text>(precision.UI, "label");
            check(label.text == "Direction (X,Y,Z): 0 m", "Readout: one line contains tool, axes, value and units");
            check(uiRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length == 0, "Readout: no white background box exists");
            var rect = (RectTransform)uiRoot.transform;
            check(rect.anchorMin == Vector2.one && rect.anchorMax == Vector2.one && rect.pivot == Vector2.one,
                "Readout: anchored in top-right corner");
            Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
            check(label.textInfo.characterCount > 10 && label.textInfo.meshInfo.Any(mesh => mesh.vertexCount > 0),
                "Readout: native font generates visible text geometry without vertical clipping");
            precision.BeginHandle(true, 2, Vector3.forward, originalPosition + Vector3.right);
            Type("33.5"); precision.UI.Refresh(true, part, true); label.ForceMeshUpdate();
            check(label.text == "Rotation (Z): 33.5\u00b0", "Readout: rotation uses requested compact degrees format");
            check(label.textInfo.meshInfo.Any(mesh => mesh.vertexCount > 0), "Readout: typed rotation text has visible geometry");
            precision.Cancel(); NextFrame();
            InputAt(Vector2.zero); precision.ProcessInput();
            check(!precision.Session.Active && !precision.BlockGameInput && !precision.HasReadout,
                "Handles: no input leaves native camera and selection controls free");
            ResetPose();
            precision.BeginHandle(false, 0, Vector3.right, originalPosition + Vector3.right);
            precision.Apply(); NextFrame();
            part.transform.position += Vector3.up;
            check(!precision.BeginRecentAdjustment(), "Handles: unrelated transform invalidates remembered operation");
            ResetPose();
            precision.BeginHandle(false, 0, Vector3.right, originalPosition + Vector3.right);
            precision.Apply(); NextFrame();
            Call(placer, "set_currentSelectedPart", part);
            check(!precision.BeginRecentAdjustment(), "Handles: reselecting a part clears stale numeric adjustment");
        }
        finally
        {
            precision.Cancel(); NextFrame();
            placer.moveGizmo = originalMove; placer.rotationGizmo = originalRotate;
            part.SetPositionAndRotation(originalPosition, originalRotation);
            cam.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
            UnityEngine.Object.DestroyImmediate(move.gameObject); UnityEngine.Object.DestroyImmediate(rotate.gameObject);
            InputAt(Vector2.zero);
        }
    }
}

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
    private static Vector2 PickRing(Camera camera, RotationGizmo gizmo, RotationGizmoElement ring)
    {
        var colliders = gizmo.GetComponentsInChildren<Collider>().Where(c => c.GetComponentInParent<RotationGizmoElement>() != null).ToArray();
        var center = gizmo.transform.position;
        float reach = colliders.Max(c => Vector3.Distance(center, c.bounds.center) + c.bounds.extents.magnitude);
        for (int angle = 0; angle < 360; angle += 11)
        {
            var radial = Quaternion.AngleAxis(angle, ring.transform.forward) * ring.transform.up;
            for (int radius = 1; radius <= 100; radius++)
            {
                Vector2 mouse = camera.WorldToScreenPoint(center + radial * (radius * reach / 100));
                var ray = camera.ScreenPointToRay(mouse);
                var hits = colliders.Where(c => c.enabled && c.Raycast(ray, out _, 100)).ToArray();
                if (hits.Length == 1 && (hits[0].GetComponentInParent<RotationGizmoElement>() == ring ||
                    hits[0].GetComponentInChildren<RotationGizmoElement>() == ring)) return mouse;
            }
        }
        throw new Exception("Could not pick native rotation ring " + ring.name + " active=" + ring.gameObject.activeInHierarchy + " colliders=" +
            string.Join(", ", colliders.Select(c => c.name + " " + c.bounds.size + " " + c.enabled)));
    }
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
            if (plugin.IsFocused) plugin.ToggleFocus();
            var angleButton = new GameObject("Angle cycle fixture", typeof(RectTransform)).AddComponent<RotationSnapButton>();
            angleButton.gizmo = rotate;
            angleButton.icon = angleButton.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            angleButton.snapAngleText = new GameObject("Angle label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            angleButton.snapAngleText.transform.SetParent(angleButton.transform, false);
            Call(rotate, "Awake"); Call(angleButton, "Start");
            foreach (int step in new[] { 1, 5, 15, 30, 45, 60, 75, 90, 0, 1 })
            {
                float expected = step == 0 ? -37.4f : Mathf.Round(-37.4f / step) * step;
                Vector2 center = cam.WorldToScreenPoint(originalPosition);
                var start = center + Vector2.right * 100;
                var end = center + (Vector2)(Quaternion.AngleAxis(-37.4f, Vector3.forward) * Vector3.right) * 100;
                foreach (bool ring in new[] { false, true })
                {
                    ResetPose(); InputAt(ring ? (Vector2)cam.WorldToScreenPoint(originalPosition + Vector3.right) : start, ring);
                    if (ring) precision.BeginHandle(true, 2, Vector3.forward, originalPosition + Vector3.right);
                    else { precision.Begin(true, true); precision.Constrain(2, false); }
                    InputAt(ring ? (Vector2)precision.ProjectionCamera.WorldToScreenPoint(originalPosition +
                        Quaternion.AngleAxis(-37.4f, Vector3.forward) * Vector3.right) : end, ring); precision.UpdateMouse();
                    check(Quaternion.Angle(part.transform.rotation, Quaternion.AngleAxis(expected, Vector3.forward)) < 0.05f,
                        "Advanced snap: " + (ring ? "dragged ring" : "axis rotation") + " follows the button at " + step + " (" + precision.ReadoutLine + ")");
                }
                ResetPose(); InputAt(start); precision.Begin(true, true);
                Put(precision, "<Trackball>k__BackingField", true);
                InputAt(start + Vector2.right * (-37.4f / 0.35f)); precision.UpdateMouse();
                check(Quaternion.Angle(part.transform.rotation, Quaternion.AngleAxis(expected, cam.transform.up)) < 0.05f,
                    "Advanced snap: trackball follows the button at " + step);
                ResetPose();
                check(rotate.snapIncrement == step && angleButton.snapAngleText.text == (step == 0 ? "Free" : step + "\u00b0") &&
                    Mathf.Abs(RotationSteps.Apply(-37.4f, rotate) - expected) < 0.0001f,
                    "Angle button: displayed step and applied rotation agree at " + step);
                angleButton.Press();
            }
            Call(rotate, "Awake");
            UnityEngine.Object.DestroyImmediate(angleButton.gameObject);
            foreach (bool rotationMenu in new[] { false, true })
            for (int axis = 0; axis < 3; axis++)
            {
                ResetPose();
                move.gameObject.SetActive(!rotationMenu);
                rotate.gameObject.SetActive(rotationMenu);
                if (rotationMenu) placer.RotatePart(); else placer.MovePart();
                var key = axis == 0 ? Key.X : axis == 1 ? Key.Y : Key.Z;
                InputAt(Vector2.zero, false, key); precision.ProcessInput();
                check(precision.Session.Active && precision.Rotating == rotationMenu && precision.Axis == axis && !precision.Local,
                    "Menu shortcut: " + key + " starts a world-axis edit directly in " + (rotationMenu ? "Rotate" : "Move"));
                InputAt(Vector2.zero);
                Type("2");
                var direction = TransformMath.Axis(axis);
                check(rotationMenu ? Quaternion.Angle(part.transform.rotation, Quaternion.AngleAxis(2, direction)) < 0.04f :
                    Vector3.Distance(part.transform.position, originalPosition + direction * 2) < 0.001f,
                    "Menu shortcut: typed value affects only " + key + " in " + (rotationMenu ? "Rotate" : "Move"));
                Type("5");
                InputAt(Vector2.zero, false, Key.Backspace); precision.ProcessInput();
                plugin.ProcessPartShortcuts();
                check(precision.Session.Active && placer.currentSelectedPart == part &&
                    (rotationMenu ? Quaternion.Angle(part.transform.rotation, Quaternion.AngleAxis(2, direction)) < 0.04f :
                    Vector3.Distance(part.transform.position, originalPosition + direction * 2) < 0.001f),
                    "Menu shortcut: Backspace erases the last digit and keeps the active part");
            }
            ResetPose(); move.gameObject.SetActive(true); rotate.gameObject.SetActive(true);
            for (int axis = 0; axis < 3; axis++)
            foreach (bool rotation in new[] { false, true })
            {
                ResetPose();
                var pose = Quaternion.Euler(20, 35, -15); part.transform.rotation = pose;
                precision.Begin(rotation, true); precision.Constrain(axis, false); precision.Constrain(axis, false);
                foreach (char c in rotation ? "-33.5" : "0.35") precision.Type(c); precision.UpdateMouse();
                var keyboardPosition = part.transform.position; var keyboardRotation = part.transform.rotation;
                precision.Cancel(); NextFrame();
                var direction = pose * (axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward);
                precision.BeginHandle(rotation, axis, direction, originalPosition + Vector3.right);
                Type(rotation ? "-33.5" : "0.35");
                check(Vector3.Distance(part.transform.position, keyboardPosition) < 0.001f && Quaternion.Angle(part.transform.rotation, keyboardRotation) < 0.03f,
                    "Axis consistency: keyboard local and native handle match for " + (rotation ? "rotation " : "move ") + axis);
                precision.Cancel(); NextFrame();
            }
            ResetPose(); rotate.gameObject.SetActive(false);
            InputAt(cam.WorldToScreenPoint(move.X.transform.position), true);
            precision.ProcessInput();
            check(precision.Session.Active && !precision.Rotating, "Handles: clicking actual arrow collider starts precision without G");
            check(precision.ReadoutTitle == "Movement  X handle", "Handles: arrow readout shows only movement and clicked axis");
            Vector2 moveStart = cam.WorldToScreenPoint(move.X.transform.position);
            InputAt(cam.WorldToScreenPoint(move.X.transform.position + Vector3.right * 0.37f), true); precision.ProcessInput();
            check(Vector3.Distance(part.transform.position, originalPosition + Vector3.right * 0.37f) < 0.001f, "Handles: mouse movement is precise by default without snap");
            check(Vector3.Distance(move.transform.position, part.transform.position) < 0.0001f, "Handles: native arrows follow live movement preview");
            check(!plugin.IsFocused, "Camera: beginning a precision move preserves Unfocused mode");
            var cameraBeforeDrag = cam.transform.position;
            Put(plugin, "transitionStart", Time.unscaledTime - 2);
            plugin.UpdateFollowingCamera();
            check(Vector3.Distance(cam.transform.position, cameraBeforeDrag) < 0.0001f,
                "Camera: rendered camera remains stationary during movement preview");
            precision.ProcessInput(); plugin.UpdateFollowingCamera(); precision.ProcessInput();
            check(Vector3.Distance(part.transform.position, originalPosition + Vector3.right * 0.37f) < 0.001f,
                "Following: stationary cursor cannot feed camera motion back into movement");
            plugin.ToggleFocus(); plugin.ToggleFocus();
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
            var template = Resources.FindObjectsOfTypeAll<RotationGizmo>().First(g => g != rotate && g.X != null && g.Y != null && g.Z != null);
            var stock = UnityEngine.Object.Instantiate(template);
            try
            {
                stock.gameObject.SetActive(true); stock.transform.localScale = Vector3.one;
                placer.rotationGizmo = stock;
                var pose = Quaternion.Euler(23, 51, -17);
                foreach (var ring in new[] { stock.X, stock.Y, stock.Z })
                {
                    part.SetPositionAndRotation(originalPosition, pose);
                    stock.transform.SetPositionAndRotation(originalPosition, pose);
                    var normal = ring.transform.forward;
                    cam.transform.SetPositionAndRotation(originalPosition - normal * 8, Quaternion.LookRotation(normal, ring.transform.up));
                    Physics.SyncTransforms();
                    Vector2 click = PickRing(cam, stock, ring);
                    InputAt(click, true); precision.ProcessInput();
                    check(precision.Session.Active && precision.Rotating, "Handles: native ring picked on rotated part " + ring.name);
                    var plane = new Plane(normal, originalPosition);
                    var ray = cam.ScreenPointToRay(click);
                    if (!plane.Raycast(ray, out float distance)) throw new Exception("Native ring is behind camera");
                    Vector3 radius = ray.GetPoint(distance) - originalPosition;
                    InputAt(cam.WorldToScreenPoint(originalPosition + Quaternion.AngleAxis(25, normal) * radius), true); precision.ProcessInput();
                    check(Quaternion.Angle(part.transform.rotation, Quaternion.AngleAxis(25, normal) * pose) < 0.08f,
                        "Handles: circular mouse drag follows native ring plane " + ring.name);
                    int axis = Enumerable.Range(0, 3).OrderByDescending(i => Mathf.Abs(Vector3.Dot(pose * TransformMath.Axis(i), normal))).First();
                    Type("32");
                    check(precision.Axis == axis && Quaternion.Angle(part.transform.rotation, pose * Quaternion.AngleAxis(32, TransformMath.Axis(axis))) < 0.08f,
                        "Handles: native ring readout and typed angle agree with the part's local axis " + ring.name);
                    precision.Cancel(); NextFrame(); InputAt(Vector2.zero);
                }
            }
            finally { placer.rotationGizmo = rotate; UnityEngine.Object.DestroyImmediate(stock.gameObject); }
            var arrowTemplate = Resources.FindObjectsOfTypeAll<MoveGizmo>().First(g => g != move && g.X != null && g.Y != null && g.Z != null);
            var stockArrows = UnityEngine.Object.Instantiate(arrowTemplate);
            try
            {
                rotate.gameObject.SetActive(false);
                stockArrows.gameObject.SetActive(true); stockArrows.transform.localScale = Vector3.one;
                placer.moveGizmo = stockArrows;
                var pose = Quaternion.Euler(-18, 43, 11);
                foreach (var arrow in new[] { stockArrows.X, stockArrows.Y, stockArrows.Z })
                {
                    part.SetPositionAndRotation(originalPosition, pose);
                    stockArrows.transform.SetPositionAndRotation(originalPosition, pose);
                    var view = pose * new Vector3(5, 4, -6);
                    cam.transform.SetPositionAndRotation(originalPosition + view, Quaternion.LookRotation(-view));
                    Physics.SyncTransforms();
                    var collider = arrow.GetComponentInChildren<Collider>() ?? arrow.GetComponentInParent<Collider>();
                    Vector2 click = cam.WorldToScreenPoint(collider.bounds.center);
                    var direction = arrow.direction.forward;
                    int axis = Enumerable.Range(0, 3).OrderByDescending(i => Mathf.Abs(Vector3.Dot(pose * TransformMath.Axis(i), direction))).First();
                    InputAt(click, true); precision.ProcessInput();
                    Type("0.35");
                    check(precision.Session.Active && !precision.Rotating && precision.Axis == axis &&
                        Vector3.Distance(part.transform.position, originalPosition + 0.35f * direction) < 0.001f,
                        "Audit: native movement arrow matches its local axis and entered distance " + arrow.name + " local=" + Quaternion.Inverse(pose) * direction);
                    precision.Cancel(); NextFrame(); InputAt(Vector2.zero);
                }
                for (int normal = 0; normal < 3; normal++)
                {
                    var first = normal == 0 ? stockArrows.Y : stockArrows.X;
                    var second = normal == 2 ? stockArrows.Y : stockArrows.Z;
                    part.SetPositionAndRotation(originalPosition, pose);
                    stockArrows.transform.SetPositionAndRotation(originalPosition, pose);
                    Physics.SyncTransforms();
                    Vector3 Reach(MoveGizmoElement arrow)
                    {
                        var collider = arrow.GetComponentInChildren<Collider>() ?? arrow.GetComponentInParent<Collider>();
                        return Vector3.Project(collider.bounds.center - originalPosition, arrow.direction.forward);
                    }
                    var grab = originalPosition + (Reach(first) + Reach(second)) * 0.295f;
                    Vector2 start = cam.WorldToScreenPoint(grab);
                    InputAt(start, true); precision.ProcessInput();
                    check(precision.Session.Active && precision.PlaneLock && precision.Local && precision.Axis == normal,
                        "Plane handles: actual gizmo click starts local plane excluding " + "XYZ"[normal] + " (" + precision.ReadoutLine + ")");
                    var delta = first.direction.forward * 0.31f - second.direction.forward * 0.23f;
                    Vector2 end = cam.WorldToScreenPoint(grab + delta);
                    InputAt(end, true); precision.ProcessInput();
                    check(Vector3.Distance(part.transform.position, originalPosition + delta) < 0.001f,
                        "Plane handles: both axes track the cursor on a rotated part, excluding " + "XYZ"[normal]);
                    InputAt(end); precision.ProcessInput();
                    check(!precision.Session.Active && Vector3.Distance(part.transform.position, originalPosition + delta) < 0.001f,
                        "Plane handles: release confirms both offsets");
                    precision.Cancel(); NextFrame(); InputAt(Vector2.zero);
                    part.SetPositionAndRotation(originalPosition, pose);
                    stockArrows.transform.SetPositionAndRotation(originalPosition, pose);
                    Physics.SyncTransforms();
                    InputAt(start, true); precision.ProcessInput();
                    Type("0.25");
                    InputAt(start, true, Key.Tab); precision.ProcessInput();
                    InputAt(start, true);
                    Type("-0.4");
                    var exact = originalPosition + first.direction.forward * 0.25f - second.direction.forward * 0.4f;
                    InputAt(start); precision.ProcessInput();
                    check(precision.Session.Active && Vector3.Distance(part.transform.position, exact) < 0.001f,
                        "Plane handles: two typed local offsets preview separately and await confirmation");
                    InputAt(start, false, Key.Escape); precision.ProcessInput();
                    check(!precision.Session.Active && Vector3.Distance(part.transform.position, originalPosition) < 0.001f,
                        "Plane handles: Escape restores the pose from before the drag");
                    NextFrame(); InputAt(Vector2.zero);
                }
            }
            finally { placer.moveGizmo = move; UnityEngine.Object.DestroyImmediate(stockArrows.gameObject); }
            cam.transform.SetPositionAndRotation(originalPosition - Vector3.forward * 15, Quaternion.identity);
            ResetPose();
            move.gameObject.SetActive(false); rotate.gameObject.SetActive(false);
            foreach (var pair in new[] { (Key.X, Key.Y, 2), (Key.X, Key.Z, 1), (Key.Y, Key.Z, 0) })
            foreach (bool chord in new[] { false, true })
            {
                ResetPose();
                Vector2 center = cam.WorldToScreenPoint(originalPosition);
                if (chord)
                {
                    InputAt(center, false, Key.G, pair.Item1, pair.Item2); precision.ProcessInput();
                }
                else
                {
                    InputAt(center, false, Key.G); precision.ProcessInput();
                    InputAt(center, false, pair.Item1); precision.ProcessInput();
                    InputAt(center, false, pair.Item2); precision.ProcessInput();
                }
                var expected = new Vector3(2, 2, 2);
                expected[pair.Item3] = 0;
                InputAt(center); Type("2");
                InputAt(center, false, Key.Tab); precision.ProcessInput();
                InputAt(center); Type("2");
                check(precision.Session.Active && precision.PlaneLock && !precision.Local && precision.Axis == pair.Item3 &&
                    Vector3.Distance(part.transform.position, originalPosition + expected) < 0.001f,
                    "Plane keys: " + pair.Item1 + "+" + pair.Item2 + " constrains G and accepts two components, chord=" + chord);
            }
            ResetPose();
            rotate.gameObject.SetActive(true);
            placer.RotatePart();
            check(!precision.Session.Active && precision.ReadoutTitle == "Rotation", "Handles: native Rotate button only shows rotation hint without capturing input");
            placer.MovePart();
            check(!precision.Session.Active && precision.ReadoutTitle == "Movement", "Handles: native Move button replaces rotation hint with movement hint");
            precision.UI.Refresh(true, part, true);
            var uiRoot = Get<GameObject>(precision.UI, "root");
            check(uiRoot.GetComponentsInChildren<TMP_InputField>(true).Length == 0 && uiRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length == 0,
                "Handles: contextual readout contains no old panel fields or buttons");
            var label = Get<TMP_Text>(precision.UI, "label");
            check(label.text == "Move by (X,Y,Z): 0 m", "Readout: one line contains tool, axes, value and units");
            check(uiRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length == 0, "Readout: no white background box exists");
            var rect = (RectTransform)uiRoot.transform;
            check(rect.anchorMin == Vector2.one && rect.anchorMax == Vector2.one && rect.pivot == Vector2.one,
                "Readout: anchored in top-right corner");
            check(rect.anchoredPosition.y <= -150 && label.fontSize >= 32 && label.outlineWidth >= 0.29f && label.outlineColor == Color.black,
                "Readout: larger bold text sits lower with a prominent black outline pos=" + rect.anchoredPosition + " size=" + label.fontSize + " outline=" + label.outlineWidth + " color=" + label.outlineColor + " shader=" + label.fontSharedMaterial.shader.name + " materialColor=" + label.fontMaterial.GetColor("_OutlineColor"));
            Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
            check(label.textInfo.characterCount > 10 && label.textInfo.meshInfo.Any(mesh => mesh.vertexCount > 0),
                "Readout: native font generates visible text geometry without vertical clipping");
            precision.BeginHandle(true, 2, Vector3.forward, originalPosition + Vector3.right);
            Type("33.5"); precision.UI.Refresh(true, part, true); label.ForceMeshUpdate();
            check(label.text == "Rotate by (Z, local): 33.5\u00b0", "Readout: rotation uses requested compact degrees format");
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

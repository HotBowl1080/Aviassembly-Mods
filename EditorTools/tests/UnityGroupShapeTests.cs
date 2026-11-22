using System;
using System.Collections.Generic;
using System.Linq;
using Aviassembly.EditorTools;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class UnityGroupShapeTests
{
    private static void Put(object o, string f, object v) => AccessTools.Field(o.GetType(), f).SetValue(o, v);
    private static T Get<T>(object o, string f) => (T)AccessTools.Field(o.GetType(), f).GetValue(o);
    private static object Call(object o, string m, params object[] a) => AccessTools.Method(o.GetType(), m).Invoke(o, a);
    public static void Run(Plugin plugin, PartPlacer placer, PlaneContainer plane, Action<bool, string> check)
    {
        var old = placer.currentSelectedPart; var selection = plugin.Selection; var shape = plugin.ShapeEditor;
        var objects = new List<GameObject>();
        BuildingPart Part(string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(go); go.name = name; go.transform.SetParent(plane.transform); go.transform.position = position;
            var part = go.AddComponent<BuildingPart>(); part.hasBeenPlaced = true; part.snapPoints = new Transform[0]; part.isBasePart = true;
            go.AddComponent<PrecisionFixturePart>(); placer.partContainer.AddPart(part); return part;
        }
        void ResetInput()
        {
            plugin.Precision.Cancel(); shape.Cancel();
            Put(plugin.Precision, "consumedFrame", -1); Call(plugin.Precision, "ReleaseBlocker");
            Put(shape, "consumed", -1); Call(shape, "ReleaseBlocker"); Put(selection, "consumed", -1);
            InputSystem.QueueStateEvent(Mouse.current, new MouseState());
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); InputSystem.Update();
        }
        var beforeMoney = MoneyManager.Instance.money;
        var modeData = GameManager.Instance.gameModeData;
        try
        {
            ResetInput(); selection.Observe(placer);
            var a = Part("Group A", new Vector3(30, 0, 0)); var b = Part("Group B", new Vector3(34, 0, 0)); var child = Part("Group child", new Vector3(30, 1, 0));
            child.SetParent(a.gameObject);
            selection.Set(new[] { a }); selection.Toggle(b);
            check(selection.Count == 2 && placer.currentSelectedPart == b && placer.currentMovingPart == null, "Group: Shift-toggle semantics add active part without picking it up");
            selection.Toggle(b); check(selection.Count == 1 && placer.currentSelectedPart == a, "Group: toggling selected part removes it");
            plugin.ToggleFocus();
            var focusOrigin = Get<Vector3>(plugin, "orbitOrigin");
            selection.Toggle(b); Call(plugin, "SyncSelection");
            check(Get<Vector3>(plugin, "orbitOrigin") == focusOrigin, "Group focus: adding a part leaves camera pivot unchanged");
            selection.Set(new[] { a, child, b }); Call(plugin, "SyncSelection");
            check(Get<Vector3>(plugin, "orbitOrigin") == focusOrigin, "Group focus: replacing a group leaves camera pivot unchanged");
            plugin.ToggleFocus(); plugin.ToggleFocus();
            check(Vector3.Distance(Get<Vector3>(plugin, "orbitOrigin"), selection.Pivot) < 0.0001f, "Group focus: explicitly enabling focus targets selection center");
            selection.Refresh();
            var countLabel = Get<TMPro.TMP_Text>(selection, "label");
            check(countLabel.text == "3 selected" && countLabel.rectTransform.anchorMax == Vector2.one,
                "Group: a short count appears on the right side clear of the parts palette");
            plugin.ToggleFocus();
            selection.Set(new[] { a, child, b });
            var a0 = a.transform.position; var b0 = b.transform.position; var c0 = child.transform.position;
            var savedOffset = a.rotationOffset = Quaternion.Euler(12, 23, 34);
            var baseline = PlaneStorage.Instance.SaveToStream().ToArray();
            var s = plugin.Precision.Session;
            check(s.Begin(placer) && s.IsGroup, "Group: actual transform session captures multiple selected parts");
            var pivot = s.StartPosition; var startRotation = s.StartRotation;
            s.Preview(pivot + Vector3.right * 0.35f, startRotation);
            check(Quaternion.Angle(a.rotationOffset, savedOffset) < 0.02f, "Audit: translating a group preserves each part's saved rotation offset");
            check(Vector3.Distance(a.transform.position, a0 + Vector3.right * 0.35f) < 0.0001f && Vector3.Distance(b.transform.position, b0 + Vector3.right * 0.35f) < 0.0001f,
                "Group: exact movement applies once to every selected root");
            check(Vector3.Distance(child.transform.position, c0 + Vector3.right * 0.35f) < 0.0001f, "Group: selected parent and child do not double-transform");
            check(baseline.SequenceEqual(PlaneStorage.Instance.SaveToStream().ToArray()), "Group: saves exclude all preview poses");
            var delta = Quaternion.AngleAxis(90, Vector3.up); s.Preview(pivot, delta * startRotation);
            check(Vector3.Distance(a.transform.position, pivot + delta * (a0 - pivot)) < 0.0001f && Vector3.Distance(b.transform.position, pivot + delta * (b0 - pivot)) < 0.0001f,
                "Group: rotation uses shared median pivot");
            s.Cancel(); check(Vector3.Distance(a.transform.position, a0) < 0.0001f && Vector3.Distance(child.transform.position, c0) < 0.0001f && child.parent == a,
                "Group: cancel restores all parts and attachment graph");
            b.lockRotation = true;
            selection.Set(new[] { a, b });
            var pairPivot = selection.Pivot;
            var lockedRotation = b.transform.rotation;
            bool began = plugin.Precision.BeginHandle(true, 1, Vector3.up, pairPivot + Vector3.right);
            check(began, "Audit: a locked active part does not block rotation of an unlocked group member");
            if (began)
            {
                foreach (char c in "30") plugin.Precision.Type(c);
                plugin.Precision.UpdateMouse(); plugin.Precision.Apply();
                Put(plugin.Precision, "consumedFrame", -1); Call(plugin.Precision, "ReleaseBlocker");
                check(plugin.Precision.BeginRecentAdjustment(), "Audit: grouped ring accepts a replacement angle after release");
                foreach (char c in "45") plugin.Precision.Type(c);
                plugin.Precision.UpdateMouse();
                check(Vector3.Distance(a.transform.position, pairPivot + Quaternion.AngleAxis(45, Vector3.up) * (a0 - pairPivot)) < 0.001f &&
                    Quaternion.Angle(b.transform.rotation, lockedRotation) < 0.02f,
                    "Audit: replacing a grouped angle preserves the locked orientation and uses the total angle");
                ResetInput();
                a.SetPositionAndRotation(a0, Quaternion.identity); a.rotationOffset = savedOffset;
                b.SetPositionAndRotation(b0, lockedRotation); child.SetParent(a.gameObject); child.SetPosition(c0);
            }
            b.lockRotation = false;
            selection.Set(new[] { a, b }); s.Begin(placer); s.Preview(s.StartPosition + Vector3.up, s.StartRotation); s.Commit();
            check(Vector3.Distance(a.transform.position, a0 + Vector3.up) < 0.0001f && Vector3.Distance(b.transform.position, b0 + Vector3.up) < 0.0001f, "Group: commit preserves whole group movement");
            selection.Set(new[] { a });
            plugin.HideParts.Observe(placer, false); plugin.HideParts.HideSelected();
            selection.Set(new[] { a, b }); check(selection.Count == 1 && selection.Parts[0] == b, "Group: hidden parts excluded from selection");
            plugin.HideParts.ShowAll();
            var camera = BuildingCamera.cam; var cameraPosition = camera.transform.position; var cameraRotation = camera.transform.rotation;
            camera.transform.SetPositionAndRotation(new Vector3(32, 0, -20), Quaternion.identity);
            {
                ResetInput(); selection.Set(new[] { a });
                var precision = plugin.Precision;
                var from = a.transform.TransformPoint(new Vector3(0.5f, -0.5f, -0.5f));
                var to = b.transform.TransformPoint(new Vector3(0.12f, 0.5f, -0.5f));
                var initial = a.transform.position;
                var magnet = UnityEngine.Object.FindObjectsByType<SnappingButton>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .First(m => m.buttonImage != null && m.GetComponentInParent<Canvas>()?.GetComponentInChildren<MirrorButton>(true) != null);
                void Choose(string label)
                {
                    Put(plugin, "snapMenuClosedFrame", -1);
                    magnet.Press();
                    var menu = Get<SnapReferenceMenu>(plugin, "snapMenu");
                    check(menu != null && plugin.BlockPanelClick, "Magnet: opens options on the stock button and blocks clicks through the popup");
                    var option = Get<GameObject>(menu, "root").GetComponentsInChildren<UnityEngine.UI.Button>().First(v => v.name == "Snap " + label);
                    option.onClick.Invoke();
                    check(Get<SnapReferenceMenu>(plugin, "snapMenu") == null, "Magnet: selecting " + label + " closes the popup");
                    Put(plugin, "snapMenuClosedFrame", -1);
                }
                Choose("Edge");
                var stockMeshes = Resources.FindObjectsOfTypeAll<BuildingPart>().SelectMany(p => p.GetComponentsInChildren<MeshFilter>(true))
                    .Select(f => f.sharedMesh).Where(m => m != null).Distinct().ToArray();
                check(stockMeshes.Any(m => m.isReadable), "Snap: loaded aircraft assets have " + stockMeshes.Count(m => m.isReadable) +
                    " readable meshes out of " + stockMeshes.Length);
                void Pointer(Vector2 pixel, bool held, params Key[] keys)
                {
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = pixel, buttons = (ushort)(held ? 1 : 0) });
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys)); InputSystem.Update();
                }
                var grab = (Vector2)camera.WorldToScreenPoint(from);
                var dest = (Vector2)camera.WorldToScreenPoint(to);
                var outside = dest + new Vector2(80, 60);
                var initialB = b.transform.position;
                b.SetPosition(initialB + Vector3.down * 3);
                to += Vector3.down * 3;
                dest = camera.WorldToScreenPoint(to);
                void Arrow()
                {
                    Pointer(grab, true);
                    check(precision.BeginHandle(false, 1, Vector3.up, from), "Snap: vertical arrow starts a mouse drag");
                }
                Arrow();
                Pointer(dest, true); precision.ProcessInput();
                var expected = initial + Vector3.up * (to.y - from.y);
                check(precision.ReadoutLine.Contains("Snapped") && Vector3.Distance(a.transform.position, expected) < 0.001f,
                    "Snap: dragging over the other part's upper edge aligns the underside on Y with X and Z preserved");
                Pointer(outside, true); precision.ProcessInput();
                check(!precision.ReadoutLine.Contains("Snapped"), "Snap: leaving the edge immediately restores ordinary arrow dragging");
                Pointer(dest, true); precision.ProcessInput();
                Pointer(dest, false); precision.ProcessInput();
                check(!precision.Session.Active && Vector3.Distance(a.transform.position, expected) < 0.001f,
                    "Snap: releasing over an edge commits without a source click or shortcut");
                a.SetPosition(initial); ResetInput();
                Pointer(grab, false); precision.Begin(false, true); precision.Constrain(1, false);
                Pointer(dest, false, Key.V); precision.ProcessInput();
                check(precision.ReadoutLine.Contains("Snapped") && Vector3.Distance(a.transform.position, expected) < 0.001f,
                    "Snap: V does not enter a separate picking mode");
                precision.Type('2'); precision.UpdateMouse();
                check(!precision.ReadoutLine.Contains("Snapped") && Vector3.Distance(a.transform.position, initial + Vector3.up * 2) < 0.001f,
                    "Snap: numeric movement wins over the hovered geometry");
                ResetInput();
                check(a.transform.position == initial, "Snap: cancel restores the initial pose");
                Choose("Center");
                var filter = a.GetComponent<MeshFilter>();
                var originalMesh = filter.sharedMesh;
                var offsetMesh = UnityEngine.Object.Instantiate(originalMesh);
                try
                {
                    offsetMesh.vertices = offsetMesh.vertices.Select(v => v + Vector3.up * 0.3f).ToArray();
                    offsetMesh.RecalculateBounds(); filter.sharedMesh = offsetMesh;
                    Arrow(); Pointer(dest, true); precision.ProcessInput();
                    check(precision.ReadoutLine.Contains("Snapped") && Mathf.Abs(a.transform.position.y + 0.3f - to.y) < 0.001f,
                        "Magnet: Center aligns an offset mesh's visible midpoint to the target height");
                    ResetInput(); Pointer(grab, false); precision.Begin(false, true);
                    Pointer(dest, false); precision.ProcessInput();
                    check(precision.ReadoutLine.Contains("Snapped") && Vector3.Distance(a.transform.TransformPoint(Vector3.up * 0.3f), to) < 0.001f,
                        "Magnet: unconstrained move aligns all three coordinates of the visible center");
                    ResetInput(); Choose("Edge"); Arrow(); Pointer(dest, true); precision.ProcessInput();
                    check(precision.ReadoutLine.Contains("Snapped") && Mathf.Abs(a.transform.position.y - 0.2f - to.y) < 0.001f,
                        "Magnet: Edge aligns the underside of the same offset mesh");
                    ResetInput(); Choose("Off"); Arrow(); Pointer(dest, true); precision.ProcessInput();
                    check(!PartPlacer.snapMode && !precision.ReadoutLine.Contains("Snapped"), "Magnet: Off disables native and move snapping");
                    ResetInput(); Choose("Edge");
                    magnet.Press();
                    var overlay = Get<GameObject>(Get<SnapReferenceMenu>(plugin, "snapMenu"), "root");
                    overlay.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    check(PartPlacer.snapMode && plugin.Reference == SnapReference.Edge && !overlay.activeSelf &&
                        InputManager.Instance.currentInputBlocker == null, "Magnet: clicking outside preserves the setting and releases editor input");
                    Put(plugin, "snapMenuClosedFrame", -1);
                }
                finally { ResetInput(); filter.sharedMesh = originalMesh; UnityEngine.Object.DestroyImmediate(offsetMesh); }
                selection.Set(new[] { b }); plugin.HideParts.HideSelected(); selection.Set(new[] { a });
                Arrow(); Pointer(dest, true); precision.ProcessInput();
                check(!precision.ReadoutLine.Contains("Snapped"), "Snap: hidden parts cannot attract the move arrow");
                ResetInput(); plugin.HideParts.ShowAll();
                b.SetPosition(initialB);
            }
            var screen = camera.WorldToScreenPoint(b.transform.position);
            ResetInput(); selection.Set(new[] { a, b }); plugin.ToggleFocus();
            var originalMask = placer.selectionMask; placer.selectionMask = ~0;
            Physics.SyncTransforms();
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = screen, buttons = 1 }); InputSystem.Update();
            Put(selection, "inputFrame", -1); selection.ProcessInput(); Put(selection, "consumed", -1); Call(plugin, "SyncSelection");
            check(selection.Count == 1 && placer.currentSelectedPart == b && Vector3.Distance(Get<Vector3>(plugin, "orbitOrigin"), b.transform.position) < 0.001f,
                "Group focus: ordinary click exits group and centers clicked part");
            var tint = Get<HiddenXray>(selection, "highlight");
            check(tint.DrawCount == 0, "Group: ordinary selection uses the native single-part highlight");
            ResetInput();
            selection.Clear();
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.LeftShift));
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = screen, buttons = 1 });
            InputSystem.Update();
            Put(selection, "inputFrame", -1); selection.ProcessInput();
            check(selection.Count == 1 && placer.currentSelectedPart == b && tint.DrawCount > 0,
                "Group: the first Shift-click draws the selection highlight immediately");
            placer.selectionMask = originalMask; ResetInput(); plugin.ToggleFocus();
            selection.SelectBox((Vector2)screen - Vector2.one * 12, (Vector2)screen + Vector2.one * 12, false, false);
            check(selection.Count == 1 && selection.Parts[0] == b, "Group: box selects projected origins inside rectangle");
            selection.SelectBox((Vector2)screen - Vector2.one * 12, (Vector2)screen + Vector2.one * 12, false, true);
            check(selection.Count == 0, "Group: Ctrl-box subtracts from selection");
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            ResetInput(); InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.A)); InputSystem.Update();
            Put(selection, "inputFrame", -1); selection.ProcessInput();
            check(selection.Parts.Contains(a) && selection.Parts.Contains(b) && selection.Parts.Contains(child), "Group: A input selects visible placed parts");
            ResetInput(); InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.LeftAlt, Key.A)); InputSystem.Update();
            Put(selection, "inputFrame", -1); selection.ProcessInput();
            check(selection.Count == 0 && placer.currentSelectedPart == null, "Group: Alt+A input clears selection");
            ResetInput(); InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.B)); InputSystem.Update();
            Put(selection, "inputFrame", -1); selection.ProcessInput();
            check(selection.BlockInput, "Group: B starts exclusive box selection");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Escape)); InputSystem.Update();
            Put(selection, "inputFrame", -1); selection.ProcessInput(); ResetInput();
            check(!Get<bool>(selection, "box"), "Group: Escape cancels box selection");
            selection.Set(new[] { a, b }); plugin.HideParts.HideSelected();
            check(plugin.HideParts.IsHidden(a) && plugin.HideParts.IsHidden(b) && !plugin.HideParts.IsHidden(child), "Group: Hide button hides selected parts but not unselected children");
            plugin.HideParts.ShowAll(); ResetInput();
            selection.Set(new[] { a, b });
            var groupStartA = a.transform.position; var groupStartB = b.transform.position;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.G)); InputSystem.Update(); plugin.Precision.ProcessInput();
            check(s.Active && s.IsGroup, "Group: G input starts group transform");
            plugin.Precision.Constrain(0, false); plugin.Precision.Type('0'); plugin.Precision.Type('.'); plugin.Precision.Type('3'); plugin.Precision.Type('5');
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); InputSystem.Update(); plugin.Precision.ProcessInput();
            check(Vector3.Distance(a.transform.position, groupStartA + Vector3.right * 0.35f) < 0.0001f && Vector3.Distance(b.transform.position, groupStartB + Vector3.right * 0.35f) < 0.0001f,
                "Group: G X typed offset routes to all selected roots");
            ResetInput();
            selection.Set(new[] { a, b });
            check(!shape.Begin(ShapeField.Scale, 0, false, placer) && shape.Readout.Contains("editable fuselage"), "Shape: unsupported parts rejected instead of applying unsaved transform scale");
            shape.ClearHint(); selection.Clear();

            var template = Resources.FindObjectsOfTypeAll<ProceduralFuselage>().FirstOrDefault(f => f.GetComponent<BuildingPart>() != null && f.GetComponent<ProceduralFuselageMesh>() != null);
            check(template != null, "Shape: installed game exposes native procedural fuselage asset");
            if (template == null) return;
            ProceduralFuselage Fuselage(string name, Vector3 pos)
            {
                var go = UnityEngine.Object.Instantiate(template.gameObject); objects.Add(go); go.name = name; go.transform.SetParent(plane.transform); go.transform.SetPositionAndRotation(pos, Quaternion.identity); go.transform.localScale = Vector3.one; go.SetActive(true);
                var f = go.GetComponent<ProceduralFuselage>(); var part = go.GetComponent<BuildingPart>(); var mesh = go.GetComponent<ProceduralFuselageMesh>(); var native = go.GetComponent<Fuselage>();
                part.hasBeenPlaced = false; part.children.Clear(); part.decals.Clear(); part.price = 100;
                Put(f, "buildingPart", part); Put(f, "fuselage", native); Put(f, "mesh", mesh);
                mesh.Init();
                var side = new ProceduralFuselageSide { radius = Vector2.one * 0.5f, lengthOffset = new Vector3(0, 0, 0.5f), roundness = Vector4.one * 0.25f, hallowness = 0 };
                var settings = new ProceduralFuselageTransform { side1 = side, side2 = side };
                var data = new FuselageData(1, 1, 100, mesh.GetMeshVolume(settings)); Put(f, "baseData", data); Put(f, "currentData", data);
                f.appliedTransform = settings; f.UpdateChildPosition(); f.ApplyTransformation(settings, true);
                part.hasBeenPlaced = true; part.isBasePart = true; placer.partContainer.AddPart(part); return f;
            }
            GameManager.Instance.gameModeData = new GameModeData(); GameManager.Instance.gameModeData.creativeMode = false;
            MoneyManager.Instance.money = 100000;
            var fa = Fuselage("Shape A", new Vector3(50, 0, 0)); var fb = Fuselage("Shape B", new Vector3(55, 0, 0));
            var pa = fa.GetComponent<BuildingPart>(); var pb = fb.GetComponent<BuildingPart>(); selection.Set(new[] { pa, pb }); ResetInput();
            var anchor = pa.transform.position;
            var initialChild = pb.transform.position;
            pb.SetParent(pa.gameObject);
            selection.Set(new[] { pb, pa });
            var scalePivot = selection.Pivot;
            shape.Begin(ShapeField.Scale, 0, false, placer); shape.Preview(1.5f);
            var expectedChild = scalePivot + (initialChild - scalePivot) * 1.5f;
            check(Vector3.Distance(pb.transform.position, expectedChild) < 0.001f,
                "Audit: scaling child-first selection applies spacing exactly once");
            check(Vector3.Distance(pa.transform.position, scalePivot + (anchor - scalePivot) * 1.5f) < 0.001f,
                "Audit: scaling uses the same group center for the parent");
            ResetInput();
            check(Vector3.Distance(pb.transform.position, initialChild) < 0.001f && pb.parent == pa,
                "Audit: cancelling child-first scale restores placement and attachment");
            pb.SetParent(null); fa.UpdateChildPosition();
            var mirrorShape = Fuselage(pa.name, placer.symmetryPlane.GetMirroredPosition(anchor));
            var mirrored = mirrorShape.GetComponent<BuildingPart>();
            mirrored.transform.rotation = placer.symmetryPlane.GetMirroredRotation(pa.transform);
            var mirrorStart = mirrored.transform.position;
            bool oldMirror = PartPlacer.mirrorMode;
            PartPlacer.mirrorMode = true;
            selection.Set(new[] { pa });
            check(placer.GetMirrorPart(pa) == mirrored, "Audit: native lookup identifies the matching procedural mirror");
            shape.Begin(ShapeField.Scale, 0, false, placer); shape.Preview(1.5f);
            check(Vector3.Distance(mirrored.transform.position, mirrorStart) < 0.001f &&
                Mathf.Abs(mirrorShape.CurrentTransform.side1.radius.x - 0.75f) < 0.0001f,
                "Audit: scaling one fuselage resizes its automatic mirror in place");
            ResetInput(); PartPlacer.mirrorMode = oldMirror;
            selection.Set(new[] { pa, pb });
            var originalManager = placer.procduralPartController;
            var fixture = new GameObject("Shape handle fixture"); objects.Add(fixture);
            var manager = fixture.AddComponent<ProceduralPartManager>(); manager.gizmoManager = fixture.AddComponent<ProceduralGizmoManager>();
            Put(manager, "selectedPart", pb);
            manager.gizmoManager.gizmo1 = fixture.AddComponent<ProceduralGizmo>();
            var handleObject = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(handleObject);
            handleObject.transform.position = new Vector3(52, 0, 0);
            manager.gizmoManager.gizmo1.xScaleGizmo = handleObject.AddComponent<ProceduralAxisScaleGizmo>();
            var savedCameraPosition = camera.transform.position; var savedCameraRotation = camera.transform.rotation;
            camera.transform.SetPositionAndRotation(new Vector3(52, 0, -20), Quaternion.identity);
            placer.procduralPartController = manager;
            try
            {
                Physics.SyncTransforms(); var hitPoint = camera.WorldToScreenPoint(handleObject.transform.position);
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = hitPoint, buttons = 1 }); InputSystem.Update();
                shape.ProcessInput();
                check(shape.Active && shape.Readout.StartsWith("Width"), "Shape: native width handle collider starts precision input");
                shape.Type('1'); shape.Type('.'); shape.Type('2'); shape.Type('3'); shape.Type('7'); shape.ProcessInput();
                check(Mathf.Abs(fa.CurrentTransform.side1.radius.x * 2 - 1.237f) < 0.00001f, "Shape: typed native handle value updates selected meshes");
            }
            finally
            {
                placer.procduralPartController = originalManager; ResetInput();
                camera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation); handleObject.SetActive(false);
            }
            var settingsA = fa.AppliedTransform; var settingsB = fb.AppliedTransform;
            var dragCameraPosition = camera.transform.position; var dragCameraRotation = camera.transform.rotation;
            bool focusBeforeLimit = plugin.IsFocused;
            foreach (bool focused in new[] { false, true })
            {
                ResetInput();
                if (plugin.IsFocused != focused) plugin.ToggleFocus();
                camera.transform.SetPositionAndRotation(new Vector3(52, 0, -20), Quaternion.identity);
                var cameraPositionAtStart = camera.transform.position;
                Vector2 start = camera.WorldToScreenPoint(pa.transform.position);
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = start, buttons = 1 }); InputSystem.Update();
                shape.Begin(ShapeField.Width, 0, true, placer);
                shape.SetDragAxis(camera, pa.transform.position, Vector3.right, 2);
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = start - Vector2.right * 2000, scroll = Vector2.up * 120, buttons = 1 });
                InputSystem.Update();
                shape.ProcessInput();
                placer.buildingCamera.UpdateOrigin(true);
                placer.buildingCamera.Update();
                plugin.UpdateFollowingCamera();
                check(shape.Valid && Mathf.Abs(fa.CurrentTransform.side1.radius.x * 2 - Get<float>(shape, "dragMinimum")) < 0.0001f,
                    "Shape: dragging beyond the minimum holds the native width limit");
                check(camera.transform.position == cameraPositionAtStart && camera.transform.rotation == Quaternion.identity && plugin.IsFocused == focused,
                    "Shape: minimum width keeps the camera fixed in " + (focused ? "Focused" : "Unfocused") + " mode");
            }
            ResetInput();
            if (plugin.IsFocused != focusBeforeLimit) plugin.ToggleFocus();
            foreach (float roll in new[] { 0f, 45f })
            {
                ResetInput(); camera.transform.SetPositionAndRotation(new Vector3(52, 0, -20), Quaternion.Euler(0, 0, roll));
                Vector2 start = camera.WorldToScreenPoint(pa.transform.position);
                Vector2 projected = (Vector2)camera.WorldToScreenPoint(pa.transform.position + new Vector3(1, 1, 0).normalized) - start;
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = start }); InputSystem.Update();
                shape.Begin(ShapeField.Width, 0, false, placer);
                shape.SetDragAxis(camera, pa.transform.position, new Vector3(1, 1, 0), 2);
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = start + projected * 0.1f }); InputSystem.Update(); shape.ProcessInput();
                check(Mathf.Abs(fa.CurrentTransform.side1.radius.x * 2 - 1.2f) < 0.001f, "Shape drag: diagonal handle follows screen projection at camera roll " + roll);
                plugin.ToggleFocus(); plugin.UpdateFollowingCamera(); shape.ProcessInput();
                check(shape.Active && Mathf.Abs(fa.CurrentTransform.side1.radius.x * 2 - 1.2f) < 0.001f, "Shape drag: changing focus with stationary mouse preserves size at camera roll " + roll);
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = start + new Vector2(-projected.y, projected.x) * 0.1f }); InputSystem.Update(); shape.ProcessInput();
                check(Mathf.Abs(fa.CurrentTransform.side1.radius.x * 2 - 1f) < 0.001f, "Shape drag: perpendicular motion leaves value unchanged at camera roll " + roll);
                ResetInput();
            }
            camera.transform.SetPositionAndRotation(new Vector3(52, 0, -20), Quaternion.identity);
            shape.Begin(ShapeField.Width, 0, false, placer); shape.SetDragAxis(camera, new Vector3(52, 0, 0), Vector3.forward, 2); shape.ProcessInput();
            check(shape.Readout.Contains("Drag up/down"), "Shape drag: end-on handle explains vertical fallback"); ResetInput();
            shape.Begin(ShapeField.Width, 0, false, placer); foreach (char c in "1.237") shape.Type(c); shape.ProcessInput();
            var previewBeforeFocus = fa.CurrentTransform;
            bool wasFocused = plugin.IsFocused;
            plugin.ToggleFocus();
            check(plugin.IsFocused != wasFocused && shape.Active && fa.CurrentTransform.Equals(previewBeforeFocus, false), "Shape focus: toggle works without changing or committing numeric preview");
            Call(plugin, "LateUpdate");
            var focusUI = Get<object>(plugin, "focusUI");
            var focusButton = Get<UnityEngine.UI.Button>(focusUI, "button");
            check(focusButton.IsInteractable(), "Shape focus: camera button stays enabled during shape edit");
            var focusRect = (RectTransform)focusButton.transform;
            var focusCanvas = focusButton.GetComponentInParent<Canvas>(); Canvas.ForceUpdateCanvases();
            Vector2 focusPoint = RectTransformUtility.WorldToScreenPoint(focusCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : focusCanvas.worldCamera, focusRect.TransformPoint(focusRect.rect.center));
            Put(shape, "beginFrame", Time.frameCount - 1);
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = focusPoint, buttons = 1 }); InputSystem.Update(); shape.ProcessInput();
            check(plugin.PointerOverPanel() && shape.Active && fa.AppliedTransform.Equals(settingsA, false), "Shape focus: pressing camera button does not confirm edit");
            focusButton.onClick.Invoke();
            check(plugin.IsFocused == wasFocused && shape.Active && fa.CurrentTransform.Equals(previewBeforeFocus, false), "Shape focus: button returns camera mode and preserves preview");
            check(shape.Readout.Contains(" = 1.237 m"), "Shape readout: dimensions explicitly show final value");
            ResetInput(); camera.transform.SetPositionAndRotation(dragCameraPosition, dragCameraRotation);
            baseline = PlaneStorage.Instance.SaveToStream().ToArray(); float money = MoneyManager.Instance.money;
            check(shape.Begin(ShapeField.Width, 0, false, placer) && shape.Preview(1.237f), "Shape: exact width starts and previews without native snapping");
            check(Mathf.Abs(fa.CurrentTransform.side1.radius.x * 2 - 1.237f) < 0.00001f && Mathf.Abs(fb.CurrentTransform.side1.radius.x * 2 - 1.237f) < 0.00001f,
                "Shape: typed width reaches both selected native procedural meshes");
            check(fa.AppliedTransform.Equals(settingsA, false) && MoneyManager.Instance.money == money, "Shape: preview leaves committed settings and money unchanged");
            check(baseline.SequenceEqual(PlaneStorage.Instance.SaveToStream().ToArray()), "Shape: native save bytes exclude geometry and price previews");
            check(Mathf.Abs(fa.CurrentTransform.side1.radius.x * 2 - 1.237f) < 0.00001f, "Shape: geometry preview resumes after save");
            shape.Cancel(); ResetInput();
            check(fa.CurrentTransform.Equals(settingsA, false) && fb.CurrentTransform.Equals(settingsB, false) && baseline.SequenceEqual(PlaneStorage.Instance.SaveToStream().ToArray()), "Shape: cancel restores both meshes and exact native saves");
            shape.Begin(ShapeField.Corner2, 1, false, placer); shape.Preview(33.5f);
            check(Mathf.Abs(fa.CurrentTransform.side2.roundness.z - 0.335f) < 0.00001f && fa.CurrentTransform.side2.roundness.x == settingsA.side2.roundness.x,
                "Shape: exact corner roundness changes only chosen end and corner");
            check(!shape.Preview(101) && !shape.Valid && shape.Readout.Contains("0 to 100%"), "Shape: typed roundness over the limit shows the valid range");
            check(baseline.SequenceEqual(PlaneStorage.Instance.SaveToStream().ToArray()) && !shape.Valid &&
                Mathf.Abs(fa.CurrentTransform.side2.roundness.z - 0.335f) < 0.00001f,
                "Shape: saving during invalid input restores the last valid preview and keeps it uncommitted");
            shape.Preview(33.5f); check(shape.Apply(), "Shape: native geometry commit succeeds for multiple parts"); ResetInput();
            check(Mathf.Abs(fa.AppliedTransform.side2.roundness.z - 0.335f) < 0.00001f && Mathf.Abs(fb.AppliedTransform.side2.roundness.z - 0.335f) < 0.00001f,
                "Shape: committed corners persist in native applied state");
            var idleGizmoObject = new GameObject("Shape idle gizmo fixture"); objects.Add(idleGizmoObject);
            var idleGizmo = idleGizmoObject.AddComponent<ProceduralGizmo>();
            var idleResult = idleGizmo.GetUpdatedFuselageSide(fa.AppliedTransform.side2, fa.AppliedTransform.side1);
            check(idleResult.roundness == fa.AppliedTransform.side2.roundness && idleResult.radius == fa.AppliedTransform.side2.radius,
                "Shape: native idle gizmo update preserves exact dimensions and corner values");
            shape.Begin(ShapeField.Hollowness, 0, false, placer); shape.Preview(42.5f);
            check(Mathf.Abs(fa.CurrentTransform.side1.hallowness - 0.425f) < 0.00001f, "Shape: hollowness supports exact percentages"); shape.Cancel(); ResetInput();
            var scaleBefore = fa.AppliedTransform; shape.Begin(ShapeField.Scale, 0, false, placer); shape.Preview(1.25f);
            check(shape.Readout.Contains("\u00d7 1.25"), "Shape readout: scale explicitly shows multiplier");
            check(Mathf.Abs(fa.CurrentTransform.side1.radius.x - scaleBefore.side1.radius.x * 1.25f) < 0.00001f && pa.transform.localScale == Vector3.one,
                "Shape: S scaling edits saved procedural geometry rather than Transform.localScale");
            shape.Cancel(); ResetInput();
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.S)); InputSystem.Update(); shape.ProcessInput();
            check(shape.Active, "Shape: S input starts native geometry scaling");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); InputSystem.Update();
            shape.Type('1'); shape.Type('.'); shape.Type('2'); shape.Type('5'); shape.ProcessInput();
            check(Mathf.Abs(fa.CurrentTransform.side1.radius.x - scaleBefore.side1.radius.x * 1.25f) < 0.00001f, "Shape: numeric input routes into active scale preview");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Escape)); InputSystem.Update(); shape.ProcessInput();
            check(!shape.Active && fa.AppliedTransform.Equals(scaleBefore, false), "Shape: Escape input cancels typed scaling"); ResetInput();
            MoneyManager.Instance.money = 0; shape.Begin(ShapeField.Width, 0, false, placer); shape.Preview(2.5f);
            check(!shape.Apply() && shape.Active && shape.Readout.Contains("not enough money"), "Shape: unaffordable edits stay uncommitted");
            shape.Cancel(); ResetInput();
            check(MoneyManager.Instance.money == 0, "Shape: cancelled unaffordable edit does not charge money");
            selection.Set(new[] { pa });
            shape.Begin(ShapeField.Width, 0, false, placer); shape.Preview(1.1f);
            Call(placer, "set_currentSelectedPart", pb);
            check(!shape.Active, "Shape: changing selection cancels active geometry preview"); ResetInput();

            var sample = Fuselage("Native shape handle sample", new Vector3(65, 0, 0));
            var samplePart = sample.GetComponent<BuildingPart>();
            var sampleSettings = sample.AppliedTransform;
            sampleSettings.side1.roundness = new Vector4(0.2f, 0.4f, 0.6f, 0.8f);
            sampleSettings.side2.radius.x = 0.02f;
            sample.ApplyTransformation(sampleSettings, true);
            sampleSettings = sample.AppliedTransform;
            selection.Set(new[] { samplePart });
            MoneyManager.Instance.money = 100000;
            check(shape.Begin(ShapeField.Roundness, 0, false, placer) && shape.Preview(75) &&
                sample.CurrentTransform.side1.roundness == Vector4.one * 0.75f && sample.CurrentTransform.side2.radius.x == 0.02f,
                "Shape: roundness can edit a saved part with a narrow opposite end without changing that end");
            ResetInput();

            var native = Resources.FindObjectsOfTypeAll<ProceduralGizmoManager>().FirstOrDefault(g => g.gizmo1?.roundnessGizmo != null && g.gizmo2 != null);
            check(native != null, "Shape: loaded scene supplies the native end handles");
            if (native == null) return;
            var nativeFixture = new GameObject("Native end gizmo fixture"); objects.Add(nativeFixture);
            var nativeManager = nativeFixture.AddComponent<ProceduralPartManager>();
            var nativeGizmos = nativeFixture.AddComponent<ProceduralGizmoManager>();
            nativeManager.partPlacer = nativeGizmos.partPlacer = placer;
            nativeManager.gizmoManager = nativeGizmos;
            nativeGizmos.gizmo1 = UnityEngine.Object.Instantiate(native.gizmo1, nativeFixture.transform);
            nativeGizmos.gizmo2 = UnityEngine.Object.Instantiate(native.gizmo2, nativeFixture.transform);
            var end = nativeGizmos.gizmo1;
            end.SetGizmoManager(nativeGizmos);
            nativeGizmos.gizmo2.SetGizmoManager(nativeGizmos);
            Put(nativeManager, "selectedPart", samplePart);
            placer.procduralPartController = nativeManager;
            try
            {
                var front = sample.transform.forward;
                camera.transform.SetPositionAndRotation(sample.transform.position + front * 6, Quaternion.LookRotation(-front, sample.transform.up));
                nativeGizmos.SelectNewPart(sample);
                nativeGizmos.gizmo2.gameObject.SetActive(false);
                nativeGizmos.SetGizmoSelectionMode((int)GizmoMode.Roundness);
                nativeGizmos.UpdateGizmos(samplePart, null, sampleSettings);
                end.transform.localScale = end.targetScale;
                nativeGizmos.UpdateGizmos(samplePart, null, sampleSettings);
                Physics.SyncTransforms();
                var collider = end.roundnessGizmo.GetComponentInChildren<Collider>();
                check(collider != null, "Shape: the native roundness handle has a raycast collider");
                if (collider == null) return;
                Vector2 click = camera.WorldToScreenPoint(collider.bounds.center);
                Vector2 center = camera.WorldToScreenPoint(sampleSettings.GetBaseOrigin1(sample.transform));
                Vector2 radial = click - center;
                Vector2 inward = radial.sqrMagnitude < 16 ? Vector2.left : -radial.normalized;
                float referencePixel = Mathf.Max(480, camera.pixelHeight) / 1080f;
                float fullRange = 100f / 0.38f * referencePixel;
                void Pointer(Vector2 position, bool pressed, bool fine = false)
                {
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = position, buttons = pressed ? (ushort)1 : (ushort)0 });
                    InputSystem.QueueStateEvent(Keyboard.current, fine ? new KeyboardState(Key.LeftShift) : new KeyboardState());
                    InputSystem.Update(); shape.ProcessInput();
                }

                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = click, buttons = 1 }); InputSystem.Update();
                end.roundnessGizmo.OnHover();
                check(Get<bool>(end.roundnessGizmo, "selected"), "Shape: native hover can receive the press before the mod");
                shape.ProcessInput();
                check(shape.Active && shape.Readout.StartsWith("Roundness") && !Get<bool>(end.roundnessGizmo, "selected"),
                    "Shape: mod captures the stock roundness handle and releases the stock drag");
                Pointer(click, false);
                check(!shape.Active && sample.AppliedTransform.Equals(sampleSettings, false), "Shape: a click without a drag preserves mixed corners");
                ResetInput();
                Pointer(click, true);
                Pointer(click + inward * fullRange * 0.25f, true);
                check(Vector4.Distance(sample.CurrentTransform.side1.roundness, new Vector4(0.45f, 0.65f, 0.85f, 1f)) < 0.0001f,
                    "Shape: the native roundness drag moves all corners by a common delta and caps each corner");
                Pointer(click + inward * fullRange * 1.5f, true);
                check(shape.Valid && sample.CurrentTransform.side1.roundness == Vector4.one && shape.Readout.Contains("100%"),
                    "Shape: dragging past the roundness limit displays the capped preview");
                Pointer(click + inward * fullRange * 1.45f, true);
                check(Mathf.Abs(sample.CurrentTransform.side1.roundness.x - 0.95f) < 0.0001f,
                    "Shape: reversing an overshot roundness drag changes the preview immediately");
                Pointer(click + inward * fullRange * 1.5f, false);
                check(!shape.Active && sample.AppliedTransform.side1.roundness == Vector4.one,
                    "Shape: releasing an overshot drag commits and closes the edit");
                ResetInput();

                nativeGizmos.UpdateGizmos(samplePart, null, sample.AppliedTransform);
                Physics.SyncTransforms();
                var corner = end.corners[0].GetComponentInChildren<Collider>();
                Vector2 cornerClick = camera.WorldToScreenPoint(corner.bounds.center);
                Vector2 cornerOut = (cornerClick - center).normalized;
                Pointer(cornerClick, true);
                Pointer(cornerClick + cornerOut * (100 * referencePixel), true);
                check(shape.Active && Vector4.Distance(sample.CurrentTransform.side1.roundness, new Vector4(1, 0.62f, 1, 1)) < 0.001f,
                    "Shape: a 100 pixel individual corner drag at 1080p changes only that corner by 38 points");
                ResetInput();

                nativeGizmos.SetGizmoSelectionMode((int)GizmoMode.Hallowness);
                nativeGizmos.UpdateGizmos(samplePart, null, sample.AppliedTransform);
                Physics.SyncTransforms();
                Vector2 hollowClick = camera.WorldToScreenPoint(end.hallownesGizmo.GetComponentInChildren<Collider>().bounds.center);
                var hollowRadial = hollowClick - center;
                Vector2 outward = (hollowRadial.sqrMagnitude < 16 ? Vector2.right : hollowRadial.normalized) * referencePixel;
                Pointer(hollowClick, true);
                Pointer(hollowClick + outward * 100, true);
                check(shape.Active && Mathf.Abs(sample.CurrentTransform.side1.hallowness - 0.45f) < 0.0001f,
                    "Shape: a 100 pixel hollowness drag at 1080p changes 45 percentage points");
                Pointer(hollowClick + outward * 100, true, fine: true);
                check(Mathf.Abs(sample.CurrentTransform.side1.hallowness - 0.45f) < 0.0001f,
                    "Shape: engaging Shift during a drag leaves the preview where it is");
                Pointer(hollowClick + outward * 200, true, fine: true);
                check(Mathf.Abs(sample.CurrentTransform.side1.hallowness - 0.495f) < 0.0001f,
                    "Shape: Shift reduces subsequent drag motion to one tenth");
                Pointer(hollowClick + outward * 200, true);
                check(Mathf.Abs(sample.CurrentTransform.side1.hallowness - 0.495f) < 0.0001f,
                    "Shape: releasing Shift during a drag leaves the preview where it is");
                Pointer(hollowClick + outward * 600, false);
                check(!shape.Active && Mathf.Abs(sample.AppliedTransform.side1.hallowness - 0.9f) < 0.0001f,
                    "Shape: a fast hollowness drag commits at the 90 percent game limit");
                ResetInput();

                nativeGizmos.SetGizmoSelectionMode((int)GizmoMode.Move);
                nativeGizmos.UpdateGizmos(samplePart, null, sample.AppliedTransform);
                var handleStart = end.transform.position;
                float offset = sample.AppliedTransform.side1.lengthOffset.x;
                check(shape.Begin(ShapeField.OffsetX, 0, false, placer) && shape.Preview(offset + 0.3f) &&
                    Vector3.Distance(end.transform.position, handleStart + sample.transform.right * 0.3f) < 0.0001f,
                    "Shape: the actual end handles track an offset preview");
                ResetInput();
                check(Vector3.Distance(end.transform.position, handleStart) < 0.0001f, "Shape: cancelling puts the native handles back with the fuselage");

                var stockSnap = PartPlacer.snapMode;
                PartPlacer.snapMode = false;
                foreach (int side in new[] { 0, 1 })
                foreach (bool horizontal in new[] { true, false })
                foreach (float zoom in new[] { 3f, 8f })
                {
                    var activeEnd = side == 0 ? end : nativeGizmos.gizmo2;
                    end.gameObject.SetActive(side == 0);
                    nativeGizmos.gizmo2.gameObject.SetActive(side == 1);
                    var view = (front * (side == 0 ? 1 : -1) + sample.transform.right * 0.32f + sample.transform.up * 0.16f).normalized;
                    camera.transform.SetPositionAndRotation(sample.transform.position + view * zoom, Quaternion.LookRotation(-view));
                    nativeGizmos.UpdateGizmos(samplePart, null, sample.AppliedTransform);
                    activeEnd.transform.localScale = activeEnd.targetScale;
                    Physics.SyncTransforms();
                    var handle = (horizontal ? activeEnd.xOffset : activeEnd.yOffset).GetComponentInChildren<Collider>();
                    Vector2 start = camera.WorldToScreenPoint(handle.bounds.center);
                    var ray = camera.ScreenPointToRay(start);
                    if (!handle.Raycast(ray, out var hit, 100)) throw new Exception("Offset fixture missed the native handle");
                    var grab = activeEnd.transform.InverseTransformPoint(hit.point);
                    var direction = horizontal ? sample.transform.right : sample.transform.up * Mathf.Sign(sample.transform.localScale.y);
                    Pointer(start, true);
                    check(shape.Active && shape.Readout.StartsWith(horizontal ? "Offset X" : "Offset Y"),
                        "Shape: picks the native " + (horizontal ? "horizontal" : "vertical") + " arrow on end " + (side + 1));
                    foreach (float movement in new[] { 0.3f, 0.9f, -0.25f })
                    {
                        Vector2 mouse = camera.WorldToScreenPoint(hit.point + direction * movement);
                        Pointer(mouse, true);
                        Vector2 visible = camera.WorldToScreenPoint(activeEnd.transform.TransformPoint(grab));
                        check(Vector2.Distance(visible, mouse) < 0.35f,
                            "Shape: offset arrow tracks " + movement + " m on end " + (side + 1) + " at zoom " + zoom + ", horizontal=" + horizontal);
                    }
                    ResetInput();
                }
                PartPlacer.snapMode = true;
                end.gameObject.SetActive(true); nativeGizmos.gizmo2.gameObject.SetActive(false);
                camera.transform.SetPositionAndRotation(sample.transform.position + front * 6, Quaternion.LookRotation(-front, sample.transform.up));
                nativeGizmos.UpdateGizmos(samplePart, null, sample.AppliedTransform);
                end.transform.localScale = end.targetScale;
                var originalEnd = sample.AppliedTransform.GetBaseOrigin1(sample.transform);
                var snapTarget = Part("Offset snap target", originalEnd + sample.transform.right * 1.8f + sample.transform.up * 0.8f);
                snapTarget.transform.rotation = sample.transform.rotation;
                snapTarget.transform.localScale = Vector3.one * 0.25f;
                Physics.SyncTransforms();
                var snapCorner = snapTarget.transform.TransformPoint(new Vector3(0.5f, 0.5f, 0.5f));
                Vector2 sourceArrow = camera.WorldToScreenPoint(end.xOffset.GetComponentInChildren<Collider>().bounds.center);
                Vector2 targetCorner = camera.WorldToScreenPoint(snapCorner);
                Pointer(sourceArrow, true);
                Pointer(targetCorner, true);
                var snappedEnd = sample.CurrentTransform.GetBaseOrigin1(sample.transform);
                float wanted = Vector3.Dot(snapCorner - originalEnd, sample.transform.right);
                check(shape.Readout.Contains("Snapped") &&
                    Vector3.Distance(snappedEnd, originalEnd + sample.transform.right * wanted) < 0.0001f,
                    "Shape: horizontal geometry snap matches the hovered corner on only the edited axis");
                Pointer(targetCorner, true, fine: true);
                check(!shape.Readout.Contains("Snapped"), "Shape: Shift disengages geometry snap for fine positioning");
                Pointer(targetCorner, false);
                check(Vector3.Distance(sample.AppliedTransform.GetBaseOrigin1(sample.transform), snappedEnd) < 0.0001f,
                    "Shape: releasing the offset handle commits the snapped end position");
                ResetInput();
                placer.partContainer.RemovePart(snapTarget);
                UnityEngine.Object.DestroyImmediate(snapTarget.gameObject);
                PartPlacer.snapMode = false;

                nativeGizmos.gizmo2.gameObject.SetActive(true);
                foreach (int side in new[] { 0, 1 })
                foreach (var view in new[] { new Vector3(6, 0, 0), new Vector3(3, 2, 5), new Vector3(-9, 4, -7) })
                {
                    var activeEnd = side == 0 ? end : nativeGizmos.gizmo2;
                    var outwardAxis = side == 0 ? front : -front;
                    camera.transform.SetPositionAndRotation(sample.transform.position + view, Quaternion.LookRotation(-view));
                    nativeGizmos.UpdateGizmos(samplePart, null, sample.AppliedTransform);
                    activeEnd.transform.localScale = activeEnd.targetScale;
                    Physics.SyncTransforms();
                    var handle = activeEnd.sizeGizmo.GetComponentInChildren<Collider>();
                    Vector2 start = camera.WorldToScreenPoint(handle.bounds.center);
                    var ray = camera.ScreenPointToRay(start);
                    if (!handle.Raycast(ray, out var hit, 100)) throw new Exception("Length fixture missed the native handle");
                    var grab = activeEnd.transform.InverseTransformPoint(hit.point);
                    Pointer(start, true);
                    check(shape.Active && shape.Readout.StartsWith("Length offset (End " + (side + 1) + ")"),
                        "Shape: pointer picks the native length handle on end " + (side + 1) + " from " + view);
                    foreach (float distance in new[] { 0.25f, 0.85f, 0.15f, -0.15f })
                    {
                        Vector2 mouse = camera.WorldToScreenPoint(hit.point + outwardAxis * distance);
                        Pointer(mouse, true);
                        Vector2 rendered = camera.WorldToScreenPoint(activeEnd.transform.TransformPoint(grab));
                        check(Vector2.Distance(rendered, mouse) < 0.35f,
                            "Shape: grabbed length handle follows pointer within a pixel on end " + (side + 1) + " at " + distance + " m from " + view);
                    }
                    ResetInput();
                }
                PartPlacer.snapMode = stockSnap;

                // The native commit recenters the fuselage pivot. The displayed offset still
                // refers to the handle's original pivot when the player types after release.
                shape.Begin(ShapeField.Length, 0, false, placer);
                shape.Preview(0.85f);
                var intendedEnd = sample.CurrentTransform.GetBaseOrigin1(sample.transform);
                shape.Apply();
                var savedShape = sample.AppliedTransform;
                float paid = MoneyManager.Instance.money;
                var readout = plugin.Precision.UI;
                readout.Refresh(true, samplePart, true, "Copied fuselage");
                check(Get<TMPro.TMP_Text>(readout, "label").text == "Copied fuselage",
                    "Audit: copy feedback is visible after a completed shape edit");
                Put(shape, "consumed", -1); Call(shape, "ReleaseBlocker");
                void Readjust(string number)
                {
                    foreach (char digit in number) Call(shape, "OnText", digit);
                    shape.ProcessInput();
                }
                Readjust("0.85");
                check(shape.Active && Vector3.Distance(sample.CurrentTransform.GetBaseOrigin1(sample.transform), intendedEnd) < 0.001f,
                    "Audit: retyping a committed length offset leaves the endpoint at the displayed position");
                shape.Apply();
                check(sample.AppliedTransform.Equals(savedShape, false) && MoneyManager.Instance.money == paid,
                    "Audit: confirming the same length again preserves native geometry and money");
                Put(shape, "consumed", -1); Call(shape, "ReleaseBlocker");
                Readjust("0.95");
                check(shape.Active && Vector3.Distance(sample.CurrentTransform.GetBaseOrigin1(sample.transform), intendedEnd + front * 0.1f) < 0.001f,
                    "Audit: a second length readjustment adds only the difference between the entered offsets");
                shape.Apply();
                plugin.Precision.NativeToolOpened(placer, true);
                check(shape.Readout.Length == 0 && plugin.Precision.ReadoutLine.StartsWith("Rotate by"),
                    "Audit: opening rotation clears the old shape readout and numeric target");
                ResetInput();

                selection.Clear();
                nativeGizmos.SetGizmoSelectionMode((int)GizmoMode.Default);
                nativeGizmos.UpdateGizmos(samplePart, samplePart.snapPoints[0], sample.AppliedTransform);
                var stockInput = sample.AppliedTransform.side1;
                stockInput.roundness.x = 0.3375f;
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = click, buttons = 1 }); InputSystem.Update();
                end.sizeGizmo.OnHover();
                end.sizeGizmo.offset = stockInput.lengthOffset.z + 0.2f;
                var stockResult = end.GetUpdatedFuselageSide(stockInput, sample.AppliedTransform.side2);
                check(Mathf.Abs(stockResult.lengthOffset.z - stockInput.lengthOffset.z - 0.2f) < 0.0001f && stockResult.roundness.x == stockInput.roundness.x,
                    "Shape: native hover resizing works and keeps unrelated exact corners on an unselected fuselage");
                ResetInput();
                bool originalFocus = plugin.IsFocused;
                foreach (bool wantFocus in new[] { true, false })
                {
                    if (plugin.IsFocused != wantFocus) plugin.ToggleFocus();
                    Call(plugin, "SyncSelection");
                    var origin = Get<Vector3>(plugin, "orbitOrigin");
                    var updated = sample.AppliedTransform;
                    updated.side1.lengthOffset.z += 0.2f;
                    Call(nativeManager, "ApplyTransformation", sample, null, updated);
                    Call(plugin, "SyncSelection");
                    check(placer.currentSelectedPart == null && plugin.IsFocused == wantFocus &&
                        Vector3.Distance(Get<Vector3>(plugin, "orbitOrigin"), wantFocus ? samplePart.transform.position : origin) < 0.001f,
                        "Resize: committing the native hover handle " + (wantFocus ? "centers the edited part without a selection click" : "preserves the unfocused pivot"));
                }
                if (plugin.IsFocused != originalFocus) plugin.ToggleFocus();
                var parentShape = Fuselage("Attached scale parent", new Vector3(74, 0, 0));
                var childShape = Fuselage("Attached scale child", new Vector3(74, 0, 1));
                var root = parentShape.GetComponent<BuildingPart>();
                var leaf = childShape.GetComponent<BuildingPart>();
                leaf.SetParent(root.gameObject);
                selection.Set(new[] { leaf, root });
                if (!plugin.IsFocused) plugin.ToggleFocus();
                shape.Begin(ShapeField.Scale, 0, false, placer); shape.Preview(1.4f);
                var outerEnd = childShape.CurrentTransform.GetBaseOrigin1(leaf.transform);
                shape.Apply(); ResetInput();
                Call(plugin, "SyncSelection");
                check(Vector3.Distance(Get<Vector3>(plugin, "orbitOrigin"), selection.Pivot) < 0.001f,
                    "Resize: confirmed group scale focuses the updated group center");
                if (plugin.IsFocused != originalFocus) plugin.ToggleFocus();
                check(Vector3.Distance(outerEnd, childShape.AppliedTransform.GetBaseOrigin1(leaf.transform)) < 0.001f,
                    "Audit: attached child ends commit where the scale preview showed them");

                foreach (var field in new[] { ShapeField.OffsetX, ShapeField.OffsetY, ShapeField.Length })
                {
                    shape.Begin(field, 1, false, placer); shape.Preview(0.75f); shape.Apply();
                    var paidGroup = MoneyManager.Instance.money;
                    var originalSave = PlaneStorage.Instance.SaveToStream().ToArray();
                    Put(shape, "consumed", -1); Call(shape, "ReleaseBlocker");
                    Readjust("0.75");
                    check(shape.Active && shape.Apply() && originalSave.SequenceEqual(PlaneStorage.Instance.SaveToStream().ToArray()) &&
                        MoneyManager.Instance.money == paidGroup,
                        "Audit: same numeric offset on end 2 preserves the saved group and budget for " + field);
                    ResetInput();
                }
            }
            finally
            {
                ResetInput(); placer.procduralPartController = originalManager;
                camera.transform.SetPositionAndRotation(dragCameraPosition, dragCameraRotation);
            }
        }
        finally
        {
            ResetInput(); selection.Clear(); plugin.HideParts.ShowAll();
            foreach (var go in objects) if (go != null) { var p = go.GetComponent<BuildingPart>(); if (p != null) placer.partContainer.RemovePart(p); UnityEngine.Object.DestroyImmediate(go); }
            MoneyManager.Instance.money = beforeMoney; GameManager.Instance.gameModeData = modeData;
            Call(placer, "set_currentSelectedPart", old);
        }
    }
}

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
            selection.Set(new[] { a, child, b });
            var a0 = a.transform.position; var b0 = b.transform.position; var c0 = child.transform.position;
            var baseline = PlaneStorage.Instance.SaveToStream().ToArray();
            var s = plugin.Precision.Session;
            check(s.Begin(placer) && s.IsGroup, "Group: actual transform session captures multiple selected parts");
            var pivot = s.StartPosition; var startRotation = s.StartRotation;
            s.Preview(pivot + Vector3.right * 0.35f, startRotation);
            check(Vector3.Distance(a.transform.position, a0 + Vector3.right * 0.35f) < 0.0001f && Vector3.Distance(b.transform.position, b0 + Vector3.right * 0.35f) < 0.0001f,
                "Group: exact movement applies once to every selected root");
            check(Vector3.Distance(child.transform.position, c0 + Vector3.right * 0.35f) < 0.0001f, "Group: selected parent and child do not double-transform");
            check(baseline.SequenceEqual(PlaneStorage.Instance.SaveToStream().ToArray()), "Group: saves exclude all preview poses");
            var delta = Quaternion.AngleAxis(90, Vector3.up); s.Preview(pivot, delta * startRotation);
            check(Vector3.Distance(a.transform.position, pivot + delta * (a0 - pivot)) < 0.0001f && Vector3.Distance(b.transform.position, pivot + delta * (b0 - pivot)) < 0.0001f,
                "Group: rotation uses shared median pivot");
            s.Cancel(); check(Vector3.Distance(a.transform.position, a0) < 0.0001f && Vector3.Distance(child.transform.position, c0) < 0.0001f && child.parent == a,
                "Group: cancel restores all parts and attachment graph");
            selection.Set(new[] { a, b }); s.Begin(placer); s.Preview(s.StartPosition + Vector3.up, s.StartRotation); s.Commit();
            check(Vector3.Distance(a.transform.position, a0 + Vector3.up) < 0.0001f && Vector3.Distance(b.transform.position, b0 + Vector3.up) < 0.0001f, "Group: commit preserves whole group movement");
            selection.Set(new[] { a });
            plugin.HideParts.Observe(placer, false); plugin.HideParts.HideSelected();
            selection.Set(new[] { a, b }); check(selection.Count == 1 && selection.Parts[0] == b, "Group: hidden parts excluded from selection");
            plugin.HideParts.ShowAll();
            var camera = BuildingCamera.cam; var cameraPosition = camera.transform.position; var cameraRotation = camera.transform.rotation;
            camera.transform.SetPositionAndRotation(new Vector3(32, 0, -20), Quaternion.identity);
            var screen = camera.WorldToScreenPoint(b.transform.position);
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
            var originalManager = placer.procduralPartController;
            var fixture = new GameObject("Shape handle fixture"); objects.Add(fixture);
            var manager = fixture.AddComponent<ProceduralPartManager>(); manager.gizmoManager = fixture.AddComponent<ProceduralGizmoManager>();
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
            check(!shape.Preview(101) && !shape.Valid, "Shape: invalid roundness cannot silently exceed native limit");
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

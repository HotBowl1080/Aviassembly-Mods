using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aviassembly.EditorTools;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class PrecisionFixturePart : PlanePart
{
    public override PartStat[] GetPartStats() => new PartStat[0];
    public override void UpdatePart(PlaneContainer container) { }
}

public static class UnityPrecisionTests
{
    private static object Call(object o, string method, params object[] args) => AccessTools.Method(o.GetType(), method).Invoke(o, args);
    private static void Put(object o, string name, object value) => AccessTools.Field(o.GetType(), name).SetValue(o, value);
    private static T Get<T>(object o, string name) => (T)AccessTools.Field(o.GetType(), name).GetValue(o);
    private static void Singleton<T>(T c) where T : MonoBehaviour => AccessTools.Field(typeof(Singleton<T>), "m_Instance").SetValue(null, c);
    public static void Run(Plugin plugin, PartPlacer placer, PlaneContainer plane, BuildingPart part, Action<bool, string> check)
    {
        var decals = new GameObject("Precision decal fixture").AddComponent<DecalContainer>(); Singleton(decals);
        var money = new GameObject("Precision money fixture").AddComponent<MoneyManager>(); Singleton(money);
        money.money = 1234;
        var storage = plane.gameObject.AddComponent<PlaneStorage>(); Singleton(storage);
        storage.partPlacer = placer; storage.decalContainer = decals;
        Put(storage, "planeContainer", plane); Put(storage, "history", Activator.CreateInstance(AccessTools.Field(typeof(PlaneStorage), "history").FieldType));
        part.gameObject.AddComponent<PrecisionFixturePart>();
        part.snapPoints = new Transform[0]; part.isBasePart = true; part.baseRotation = Quaternion.identity;
        part.rotationOffset = part.transform.rotation;
        var child = new GameObject("Precision child").AddComponent<BuildingPart>();
        child.transform.SetParent(plane.transform); child.transform.position = part.transform.position + Vector3.up * 2;
        child.hasBeenPlaced = true; child.snapPoints = new Transform[0]; child.gameObject.AddComponent<PrecisionFixturePart>();
        child.SetParent(part.gameObject);
        placer.partContainer = new PartContainer(); placer.partContainer.AddPart(part); placer.partContainer.AddPart(child);
        placer.symmetryPlane = new SymmetryPlane(placer);
        PartPlacer.mirrorMode = false;
        Call(placer, "set_currentSelectedPart", part); plugin.Observe(placer);
        var precision = plugin.Precision;
        var session = precision.Session;
        Vector3 p0 = part.transform.position, child0 = child.transform.position;
        Quaternion r0 = part.transform.rotation;
        var originalSave = storage.SaveToStream().ToArray();
        check(session.Begin(placer), "Precision: native transaction begins for placed part");
        var history = Get<System.Collections.IList>(storage, "history");
        check(history.Count == 1, "Precision: baseline captured once in native history");
        session.Preview(p0 + Vector3.right * 0.35f, r0);
        check(Vector3.Distance(part.transform.position, p0 + Vector3.right * 0.35f) < 0.00001f, "Precision: exact 0.35 movement bypasses snapping");
        check(Vector3.Distance(child.transform.position, child0 + Vector3.right * 0.35f) < 0.00001f, "Precision: attached child follows native movement");
        session.Preview(p0 + Vector3.right * 3, r0);
        session.Preview(p0 + Vector3.right * 33, r0);
        check(Vector3.Distance(part.transform.position, p0 + Vector3.right * 33) < 0.00001f, "Precision: repeated previews use initial pose rather than accumulating");
        storage.UpdateHistory(); Call(storage, "ExecuteHistoryUpdate");
        check(history.Count == 1, "Precision: intermediate previews never enter native undo history");
        var previewSave = storage.SaveToStream().ToArray();
        check(originalSave.SequenceEqual(previewSave), "Precision: native serialization sees original state during preview");
        check(Vector3.Distance(part.transform.position, p0 + Vector3.right * 33) < 0.00001f, "Precision: live preview restored after serialization");
        session.Cancel();
        check(Vector3.Distance(part.transform.position, p0) < 0.00001f && Vector3.Distance(child.transform.position, child0) < 0.00001f, "Precision: cancel restores selected part and attached child");
        check(originalSave.SequenceEqual(storage.SaveToStream().ToArray()), "Precision: cancel restores exact native save bytes");
        check(child.parent == part && child.transform.parent == plane.transform, "Precision: preview/cancel preserves native logical and transform parenting");

        session.Begin(placer); session.Preview(p0 + new Vector3(-0.35f, 0.125f, 1.3f), r0); session.Commit();
        check(Get<bool>(storage, "updateHistory"), "Precision: apply schedules native history commit");
        Call(storage, "ExecuteHistoryUpdate");
        check(history.Count == 2, "Precision: one completed transform creates exactly one native undo step");
        check(!originalSave.SequenceEqual(storage.SaveToStream().ToArray()), "Precision: committed values are serialized normally");
        check(Get<MemoryStream>(history[0], "plane").ToArray().SequenceEqual(originalSave), "Precision: prior history entry retains exact original aircraft for undo");
        check(Mathf.Abs(money.money - 1234) < 0.001f, "Precision: edits leave money unchanged");
        p0 = part.transform.position; child0 = child.transform.position;
        session.Begin(placer); session.Preview(p0, Quaternion.AngleAxis(-33.5f, Vector3.up) * r0);
        check(Mathf.Abs(Quaternion.Angle(part.transform.rotation, r0) - 33.5f) < 0.001f, "Precision: signed fractional rotation applied exactly");
        check(Vector3.Distance(child.transform.position, p0 + Quaternion.AngleAxis(-33.5f, Vector3.up) * (child0 - p0)) < 0.0001f, "Precision: child rotates around the selected part pivot");
        check(Quaternion.Angle(part.rotationOffset, Quaternion.Inverse(part.baseRotation) * part.transform.rotation) < 0.001f, "Precision: native rotationOffset matches preview");
        session.Cancel();
        check(Quaternion.Angle(part.transform.rotation, r0) < 0.001f, "Precision: rotation cancellation restores initial orientation");

        Vector3[] angles = { Vector3.zero, new Vector3(30, 45, 60), new Vector3(-33.5f, 2.7f, 163), new Vector3(20, 90, 80), new Vector3(-60, -90, 20), new Vector3(180, -75, 359) };
        foreach (var e in angles)
        {
            var q = TransformMath.EulerXYZ(e);
            check(Quaternion.Angle(q, TransformMath.EulerXYZ(TransformMath.EulerXYZ(q))) < 0.08f, "Precision: Blender XYZ Euler round trip " + e);
        }
        check(TransformMath.FromBlender(Vector3.forward) == Vector3.up, "Precision: Blender Z maps to vertical");
        check(Vector3.Distance(TransformMath.EulerXYZ(new Vector3(0, 0, 90)) * Vector3.right, Vector3.forward) < 0.0001f, "Precision: Blender positive Z rotation maps X toward Y");
        foreach (var invalid in new[] { "-", ".", "NaN", "Infinity", "1..2", "1e999" })
            check(!TransformMath.Number(invalid, out _), "Precision: rejects incomplete or nonfinite input " + invalid);
        check(TransformMath.Number("-0.0000001", out float small) && small < 0, "Precision: accepts small signed decimals");

        // Exercise modal commands with actual Unity camera and input-device state.
        Put(precision, "placer", placer);
        check(precision.Begin(false, true), "Precision: G-style modal move begins");
        precision.Constrain(1, false); precision.Type('0'); precision.Type('.'); precision.Type('3'); precision.Type('5'); precision.UpdateMouse();
        check(Vector3.Distance(part.transform.position, p0 + Vector3.up * 0.35f) < 0.00001f, "Precision: G Y 0.35 moves vertically by exact amount");
        precision.Constrain(1, false);
        check(precision.Local && precision.Axis == 1, "Precision: repeating axis switches to Local");
        precision.Constrain(1, false);
        check(precision.Axis == -1 && !precision.Local, "Precision: third axis press removes constraint");
        precision.Cancel();
        Put(precision, "consumedFrame", -1);
        check(precision.Begin(true, true), "Precision: R-style modal rotation begins");
        precision.Constrain(1, false); foreach (char c in "-33.5") precision.Type(c); precision.UpdateMouse();
        check(Quaternion.Angle(part.transform.rotation, Quaternion.AngleAxis(-33.5f, Vector3.up) * r0) < 0.001f, "Precision: R Y -33.5 uses native rotation convention");
        precision.Type('-'); precision.UpdateMouse();
        check(Quaternion.Angle(part.transform.rotation, Quaternion.AngleAxis(33.5f, Vector3.up) * r0) < 0.001f, "Precision: minus toggles the whole numeric value");
        precision.Cancel(); Put(precision, "consumedFrame", -1);
        precision.Begin(false, true); precision.Constrain(0, false); precision.Type('2'); precision.Type('/'); precision.UpdateMouse();
        check(Vector3.Distance(part.transform.position, p0 + Vector3.right * 0.5f) < 0.00001f, "Precision: slash numeric reciprocal works");
        precision.Cancel(); Put(precision, "consumedFrame", -1);
        precision.Begin(false, true); precision.Type('-'); precision.UpdateMouse(); precision.Apply();
        check(session.Active && !precision.ValidInput, "Precision: partial numeric entry cannot commit");
        precision.Cancel(); Put(precision, "consumedFrame", -1);
        precision.Begin(false, true); precision.Constrain(1, true);
        check(precision.PlaneLock, "Precision: Shift Y excludes the vertical axis");
        Call(placer, "set_currentSelectedPart", child);
        check(!session.Active && Vector3.Distance(part.transform.position, p0) < 0.0001f, "Precision: selection change cancels preview before native selection mutation");
        Call(placer, "set_currentSelectedPart", part); plugin.Observe(placer);
        Put(precision, "consumedFrame", -1);
        part.lockRotation = true;
        check(!precision.Begin(true, true), "Precision: rotation-locked parts reject rotation mode");
        part.lockRotation = false;
        // The contextual readout uses native styling without a form or input focus.
        Call(plugin, "LateUpdate");
        var ui = precision.UI;
        check(ui != null && ui.IsAlive, "Precision: contextual readout constructed");
        if (ui != null)
        {
            ui.Refresh(true, part, true);
            var root = Get<GameObject>(ui, "root");
            check(root.GetComponentsInChildren<TMP_InputField>(true).Length == 0, "Precision: readout has no absolute-coordinate fields");
            check(!root.activeSelf && !precision.BlockGameInput, "Precision: idle starts in normal game mode without readout");
            check(root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g => !g.raycastTarget), "Precision: readout does not intercept clicks");
        }
        Put(precision, "consumedFrame", -1);
        Call(precision, "ReleaseBlocker");
        // Real input-device state drives projection, modifiers, and local constraints.
        var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        Put(precision, "keyboard", keyboard);
        var cam = BuildingCamera.cam;
        Vector3 cameraPosition = cam.transform.position;
        Quaternion cameraRotation = cam.transform.rotation;
        cam.transform.SetPositionAndRotation(p0 + new Vector3(0, 0, -15), Quaternion.identity);
        Vector2 screenStart = cam.WorldToScreenPoint(p0);
        void InputAt(Vector2 point, params Key[] keys)
        {
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }
        void ResetTransaction()
        {
            precision.Cancel(); Put(precision, "consumedFrame", -1); Call(precision, "ReleaseBlocker");
            InputAt(screenStart);
        }
        InputAt(screenStart);
        precision.Begin(false, true); precision.Type('2');
        InputAt(screenStart, Key.Tab); precision.ProcessInput();
        InputAt(screenStart, Key.Backspace); precision.ProcessInput();
        check(Vector3.Distance(part.transform.position, p0 + Vector3.right * 2) < 0.001f,
            "Audit: Backspace in a blank component preserves the number on another axis");
        precision.Type('-'); precision.UpdateMouse(); precision.Constrain(0, false); precision.UpdateMouse();
        check(precision.ValidInput && Vector3.Distance(part.transform.position, p0 + Vector3.right * 2) < 0.001f,
            "Audit: a hidden unfinished component cannot invalidate a single-axis move");
        ResetTransaction();
        precision.Begin(true, true); Put(precision, "beginFrame", Time.frameCount - 1);
        InputAt(screenStart, Key.R); precision.ProcessInput();
        InputAt(screenStart, Key.Tab); precision.ProcessInput();
        InputAt(screenStart, Key.R); precision.ProcessInput();
        precision.Type('3'); precision.UpdateMouse();
        check(!precision.Trackball && Quaternion.Angle(part.transform.rotation, Quaternion.AngleAxis(3, cam.transform.forward) * r0) < 0.03f,
            "Audit: exiting trackball returns numeric input to the displayed angle");
        ResetTransaction();
        precision.Begin(false, true); precision.Constrain(0, false);
        InputAt(cam.WorldToScreenPoint(p0 + Vector3.right * 2.35f)); precision.UpdateMouse();
        check(Vector3.Distance(part.transform.position, p0 + Vector3.right * 2.35f) < 0.001f, "Precision: mouse projection moves on selected axis");
        InputAt(cam.WorldToScreenPoint(p0 + Vector3.right * 2.35f), Key.LeftShift); precision.UpdateMouse();
        check(Vector3.Distance(part.transform.position, p0 + Vector3.right * 0.235f) < 0.001f, "Precision: Shift mouse motion uses tenth precision");
        InputAt(cam.WorldToScreenPoint(p0 + Vector3.right * 2.35f), Key.LeftCtrl); precision.UpdateMouse();
        check(Vector3.Distance(part.transform.position, p0 + Vector3.right * 2) < 0.001f, "Precision: Ctrl mouse motion snaps to whole units");
        InputAt(cam.WorldToScreenPoint(p0 + Vector3.right * 2.35f), Key.LeftCtrl, Key.LeftShift); precision.UpdateMouse();
        check(Vector3.Distance(part.transform.position, p0 + Vector3.right * 0.2f) < 0.001f, "Precision: Ctrl Shift uses fine snapping");
        ResetTransaction();
        precision.Begin(false, true);
        var freeOffset = new Vector3(0.7f, 0.4f, 0);
        InputAt(cam.WorldToScreenPoint(p0 + freeOffset)); precision.UpdateMouse();
        check(Vector3.Distance(part.transform.position, p0 + freeOffset) < 0.001f, "Precision: unconstrained mouse move follows view plane");
        precision.ConstrainByMouse(screenStart + Vector2.up * 100, false);
        check(precision.Axis == 1 && !precision.PlaneLock, "Precision: middle mouse chooses projected vertical Y axis");
        ResetTransaction();
        var oblique = Quaternion.Euler(0, 32, 0);
        cam.transform.SetPositionAndRotation(p0 - oblique * Vector3.forward * 15, oblique);
        precision.Begin(false, true); precision.Constrain(2, false);
        InputAt(cam.WorldToScreenPoint(p0 + Vector3.forward * 3)); precision.UpdateMouse();
        check(Vector3.Distance(part.transform.position, p0 + Vector3.forward * 3) < 0.001f,
            "Precision: projected axis drag matches the mouse at an oblique angle");
        ResetTransaction();
        var narrow = Quaternion.Euler(0, 4, 0);
        cam.transform.SetPositionAndRotation(p0 - narrow * Vector3.forward * 15, narrow);
        precision.Begin(false, true); precision.Constrain(2, false);
        InputAt(screenStart - Vector2.right * 80); precision.UpdateMouse();
        float displacement = Vector3.Dot(part.transform.position - p0, Vector3.forward);
        float projected = screenStart.x - cam.WorldToScreenPoint(part.transform.position).x;
        check(displacement > 0 && displacement < 15 && projected >= 0 && projected <= 80.001f,
            "Precision: almost end-on axis motion stays bounded without overshooting the cursor");
        ResetTransaction();
        cam.transform.SetPositionAndRotation(p0 + new Vector3(0, 0, -15), Quaternion.identity);
        part.transform.rotation = Quaternion.AngleAxis(45, Vector3.up) * r0;
        var localRotation = part.transform.rotation;
        precision.Begin(false, true); precision.Constrain(0, false); precision.Constrain(0, false);
        foreach (char c in "0.35") precision.Type(c); precision.UpdateMouse();
        check(Vector3.Distance(part.transform.position, p0 + localRotation * Vector3.right * 0.35f) < 0.001f, "Precision: repeated X numeric input uses original local axis");
        ResetTransaction(); part.transform.rotation = r0;
        precision.Begin(false, true); precision.Constrain(1, true);
        precision.Type('2'); Put(precision, "component", 1); precision.Type('3'); precision.UpdateMouse();
        check(Vector3.Distance(part.transform.position, p0 + new Vector3(2, 0, 3)) < 0.001f, "Precision: plane-lock numbers map to both included axes");
        ResetTransaction();
        InputAt(screenStart + Vector2.right * 100);
        precision.Begin(true, true); precision.Constrain(2, false);
        InputAt(screenStart + Vector2.up * 100); precision.UpdateMouse();
        check(Mathf.Abs(Quaternion.Angle(part.transform.rotation, r0) - 90) < 0.02f, "Precision: mouse rotation measures angle around projected pivot");
        ResetTransaction();
        precision.Begin(true, true); Put(precision, "<Trackball>k__BackingField", true);
        InputAt(screenStart + new Vector2(50, 25)); precision.UpdateMouse();
        check(Quaternion.Angle(part.transform.rotation, r0) > 10, "Precision: trackball combines both mouse directions");
        ResetTransaction();
        cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        InputAt(Vector2.zero);
        Put(placer.symmetryPlane, "planeNormal", Vector3.right);
        Put(placer.symmetryPlane, "plane", new Plane(Vector3.right, Vector3.zero));
        var mirror = new GameObject(part.gameObject.name).AddComponent<BuildingPart>();
        mirror.transform.SetParent(plane.transform);
        mirror.transform.position = placer.symmetryPlane.GetMirroredPosition(p0);
        mirror.transform.rotation = Quaternion.Euler(13, 27, 41);
        mirror.hasBeenPlaced = true; mirror.snapPoints = new Transform[0];
        mirror.gameObject.AddComponent<PrecisionFixturePart>();
        var mirrorMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mirrorMesh.transform.SetParent(mirror.transform);
        mirrorMesh.transform.position = placer.symmetryPlane.GetMirroredPosition(CustomMath.GetObjectCenter(part.gameObject));
        placer.partContainer.AddPart(mirror); PartPlacer.mirrorMode = true;
        check(placer.GetMirrorPart(part) == mirror, "Precision: native counterpart lookup finds mirror fixture");
        Vector3 mirrorPosition = mirror.transform.position;
        Quaternion mirrorRotation = mirror.transform.rotation;
        session.Begin(placer); session.Preview(p0 + Vector3.right * 0.35f, r0);
        check(Vector3.Distance(mirror.transform.position, placer.symmetryPlane.GetMirroredPosition(part.transform.position)) < 0.001f,
            "Precision: native mirror follows translation");
        check(Quaternion.Angle(mirror.transform.rotation, mirrorRotation) < 0.02f, "Precision: moving preserves existing mirror rotation");
        session.Cancel();
        session.Begin(placer); session.Preview(p0, Quaternion.AngleAxis(-33.5f, Vector3.up) * r0);
        check(Quaternion.Angle(mirror.transform.rotation, placer.symmetryPlane.GetMirroredRotation(part.transform)) < 0.02f,
            "Precision: rotating uses native mirror orientation");
        session.Cancel();
        check(Vector3.Distance(mirror.transform.position, mirrorPosition) < 0.001f && Quaternion.Angle(mirror.transform.rotation, mirrorRotation) < 0.02f,
            "Precision: cancel restores mirror position and rotation");
        PartPlacer.mirrorMode = false;
        placer.partContainer.RemovePart(mirror); UnityEngine.Object.DestroyImmediate(mirror.gameObject);
    }
}

using System;
using System.Linq;
using Aviassembly.EditorTools;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class UnityInspectionTests
{
    private static T Get<T>(object o, string field) => (T)AccessTools.Field(o.GetType(), field).GetValue(o);
    public static void Run(Plugin plugin, PartPlacer placer, PlaneContainer plane, Action<bool, string> check)
    {
        var selected = placer.currentSelectedPart;
        var propGo = new GameObject("Prop inspection fixture"); propGo.transform.SetParent(plane.transform);
        propGo.transform.position = new Vector3(100, 100, 100);
        var part = propGo.AddComponent<BuildingPart>(); part.hasBeenPlaced = true;
        var rotor = propGo.AddComponent<RotorCollider>(); rotor.rotorLength = 2; rotor.rotorPosition = Vector3.right * 0.3f;
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.transform.SetParent(plane.transform);
        var other = obstacle.AddComponent<BuildingPart>(); other.hasBeenPlaced = true;
        other.partName = "Inspection obstruction";
        var renderer = obstacle.GetComponent<Renderer>(); var collider = obstacle.GetComponent<Collider>();
        var originalMaterial = renderer.sharedMaterial;
        var clearance = plugin.Clearance; var hide = plugin.HideParts;
        void Select(BuildingPart target) => AccessTools.PropertySetter(typeof(PartPlacer), "currentSelectedPart").Invoke(placer, new object[] { target });
        void Refresh() { Physics.SyncTransforms(); clearance.Tick(placer, true); }
        try
        {
            AccessTools.Field(typeof(PrecisionController), "consumedFrame").SetValue(plugin.Precision, -1);
            InputManager.Instance.currentInputBlocker = null;
            InputSystem.QueueStateEvent(Mouse.current, new MouseState()); InputSystem.Update();
            Select(part);
            Vector3 center = rotor.transform.TransformPoint(rotor.rotorPosition);
            obstacle.transform.position = center + Vector3.up;
            Refresh();
            var query = Get<MeshCollider>(clearance, "probe");
            check(clearance.HasProp && clearance.Obstructions == 1, "Prop: native-radius sweep detects an intersecting aircraft collider count=" + clearance.Obstructions + " probe=" + query.bounds + " convex=" + query.convex + " obstacle=" + collider.bounds + " mesh=" + query.sharedMesh.bounds + " pos=" + query.transform.position);
            check(!query.enabled && propGo.GetComponentsInChildren<Collider>().Length == 0,
                "Prop: query collider disabled between checks and outside aircraft hierarchy");
            var ring = Get<LineRenderer>(clearance, "ring");
            Refresh(); check(clearance.Obstructions == 1, "Prop: repeated queries retain the same obstruction");
            check(clearance.HighlightDrawCount > 0 && renderer.sharedMaterial == originalMaterial, "Prop: obstruction highlights leave native materials untouched");
            check(Vector3.Distance(ring.GetPosition(0), center + rotor.transform.TransformVector(Vector3.up) * 2) < 0.001f,
                "Prop: overlay uses native rotor position and rotation axis");
            obstacle.transform.position = center + Vector3.right * 1.5f; Refresh();
            check(clearance.Obstructions == 0 && Mathf.Abs(clearance.Gap - 1f) < 0.002f && clearance.Readout.Contains("Approx gap"),
                "Prop: axial gap reports estimated metres without calling it exact");
            check(clearance.HighlightDrawCount == 0, "Prop: moving clear removes stale obstruction highlights");
            obstacle.transform.position = center + Vector3.up * 2.55f; Refresh();
            check(clearance.Obstructions == 0 && clearance.Gap < 0.1f, "Prop: near blade tip warns without a false intersection");
            rotor.helicopter = true; Refresh();
            check(!clearance.HasProp && !ring.gameObject.activeSelf, "Prop: helicopter rotors excluded");
            rotor.helicopter = false; Select(other); Refresh();
            check(!clearance.HasProp && clearance.Readout == "", "Prop: ordinary parts never receive clearance readout");
            Select(part);
            obstacle.transform.position = center + Vector3.up;
            obstacle.transform.SetParent(propGo.transform, true);
            Refresh(); check(clearance.Obstructions == 1, "Prop: attached child parts still count as obstructions");
            UnityEngine.Object.DestroyImmediate(other); Refresh();
            check(clearance.Obstructions == 0, "Prop: own engine housing is excluded");
            other = obstacle.AddComponent<BuildingPart>(); other.hasBeenPlaced = true;
            obstacle.transform.SetParent(plane.transform, true);
            Select(other); hide.Observe(placer, false);
            check(hide.HideSelected() && hide.XrayDrawCount > 0, "X-ray: hidden mesh receives ghost draw commands by default");
            check(!renderer.enabled && renderer.sharedMaterial == originalMaterial && collider.enabled,
                "X-ray: ghost preserves original materials, hidden renderer state and physics");
            Select(part); Refresh();
            check(clearance.Obstructions == 1, "Prop: hidden parts still obstruct the sweep");
            check(hide.IsHiddenCollider(collider), "X-ray: ghost remains excluded from editor picking");
            hide.RefreshXray();
            check(hide.XrayEnabled && hide.XrayDrawCount > 0 && hide.IsHidden(other), "X-ray: hidden parts always retain ghost view");
            check(typeof(HideParts).GetMethod("ToggleXray") == null && !typeof(HideParts).GetProperty("XrayEnabled").CanWrite,
                "X-ray: cannot be disabled by a toggle or setter");
            hide.ShowAll();
            check(renderer.enabled && hide.XrayDrawCount == 0 && renderer.sharedMaterial == originalMaterial,
                "X-ray: show all removes ghosts and restores original appearance");
            // Non-convex aircraft collision geometry must also be supported.
            UnityEngine.Object.DestroyImmediate(collider);
            var meshCollider = obstacle.AddComponent<MeshCollider>(); meshCollider.sharedMesh = obstacle.GetComponent<MeshFilter>().sharedMesh;
            obstacle.transform.position = center + Vector3.right * 1.5f; Refresh();
            check(clearance.Obstructions == 0 && clearance.Gap > 0.9f, "Prop: gap queries support non-convex aircraft mesh colliders");
            obstacle.transform.position = center + Vector3.up; Refresh();
            check(clearance.Obstructions == 1, "Prop: sweep detects non-convex mesh obstruction");
            propGo.transform.rotation = Quaternion.Euler(0, 0, 90); propGo.transform.localScale = new Vector3(1, -1.5f, 1.5f);
            center = rotor.transform.TransformPoint(rotor.rotorPosition);
            obstacle.transform.position = center + rotor.transform.TransformVector(Vector3.up) * 1.5f; Refresh();
            check(clearance.Obstructions == 1, "Prop: rotated mirrored and resized sweep remains aligned");
            clearance.Toggle(); Refresh();
            check(!clearance.Enabled && !clearance.HasProp, "Prop: toolbar disables inspection");
            clearance.Toggle(); Refresh();
            check(clearance.Enabled && clearance.HasProp, "Prop: toolbar re-enables inspection");
            var menu = Get<PlacementToolbar>(clearance, "partMenu"); var propButton = Get<Button>(clearance, "button");
            check(propButton.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("All Props"), "Prop menu: global toggle scope is explicit");
            check(menu != null && propButton != null && propButton.GetComponentInParent<PlacementToolbar>() == menu,
                "Prop menu: control belongs to the native selected-part toolbar");
            if (menu != null && propButton != null)
            {
                var eventSystem = EventSystem.current; GameObject temporaryEvents = null;
                if (eventSystem == null) { temporaryEvents = new GameObject("Prop menu events", typeof(EventSystem)); eventSystem = temporaryEvents.GetComponent<EventSystem>(); }
                menu.EnableToolbar(part.gameObject); menu.transform.localScale = Vector3.one;
                menu.transform.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0);
                Refresh(); Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)menu.transform); Canvas.ForceUpdateCanvases();
                check(propButton.gameObject.activeInHierarchy && propButton.IsInteractable(), "Prop menu: visible and enabled for selected propeller");
                var canvas = menu.GetComponentInParent<Canvas>(); var rect = (RectTransform)propButton.transform;
                Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
                var data = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left, position = point, eligibleForClick = true };
                bool before = clearance.Enabled;
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point, buttons = 1 }); InputSystem.Update();
                ExecuteEvents.Execute(propButton.gameObject, data, ExecuteEvents.pointerDownHandler); Refresh();
                check(propButton.IsInteractable() && plugin.BlockPanelClick, "Prop menu: full pointer press blocks world selection and remains enabled");
                check(Get<int>(menu, "lastEnableCallFrame") == Time.frameCount, "Prop menu: native visibility heartbeat survives pointer press");
                AccessTools.Field(typeof(PlacementToolbar), "lastEnableCallFrame").SetValue(menu, Time.frameCount - 3);
                AccessTools.Method(typeof(PlacementToolbar), "Update").Invoke(menu, null);
                check(Get<int>(menu, "lastEnableCallFrame") == Time.frameCount,
                    "Prop menu: patched native Update refreshes visibility before native animation checks");
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point }); InputSystem.Update();
                ExecuteEvents.Execute(propButton.gameObject, data, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(propButton.gameObject, data, ExecuteEvents.pointerClickHandler); Refresh();
                check(clearance.Enabled != before && !clearance.HasProp, "Prop menu: pointer release toggles inspection off");
                propButton.onClick.Invoke(); Refresh(); check(clearance.Enabled && clearance.HasProp, "Prop menu: same control restores inspection");
                Select(other); menu.EnableToolbar(other.gameObject); Refresh();
                check(!propButton.gameObject.activeSelf, "Prop menu: control disappears for ordinary parts");
                Select(null); Refresh(); check(!propButton.gameObject.activeSelf, "Prop menu: control disappears when selection clears");
                Select(part); menu.EnableToolbar(part.gameObject); Refresh();
                var lines = new System.Collections.Generic.List<string>();
                foreach (Transform child in propButton.transform.parent) lines.Add(child.name + " " + string.Join(",", child.GetComponents<Component>().Select(c => c.GetType().Name)) + " " + ((RectTransform)child).rect);
                System.IO.File.WriteAllLines(System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "prop-menu-layout.txt"), lines);
                InputSystem.QueueStateEvent(Mouse.current, new MouseState()); InputSystem.Update();
                if (temporaryEvents != null) UnityEngine.Object.DestroyImmediate(temporaryEvents);
            }
            Select(other); hide.Observe(placer, false); hide.HideSelected();
            GameManager.gameMode = GameMode.Flying; hide.Observe(placer, false); Refresh();
            check(hide.XrayDrawCount == 0 && renderer.enabled && !clearance.HasProp && !ring.gameObject.activeSelf,
                "Inspection: flight restores hidden visuals and removes both overlays");
            GameManager.gameMode = GameMode.Building;
            var native = Resources.FindObjectsOfTypeAll<RotorCollider>().Where(r => r != rotor && !r.helicopter && r.rotorLength > 0).ToArray();
            check(native.Length > 0, "Prop: installed Building assets contain supported native propeller definitions (" + native.Length + ")");
        }
        finally
        {
            GameManager.gameMode = GameMode.Building; hide.ShowAll();
            UnityEngine.Object.DestroyImmediate(obstacle); UnityEngine.Object.DestroyImmediate(propGo);
            Select(selected); clearance.Tick(placer, true);
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using Aviassembly.EditorTools;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Add this file to Aviassembly.FocusRuntimeTests and call Run immediately after
/// UnityPrecisionTests.Run. Uses that callback-suppressed synthetic fixture only.
/// Does not create another Plugin/HideParts instance or launch another process.
/// Keyboard dispatch is exercised directly, not with OS key injection.
/// </summary>
public static class UnityHidePartsIntegrationTests
{
    private static object Call(object o, string method, params object[] args) => AccessTools.Method(o.GetType(), method).Invoke(o, args);
    private static void Put(object o, string field, object value) => AccessTools.Field(o.GetType(), field).SetValue(o, value);
    private static T Get<T>(object o, string field) => (T)AccessTools.Field(o.GetType(), field).GetValue(o);

    public static void Run(Plugin plugin, PartPlacer placer, PlaneContainer plane, BuildingPart part, Action<bool, string> check)
    {
        var hide = plugin.HideParts;
        var precision = plugin.Precision;
        check(hide != null && precision != null, "Combined: production Plugin owns both modules");
        if (hide == null || precision == null) return;
        var selectedBefore = placer.currentSelectedPart;
        var eventSystem = EventSystem.current;
        GameObject temporaryEventSystem = null;
        if (eventSystem == null)
        {
            temporaryEventSystem = new GameObject("Combined fixture EventSystem", typeof(EventSystem));
            eventSystem = temporaryEventSystem.GetComponent<EventSystem>();
        }
        var focusedBefore = eventSystem.currentSelectedGameObject;
        GameObject fieldObject = null;
        var owned = GameObject.CreatePrimitive(PrimitiveType.Cube);
        owned.name = "Combined hidden target";
        owned.transform.SetParent(plane.transform, false);
        var hiddenTarget = owned.AddComponent<BuildingPart>(); hiddenTarget.hasBeenPlaced = true;
        owned.AddComponent<CombinedHideFixturePart>();
        hiddenTarget.partName = "Combined hidden target";
        hiddenTarget.snapPoints = new Transform[0];
        var renderer = owned.GetComponent<Renderer>();
        var collider = owned.GetComponent<Collider>();
        var startPosition = owned.transform.position;

        void Select(BuildingPart p)
        {
            Call(placer, "set_currentSelectedPart", p);
            hide.Observe(placer, false);
        }
        void ReleasePrecision()
        {
            precision.Cancel();
            Put(precision, "consumedFrame", -1);
            Call(precision, "ReleaseBlocker");
        }
        void Blocked(string reason)
        {
            check(!hide.HandleShortcut(KeyCode.V), "Combined: V blocked " + reason);
            check(!hide.HandleShortcut(KeyCode.I), "Combined: I blocked " + reason);
            check(!hide.HideSelected(), "Combined: Hide button action blocked " + reason);
            hide.ShowAllFromUI();
            check(hide.HiddenCount == 1 && !renderer.enabled && placer.currentSelectedPart == part,
                "Combined: Show All button blocked without selection mutation " + reason);
            var existingUI = Get<HidePartsUI>(hide, "ui");
            if (existingUI != null)
            {
                Get<Button>(existingUI, "hideButton").onClick.Invoke();
                Get<Button>(existingUI, "showButton").onClick.Invoke();
                check(hide.HiddenCount == 1 && !renderer.enabled && placer.currentSelectedPart == part,
                    "Combined: actual toolbar listeners enforce guards " + reason);
            }
        }
        try
        {
            ReleasePrecision(); eventSystem.SetSelectedGameObject(null); hide.ShowAll();
            Select(hiddenTarget);
            check(hide.HandleShortcut(KeyCode.V) && hide.HiddenCount == 1 && !renderer.enabled,
                "Combined: V hides a selected placed part through production module");
            check(collider.enabled && owned.activeSelf && owned.transform.position == startPosition,
                "Combined: hide leaves physics activation and position unchanged");
            check(placer.currentSelectedPart == null, "Combined: hide clears native selection");
            Select(part); Put(precision, "placer", placer);
            check(precision.Begin(false, true), "Combined: real Precision transaction starts with another part hidden");
            check(precision.BlockGameInput, "Combined: active Precision owns input");
            Blocked("during Precision");
            precision.Cancel();
            // Release the native blocker to prove the injected consumed-frame predicate itself works.
            Call(precision, "ReleaseBlocker");
            check(!precision.Session.Active && precision.BlockGameInput, "Combined: cancelled transaction consumes this frame");
            Blocked("during consumed frame");
            Put(precision, "consumedFrame", -1);

            // This independent focused-field case catches missing guards after a transaction ends.
            fieldObject = new GameObject("Combined game text focus", typeof(RectTransform), typeof(TMP_InputField));
            var field = fieldObject.GetComponent<TMP_InputField>();
            eventSystem.SetSelectedGameObject(fieldObject);
            Put(field, "m_AllowInput", true);
            check(plugin.TextEntryActive && !precision.BlockGameInput, "Combined: focused precision field is detected without active transaction");
            Blocked("during text focus");
            check(!GetProperty<bool>(plugin, "CanUseCamera"), "Combined: F camera action blocked during text focus");
            check(plugin.BlockPanelClick, "Combined: native PartPlacer and gizmo input blocked during text focus");
            Put(field, "m_AllowInput", false); eventSystem.SetSelectedGameObject(null);
            check(hide.HandleShortcut(KeyCode.I) && hide.HiddenCount == 0 && renderer.enabled,
                "Combined: I restores all after text focus ends");
            Select(hiddenTarget);
            check(hide.HandleShortcut(KeyCode.V), "Combined: V works again after all guards release");
            hide.RefreshUI();
            var ui = Get<HidePartsUI>(hide, "ui");
            check(ui != null && ui.IsAlive, "Combined: native hide toolbar exists alongside Precision");
            if (ui != null)
            {
                foreach (bool crossed in new[] { true, false })
                {
                    var root = Get<GameObject>(ui, crossed ? "hideRoot" : "showRoot");
                    var icon = root.GetComponentInChildren<HidePartsEyeIcon>();
                    check(root.GetComponentInChildren<TMP_Text>(true) == null && icon != null && icon.Crossed == crossed &&
                        icon.rectTransform.anchorMin == Vector2.one * 0.5f && icon.rectTransform.anchoredPosition == Vector2.zero,
                        "Combined: " + (crossed ? "Hide" : "Show All") + " displays only a centered eye icon");
                }
                Get<Button>(ui, "showButton").onClick.Invoke();
                check(hide.HiddenCount == 0 && renderer.enabled, "Combined: real Show All button listener restores part");
                Select(hiddenTarget); hide.RefreshUI();
                Get<Button>(ui, "hideButton").onClick.Invoke();
                check(hide.HiddenCount == 1 && !renderer.enabled, "Combined: real Hide button listener hides selected part");
                hide.ShowAll(); Select(hiddenTarget); hide.RefreshUI(); Canvas.ForceUpdateCanvases();
                void PointerClick(Button button, string label)
                {
                    var canvas = Get<Canvas>(ui, "canvas");
                    var rect = (RectTransform)button.transform;
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                        rect.TransformPoint(rect.rect.center));
                    var data = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left, position = point, eligibleForClick = true };
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point, buttons = 1 }); InputSystem.Update();
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                    hide.Observe(placer, false); hide.RefreshUI();
                    plugin.Clearance?.Tick(placer, true);
                    check(button.IsInteractable(), "Pointer: " + label + " stays enabled while held down");
                    check(plugin.BlockPanelClick, "Pointer: " + label + " press blocks aircraft picking");
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point }); InputSystem.Update();
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
                }
                PointerClick(Get<Button>(ui, "hideButton"), "Hide");
                check(hide.HiddenCount == 1 && !renderer.enabled, "Pointer: Hide completes a full press and release");
                hide.RefreshUI();
                PointerClick(Get<Button>(ui, "showButton"), "Show All");
                check(hide.HiddenCount == 0 && renderer.enabled, "Pointer: Show All completes a full press and release");
                check(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .All(b => b.gameObject.name != "EditorTools X-ray"), "UI: permanent X-ray has no button");
                InputSystem.QueueStateEvent(Mouse.current, new MouseState()); InputSystem.Update();
            }
            check(!hide.HandleShortcut(KeyCode.H), "Combined: former H shortcut has no action");
            check(hide.HandleShortcut(KeyCode.I) && renderer.enabled, "Combined: final I restores original renderer state");
            CheckLayout(plugin, check);
        }
        finally
        {
            if (fieldObject != null) { Put(fieldObject.GetComponent<TMP_InputField>(), "m_AllowInput", false); }
            eventSystem.SetSelectedGameObject(null);
            ReleasePrecision(); hide.ShowAll();
            if (fieldObject != null) UnityEngine.Object.DestroyImmediate(fieldObject);
            UnityEngine.Object.DestroyImmediate(owned);
            Select(selectedBefore);
            eventSystem.SetSelectedGameObject(focusedBefore);
            if (temporaryEventSystem != null) UnityEngine.Object.DestroyImmediate(temporaryEventSystem);
        }
    }
    private static T GetProperty<T>(object o, string name) => (T)AccessTools.Property(o.GetType(), name).GetValue(o, null);

    /// <summary>Temporarily makes every native bottom toolbar control visible, measures screen bounds, then restores visibility.</summary>
    public static void CheckLayout(Plugin plugin, Action<bool, string> check)
    {
        plugin.HideParts.RefreshUI();
        var ui = Get<HidePartsUI>(plugin.HideParts, "ui");
        if (ui == null) { check(false, "Combined layout: hide toolbar must exist"); return; }
        var canvas = Get<Canvas>(ui, "canvas");
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var saved = new Dictionary<GameObject, bool>();
        var controls = new List<RectTransform>();
        var rows = new HashSet<RectTransform>();
        var names = new HashSet<string> { "Snapping", "Undo", "Mirror", "RotationSnap" };
        void Add(GameObject go)
        {
            if (go == null || !(go.transform is RectTransform rect) || controls.Contains(rect)) return;
            saved[go] = go.activeSelf; go.SetActive(true); controls.Add(rect);
            if (rect.parent is RectTransform parent) rows.Add(parent);
        }
        Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        try
        {
            int nativeCount = 0;
            foreach (var native in canvas.GetComponentsInChildren<MirrorButton>(true))
            {
                foreach (Transform sibling in native.transform.parent)
                    if (names.Contains(sibling.name)) { Add(sibling.gameObject); nativeCount++; }
                break;
            }
            Add(Get<GameObject>(ui, "hideRoot")); Add(Get<GameObject>(ui, "showRoot"));
            var focus = Get<object>(plugin, "focusUI");
            if (focus != null) Add(Get<GameObject>(focus, "root"));
            Canvas.ForceUpdateCanvases();
            foreach (var row in rows) LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            Canvas.ForceUpdateCanvases();
            check(nativeCount == 4, "Combined layout: all four native bottom controls forced visible");
            var obstacles = new List<Rect>();
            string obstacleDetails = "";
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(false))
            {
                string value = label.text.Trim();
                if (value != "CG" && value != "Drag") continue;
                var selectable = label.GetComponentInParent<Selectable>();
                var rect = ScreenRect(selectable != null ? (RectTransform)selectable.transform : label.rectTransform);
                obstacles.Add(rect); obstacleDetails += " " + value + "=" + rect;
            }
            check(obstacles.Count >= 2, "Combined layout: native CG and Drag controls located" + obstacleDetails);
            bool clear = true, inScreen = true; float maxX = 0;
            string details = "";
            foreach (var control in controls)
            {
                var rect = ScreenRect(control); maxX = Mathf.Max(maxX, rect.xMax);
                inScreen &= rect.xMin >= -0.5f && rect.xMax <= Screen.width + 0.5f && rect.yMin >= -0.5f && rect.yMax <= Screen.height + 0.5f;
                foreach (var obstacle in obstacles) if (rect.Overlaps(obstacle)) clear = false;
                details += " " + control.name + "=" + rect;
            }
            check(inScreen, "Combined layout: every toolbar control inside screen " + Screen.width + "x" + Screen.height + "; maxX=" + maxX.ToString("F2"));
            check(clear, "Combined layout: toolbar clears right CG/Drag controls; maxX=" + maxX.ToString("F2") + obstacleDetails + details);
            bool siblingsClear = true;
            for (int i = 0; i < controls.Count; i++)
                for (int j = i + 1; j < controls.Count; j++)
                    if (ScreenRect(controls[i]).Overlaps(ScreenRect(controls[j]))) siblingsClear = false;
            check(siblingsClear, "Combined layout: all native and custom toolbar controls avoid each other");
        }
        finally
        {
            foreach (var item in saved) if (item.Key != null) item.Key.SetActive(item.Value);
            foreach (var row in rows) if (row != null) LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        }
    }
}

public sealed class CombinedHideFixturePart : PlanePart
{
    public override PartStat[] GetPartStats() => new PartStat[0];
    public override void UpdatePart(PlaneContainer container) { }
}

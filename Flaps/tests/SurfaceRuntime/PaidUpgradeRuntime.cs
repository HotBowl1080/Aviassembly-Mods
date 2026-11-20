using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[BepInPlugin("rylan.flaps.paid.runtime-tests", "Paid Flaps Runtime Tests", "1.0.0")]
[BepInDependency("com.andrewstack.aviassembly.flaps")]
public sealed partial class PaidUpgradeRuntime : BaseUnityPlugin
{
    readonly List<string> results = new List<string>();
    GameManager manager;
    PlaneContainer plane;
    PlaneStorage storage;
    Rigidbody body;
    static string Host => Paths.GameRootPath;

    void Awake()
    {
        if (!File.Exists(Path.Combine(Host, "PAID_FLAPS_TEST_ONLY"))) return;
        AudioListener.pause = true;
        AudioListener.volume = 0;
    }

    IEnumerator Start()
    {
        if (!File.Exists(Path.Combine(Host, "PAID_FLAPS_TEST_ONLY"))) yield break;
        SceneManager.sceneLoaded += DisableNativeScripts;
        foreach (string scene in new[] { "Building", "Flying", "Persistent", "Game" })
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
        SceneManager.sceneLoaded -= DisableNativeScripts;
        var prepared=new List<WingGeometryCache.Entry>();
        foreach(var wing in Resources.FindObjectsOfTypeAll<Wing>()){
            byte code=WingFlapIds.Of(wing.name);
            if(code<1 || code>8 || wing.gameObject.scene.IsValid())continue;
            prepared.Add(WingGeometryCache.Request(wing.GetComponent<MeshFilter>().sharedMesh,code==1));
        }
        float deadline=Time.realtimeSinceStartup+15f;
        while(prepared.Any(entry=>entry!=null && !entry.build.IsCompleted) && Time.realtimeSinceStartup<deadline)yield return null;
        try
        {
            SetupFlight();
            RunWings();
            CheckInputs();
            results.Add("RESULT " + results.Count + " passed, 0 failed");
        }
        catch (Exception ex) { results.Add("FAIL " + ex); }
        File.WriteAllLines(Path.Combine(Host, "paid-flaps-results.txt"), results);
        Application.Quit(results.Last().StartsWith("RESULT", StringComparison.Ordinal) ? 0 : 1);
    }

    static void DisableNativeScripts(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (script && script.GetType().Assembly == typeof(Wing).Assembly) script.enabled = false;
    }

    static T SetSingleton<T>(T value) where T : MonoBehaviour
    {
        AccessTools.Field(typeof(Singleton<T>), "m_Instance").SetValue(null, value);
        value.enabled = false;
        return value;
    }

    void SetupFlight()
    {
        FlapsPlugin.instance.enabled = false;
        manager = SetSingleton(new GameObject("Flap test manager").AddComponent<GameManager>());
        manager.gameModeData = new GameModeData();
        manager.inMenu = manager.modalsOpen = false;
        GameManager.gameMode = GameMode.Flying;
        plane = SetSingleton(new GameObject("Flap test aircraft").AddComponent<PlaneContainer>());
        storage = SetSingleton(plane.gameObject.AddComponent<PlaneStorage>());
        body = plane.gameObject.AddComponent<Rigidbody>();
        body.mass = 10f;
        body.useGravity = false;
        plane.planeParts = new List<PlanePart>();
        plane.planeGravityMultiplier = plane.liftMultiplier = 1f;
        AccessTools.Field(typeof(PlaneContainer), "rb").SetValue(plane, body);
        AccessTools.Field(typeof(PlaneContainer), "cachedLocalForward").SetValue(plane, Vector3.forward);
        AccessTools.PropertySetter(typeof(PlaneContainer), "FlightModeInitialized").Invoke(plane, new object[] { true });
        var menu = MenuDialog.Instance;
        if (menu)
            foreach (var panel in new[] { menu.settingsPanel, menu.menuDialog, menu.saveDialog, menu.quitDialog })
                if (panel) panel.SetActive(false);
        Time.timeScale = 1f;
        FlapsPlugin.flightUI = null;
        Tick();
        Check(FlapsPlugin.activePlane == plane && FlapFlightInput.Allowed, "Native flight state permits controls");
        Check(AudioListener.pause && AudioListener.volume == 0, "Private Unity fixture is muted");
    }

    void RunWings()
    {
        var nativeWings = Resources.FindObjectsOfTypeAll<Wing>().Where(w => !w.gameObject.scene.IsValid())
            .GroupBy(w => w.name).Select(group => group.First()).OrderBy(w => w.name).ToList();
        Check(nativeWings.Count == 16 && nativeWings.Count(FlapsPlugin.Supports) == 13, "13 movable surface wings in the native 16-wing catalog");
        var catalog = new PreviewCatalog();
        foreach (var prefab in nativeWings)
        {
            var visual = prefab.GetComponentInChildren<WingVisual>(true);
            Check(FlapsPlugin.Supports(prefab) == (visual && visual.controlSurface), "Eligibility follows stock surface: " + prefab.name);
            if (!FlapsPlugin.Supports(prefab)) continue;
            foreach (bool mirrored in new[] { false, true })
            {
                var wing = Clone(prefab, mirrored);
                var wingVisual = wing.GetComponentInChildren<WingVisual>(true);
                AccessTools.Method(typeof(WingVisual), "Start").Invoke(wingVisual, null);
                NativeUpdate(wingVisual);
                var stock = wing.GetComponent<MeshFilter>().sharedMesh;
                var upgrade = WingUpgrades.Ensure(wing);
                float basePrice = upgrade.BasePrice;
                string label = prefab.name + (mirrored ? " mirrored" : " normal");
                if (!mirrored) CheckSettings(wing, label);
                var preview = StockEntry(wing);
                upgrade.Restore(basePrice, WingUpgradeOptions.Flaps);
                var marker = wing.GetComponent<FlapMarker>();
                Check(marker.IsReady && marker.CanOperate(plane), "Ready on real prefab: " + label);
                Check(FlapsPlugin.ShowSelector, "Flap lever becomes available: " + label);
                FlapsPlugin.SelectStage(3);
                if (!mirrored && marker.NativeSurface != null) ExportPoses(wing, wingVisual, marker, preview, catalog);
                else
                {
                    marker.Advance(1f / 60f);
                    Check(Mathf.Abs(marker.CurrentAngle - .75f) < .001f, "Gradual opening: " + label);
                    marker.Advance(1f);
                }
                NativeUpdate(wingVisual);
                Check(marker.CurrentAngle == 30f, "Full 30 degree detent: " + label);
                if (marker.NativeSurface != null)
                {
                    Check(wing.GetComponent<MeshFilter>().sharedMesh == stock, "Native wing skin is retained: " + label);
                    Check(marker.NativeSurface.EdgeDisplacement(plane) < -.0001f, "Hinge moves toward wing underside: " + label);
                    CheckSteering(wingVisual, marker, label);
                    var rotation=wing.transform.rotation;
                    var forward=wing.orientation.forward;body.linearVelocity=forward*28f;plane.gravityForce=9.81f;
                    var priorForce=body.GetAccumulatedForce();var priorTorque=body.GetAccumulatedTorque();
                    marker.NativeSurface.ApplyForces(plane,30f);
                    var applied=body.GetAccumulatedForce()-priorForce;
                    Check(Vector3.Dot(applied,forward)<0 && (body.GetAccumulatedTorque()-priorTorque).sqrMagnitude<.00001f,"Deployment adds drag without uncommanded torque: "+label);
                    wing.transform.rotation=Quaternion.FromToRotation(wing.orientation.up,plane.transform.right)*rotation;
                    forward=wing.orientation.forward;body.linearVelocity=forward*28f;priorForce=body.GetAccumulatedForce();
                    marker.NativeSurface.ApplyForces(plane,30f);applied=body.GetAccumulatedForce()-priorForce;
                    Check(Vector3.ProjectOnPlane(applied,forward).magnitude<.001f,"Vertical installation adds drag without sideways lift: "+label);
                    wing.transform.rotation=rotation;body.linearVelocity=Vector3.zero;
                }
                else Check(wing.GetComponent<MeshFilter>().sharedMesh != stock, "Dedicated inboard geometry: " + label);

                using (var saved = new MemoryStream())
                {
                    WingUpgradeSaveCodec.Append(saved, WingUpgradePersistence.Capture(storage));
                    Check(WingUpgradeSaveCodec.TryRead(saved, out var entries) && entries.Count == 1 &&
                        entries[0].wingType == WingFlapIds.Of(prefab.name), "Upgrade persists with stable part identity: " + label);
                    FlapsPlugin.SelectStage(0);
                    marker.Advance(1f);
                    NativeUpdate(wingVisual);
                    Check(marker.CurrentAngle == 0f && (marker.NativeSurface == null ||
                        Mathf.Abs(marker.NativeSurface.EdgeDisplacement(plane)) < .0001f), "Full retraction restores native surface: " + label);
                    FlapsPlugin.SelectStage(3);
                    marker.Advance(1f);
                    NativeUpdate(wingVisual);
                    upgrade.Restore(basePrice, WingUpgradeOptions.None);
                    Check(!marker.IsReady && wing.GetComponent<MeshFilter>().sharedMesh == (FlapsPlugin.SupportsSpoilers(wing)?WingGeometryCache.Stowed(WingGeometryCache.Request(stock,true)):stock) && (marker.NativeSurface == null ||
                        Mathf.Abs(marker.NativeSurface.EdgeDisplacement(plane)) < .0001f), "Removing deployed upgrade restores the stock state: " + label);
                    WingUpgradePersistence.Restore(storage, entries);
                    Check(marker.IsReady && upgrade.HasFlaps && !upgrade.HasSpoilers, "Saved choice restores: " + label);
                }
                FlapsPlugin.SelectStage(0);
                plane.planeParts.Remove(wing);
                DestroyImmediate(wing.gameObject);
            }
        }
        Check(catalog.wings.Count == 5, "Native preview includes all five new shared surface wings");
        File.WriteAllText(Path.Combine(Host, "native-flap-poses.json"), SerializeCatalog(catalog));
    }

    void CheckSettings(Wing wing, string label)
    {
        var source = Resources.FindObjectsOfTypeAll<PartSettingsPanel>()[0];
        var panel = Instantiate(source);
        panel.enabled = false;
        try
        {
            panel.OpenSettings(wing, false);
            var flapRow = panel.content.Find("Flap Mod Flaps purchase");
            Check(flapRow && flapRow.GetComponent<Toggle>() && !flapRow.GetComponent<Toggle>().isOn,
                "Editor opens the native wing settings with a Flaps purchase: " + label);
            var spoilerRow = panel.content.Find("Flap Mod Spoilers purchase");
            Check((bool)spoilerRow == FlapsPlugin.SupportsSpoilers(wing), "Spoiler purchase availability: " + label);
        }
        finally { DestroyImmediate(panel.gameObject); }
    }

    void CheckSteering(WingVisual visual, FlapMarker marker, string label)
    {
        var steering = AccessTools.Field(typeof(WingVisual), "angle");
        var target = AccessTools.Field(typeof(WingVisual), "targetAngle");
        var cached = AccessTools.Field(typeof(WingVisual), "localRotation");
        foreach (float input in new[] { 9f, -9f })
        {
            target.SetValue(visual, input);
            steering.SetValue(visual, input);
            NativeUpdate(visual);
            Vector3 axes = (Vector3)cached.GetValue(visual);
            float applied = (float)steering.GetValue(visual) + marker.NativeSurface.DeflectionSign(plane) * 30f;
            Check(Quaternion.Angle(Quaternion.Euler(axes.x, axes.y, applied), visual.controlSurface.localRotation) < .1f,
                "Native steering composes with flap deflection " + input + ": " + label);
        }
    }

    void CheckInputs()
    {
        var prefab = Resources.FindObjectsOfTypeAll<Wing>().First(w => w.name == "Wing" && !w.gameObject.scene.IsValid());
        var wing = Clone(prefab, false);
        var upgrade = WingUpgrades.Ensure(wing);
        upgrade.Restore(upgrade.BasePrice, WingUpgradeOptions.Flaps);
        var behavior = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var gamepad = InputSystem.AddDevice<Gamepad>();
        keyboard.MakeCurrent();
        gamepad.MakeCurrent();
        try
        {
            void Stick(float x, float y)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(x, y) });
                InputSystem.Update();
                Tick();
            }
            void KeyPress(params Key[] keys)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                InputSystem.Update();
                Tick();
            }

            Stick(0, -1); Check(FlapsPlugin.stage == 0, "Controller held on entry requires neutral");
            Stick(0, 0); Stick(1, 0); Check(FlapsPlugin.stage == 0, "Horizontal stick does not change flaps");
            Stick(0, -.4f); Check(FlapsPlugin.stage == 0, "Small right stick motion is ignored");
            Stick(0, -1); Check(FlapsPlugin.stage == 1, "Right stick down extends one detent");
            Stick(0, -1); Stick(0, 1); Check(FlapsPlugin.stage == 1, "A held or reversed stick cannot repeat without neutral");
            Stick(0, 0); Stick(0, -1); Check(FlapsPlugin.stage == 2, "Neutral lets the next push extend one more detent");
            manager.modalsOpen = true;
            Stick(0, 0); Stick(0, -1);
            manager.modalsOpen = false;
            Stick(0, -1); Check(FlapsPlugin.stage == 2, "Holding the stick through a modal cannot move flaps");
            Stick(0, 0); Stick(0, 1); Check(FlapsPlugin.stage == 1, "Right stick up retracts one detent");
            KeyPress(Key.RightBracket); Check(FlapsPlugin.stage == 2, "Right bracket still extends with a connected gamepad");
            KeyPress(); KeyPress(Key.LeftBracket); Check(FlapsPlugin.stage == 1, "Left bracket still retracts with a connected gamepad");
            KeyPress(); Stick(0, 0); Stick(0, 1); Stick(0, 0); Stick(0, 1);
            Check(FlapsPlugin.stage == 0, "Selector clamps at the retracted detent");
        }
        finally
        {
            InputSystem.RemoveDevice(gamepad);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = behavior;
        }
    }

    Wing Clone(Wing prefab, bool mirrored)
    {
        var wing = Instantiate(prefab.gameObject, plane.transform).GetComponent<Wing>();
        foreach (var script in wing.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
        foreach (var collider in wing.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        wing.transform.localPosition = Vector3.zero;
        wing.transform.localRotation = Quaternion.identity;
        if (mirrored) wing.transform.localScale = Vector3.Scale(wing.transform.localScale, new Vector3(1, -1, 1));
        wing.enabled = true;
        wing.rb = body;
        plane.planeParts.Add(wing);
        return wing;
    }

    static void NativeUpdate(WingVisual visual) => AccessTools.Method(typeof(WingVisual), "Update").Invoke(visual, null);
    static void Tick() => AccessTools.Method(typeof(FlapsPlugin), "Update").Invoke(FlapsPlugin.instance, null);

    void Check(bool condition, string check)
    {
        if (!condition) throw new InvalidOperationException(check);
        results.Add("PASS " + check);
    }
}

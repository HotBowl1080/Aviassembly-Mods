using System;
using System.Collections.Generic;

using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[BepInPlugin("com.andrewstack.aviassembly.flaps", "Flap Mod", "0.24.0")]
public class FlapsPlugin : BaseUnityPlugin
{
    internal static int stage { get; private set; }
    internal static readonly List<FlapMarker> fittedWings = new List<FlapMarker>();
    internal static FlyingUIController flightUI;
    internal static GameObject template;
    internal static FlapsPlugin instance;
    internal static PlaneContainer activePlane;
    internal static bool spoilersRequested;
    internal static bool Supports(Wing wing) { return wing && HingedWingProfile.Code(wing.name)!=0; }
    internal static bool SupportsSpoilers(Wing wing) { return wing && wing.name.StartsWith("BigWing"); }
    internal static void LogDetail(string message) { if (instance) instance.Logger.LogInfo(message); }
    void Awake() { instance = this; new Harmony("com.andrewstack.aviassembly.flaps").PatchAll(); }
    void Update()
    {
        var controls=InputManager.Instance;
        float largestThrottle=0f;
        if(activePlane) foreach(PlanePart part in activePlane.planeParts)
        {
            Engine engine=part as Engine;
            if(engine && engine.isActiveAndEnabled) largestThrottle=Mathf.Max(largestThrottle,engine.currentThrottle);
        }
        spoilersRequested=activePlane && activePlane.FlightModeInitialized && controls &&
            controls.throttleInput<0f && largestThrottle<=.02f &&
            !(flightUI && flightUI.UIOpen);
        if (Input.GetKeyDown(KeyCode.RightBracket)) SelectStage(stage + 1);
        if (Input.GetKeyDown(KeyCode.LeftBracket)) SelectStage(stage - 1);
    }
    internal static bool ShowSelector
    {
        get
        {
            if (!activePlane || !activePlane.FlightModeInitialized || (flightUI && flightUI.UIOpen)) return false;
            foreach (FlapMarker marker in fittedWings)
                if (marker && marker.HasFlaps && marker.CanOperate(activePlane)) return true;
            return false;
        }
    }
    internal static void SelectStage(int desired)
    {
        if (!ShowSelector || Time.timeScale <= 0f) return;
        var input=InputManager.Instance;
        if (input && input.currentInputBlocker) return;
        stage=Mathf.Clamp(desired,0,BigWingPreset.FlapAngles.Length-1);
    }
    internal static GameObject FindTemplate()
    {
        if (template) return template;
        foreach (Engine e in Resources.FindObjectsOfTypeAll<Engine>())
            if (e && e.settingsPrefab && e.settingsPrefab.GetComponent<EngineSettings>()) return template = e.settingsPrefab;
        return null;
    }
}

public class FlapMarker : MonoBehaviour
{
    MeshFilter filter;
    Mesh original, deployed;
    Wing wing;
    PlaneContainer owner;
    BigWingGeometry geometry;
    StraightWingGeometry straight;
    WingUpgrades upgrades;
    float angle;
    float spoilerFraction;
    internal float SpoilerFraction { get { return spoilerFraction; } }
    public bool IsReady { get { return enabled && upgrades && upgrades.Options!=WingUpgradeOptions.None && (geometry != null || straight != null); } }
    internal bool HasFlaps { get { return upgrades && upgrades.HasFlaps; } }
    internal bool HasSpoilers { get { return upgrades && upgrades.HasSpoilers; } }
    internal bool IsStraight { get { return straight != null; } }
    internal float CurrentAngle { get { return angle; } }
    internal float FlapAreaFraction { get { return straight==null?0f:straight.FlapAreaFraction; } }
    internal bool CanOperate(PlaneContainer plane) { return IsReady && owner==plane && isActiveAndEnabled && wing && wing.isActiveAndEnabled && wing.rb; }

    WingGeometryCache.Entry prepared;
    internal bool Preparing { get; private set; }
    internal Mesh NativeMesh { get { return original; } }
    bool uploaded;
    float uploadedAngle,uploadedSpoilers;
    Vector3 uploadedDown;

    bool BindNative()
    {
        wing=GetComponent<Wing>();upgrades=GetComponent<WingUpgrades>();filter=GetComponent<MeshFilter>();
        if(!wing || !filter || !filter.sharedMesh)return false;
        if(!original)original=WingGeometryCache.NativeMesh(upgrades && upgrades.StockMesh?upgrades.StockMesh:filter.sharedMesh);
        if(!original || !original.isReadable || original.subMeshCount<1 || original.subMeshCount>2)return false;
        if(prepared==null)prepared=WingGeometryCache.Request(original,FlapsPlugin.SupportsSpoilers(wing));
        return prepared!=null;
    }
    void Start()
    {
        wing=GetComponent<Wing>();upgrades=GetComponent<WingUpgrades>();
        if(!upgrades || upgrades.Options==WingUpgradeOptions.None){ShowDefaultModel();return;}
        InitializeGeometry();SynchronizeOwnership();
    }
    internal void ShowDefaultModel()
    {
        wing=GetComponent<Wing>();upgrades=GetComponent<WingUpgrades>();
        if(!FlapsPlugin.SupportsSpoilers(wing)){enabled=false;return;}
        // Default appearance changes no ownership, price, lift or drag.
        enabled=true;
        if(upgrades && upgrades.Options!=WingUpgradeOptions.None)return;
        if(!BindNative())return;
        var mesh=WingGeometryCache.Stowed(prepared);Preparing=mesh==null && !prepared.build.IsCompleted;
        if(mesh){if(filter.sharedMesh!=mesh)filter.sharedMesh=mesh;enabled=false;}
    }
    internal bool InitializeGeometry()
    {
        if(geometry!=null || straight!=null)return true;
        try{
            if(!BindNative())return false;
            var ready=WingGeometryCache.Ready(prepared);
            Preparing=ready==null && !prepared.build.IsCompleted;if(ready==null)return false;
            if(ready.big!=null){geometry=new BigWingGeometry(ready.big);}
            else straight=new StraightWingGeometry(ready.straight);
            var positions=geometry!=null?geometry.positions:straight.positions;
            var normals=geometry!=null?geometry.normals:straight.normals;
            deployed=WingGeometryCache.MakeMesh(ready,positions,normals,original.bounds,true);
            filter.sharedMesh=deployed;owner=GetComponentInParent<PlaneContainer>();FlapsPlugin.activePlane=owner;
            uploaded=true;uploadedAngle=uploadedSpoilers=0f;uploadedDown=Vector3.down;
            return true;
        }catch(Exception ex){FlapsPlugin.LogDetail("Flap geometry setup failed: "+ex);return false;}
    }
    internal void SynchronizeOwnership()
    {
        wing=GetComponent<Wing>();upgrades=GetComponent<WingUpgrades>();
        if(!HasFlaps)angle=0f;if(!HasSpoilers)spoilerFraction=0f;
        bool installed=upgrades && upgrades.Options!=WingUpgradeOptions.None;
        if(!installed){
            FlapsPlugin.fittedWings.Remove(this);
            if(FlapsPlugin.SupportsSpoilers(wing))ShowDefaultModel();
            else{enabled=false;if(filter && original)filter.sharedMesh=original;}
            return;
        }
        enabled=true;if(geometry==null && straight==null && !InitializeGeometry())return;
        if(!FlapsPlugin.fittedWings.Contains(this))FlapsPlugin.fittedWings.Add(this);
        Vector3 down=owner && owner.FlightModeInitialized && wing.rb?transform.InverseTransformVector(-wing.rb.transform.up).normalized:Vector3.down;
        UploadPose(down);
    }
    void UploadPose(Vector3 down)
    {
        if(uploaded && uploadedAngle==angle && uploadedSpoilers==spoilerFraction && (uploadedDown-down).sqrMagnitude<1e-10f){filter.sharedMesh=deployed;return;}
        if(IsStraight){straight.Pose(angle,down);deployed.vertices=straight.positions;deployed.normals=straight.normals;}
        else{geometry.Pose(angle,down,spoilerFraction);deployed.vertices=geometry.positions;deployed.normals=geometry.normals;}
        filter.sharedMesh=deployed;uploaded=true;uploadedAngle=angle;uploadedSpoilers=spoilerFraction;uploadedDown=down;
    }
    void LateUpdate()
    {
        if(!upgrades || upgrades.Options==WingUpgradeOptions.None){ShowDefaultModel();return;}
        if(geometry==null && straight==null){if(InitializeGeometry())SynchronizeOwnership();return;}
        if(!IsReady || !wing)return;
        bool flying=wing.rb && owner && owner.FlightModeInitialized;
        float next=flying && HasFlaps?Mathf.MoveTowards(angle,BigWingPreset.Angle(FlapsPlugin.stage),45f*Time.deltaTime):0f;
        float nextSpoiler=flying && HasSpoilers?Mathf.MoveTowards(spoilerFraction,FlapsPlugin.spoilersRequested?1f:0f,3f*Time.deltaTime):0f;
        if(next==angle && nextSpoiler==spoilerFraction)return;
        angle=next;spoilerFraction=nextSpoiler;
        Vector3 down=flying?transform.InverseTransformVector(-wing.rb.transform.up).normalized:Vector3.down;UploadPose(down);
    }
    void OnDestroy()
    {
        FlapsPlugin.fittedWings.Remove(this);
        if(filter && deployed && filter.sharedMesh==deployed) filter.sharedMesh=original;
        if(deployed) Destroy(deployed);
    }
}

[HarmonyPatch(typeof(PlacementUI), "LateUpdate")]
public static class EnableWingSettings
{
    static void Prefix(PlacementUI __instance)
    {
        if (!__instance.placer || !__instance.placer.currentSelectedPart) return;
        Wing wing = __instance.placer.currentSelectedPart.GetComponentInChildren<Wing>();
        if (FlapsPlugin.Supports(wing) && wing.GetComponentInChildren<WingVisual>() && !wing.settingsPrefab)
            wing.settingsPrefab = FlapsPlugin.FindTemplate();
    }
}

public sealed class FlapOptionCircle : MaskableGraphic
{
    public bool outline;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        Draw(mesh,rectTransform.rect,color,outline);
    }

    internal static void Draw(VertexHelper mesh,Rect rect,Color32 ink,bool outline)
    {
        mesh.Clear();
        const int segments=32;
        Vector2 center=rect.center;
        float r=Mathf.Min(rect.width,rect.height)*.5f;
        Color32 clear=ink;
        clear.a=0;
        // Feather both visible contours. The center of the selected dot is opaque.
        for(int i=0;i<segments;i++)
        {
            double a=i*2.0*Math.PI/segments;
            Vector2 dir=new Vector2((float)Math.Cos(a),(float)Math.Sin(a));
            mesh.AddVert(center+dir*(outline ? r*.70f : 0f),outline ? clear : ink,Vector2.zero);
            mesh.AddVert(center+dir*(outline ? r*.80f : r*.5f),ink,Vector2.zero);
            mesh.AddVert(center+dir*(r-.65f),ink,Vector2.zero);
            mesh.AddVert(center+dir*r,clear,Vector2.zero);
            int start=i*4, next=((i+1)%segments)*4;
            for(int band=0;band<3;band++) {
                mesh.AddTriangle(start+band,next+band+1,start+band+1);
                mesh.AddTriangle(start+band,next+band,next+band+1);
            }
        }
    }
}

[HarmonyPatch(typeof(Wing), "UpdatePart")]
public static class FlapPhysics
{
    static void Prefix(Wing __instance,out float __state)
    {
        __state=__instance.liftForce;
        var marker=__instance.GetComponent<FlapMarker>();
        if(marker && marker.IsReady) __instance.liftForce*=1f-.55f*marker.SpoilerFraction;
    }
    static void Postfix(Wing __instance, PlaneContainer __0,float __state)
    {
        __instance.liftForce=__state;
        FlapMarker marker = __instance.GetComponent<FlapMarker>();
        if (!marker || !marker.IsReady || !__instance.rb || !__instance.orientation) return;
        if(marker.IsStraight){
            if(marker.CurrentAngle<=0f || !__0 || !__0.FlightModeInitialized || !marker.CanOperate(__0))return;
            float weight=__instance.GetWingArea()*Mathf.Max(0f,__instance.liftForce),total=0f;
            foreach(PlanePart part in __0.planeParts){
                Wing other=part as Wing;
                if(other && other.isActiveAndEnabled && other.gameObject.activeInHierarchy && other.rb==__instance.rb)
                    total+=other.GetWingArea()*Mathf.Max(0f,other.liftForce);
            }
            var force=StraightWingAerodynamics.Evaluate(
                __instance.rb.GetPointVelocity(__instance.orientation.position),
                __instance.orientation.forward,__instance.orientation.right,
                Mathf.Sign(__instance.transform.localScale.y),marker.CurrentAngle,marker.FlapAreaFraction,
                __instance.GetWingArea(),__instance.liftForce,__instance.dragForce,
                __0.GravityMultiplier,__0.liftMultiplier,__instance.rb.mass,__0.RealGravity,
                total>0f?weight/total:0f);
            // Match the game's balanced lift origin. Drag acts through the
            // rigidbody COM instead of creating a new wing-pivot pitch moment.
            __instance.rb.AddForceAtPosition(force.lift,__0.GetLiftOrigin());
            __instance.rb.AddForce(force.drag);
            return;
        }
        float spoiler=marker.SpoilerFraction;
        __0.airbreakDrag+=.65f*spoiler;
        if(spoiler>0f && __0.IsGrounded())
        {
            Vector3 groundVelocity=Vector3.ProjectOnPlane(__instance.rb.linearVelocity,Vector3.up);
            float deceleration=Mathf.Min(2f,groundVelocity.magnitude/(Time.fixedDeltaTime*Mathf.Max(1,FlapsPlugin.fittedWings.Count)));
            __instance.rb.AddForce(-groundVelocity.normalized*deceleration*spoiler,ForceMode.Acceleration);
        }
        if(!marker.HasFlaps || FlapsPlugin.stage==0) return;
        Vector3 velocity = __instance.rb.GetPointVelocity(__instance.orientation.position);
        float speed = velocity.magnitude;
        if (speed < 0.1f) return;
        float fraction = BigWingPreset.Angle(FlapsPlugin.stage) / 30f;
        float strength = speed * speed * __instance.GetWingArea() * __instance.liftForce * __0.GravityMultiplier * __0.liftMultiplier;
        float slatLift=marker.IsStraight?0f:.08f*BigWingPreset.SlatExtension(BigWingPreset.Angle(FlapsPlugin.stage));
        Vector3 lift = Vector3.ProjectOnPlane(__instance.rb.transform.up, velocity.normalized).normalized * strength * (.25f*fraction+slatLift)*(1f-.55f*spoiler);
        Vector3 drag = -velocity.normalized * strength * 0.18f * fraction;
        __instance.rb.AddForceAtPosition(lift + drag, __instance.orientation.position);
    }
}

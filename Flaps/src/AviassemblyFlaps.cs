using System;
using System.Collections.Generic;

using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[BepInPlugin("com.andrewstack.aviassembly.flaps", "Aviassembly Flaps", "0.11.0")]
public class FlapsPlugin : BaseUnityPlugin
{
    internal static int stage { get; private set; }
    internal static readonly List<FlapMarker> fittedWings = new List<FlapMarker>();
    internal static FlyingUIController flightUI;
    internal static GameObject template;
    internal static FlapsPlugin instance;
    internal static PlaneContainer activePlane;
    internal static bool spoilersRequested;
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
                if (marker && marker.CanOperate(activePlane)) return true;
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
    float angle;
    float spoilerFraction;
    internal float SpoilerFraction { get { return spoilerFraction; } }
    public bool IsReady { get { return geometry != null; } }
    internal bool CanOperate(PlaneContainer plane) { return owner==plane && isActiveAndEnabled && wing && wing.isActiveAndEnabled && wing.rb; }

    void Start()
    {
        try {
            wing=GetComponent<Wing>();
            filter=GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable) return;
            original=filter.sharedMesh;
            if(original.subMeshCount!=2) return;
            var shape=new BigWingGeometry(original.vertices, original.normals, original.uv,
                original.GetTriangles(0),original.GetTriangles(1));
            shape.Pose(0f,Vector3.down,0f);
            var tex=new Vector2[shape.vertices.Count];
            for(int i=0;i<tex.Length;i++) tex[i]=shape.vertices[i].uv;
            deployed=new Mesh();
            deployed.name="Big Wing deployed";
            deployed.MarkDynamic();
            deployed.vertices=shape.positions;
            deployed.normals=shape.normals;
            deployed.uv=tex;
            deployed.subMeshCount=2;
            for(int i=0;i<2;i++) deployed.SetTriangles(shape.triangles[i].ToArray(),i,false);
            Bounds bounds=original.bounds;
            bounds.Expand(.9f);
            deployed.bounds=bounds;
            geometry=shape;
            filter.sharedMesh=deployed;
            owner=GetComponentInParent<PlaneContainer>();
            FlapsPlugin.activePlane=owner;
            FlapsPlugin.fittedWings.Add(this);
        } catch(Exception ex) { FlapsPlugin.LogDetail("Flap geometry setup failed: "+ex); }
    }
    void LateUpdate()
    {
        if(!IsReady || !wing) return;
        bool flying=wing.rb && FlapsPlugin.activePlane && FlapsPlugin.activePlane.FlightModeInitialized;
        float next=flying ? Mathf.MoveTowards(angle,BigWingPreset.Angle(FlapsPlugin.stage),45f*Time.deltaTime) : 0f;
        float nextSpoiler=flying ? Mathf.MoveTowards(spoilerFraction,FlapsPlugin.spoilersRequested?1f:0f,3f*Time.deltaTime) : 0f;
        if(next==angle && nextSpoiler==spoilerFraction) return;
        angle=next;
        spoilerFraction=nextSpoiler;
        Vector3 localDown=flying?transform.InverseTransformVector(-wing.rb.transform.up).normalized:Vector3.down;
        geometry.Pose(angle,localDown,spoilerFraction);
        deployed.vertices=geometry.positions;
        deployed.normals=geometry.normals;
        if(filter.sharedMesh!=deployed) filter.sharedMesh=deployed;
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
        if (wing && wing.name.StartsWith("BigWing") && wing.GetComponentInChildren<WingVisual>() && !wing.settingsPrefab)
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

[HarmonyPatch(typeof(PartSettingsPanel), "OpenSettings")]
public static class WingSettings
{
    static FlapOptionCircle Circle(RectTransform parent,bool outline,float size)
    {
        var obj=new GameObject(outline ? "Flap outline" : "Flap dot",typeof(RectTransform),typeof(FlapOptionCircle));
        var graphic=obj.GetComponent<FlapOptionCircle>();
        graphic.outline=outline;
        graphic.color=outline ? new Color(.34f,.34f,.34f) : new Color(.12f,.12f,.12f);
        graphic.raycastTarget=false;
        RectTransform rect=graphic.rectTransform;
        rect.SetParent(parent,false); rect.anchorMin=rect.anchorMax=new Vector2(0f,.5f);
        rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=new Vector2(17f,0f); rect.sizeDelta=new Vector2(size,size);
        return graphic;
    }

    static bool Prefix(PartSettingsPanel __instance, PlanePart __0, bool __1)
    {
        Wing wing = __0 as Wing;
        if (!wing || !wing.name.StartsWith("BigWing") || __1) return true;
        GameObject template = FlapsPlugin.FindTemplate();
        if (!template) return true;
        if (__instance.content) UnityEngine.Object.Destroy(__instance.content.gameObject);
        GameObject panel = UnityEngine.Object.Instantiate(template);
        panel.transform.SetParent(__instance.transform, false);
        panel.transform.localScale = Vector3.one;
        __instance.content = (RectTransform)panel.transform;
        EngineSettings settings = panel.GetComponent<EngineSettings>();
        settings.enabled = false;
        Toggle source=settings.invertToggle;
        TMP_Text label=Array.Find(panel.GetComponentsInChildren<TMP_Text>(true), t=>t.name=="InvertLabel");
        if (!label) throw new InvalidOperationException("Big Wing settings label is missing");
        RectTransform body=__instance.content;
        foreach (LayoutGroup layout in panel.GetComponents<LayoutGroup>()) layout.enabled=false;
        ContentSizeFitter fitter=panel.GetComponent<ContentSizeFitter>();
        if(fitter) fitter.enabled=false;
        for(int i=0;i<body.childCount;i++) body.GetChild(i).gameObject.SetActive(false);
        body.sizeDelta=new Vector2(154f,32f);
        var row=new GameObject("Big Wing flap setting",typeof(RectTransform),typeof(Image),typeof(Toggle));
        RectTransform line=(RectTransform)row.transform;
        line.SetParent(body,false);
        line.anchorMin=Vector2.zero; line.anchorMax=Vector2.one;
        line.offsetMin=line.offsetMax=Vector2.zero;
        row.GetComponent<Image>().color=Color.clear;
        Toggle toggle=row.GetComponent<Toggle>();
        FlapOptionCircle box=Circle(line,true,20f);
        FlapOptionCircle tick=Circle(box.rectTransform,false,10f);
        tick.rectTransform.anchorMin=tick.rectTransform.anchorMax=new Vector2(.5f,.5f);
        tick.rectTransform.anchoredPosition=Vector2.zero;
        toggle.targetGraphic=box;
        toggle.graphic=tick;
        toggle.colors=source.colors;
        toggle.toggleTransition=Toggle.ToggleTransition.None;
        label.transform.SetParent(line,false);
        label.gameObject.SetActive(true);
        RectTransform labelRect=label.rectTransform;
        labelRect.anchorMin=Vector2.zero; labelRect.anchorMax=Vector2.one;
        labelRect.offsetMin=new Vector2(36f,0f); labelRect.offsetMax=new Vector2(-4f,0f);
        labelRect.localScale=Vector3.one;
        label.margin=Vector4.zero;
        label.text="Enable flaps";
        label.alignment=TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing=true; label.fontSizeMin=12f; label.fontSizeMax=18f;
        label.raycastTarget=false;
        toggle.SetIsOnWithoutNotify(wing.GetComponent<FlapMarker>());
        // The native Toggle can skip its graphic update when the initial value is already false.
        tick.canvasRenderer.SetAlpha(toggle.isOn ? 1f : 0f);

        toggle.onValueChanged.AddListener(delegate(bool enabled) {
            if (!wing) return;
            FlapMarker mark = wing.GetComponent<FlapMarker>();
            if (enabled && !mark) wing.gameObject.AddComponent<FlapMarker>();
            if (!enabled && mark) UnityEngine.Object.Destroy(mark);
        });
        __instance.Update();
        return false;
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
        float spoiler=marker.SpoilerFraction;
        __0.airbreakDrag+=.65f*spoiler;
        if(spoiler>0f && __0.IsGrounded())
        {
            Vector3 groundVelocity=Vector3.ProjectOnPlane(__instance.rb.linearVelocity,Vector3.up);
            float deceleration=Mathf.Min(2f,groundVelocity.magnitude/(Time.fixedDeltaTime*Mathf.Max(1,FlapsPlugin.fittedWings.Count)));
            __instance.rb.AddForce(-groundVelocity.normalized*deceleration*spoiler,ForceMode.Acceleration);
        }
        if(FlapsPlugin.stage==0) return;
        Vector3 velocity = __instance.rb.GetPointVelocity(__instance.orientation.position);
        float speed = velocity.magnitude;
        if (speed < 0.1f) return;
        float fraction = BigWingPreset.Angle(FlapsPlugin.stage) / 30f;
        float strength = speed * speed * __instance.GetWingArea() * __instance.liftForce * __0.GravityMultiplier * __0.liftMultiplier;
        float slatLift=.08f*BigWingPreset.SlatExtension(BigWingPreset.Angle(FlapsPlugin.stage));
        Vector3 lift = Vector3.ProjectOnPlane(__instance.rb.transform.up, velocity.normalized).normalized * strength * (.25f*fraction+slatLift)*(1f-.55f*spoiler);
        Vector3 drag = -velocity.normalized * strength * 0.18f * fraction;
        __instance.rb.AddForceAtPosition(lift + drag, __instance.orientation.position);
    }
}






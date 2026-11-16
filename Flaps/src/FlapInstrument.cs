using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[HarmonyPatch(typeof(SpeedDial), "Start")]
internal static class AttachFlapInstrument
{
    static void Postfix(SpeedDial __instance)
    {
        if (!__instance.GetComponent<FlapInstrument>())
            __instance.gameObject.AddComponent<FlapInstrument>().Attach(__instance);
    }
}

// Layout units are in the native 274-unit airspeed dial's coordinate system.
public sealed class FlapInstrument : MonoBehaviour
{
    internal const float Width=80f, Height=216f, Gap=14f;
    internal const float RowHeight=41f, FirstRowTop=35f;
    SpeedDial dial;
    RectTransform face;
    FlapInstrumentPlate plate;
    int displayed=-1;
    internal void Attach(SpeedDial speedDial)
    {
        dial=speedDial;
        FlapsPlugin.flightUI=dial.GetComponentInParent<FlyingUIController>();
    }

    static RectTransform Child(RectTransform parent,string name)
    {
        var obj=new GameObject(name,typeof(RectTransform));
        var rect=(RectTransform)obj.transform;
        rect.SetParent(parent,false);
        return rect;
    }

    static void Fit(RectTransform child, float top, float height, float inset)
    {
        child.anchorMin=new Vector2(0f,1f);
        child.anchorMax=Vector2.one;
        child.pivot=new Vector2(.5f,1f);
        child.anchoredPosition=new Vector2(0f,-top);
        child.sizeDelta=new Vector2(-2f*inset,height);
    }

    static void Caption(RectTransform rect,string text,float size,TMP_Text example,bool heading)
    {
        TextMeshProUGUI label=rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (example && example.font) label.font=example.font;
        label.alignment=TextAlignmentOptions.Midline;
        label.fontStyle=heading ? FontStyles.Bold : FontStyles.Normal;
        label.fontSize=size;
        label.color=heading ? new Color32(83,83,83,255) : new Color32(41,41,41,255);
        label.text=text;
        label.raycastTarget=false;
    }

    void Build()
    {
        var dialRect=(RectTransform)dial.transform;
        face=Child(dialRect,"Flap selector");
        face.anchorMin=face.anchorMax=new Vector2(0f,.5f);
        face.pivot=new Vector2(1f,.5f);
        face.anchoredPosition=new Vector2(-Gap,0f);
        face.sizeDelta=new Vector2(Width,Height);
        plate=face.gameObject.AddComponent<FlapInstrumentPlate>();
        plate.raycastTarget=false;

        var nativeLabel=dialRect.Find("Labels");
        TMP_Text heading=nativeLabel ? nativeLabel.GetComponent<TMP_Text>() : dial.knotsMeter;
        var nativeNumber=dialRect.Find("Speed");
        TMP_Text digits=nativeNumber ? nativeNumber.GetComponent<TMP_Text>() : dial.knotsMeter;
        var title=Child(face,"Title");
        Fit(title,5f,28f,4f);
        Caption(title,"FLAPS",17.5f,heading,true);

        for(int stage=0;stage<4;stage++)
        {
            int selected=stage;
            var row=Child(face,"Flaps "+stage*10);
            Fit(row,FirstRowTop+stage*RowHeight,RowHeight,6f);
            var hit=row.gameObject.AddComponent<Image>();
            hit.color=Color.white;
            var button=row.gameObject.AddComponent<Button>();
            button.targetGraphic=hit;
            var colors=ColorBlock.defaultColorBlock;
            colors.normalColor=colors.selectedColor=Color.clear;
            colors.highlightedColor=new Color(1,1,1,.18f);
            colors.pressedColor=new Color(0,0,0,.10f);
            colors.fadeDuration=.06f;
            button.colors=colors;
            hit.canvasRenderer.SetColor(Color.clear);
            var navigation=button.navigation;
            navigation.mode=Navigation.Mode.None;
            button.navigation=navigation;
            button.onClick.AddListener(delegate { FlapsPlugin.SelectStage(selected); Sync(); });
            var number=Child(row,"Degrees");
            Fit(number,0f,RowHeight,2f);
            Caption(number,(stage*10).ToString()+"�",22f,digits,false);
        }
    }

    void Sync()
    {
        if (displayed==FlapsPlugin.stage) return;
        displayed=FlapsPlugin.stage;
        plate.SetSelection(displayed);
    }

    void LateUpdate()
    {
        if (!dial) return;
        bool visible=FlapsPlugin.ShowSelector;
        if (visible && !face) Build();
        if (!face) return;
        if (face.gameObject.activeSelf!=visible) face.gameObject.SetActive(visible);
        if (visible) Sync();
    }
    void OnDisable() { if(face) face.gameObject.SetActive(false); }
    void OnDestroy() { if(face) Destroy(face.gameObject); }
}

// The scale, bezel and orange detent are one canvas graphic. Only a new command
// or layout change rebuilds its mesh. Buttons on top handle normal HUD clicks.
public sealed class FlapInstrumentPlate : MaskableGraphic
{
    int selection;
    internal void SetSelection(int stage)
    {
        selection=stage;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        Paint(mesh,rectTransform.rect,selection);
    }

    internal static void Paint(VertexHelper mesh, Rect rect,int stage)
    {
        mesh.Clear();
        float x=rect.xMin, y=rect.yMin, w=rect.width, h=rect.height;
        Rounded(mesh,x,y-1.5f,w,h,8,new Color32(40,39,42,165));
        Rounded(mesh,x,y,w,h,8,new Color32(250,250,248,255));
        Rounded(mesh,x+3.8f,y+3.8f,w-7.6f,h-7.6f,4.5f,new Color32(149,149,149,255));
        Rounded(mesh,x+6f,y+6f,w-12f,h-12f,2.5f,new Color32(197,197,197,255));
        Rounded(mesh,x+9f,rect.yMax-34f,w-18f,1.6f,.7f,new Color32(163,163,163,255));
        for(int i=0;i<4;i++)
        {
            float cy=rect.yMax-FlapInstrument.FirstRowTop-(i+.5f)*FlapInstrument.RowHeight;
            if(i==stage)
                Rounded(mesh,x+6f,cy-13.5f,w-12f,27f,2f,new Color32(248,170,68,255));
            Color32 tick=i==stage ? new Color32(133,88,35,255) : new Color32(144,144,144,255);
            Rounded(mesh,x+6f,cy-2f,6f,4f,1f,tick);
            Rounded(mesh,rect.xMax-12f,cy-2f,6f,4f,1f,tick);
        }
    }
    static void Rounded(VertexHelper mesh,float x,float y,float width,float height,float radius,Color32 ink)
    {
        int middle=mesh.currentVertCount;
        mesh.AddVert(new Vector3(x+width*.5f,y+height*.5f),ink,Vector2.zero);
        const int pointsPerCorner=5, perimeter=4*pointsPerCorner;
        for(int corner=0;corner<4;corner++)
        {
            float cx=(corner==0 || corner==3) ? x+width-radius : x+radius;
            float cy=corner<2 ? y+height-radius : y+radius;
            for(int k=0;k<pointsPerCorner;k++)
            {
                double angle=(corner+k/(pointsPerCorner-1.0))*Math.PI*.5;
                mesh.AddVert(new Vector3(cx+radius*(float)Math.Cos(angle),cy+radius*(float)Math.Sin(angle)),ink,Vector2.zero);
            }
        }
        for(int p=0;p<perimeter;p++) mesh.AddTriangle(middle,middle+1+(p+1)%perimeter,middle+1+p);
    }
}



using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[HarmonyPatch(typeof(SpeedDial), "Start")]
internal static class AttachFlapInstrument
{
    static void Postfix(SpeedDial dial)
    {
        if (!dial.GetComponent<FlapInstrument>())
            dial.gameObject.AddComponent<FlapInstrument>().Attach(dial);
    }
}

public sealed class FlapInstrument : MonoBehaviour
{
    internal const float Width = 72f, Height = 274f, Gap = 30f, EndInset = 34f;
    SpeedDial dial;
    RectTransform face;
    FlapInstrumentPlate plate;
    float handleStage, velocity;

    internal void Attach(SpeedDial value)
    {
        dial = value;
        FlapsPlugin.flightUI = dial.GetComponentInParent<FlyingUIController>();
    }

    void Build()
    {
        var obj = new GameObject("Flap lever", typeof(RectTransform), typeof(FlapInstrumentPlate), typeof(FlapLeverInput));
        face = (RectTransform)obj.transform;
        face.SetParent(dial.transform, false);
        face.anchorMin = face.anchorMax = new Vector2(0f, .5f);
        face.pivot = new Vector2(1f, .5f);
        face.anchoredPosition = new Vector2(-Gap, 0f);
        face.sizeDelta = new Vector2(Width, Height);
        plate = obj.GetComponent<FlapInstrumentPlate>();
        handleStage = FlapsPlugin.stage;
        plate.SetPosition(handleStage);
    }

    void LateUpdate()
    {
        if (!dial) return;
        bool visible = FlapsPlugin.ShowSelector;
        if (visible && !face) Build();
        if (!face) return;
        if (face.gameObject.activeSelf != visible) face.gameObject.SetActive(visible);
        if (!visible) return;
        float next = Mathf.SmoothDamp(handleStage, FlapsPlugin.stage, ref velocity, .16f, 12f, Time.unscaledDeltaTime);
        if (Mathf.Abs(next - FlapsPlugin.stage) < .0005f) { next = FlapsPlugin.stage; velocity = 0f; }
        if (next != handleStage) { handleStage = next; plate.SetPosition(next); }
    }

    void OnDisable() { if (face) face.gameObject.SetActive(false); }
    void OnDestroy() { if (face) Destroy(face.gameObject); }
}

public sealed class FlapLeverInput : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
{
    internal static int StageAt(float y, Rect rect)
    {
        float top = rect.yMax - FlapInstrument.EndInset;
        float bottom = rect.yMin + FlapInstrument.EndInset;
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01((top - y) / (top - bottom)) * 3f), 0, 3);
    }

    void Select(PointerEventData pointer)
    {
        if (pointer.button != PointerEventData.InputButton.Left) return;
        var rect = (RectTransform)transform;
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, pointer.position, pointer.pressEventCamera, out local))
            FlapsPlugin.SelectStage(StageAt(local.y, rect.rect));
    }
    public void OnPointerDown(PointerEventData pointer) { Select(pointer); }
    public void OnBeginDrag(PointerEventData pointer) { Select(pointer); }
    public void OnDrag(PointerEventData pointer) { Select(pointer); }
}

public sealed class FlapInstrumentPlate : MaskableGraphic
{
    float position;
    internal void SetPosition(float stage) { position = stage; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper mesh) { Paint(mesh, rectTransform.rect, position); }

    internal static void Paint(VertexHelper mesh, Rect rect, float stage)
    {
        mesh.Clear();
        float x = rect.xMin, y = rect.yMin, w = rect.width, h = rect.height;
        Rounded(mesh,x+1.5f,y-2f,w,h,18f,new Color32(25,24,30,110));
        Rounded(mesh,x,y,w,h,18f,new Color32(252,252,250,255));
        Rounded(mesh,x+3f,y+3f,w-6f,h-6f,15f,new Color32(162,162,160,255));
        Rounded(mesh,x+6f,y+6f,w-12f,h-12f,12f,new Color32(197,197,195,255));
        Rounded(mesh,x+8f,y+8f,w-16f,h-16f,10f,new Color32(207,207,205,255));
        float top = rect.yMax - FlapInstrument.EndInset, bottom = rect.yMin + FlapInstrument.EndInset;
        for (int i = 0; i < 4; i++)
        {
            float notchY = Mathf.Lerp(top,bottom,i/3f);
            Rounded(mesh,rect.center.x-21f,notchY-4.3f,42f,8.6f,4.3f,new Color32(15,15,15,255));
        }
        Rounded(mesh,rect.center.x-4.8f,bottom-7f,9.6f,top-bottom+14f,4.8f,new Color32(77,77,75,255));
        Rounded(mesh,rect.center.x-2.7f,bottom-5f,3f,top-bottom+10f,1.5f,new Color32(121,121,117,255));
        float handleY = Mathf.Lerp(top,bottom,Mathf.Clamp(stage,0f,3f)/3f);
        Ball(mesh,new Vector2(rect.center.x+1.8f,handleY-2.2f),16.4f,true);
        Ball(mesh,new Vector2(rect.center.x,handleY),15f,false);
    }

    static void Ball(VertexHelper mesh, Vector2 center, float radius, bool shadow)
    {
        const int segments = 48, rings = 8;
        int start = mesh.currentVertCount;
        for (int r = 0; r <= rings; r++)
        {
            float radial = r/(float)rings;
            for (int i = 0; i < segments; i++)
            {
                double a = i*2.0*Math.PI/segments;
                float dx = radial*(float)Math.Cos(a), dy = radial*(float)Math.Sin(a);
                float light = Mathf.Clamp01(.63f+.25f*(float)Math.Sqrt(Math.Max(0,1f-radial*radial))-.17f*dx+.17f*dy);
                byte gray = (byte)(light*255f);
                Color32 ink = shadow ? new Color32(20,20,20,(byte)(r==rings?0:90)) :
                    new Color32(gray,gray,(byte)Math.Max(0,gray-2),(byte)(r==rings?0:255));
                mesh.AddVert(center+new Vector2(dx,dy)*radius,ink,Vector2.zero);
                if (r == 0) continue;
                int here = start+r*segments+i, next = start+r*segments+(i+1)%segments;
                mesh.AddTriangle(here-segments,next,here);
                mesh.AddTriangle(here-segments,next-segments,next);
            }
        }
    }

    static void Rounded(VertexHelper mesh,float x,float y,float width,float height,float radius,Color32 ink)
    {
        int middle = mesh.currentVertCount;
        mesh.AddVert(new Vector3(x+width*.5f,y+height*.5f),ink,Vector2.zero);
        const int pointsPerCorner = 8, perimeter = 4*pointsPerCorner;
        for (int corner = 0; corner < 4; corner++)
        {
            float cx = (corner==0||corner==3)?x+width-radius:x+radius;
            float cy = corner<2?y+height-radius:y+radius;
            for (int k = 0; k < pointsPerCorner; k++)
            {
                double a = (corner+k/(pointsPerCorner-1.0))*Math.PI*.5;
                mesh.AddVert(new Vector3(cx+radius*(float)Math.Cos(a),cy+radius*(float)Math.Sin(a)),ink,Vector2.zero);
            }
        }
        for (int p = 0; p < perimeter; p++)
            mesh.AddTriangle(middle,middle+1+(p+1)%perimeter,middle+1+p);
    }
}

using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HarmonyLib;

[HarmonyPatch(typeof(PartSettingsPanel),"OpenSettings")]
internal static class WingUpgradeMenu
{
    static FlapOptionCircle Circle(RectTransform parent,bool outline,float size)
    {
        var obj=new GameObject(outline?"Upgrade outline":"Upgrade dot",typeof(RectTransform),typeof(FlapOptionCircle));
        var graphic=obj.GetComponent<FlapOptionCircle>();graphic.outline=outline;
        graphic.color=outline?new Color(.34f,.34f,.34f):new Color(.12f,.12f,.12f);graphic.raycastTarget=false;
        var rect=graphic.rectTransform;rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=new Vector2(0f,.5f);rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=new Vector2(17f,0f);rect.sizeDelta=new Vector2(size,size);return graphic;
    }
    static TMP_Text Label(TMP_Text source,RectTransform parent,int row,float height)
    {
        var label=UnityEngine.Object.Instantiate(source,parent,false);label.gameObject.SetActive(true);
        var rect=label.rectTransform;rect.anchorMin=new Vector2(0f,1f);rect.anchorMax=new Vector2(1f,1f);rect.pivot=new Vector2(.5f,1f);
        rect.anchoredPosition=new Vector2(0f,-row*34f);rect.sizeDelta=new Vector2(-12f,height);
        label.margin=Vector4.zero;label.alignment=TextAlignmentOptions.MidlineLeft;label.raycastTarget=false;
        label.enableAutoSizing=true;label.fontSizeMin=12f;label.fontSizeMax=18f;return label;
    }
    static void Row(RectTransform body,TMP_Text source,ColorBlock colors,WingUpgrades state,WingUpgradeOptions option,string name,int index,TMP_Text status,PartPlacer placer,WingUpgradeMenuRefresh live)
    {
        var row=new GameObject("Flap Mod "+name+" purchase",typeof(RectTransform),typeof(Image),typeof(Toggle));
        var rect=(RectTransform)row.transform;rect.SetParent(body,false);rect.anchorMin=new Vector2(0f,1f);rect.anchorMax=new Vector2(1f,1f);
        rect.pivot=new Vector2(.5f,1f);rect.sizeDelta=new Vector2(0f,32f);rect.anchoredPosition=new Vector2(0f,-index*34f);
        row.GetComponent<Image>().color=Color.clear;
        var toggle=row.GetComponent<Toggle>();var box=Circle(rect,true,20f);var dot=Circle(box.rectTransform,false,10f);
        dot.rectTransform.anchorMin=dot.rectTransform.anchorMax=new Vector2(.5f,.5f);dot.rectTransform.anchoredPosition=Vector2.zero;
        toggle.targetGraphic=box;toggle.graphic=dot;toggle.colors=colors;toggle.toggleTransition=Toggle.ToggleTransition.None;
        var label=Label(source,rect,0,32f);label.rectTransform.offsetMin=new Vector2(36f,-32f);label.rectTransform.offsetMax=new Vector2(-4f,0f);
        Action refresh=()=>{
            if(!state)return;
            bool owned=(state.Options&option)!=0;toggle.SetIsOnWithoutNotify(owned);dot.canvasRenderer.SetAlpha(owned?1f:0f);
            var group=WingUpgradeMirroring.Group(state,placer);float amount=0f;bool paid=true;
            foreach(var peer in group)if(((peer.Options&option)!=0)==owned){amount+=peer.UnitPrice;paid&=peer.Part.hasBeenPlaced;}
            bool creative=GameManager.Instance && GameManager.Instance.gameModeData.creativeMode;
            string price=amount.ToString("#,0.##",CultureInfo.InvariantCulture);
            label.text=name+(owned?"  -$"+price+(paid && !creative?" refund":" value"):"  +$"+price)+(group.Count>1?" (pair)":"");
        };
        refresh();
        live.refresh+=refresh;
        toggle.onValueChanged.AddListener(selected=>{
            if(!state)return;string error;
            var group=WingUpgradeMirroring.Group(state,placer);
            bool changed=WingUpgrades.TrySetGroup(group,option,selected,out error);refresh();
            status.text=changed?"":error;status.gameObject.SetActive(!changed);
            body.sizeDelta=new Vector2(280f,(FlapsPlugin.SupportsSpoilers(state.Wing)?2:1)*34f+(changed?0f:26f));
            status.color=changed?source.color:new Color(.7f,.14f,.14f);
        });
    }
    static bool Prefix(PartSettingsPanel __instance,PlanePart __0,bool __1)
    {
        var wing=__0 as Wing;if(!FlapsPlugin.Supports(wing) || __1)return true;
        var template=FlapsPlugin.FindTemplate();if(!template)return true;
        var state=WingUpgrades.Ensure(wing);
        if(__instance.content)UnityEngine.Object.Destroy(__instance.content.gameObject);
        var panel=UnityEngine.Object.Instantiate(template);panel.transform.SetParent(__instance.transform,false);panel.transform.localScale=Vector3.one;
        var body=(RectTransform)panel.transform;__instance.content=body;
        var settings=panel.GetComponent<EngineSettings>();settings.enabled=false;
        var source=Array.Find(panel.GetComponentsInChildren<TMP_Text>(true),t=>t.name=="InvertLabel");
        if(!source)throw new InvalidOperationException("Flap Mod settings label is missing");
        var colors=settings.invertToggle.colors;
        foreach(var layout in panel.GetComponents<LayoutGroup>())layout.enabled=false;
        var fitter=panel.GetComponent<ContentSizeFitter>();if(fitter)fitter.enabled=false;
        for(int i=0;i<body.childCount;i++)body.GetChild(i).gameObject.SetActive(false);
        int rows=FlapsPlugin.SupportsSpoilers(wing)?2:1;body.sizeDelta=new Vector2(280f,rows*34f);
        var status=Label(source,body,rows,24f);status.text="";status.fontSizeMax=13f;status.gameObject.SetActive(false);
        var live=panel.AddComponent<WingUpgradeMenuRefresh>();
        Row(body,source,colors,state,WingUpgradeOptions.Flaps,"Flaps",0,status,__instance.partPlacer,live);
        if(rows==2)Row(body,source,colors,state,WingUpgradeOptions.Spoilers,"Spoilers",1,status,__instance.partPlacer,live);
        __instance.Update();return false;
    }
}

public sealed class WingUpgradeMenuRefresh : MonoBehaviour
{
    internal Action refresh;
    float nextRefresh;
    void LateUpdate(){if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.15f;if(refresh!=null)refresh();}
}

using System.IO;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[HarmonyPatch(typeof(LoadPlaneButton),"Init")]
internal static class ShowSavedWingUpgrades
{
    static void Postfix(LoadPlaneButton __instance,string __0)
    {
        var receipt=__instance.GetComponent<WingUpgradeReceiptView>();
        if(!receipt)receipt=__instance.gameObject.AddComponent<WingUpgradeReceiptView>();
        receipt.Bind(__instance,__0);
    }
}

public sealed class WingUpgradeReceiptView : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    LoadPlaneButton row;
    string path,description;
    bool checkedFile,showing;
    GameObject holder;
    TooltipSettings settings;

    internal void Bind(LoadPlaneButton button,string file){
        Hide();if(holder)Destroy(holder);
        row=button;path=file;description=null;settings=null;checkedFile=false;
    }
    public void OnPointerEnter(PointerEventData pointer){
        if(!row || !row.cost || !row.cost.gameObject.activeInHierarchy || !TooltipSystem.Instance)return;
        if(!checkedFile){
            checkedFile=true;
            try{using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))description=WingUpgradeReceipt.Read(stream);}
            catch(System.Exception error) when(error is IOException || error is InvalidDataException || error is System.UnauthorizedAccessException){return;}
        }
        if(description==null)return;
        if(settings==null){
            holder=new GameObject("Saved wing upgrade details");holder.SetActive(false);holder.transform.SetParent(transform,false);
            var content=new GameObject("Upgrade value",typeof(RectTransform),typeof(TextMeshProUGUI));
            content.transform.SetParent(holder.transform,false);
            var label=content.GetComponent<TextMeshProUGUI>();label.font=row.cost.font;label.fontSize=18;
            label.color=row.header.color;label.richText=false;label.text=description;label.raycastTarget=false;
            label.rectTransform.sizeDelta=label.GetPreferredValues(description)+new Vector2(16,12);
            label.alignment=TextAlignmentOptions.Center;
            settings=new TooltipSettings{prefab=content,targetRect=(RectTransform)row.transform};
        }
        TooltipSystem.Instance.Show(settings);showing=true;
    }
    public void OnPointerExit(PointerEventData pointer){Hide();}
    void OnDisable(){Hide();}
    void Hide(){if(showing && TooltipSystem.Instance)TooltipSystem.Instance.Hide();showing=false;}
}

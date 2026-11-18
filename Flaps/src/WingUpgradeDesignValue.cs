using System;
using System.IO;
using UnityEngine;
using HarmonyLib;

internal static class WingUpgradeDesignValue
{
    internal static void Synchronize(Transform plane)
    {
        foreach(var state in plane.GetComponentsInChildren<WingUpgrades>(true))state.SynchronizePrice();
    }
    internal sealed class LoadState
    {
        internal MemoryStream before;
        internal float cash,value;
        internal bool creative,completed;
    }
}

[HarmonyPatch(typeof(PlaneStorage),"GetPlaneCost")]
internal static class PricePurchasedWingOptions
{
    static void Prefix(PlaneStorage __instance){WingUpgradeDesignValue.Synchronize(__instance.transform);}
}

[HarmonyPatch(typeof(PlaneContainer),"GetPlanePrice")]
internal static class ShowPurchasedWingValue
{
    static void Prefix(PlaneContainer __instance){WingUpgradeDesignValue.Synchronize(__instance.transform);}
}

[HarmonyPatch(typeof(PlaneStorage),"LoadFromFile")]
internal static class ReconcilePurchasedDesign
{
    static void Prefix(PlaneStorage __instance,out WingUpgradeDesignValue.LoadState __state)
    {
        __state=null;if(!MoneyManager.Instance || !GameManager.Instance)return;
        // Preserve the current aircraft as well as cash so an underquoted
        // design cannot install unaffordable upgrades before its value is known.
        __state=new WingUpgradeDesignValue.LoadState{
            cash=MoneyManager.Instance.money,value=__instance.GetPlaneCost(),
            creative=GameManager.Instance.gameModeData.creativeMode,before=__instance.SaveToStream()};
    }
    static void Postfix(PlaneStorage __instance,ref bool __result,WingUpgradeDesignValue.LoadState __state)
    {
        if(__state==null)return;
        if(!__result){MoneyManager.Instance.money=__state.cash;__state.completed=true;return;}
        float cash;
        if(WingUpgradeEconomy.TryLoadBalance(__state.cash,__state.value,__instance.GetPlaneCost(),__state.creative,out cash)){
            // The authoritative value is the completely restored aircraft,
            // including its options, rather than a possibly stale file header.
            MoneyManager.Instance.money=cash;__state.completed=true;return;
        }
        __instance.LoadFromStream(__state.before,PersistentStorage.SaveVersion);
        MoneyManager.Instance.money=__state.cash;__state.completed=true;__result=false;
        if(__instance.partPlacer && __instance.partPlacer.priceFeedbackManager)__instance.partPlacer.priceFeedbackManager.ShowNotEnoughMoneyTooltip();
    }
    static Exception Finalizer(Exception __exception,PlaneStorage __instance,WingUpgradeDesignValue.LoadState __state)
    {
        if(__state==null)return __exception;
        try{
            if(__exception!=null && !__state.completed){
                __instance.LoadFromStream(__state.before,PersistentStorage.SaveVersion);MoneyManager.Instance.money=__state.cash;
            }
        }catch(Exception rollback){FlapsPlugin.LogDetail("Aircraft load rollback failed: "+rollback);}
        finally{__state.before.Dispose();}
        return __exception;
    }
}

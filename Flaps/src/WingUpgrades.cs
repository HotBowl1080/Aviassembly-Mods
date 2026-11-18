using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

public sealed class WingUpgrades : MonoBehaviour
{
    [SerializeField] bool initialized;
    [SerializeField] float basePrice;
    [SerializeField] byte purchased;
    [SerializeField] Mesh stockMesh;
    Wing wing;
    BuildingPart part;
    internal WingUpgradeOptions Options { get { return (WingUpgradeOptions)purchased; } }
    internal bool HasFlaps { get { return (Options&WingUpgradeOptions.Flaps)!=0; } }
    internal bool HasSpoilers { get { return FlapsPlugin.SupportsSpoilers(wing) && (Options&WingUpgradeOptions.Spoilers)!=0; } }
    internal float BasePrice { get { return basePrice; } }
    internal float UnitPrice { get { return WingUpgradeEconomy.UnitPrice(basePrice); } }
    internal Mesh StockMesh { get { return stockMesh; } }
    internal BuildingPart Part { get { return part; } }
    internal Wing Wing { get { return wing; } }
    internal void SynchronizePrice(){Initialize();}
    internal static WingUpgrades Ensure(Wing wing)
    {
        if(!FlapsPlugin.Supports(wing))return null;
        var state=wing.GetComponent<WingUpgrades>();
        if(!state)state=wing.gameObject.AddComponent<WingUpgrades>();
        state.Initialize();return state;
    }
    void Awake() { if(initialized)Initialize(); }
    void Start() { Initialize();if(Options!=WingUpgradeOptions.None)ApplyRuntime(); }
    void Initialize()
    {
        wing=GetComponent<Wing>();part=GetComponentInParent<BuildingPart>();
        if(!wing || !part)throw new InvalidOperationException("Wing upgrade has no building part");
        if(!initialized){
            basePrice=part.price;
            var filter=wing.GetComponent<MeshFilter>();var marker=wing.GetComponent<FlapMarker>();
            stockMesh=marker && marker.NativeMesh?marker.NativeMesh:filter?WingGeometryCache.NativeMesh(filter.sharedMesh):null;
            initialized=true;
        }
        part.price=WingUpgradeEconomy.TotalPrice(basePrice,Options);
    }
    internal bool TrySet(WingUpgradeOptions option,bool selected,out string error)
    {
        return TrySetGroup(new[]{this},option,selected,out error);
    }
    internal static bool TrySetGroup(IList<WingUpgrades> states,WingUpgradeOptions option,bool selected,out string error)
    {
        error=null;
        if(!MoneyManager.Instance || !GameManager.Instance || !PlaneStorage.Instance)
        { error="Change upgrades in the aircraft editor";return false; }
        var group=new List<WingUpgrades>();var unique=new HashSet<WingUpgrades>();
        foreach(var state in states)if(state && unique.Add(state)){state.Initialize();group.Add(state);}
        var requests=new WingUpgradeEconomy.Request[group.Count];
        var before=new byte[group.Count];var pricesBefore=new float[group.Count];var markers=new FlapMarker[group.Count];
        bool changed=false,placedChange=false;
        for(int i=0;i<group.Count;i++){
            var state=group[i];var plane=state.wing.GetComponentInParent<PlaneContainer>();
            if(plane && plane.FlightModeInitialized){error="Change upgrades in the aircraft editor";return false;}
            var next=selected?state.Options|option:state.Options&~option;
            requests[i]=new WingUpgradeEconomy.Request{basePrice=state.basePrice,before=state.Options,after=next,spoilersSupported=FlapsPlugin.SupportsSpoilers(state.wing),placed=state.part.hasBeenPlaced};
            before[i]=state.purchased;pricesBefore[i]=state.part.price;
            if(next!=state.Options){changed=true;placedChange|=state.part.hasBeenPlaced;}
        }
        if(!changed)return true;
        bool creative=GameManager.Instance.gameModeData.creativeMode;
        WingUpgradeEconomy.Plan[] plans;float cashChange;
        if(!WingUpgradeEconomy.TryGroupPlan(requests,creative,MoneyManager.Instance.money,out plans,out cashChange,out error)){
            if(group.Count==1 && error=="Not enough money for both wings")error="Not enough money";
            return false;
        }
        // Validate every mesh before changing either ownership or money.
        for(int i=0;i<group.Count;i++){
            markers[i]=group[i].wing.GetComponent<FlapMarker>();
            if(requests[i].after==WingUpgradeOptions.None)continue;
            if(!markers[i])markers[i]=group[i].wing.gameObject.AddComponent<FlapMarker>();
            if(!markers[i].InitializeGeometry()){
                foreach(var marker in markers)if(marker)marker.SynchronizeOwnership();
                error=markers[i].Preparing?"Wing model is loading; try again shortly":"This wing mesh cannot be modified";return false;
            }
        }
        float moneyBefore=MoneyManager.Instance.money;
        try{
            for(int i=0;i<group.Count;i++){group[i].purchased=(byte)requests[i].after;group[i].part.price=plans[i].price;}
            foreach(var marker in markers)if(marker)marker.SynchronizeOwnership();
            MoneyManager.Instance.ChangeMoneyAmount(cashChange);
            if(placedChange)PlaneStorage.Instance.UpdateHistory();
            return true;
        }catch(Exception ex){
            for(int i=0;i<group.Count;i++){group[i].purchased=before[i];group[i].part.price=pricesBefore[i];}
            MoneyManager.Instance.money=moneyBefore;
            foreach(var marker in markers)if(marker)marker.SynchronizeOwnership();
            FlapsPlugin.LogDetail("Upgrade transaction rolled back: "+ex);error="Upgrade could not be changed";return false;
        }
    }
    internal void Restore(float savedBasePrice,WingUpgradeOptions options)
    {
        Initialize();basePrice=savedBasePrice;purchased=(byte)options;
        part.price=WingUpgradeEconomy.TotalPrice(basePrice,Options);
        ApplyRuntime(); // Restoring/copying never debits or refunds money.
    }
    void ApplyRuntime()
    {
        var marker=wing.GetComponent<FlapMarker>();
        if(Options!=WingUpgradeOptions.None){
            if(!marker)marker=wing.gameObject.AddComponent<FlapMarker>();
            if(!marker.InitializeGeometry() && !marker.Preparing)FlapsPlugin.LogDetail("Saved wing upgrades retained, but the current mesh could not be modified");
        }
        if(marker)marker.SynchronizeOwnership();
    }
}

internal static class WingUpgradeMirroring
{
    internal static List<WingUpgrades> Group(WingUpgrades primary,PartPlacer placer)
    {
        var result=new List<WingUpgrades>{primary};
        if(!primary || !placer || !PartPlacer.mirrorMode)return result;
        var other=placer.GetMirrorPart(primary.Part);
        if(!other && placer.currentSelectedPart==primary.Part)other=placer.currentMirrorPart;
        if(!other || other==primary.Part || !other.gameObject.activeInHierarchy)return result;
        var wing=other.GetComponentInChildren<Wing>(true);
        if(FlapsPlugin.Supports(wing) && HingedWingProfile.Code(wing.name)==HingedWingProfile.Code(primary.Wing.name)){
            var state=WingUpgrades.Ensure(wing);if(state!=primary)result.Add(state);
        }
        return result;
    }
}

[HarmonyPatch(typeof(BuildingPart),"MakeMirrorCopy")]
internal static class CopyWingUpgrades
{
    static void Postfix(BuildingPart __instance,BuildingPart __result)
    {
        if(!__instance || !__result)return;
        var sources=__instance.GetComponentsInChildren<Wing>(true);
        var targets=__result.GetComponentsInChildren<Wing>(true);
        for(int i=0;i<sources.Length && i<targets.Length;i++){
            var state=sources[i].GetComponent<WingUpgrades>();
            if(state && state.Options!=WingUpgradeOptions.None && FlapsPlugin.Supports(targets[i]))
                WingUpgrades.Ensure(targets[i]).Restore(state.BasePrice,state.Options);
        }
    }
}

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

internal static class WingUpgradePersistence
{
    internal static byte WingType(Wing wing) { return wing.name.StartsWith("BigWing")?(byte)1:(byte)2; }
    internal static List<WingUpgradeSaveCodec.Record> Capture(PlaneStorage storage)
    {
        var records=new List<WingUpgradeSaveCodec.Record>();
        for(int i=0;i<storage.transform.childCount;i++){
            var wings=storage.transform.GetChild(i).GetComponentsInChildren<Wing>(true);
            for(int j=0;j<wings.Length;j++){
                var state=wings[j].GetComponent<WingUpgrades>();
                if(state && state.Options!=WingUpgradeOptions.None)
                    records.Add(new WingUpgradeSaveCodec.Record{partIndex=i,wingIndex=j,wingType=WingType(wings[j]),basePrice=state.BasePrice,options=state.Options});
            }
        }
        return records;
    }
    internal static void Restore(PlaneStorage storage,List<WingUpgradeSaveCodec.Record> records)
    {
        // Validate every target before applying any record.
        var targets=new List<Wing>();
        foreach(var record in records){
            if(record.partIndex>=storage.transform.childCount)throw new InvalidDataException("Saved upgrade part is missing");
            var wings=storage.transform.GetChild(record.partIndex).GetComponentsInChildren<Wing>(true);
            if(record.wingIndex>=wings.Length || !FlapsPlugin.Supports(wings[record.wingIndex]) || WingType(wings[record.wingIndex])!=record.wingType)
                throw new InvalidDataException("Saved upgrade wing does not match");
            targets.Add(wings[record.wingIndex]);
        }
        for(int i=0;i<records.Count;i++)WingUpgrades.Ensure(targets[i]).Restore(records[i].basePrice,records[i].options);
    }
}

[HarmonyPatch(typeof(PlaneStorage),"SaveToStream")]
internal static class SaveWingUpgradeSnapshot
{
    static void Postfix(PlaneStorage __instance,MemoryStream __result)
    { WingUpgradeSaveCodec.Append(__result,WingUpgradePersistence.Capture(__instance)); }
}

[HarmonyPatch(typeof(PlaneStorage),"Load")]
internal static class LoadWingUpgradeSnapshot
{
    static void Postfix(PlaneStorage __instance,GameDataReader __0)
    {
        try{
            List<WingUpgradeSaveCodec.Record> records;
            if(WingUpgradeSaveCodec.TryRead(__0.GetStream(),out records))WingUpgradePersistence.Restore(__instance,records);
        }catch(Exception ex) when(ex is IOException || ex is InvalidDataException){
            // A malformed optional extension must not stop native campaign
            // money/world loading. The codec has already restored its cursor.
            FlapsPlugin.LogDetail("Upgrade save extension was rejected: "+ex.Message);
        }
    }
}

[HarmonyPatch(typeof(PersistentStorage),"Save")]
internal static class SaveCampaignWingUpgrades
{
    static void Postfix(string ___savePath)
    {
        // Campaign serialization continues after PlaneStorage.Save. Put its
        // extension at EOF only after the native writer has finished/closed.
        if(!PlaneStorage.Instance)return;
        var records=WingUpgradePersistence.Capture(PlaneStorage.Instance);
        if(records.Count==0)return;
        using(var stream=new FileStream(___savePath,FileMode.Open,FileAccess.ReadWrite,FileShare.Read))
            WingUpgradeSaveCodec.Append(stream,records);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[BepInPlugin("aviassembly.test.wingreceipt","Wing runtime fixture","1.0")]
[BepInDependency("com.andrewstack.aviassembly.flaps")]
public sealed class WingRuntimeFixture : BaseUnityPlugin
{
    readonly List<string> report=new List<string>();
    readonly List<string> errors=new List<string>();
    static void Set(object target,string field,object value){AccessTools.Field(target.GetType(),field).SetValue(target,value);}
    static T Instance<T>(T obj) where T:MonoBehaviour {AccessTools.Field(typeof(Singleton<>).MakeGenericType(typeof(T)),"m_Instance").SetValue(null,obj);return obj;}
    static T Native<T>() where T:MonoBehaviour {return Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(v=>v.gameObject.scene.IsValid())??new GameObject(typeof(T).Name).AddComponent<T>();}
    void Check(bool ok,string name){if(!ok)throw new Exception(name);report.Add("PASS "+name);}
    void CatchLog(string msg,string stack,LogType type){if(type==LogType.Exception)errors.Add(msg+" "+stack);}
    IEnumerator Start(){
        string home=Paths.GameRootPath;
        if(!File.Exists(Path.Combine(home,"FLAP_TEST_HOST_ONLY")))yield break;
        yield return SceneManager.LoadSceneAsync("Persistent",LoadSceneMode.Additive);
        yield return SceneManager.LoadSceneAsync("Building",LoadSceneMode.Additive);
        var cache=typeof(FlapsPlugin).Assembly.GetType("WingGeometryCache",true);
        var entries=(IDictionary)AccessTools.Field(cache,"entries").GetValue(null);
        float deadline=Time.realtimeSinceStartup+12f;
        while(entries.Values.Cast<object>().Any(entry=>!((System.Threading.Tasks.Task)AccessTools.Field(entry.GetType(),"build").GetValue(entry)).IsCompleted) && Time.realtimeSinceStartup<deadline)yield return null;
        bool passed=false;
        try{Application.logMessageReceived+=CatchLog;Run(home);Check(errors.Count==0,"No Unity exceptions during transactions");passed=true;}
        catch(Exception ex){report.Add("FAIL "+ex);report.AddRange(errors);}
        finally{Application.logMessageReceived-=CatchLog;}
        File.WriteAllLines(Path.Combine(home,"flap-results.txt"),report.ToArray());Application.Quit(passed?0:1);
    }
    void Run(string home){
        var prefab=Resources.FindObjectsOfTypeAll<Wing>().First(v=>v.name=="BigWing" && !v.gameObject.scene.IsValid());
        var library=Native<PartPrefabs>();AccessTools.Field(typeof(PartPrefabs),"instance").SetValue(null,library);
        Set(library,"allPartPrefabs",new List<GameObject>{prefab.gameObject});
        var research=Instance(Native<ResearchManager>());var unlocked=new List<BuildingPart>{prefab.GetComponent<BuildingPart>()};
        Set(research,"allParts",unlocked);research.unlockedParts=unlocked;
        var manager=Instance(Native<GameManager>());manager.gameModeData=new GameModeData();manager.gameModeData.creativeMode=false;
        var money=Instance(Native<MoneyManager>());money.money=5000;
        var plane=new GameObject("Test aircraft").AddComponent<PlaneContainer>();Instance(plane);
        var storage=Instance(plane.gameObject.AddComponent<PlaneStorage>());Set(storage,"planeContainer",plane);
        storage.ResetPlaneEvent+=()=>{};var decals=Instance(Native<DecalContainer>());
        decals.decals=new List<Decal>();decals.colliders=new List<Collider>();
        storage.partPlacer=Native<PartPlacer>();foreach(var placer in Resources.FindObjectsOfTypeAll<PartPlacer>())if(placer.gameObject.scene.IsValid())placer.partContainer=new PartContainer();
        Instance(Native<BuildingCamera>());
        var wing=Instantiate(prefab.gameObject,plane.transform).GetComponent<Wing>();var part=wing.GetComponent<BuildingPart>();part.hasBeenPlaced=true;
        var upgrade=AccessTools.Method(typeof(WingUpgrades),"Ensure").Invoke(null,new object[]{wing});
        var restore=AccessTools.Method(typeof(WingUpgrades),"Restore");var flags=restore.GetParameters()[1].ParameterType;
        float stock=part.price;restore.Invoke(upgrade,new object[]{stock,Enum.ToObject(flags,3)});
        var file=storage.SaveToFile(Path.GetFullPath(Path.Combine(home,"fixture-plane")));
        Check(Path.GetDirectoryName(file)==Path.GetFullPath(home).TrimEnd(Path.DirectorySeparatorChar),"Design file belongs to isolated player");
        float surcharge=stock*.5f;
        Check(Math.Abs(storage.GetSaveFileCost(file)-stock-surcharge)<.01f,"Native saved aircraft header includes both upgrades");
        restore.Invoke(upgrade,new object[]{stock,Enum.ToObject(flags,0)});
        Check(storage.LoadFromFile(file),"Native aircraft design loads");
        Check(Math.Abs(money.money-(5000-surcharge))<.01f,"Loading upgraded aircraft charges the difference");
        Check(Math.Abs(storage.GetPlaneCost()-stock-surcharge)<.01f,"Restored part value includes both options");
        Check(storage.LoadFromFile(file) && Math.Abs(money.money-(5000-surcharge))<.01f,"Repeated native load keeps cash unchanged");
        var menu=Native<LoadPlanePanel>();var row=Instantiate(menu.buttonPrefab).GetComponent<LoadPlaneButton>();
        row.Init(file,menu,false,(int)(stock+surcharge),false);
        var trigger=row.GetComponent<WingUpgradeReceiptView>();Check(trigger!=null,"Native saved aircraft row receives upgrade details");
        var tips=Instance(Native<TooltipSystem>());trigger.OnPointerEnter(null);
        var label=tips.tooltip.GetComponentsInChildren<TMP_Text>(true).First(t=>t.text.Contains("Saved wing upgrades"));
        Check(label.gameObject.activeInHierarchy && label.text.Contains("Flaps (1)") && label.text.Contains("Spoilers (1)"),"Native tooltip opens with one flap and one spoiler");
        trigger.OnPointerExit(null);Check(!tips.tooltip.gameObject.activeSelf,"Native tooltip closes on pointer exit");
        using(var stream=new FileStream(file,FileMode.Open,FileAccess.Write)){stream.Position=4;new BinaryWriter(stream).Write(stock);}
        var current=storage.transform.GetChild(0).GetComponent<WingUpgrades>();restore.Invoke(current,new object[]{stock,Enum.ToObject(flags,0)});
        money.money=surcharge-1;
        Check(!storage.LoadFromFile(file),"Underquoted unaffordable design is rejected");
        Check(Math.Abs(storage.GetPlaneCost()-stock)<.01f && Math.Abs(money.money-(surcharge-1))<.01f,"Rejection restores the original aircraft and cash");
        foreach(var name in new[]{"BigWing","StraightWing","SmallRecreationalWing","GliderWing","ElectronWing","SmallJetWing","DeltaWing02","Wing"}){
            var source=Resources.FindObjectsOfTypeAll<Wing>().First(v=>v.name==name && !v.gameObject.scene.IsValid());
            var placed=Instantiate(source.gameObject,plane.transform);var filter=placed.GetComponent<MeshFilter>();
            var native=source.GetComponent<MeshFilter>().sharedMesh;
            var building=placed.GetComponent<BuildingPart>();building.hasBeenPlaced=true;var price=building.price;
            AccessTools.Method(typeof(BuildingPart),"Start").Invoke(building,null);
            var defaultMesh=filter.sharedMesh;
            Check(name=="BigWing"?defaultMesh!=native:defaultMesh==native,name+" unowned appearance");
            var options=AccessTools.Method(typeof(WingUpgrades),"Ensure").Invoke(null,new object[]{placed.GetComponent<Wing>()});
            var method=AccessTools.Method(typeof(WingUpgrades),"TrySet");
            money.money=price*2f;var choose=new object[]{Enum.ToObject(flags,1),true,null};
            var start=System.Diagnostics.Stopwatch.StartNew();bool bought=(bool)method.Invoke(options,choose);start.Stop();
            Check(bought && filter.sharedMesh!=defaultMesh,name+" purchases actual animated mesh: "+choose[2]);
            Check(Math.Abs(building.price-price*1.25f)<.01f && Math.Abs(money.money-price*1.75f)<.01f,name+" charges exactly one wing upgrade");
            choose[1]=false;Check((bool)method.Invoke(options,choose) && filter.sharedMesh==defaultMesh && Math.Abs(money.money-price*2f)<.01f,name+" removal restores appearance and refunds purchase");
            report.Add("MEASURE "+name+" first purchase after prewarm: "+start.Elapsed.TotalMilliseconds.ToString("F2")+" ms");
            placed.transform.SetParent(null);Destroy(placed);
        }
        var pairWing=Instantiate(prefab.gameObject,plane.transform);
        pairWing.GetComponent<BuildingPart>().hasBeenPlaced=true;
        var pair=plane.GetComponentsInChildren<Wing>().Select(w=>(WingUpgrades)AccessTools.Method(typeof(WingUpgrades),"Ensure").Invoke(null,new object[]{w})).ToList();
        Check(pair.Count==2,"Undo fixture contains two placed Big Wings");
        var historyField=AccessTools.Field(typeof(PlaneStorage),"history");
        historyField.SetValue(storage,Activator.CreateInstance(historyField.FieldType));
        var snapshot=AccessTools.Method(typeof(PlaneStorage),"ExecuteHistoryUpdate");
        GameManager.gameMode=GameMode.Building;money.money=5000f;snapshot.Invoke(storage,null);
        var beforeValue=storage.GetPlaneCost();
        var purchase=new object[]{pair,Enum.ToObject(flags,1),true,null};
        Check((bool)AccessTools.Method(typeof(WingUpgrades),"TrySetGroup").Invoke(null,purchase),"Both placed wings receive the paired upgrade");
        Check(money.money==5000f-surcharge && storage.GetPlaneCost()==beforeValue+surcharge,"Paired purchase charges each installed flap once");
        snapshot.Invoke(storage,null);
        var undoClock=System.Diagnostics.Stopwatch.StartNew();AccessTools.Method(typeof(PlaneStorage),"Undo").Invoke(storage,null);undoClock.Stop();
        Check(money.money==5000f && storage.GetPlaneCost()==beforeValue && storage.transform.childCount==2,"One native undo restores both wings and exact cash");
        Check(!storage.GetComponentsInChildren<WingUpgrades>().Any(s=>(byte)AccessTools.Field(typeof(WingUpgrades),"purchased").GetValue(s)!=0),"Undo removes both saved ownership flags");
        report.Add("MEASURE two-wing native undo: "+undoClock.Elapsed.TotalMilliseconds.ToString("F2")+" ms");
    }
}

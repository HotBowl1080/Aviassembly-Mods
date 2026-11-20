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
public sealed class CompatibilityFixture : BaseUnityPlugin
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
        bool passed=false;
        try{Run(home);passed=true;}catch(Exception ex){report.Add("FAIL "+ex);}
        File.WriteAllLines(Path.Combine(home,"flap-results.txt"),report.ToArray());Application.Quit(passed?0:1);
    }
    void Run(string home){
        var spec=File.ReadAllText(Path.Combine(home,"compatibility-case.txt")).Trim().Split(' ');
        bool save=spec[0]=="save";string name=spec[1];
        var prefab=Resources.FindObjectsOfTypeAll<Wing>().First(v=>v.name==name && !v.gameObject.scene.IsValid());
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
        float price=part.price;float before=money.money;
        var design=Path.GetFullPath(Path.Combine(home,"compat-"+name));
        if(save){
            restore.Invoke(upgrade,new object[]{price,Enum.ToObject(flags,1)});
            Check(storage.SaveToFile(design)==design+".planedesign","Legacy plugin saved the isolated aircraft: "+name);
        }else{
            Check(storage.LoadFromFile(design+".planedesign"),"Current plugin loaded the earlier native design: "+name);
            var restored=storage.transform.GetChild(0).GetComponent<WingUpgrades>();
            Check(restored && (byte)AccessTools.Field(typeof(WingUpgrades),"purchased").GetValue(restored)==1,"Paid flap retained: "+name);
            Check(storage.GetPlaneCost()==price*1.25f && money.money==before-price*.25f,"Saved upgrade contributes to aircraft value and purchase difference");
            Check(storage.LoadFromFile(design+".planedesign") && money.money==before-price*.25f,"Second load grants no money and charges nothing extra");
        }
    }
}

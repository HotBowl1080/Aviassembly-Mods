// Minimal in-memory adapters for exercising the production transaction/save
// classes. No Unity player, campaign balance or user save is changed by tests.
using System;
using System.Collections.Generic;
using System.Reflection;
namespace UnityEngine {
 public class Object { public static implicit operator bool(Object value){return value!=null;} }
 [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute {}
 public class Component : Object {
  public GameObject gameObject;
  public string name {get{return gameObject.name;}}
  public Transform transform {get{return gameObject.transform;}}
  public T GetComponent<T>() where T:Component {return gameObject.GetComponent<T>();}
  public T GetComponentInParent<T>() where T:Component {for(var o=gameObject;o!=null;o=o.transform.parent==null?null:o.transform.parent.gameObject){var c=o.GetComponent<T>();if(c!=null)return c;}return null;}
  public T GetComponentInChildren<T>(bool inactive) where T:Component {var all=GetComponentsInChildren<T>(inactive);return all.Length>0?all[0]:null;}
  public T[] GetComponentsInChildren<T>(bool inactive) where T:Component {return gameObject.GetComponentsInChildren<T>(inactive);}
 }
 public class MonoBehaviour : Component {public bool enabled=true;}
 public class Mesh : Object {}
 public class MeshFilter : Component {public Mesh sharedMesh=new Mesh();}
 public class Transform : Component {
  public Transform parent;
  public readonly List<Transform> children=new List<Transform>();
  public int childCount {get{return children.Count;}}
  public Transform GetChild(int i){return children[i];}
 }
 public class GameObject : Object {
  public string name;
  public bool activeInHierarchy=true;
  public readonly Transform transform;
  readonly List<Component> components=new List<Component>();
  public GameObject(string name){this.name=name;transform=new Transform{gameObject=this};components.Add(transform);}
  public void Parent(GameObject parent){transform.parent=parent.transform;parent.transform.children.Add(transform);}
  public T GetComponent<T>() where T:Component {foreach(var c in components)if(c is T)return (T)c;return null;}
  public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components.Add(c);var awake=typeof(T).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic);if(awake!=null)awake.Invoke(c,null);return c;}
  public T[] GetComponentsInChildren<T>(bool inactive) where T:Component {var all=new List<T>();foreach(var c in components)if(c is T)all.Add((T)c);foreach(var child in transform.children)all.AddRange(child.gameObject.GetComponentsInChildren<T>(inactive));return all.ToArray();}
 }
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method,AllowMultiple=true)] public sealed class HarmonyPatch : Attribute {public HarmonyPatch(Type type,string method){}}
}
public class Wing : UnityEngine.MonoBehaviour {}
public class BuildingPart : UnityEngine.MonoBehaviour {public float price;public bool hasBeenPlaced;}
public class PlaneContainer : UnityEngine.MonoBehaviour {public bool FlightModeInitialized;}
public class PlaneStorage : UnityEngine.MonoBehaviour {
 public static PlaneStorage Instance;public int histories;public void UpdateHistory(){histories++;}
 public PartPlacer partPlacer;public float value;public float GetPlaneCost()=>value;
 public System.IO.MemoryStream SaveToStream()=>new System.IO.MemoryStream(BitConverter.GetBytes(value));
 public void LoadFromStream(System.IO.MemoryStream before,int version){value=BitConverter.ToSingle(before.ToArray(),0);}
}
public class TestFeedback : UnityEngine.Object { public void ShowNotEnoughMoneyTooltip(){} }
public class PartPlacer : UnityEngine.MonoBehaviour {
 public TestFeedback priceFeedbackManager;
 public static bool mirrorMode;
 public BuildingPart currentSelectedPart,currentMirrorPart,partner;
 public BuildingPart GetMirrorPart(BuildingPart part){return mirrorMode?partner:null;}
}
public class GameModeData {public bool creativeMode;}
public class GameManager : UnityEngine.Object {public static GameManager Instance=new GameManager();public GameModeData gameModeData=new GameModeData();}
public class MoneyManager : UnityEngine.Object {
 public static MoneyManager Instance=new MoneyManager();public float money;
 public void ChangeMoneyAmount(float amount){if(!GameManager.Instance.gameModeData.creativeMode)money+=amount;}
}
public class PersistentStorage { public const int SaveVersion=31; }
public class GameDataReader {readonly System.IO.Stream stream;public GameDataReader(System.IO.Stream stream){this.stream=stream;}public System.IO.Stream GetStream(){return stream;}}
public class FlapMarker : UnityEngine.MonoBehaviour {
 public UnityEngine.Mesh NativeMesh=>null;
 public bool Preparing=>false;
 public static bool FailGeometry;
 public static string FailOnName;
 public WingUpgradeOptionsForTest observed;
 public bool InitializeGeometry(){return !FailGeometry && name!=FailOnName;}
 public void SynchronizeOwnership(){var s=GetComponent<WingUpgrades>();observed=(WingUpgradeOptionsForTest)(byte)s.Options;enabled=s.Options!=WingUpgradeOptions.None;}
}
public enum WingUpgradeOptionsForTest {None=0,Flaps=1,Spoilers=2}
internal static class WingGeometryCache {internal static UnityEngine.Mesh NativeMesh(UnityEngine.Mesh mesh)=>mesh;}
internal static class HingedWingProfile {
 internal static byte Code(string name){return name.StartsWith("BigWing")?(byte)1:name.StartsWith("StraightWing")?(byte)2:(byte)3;}
}
internal static class FlapsPlugin {
 internal static bool Supports(Wing wing){return wing && (wing.name.StartsWith("BigWing")||wing.name.StartsWith("StraightWing") || wing.name.StartsWith("SmallRecreationalWing"));}
 internal static bool SupportsSpoilers(Wing wing){return wing && wing.name.StartsWith("BigWing");}
 internal static void LogDetail(string message){Console.WriteLine(message);}
}

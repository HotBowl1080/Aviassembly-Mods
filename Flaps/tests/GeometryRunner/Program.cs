using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using UnityEngine;
class Program
{
 static Vector3[] Vec(JsonElement d){return d.EnumerateArray().Select(v=>new Vector3(v[0].GetSingle(),v[1].GetSingle(),v[2].GetSingle())).ToArray();}
 static void Check(bool ok,string description){if(!ok)throw new Exception(description);}
 static float[] Flat(IEnumerable<Vector3> values){return values.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray();}
 static string Key(Vector3 v){return Math.Round(v.x*100000)+","+Math.Round(v.y*100000)+","+Math.Round(v.z*100000);}
 static int BoundaryCount(StraightWingGeometry g,bool flap){
  var moving=new HashSet<int>(g.moving);var edges=new Dictionary<string,int>();
  foreach(var sub in g.triangles)for(int i=0;i<sub.Count;i+=3){if(moving.Contains(sub[i])!=flap)continue;
   for(int j=0;j<3;j++){string a=Key(g.vertices[sub[i+j]].p),b=Key(g.vertices[sub[i+(j+1)%3]].p);if(a==b)continue;string key=string.CompareOrdinal(a,b)<0?a+"|"+b:b+"|"+a;edges[key]=edges.TryGetValue(key,out int n)?n+1:1;}}
  return edges.Count(e=>e.Value!=2);
 }
 static void Main(string[] args){try{Run(args);}catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}}
 static void Run(string[] args){
  var doc=JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;var p=Vec(doc.GetProperty("positions"));var n=Vec(doc.GetProperty("normals"));
  var uv=doc.GetProperty("uv").EnumerateArray().Select(v=>new Vector2(v[0].GetSingle(),v[1].GetSingle())).ToArray();
  var tri=doc.GetProperty("triangles").EnumerateArray().Select(a=>a.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();
  var g=new StraightWingGeometry(p,n,uv,tri[0],tri[1]);
  var moving=new HashSet<int>(g.moving);Check(moving.Count>0,"No flap vertices");
  int flapBoundaries=BoundaryCount(g,true);Console.WriteLine("Cut vertices="+g.positions.Length+", flap vertices="+moving.Count+", flap boundary/nonmanifold edges="+flapBoundaries+", stationary="+BoundaryCount(g,false));
  Check(flapBoundaries==0,"Flap must be watertight after welding seams");
  Check(BoundaryCount(g,false)==0,"Fixed wing must be watertight after cutting");
  var rest=g.vertices.Select(v=>v.p).ToArray();
  int checks=0;
  foreach(Vector3 down in new[]{Vector3.down,Vector3.up}){
   Vector3 root=down.y<0?StraightWingGeometry.HingeRoot:StraightWingGeometry.ReverseHingeRoot,tip=down.y<0?StraightWingGeometry.HingeTip:StraightWingGeometry.ReverseHingeTip;
   var noTravel=new StraightWingFlapMotion(rest,g.vertices.Select(v=>v.n).ToArray(),g.moving.ToArray(),root,tip,Vector3.left,0f,0f);
   Vector3 axis=(tip-root).normalized,aft=Vector3.ProjectOnPlane(Vector3.left,axis).normalized;
   int trailing=g.moving.OrderByDescending(i=>Vector3.Dot(rest[i]-root,aft)).First();
   for(int angle=0;angle<=30;angle++){
    g.Pose(angle,down);
    noTravel.Pose(angle,down);
    Vector3 projectedDown=Vector3.ProjectOnPlane(down,axis).normalized;
    float blend=Math.Clamp((angle-20f)/10f,0f,1f);blend=blend*blend*(3f-2f*blend);
    Vector3 expectedTravel=(aft*.0254f+projectedDown*.0127f)*blend;
    foreach(int id in g.moving)Check((g.positions[id]-noTravel.positions[id]-expectedTravel).magnitude<.000001f,"Unexpected last-setting travel");
    foreach(int id in g.moving){Vector3 q=g.positions[id];Check(q.x+.1341985f*q.z+.4773043f<.00001f,"Flap crosses the fixed hinge plane: down="+down+", angle="+angle+", id="+id+", point="+q.ToString("F7"));}
    if(angle>0)Check(Vector3.Dot(g.positions[trailing]-rest[trailing],projectedDown)>0f,"Trailing edge moved upward");
    for(int i=0;i<rest.Length;i++){
     Check(float.IsFinite(g.positions[i].x)&&float.IsFinite(g.normals[i].x),"Nonfinite pose");
     if(!moving.Contains(i))Check((g.positions[i]-rest[i]).sqrMagnitude<1e-12f,"Stationary wing moved");
     if(angle==0)Check((g.positions[i]-rest[i]).sqrMagnitude<1e-12f,"Stowed seam does not close");
    }
    int a=g.moving[0],b=g.moving[g.moving.Count/2];Check(Math.Abs((g.positions[a]-g.positions[b]).magnitude-(rest[a]-rest[b]).magnitude)<.00001f,"Flap deformed");checks++;
   }
   for(int angle=30;angle>=0;angle--)g.Pose(angle,down);
   Check(g.positions.Zip(rest,(a,b)=>(a-b).sqrMagnitude).Max()<1e-12f,"Retraction failed");
  }
  var meshes=new List<object>();
  foreach(bool flap in new[]{false,true}){
   var ids=Enumerable.Range(0,rest.Length).Where(i=>moving.Contains(i)==flap).ToArray();var remap=ids.Select((id,i)=>(id,i)).ToDictionary(x=>x.id,x=>x.i);
   var submeshes=g.triangles.Select(t=>new {triangles=t.Where(remap.ContainsKey).Select(i=>remap[i]).ToArray()}).ToArray();
   g.Pose(0,Vector3.down);var positions=Flat(ids.Select(i=>g.positions[i]));var normals=Flat(ids.Select(i=>g.normals[i]));
   var frames=new List<object>();if(flap)for(int angle=0;angle<=30;angle++){g.Pose(angle,Vector3.down);frames.Add(new {angle,positions=Flat(ids.Select(i=>g.positions[i])),normals=Flat(ids.Select(i=>g.normals[i]))});}
   meshes.Add(new {name=flap?"Hinged inboard flap":"Straight Wing fixed shell",role=flap?"flap":"wing",positions,normals,submeshes,frames=flap?frames:null});
  }
  File.WriteAllText(args[1],JsonSerializer.Serialize(new {schemaVersion=1,wings=new[]{new {id="straight-wing",name="Straight Wing",version="0.21.0",format="raw",hasFlaps=true,meshes}}}));
  Console.WriteLine("PASS "+checks+" mirrored deployment poses, rigid motion, stowed closure and retraction; actual C# poses exported.");
 }
}



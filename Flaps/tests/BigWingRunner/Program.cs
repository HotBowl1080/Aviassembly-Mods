using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using UnityEngine;
class Program {
 static bool Hit(Vector3 p,Vector3 q,Vector3[] f,out Vector3 point){
  point=Vector3.zero;var d=q-p;var e1=f[1]-f[0];var e2=f[2]-f[0];var h=Vector3.Cross(d,e2);float a=Vector3.Dot(e1,h);
  if(Math.Abs(a)<1e-9f)return false;float inv=1f/a;var s=p-f[0];float u=inv*Vector3.Dot(s,h);if(u<.00001f||u>.99999f)return false;
  var cross=Vector3.Cross(s,e1);float v=inv*Vector3.Dot(d,cross);if(v<.00001f||u+v>.99999f)return false;
  float t=inv*Vector3.Dot(e2,cross);if(t<=.00001f||t>=.99999f)return false;point=p+d*t;return true;
 }
 static void Main(string[] args){try{Run(args);}catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}}
 static void Run(string[] args){
  using(var r=new BinaryReader(File.OpenRead(args[0]))){
   int count=r.ReadInt32(),subCount=r.ReadInt32();var p=new Vector3[count];var n=new Vector3[count];
   for(int i=0;i<count;i++)p[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
   var uv=new Vector2[r.ReadInt32()];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
   int colors=r.ReadInt32();r.BaseStream.Position+=16*colors;var tri=new int[subCount][];
   for(int s=0;s<subCount;s++){tri[s]=new int[r.ReadInt32()];for(int i=0;i<tri[s].Length;i++)tri[s][i]=r.ReadInt32();
    for(int i=0;i<tri[s].Length;i+=3){int a=tri[s][i],bb=tri[s][i+1],c=tri[s][i+2];var normal=Vector3.Cross(p[bb]-p[a],p[c]-p[a]);n[a]+=normal;n[bb]+=normal;n[c]+=normal;}
   }
   for(int i=0;i<count;i++)n[i]=n[i].normalized;
   if(args.Length>1){
    using var native=JsonDocument.Parse(File.ReadAllText(args[1]));
    n=native.RootElement.GetProperty("normals").EnumerateArray().Select(v=>new Vector3(v[0].GetSingle(),v[1].GetSingle(),v[2].GetSingle())).ToArray();
   }
   var clock=Stopwatch.StartNew();var g=new BigWingGeometry(p,n,uv,tri[0],tri[1]);clock.Stop();
   Console.WriteLine("Build "+clock.Elapsed.TotalMilliseconds+" ms, vertices="+g.positions.Length+", triangles="+g.triangles.Sum(s=>s.Count)/3);
   clock.Restart();for(int i=0;i<20;i++){var clone=new BigWingGeometry(g);clone.Pose(0,Vector3.down,0);}clock.Stop();Console.WriteLine("Clone plus stowed pose average "+clock.Elapsed.TotalMilliseconds/20+" ms");
   var flags=BindingFlags.NonPublic|BindingFlags.Instance;var moving=g.MovingVertexIds();
   var devices=typeof(BigWingGeometry).GetField("devices",flags).GetValue(g);
   foreach(var down in new[]{Vector3.up,Vector3.down})for(int angle=0;angle<=30;angle++){
    g.Pose(angle,down,0);
    if(g.positions.Any(v=>!float.IsFinite(v.x+v.y+v.z)))throw new Exception("Nonfinite pose");
    if((g.positions[25]-p[25]).sqrMagnitude>1e-12f)throw new Exception("Fixed center plate moved");
    if(angle>0 && (g.positions[135]-p[135]).sqrMagnitude<1e-9f)throw new Exception("Adjacent flap did not deploy");
   }
   var cloneCheck=new BigWingGeometry(g);var prior=(Vector3[])g.positions.Clone();cloneCheck.Pose(29,Vector3.up,1);
   if(!prior.SequenceEqual(g.positions))throw new Exception("Posing a clone changed its source");
   if(args.Length>2){
    using var writer=new BinaryWriter(File.Create(args[2]));
    var tags=Enumerable.Repeat(-1,g.positions.Length).ToArray();
    var groups=(List<int>[])typeof(BigWingGeometry).GetField("moving",flags).GetValue(g);
    for(int j=0;j<groups.Length;j++)foreach(int index in groups[j])tags[index]=j;
    int category=10;foreach(var device in (System.Collections.IEnumerable)devices.GetType().GetField("devices",flags).GetValue(devices)){
     foreach(int index in (List<int>)device.GetType().GetField("indices",flags).GetValue(device))tags[index]=category;
     category++;
    }
    writer.Write(tags.Length);foreach(int tag in tags)writer.Write((byte)tag);
    var indices=g.triangles.SelectMany(s=>s).ToArray();writer.Write(indices.Length);foreach(int id in indices)writer.Write(id);
    for(int angle=0;angle<=30;angle++){g.Pose(angle,Vector3.down,0);
     foreach(var buffer in new[]{g.positions,g.normals})foreach(var point in buffer){writer.Write(point.x);writer.Write(point.y);writer.Write(point.z);}
    }
   }
  }
 }
}

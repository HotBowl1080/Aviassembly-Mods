using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

// Unity mesh access stays on the main thread. Only managed arrays and pure
// geometry calculations run on the worker; no purchase/load waits on a task.
internal static class WingGeometryCache
{
    internal sealed class Prepared
    {
        internal BigWingGeometry big;
        internal StraightWingGeometry straight;
        internal Vector3[] positions,normals;
        internal Vector2[] uv;
        internal int[][] triangles;
    }
    internal sealed class Entry
    {
        internal Mesh native,stowed;
        internal Task<Prepared> build;
        internal bool warned;
    }
    static readonly Dictionary<Mesh,Entry> entries=new Dictionary<Mesh,Entry>();
    static readonly Dictionary<Mesh,Mesh> nativeByStowed=new Dictionary<Mesh,Mesh>();
    internal static Mesh NativeMesh(Mesh mesh)
    { Mesh original;return mesh && nativeByStowed.TryGetValue(mesh,out original)?original:mesh; }
    internal static Entry Request(Mesh mesh,bool big)
    {
        mesh=NativeMesh(mesh);if(!mesh || !mesh.isReadable)return null;
        Entry entry;if(entries.TryGetValue(mesh,out entry))return entry;
        // Snapshot native buffers once, while still on Unity's main thread.
        var p=mesh.vertices;var n=mesh.normals;var uv=mesh.uv;
        var skin=mesh.GetTriangles(0);bool single=mesh.subMeshCount==1;var edge=single?new int[0]:mesh.GetTriangles(1);
        entry=new Entry{native=mesh};entries.Add(mesh,entry);
        entry.build=Task.Run(()=>{
            var ready=new Prepared();List<int>[] triangles;
            if(big){
                ready.big=new BigWingGeometry(p,n,uv,skin,edge);ready.big.Pose(0f,Vector3.down,0f);
                ready.positions=ready.big.positions;ready.normals=ready.big.normals;triangles=ready.big.triangles;
                ready.uv=new Vector2[ready.big.vertices.Count];for(int i=0;i<ready.uv.Length;i++)ready.uv[i]=ready.big.vertices[i].uv;
            }else{
                ready.straight=new StraightWingGeometry(p,n,uv,skin,edge);ready.straight.Pose(0f,Vector3.down);
                ready.positions=ready.straight.positions;ready.normals=ready.straight.normals;triangles=ready.straight.triangles;
                ready.uv=new Vector2[ready.straight.vertices.Count];for(int i=0;i<ready.uv.Length;i++)ready.uv[i]=ready.straight.vertices[i].uv;
            }
            if(single){var combined=new List<int>(triangles[0]);combined.AddRange(triangles[1]);ready.triangles=new[]{combined.ToArray()};}
            else ready.triangles=new[]{triangles[0].ToArray(),triangles[1].ToArray()};return ready;
        });
        return entry;
    }
    internal static Prepared Ready(Entry entry)
    {
        if(entry==null || !entry.build.IsCompleted)return null;
        if(entry.build.IsFaulted){
            if(!entry.warned){entry.warned=true;FlapsPlugin.LogDetail("Wing geometry preparation failed: "+entry.build.Exception);}
            return null;
        }
        return entry.build.Result;
    }
    internal static Mesh MakeMesh(Prepared ready,Vector3[] positions,Vector3[] normals,Bounds bounds,bool dynamic)
    {
        var mesh=new Mesh();mesh.name=dynamic?"Flap Mod animated wing":"Flap Mod default Big Wing";
        if(dynamic)mesh.MarkDynamic();mesh.vertices=positions;mesh.normals=normals;mesh.uv=ready.uv;mesh.subMeshCount=ready.triangles.Length;
        for(int i=0;i<ready.triangles.Length;i++)mesh.SetTriangles(ready.triangles[i],i,false);
        bounds.Expand(.9f);mesh.bounds=bounds;return mesh;
    }
    internal static Mesh Stowed(Entry entry)
    {
        var ready=Ready(entry);if(ready==null)return null;
        if(!entry.stowed){entry.stowed=MakeMesh(ready,ready.positions,ready.normals,entry.native.bounds,false);nativeByStowed[entry.stowed]=entry.native;}
        return entry.stowed;
    }
}

[HarmonyPatch(typeof(PartPrefabs),"Awake")]
internal static class PrepareWingGeometry
{
    static void Postfix(List<GameObject> ___allPartPrefabs)
    {
        var prefabs=___allPartPrefabs;if(prefabs==null)return;
        foreach(var prefab in prefabs){
            if(!prefab || WingFlapIds.Of(prefab.name)>=9)continue;
            var wing=prefab.GetComponentInChildren<Wing>(true);if(!FlapsPlugin.Supports(wing))continue;
            var filter=wing.GetComponent<MeshFilter>();if(filter && filter.sharedMesh && filter.sharedMesh.subMeshCount>=1 && filter.sharedMesh.subMeshCount<=2)
                WingGeometryCache.Request(filter.sharedMesh,FlapsPlugin.SupportsSpoilers(wing));
        }
    }
}

[HarmonyPatch(typeof(BuildingPart),"Start")]
internal static class DefaultBigWingModel
{
    static void Postfix(BuildingPart __instance)
    {
        var wing=__instance.GetComponent<Wing>();if(!FlapsPlugin.SupportsSpoilers(wing))return;
        var marker=wing.GetComponent<FlapMarker>();if(!marker)marker=wing.gameObject.AddComponent<FlapMarker>();
        marker.ShowDefaultModel();
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Capture the real loaded game wings, including their native control surfaces.
// It does not synthesize replacement models or enable flaps on other wings.
public static class WingCatalogExporter
{
    [Serializable] public sealed class Catalog
    {
        public int schemaVersion=1;
        public string coverage="Loaded Wing assets; verify against the game's part catalog.";
        public List<Entry> wings=new List<Entry>();
        public List<string> warnings=new List<string>();
    }
    [Serializable] public sealed class Entry
    {
        public string id,name;
        public List<Shape> meshes=new List<Shape>();
    }
    [Serializable] public sealed class Shape
    {
        public string name,role;
        public float[] positions,normals;
        public List<SubMesh> submeshes=new List<SubMesh>();
    }
    [Serializable] public sealed class SubMesh { public int[] triangles; }

    public static Catalog Capture()
    {
        var result=new Catalog();
        var seen=new HashSet<string>();
        foreach(Wing wing in Resources.FindObjectsOfTypeAll<Wing>())
        {
            if(!wing)continue;
            string name=wing.name.Replace("(Clone)","").Trim();
            MeshFilter primary=wing.GetComponent<MeshFilter>();
            string key=name+"|"+(primary && primary.sharedMesh?
                primary.sharedMesh.name+"|"+primary.sharedMesh.vertexCount:"children");
            if(!seen.Add(key))continue;
            var entry=new Entry{id=key,name=name};
            var controls=new List<Transform>();
            foreach(WingVisual visual in wing.GetComponentsInChildren<WingVisual>(true))
                if(visual.controlSurface)controls.Add(visual.controlSurface);
            foreach(MeshFilter filter in wing.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh=filter.sharedMesh;
                if(!mesh || !mesh.isReadable)
                {
                    result.warnings.Add(name+"/"+filter.name+": mesh is not readable");
                    continue;
                }
                // Bake every child transform into Wing-local coordinates.
                Matrix4x4 transform=wing.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                Matrix4x4 normalTransform=transform.inverse.transpose;
                Vector3[] p=mesh.vertices,n=mesh.normals;
                var shape=new Shape{name=filter.name,role="wing",
                    positions=new float[p.Length*3],normals=new float[p.Length*3]};
                foreach(Transform control in controls)
                    if(filter.transform==control || filter.transform.IsChildOf(control))shape.role="aileron";
                for(int i=0;i<p.Length;i++)
                {
                    Vector3 position=transform.MultiplyPoint3x4(p[i]);
                    Vector3 normal=n.Length==p.Length?normalTransform.MultiplyVector(n[i]).normalized:Vector3.zero;
                    shape.positions[i*3]=position.x;shape.positions[i*3+1]=position.y;shape.positions[i*3+2]=position.z;
                    shape.normals[i*3]=normal.x;shape.normals[i*3+1]=normal.y;shape.normals[i*3+2]=normal.z;
                }
                bool reflected=transform.determinant<0f;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    int[] triangles=mesh.GetTriangles(sub);
                    if(reflected)for(int i=0;i<triangles.Length;i+=3)
                    {int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
                    shape.submeshes.Add(new SubMesh{triangles=triangles});
                }
                entry.meshes.Add(shape);
            }
            if(entry.meshes.Count>0)result.wings.Add(entry);
        }
        result.wings.Sort((a,b)=>StringComparer.OrdinalIgnoreCase.Compare(a.name,b.name));
        return result;
    }

    public static void Export(string absolutePath)
    {
        if(!Path.IsPathRooted(absolutePath))throw new ArgumentException("Use an absolute catalog path");
        // Run before enabling the flap modifier so captured main meshes are stock.
        File.WriteAllText(absolutePath,JsonUtility.ToJson(Capture(),true));
    }
}

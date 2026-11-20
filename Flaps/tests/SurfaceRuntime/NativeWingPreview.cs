using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Runtime.Serialization.Json;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class PaidUpgradeRuntime
{
    [Serializable] public sealed class PreviewCatalog { public int schemaVersion=1;public List<PreviewWing> wings=new List<PreviewWing>(); }
    [Serializable] public sealed class PreviewWing { public string id,name,version="1.0.0",format="raw";public bool hasFlaps;public List<PreviewMesh> meshes=new List<PreviewMesh>(); }
    [Serializable] public sealed class PreviewMesh { public string name,role;public float[] positions,normals;public List<PreviewSubmesh> submeshes=new List<PreviewSubmesh>();public List<PreviewFrame> frames; }
    [Serializable] public sealed class PreviewSubmesh { public int[] triangles; }
    [Serializable] public sealed class PreviewFrame { public float angle;public float[] positions,normals; }
    static string WingName(string value)=>Regex.Replace(Regex.Replace(value.Replace("(Clone)", ""),"([a-z])([A-Z])","$1 $2"),"([a-z])([0-9])","$1 $2");
    static string WingId(string value)=>WingName(value).Replace(' ','-').ToLowerInvariant();
    PreviewFrame Frame(Wing wing,MeshFilter filter,float angle)
    {
        var mesh=filter.sharedMesh;if(!mesh || !mesh.isReadable)throw new Exception("Unreadable actual wing mesh: "+wing.name+"/"+filter.name);
        var p=mesh.vertices;var n=mesh.normals;if(p.Length!=n.Length)throw new Exception("Missing actual mesh normals");
        var matrix=wing.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;var normal=matrix.inverse.transpose;
        var frame=new PreviewFrame{angle=angle,positions=new float[p.Length*3],normals=new float[p.Length*3]};
        for(int i=0;i<p.Length;i++){
            Vector3 v=matrix.MultiplyPoint3x4(p[i]),u=normal.MultiplyVector(n[i]).normalized;
            frame.positions[3*i]=v.x;frame.positions[3*i+1]=v.y;frame.positions[3*i+2]=v.z;
            frame.normals[3*i]=u.x;frame.normals[3*i+1]=u.y;frame.normals[3*i+2]=u.z;
        }return frame;
    }
    PreviewWing StockEntry(Wing wing)
    {
        var result=new PreviewWing{id=WingId(wing.name),name=WingName(wing.name),hasFlaps=false};
        var visual=wing.GetComponentInChildren<WingVisual>(true);var surface=visual?visual.controlSurface:null;
        foreach(var filter in wing.GetComponentsInChildren<MeshFilter>(true)){
            var f=Frame(wing,filter,0);bool control=surface && (filter.transform==surface||filter.transform.IsChildOf(surface));
            var shape=new PreviewMesh{name=filter.name,role=control?"aileron":"wing",positions=f.positions,normals=f.normals};
            bool reflected=(wing.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix).determinant<0;
            for(int i=0;i<filter.sharedMesh.subMeshCount;i++){
                var indices=filter.sharedMesh.GetTriangles(i);if(reflected)for(int t=0;t<indices.Length;t+=3){int swap=indices[t+1];indices[t+1]=indices[t+2];indices[t+2]=swap;}
                shape.submeshes.Add(new PreviewSubmesh{triangles=indices});
            }result.meshes.Add(shape);
        }return result;
    }
    void ExportPoses(Wing wing, WingVisual visual, FlapMarker marker, PreviewWing entry, PreviewCatalog catalog)
    {
        entry.hasFlaps = true;
        var filters = wing.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mesh in entry.meshes)
            if (mesh.role == "aileron")
            {
                mesh.role = "flap";
                mesh.frames = new List<PreviewFrame>();
            }
        Check(entry.meshes.Exists(mesh => mesh.frames != null), "Preview highlights the actual hinged mesh: " + wing.name);
        for (int angle = 0; angle <= 30; angle++)
        {
            if (angle > 0) marker.Advance(1f / 45f);
            NativeUpdate(visual);
            Check(Mathf.Abs(marker.CurrentAngle - angle) < .0001f, "Runtime-exported pose " + angle + ": " + wing.name);
            for (int i = 0; i < filters.Length; i++)
                if (entry.meshes[i].frames != null) entry.meshes[i].frames.Add(Frame(wing, filters[i], angle));
        }
        catalog.wings.Add(entry);
    }
    string SerializeCatalog(PreviewCatalog catalog)
    {
        using(var stream=new MemoryStream()){
            new DataContractJsonSerializer(typeof(PreviewCatalog)).WriteObject(stream,catalog);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}

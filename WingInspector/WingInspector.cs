using BepInEx;
using System;
using System.IO;
using UnityEngine;

[BepInPlugin("com.andrewstack.winginspector", "Wing Inspector", "1.0.0")]
public class WingInspector : BaseUnityPlugin
{
    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F6)) return;
        Wing[] wings = Resources.FindObjectsOfTypeAll<Wing>();
        Logger.LogInfo("=== WING INSPECTION START: " + wings.Length + " wings ===");
        foreach (Wing wing in wings)
        {
            if (!wing.gameObject.scene.IsValid()) continue;
            Logger.LogInfo("WING " + Path(wing.transform) + " active=" + wing.gameObject.activeInHierarchy + " area=" + wing.area + " lift=" + wing.liftForce + " drag=" + wing.dragForce);
            foreach (WingVisual visual in wing.GetComponentsInChildren<WingVisual>(true))
                Logger.LogInfo(" VISUAL " + Path(visual.transform) + " surface=" + (visual.controlSurface ? Path(visual.controlSurface) : "null") + " maxAngle=" + visual.maxAngle);
            foreach (Transform child in wing.GetComponentsInChildren<Transform>(true))
                Logger.LogInfo(" NODE " + Path(child) + " mesh=" + (child.GetComponent<MeshFilter>() ? child.GetComponent<MeshFilter>().sharedMesh.name : "-") + " local=" + child.localPosition + " rotation=" + child.localEulerAngles);
        }
        Logger.LogInfo("=== WING INSPECTION END ===");
        foreach (Wing wing in wings)
        {
            if (!wing.gameObject.scene.IsValid() || !wing.gameObject.activeInHierarchy || !wing.name.Contains("BigWing")) continue;
            MeshFilter filter = wing.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) continue;
            Mesh mesh = filter.sharedMesh;
            Logger.LogInfo("BIGWING MESH name=" + mesh.name + " readable=" + mesh.isReadable + " vertices=" + mesh.vertexCount + " submeshes=" + mesh.subMeshCount);
            Renderer rend = wing.GetComponent<Renderer>();
            if (rend) foreach (Material mat in rend.sharedMaterials) Logger.LogInfo("BIGWING MATERIAL " + (mat ? mat.name : "null"));
            if (!mesh.isReadable) break;
            try {
                using (MemoryStream ms = new MemoryStream()) using (BinaryWriter writer = new BinaryWriter(ms)) {
                    Vector3[] vertices = mesh.vertices; Vector2[] uv = mesh.uv; Color[] colors = mesh.colors;
                    writer.Write(vertices.Length); writer.Write(mesh.subMeshCount);
                    foreach (Vector3 v in vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                    writer.Write(uv.Length); foreach (Vector2 u in uv) {writer.Write(u.x); writer.Write(u.y);}
                    writer.Write(colors.Length); foreach (Color c in colors) {writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a);}
                    for (int i=0; i<mesh.subMeshCount; i++) { int[] tris=mesh.GetTriangles(i); writer.Write(tris.Length); foreach (int index in tris) writer.Write(index); }
                    writer.Flush(); Logger.LogInfo("BIGWING_MESH_BASE64 " + Convert.ToBase64String(ms.ToArray()));
                }
            } catch (Exception ex) { Logger.LogError("BIGWING MESH error " + ex); }
            break;
        }
    }
    private static string Path(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}


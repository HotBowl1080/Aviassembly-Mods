using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace MachWorks.BackCountryOperations
{
    internal sealed class ValleyStrip : IDisposable
    {
        internal static readonly Vector3 ChunkOrigin = new Vector3(0f, 0f, 1000f);
        internal static readonly Vector2 Blocker = new Vector2(123.132561f, 1350.59509f);
        internal static readonly Vector3 FirstEnd = new Vector3(110f, 50.404f, 1260f);
        internal static readonly Vector3 SecondEnd = new Vector3(110f, 48.921f, 1360f);
        internal static readonly Vector3 Center = (FirstEnd + SecondEnd) * 0.5f;
        internal const float HalfLength = 50f;
        internal const float HalfWidth = 12f;

        private readonly ManualLogSource logger;
        private GameObject markings;
        private Material material;
        private Material surfaceMaterial;
        private Mesh mesh;
        private readonly List<Mesh> surfaceMeshes = new List<Mesh>();
        private bool warnedBlocked;
        internal bool Verified { get; private set; }
        internal Vector3? CargoBay { get; private set; }

        internal ValleyStrip(ManualLogSource logger)
        {
            this.logger = logger;
        }

        internal void Update()
        {
            if (markings != null)
                return;

            foreach (var tile in UnityEngine.Object.FindObjectsByType<global::Terrain>(FindObjectsSortMode.None))
            {
                var ground = tile.GetComponent<MeshCollider>();
                if (ground == null || !ground.enabled || ground.sharedMesh == null ||
                    !TrySample(ground, FirstEnd, out var first) || !TrySample(ground, SecondEnd, out var second))
                    continue;

                if (Mathf.Abs(first.y - FirstEnd.y) > 2f || Mathf.Abs(second.y - SecondEnd.y) > 2f)
                    return;

                if (IsObstructed(ground, (first + second) * 0.5f, new Vector3(HalfWidth, 8f, HalfLength)))
                {
                    if (!warnedBlocked)
                        logger.LogWarning("Valley Strip footprint is obstructed. The site has not been verified.");
                    warnedBlocked = true;
                    return;
                }

                warnedBlocked = false;
                Verified = true;
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (shader == null)
                    return;

                var vertices = new List<Vector3>();
                for (var station = 0; station <= 4; station++)
                {
                    var center = Vector3.Lerp(first, second, Mathf.Lerp(.025f, .975f, station / 4f));
                    var length = station == 0 || station == 4 ? 4.5f : 1.8f;
                    AddMark(ground, vertices, center - Vector3.right * (HalfWidth - 0.65f), 0.8f, length);
                    AddMark(ground, vertices, center + Vector3.right * (HalfWidth - 0.65f), 0.8f, length);
                }

                if (!CargoBay.HasValue)
                    CargoBay = FindBay(ground);
                if (CargoBay.HasValue && !IsObstructed(ground, CargoBay.Value, new Vector3(6f, 5f, 7f)))
                {
                    var bay = CargoBay.Value;
                    AddMark(ground, vertices, bay + Vector3.forward * 7f, 12f, 0.6f);
                    AddMark(ground, vertices, bay - Vector3.forward * 7f, 12f, 0.6f);
                    AddMark(ground, vertices, bay - Vector3.right * 6f, 0.6f, 14f);
                    AddMark(ground, vertices, bay + Vector3.right * 6f, 0.6f, 14f);
                }
                else
                {
                    CargoBay = null;
                }

                if (vertices.Count == 0)
                    return;

                ClearSurfaceMeshes();
                AddMarkings(tile.transform, shader, vertices);
                AddSurface(ground, first, second, HalfWidth, 29, true);
                if (CargoBay.HasValue)
                {
                    var bay = CargoBay.Value;
                    AddSurface(ground, bay - Vector3.forward * 7f, bay + Vector3.forward * 7f, 6f, 9, false);
                    float side = Mathf.Sign(bay.x - Center.x);
                    var runwayEdge = new Vector3(Center.x + side * (HalfWidth - 1f), bay.y, bay.z);
                    AddSurface(ground, runwayEdge, bay, 4f, 9, false);
                }
                logger.LogInfo($"Valley Strip marked at X 110, Z 1260–1360. Off-runway cargo bay: {CargoBay?.ToString() ?? "unavailable"}.");
                return;
            }
        }

        private static Vector3? FindBay(MeshCollider ground)
        {
            foreach (var offset in new[] { -22f, 22f })
            {
                for (var along = -30; along <= 30; along += 15)
                {
                    var point = Center + new Vector3(offset, 0f, along);
                    if (!TrySample(ground, point, out var bay) ||
                        IsObstructed(ground, bay, new Vector3(6f, 5f, 7f)))
                        continue;

                    if (TrySample(ground, point + new Vector3(-6f, 0f, -7f), out var a) &&
                        TrySample(ground, point + new Vector3(6f, 0f, 7f), out var b) &&
                        TrySample(ground, point + new Vector3(-6f, 0f, 7f), out var c) &&
                        TrySample(ground, point + new Vector3(6f, 0f, -7f), out var d) &&
                        Mathf.Abs(a.y - bay.y) < 1.4f && Mathf.Abs(b.y - bay.y) < 1.4f &&
                        Mathf.Abs(c.y - bay.y) < 1.4f && Mathf.Abs(d.y - bay.y) < 1.4f)
                        return bay;
                }
            }

            return null;
        }

        private static bool IsObstructed(MeshCollider ground, Vector3 center, Vector3 extents)
        {
            foreach (var hit in Physics.OverlapBox(center + Vector3.up * 4f, extents,
                Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit != ground && hit.GetComponentInParent<PlaneContainer>() == null)
                    return true;
            }
            return false;
        }

        private static bool TrySample(MeshCollider ground, Vector3 point, out Vector3 surface)
        {
            if (ground.Raycast(new Ray(point + Vector3.up * 40f, Vector3.down), out var hit, 80f))
            {
                surface = hit.point;
                return true;
            }
            surface = default;
            return false;
        }

        private static void AddMark(MeshCollider ground, List<Vector3> vertices, Vector3 center, float width, float length)
        {
            var x = Vector3.right * width / 2f;
            var z = Vector3.forward * length / 2f;
            if (!TrySample(ground, center - x - z, out var a) || !TrySample(ground, center - x + z, out var b) ||
                !TrySample(ground, center + x + z, out var c) || !TrySample(ground, center + x - z, out var d))
                return;

            var lift = Vector3.up * 0.12f;
            vertices.AddRange(new[] { a + lift, b + lift, c + lift, a + lift, c + lift, d + lift });
        }

        private void AddMarkings(Transform parent, Shader shader, List<Vector3> vertices)
        {
            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);
            if (material == null)
                material = new Material(shader) { color = new Color(0.83f, 0.79f, 0.65f) };

            markings = new GameObject("Valley Strip markings", typeof(MeshFilter), typeof(MeshRenderer));
            markings.transform.SetParent(parent, false);
            for (var i = 0; i < vertices.Count; i++)
                vertices[i] = parent.InverseTransformPoint(vertices[i]);
            var indices = new int[vertices.Count];
            for (var i = 0; i < indices.Length; i++)
                indices[i] = i;

            mesh = new Mesh { name = "Valley Strip ground markers" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            markings.GetComponent<MeshFilter>().sharedMesh = mesh;
            markings.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void AddSurface(MeshCollider ground, Vector3 first, Vector3 second, float halfWidth, int lengthSamples, bool runway)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return;

            const int widthSamples = 11;
            const float feather = 3f;
            float expandedHalfWidth = halfWidth + feather;
            var along = second - first;
            along.y = 0f;
            float length = along.magnitude;
            if (length < 0.5f)
                return;
            var side = Vector3.Cross(Vector3.up, along / length);
            var vertices = new List<Vector3>(lengthSamples * widthSamples);
            var colors = new List<Color>(vertices.Capacity);
            var triangles = new List<int>();
            var parent = markings.transform;
            for (var row = 0; row < lengthSamples; row++)
            {
                var distance = Mathf.Lerp(-feather, length + feather, row / (lengthSamples - 1f));
                var center = Vector3.LerpUnclamped(first, second, distance / length);
                for (var column = 0; column < widthSamples; column++)
                {
                    var across = Mathf.Lerp(-expandedHalfWidth, expandedHalfWidth, column / (widthSamples - 1f));
                    if (!TrySample(ground, center + side * across, out var surface))
                        return;

                    vertices.Add(parent.InverseTransformPoint(surface + Vector3.up * (runway ? .07f : .085f)));
                    var edge = Mathf.Min(expandedHalfWidth - Mathf.Abs(across),
                        Mathf.Min(distance + feather, length + feather - distance));
                    var grain = Mathf.PerlinNoise(surface.x * .18f, surface.z * .15f);
                    var track = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.Abs(across) - halfWidth * .25f) / 3f);
                    var grass = new Color(.39f, .41f, .24f);
                    var wear = new Color(.60f, .51f, .33f);
                    var color = Color.Lerp(grass, wear, Mathf.Clamp01(grain * .65f + track * .3f));
                    var opacity = runway ? .38f + .17f * track : .49f;
                    color.a = Mathf.SmoothStep(0f, opacity, Mathf.Clamp01((edge + (grain - .5f) * .65f) / feather));
                    if (row == 0 || row == lengthSamples - 1 || column == 0 || column == widthSamples - 1)
                        color.a = 0f;
                    colors.Add(color);
                    if (row == 0 || column == 0)
                        continue;

                    var c = row * widthSamples + column;
                    var a = c - widthSamples - 1;
                    var b = c - 1;
                    var d = c - widthSamples;
                    triangles.AddRange(new[] { a, b, c, a, c, d });
                }
            }

            if (surfaceMaterial == null)
            {
                surfaceMaterial = new Material(shader);
                surfaceMaterial.mainTexture = Texture2D.whiteTexture;
            }

            var surfaceMesh = new Mesh { name = runway ? "Valley Strip worn grass" : "Compacted apron grass" };
            surfaceMesh.SetVertices(vertices);
            surfaceMesh.SetColors(colors);
            surfaceMesh.SetTriangles(triangles, 0);
            surfaceMesh.RecalculateNormals();
            surfaceMeshes.Add(surfaceMesh);
            var surfaceObject = new GameObject(surfaceMesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            surfaceObject.transform.SetParent(parent, false);
            surfaceObject.GetComponent<MeshFilter>().sharedMesh = surfaceMesh;
            surfaceObject.GetComponent<MeshRenderer>().sharedMaterial = surfaceMaterial;
        }

        private void ClearSurfaceMeshes()
        {
            foreach (var surfaceMesh in surfaceMeshes)
                if (surfaceMesh != null)
                    UnityEngine.Object.Destroy(surfaceMesh);
            surfaceMeshes.Clear();
        }

        public void Dispose()
        {
            if (markings != null)
                UnityEngine.Object.Destroy(markings);
            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);
            if (material != null)
                UnityEngine.Object.Destroy(material);
            ClearSurfaceMeshes();
            if (surfaceMaterial != null)
                UnityEngine.Object.Destroy(surfaceMaterial);
            markings = null;
            mesh = null;
            material = null;
            surfaceMaterial = null;
            Verified = false;
            CargoBay = null;
            warnedBlocked = false;
        }
    }
}

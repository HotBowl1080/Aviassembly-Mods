using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MachWorks.BackCountryOperations
{
    internal sealed class TownAirfieldWorks : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<RunwayGenerator, GameObject> Pavement =
            AccessTools.FieldRefAccess<RunwayGenerator, GameObject>("runway");
        private static readonly AccessTools.FieldRef<RunwayGenerator, GameObject> Shoulder =
            AccessTools.FieldRefAccess<RunwayGenerator, GameObject>("outline");
        private static readonly AccessTools.FieldRef<RunwayGenerator, GameObject> LeftMarking =
            AccessTools.FieldRefAccess<RunwayGenerator, GameObject>("leftMarking");
        private static readonly AccessTools.FieldRef<RunwayGenerator, GameObject> RightMarking =
            AccessTools.FieldRefAccess<RunwayGenerator, GameObject>("rightMarking");
        private static readonly AccessTools.FieldRef<RunwayGenerator, List<GameObject>> CenterMarkings =
            AccessTools.FieldRefAccess<RunwayGenerator, List<GameObject>>("markings");
        private static readonly AccessTools.FieldRef<RunwayGenerator, Vector2> RunwaySize =
            AccessTools.FieldRefAccess<RunwayGenerator, Vector2>("runwaySize");

        private readonly List<Material> materials = new List<Material>();
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private Vector3 bayCenter;
        private Vector2 dimensions;
        private bool maplewood, village;
        private Transform bay;
        private Renderer apron, dirtStrip;
        private Material washedGravel, gradedGravel, stripDirt;

        internal static void Prepare(AirportManager manager)
        {
            if (!manager)
                return;
            var home = manager.GetHomeBase();
            foreach (var runway in Object.FindObjectsByType<RunwayGenerator>(FindObjectsSortMode.None))
            {
                if (!Pavement(runway))
                    continue;
                var position = runway.transform.position;
                bool maplewood = Near(position, TownContracts.MaplewoodPosition);
                bool village = maplewood || Near(position, TownContracts.SpringHillsPosition);
                bool baseBay = home != null && Near(position, home.position) && CampaignProgress.IsDone(ValleyProgress.MaplewoodDrains);
                if (!village && !baseBay)
                    continue;
                if (!runway.TryGetComponent<TownAirfieldWorks>(out var works))
                {
                    works = runway.gameObject.AddComponent<TownAirfieldWorks>();
                    works.Build(runway, village, maplewood, baseBay);
                }
                works.FitToTerrain();
                if (works.apron && works.maplewood && CampaignProgress.IsDone(ValleyProgress.MaplewoodDrains))
                    works.apron.sharedMaterial = works.gradedGravel;
            }
        }

        private static bool Near(Vector3 position, Vector3 airport) =>
            new Vector2(position.x - airport.x, position.z - airport.z).sqrMagnitude < 85f * 85f;

        private void Build(RunwayGenerator runway, bool village, bool isMaplewood, bool baseBay)
        {
            dimensions = RunwaySize(runway);
            this.village = village;
            maplewood = isMaplewood;
            if (village)
            {
                var dirt = Paint("Compacted dirt", new Color(.51f, .39f, .27f));
                dirt.mainTexture = Grain("Dirt and aggregate", .60f);
                dirt.mainTextureScale = new Vector2(4f, 50f);
                SetSurface(Pavement(runway), dirt);
                stripDirt = GroundPaint("Dirt runway surface", dirt.mainTexture, new Color(.61f, .46f, .31f));
                var shoulder = Paint("Grassy runway shoulder", new Color(.41f, .41f, .29f));
                shoulder.mainTexture = dirt.mainTexture;
                shoulder.mainTextureScale = dirt.mainTextureScale;
                SetSurface(Shoulder(runway), shoulder);
                SetSurface(LeftMarking(runway), null);
                SetSurface(RightMarking(runway), null);
                if (CenterMarkings(runway) is List<GameObject> markings)
                    foreach (var marking in markings)
                        SetSurface(marking, null);

                var markers = Paint("Chalk runway edge markers", new Color(.76f, .74f, .61f));
                for (int side = -1; side <= 1; side += 2)
                    for (int i = -4; i <= 4; i++)
                        WindFarmWorks.Box(transform, "Dirt runway edge marker",
                            new Vector3(side * (dimensions.x * .5f - .65f), .045f, dimensions.y * .111f * i),
                            new Vector3(.62f, .025f, i == -4 || i == 4 ? 4f : 1.5f), markers);
            }

            if (!maplewood && !baseBay)
                return;
            bayCenter = maplewood ? TownContracts.MaplewoodBay : TownContracts.BaseBay;
            bay = new GameObject(maplewood ? "Maplewood delivery bay" : "Base pump return bay").transform;
            bay.SetParent(transform, false);
            var paint = Paint("Delivery bay paint", new Color(.85f, .64f, .32f));
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    WindFarmWorks.Box(bay, "Delivery bay corner", bayCenter + new Vector3(x * 8.9f, .09f, z * 10.5f),
                        new Vector3(3.2f, .035f, .32f), paint);
                    WindFarmWorks.Box(bay, "Delivery bay corner", bayCenter + new Vector3(x * 10.5f, .09f, z * 8.9f),
                        new Vector3(.32f, .035f, 3.2f), paint);
                }
        }

        private static void SetSurface(GameObject item, Material material)
        {
            if (!item)
                return;
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                if (material)
                    renderer.sharedMaterial = material;
                else
                    renderer.enabled = false;
            }
        }

        private void FitToTerrain()
        {
            if ((!village || dirtStrip) && (!bay || apron))
                return;
            var plane = PlaneContainer.Instance;
            if (!plane || (plane.transform.position - transform.position).sqrMagnitude > 1100f * 1100f)
                return;
            var ground = new List<MeshCollider>();
            foreach (var tile in Object.FindObjectsByType<global::Terrain>(FindObjectsSortMode.None))
                if (tile.TryGetComponent<MeshCollider>(out var collider))
                    ground.Add(collider);
            if (village && !dirtStrip)
                BuildDirtStrip(ground);
            if (bay && !apron)
                TryBuildApron(ground);
        }

        private void BuildDirtStrip(List<MeshCollider> ground)
        {
            const int columns = 9;
            int rows = Mathf.CeilToInt(dimensions.y / 8f) + 1;
            var vertices = new Vector3[columns * rows];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new List<int>();
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    float v = row / (rows - 1f);
                    float u = column / (columns - 1f);
                    float z = (v - .5f) * dimensions.y;
                    float x = (u - .5f) * dimensions.x;
                    if (!TryGround(ground, new Vector3(x, 0f, z), out var local))
                        return;
                    int index = row * columns + column;
                    vertices[index] = local + Vector3.up * .085f;
                    uv[index] = new Vector2(x / 4f, z / 4f);
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u) * 10f, Mathf.Min(v, 1f - v) * 55f);
                    float grass = .88f + .12f * Mathf.PerlinNoise(x * .18f, z * .06f);
                    colors[index] = new Color(grass, grass, grass, Mathf.Clamp01(edge));
                    AddQuad(triangles, row, column, index, columns);
                }
            dirtStrip = CreateSurface(transform, "Graded dirt strip", vertices, uv, colors, triangles, stripDirt);
        }

        private void TryBuildApron(List<MeshCollider> ground)
        {

            const int columns = 15;
            const int rows = 14;
            var vertices = new Vector3[columns * rows];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new List<int>();
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    float across = column / (columns - 1f);
                    float along = row / (rows - 1f);
                    float halfWidth = Mathf.Lerp(5f, 15f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.08f, .6f, across)));
                    float x = Mathf.Lerp(dimensions.x * .5f, bayCenter.x + 14f, across);
                    float z = bayCenter.z + (along * 2f - 1f) * halfWidth;
                    if (!TryGround(ground, new Vector3(x, 0f, z), out var local))
                        return;
                    int index = row * columns + column;
                    vertices[index] = local + Vector3.up * .06f;
                    uv[index] = new Vector2(x / 3f, z / 3f);
                    float edge = Mathf.Min(1f - across, Mathf.Min(along, 1f - along));
                    colors[index] = new Color(1f, 1f, 1f, Mathf.Clamp01(edge * 8f));
                    AddQuad(triangles, row, column, index, columns);
                }

            foreach (Transform corner in bay)
                if (TryGround(ground, corner.localPosition, out var local))
                    corner.localPosition = local + Vector3.up * .09f;

            var gravel = Grain("Apron gravel", .56f);
            washedGravel = GroundPaint("Weathered apron", gravel, new Color(.66f, .55f, .40f));
            gradedGravel = GroundPaint("Graded gravel", gravel, new Color(.83f, .80f, .73f));
            var material = maplewood && !CampaignProgress.IsDone(ValleyProgress.MaplewoodDrains) ? washedGravel : gradedGravel;
            apron = CreateSurface(bay, "Gravel taxi spur", vertices, uv, colors, triangles, material);
            if (maplewood)
            {
                var ditch = bayCenter + new Vector3(15f, 0f, -11f);
                if (TryGround(ground, ditch, out var local))
                {
                    var stone = Paint("Drainage culvert concrete", new Color(.52f, .50f, .43f));
                    var opening = Paint("Drainage culvert opening", new Color(.23f, .22f, .19f));
                    WindFarmWorks.Box(bay, "Drainage culvert", local + Vector3.up * .27f, new Vector3(1.8f, .54f, 1.7f), stone);
                    WindFarmWorks.Box(bay, "Drainage culvert mouth", local + new Vector3(0f, .23f, -.86f),
                        new Vector3(1.12f, .34f, .025f), opening);
                }
            }
        }

        private static void AddQuad(List<int> triangles, int row, int column, int index, int columns)
        {
            if (column == 0 || row == 0)
                return;
            triangles.Add(index - columns - 1);
            triangles.Add(index - 1);
            triangles.Add(index - columns);
            triangles.Add(index - columns);
            triangles.Add(index - 1);
            triangles.Add(index);
        }

        private Renderer CreateSurface(Transform parent, string name, Vector3[] vertices, Vector2[] uv, Color[] colors,
            List<int> triangles, Material material)
        {
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, colors = colors, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            meshes.Add(mesh);
            var surface = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(parent, false);
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private Material GroundPaint(string name, Texture texture, Color color)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = name, mainTexture = texture, color = color };
            materials.Add(material);
            return material;
        }

        private bool TryGround(List<MeshCollider> ground, Vector3 local, out Vector3 result)
        {
            var world = transform.TransformPoint(local);
            foreach (var tile in ground)
                if (tile.Raycast(new Ray(world + Vector3.up * 20f, Vector3.down), out var hit, 40f))
                {
                    result = transform.InverseTransformPoint(hit.point);
                    return true;
                }
            result = default;
            return false;
        }

        private Material Paint(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", .05f);
            materials.Add(material);
            return material;
        }

        private Texture2D Grain(string name, float brightness)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
                { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float light = brightness + .20f * Mathf.PerlinNoise(x * .15f, y * .15f) +
                        .19f * Mathf.PerlinNoise(x * .57f + 6f, y * .51f);
                    pixels[y * size + x] = new Color(light, light, light);
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            textures.Add(texture);
            return texture;
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes)
                Destroy(mesh);
            foreach (var material in materials)
                Destroy(material);
            foreach (var texture in textures)
                Destroy(texture);
        }
    }
}

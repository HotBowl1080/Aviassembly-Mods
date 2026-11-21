using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MachWorks.BackCountryOperations
{
    internal sealed class WindFarmWorks : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<RunwayGenerator, GameObject> Pavement =
            AccessTools.FieldRefAccess<RunwayGenerator, GameObject>("runway");
        private static readonly AccessTools.FieldRef<RunwayGenerator, Vector2> RunwaySize =
            AccessTools.FieldRefAccess<RunwayGenerator, Vector2>("runwaySize");

        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<GameObject> cracks = new List<GameObject>();
        private readonly List<GameObject> patches = new List<GameObject>();
        private RunwayGenerator runway;
        private Transform surface, cart;
        private GameObject wornMarkings;
        private readonly GameObject[] turnarounds = new GameObject[2];
        private Texture2D asphaltWear, gravelTexture;
        private Vector2 dimensions;
        private float animationStart = -1f;
        private BasePhase phase;
        private bool gravelReady;
        private float nextApronCheck;
        private float nextTurnaroundCheck;

        private enum BasePhase { Worn, Repairing, Repaired }

        internal static void Prepare(Airport airport)
        {
            if (airport == null)
                return;
            foreach (var generator in Object.FindObjectsByType<RunwayGenerator>(FindObjectsSortMode.None))
            {
                var offset = generator.transform.position - airport.position;
                if (offset.x * offset.x + offset.z * offset.z > 85f * 85f || !Pavement(generator))
                    continue;
                if (!generator.TryGetComponent<WindFarmWorks>(out _))
                {
                    var works = generator.gameObject.AddComponent<WindFarmWorks>();
                    works.runway = generator;
                    works.Build();
                }
                return;
            }
        }

        private void Build()
        {
            dimensions = RunwaySize(runway);
            surface = new GameObject("Wind Farm runway repairs").transform;
            surface.SetParent(transform, false);
            var weather = MakeWeatheredAsphalt();
            var fissure = Paint("Cracked asphalt", new Color(.085f, .079f, .086f));
            var patch = Paint("New asphalt", new Color(.19f, .18f, .20f));
            var white = Paint("Runway paint", new Color(.89f, .87f, .81f));
            var depot = Paint("Maintenance shed", new Color(.55f, .51f, .41f));
            var hazard = Paint("Service yellow", new Color(.87f, .65f, .24f));
            var dark = Paint("Cart chassis", new Color(.18f, .19f, .21f));

            // Low-profile visual wear on the existing pavement. Preserve the stock runway collider.
            for (int i = 0; i < 10; i++)
            {
                float z = dimensions.y * (-.37f + .079f * i) + Mathf.Sin(i * 4.3f) * 8f;
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * dimensions.x * (.22f + .028f * Mathf.Cos(i * 7f));
                WeatherStain(new Vector3(x, .024f, z), new Vector2(dimensions.x * .46f, 18f + i % 4 * 4f),
                    i * 180f, weather);
                var fracture = new GameObject("Surface cracks");
                fracture.transform.SetParent(surface, false);
                var cursor = new Vector3(x - side * 3.8f, .043f, z - 5f);
                for (int segment = 0; segment < 7; segment++)
                {
                    var next = cursor + new Vector3(side * (1.1f + .45f * Mathf.Sin(segment * 6.3f + i)), 0f,
                        1.15f + .85f * Mathf.Sin(segment * 4.7f + i * 2.5f));
                    Crack(fracture.transform, cursor, next, Mathf.Lerp(.21f, .085f, segment / 6f), fissure);
                    if (segment % 2 == 1)
                    {
                        var split = cursor + new Vector3(side * .85f, 0f, -1.7f - .45f * (i % 4));
                        Crack(fracture.transform, cursor, split, .10f, fissure);
                        Crack(fracture.transform, split, split + new Vector3(-side * .55f, 0f, -1.25f), .062f, fissure);
                    }
                    cursor = next;
                }
                if (i < 5)
                {
                    cracks.Add(fracture);
                    patches.Add(Patch(new Vector3(x, .049f, z), new Vector2(dimensions.x * .44f, 17f + i % 3 * 3f), i, patch));
                }
            }

            var oldPaint = Paint("Weathered runway paint", new Color(.59f, .56f, .48f));
            wornMarkings = BuildRunwayMarkings(oldPaint, false);
            patches.Add(BuildRunwayMarkings(white, true));

            var bay = OpeningAirfields.ServiceOffset;
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(surface, "Service bay corner", bay + new Vector3(x * 11f, .04f, z * 12.5f), new Vector3(3f, .02f, .34f), hazard);
                    Box(surface, "Service bay corner", bay + new Vector3(x * 12.4f, .04f, z * 11.2f), new Vector3(.34f, .02f, 3f), hazard);
                }
            var shed = bay + new Vector3(18f, 0f, 13f);
            Box(surface, "Wind Farm service shed", shed + Vector3.up * 1.4f, new Vector3(7f, 2.8f, 5f), depot);
            Box(surface, "Service shed roof", shed + Vector3.up * 2.9f, new Vector3(7.4f, .28f, 5.4f), dark);
            Box(surface, "Service shed roller door", shed + new Vector3(-3.52f, 1.1f, 0f), new Vector3(.08f, 2.2f, 2.8f), dark);

            cart = new GameObject("Runway maintenance cart").transform;
            cart.SetParent(surface, false);
            Box(cart, "Cart bed", Vector3.up * .65f, new Vector3(1.9f, .35f, 3.1f), hazard);
            Box(cart, "Cab", new Vector3(0f, 1.12f, .74f), new Vector3(1.65f, .86f, 1.15f), hazard);
            Box(cart, "Windscreen", new Vector3(0f, 1.30f, 1.33f), new Vector3(1.48f, .38f, .035f), dark);
            Box(cart, "Patch material", new Vector3(0f, 1f, -.75f), new Vector3(1.43f, .43f, 1.1f), dark);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Box(cart, "Cart wheel", new Vector3(x * .92f, .33f, z * 1.03f), new Vector3(.36f, .63f, .64f), dark);
            cart.gameObject.SetActive(false);

            bool repaired = CampaignProgress.IsDone(ValleyProgress.WindRunwayRepair);
            phase = repaired ? BasePhase.Repaired : BasePhase.Worn;
            ApplyReveal(repaired ? patches.Count : 0);
        }

        private GameObject BuildRunwayMarkings(Material paint, bool repaired)
        {
            var markings = new GameObject(repaired ? "Finished runway markings" : "Worn threshold paint");
            markings.transform.SetParent(surface, false);
            float endInset = Mathf.Min(18f, dimensions.y * .12f);
            float threshold = dimensions.y * .5f - endInset;
            float stripeWidth = Mathf.Clamp(dimensions.x * .045f, .45f, 1.4f);
            float stripeLength = Mathf.Min(9f, endInset * .55f);
            float spacing = dimensions.x * .105f;
            for (int end = -1; end <= 1; end += 2)
            {
                for (int stripe = -3; stripe <= 3; stripe++)
                {
                    if (stripe == 0 || (!repaired && (stripe + end + 4) % 3 != 0))
                        continue;
                    float length = repaired ? stripeLength : stripeLength * (.40f + .13f * Mathf.Abs(stripe));
                    Box(markings.transform, "Threshold bar", new Vector3(stripe * spacing, .061f, end * threshold),
                        new Vector3(stripeWidth, .018f, length), paint);
                }

                if (!repaired)
                    continue;
                for (int side = -1; side <= 1; side += 2)
                    Box(markings.transform, "Aiming point", new Vector3(side * dimensions.x * .24f, .061f,
                        end * (threshold - endInset * 1.65f)), new Vector3(stripeWidth * 1.65f, .018f, stripeLength * 1.4f), paint);
            }

            return markings;
        }

        private Material MakeWeatheredAsphalt()
        {
            const int size = 96;
            asphaltWear = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Feathered asphalt wear" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f);
                    float v = y / (size - 1f);
                    float mottling = Mathf.PerlinNoise(u * 8.3f, v * 11.7f);
                    float grain = Mathf.PerlinNoise(u * 50f + 23f, v * 54f);
                    float edge = new Vector2((u - .5f) * 2f, (v - .5f) * 2f).magnitude;
                    float opacity = Mathf.Clamp01((1f - edge + (mottling - .5f) * .4f) * 3f) * (.3f + .52f * mottling);
                    float gray = .22f + .16f * mottling + .09f * grain;
                    pixels[y * size + x] = new Color(gray * .99f, gray * .97f, gray, opacity);
                }
            asphaltWear.SetPixels32(pixels);
            asphaltWear.Apply(true, true);
            var material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"))
                { name = "Asphalt aggregate", mainTexture = asphaltWear };
            materials.Add(material);
            return material;
        }

        private void WeatherStain(Vector3 center, Vector2 size, float rotation, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Mottled asphalt";
            var collider = quad.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            quad.transform.SetParent(surface, false);
            quad.transform.localPosition = center;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.transform.localRotation = Quaternion.Euler(90f, rotation, 0f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void Crack(Transform parent, Vector3 from, Vector3 to, float width, Material material)
        {
            var along = to - from;
            var crack = Box(parent, "Pavement fissure", (from + to) * .5f, new Vector3(width, .011f, along.magnitude + .08f), material);
            crack.transform.localRotation = Quaternion.LookRotation(along, Vector3.up);
        }

        private GameObject Patch(Vector3 center, Vector2 size, int seed, Material material)
        {
            const int sides = 14;
            var vertices = new Vector3[sides + 1];
            var triangles = new int[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                float radius = .47f + .054f * Mathf.Sin(i * 4.78f + seed * 6.32f);
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * size.x * radius, 0f, Mathf.Sin(angle) * size.y * radius);
                triangles[i * 3 + 1] = (i + 1) % sides + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Asphalt repair edge", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            meshes.Add(mesh);
            var piece = new GameObject("Asphalt patch", typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(surface, false);
            piece.transform.localPosition = center;
            piece.GetComponent<MeshFilter>().sharedMesh = mesh;
            piece.GetComponent<MeshRenderer>().sharedMaterial = material;
            return piece;
        }

        private void LateUpdate()
        {
            if (!surface)
                return;

            var pavement = Pavement(runway);
            if (pavement && pavement.TryGetComponent<MeshRenderer>(out var renderer))
            {
                var pos = surface.localPosition;
                pos.y = transform.InverseTransformPoint(new Vector3(transform.position.x, renderer.bounds.max.y, transform.position.z)).y;
                surface.localPosition = pos;
            }

            if (!gravelReady && Time.unscaledTime >= nextApronCheck)
            {
                nextApronCheck = Time.unscaledTime + 1f;
                gravelReady = TryBuildGravelApron();
            }

            if (phase == BasePhase.Repaired && Time.unscaledTime >= nextTurnaroundCheck &&
                (!turnarounds[0] || !turnarounds[1]))
            {
                nextTurnaroundCheck = Time.unscaledTime + 2f;
                TryBuildTurnarounds();
            }

            if (phase == BasePhase.Worn && CampaignProgress.IsDone(ValleyProgress.WindRunwayRepair))
            {
                phase = BasePhase.Repairing;
                animationStart = Time.time;
                cart.gameObject.SetActive(true);
            }
            if (phase != BasePhase.Repairing)
                return;

            float t = Mathf.Clamp01((Time.time - animationStart) / 6f);
            float startZ = dimensions.y * -.43f;
            float endZ = dimensions.y * -.02f;
            cart.localPosition = new Vector3(dimensions.x * -.16f, .045f, Mathf.Lerp(startZ, endZ, t));
            ApplyReveal(Mathf.Min(patches.Count, Mathf.FloorToInt(t * (patches.Count + .9f))));
            if (t >= 1f)
            {
                phase = BasePhase.Repaired;
                cart.gameObject.SetActive(false);
            }
        }

        private void ApplyReveal(int count)
        {
            wornMarkings.SetActive(count < patches.Count);
            for (int i = 0; i < patches.Count; i++)
            {
                bool revealed = i < count;
                patches[i].SetActive(revealed);
                if (i < cracks.Count)
                    cracks[i].SetActive(!revealed);
            }
        }

        private void TryBuildTurnarounds()
        {
            var grounds = new List<MeshCollider>();
            foreach (var terrain in Object.FindObjectsByType<global::Terrain>(FindObjectsSortMode.None))
            {
                var collider = terrain.GetComponent<MeshCollider>();
                if (collider && collider.enabled && collider.sharedMesh)
                    grounds.Add(collider);
            }
            if (grounds.Count == 0)
                return;

            for (var end = 0; end < turnarounds.Length; end++)
            {
                if (turnarounds[end])
                    continue;
                float z = (end == 0 ? -1f : 1f) * (dimensions.y * .5f - 21f);
                foreach (int side in new[] { -1, 1 })
                {
                    if (!TryBuildTurnaround(grounds, side, z, out var pad))
                        continue;
                    turnarounds[end] = pad;
                    break;
                }
            }
        }

        private bool TryBuildTurnaround(List<MeshCollider> grounds, int side, float z, out GameObject pad)
        {
            pad = null;
            // Keep the bulb alongside the runway and out of the departure path.
            const int columns = 13;
            const int rows = 17;
            const float extraWidth = 23f;
            const float halfLength = 19f;
            float runwayEdge = dimensions.x * .5f;
            var vertices = new Vector3[columns * rows];
            var colors = new Color[vertices.Length];
            var triangles = new List<int>();
            float low = float.PositiveInfinity;
            float high = float.NegativeInfinity;
            for (int row = 0; row < rows; row++)
            {
                float along = row / (rows - 1f) * 2f - 1f;
                float bulb = Mathf.Sqrt(Mathf.Max(0f, 1f - along * along));
                for (int column = 0; column < columns; column++)
                {
                    float across = column / (columns - 1f);
                    var local = new Vector3(side * (runwayEdge + (.35f + extraWidth * bulb) * across), 0f, z + along * halfLength);
                    var desired = surface.TransformPoint(local);
                    if (!TryTurnaroundGround(grounds, desired, out var ground) ||
                        ground.normal.y < .975f || (column == 0 && Mathf.Abs(ground.point.y - desired.y) > .65f))
                        return false;

                    low = Mathf.Min(low, ground.point.y);
                    high = Mathf.Max(high, ground.point.y);
                    if (high - low > 2.4f)
                        return false;

                    int i = row * columns + column;
                    vertices[i] = surface.InverseTransformPoint(ground.point + Vector3.up * .075f);
                    float edgeAlpha = Mathf.SmoothStep(0f, .88f,
                        Mathf.Clamp01(Mathf.Min(1f - Mathf.Abs(along), 1f - across) * 10f));
                    float grain = Mathf.PerlinNoise(local.x * .19f, local.z * .21f);
                    var gravel = Color.Lerp(new Color(.34f, .32f, .28f), new Color(.48f, .45f, .38f), grain);
                    if (row == rows / 2 && column < columns / 2)
                        gravel = Color.Lerp(gravel, new Color(.68f, .55f, .33f), .62f);
                    gravel.a = edgeAlpha;
                    colors[i] = gravel;
                    if (row == 0 || column == 0)
                        continue;

                    int a = i - columns - 1, b = i - 1, d = i - columns;
                    if (side == 1)
                        triangles.AddRange(new[] { a, b, i, a, i, d });
                    else
                        triangles.AddRange(new[] { a, i, b, a, d, i });
                }
            }

            var center = surface.TransformPoint(new Vector3(side * (runwayEdge + extraWidth * .5f), 0f, z));
            center.y = high + 2.4f;
            var pavement = Pavement(runway);
            foreach (var hit in Physics.OverlapBox(center, new Vector3(extraWidth * .5f, 4.8f, halfLength),
                surface.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.GetComponentInParent<PlaneContainer>() != null ||
                    (pavement && hit.transform.IsChildOf(pavement.transform)) ||
                    (hit is MeshCollider terrain && grounds.Contains(terrain)))
                    continue;
                return false;
            }

            var shader = Shader.Find("Sprites/Default");
            if (!shader)
                return false;

            var material = new Material(shader) { name = "Compacted turnaround gravel", mainTexture = Texture2D.whiteTexture };
            materials.Add(material);
            var mesh = new Mesh { name = "Terrain-following turnaround", vertices = vertices, colors = colors, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            meshes.Add(mesh);
            pad = new GameObject("Runway end turnaround", typeof(MeshFilter), typeof(MeshRenderer));
            pad.transform.SetParent(surface, false);
            pad.GetComponent<MeshFilter>().sharedMesh = mesh;
            pad.GetComponent<MeshRenderer>().sharedMaterial = material;
            return true;
        }

        private static bool TryTurnaroundGround(List<MeshCollider> grounds, Vector3 point, out RaycastHit hit)
        {
            var ray = new Ray(point + Vector3.up * 12f, Vector3.down);
            foreach (var ground in grounds)
            {
                var bounds = ground.bounds;
                if (point.x >= bounds.min.x && point.x <= bounds.max.x &&
                    point.z >= bounds.min.z && point.z <= bounds.max.z && ground.Raycast(ray, out hit, 24f))
                    return true;
            }
            hit = default;
            return false;
        }

        private bool TryBuildGravelApron()
        {
            var plane = PlaneContainer.Instance;
            if (!plane || (plane.transform.position - transform.position).sqrMagnitude > 1300f * 1300f)
                return false;

            var grounds = Object.FindObjectsByType<global::Terrain>(FindObjectsSortMode.None);
            var bay = OpeningAirfields.ServiceOffset;
            const int columns = 22;
            const int rows = 20;
            var vertices = new Vector3[columns * rows];
            var colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new List<int>();
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    float across = column / (columns - 1f);
                    float along = row / (rows - 1f);
                    float localX = Mathf.Lerp(dimensions.x * .5f + .45f, bay.x + 16f, across);
                    float halfWidth = Mathf.Lerp(6.5f, 16.5f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.15f, .5f, across)));
                    float localZ = bay.z + (along * 2f - 1f) * halfWidth;
                    var desired = transform.TransformPoint(new Vector3(localX, 0f, localZ));
                    bool sampled = false;
                    foreach (var tile in grounds)
                    {
                        var collider = tile.GetComponent<MeshCollider>();
                        if (!collider || !collider.Raycast(new Ray(desired + Vector3.up * 20f, Vector3.down), out var hit, 40f))
                            continue;
                        desired = hit.point + Vector3.up * .065f;
                        sampled = true;
                        break;
                    }
                    if (!sampled)
                        return false;

                    int index = row * columns + column;
                    vertices[index] = transform.InverseTransformPoint(desired);
                    float edge = Mathf.Min(1f - across, Mathf.Min(along, 1f - along));
                    float mottling = Mathf.PerlinNoise(localX * .15f, localZ * .13f);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge * 13f + (mottling - .5f) * .35f));
                    colors[index] = new Color(1f, 1f, 1f, alpha);
                    uv[index] = new Vector2(localX / 4f, localZ / 4f);
                    if (column > 0 && row > 0)
                        triangles.AddRange(new[] { index - columns - 1, index - 1, index - columns,
                            index - columns, index - 1, index });
                }

            var mesh = new Mesh { name = "Gravel apron and taxi spur", vertices = vertices, colors = colors, uv = uv,
                triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            meshes.Add(mesh);

            const int size = 64;
            gravelTexture = new Texture2D(size, size, TextureFormat.RGBA32, true)
                { name = "Compacted gravel", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float grain = Mathf.PerlinNoise(x * .43f + 23f, y * .47f + 17f);
                    float gray = .36f + .27f * grain;
                    pixels[y * size + x] = new Color(gray * 1.08f, gray * 1.015f, gray * .875f);
                }
            gravelTexture.SetPixels32(pixels);
            gravelTexture.Apply(true, true);

            var material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"))
                { name = "Wind Farm apron gravel", mainTexture = gravelTexture };
            materials.Add(material);
            var apron = new GameObject("Wind Farm gravel taxi spur", typeof(MeshFilter), typeof(MeshRenderer));
            apron.transform.SetParent(transform, false);
            apron.GetComponent<MeshFilter>().sharedMesh = mesh;
            apron.GetComponent<MeshRenderer>().sharedMaterial = material;
            return true;
        }

        private Material Paint(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", .11f);
            materials.Add(material);
            return material;
        }

        internal static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            var collider = piece.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = scale;
            piece.GetComponent<MeshRenderer>().sharedMaterial = material;
            return piece;
        }

        private void OnDestroy()
        {
            if (gravelTexture)
                Destroy(gravelTexture);
            if (asphaltWear)
                Destroy(asphaltWear);
            foreach (var mesh in meshes)
                if (mesh)
                    Destroy(mesh);
            foreach (var material in materials)
                if (material)
                    Destroy(material);
        }
    }
}


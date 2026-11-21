using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MachWorks.BackCountryOperations
{
    internal enum BushFlapKit { None, Utility, HighLift }

    internal sealed class BushUtilityWings : IDisposable
    {
        private readonly Harmony harmony = new Harmony(BackCountryOperationsPlugin.PluginId + ".utilitywings");
        private readonly List<Object> assets = new List<Object>();
        private readonly Dictionary<string, GameObject> variants = new Dictionary<string, GameObject>();
        private GameObject[] wings;
        private GameObject storage, controller, settings;
        private Material wingFinish;
        private ManualLogSource log;
        private static BushUtilityWings current;

        internal static BushUtilityWings Install(ManualLogSource log)
        {
            var feature = new BushUtilityWings { log = log };
            current = feature;
            try
            {
                feature.Patch(typeof(PartPrefabs), "Awake", postfix: nameof(RegisterParts));
                feature.Patch(typeof(PartPrefabs), nameof(PartPrefabs.GetPartPrefab), postfix: nameof(ResolveVariant));
                feature.Patch(typeof(PartBar), "Awake", postfix: nameof(AddCatalogButtons));
                feature.Patch(typeof(ResearchManager), "Awake", prefix: nameof(RegisterResearch));
                feature.Patch(typeof(ResearchManager), nameof(ResearchManager.FindPart), prefix: nameof(ResolveResearch));
                feature.Patch(typeof(ResearchTreeLayoutBuilder), "BuildLayout", prefix: nameof(RegisterLayout));
                feature.Patch(typeof(PartSettingsPanel), nameof(PartSettingsPanel.OpenSettings), postfix: nameof(ConnectSettings));
                feature.Patch(typeof(PlaneContainer), nameof(PlaneContainer.ActivateFlyMode), postfix: nameof(PrepareFlight));
                feature.Patch(typeof(Wing), nameof(Wing.UpdatePart), prefix: nameof(ApplyFlaps), finalizer: nameof(RestoreWing));
                feature.Patch(typeof(BuildingPart), nameof(BuildingPart.Load), postfix: nameof(RestoreLegacyWingColor));
                GroundEffect.Install(feature.harmony);
                return feature;
            }
            catch (Exception error)
            {
                log.LogError($"Could not install bush wings and flaps: {error}");
                feature.Dispose();
                return null;
            }
        }

        private void Patch(Type type, string target, string prefix = null, string postfix = null, string finalizer = null)
        {
            harmony.Patch(AccessTools.DeclaredMethod(type, target),
                prefix: prefix == null ? null : new HarmonyMethod(typeof(BushUtilityWings), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(BushUtilityWings), postfix),
                finalizer: finalizer == null ? null : new HarmonyMethod(typeof(BushUtilityWings), finalizer));
        }

        private static void RegisterParts(PartPrefabs __instance, List<GameObject> ___allPartPrefabs)
        {
            try
            {
                if (current.wings == null)
                    current.Build(___allPartPrefabs);
                if (current.wings == null)
                    return;
                foreach (var wing in current.wings)
                    if (!___allPartPrefabs.Contains(wing))
                        ___allPartPrefabs.Add(wing);
                current.AddResearch(__instance.researchTree);
            }
            catch (Exception error)
            {
                current.log.LogError($"Could not build bush wing prefabs: {error}");
            }
        }

        private static void RegisterResearch(ResearchManager __instance) => current.AddResearch(__instance.researchTree);
        private static void RegisterLayout(ResearchTreeLayoutBuilder __instance) => current.AddResearch(__instance.researchTree);

        private void AddResearch(ResearchTree tree)
        {
            if (!tree || wings == null || tree.items.Any(node => node && node.Prefab == wings[0]))
                return;
            var parent = tree.items.FirstOrDefault(node => node && node.Prefab && node.Prefab.name == "BiplaneWing");
            const float cell = ResearchGrid.Cell;
            int row = ResearchGrid.InsertAfter(tree, parent);
            int column = parent ? ResearchGrid.GridIndex(parent.window.x) + 1 : 0;
            for (int index = 0; index < wings.Length; index++)
            {
                var node = ScriptableObject.CreateInstance<ResearchTreeItem>();
                node.name = wings[index].name;
                node.Prefab = wings[index];
                node.Cost1 = index == 0 ? 5 : 10;
                node.window = new Rect((column + index) * cell, row * cell, cell, cell);
                tree.items.Add(node);
                if (parent)
                    parent.children.Add(node);
                parent = node;
                assets.Add(node);
            }
        }

        private static void AddCatalogButtons(PartBar __instance, GameObject[] ___prefabs, GameObject[] ___fullVersionPrefabs)
        {
            if (current.wings != null &&
                (___prefabs ?? Array.Empty<GameObject>()).Concat(___fullVersionPrefabs ?? Array.Empty<GameObject>())
                .Any(part => part && part.name == "BiplaneWing"))
                AccessTools.DeclaredMethod(typeof(PartBar), "SpawnButtons")
                    .Invoke(__instance, new object[] { current.wings });
        }

        private static void ResolveVariant(string __0, ref GameObject __result)
        {
            if (!__result && !string.IsNullOrEmpty(__0) &&
                current.variants.TryGetValue(BushLandingGear.WithoutClone(__0), out var variant))
                __result = variant;
        }

        private static void ResolveResearch(ref string __0)
        {
            if (!string.IsNullOrEmpty(__0) &&
                current.variants.TryGetValue(BushLandingGear.WithoutClone(__0), out var variant))
                __0 = variant.GetComponent<BushWingFlaps>().PartId;
        }

        private static void ConnectSettings(PartSettingsPanel __instance, PlanePart __0, bool __1)
        {
            if (!__1 && __0.TryGetComponent<BushWingFlaps>(out _) && __instance.content &&
                __instance.content.TryGetComponent<BushFlapPanel>(out var panel))
                panel.Placer = __instance.partPlacer;
        }

        private static void PrepareFlight(PlaneContainer __instance)
        {
            if (current.controller)
                current.controller.GetComponent<BushFlapControls>().Prepare(__instance);
        }

        private static void RestoreLegacyWingColor(BuildingPart __instance)
        {
            // Previously these templates had no paintable surfaces, so their saved paint was unset.
            if (__instance.GetCurrentColor().a == 0f && __instance.TryGetComponent<BushWingFlaps>(out _))
                __instance.SetColor(current.wingFinish.color, true);
        }

        private struct WingState
        {
            internal float Incidence, Lift, Drag;
            internal bool Applied;
        }

        private static void ApplyFlaps(Wing __instance, out WingState __state)
        {
            __state = default;
            if (!__instance.TryGetComponent<BushWingFlaps>(out var flaps) || flaps.Deflection <= 0f)
                return;

            __state = new WingState
            {
                Incidence = __instance.angleOfAttackOffset, Lift = __instance.liftForce,
                Drag = __instance.dragForce, Applied = true
            };
            float amount = flaps.Deflection / 40f;
            __instance.angleOfAttackOffset += 7.5f * amount;
            __instance.liftForce *= 1f + .34f * amount;
            __instance.dragForce *= 1f + 1.15f * amount;
        }

        private static void RestoreWing(Wing __instance, WingState __state)
        {
            if (!__state.Applied)
                return;
            __instance.angleOfAttackOffset = __state.Incidence;
            __instance.liftForce = __state.Lift;
            __instance.dragForce = __state.Drag;
        }

        private void Build(List<GameObject> catalog)
        {
            var biplane = catalog.FirstOrDefault(part => part && part.name == "BiplaneWing");
            var utility = catalog.FirstOrDefault(part => part && part.name == "Wing");
            if (!biplane || !utility)
                throw new InvalidOperationException("Could not find stock wing templates.");

            var material = catalog.Where(part => part && part.name == "SmallRearWheel")
                .SelectMany(part => part.GetComponentsInChildren<MeshRenderer>(true))
                .SelectMany(renderer => renderer.sharedMaterials)
                .FirstOrDefault(entry => entry && entry.name == "PartWhite");
            wingFinish = material ? material :
                MakeMaterial(biplane.GetComponentInChildren<MeshRenderer>(true).sharedMaterial,
                    "Utility wing white", new Color(.92f, .92f, .92f));

            storage = new GameObject("BackCountry Utility Wing Templates");
            storage.SetActive(false);
            Object.DontDestroyOnLoad(storage);
            settings = new GameObject("Wing flap settings", typeof(RectTransform), typeof(BushFlapPanel));
            settings.transform.SetParent(storage.transform, false);
            var wooden = biplane.GetComponent<BuildingPart>();
            var woodenWing = biplane.GetComponentInChildren<Wing>(true);
            if (!TryGetWoodenDimensions(wooden, woodenWing, out float woodenSpan, out float front, out float back))
            {
                woodenSpan = 3.181f;
                front = .676f;
                back = -.689f;
            }
            float chord = front - back;
            wings = new[]
            {
                MakeWing(wooden, woodenWing, true, woodenSpan, chord, front),
                // Approximate the 172S's 7.48 aspect ratio with the wooden center section and tapered outer panels.
                MakeWing(utility.GetComponent<BuildingPart>(), utility.GetComponentInChildren<Wing>(true), false,
                    chord * 2.9f, chord, front)
            };
            foreach (var wing in wings)
                foreach (var kit in new[] { BushFlapKit.Utility, BushFlapKit.HighLift })
                {
                    var variant = Object.Instantiate(wing, storage.transform, false);
                    variant.GetComponent<BushWingFlaps>().SetKit(kit);
                    variants.Add(variant.name, variant);
                }

            controller = new GameObject("BackCountry Flap Controls", typeof(BushFlapControls));
            Object.DontDestroyOnLoad(controller);
            log.LogInfo("Registered Cub utility and 172 utility wings with integrated flap kits.");
        }

        private GameObject MakeWing(BuildingPart stock, Wing stockWing, bool cub, float span, float chord, float leadingEdge)
        {
            string id = cub ? "BCOCubWing" : "BCO172Wing";
            var root = Child(storage.transform, id, Vector3.zero);
            root.gameObject.layer = stock.gameObject.layer;
            var part = root.gameObject.AddComponent<BuildingPart>();
            part.partName = cub ? "Cub Utility Wing" : "172 Utility Wing";
            part.price = cub ? 110f : 155f;
            part.radius = stock.radius;
            part.framing = stock.framing;
            part.snapPoints = Array.Empty<Transform>();
            part.tryOrientUpwards = stock.tryOrientUpwards;
            part.tryMatchParentRotation = stock.tryMatchParentRotation;
            part.ignoreSnappingOnTangentPlane = stock.ignoreSnappingOnTangentPlane;
            part.symetrical = stock.symetrical;
            part.flipSymmetry = stock.flipSymmetry;
            part.forwardOrientation = stock.forwardOrientation;
            var frame = Child(root, "Airfoil frame", Vector3.zero);
            var orientation = stockWing.orientation;
            frame.localRotation = Quaternion.Inverse(stock.transform.rotation) * orientation.rotation;
            var wing = root.gameObject.AddComponent<Wing>();
            wing.area = new Vector2(stockWing.area.x * (cub ? 1.12f : 1.25f), stockWing.area.y);
            wing.angleOfAttackOffset = stockWing.angleOfAttackOffset + (cub ? 1.8f : 1.0f);
            wing.liftForce = stockWing.liftForce * (cub ? 1.16f : 1.05f);
            wing.dragForce = stockWing.dragForce * (cub ? 1.22f : 1.06f);
            wing.maxSpeed = stockWing.maxSpeed;
            wing.weight = stockWing.weight * (cub ? 1.12f : .90f);
            wing.settingsPrefab = settings;
            part.collisionPoint = Child(root, "Root attach point", Vector3.zero);
            if (stock.collisionPoint)
            {
                part.collisionPoint.localPosition = stock.transform.InverseTransformPoint(stock.collisionPoint.position);
                part.collisionPoint.localRotation = Quaternion.Inverse(stock.transform.rotation) * stock.collisionPoint.rotation;
            }
            // The stock part origin is midspan. Build the new wing outward from its actual fuselage mount.
            frame.localPosition = part.collisionPoint.localPosition;

            wing.orientation = Child(frame, "Lift center", new Vector3(span * .40f, 0f, leadingEdge - chord * .35f));

            var stations = cub ? new[] { 0f, .065f, .59f, .70f, .91f, .94f, .97f, 1f } :
                new[] { 0f, .065f, .56f, .59f, .85f, .94f, 1f };
            AddPanel(frame, "Fixed wing panel", cub, span, chord, leadingEdge, stations, 0f, .74f, Vector3.zero);
            AddPanel(frame, "Root trailing edge", cub, span, chord, leadingEdge, new[] { 0f, .065f }, .74f, 1f, Vector3.zero);
            AddPanel(frame, "Tip trailing edge", cub, span, chord, leadingEdge,
                Array.FindAll(stations, station => station >= .94f), .74f, 1f, Vector3.zero);

            var flapPivot = new Vector3(span * .07f, 0f, leadingEdge - chord * .74f);
            var flap = Child(frame, "Inboard flap hinge", flapPivot);
            AddPanel(flap, "Inboard flap panel", cub, span, chord, leadingEdge,
                Array.FindAll(stations, station => station >= .065f && station <= .59f), .74f, 1f, flapPivot);
            var aileronPivot = new Vector3(span * .59f, 0f, leadingEdge - chord * .73f);
            var hinge = Child(frame, "Aileron hinge", aileronPivot);
            var visualPivot = Child(hinge, "Aileron actuator", Vector3.zero);
            hinge.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var surface = Child(visualPivot, "Aileron panel frame", Vector3.zero);
            surface.localRotation = Quaternion.Euler(0f, -90f, 0f);
            AddPanel(surface, "Outboard aileron panel", cub, span, chord, leadingEdge,
                Array.FindAll(stations, station => station >= .59f && station <= .94f), .74f, 1f, aileronPivot);
            var visual = root.gameObject.AddComponent<WingVisual>();
            visual.controlSurface = visualPivot;
            visual.maxAngle = cub ? 19f : 22f;

            var colliderLayer = stock.GetComponentInChildren<Collider>(true);
            var solid = Child(frame, "Wing placement collider", new Vector3(span * .46f, 0f, leadingEdge - .43f * chord));
            if (colliderLayer)
                solid.gameObject.layer = colliderLayer.gameObject.layer;
            solid.gameObject.AddComponent<BoxCollider>().size = new Vector3(span * .94f, chord * .14f, chord * .87f);

            root.gameObject.AddComponent<BushWingFlaps>().Configure(part, flap, id);
            return root.gameObject;
        }

        private static bool TryGetWoodenDimensions(BuildingPart stock, Wing wing, out float span,
            out float front, out float back)
        {
            span = 0f;
            front = float.NegativeInfinity;
            back = float.PositiveInfinity;
            var mount = stock.collisionPoint ? stock.collisionPoint.position : stock.transform.position;
            var stockFrame = Matrix4x4.TRS(mount, wing.orientation.rotation, stock.transform.lossyScale).inverse;
            foreach (var filter in stock.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (!mesh || !mesh.isReadable || !filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled)
                    continue;

                var toFrame = stockFrame * filter.transform.localToWorldMatrix;
                foreach (var vertex in mesh.vertices)
                {
                    var point = toFrame.MultiplyPoint3x4(vertex);
                    span = Mathf.Max(span, Mathf.Abs(point.x));
                    if (Mathf.Abs(point.x) > .20f)
                        continue;
                    front = Mathf.Max(front, point.z);
                    back = Mathf.Min(back, point.z);
                }
            }
            return span > .1f && front - back > .1f;
        }

        private static Vector3 Point(bool cub, float span, float chord, float leadingEdge, float station, float chordFraction, bool upper)
        {
            float outer = Mathf.InverseLerp(cub ? .91f : .56f, 1f, station);
            float taper = cub ? Mathf.Lerp(1f, .48f, outer * outer) : Mathf.Lerp(1f, .64f, outer);
            float leading = leadingEdge - chord * (cub ? .19f : .072f) * outer;
            float x = chordFraction;
            float sectionChord = chord * taper;
            // Square-root thickness rounds the nose. The aft edge closes with the flap and aileron at neutral.
            float thickness = 5f * (cub ? .115f : .105f) * (.2969f * Mathf.Sqrt(x) - .1260f * x +
                x * x * (-.3516f + x * (.2843f - .1036f * x)));
            float camber = (cub ? .023f : .018f) * 4f * x * (1f - x);
            float height = sectionChord * (camber + (upper ? thickness : -thickness));
            return new Vector3(span * station, height + station * (cub ? .048f : .085f), leading - x * sectionChord);
        }

        private void AddPanel(Transform parent, string name, bool cub, float span, float chord, float leadingEdge, float[] stations,
            float first, float last, Vector3 pivot)
        {
            const int divisions = 12;
            int ring = 2 * (divisions + 1);
            var points = new Vector3[ring * stations.Length];
            var faces = new List<int>(ring * stations.Length * 6);
            for (int section = 0; section < stations.Length; section++)
            {
                float station = stations[section];
                for (int index = 0; index <= divisions; index++)
                {
                    float fraction = .5f - .5f * Mathf.Cos(Mathf.PI * index / divisions);
                    points[section * ring + index] =
                        Point(cub, span, chord, leadingEdge, station, Mathf.Lerp(first, last, fraction), true) - pivot;
                    points[section * ring + divisions + 1 + index] =
                        Point(cub, span, chord, leadingEdge, station, Mathf.Lerp(last, first, fraction), false) - pivot;
                }
            }
            for (int section = 0; section < stations.Length - 1; section++)
                for (int index = 0; index < ring; index++)
                {
                    int start = section * ring + index;
                    int next = section * ring + (index + 1) % ring;
                    faces.AddRange(new[] { start, start + ring, next, next, start + ring, next + ring });
                }
            int tip = points.Length - ring;
            for (int index = 1; index < ring - 1; index++)
            {
                faces.AddRange(new[] { 0, index, index + 1, tip, tip + index + 1, tip + index });
            }
            var mesh = new Mesh { name = name, vertices = points, triangles = faces.ToArray() };
            mesh.uv = points.Select(point => new Vector2(point.x / span, point.z / chord)).ToArray();
            mesh.colors32 = Enumerable.Repeat(new Color32(255, 255, 255, 255), points.Length).ToArray();
            mesh.RecalculateNormals();
            if (first == 0f)
            {
                var normals = mesh.normals;
                for (int section = 0; section < stations.Length; section++)
                {
                    int upper = section * ring;
                    int lower = upper + ring - 1;
                    var normal = (normals[upper] + normals[lower]).normalized;
                    normals[upper] = normals[lower] = normal;
                }
                mesh.normals = normals;
            }
            mesh.RecalculateTangents();
            assets.Add(mesh);
            var panel = Child(parent, name, Vector3.zero);
            panel.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            panel.gameObject.AddComponent<MeshRenderer>().sharedMaterial = wingFinish;
            panel.gameObject.AddComponent<PaintableRenderer>();
        }

        private Material MakeMaterial(Material source, string name, Color color)
        {
            var material = new Material(source) { name = name, mainTexture = null, color = color };
            assets.Add(material);
            return material;
        }

        private static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name);
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            return child.transform;
        }

        public void Dispose()
        {
            harmony.UnpatchSelf();
            if (controller)
                Object.Destroy(controller);
            if (storage)
                Object.Destroy(storage);
            foreach (var asset in assets)
                Object.Destroy(asset);
            current = null;
        }
    }

    internal sealed class BushWingFlaps : MonoBehaviour
    {
        [SerializeField] private BuildingPart part;
        [SerializeField] private Transform hinge;
        [SerializeField] private string partId;
        [SerializeField] private BushFlapKit kit;
        private float deflection;

        internal BuildingPart Part => part;
        internal string PartId => partId;
        internal BushFlapKit Kit => kit;
        internal float Deflection => deflection;

        internal void Configure(BuildingPart buildingPart, Transform pivot, string id)
        {
            part = buildingPart;
            hinge = pivot;
            partId = id;
        }

        internal void SetKit(BushFlapKit value)
        {
            kit = value;
            part.gameObject.name = partId + (value == BushFlapKit.Utility ? "_UtilityFlaps" :
                value == BushFlapKit.HighLift ? "_HighLiftFlaps" : "");
        }

        internal void MoveToStage(int stage)
        {
            float target = kit == BushFlapKit.Utility ? stage * 10f :
                kit == BushFlapKit.HighLift ? Mathf.Min(stage * 15f, 40f) : 0f;
            deflection = Mathf.MoveTowards(deflection, target, Time.deltaTime * 45f);
            hinge.localRotation = Quaternion.Euler(-deflection, 0f, 0f);
        }

        internal void ResetStage()
        {
            deflection = 0f;
            hinge.localRotation = Quaternion.identity;
        }
    }

    internal sealed class BushFlapControls : MonoBehaviour
    {
        private static BushFlapControls activeControls;
        private BushWingFlaps[] wings = Array.Empty<BushWingFlaps>();
        private PlaneContainer plane;
        private int stage;

        internal void Prepare(PlaneContainer plane)
        {
            this.plane = plane;
            activeControls = this;
            wings = plane.GetComponentsInChildren<BushWingFlaps>();
            foreach (var wing in wings)
                wing.ResetStage();
            stage = 0;
        }

        internal static bool HasFlapsFor(PlaneContainer plane) =>
            activeControls && activeControls.plane == plane && activeControls.HasFittedFlaps();

        private void LateUpdate()
        {
            if (!UnityEngine.SceneManagement.SceneManager.GetSceneByName("Flying").isLoaded)
            {
                if (stage != 0)
                    foreach (var wing in wings)
                        if (wing)
                            wing.ResetStage();
                stage = 0;
                return;
            }
            if (!HasFittedFlaps())
                return;

            if (IntegratedFlapMod.HasSharedControls)
            {
                stage = IntegratedFlapMod.Stage;
            }
            else
            {
                var keyboard = Keyboard.current;
                var input = InputManager.Instance;
                if (keyboard != null && input && !input.currentInputBlocker)
                {
                    if (keyboard.rightBracketKey.wasPressedThisFrame)
                        stage = Mathf.Min(3, stage + 1);
                    if (keyboard.leftBracketKey.wasPressedThisFrame)
                        stage = Mathf.Max(0, stage - 1);
                }
            }
            foreach (var wing in wings)
                if (wing)
                    wing.MoveToStage(stage);
        }

        private bool HasFittedFlaps() => wings.Any(wing => wing && wing.Kit != BushFlapKit.None);

        private void OnGUI()
        {
            if (!IntegratedFlapMod.HasSharedControls && HasFittedFlaps() &&
                UnityEngine.SceneManagement.SceneManager.GetSceneByName("Flying").isLoaded)
                GUI.Label(new Rect(12f, Screen.height - 38f, 250f, 24f), $"Flaps {stage}/3   [ retract · ] extend");
        }

        private void OnDestroy()
        {
            if (activeControls == this)
                activeControls = null;
        }
    }

    internal sealed class BushFlapPanel : PartSettingsBase
    {
        private BushWingFlaps flap;
        private Toggle utilityFlaps, highLiftFlaps;
        private TMP_Text guidance;
        internal PartPlacer Placer { get; set; }

        public override void SelectPart(PlanePart part)
        {
            flap = part.GetComponent<BushWingFlaps>();
            utilityFlaps = WorkshopSettingsUi.Checkbox(transform, "Utility flaps", 3f,
                enabled => SetKit(enabled ? BushFlapKit.Utility : BushFlapKit.None));
            highLiftFlaps = WorkshopSettingsUi.Checkbox(transform, "High-lift flaps", 44f,
                enabled => SetKit(enabled ? BushFlapKit.HighLift : BushFlapKit.None));
            guidance = WorkshopSettingsUi.Label(transform, "", 84f, 46f, detail: true);
            Refresh();
        }

        private void SetKit(BushFlapKit kit)
        {
            if (kit == flap.Kit || !CampaignProgress.CanFit(kit))
                return;
            var match = Placer ? Placer.GetMirrorPart(flap.Part) : null;
            var other = match ? match.GetComponent<BushWingFlaps>() : null;
            flap.SetKit(kit);
            if (other && other.PartId == flap.PartId)
                other.SetKit(kit);
            if (PlaneStorage.Instance)
                PlaneStorage.Instance.UpdateHistory();
            Refresh();
        }

        private void Refresh()
        {
            bool utility = CampaignProgress.CanFit(BushFlapKit.Utility);
            bool highLift = CampaignProgress.CanFit(BushFlapKit.HighLift);
            WorkshopSettingsUi.SetCheckbox(utilityFlaps, flap.Kit == BushFlapKit.Utility, utility || flap.Kit == BushFlapKit.Utility);
            WorkshopSettingsUi.SetCheckbox(highLiftFlaps, flap.Kit == BushFlapKit.HighLift, highLift || flap.Kit == BushFlapKit.HighLift);
            guidance.text = !utility ? "Valley Supply Trial unlocks Utility flaps." :
                !highLift ? "Loaded Short-Field Run unlocks High-lift flaps." : "";
            ((RectTransform)transform).sizeDelta = new Vector2(WorkshopSettingsUi.Width, highLift ? 85f : 130f);
        }
    }
}


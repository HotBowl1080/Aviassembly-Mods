using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MachWorks.BackCountryOperations
{
    internal sealed class BushLandingGear : IDisposable
    {
        internal const float Travel = .28f;
        internal const float TailwheelTravel = .10f;
        internal const string LargeSuffix = "_Balloon";
        internal const string SteeringSuffix = "_Steerable";
        internal const string FixedSuffix = "_Fixed";
        private const string CoveredStrutId = "BCOYellowStrut";
        private const string ShockStrutId = "BCOShockStrut";
        private const string TailwheelId = "BCOTailwheel";
        private const int Segments = 40;
        private static readonly Vector3 Axle = new Vector3(.82f, -.82f, .05f);
        private readonly Harmony harmony = new Harmony(BackCountryOperationsPlugin.PluginId + ".bushgear");
        private readonly List<Object> assets = new List<Object>();
        private readonly Dictionary<string, GameObject> variantTemplates = new Dictionary<string, GameObject>();
        private static BushLandingGear current;
        private Transform storage;
        private GameObject[] gearParts, legacyParts;
        private GameObject settings;
        private Mesh tube, smallCasing, largeCasing, bumpMesh, balloonBumpMesh;
        private Material painted, steel, chrome, rubber;
        private PhysicsMaterial tailwheelStop;
        private ManualLogSource log;

        public static BushLandingGear Install(ManualLogSource log)
        {
            var gear = new BushLandingGear { log = log };
            current = gear;
            try
            {
                gear.Patch(typeof(PartPrefabs), "Awake", nameof(RegisterParts));
                gear.Patch(typeof(PartBar), "Awake", nameof(AddCatalogButtons));
                gear.Patch(typeof(PartPrefabs), nameof(PartPrefabs.GetPartPrefab), nameof(ResolveVariantTemplate));
                gear.Patch(typeof(ResearchManager), nameof(ResearchManager.FindPart), nameof(ResolveResearchPart), prefix: true);
                gear.Patch(typeof(ResearchManager), "Awake", nameof(RegisterResearchManager), prefix: true);
                gear.Patch(typeof(ResearchTreeLayoutBuilder), "BuildLayout", nameof(RegisterResearchLayout), prefix: true);
                gear.Patch(typeof(Wheel), nameof(Wheel.Activate), nameof(PrepareWheelForFlight));
                gear.Patch(typeof(Wheel), nameof(Wheel.Deactivate), nameof(ResetWheelForEditor));
                gear.harmony.Patch(AccessTools.DeclaredMethod(typeof(Wheel), "FixedUpdate"),
                    prefix: new HarmonyMethod(typeof(BushLandingGear), nameof(SteerWheel)),
                    transpiler: new HarmonyMethod(typeof(BushLandingGear), nameof(WheelContact)));
                gear.Patch(typeof(WheelVisual), nameof(WheelVisual.UpdateWheelVisual), nameof(RollWheel));
                gear.harmony.Patch(AccessTools.DeclaredMethod(typeof(PlaneController), "UpdateControlls"),
                    postfix: new HarmonyMethod(typeof(BushGroundHandling), nameof(BushGroundHandling.AssistTailLift)));
                gear.Patch(typeof(PartSettingsPanel), nameof(PartSettingsPanel.OpenSettings), nameof(ConnectSettings));
                gear.harmony.Patch(AccessTools.DeclaredMethod(typeof(PartPlacer), "UpdateSelection"),
                    prefix: new HarmonyMethod(typeof(BushGearLengthHandle), nameof(BushGearLengthHandle.BeforeSelection)));
                gear.Patch(typeof(PartPlacer), "Update", nameof(UpdateLengthHandle));
                gear.harmony.Patch(AccessTools.DeclaredMethod(typeof(PartPlacer), "MovePart", new[] { typeof(BuildingPart) }),
                    postfix: new HarmonyMethod(typeof(BushLandingGear), nameof(AlignPlacedGear)));
                gear.Patch(typeof(PartPlacer), "MoveMirrorPart", nameof(AlignMirroredGear));
                gear.Patch(typeof(PartPlacer), "CreateMirrorPart", nameof(AllowGearMirror), prefix: true);
                gear.Patch(typeof(PartPlacer), "GetMirrorPart", nameof(FindPlacedMirror), prefix: true);
                gear.Patch(typeof(PartPlacer), "PlaceSelectedPart", nameof(AlignMirrorBeforePlacement), prefix: true);
                gear.harmony.Patch(AccessTools.DeclaredMethod(typeof(PartPlacer), "PlacePart"),
                    prefix: new HarmonyMethod(typeof(BushLandingGear), nameof(AlignCommittedGear)));
                gear.Patch(typeof(PartExploder), "GetRootObject", nameof(ResolveExplosionRoot));
                return gear;
            }
            catch (Exception error)
            {
                gear.Dispose();
                log.LogError($"Could not install bush landing gear: {error}");
                return null;
            }
        }

        private void Patch(Type type, string target, string method, bool prefix = false)
        {
            var patch = new HarmonyMethod(typeof(BushLandingGear), method);
            harmony.Patch(AccessTools.DeclaredMethod(type, target), prefix: prefix ? patch : null,
                postfix: prefix ? null : patch);
        }

        private static void RegisterParts(PartPrefabs __instance, List<GameObject> ___allPartPrefabs)
        {
            try
            {
                if (current.gearParts == null)
                    current.Build(___allPartPrefabs);

                if (current.gearParts == null)
                    return;

                foreach (var part in current.gearParts.Concat(current.legacyParts))
                    if (!___allPartPrefabs.Contains(part))
                        ___allPartPrefabs.Add(part);

                current.AddResearch(__instance.researchTree);
            }
            catch (Exception error)
            {
                current.log.LogError($"Could not build bush landing gear: {error}");
            }
        }

        private static void RegisterResearchManager(ResearchManager __instance) => current.AddResearch(__instance.researchTree);
        private static void RegisterResearchLayout(ResearchTreeLayoutBuilder __instance) => current.AddResearch(__instance.researchTree);

        private void AddResearch(ResearchTree tree)
        {
            if (!tree || gearParts == null || tree.items.Any(item => item && item.Prefab == gearParts[0]))
                return;

            const float cell = ResearchGrid.Cell;
            var stockBike = FindResearchNode(tree, "BikeWheel");
            var mainParent = FindResearchNode(tree, "OldSteelWheel") ?? stockBike;
            var tailParent = FindResearchNode(tree, "SmallRearWheel") ?? stockBike;
            int row = ResearchGrid.InsertAfter(tree, mainParent);
            int mainColumn = mainParent ? ResearchGrid.GridIndex(mainParent.window.x) + 1 : 1;
            int tailColumn = tailParent ? ResearchGrid.GridIndex(tailParent.window.x) : 0;
            int tailRow = tailParent ? ResearchGrid.GridIndex(tailParent.window.y) + 1 : row;
            while (tree.items.Any(item => item && ResearchGrid.GridIndex(item.window.x) == tailColumn &&
                ResearchGrid.GridIndex(item.window.y) == tailRow))
                tailRow++;

            var yellowNode = AddResearchNode(tree, gearParts[0], mainParent, 5,
                new Rect(mainColumn * cell, row * cell, cell, cell));
            AddResearchNode(tree, gearParts[1], yellowNode, 10,
                new Rect((mainColumn + 1) * cell, row * cell, cell, cell));
            AddResearchNode(tree, gearParts[2], tailParent, 5,
                new Rect(tailColumn * cell, tailRow * cell, cell, cell));
        }

        private static ResearchTreeItem FindResearchNode(ResearchTree tree, string id) =>
            tree.items.FirstOrDefault(item => item && item.Prefab && item.Prefab.name == id);

        private ResearchTreeItem AddResearchNode(ResearchTree tree, GameObject part, ResearchTreeItem parent,
            int cost, Rect window)
        {
            var node = ScriptableObject.CreateInstance<ResearchTreeItem>();
            node.name = part.name;
            node.Prefab = part;
            node.Cost1 = cost;
            node.window = window;
            tree.items.Add(node);
            if (parent)
                parent.children.Add(node);
            assets.Add(node);
            return node;
        }

        private static void AddCatalogButtons(PartBar __instance, GameObject[] ___prefabs,
            GameObject[] ___fullVersionPrefabs)
        {
            if (current.gearParts != null &&
                (___prefabs ?? Array.Empty<GameObject>()).Concat(___fullVersionPrefabs ?? Array.Empty<GameObject>())
                .Any(part => part && part.name == "BikeWheel"))
                AccessTools.DeclaredMethod(typeof(PartBar), "SpawnButtons")
                    .Invoke(__instance, new object[] { current.gearParts });
        }

        private static void ResolveVariantTemplate(string __0, ref GameObject __result)
        {
            if (__result || string.IsNullOrEmpty(__0))
                return;
            var id = FixedGearId(__0);
            if (current.TryGearTemplate(id, out var template))
                __result = template;
            else if (current.TryLengthTemplate(id, out template, out var percent))
            {
                var clone = Object.Instantiate(template, current.storage, false);
                clone.GetComponentInChildren<BushWheelPart>(true).SetLengthPercent(percent);
                current.variantTemplates.Add(id, clone);
                __result = clone;
            }
        }

        private static void ResolveResearchPart(ref string __0)
        {
            if (string.IsNullOrEmpty(__0))
                return;
            var id = FixedGearId(__0);
            if (current.TryGearTemplate(id, out var template) ||
                current.TryLengthTemplate(id, out template, out _))
                __0 = template.GetComponentInChildren<BushWheelPart>(true).PartId;
        }

        private bool TryLengthTemplate(string id, out GameObject template, out int percent)
        {
            template = null;
            percent = 0;
            int suffix = id.LastIndexOf("_Length", StringComparison.Ordinal);
            if (suffix < 0 || !int.TryParse(id.Substring(suffix + 7), out percent) || percent < 65 || percent > 112)
                return false;
            string baseId = id.Substring(0, suffix);
            return TryGearTemplate(baseId, out template) &&
                template.GetComponentInChildren<BushWheelPart>(true).SupportsLength;
        }

        private bool TryGearTemplate(string id, out GameObject template)
        {
            if (variantTemplates.TryGetValue(id, out template))
                return true;
            template = gearParts?.FirstOrDefault(part => part.name == id);
            return template;
        }

        private static string FixedGearId(string name)
        {
            const string legacySuffix = "_Retract";
            string id = WithoutClone(name);
            if (!id.StartsWith(CoveredStrutId, StringComparison.Ordinal) &&
                !id.StartsWith(ShockStrutId, StringComparison.Ordinal))
                return id;
            int lengthIndex = id.LastIndexOf("_Length", StringComparison.Ordinal);
            int suffixEnd = lengthIndex < 0 ? id.Length : lengthIndex;
            int suffixStart = suffixEnd - legacySuffix.Length;
            return suffixStart >= 0 && string.CompareOrdinal(id, suffixStart, legacySuffix, 0, legacySuffix.Length) == 0
                ? id.Remove(suffixStart, legacySuffix.Length) : id;
        }

        private static void UpdateLengthHandle(PartPlacer __instance) => BushGearLengthHandle.UpdateFor(__instance);

        internal static string WithoutClone(string name) => name.Replace("(Clone)", "");

        private static void ConnectSettings(PartSettingsPanel __instance, PlanePart __0, bool __1)
        {
            if (!__1 && __0.TryGetComponent<BushWheelPart>(out _) && __instance.content &&
                __instance.content.TryGetComponent<BushTireSettings>(out var panel))
            {
                panel.Placer = __instance.partPlacer;
                panel.RefreshMirrorSettings();
            }
        }

        private static BushWheelPart CustomGear(BuildingPart part)
        {
            var gear = part ? part.GetComponentInChildren<BushWheelPart>(true) : null;
            return gear && !string.IsNullOrEmpty(gear.PartId) ? gear : null;
        }

        private static bool AllowGearMirror(BuildingPart __0)
        {
            var gear = CustomGear(__0);
            return !gear || !gear.IsTailwheel;
        }

        private static bool FindPlacedMirror(PartPlacer __instance, BuildingPart __0, ref BuildingPart __result)
        {
            var gear = CustomGear(__0);
            if (!gear)
                return true;
            var match = BushTireSettings.FindMatchingWheel(__instance, gear);
            __result = match ? match.Part : null;
            return false;
        }

        private static void AlignPlacedGear(PartPlacer __instance, BuildingPart __0)
        {
            AlignGearToAirframe(__instance, __0);
            if (__0 && __0 == __instance.currentMovingPart && __instance.currentMirrorPart)
                MatchMirrorPose(__instance, __instance.currentMirrorPart, __0);
        }

        private static void AlignCommittedGear(PartPlacer __instance, BuildingPart __0)
        {
            if (__0 && __0.parent && !__0.hasBeenPlaced)
                AlignGearToAirframe(__instance, __0);
        }

        private static void AlignMirrorBeforePlacement(PartPlacer __instance)
        {
            MatchMirrorPose(__instance, __instance.currentMirrorPart, __instance.currentMovingPart);
        }

        private static void AlignMirroredGear(PartPlacer __instance, BuildingPart __0, BuildingPart __1,
            ref GameObject __result)
        {
            if (!MatchMirrorPose(__instance, __0, __1))
                return;

            var surfaced = __result ? __result.GetComponentInParent<BuildingPart>() : null;
            var source = __1.parent;
            if (source)
            {
                var mirroredParent = __instance.GetMirrorPart(source);
                if (mirroredParent && mirroredParent != source && !CustomGear(mirroredParent))
                    __result = mirroredParent.gameObject;
                else if (source == __instance.partContainer.GetBasePart() || surfaced == source)
                    __result = source.gameObject;
            }
            if (CustomGear(__result ? __result.GetComponentInParent<BuildingPart>() : null))
                __result = null;
        }

        private static bool MatchMirrorPose(PartPlacer placer, BuildingPart mirror, BuildingPart original)
        {
            var gear = CustomGear(original);
            var symmetry = placer.symmetryPlane;
            if (!gear || gear.IsTailwheel || !mirror || symmetry == null)
                return false;

            var scale = symmetry.GetMirroredScale(original.transform.localScale);
            var rotation = symmetry.GetMirroredRotation(original.transform);
            if (mirror.transform.localScale != scale)
                mirror.SetScale(scale);
            var airframe = placer.partContainer?.GetBasePart();
            if (airframe)
            {
                mirror.baseRotation = UprightGearRotation(airframe, scale.y < 0f);
                mirror.rotationOffset = Quaternion.Inverse(mirror.baseRotation) * rotation;
            }
            mirror.SetPositionAndRotation(symmetry.GetMirroredPosition(original.transform.position), rotation);
            return true;
        }

        private static void AlignGearToAirframe(PartPlacer placer, BuildingPart part)
        {
            var gear = CustomGear(part);
            var airframe = placer.partContainer?.GetBasePart();
            if (!gear || !airframe)
                return;

            var position = part.transform.position;
            var symmetry = placer.symmetryPlane;
            var side = symmetry != null ? (position - symmetry.GetMirroredPosition(position)) * .5f :
                position - airframe.transform.position;
            float offset = Vector3.Dot(side, airframe.transform.right);
            var scale = part.transform.localScale;
            bool left = !gear.IsTailwheel && (offset < -.025f || (Mathf.Abs(offset) <= .025f && scale.y < 0f));
            scale.y = Mathf.Abs(scale.y) * (left ? -1f : 1f);
            if (part.transform.localScale != scale)
                part.SetScale(scale);
            part.baseRotation = UprightGearRotation(airframe, left);
            part.SetRotation(part.baseRotation * part.rotationOffset.normalized);
        }

        private static Quaternion UprightGearRotation(BuildingPart airframe, bool left) =>
            Quaternion.LookRotation(airframe.transform.forward, left ? -airframe.transform.up : airframe.transform.up);

        private static void PrepareWheelForFlight(Wheel __instance)
        {
            if (__instance.TryGetComponent<BushWheelPart>(out var gear))
                gear.PrepareForFlight();
        }

        private static void ResetWheelForEditor(Wheel __instance)
        {
            if (__instance.TryGetComponent<BushWheelPart>(out var gear))
                gear.ResetForEditor();
        }

        private static void SteerWheel(Wheel __instance)
        {
            __instance.TryGetComponent<BushWheelPart>(out var gear);
            if (gear)
                gear.UpdateGroundHandling();
            if (BushGroundHandling.Braking && !BushGroundHandling.IsTailwheel(__instance, gear))
                BushGroundHandling.BrakeWheel(__instance, gear);
        }

        private static IEnumerable<CodeInstruction> WheelContact(IEnumerable<CodeInstruction> method)
        {
            var raycast = AccessTools.Method(typeof(Physics), nameof(Physics.Raycast), new[]
            {
                typeof(Vector3), typeof(Vector3), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int)
            });
            var contact = AccessTools.Method(typeof(BushLandingGear), nameof(CastWheelContact));
            foreach (var instruction in method)
            {
                if (instruction.Calls(raycast))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    instruction.operand = contact;
                }
                yield return instruction;
            }
        }

        private static bool CastWheelContact(Vector3 origin, Vector3 direction, out RaycastHit hit, float range,
            int layers, Wheel wheel) => wheel.TryGetComponent<BushWheelPart>(out var gear) && gear.Large
                ? gear.CastContact(origin, direction, out hit, range, layers)
                : Physics.Raycast(origin, direction, out hit, range, layers);

        private static void ResolveExplosionRoot(PlanePart __0, ref GameObject __result)
        {
            if (__0.TryGetComponent<BushWheelPart>(out var wheel))
                __result = wheel.Part.gameObject;
        }

        private static void RollWheel(WheelVisual __instance, Vector3 __1, Vector3 __2)
        {
            if (__instance.TryGetComponent<BushWheelPart>(out var gear))
                gear.Roll(Vector3.Dot(__1, __2));
        }

        private void Build(List<GameObject> catalog)
        {
            var stock = catalog.FirstOrDefault(part => part && part.name == "OldSteelWheel")
                ?? catalog.FirstOrDefault(part => part && part.name == "BikeWheel");
            var wheel = stock ? stock.GetComponentInChildren<Wheel>(true) : null;
            if (!wheel || !wheel.partCollider)
                throw new InvalidOperationException("Could not find the stock fixed-wheel physics template.");
            var rearStock = catalog.FirstOrDefault(part => part && part.name == "SmallRearWheel");
            var rearWheel = rearStock ? rearStock.GetComponentInChildren<Wheel>(true) : null;
            var rearBuildingPart = rearStock ? rearStock.GetComponent<BuildingPart>() : null;

            var holder = new GameObject("BackCountry Landing Gear Templates");
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);
            storage = holder.transform;

            painted = new[] { rearStock, stock }.Concat(catalog).Where(part => part)
                .SelectMany(part => part.GetComponentsInChildren<MeshRenderer>(true))
                .SelectMany(renderer => renderer.sharedMaterials)
                .First(material => material && material.name == "PartWhite");
            steel = MakeMaterial(painted, "Graphite steel", new Color(.28f, .30f, .32f), .23f);
            chrome = MakeMaterial(painted, "Piston and rim alloy", new Color(.73f, .76f, .79f), .38f);
            rubber = MakeMaterial(painted, "Tire rubber", new Color(.20f, .21f, .22f), .08f);
            tailwheelStop = new PhysicsMaterial("Tailwheel bump stop")
            {
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                staticFriction = .12f,
                dynamicFriction = .09f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            assets.Add(tailwheelStop);
            tube = Cylinder("Gear tube", Segments);
            bumpMesh = Cylinder("Wheel bottom-out collision", 16);
            balloonBumpMesh = Lathe("Balloon tire bottom-out collision", 20,
                new Vector2(-.5f, 0f), new Vector2(-.5f, .38f), new Vector2(-.38f, .48f),
                new Vector2(-.25f, .5f), new Vector2(.25f, .5f), new Vector2(.38f, .48f),
                new Vector2(.5f, .38f), new Vector2(.5f, 0f));
            smallCasing = TireMesh(false);
            largeCasing = TireMesh(true);
            settings = BushTireSettings.CreateTemplate(storage);

            var framing = stock.GetComponent<BuildingPart>().framing;
            var tailFraming = rearBuildingPart ? rearBuildingPart.framing : framing;
            if (!rearWheel || !rearWheel.partCollider)
                rearWheel = wheel;
            legacyParts = new[]
            {
                MakeLegacyTire(false, wheel, framing),
                MakeLegacyTire(true, wheel, framing)
            };
            gearParts = new[]
            {
                MakeStrut(false, false, wheel, framing),
                MakeStrut(true, false, wheel, framing),
                MakeTailwheel(rearWheel, tailFraming, true)
            };
            foreach (var part in gearParts.Take(2))
                foreach (BushGearLength length in Enum.GetValues(typeof(BushGearLength)))
                    foreach (bool steerable in new[] { false, true })
                    {
                        if (length != BushGearLength.Standard || steerable)
                            RegisterVariant(part, length, false, steerable);
                        RegisterVariant(part, length, true, steerable);
                    }
            RegisterVariant(gearParts[2], BushGearLength.Standard, false, false);

            log.LogInfo("Registered covered gear, exposed-shock gear, and a tailwheel with steering settings.");
        }

        private void RegisterVariant(GameObject part, BushGearLength length, bool large, bool steerable)
        {
            var variant = Object.Instantiate(part, storage, false);
            var wheel = variant.GetComponentInChildren<BushWheelPart>(true);
            wheel.SetLength(length);
            wheel.SetLarge(large);
            wheel.SetSteerable(steerable);
            variantTemplates.Add(variant.name, variant);
        }

        private BuildingPart NewPart(string id, string label, int layer, float price, float radius, ItemFraming framing)
        {
            var root = Child(storage, id, Vector3.zero);
            root.gameObject.layer = layer;
            var part = root.gameObject.AddComponent<BuildingPart>();
            part.partName = label;
            part.price = price;
            part.radius = radius;
            part.framing = framing;
            part.forwardOrientation = Vector3.forward;
            part.symetrical = true;
            part.snapPoints = new[] { Child(root, "Airframe mount", Vector3.zero) };
            part.collisionPoint = part.snapPoints[0];
            part.collisionPoint.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            return part;
        }

        private GameObject MakeStrut(bool shocks, bool large, Wheel stock, ItemFraming framing, bool steerable = false)
        {
            string id = shocks ? ShockStrutId : CoveredStrutId;
            var part = NewPart(id + (large ? LargeSuffix : ""),
                shocks ? "Bush Gear Strut with Shocks" : "Covered Bush Gear Strut",
                stock.partCollider.layer, shocks ? 90f : 65f, 1.65f, framing);
            var mount = Child(part.transform, "Airframe mounting collider", new Vector3(0f, -.018f, 0f));
            mount.gameObject.AddComponent<BoxCollider>().size = new Vector3(.16f, .11f, .36f);
            var root = Child(part.transform, "Adjustable strut", Vector3.zero);
            // Preserve the original axle location for aircraft with a legacy separate tire.
            var axle = Axle;
            var knuckle = axle - Vector3.right * .065f;
            var front = new Vector3(.045f, -.035f, .47f);
            var rear = new Vector3(-.035f, -.045f, -.39f);
            var brace = shocks ? new Vector3(-.42f, -.31f, .27f) : new Vector3(-.31f, -.29f, .05f);
            if (!shocks)
                Render(root, "Triangular fairing", Fairing(front, rear, knuckle), painted);

            var mainLeg = Rod(root, "Forward gear leg", front, knuckle, shocks ? .071f : .095f, painted, true);
            var trailingLeg = Rod(root, "Trailing gear leg", rear, knuckle, shocks ? .054f : .08f, painted, true);
            if (shocks)
            {
                Rod(root, "Inboard forward brace", front, brace, .042f, painted, true);
                Rod(root, "Inboard rear brace", rear, brace, .034f, painted);
                Rod(root, "Inboard shock clevis", brace - Vector3.forward * .05f,
                    brace + Vector3.forward * .05f, .088f, steel);
            }
            else
            {
                Rod(root, "Upper mounting rail", front, rear, .105f, painted, true);
                Rod(root, "Inboard brace", Vector3.down * .05f, brace, .075f, painted);
            }

            Rod(root, "Forward airframe lug", front + Vector3.up * .055f, front, .12f, painted);
            Rod(root, "Rear airframe lug", rear + Vector3.up * .06f, rear, .10f, painted);
            var pin = Rod(root, "Axle spindle", knuckle - Vector3.right * .04f, axle + Vector3.right * .08f,
                .09f, chrome, true);
            var joint = Vector3.Lerp(brace, knuckle, .50f);
            var body = Rod(root, shocks ? "Inboard damper cylinder" : "Covered bungee housing",
                brace, joint, shocks ? .121f : .22f, shocks ? steel : painted);
            var shaft = Rod(root, "Exposed piston rod", joint, knuckle, shocks ? .042f : .08f, chrome);
            var tire = AddWheel(part, stock, axle, shocks, large, id, steerable: steerable);
            tire.ConfigureStrut(root);
            root.gameObject.AddComponent<BushShockStrut>().Configure(tire, brace, knuckle, front,
                rear, body, shaft, mainLeg, trailingLeg, pin);
            return part.gameObject;
        }

        private GameObject MakeTailwheel(Wheel stock, ItemFraming framing, bool steerable)
        {
            var part = NewPart(TailwheelId, "Bush Tailwheel", stock.partCollider.layer, 30f, .68f, framing);
            var root = part.transform;
            var mount = Child(root, "Tailwheel mounting collider", new Vector3(0f, -.016f, 0f));
            mount.gameObject.AddComponent<BoxCollider>().size = new Vector3(.12f, .075f, .16f);

            var axle = new Vector3(0f, -.32f, -.31f);
            var pivot = axle + Vector3.up * .185f;
            Rod(root, "Airframe attachment plate", Vector3.up * .01f, Vector3.down * .044f, .12f, painted);
            Rod(root, "Spring leaf trailing arm", new Vector3(0f, -.04f, .022f), pivot, .064f, steel, true);
            Rod(root, "Caster pivot", pivot + Vector3.up * .035f, pivot - Vector3.up * .026f, .087f, chrome);
            AddWheel(part, stock, axle, false, false, TailwheelId, tailwheel: true, steerable: steerable);
            return root.gameObject;
        }

        private GameObject MakeLegacyTire(bool large, Wheel stock, ItemFraming framing)
        {
            var part = NewPart(large ? "BCOLargeBushTire" : "BCOSmallBushTire",
                large ? "Large Bush Tire" : "Small Ribbed Tire",
                stock.partCollider.layer, large ? 40f : 25f, 1f, framing);
            AddWheel(part, stock, Vector3.zero, false, large, null);
            return part.gameObject;
        }

        private BushWheelPart AddWheel(BuildingPart part, Wheel stock, Vector3 axle, bool shocks, bool large, string id,
            bool tailwheel = false, bool steerable = false)
        {
            var root = part.transform;
            float travel = tailwheel ? TailwheelTravel : Travel;
            var suspension = Child(root, "Suspension anchor", axle);
            var slider = Child(suspension, "Sliding axle", Vector3.down * travel / 2f);
            var caster = tailwheel ? Child(slider, "Caster swivel", Vector3.zero) : null;
            var spin = Child(caster ? caster : slider, "Wheel axle rotation", Vector3.zero);
            spin.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var casing = Render(spin, "Tire casing", smallCasing, rubber).GetComponent<MeshFilter>();
            var hub = Render(spin, "Alloy rim", tube, chrome);
            var cap = Render(spin, "Outboard hub cap", tube, steel);
            var collar = Render(spin, "Inboard hub", tube, steel);
            if (tailwheel)
            {
                var forkCrown = new Vector3(0f, .174f, .015f);
                var leftShoulder = forkCrown + Vector3.left * .09f;
                var rightShoulder = forkCrown + Vector3.right * .09f;
                Rod(caster, "Caster spindle", forkCrown, forkCrown + Vector3.up * .075f, .044f, chrome);
                Rod(caster, "Fork crown", leftShoulder, rightShoulder, .036f, painted);
                Rod(caster, "Left fork", leftShoulder, Vector3.left * .087f, .025f, painted);
                Rod(caster, "Right fork", rightShoulder, Vector3.right * .087f, .025f, painted);
                Rod(caster, "Fork axle pin", Vector3.left * .107f, Vector3.right * .107f, .03f, chrome);
            }

            var visual = suspension.gameObject.AddComponent<WheelVisual>();
            visual.lowerSuspension = slider;
            visual.upperSuspensionLength = travel;
            // Spin manually with RotateSelf so mirrored scale doesn't change the rolling direction.
            visual.wheel = null;

            var picker = Child(suspension, "Workshop tire collision", Vector3.zero).gameObject;
            var selection = picker.AddComponent<BoxCollider>();
            var bump = Child(suspension, "Full-compression tire collision", Vector3.zero);
            bump.localRotation = Quaternion.Euler(0f, 90f, 0f);
            int stopLayer = stock.stopCollider ? stock.stopCollider.layer : 2;
            if ((stock.layerMask.value & (1 << stopLayer)) != 0)
                stopLayer = 2;
            bump.gameObject.layer = stopLayer;
            var hardStop = bump.gameObject.AddComponent<MeshCollider>();
            hardStop.sharedMesh = bumpMesh;
            hardStop.convex = true;
            if (tailwheel)
                hardStop.sharedMaterial = tailwheelStop;
            else if (stock.stopCollider && stock.stopCollider.TryGetComponent<Collider>(out var stockStop))
                hardStop.sharedMaterial = stockStop.sharedMaterial;
            bump.gameObject.SetActive(false);

            var wheel = suspension.gameObject.AddComponent<Wheel>();
            wheel.enabled = false;
            wheel.layerMask = stock.layerMask.value & ~(1 << stopLayer);
            wheel.rayUpOffset = Mathf.Max(stock.rayUpOffset, .08f);
            wheel.suspensionDamping = stock.suspensionDamping * (shocks ? 1.25f : 1f);
            wheel.traction = stock.traction;
            wheel.maxFriction = stock.maxFriction;
            wheel.maxWeightCapacity = stock.maxWeightCapacity * (shocks ? 1.25f : 1f);
            wheel.maxImpactVelocity = stock.maxImpactVelocity * (shocks ? 1.18f : 1f);
            wheel.partCollider = picker;
            wheel.stopCollider = bump.gameObject;
            wheel.settingsPrefab = id == null ? null : settings;

            var gear = suspension.gameObject.AddComponent<BushWheelPart>();
            gear.Configure(part, wheel, visual, axle, id, spin, casing, hub, cap, collar, selection, bump,
                smallCasing, largeCasing, hardStop, bumpMesh, balloonBumpMesh, stock, shocks, caster);
            gear.SetLarge(large);
            gear.SetSteerable(steerable);
            return gear;
        }

        private Mesh TireMesh(bool large)
        {
            var profile = new List<Vector2>
            {
                new Vector2(-.79f, large ? .35f : .44f),
                new Vector2(-.97f, .64f),
                new Vector2(-1f, .78f),
                new Vector2(-.88f, .91f),
                new Vector2(-.62f, .98f)
            };
            if (large)
            {
                profile.Add(new Vector2(0f, 1f));
            }
            else
            {
                foreach (float groove in new[] { -.34f, 0f, .34f })
                {
                    profile.Add(new Vector2(groove - .08f, 1f));
                    profile.Add(new Vector2(groove - .034f, .97f));
                    profile.Add(new Vector2(groove + .034f, .97f));
                    profile.Add(new Vector2(groove + .08f, 1f));
                }
            }
            profile.AddRange(profile.Take(5).Reverse().Select(point => new Vector2(-point.x, point.y)).ToArray());
            return Lathe(large ? "Balloon tire casing" : "Ribbed tire casing", Segments, profile.ToArray());
        }

        private Mesh Cylinder(string name, int sides)
        {
            return Lathe(name, sides,
                new Vector2(-.5f, 0f), new Vector2(-.5f, .5f),
                new Vector2(-.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(.5f, .5f), new Vector2(.5f, 0f));
        }

        private Mesh Lathe(string name, int sides, params Vector2[] profile)
        {
            var vertices = new Vector3[sides * profile.Length];
            var triangles = new List<int>();
            for (int ring = 0; ring < profile.Length; ring++)
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * 2f * Mathf.PI / sides;
                    int index = ring * sides + side;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * profile[ring].y,
                        Mathf.Sin(angle) * profile[ring].y, profile[ring].x);
                    if (ring == profile.Length - 1 || profile[ring] == profile[ring + 1])
                        continue;
                    int next = ring * sides + (side + 1) % sides;
                    if (profile[ring].y > 0f)
                        triangles.AddRange(new[] { index, next, index + sides });
                    if (profile[ring + 1].y > 0f)
                        triangles.AddRange(new[] { next, next + sides, index + sides });
                }
            return MakeMesh(name, vertices, triangles.ToArray());
        }

        private Mesh Fairing(Vector3 front, Vector3 rear, Vector3 end)
        {
            Vector3 thickness = Vector3.Cross(end - front, rear - front).normalized * .023f;
            var corners = new[]
            {
                front + thickness, rear + thickness, end + thickness,
                front - thickness, rear - thickness, end - thickness
            };
            var faces = new[] { 0, 2, 1, 3, 4, 5, 0, 4, 3, 0, 1, 4, 1, 5, 4, 1, 2, 5, 2, 3, 5, 2, 0, 3 };
            return MakeMesh("Triangular gear fairing", faces.Select(i => corners[i]).ToArray(),
                Enumerable.Range(0, faces.Length).ToArray());
        }

        private Mesh MakeMesh(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.uv = vertices.Select(vertex => new Vector2(vertex.x + .5f, vertex.z + .5f)).ToArray();
            mesh.colors32 = Enumerable.Repeat(new Color32(255, 255, 255, 255), vertices.Length).ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            assets.Add(mesh);
            return mesh;
        }

        private Material MakeMaterial(Material stock, string name, Color color, float gloss)
        {
            var material = new Material(stock) { name = name, mainTexture = null };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", gloss);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", gloss);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            assets.Add(material);
            return material;
        }

        private Transform Rod(Transform parent, string name, Vector3 from, Vector3 to, float diameter,
            Material material, bool selectable = false)
        {
            var rod = Render(parent, name, tube, material);
            rod.localScale = Vector3.one * diameter;
            PositionRod(rod, from, to);
            if (selectable)
                rod.gameObject.AddComponent<BoxCollider>().size = new Vector3(.82f, .82f, .89f);
            return rod;
        }

        internal static void PositionRod(Transform rod, Vector3 from, Vector3 to)
        {
            rod.localPosition = (from + to) / 2f;
            rod.localRotation = Quaternion.LookRotation(to - from);
            var scale = rod.localScale;
            scale.z = Vector3.Distance(from, to);
            rod.localScale = scale;
        }

        private Transform Render(Transform parent, string name, Mesh mesh, Material material)
        {
            var child = Child(parent, name, Vector3.zero);
            child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (material == painted && painted.HasProperty("_Color"))
                child.gameObject.AddComponent<PaintableRenderer>().paintableMaterialIndex = 0;
            return child;
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
            if (storage)
                Object.Destroy(storage.gameObject);
            foreach (var asset in assets)
                Object.Destroy(asset);
            current = null;
        }
    }

    internal static class ResearchGrid
    {
        internal const float Cell = 200f;
        internal static int GridIndex(float coordinate) => Mathf.FloorToInt(coordinate / Cell);

        internal static int InsertAfter(ResearchTree tree, ResearchTreeItem parent)
        {
            if (!parent)
                return tree.items.Where(item => item).Select(item => GridIndex(item.window.y)).DefaultIfEmpty(-1).Max() + 1;

            int row = GridIndex(parent.window.y) + 1;
            // Keep nearby branches in order while opening a row for the new sibling path.
            foreach (var item in tree.items)
            {
                if (!item || GridIndex(item.window.y) < row)
                    continue;
                var window = item.window;
                window.y += Cell;
                item.window = window;
            }
            return row;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MachWorks.BackCountryOperations
{
    internal sealed class SceneryProbe : IDisposable
    {
        private const string Section = "SceneryProbe";
        private static readonly DataContractJsonSerializer CaptureSerializer =
            new DataContractJsonSerializer(typeof(ChunkCapture), new DataContractJsonSerializerSettings
            {
                MaxItemsInObjectGraph = int.MaxValue,
            });
        private static SceneryProbe active;

        private readonly Harmony harmony = new Harmony(BackCountryOperationsPlugin.PluginId + ".sceneryprobe");
        private readonly ManualLogSource logger;
        private readonly string directory;
        private readonly bool centerOnStartingAirport;
        private readonly float radius;
        private readonly int limit;
        private Vector2 center;
        private bool centerReady;
        private int written;
        private bool failed;

        private SceneryProbe(ManualLogSource logger, bool centerOnStartingAirport, Vector2 center, float radius, int limit)
        {
            this.logger = logger;
            this.centerOnStartingAirport = centerOnStartingAirport;
            this.center = center;
            centerReady = !centerOnStartingAirport;
            this.radius = radius;
            this.limit = limit;
            directory = Path.Combine(Paths.BepInExRootPath, "BackCountryOperations", "captures",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        }

        internal static SceneryProbe Start(ConfigFile config, ManualLogSource logger)
        {
            var enabled = config.Bind(Section, "Enabled", false, "Capture scenery diagnostics. Restart to apply changes in this section.");
            var autoCenter = config.Bind(Section, "CenterOnStartingAirport", true, "Use the starting airport selected by the game. Turn off to use CenterX and CenterZ.");
            var centerX = config.Bind(Section, "CenterX", 0f, "World X of the center of the capture area.");
            var centerZ = config.Bind(Section, "CenterZ", 0f, "World Z of the center of the capture area.");
            var radius = config.Bind(Section, "Radius", 1000f, "Capture chunk origins within this horizontal distance. Set a positive value up to 20000.");
            var limit = config.Bind(Section, "MaxCaptures", 64,
                new ConfigDescription("Maximum chunk snapshots per launch.", new AcceptableValueRange<int>(1, 256)));

            if (!enabled.Value)
                return null;

            if (!IsFinite(centerX.Value) || !IsFinite(centerZ.Value) ||
                !IsFinite(radius.Value) || radius.Value <= 0f || radius.Value > 20000f)
            {
                logger.LogWarning("Scenery capture requires finite coordinates and a radius greater than zero and at most 20000.");
                return null;
            }

            var probe = new SceneryProbe(logger, autoCenter.Value, new Vector2(centerX.Value, centerZ.Value), radius.Value, limit.Value);
            try
            {
                probe.PatchPostfix(typeof(Chunk), nameof(Chunk.NewSurfaceDecor), nameof(AfterDecorations));
                if (probe.centerOnStartingAirport)
                    probe.PatchPostfix(typeof(AirportManager), "SetInitialAirport", nameof(AfterInitialAirport));
                active = probe;
                logger.LogInfo($"Scenery capture enabled. Matching snapshots will be saved to {probe.directory}");
                return probe;
            }
            catch (Exception error)
            {
                probe.Dispose();
                logger.LogWarning($"Scenery capture could not start: {error}");
                return null;
            }
        }

        private void PatchPostfix(Type type, string method, string postfix)
        {
            var target = AccessTools.DeclaredMethod(type, method, Type.EmptyTypes);
            if (target == null)
                throw new MissingMethodException(type.FullName, method);

            harmony.Patch(target, postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(SceneryProbe), postfix)));
        }

        public void Dispose()
        {
            if (active == this)
                active = null;

            try
            {
                harmony.UnpatchSelf();
            }
            catch (Exception error)
            {
                logger.LogWarning($"Could not detach scenery diagnostics: {error}");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void AfterDecorations(Vector3 ___position, List<Tree> ___trees)
        {
            active?.Capture(___position, ___trees);
        }

        private static void AfterInitialAirport(AirportManager __instance)
        {
            active?.SelectStartingAirport(__instance);
        }

        private void SelectStartingAirport(AirportManager airportManager)
        {
            centerReady = false;
            // The stock method uses the last airport whose data marks it as the initial spawn.
            for (var i = airportManager.airports.Count - 1; i >= 0; i--)
            {
                var airport = airportManager.airports[i];
                if (airport?.data == null || !airport.data.initialSpawnAirport)
                    continue;

                var position = airport.position;
                center = new Vector2(position.x, position.z);
                centerReady = true;
                logger.LogInfo($"Scenery capture centered on {airport.airportName} at {position}.");
                return;
            }
        }

        private void Capture(Vector3 origin, List<Tree> trees)
        {
            if (!centerReady || failed || written >= limit)
                return;

            var dx = origin.x - center.x;
            var dz = origin.z - center.y;
            if (dx * dx + dz * dz > radius * radius)
                return;

            try
            {
                var continent = UnityEngine.Object.FindFirstObjectByType<ContinentManager>();
                var world = continent != null ? continent.worldAsset : null;
                var decorations = new List<DecorationSample>(trees.Count);
                foreach (var tree in trees)
                {
                    if (tree != null)
                        decorations.Add(new DecorationSample(tree));
                }

                var capture = new ChunkCapture
                {
                    schema = 2,
                    gameVersion = Application.version,
                    pluginVersion = BackCountryOperationsPlugin.PluginVersion,
                    hasWorldAsset = world != null,
                    worldAsset = world != null ? world.name : string.Empty,
                    worldVersionStartingFrom = world != null ? world.versionStartingFrom : -1,
                    chunkOrigin = new VectorSample(origin),
                    decorationCount = decorations.Count,
                    decorations = decorations.ToArray(),
                };

                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"{written + 1:D4}.json");
                using (var output = File.Create(path + ".tmp"))
                {
                    CaptureSerializer.WriteObject(output, capture);
                }
                File.Move(path + ".tmp", path);
                written++;

                if (written == limit)
                    logger.LogInfo($"Scenery capture reached its per-launch limit of {limit} snapshots.");
            }
            catch (Exception error)
            {
                failed = true;
                logger.LogWarning($"Scenery capture stopped for this launch: {error}");
            }
        }

        [Serializable]
        private sealed class ChunkCapture
        {
            public int schema;
            public string gameVersion;
            public string pluginVersion;
            public bool hasWorldAsset;
            public string worldAsset;
            public int worldVersionStartingFrom;
            public VectorSample chunkOrigin;
            public int decorationCount;
            public DecorationSample[] decorations;
        }

        [Serializable]
        private sealed class DecorationSample
        {
            public string decorationType;
            public VectorSample position;
            public RotationSample rotation;
            public VectorSample scale;
            public string mesh;
            public string distantMesh;
            public bool hasCollider;
            public bool colliderActive;
            public VectorSample colliderMin;
            public VectorSample colliderMax;

            public DecorationSample(Tree tree)
            {
                var transform = tree.transform;
                var type = tree.surfaceDecorationType;
                decorationType = type != null ? type.name : string.Empty;
                position = new VectorSample(transform.position);
                rotation = new RotationSample(transform.rotation);
                scale = new VectorSample(transform.lossyScale);
                mesh = MeshName(tree.LOD0);
                distantMesh = MeshName(tree.LOD1);

                var collider = tree.GetComponent<CapsuleCollider>();
                hasCollider = collider != null;
                if (hasCollider)
                {
                    colliderActive = collider.enabled && collider.gameObject.activeInHierarchy;
                    // The pooled collider's physics bounds can still reflect its previous
                    // position during placement. Compute the capsule bounds from its live transform.
                    var colliderTransform = collider.transform;
                    var scale = colliderTransform.lossyScale;
                    var x = Mathf.Abs(scale.x);
                    var y = Mathf.Abs(scale.y);
                    var z = Mathf.Abs(scale.z);
                    var axis = Vector3.up;
                    var heightScale = y;
                    var radiusScale = Mathf.Max(x, z);
                    switch (collider.direction)
                    {
                        case 0:
                            axis = Vector3.right;
                            heightScale = x;
                            radiusScale = Mathf.Max(y, z);
                            break;
                        case 2:
                            axis = Vector3.forward;
                            heightScale = z;
                            radiusScale = Mathf.Max(x, y);
                            break;
                    }

                    var radius = collider.radius * radiusScale;
                    var halfSegment = Mathf.Max(0f, collider.height * heightScale * 0.5f - radius);
                    var worldAxis = colliderTransform.rotation * axis;
                    var extents = new Vector3(Mathf.Abs(worldAxis.x), Mathf.Abs(worldAxis.y), Mathf.Abs(worldAxis.z))
                        * halfSegment + Vector3.one * radius;
                    var center = colliderTransform.TransformPoint(collider.center);
                    colliderMin = new VectorSample(center - extents);
                    colliderMax = new VectorSample(center + extents);
                }
            }

            private static string MeshName(MeshFilter filter)
            {
                var mesh = filter != null ? filter.sharedMesh : null;
                return mesh != null ? mesh.name : string.Empty;
            }
        }

        [Serializable]
        private struct VectorSample
        {
            public float x;
            public float y;
            public float z;

            internal VectorSample(Vector3 value)
            {
                x = value.x;
                y = value.y;
                z = value.z;
            }
        }

        [Serializable]
        private struct RotationSample
        {
            public float x;
            public float y;
            public float z;
            public float w;

            internal RotationSample(Quaternion value)
            {
                x = value.x;
                y = value.y;
                z = value.z;
                w = value.w;
            }
        }
    }
}

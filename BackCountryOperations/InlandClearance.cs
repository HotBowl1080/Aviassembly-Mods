using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MachWorks.BackCountryOperations
{
    internal sealed class InlandClearance : IDisposable
    {
        private static readonly Vector2[] SurveyedTrees =
        {
            new Vector2(-5.902956f, 324.1728f),
            new Vector2(-5.882867f, 371.469238f),
        };

        private const float MatchRadiusSquared = 0.25f;
        private static InlandClearance active;
        private readonly ManualLogSource logger;
        private readonly bool baseEnabled;
        private readonly bool valleyEnabled;
        private readonly bool windEnabled;
        private readonly Harmony harmony = new Harmony(BackCountryOperationsPlugin.PluginId + ".inlandclearance");

        private InlandClearance(ManualLogSource logger, bool baseEnabled, bool valleyEnabled, bool windEnabled)
        {
            this.logger = logger;
            this.baseEnabled = baseEnabled;
            this.valleyEnabled = valleyEnabled;
            this.windEnabled = windEnabled;
        }

        internal static InlandClearance Start(ConfigFile config, ManualLogSource logger, bool valleyEnabled, bool windEnabled)
        {
            var baseEnabled = config.Bind("InlandClearance", "Enabled", false,
                "Clear the two surveyed trees beyond the inland end of Base in the verified world. Restart to apply.").Value;
            if (!baseEnabled && !valleyEnabled && !windEnabled)
                return null;

            var clearance = new InlandClearance(logger, baseEnabled, valleyEnabled, windEnabled);
            try
            {
                var placement = AccessTools.DeclaredMethod(typeof(Chunk), nameof(Chunk.NewSurfaceDecor), Type.EmptyTypes);
                if (placement == null)
                    throw new MissingMethodException(typeof(Chunk).FullName, nameof(Chunk.NewSurfaceDecor));

                var postfix = new HarmonyMethod(typeof(InlandClearance), nameof(AfterDecorations))
                {
                    priority = Priority.First,
                };
                clearance.harmony.Patch(placement, postfix: postfix);
                active = clearance;
                logger.LogInfo($"Managed clearance enabled. Base: {baseEnabled}. Valley Strip: {valleyEnabled}.");
                return clearance;
            }
            catch (Exception error)
            {
                clearance.Dispose();
                logger.LogWarning($"Could not enable Base inland clearance: {error}");
                return null;
            }
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
                logger.LogWarning($"Could not detach Base inland clearance: {error}");
            }
        }

        private static void AfterDecorations(Vector3 ___position, List<Tree> ___trees, ObjectPool ___surfaceDecorationPool,
            global::Terrain ___terrain)
        {
            active?.Clear(___position, ___trees, ___surfaceDecorationPool, ___terrain?.heightMap);
        }

        private void Clear(Vector3 origin, List<Tree> trees, ObjectPool pool, HeightMap heights)
        {
            var atBase = baseEnabled && origin.sqrMagnitude <= 0.01f;
            var atValley = valleyEnabled && (origin - ValleyStrip.ChunkOrigin).sqrMagnitude <= 0.01f;
            bool atWind = windEnabled && (origin - OpeningAirfields.WindPosition).sqrMagnitude <= .01f;
            if ((!atBase && !atValley && !atWind) || trees == null || pool == null)
                return;

            var continents = UnityEngine.Object.FindFirstObjectByType<ContinentManager>();
            var world = continents != null ? continents.worldAsset : null;
            if (world == null || world.name != "LocalizedVersion" || world.versionStartingFrom != 24)
                return;

            if (atWind)
            {
                RelocateWindTrees(origin, trees, heights);
                return;
            }

            if (atValley)
            {
                var blocker = FindDecoration(trees, ValleyStrip.Blocker, "Pebbles");
                if (blocker >= 0)
                {
                    Release(trees, pool, blocker);
                    logger.LogInfo("Valley Strip cleared the surveyed edge rock.");
                }
                return;
            }

            var cleared = 0;
            foreach (var target in SurveyedTrees)
            {
                var index = FindDecoration(trees, target, "Tree");
                if (index < 0)
                    continue;

                Release(trees, pool, index);
                cleared++;
            }

            logger.LogInfo($"Base inland clearance released {cleared} of {SurveyedTrees.Length} surveyed trees.");
        }

        internal static int RelocateWindTrees(Vector3 origin, List<Tree> trees, HeightMap heights)
        {
            var airports = AirportManager.Instance;
            var wind = OpeningAirfields.Find(airports, OpeningAirfields.WindPosition);
            if (wind == null || heights == null || !wind.data || !wind.data.airportPrefab)
                return 0;
            var prefab = wind.data.airportPrefab.transform;
            var runway = prefab.GetComponentInChildren<RunwayGenerator>(true);
            if (!runway)
                return 0;

            var occupied = new HashSet<Tree>();
            var rotation = airports.GetAirportRotation(wind);
            for (int i = 0; i < 7; i++)
            {
                var inset = new Vector3(-62f - 13f * Mathf.Sin(i * 2.35f), 0f, -147f + i * 48f + 12f * Mathf.Sin(i * 5.3f));
                var target = wind.position + rotation * prefab.InverseTransformPoint(runway.transform.TransformPoint(inset));
                var sample = new Vector2(target.x - origin.x + 500f, target.z - origin.z + 500f);
                target.y = heights.GetHeight(1000, sample);
                if (target.y < -2f || heights.GetCollision(1000f, sample) > .5f || heights.GetNormal(1000f, sample).y < .82f)
                    continue;
                Tree best = null;
                float nearest = 205f * 205f;
                foreach (var tree in trees)
                {
                    if (!tree || !tree.surfaceDecorationType || tree.surfaceDecorationType.name != "Tree" || occupied.Contains(tree))
                        continue;
                    float distance = (tree.transform.position - target).sqrMagnitude;
                    if (distance < 18f * 18f)
                    {
                        best = null;
                        break;
                    }
                    if (distance < nearest)
                    {
                        best = tree;
                        nearest = distance;
                    }
                }
                if (best)
                {
                    best.SetPosition(target);
                    occupied.Add(best);
                }
            }
            return occupied.Count;
        }

        private static void Release(List<Tree> trees, ObjectPool pool, int index)
        {
            pool.ReleaseObject(trees[index]);
            // Chunk.OnDestroy releases every tree still on this list.
            trees.RemoveAt(index);
        }

        private static int FindDecoration(List<Tree> trees, Vector2 target, string type)
        {
            var nearest = -1;
            var nearestDistance = MatchRadiusSquared;
            for (var i = 0; i < trees.Count; i++)
            {
                var tree = trees[i];
                if (tree == null || tree.surfaceDecorationType == null || tree.surfaceDecorationType.name != type)
                    continue;

                var position = tree.transform.position;
                var distance = (new Vector2(position.x, position.z) - target).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }
    }
}

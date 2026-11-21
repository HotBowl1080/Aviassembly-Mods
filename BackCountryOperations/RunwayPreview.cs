using System;
using System.IO;
using System.Runtime.Serialization.Json;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MachWorks.BackCountryOperations
{
    internal sealed class RunwayPreview : MonoBehaviour
    {
        private static readonly System.Reflection.FieldInfo PavementField =
            AccessTools.DeclaredField(typeof(RunwayGenerator), "runway");

        private ManualLogSource logger;
        private WorldAsset markedWorld;
        private Vector3 first;
        private Vector3 second;
        private Vector3 side;
        private float halfWidth;
        private int pointCount;
        private float nextSurveyTime;
        private bool automaticSurvey = true;
        private bool showOverlay;
        private string status = "Waiting for the starting runway to load.";

        internal void Initialize(ManualLogSource source)
        {
            logger = source;
        }

        private void Update()
        {
            if (IsTitleScreen())
                return;

            if (automaticSurvey && Time.unscaledTime >= nextSurveyTime)
            {
                nextSurveyTime = Time.unscaledTime + 2f;
                TrySurveyStartingRunway();
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                automaticSurvey = false;
                pointCount = 0;
                markedWorld = null;
                showOverlay = false;
                status = "Preview cleared. Aim at the first runway end and press F6.";
                return;
            }

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                automaticSurvey = true;
                showOverlay = true;
                nextSurveyTime = 0f;
                status = "Looking for the starting runway.";
            }
            else if (keyboard.f6Key.wasPressedThisFrame)
            {
                automaticSurvey = false;
                showOverlay = true;
                Mark(1);
            }
            else if (keyboard.f7Key.wasPressedThisFrame && pointCount >= 1)
                Mark(2);
            else if (keyboard.f8Key.wasPressedThisFrame && pointCount >= 2)
                Mark(3);
        }

        private void TrySurveyStartingRunway()
        {
            var worldManager = UnityEngine.Object.FindFirstObjectByType<ContinentManager>();
            var world = worldManager != null ? worldManager.worldAsset : null;
            var airports = UnityEngine.Object.FindFirstObjectByType<AirportManager>();
            if (world == null || airports == null)
                return;

            Airport startingAirport = null;
            foreach (var airport in airports.airports)
            {
                if (airport?.data != null && airport.data.initialSpawnAirport)
                    startingAirport = airport;
            }

            if (startingAirport == null)
                return;

            GameObject pavement = null;
            var closestDistance = 500f * 500f;
            foreach (var runway in UnityEngine.Object.FindObjectsByType<RunwayGenerator>(FindObjectsSortMode.None))
            {
                var candidate = PavementField?.GetValue(runway) as GameObject;
                if (candidate == null)
                    continue;

                var distance = (candidate.transform.position - startingAirport.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    pavement = candidate;
                }
            }

            var filter = pavement != null ? pavement.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
            {
                status = "Waiting for the starting runway mesh. F6 starts a manual survey.";
                return;
            }

            var bounds = filter.sharedMesh.bounds;
            var transform = pavement.transform;
            var top = bounds.center;
            top.y = bounds.max.y;
            var start = transform.TransformPoint(top - Vector3.forward * bounds.extents.z);
            var end = transform.TransformPoint(top + Vector3.forward * bounds.extents.z);
            var edge = transform.TransformPoint(top + Vector3.right * bounds.extents.x);
            var direction = end - start;
            direction.y = 0f;
            var lateral = Vector3.Cross(Vector3.up, direction.normalized);
            var width = Mathf.Abs(Vector3.Dot(edge - start, lateral));
            if (direction.sqrMagnitude < 1f || width < 0.5f)
            {
                status = "The runway mesh has invalid dimensions. F6 starts a manual survey.";
                return;
            }

            first = start;
            second = end;
            side = lateral;
            halfWidth = width;
            markedWorld = world;
            pointCount = 3;
            automaticSurvey = false;
            SaveRunway();
        }

        private void Mark(int point)
        {
            var camera = Camera.main;
            var mouse = Mouse.current;
            var continent = UnityEngine.Object.FindFirstObjectByType<ContinentManager>();
            var world = continent != null ? continent.worldAsset : null;
            if (camera == null || mouse == null || world == null)
            {
                status = "Load Base in flight mode before marking a point.";
                return;
            }

            if (point > 1 && markedWorld != world)
            {
                pointCount = 0;
                status = "The world changed. Mark the first end again with F6.";
                return;
            }

            var ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 3000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                status = "No surface under the pointer. Aim at the pavement and try again.";
                return;
            }

            if (point == 1)
            {
                first = hit.point;
                markedWorld = world;
                pointCount = 1;
                status = "First end marked. Aim at the opposite end and press F7.";
                return;
            }

            if (point == 2)
            {
                var direction = hit.point - first;
                direction.y = 0f;
                if (direction.sqrMagnitude < 1f)
                {
                    status = "The ends are too close together. Aim at the far end and press F7.";
                    return;
                }

                second = hit.point;
                side = Vector3.Cross(Vector3.up, direction.normalized);
                pointCount = 2;
                status = "Centerline marked. Aim at either pavement edge and press F8.";
                return;
            }

            var offset = Mathf.Abs(Vector3.Dot(hit.point - first, side));
            if (offset < 0.5f)
            {
                status = "That point is too close to the centerline. Aim at a pavement edge and press F8.";
                return;
            }

            halfWidth = offset;
            pointCount = 3;
            SaveRunway();
        }

        private void SaveRunway()
        {
            try
            {
                var directory = Path.Combine(Paths.BepInExRootPath, "BackCountryOperations", "runways");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"runway-{Guid.NewGuid():N}.json");
                var runway = new RunwayRecord
                {
                    worldAsset = markedWorld.name,
                    worldVersionStartingFrom = markedWorld.versionStartingFrom,
                    thresholdA = new PointRecord { x = first.x, z = first.z },
                    thresholdB = new PointRecord { x = second.x, z = second.z },
                    width = halfWidth * 2f,
                };

                using (var stream = File.Create(path + ".tmp"))
                {
                    new DataContractJsonSerializer(typeof(RunwayRecord)).WriteObject(stream, runway);
                }

                File.Move(path + ".tmp", path);
                status = $"Preview saved. Width {runway.width:F1} world units. F5 resurvey, F9 clear.";
                logger.LogInfo($"Runway preview saved to {path}");
            }
            catch (Exception error)
            {
                status = "Preview drawn. Saving failed, see the BepInEx log.";
                logger.LogWarning($"Could not save runway preview: {error}");
            }
        }

        private void OnGUI()
        {
            var camera = Camera.main;
            if (!showOverlay || Event.current.type != EventType.Repaint || camera == null || IsTitleScreen())
                return;

            GUI.Box(new Rect(12f, Screen.height - 110f, Mathf.Min(620f, Screen.width - 24f), 46f), status);
            if (pointCount < 2)
                return;

            var lift = Vector3.up * 0.25f;
            var start = first + lift;
            var end = second + lift;
            DrawLine(camera, start, end, Color.yellow);
            if (pointCount < 3)
                return;

            var offset = side * halfWidth;
            DrawLine(camera, start - offset, end - offset, Color.cyan);
            DrawLine(camera, start + offset, end + offset, Color.cyan);
            DrawLine(camera, start - offset, start + offset, Color.cyan);
            DrawLine(camera, end - offset, end + offset, Color.cyan);
        }

        private GameManager game;

        private bool IsTitleScreen()
        {
            if (game == null)
                game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            return game == null || game.inMenu;
        }

        internal static void DrawLine(Camera camera, Vector3 start, Vector3 end, Color color)
        {
            var a = camera.WorldToScreenPoint(start);
            var b = camera.WorldToScreenPoint(end);
            var near = camera.nearClipPlane + 0.01f;
            if (a.z < near && b.z < near)
                return;

            if (a.z < near)
                a = camera.WorldToScreenPoint(Vector3.Lerp(start, end, (near - a.z) / (b.z - a.z)));
            else if (b.z < near)
                b = camera.WorldToScreenPoint(Vector3.Lerp(end, start, (near - b.z) / (a.z - b.z)));

            var origin = new Vector2(a.x, Screen.height - a.y);
            var delta = new Vector2(b.x - a.x, a.y - b.y);
            var matrix = GUI.matrix;
            var previousColor = GUI.color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, origin);
            GUI.color = color;
            GUI.DrawTexture(new Rect(origin.x, origin.y - 1.5f, delta.magnitude, 3f), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.matrix = matrix;
        }

        [Serializable]
        private sealed class RunwayRecord
        {
            public string worldAsset;
            public int worldVersionStartingFrom;
            public PointRecord thresholdA;
            public PointRecord thresholdB;
            public float width;
        }

        [Serializable]
        private struct PointRecord
        {
            public float x;
            public float z;
        }
    }
}

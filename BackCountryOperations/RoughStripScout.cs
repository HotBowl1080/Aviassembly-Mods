using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MachWorks.BackCountryOperations
{
    internal sealed class RoughStripScout : MonoBehaviour
    {
        private const float HalfWidth = 12f;
        private const int Columns = 17;
        private const int Rows = 18;
        private const float SearchInterval = 2f;
        private const int CentersPerFrame = 3;
        private static readonly Vector3[] Headings =
        {
            Vector3.forward,
            Vector3.right,
            new Vector3(1f, 0f, 1f).normalized,
            new Vector3(-1f, 0f, 1f).normalized,
        };
        private static readonly float[] HalfLengths = { 65f, 50f };
        internal const int StripJob = 3;
        internal const int FreightJob = 4;
        internal const int GearFitJob = 5;
        internal const int LoadedFreightJob = 6;
        internal const int RepairJob = 7;
        internal const int MaplewoodDrainsJob = 8;
        internal const int PumpReturnJob = 9;
        internal static readonly string[] JobNames =
        {
            "Inland Flyover", "Valley Recon", "Coastal Patrol", "Rough-Strip Survey",
            "Valley Supply Trial", TownContracts.FarmCheckName, "Loaded Short-Field Run",
            "Repair the Wind Farm Runway", TownContracts.DrainageName, TownContracts.PumpName,
        };
        internal static readonly Vector3[][] Routes =
        {
            new[] { new Vector3(0f, 350f, 1000f) },
            new[] { new Vector3(0f, 350f, 1000f), new Vector3(620f, 540f, 1650f), new Vector3(-550f, 540f, 1500f) },
            new[] { new Vector3(0f, 180f, -850f), new Vector3(1050f, 260f, -1000f), new Vector3(1150f, 350f, -250f) },
            Array.Empty<Vector3>(),
            Array.Empty<Vector3>(),
            new[] { new Vector3(1000f, 0f, 10000f) },
            Array.Empty<Vector3>(),
            Array.Empty<Vector3>(),
        };

        private readonly List<MeshCollider> terrain = new List<MeshCollider>();
        private ManualLogSource logger;
        private WorldAsset surveyedWorld;
        private Vector3 lastSearchPosition;
        private float nextSearchTime;
        private int candidateIndex;
        private bool searched;
        private bool searching;
        private bool found;
        private Vector3 start;
        private Vector3 end;
        private Vector3 stripOffset;
        private float stripHalfLength;
        private int measuredFootprints;
        private int blockedFootprints;
        private SurveyFlight surveyFlight;
        private MapJobPanel mapJobs;
        private string saveFile;
        private GameManager game;
        private bool firstDeliveryComplete;
        private bool career;
        private ValleySiteReport bestSite;
        private int exportedBlockerCount = int.MaxValue;
        private ValleyStrip valley;
        private bool allowValley;
        private CargoType supplyCargo;
        private Airport hospital, wind;
        private bool restoreTownTracking;
        private bool scoutingLumbercamp;
        private bool lumbercampSurveyed;
        private Vector3 lumbercampOrigin;
        private Vector3 lastLumbercampPosition;
        private GUIStyle flightLabel;

        internal void Initialize(ManualLogSource source, bool valleyEnabled)
        {
            logger = source;
            allowValley = valleyEnabled;
            surveyFlight = new SurveyFlight(source);
            mapJobs = new MapJobPanel(surveyFlight, TryAcceptJob);
            if (valleyEnabled)
                valley = new ValleyStrip(source);
        }

        private void Update()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            if (Time.unscaledTime >= nextSearchTime)
            {
                nextSearchTime = Time.unscaledTime + SearchInterval;
                var manager = UnityEngine.Object.FindFirstObjectByType<ContinentManager>();
                var world = manager != null ? manager.worldAsset : null;
                var currentGame = UnityEngine.Object.FindFirstObjectByType<GameManager>();
                var currentSave = currentGame != null ? currentGame.currentSaveFile : null;
                if (world != surveyedWorld || currentGame != game || currentSave != saveFile)
                {
                    surveyedWorld = world;
                    game = currentGame;
                    saveFile = currentSave;
                    searched = false;
                    found = false;
                    searching = false;
                    scoutingLumbercamp = false;
                    lumbercampSurveyed = false;
                    lastLumbercampPosition = Vector3.zero;
                    terrain.Clear();
                    firstDeliveryComplete = false;
                    bestSite = null;
                    supplyCargo = null;
                    hospital = wind = null;
                    valley?.Dispose();
                    exportedBlockerCount = int.MaxValue;
                    surveyFlight.ClearCareer();
                    mapJobs.Reset();
                    TownContracts.ClearTracking();
                    restoreTownTracking = true;
                }

                career = game != null && game.gameModeData != null &&
                    !game.gameModeData.creativeMode && !game.gameModeData.disableAirports;
                if (SupportedWorld && career)
                {
                    var airports = UnityEngine.Object.FindFirstObjectByType<AirportManager>();
                    hospital = OpeningAirfields.Find(airports, OpeningAirfields.HospitalPosition);
                    wind = OpeningAirfields.Find(airports, OpeningAirfields.WindPosition);
                    WindFarmWorks.Prepare(wind);
                    TownAirfieldWorks.Prepare(airports);
                    var delivery = HospitalMilestone.FindFirstDelivery(airports);
                    if (delivery?.completed == true && !firstDeliveryComplete)
                        logger.LogInfo($"Battery delivery recognized from native Hospital quest {delivery.questID}.");
                    if (delivery != null)
                        firstDeliveryComplete = delivery.completed;
                    supplyCargo = allowValley && firstDeliveryComplete ? CampaignProgress.ValleyCargo(delivery?.cargoType) : null;
                    if (valley != null && LaterJobsReady && CampaignProgress.IsDone(ValleyProgress.FirstSupply))
                        TryScoutLumbercamp(airports);
                }

                if (SupportedWorld && valley != null)
                {
                    valley.Update();
                    if (valley.Verified)
                    {
                        start = ValleyStrip.FirstEnd;
                        end = ValleyStrip.SecondEnd;
                        stripHalfLength = ValleyStrip.HalfLength;
                        stripOffset = Vector3.right * HalfWidth;
                        found = true;
                    }
                }

                var position = camera.transform.position;
                position.y = 0f;
                if (allowValley && SupportedWorld && valley == null && !searching && !found && position.sqrMagnitude <= 3500f * 3500f &&
                    (!searched || (position - lastSearchPosition).sqrMagnitude >= 250f * 250f))
                {
                    lastSearchPosition = position;
                    BeginSearch();
                }
            }

            if (!SupportedWorld)
                return;

            if (surveyFlight.Active && !mapJobs.Visible && Keyboard.current?.backspaceKey.wasPressedThisFrame == true &&
                InputManager.Instance && !InputManager.Instance.currentInputBlocker)
                surveyFlight.Abandon();
            surveyFlight.Update(start, end, stripHalfLength, HalfWidth);
            if (career && wind != null && TryDeliverWindRepair(PlaneContainer.Instance, wind))
            {
                QuestAdditionUI.Instance?.DisplayText("Patch kit received. We'll start the runway repairs now.", MessageType.Misc, null);
                logger.LogInfo("Wind Farm runway repair crate delivered from Base.");
            }
            if (career && LaterJobsReady)
                UpdateTownDeliveries();
            if (searching)
                SearchBatch();
        }

        private bool SupportedWorld => surveyedWorld != null &&
            surveyedWorld.name == "LocalizedVersion" && surveyedWorld.versionStartingFrom == 24 &&
            game != null && !game.inMenu;
        private bool LaterJobsReady => firstDeliveryComplete && (CampaignProgress.IsDone(ValleyProgress.WindRunwayRepair) ||
            !CampaignProgress.Available);
        private bool CanFlyStrip => found && (!career || LaterJobsReady);
        private bool CanFlyFreight => career && LaterJobsReady && valley?.CargoBay != null && supplyCargo != null &&
            supplyCargo.cargoSpace > 0 && !supplyCargo.fragile && !supplyCargo.expires;
        internal static bool IsFreightJob(int index) => index == FreightJob || index == LoadedFreightJob;
        private static ValleyProgress FreightMilestone(int index) =>
            index == LoadedFreightJob ? ValleyProgress.LoadedSupply : ValleyProgress.FirstSupply;
        internal static int FreightAmount(int index) => index == LoadedFreightJob ? 2 : 1;

        internal static bool ShowJob(int index, bool career, bool firstDeliveryComplete) => index == RepairJob ?
            career && CampaignProgress.Available && !CampaignProgress.IsDone(ValleyProgress.WindRunwayRepair) :
            TownContracts.IsTownJob(index) ? career && firstDeliveryComplete && TownContracts.IsOffered(index) :
            index == GearFitJob && career && firstDeliveryComplete && !CampaignProgress.IsDone(ValleyProgress.FarmGearCheck);

        private void LateUpdate()
        {
            if (SupportedWorld)
                mapJobs.Update(CanFlyStrip, found ? (start + end) * .5f :
                    valley != null ? ValleyStrip.Center : (Vector3?)null, career, LaterJobsReady,
                    CanFlyFreight ? supplyCargo : null, hospital, wind);
        }

        private void OnDestroy()
        {
            surveyFlight?.Clear();
            mapJobs?.Dispose();
            valley?.Dispose();
        }

        private bool TryAcceptJob(int selection)
        {
            if (selection == RepairJob || TownContracts.IsTownJob(selection) || !ShowJob(selection, career, LaterJobsReady) ||
                surveyFlight.Active || (selection == StripJob && !CanFlyStrip) ||
                (IsFreightJob(selection) && (!CanFlyFreight || CampaignProgress.IsDone(FreightMilestone(selection)))) ||
                (selection == GearFitJob && !(career && LaterJobsReady)))
                return false;
            if (IsFreightJob(selection))
                return surveyFlight.Accept(JobNames[selection], Routes[selection], supplyCargo, valley.CargoBay.Value,
                    freightUnits: FreightAmount(selection), milestone: FreightMilestone(selection));
            if (selection == GearFitJob)
                return surveyFlight.Accept(JobNames[selection], Routes[selection], gearFitCheck: true,
                    milestone: ValleyProgress.FarmGearCheck);
            return surveyFlight.Accept(JobNames[selection], Routes[selection]);
        }

        private void UpdateTownDeliveries()
        {
            var plane = PlaneContainer.Instance;
            var airports = AirportManager.Instance;
            if (restoreTownTracking && plane && plane.FlightModeInitialized && QuestFeedbackManager.Instance)
            {
                restoreTownTracking = false;
                if (QuestFeedbackManager.Instance.currentQuest == null)
                {
                    if (!TownContracts.Track(airports, MaplewoodDrainsJob))
                        TownContracts.Track(airports, PumpReturnJob);
                }
            }
            if (TownContracts.Deliver(plane, airports, MaplewoodDrainsJob))
            {
                QuestAdditionUI.Instance?.DisplayText("Maplewood: The drain kit is here. We'll clear the ditch and grade the parking bay. Utility flaps are available in the workshop.", MessageType.Misc, null);
                logger.LogInfo("Maplewood drainage kit delivered. Utility flaps unlocked.");
            }
            if (TownContracts.Deliver(plane, airports, PumpReturnJob))
            {
                QuestAdditionUI.Instance?.DisplayText("Base: The old pump is back in the shop. The high-lift flap drawings are ready in your workshop.", MessageType.Misc, null);
                logger.LogInfo("Worn pump returned to Base. High-Lift flaps unlocked.");
            }
            TownContracts.CheckTracking(airports);
        }

        private static bool TryDeliverWindRepair(PlaneContainer plane, Airport wind)
        {
            if (!plane || !plane.FlightModeInitialized || Time.timeScale <= 0f ||
                CampaignProgress.IsDone(ValleyProgress.WindRunwayRepair) ||
                (plane.transform.position - wind.position).sqrMagnitude > 150f * 150f)
                return false;
            var inventory = CargoInventory.Instance;
            var cargo = OpeningAirfields.RepairCargo(AirportManager.Instance);
            if (!inventory || !cargo || inventory.GetCargoCount(cargo) == 0 ||
                !OpeningAirfields.AtServiceBay(wind, plane.transform.position) ||
                !plane.TryGetComponent<Rigidbody>(out var body) || body.linearVelocity.sqrMagnitude > 4f || !plane.IsGrounded())
                return false;

            inventory.RemoveCargo(cargo, 1);
            CampaignProgress.Complete(ValleyProgress.WindRunwayRepair);
            return true;
        }

        private void TryScoutLumbercamp(AirportManager airports)
        {
            var plane = PlaneContainer.Instance;
            if (!airports || !plane || !plane.FlightModeInitialized || searching || lumbercampSurveyed)
                return;
            var position = plane.transform.position;
            position.y = 0f;
            if ((position - lastLumbercampPosition).sqrMagnitude < 350f * 350f)
                return;

            foreach (var airport in airports.airports)
            {
                if (airport?.data?.airportName != "The Lumbercamp")
                    continue;
                lumbercampOrigin = airport.position;
                lumbercampOrigin.y = 0f;
                if ((position - lumbercampOrigin).sqrMagnitude > 1850f * 1850f)
                    return;

                BeginSearch();
                scoutingLumbercamp = searching;
                if (searching)
                    lastLumbercampPosition = position;
                return;
            }
        }

        private void BeginSearch()
        {
            terrain.Clear();
            foreach (var tile in UnityEngine.Object.FindObjectsByType<global::Terrain>(FindObjectsSortMode.None))
            {
                var collider = tile.GetComponent<MeshCollider>();
                if (collider != null && collider.enabled && collider.sharedMesh != null)
                    terrain.Add(collider);
            }

            candidateIndex = 0;
            measuredFootprints = 0;
            blockedFootprints = 0;
            bestSite = null;
            scoutingLumbercamp = false;
            searching = terrain.Count > 0;
            searched = searching;
        }

        private void SearchBatch()
        {
            for (var batch = 0; batch < CentersPerFrame && candidateIndex < Columns * Rows; batch++, candidateIndex++)
            {
                // Scan from the centerline out, filling in the terrain between earlier grid points.
                var lateral = candidateIndex % Columns;
                var x = ((lateral + 1) / 2) * (lateral % 2 == 0 ? -110f : 110f);
                var z = 650f + (candidateIndex / Columns) * 110f;
                var center = new Vector3(x, 0f, z);
                if (scoutingLumbercamp)
                {
                    center += lumbercampOrigin - Vector3.forward * 1650f;
                    if ((center - lumbercampOrigin).sqrMagnitude < 550f * 550f)
                        continue;
                }
                foreach (var heading in Headings)
                {
                    foreach (var halfLength in HalfLengths)
                    {
                        if (!TryMeasure(center, heading, halfLength))
                            continue;

                        if (!scoutingLumbercamp)
                            found = true;
                        FinishSearch();
                        return;
                    }
                }
            }

            if (candidateIndex >= Columns * Rows)
                FinishSearch();
        }

        private void FinishSearch()
        {
            searching = false;
            terrain.Clear();
            if (scoutingLumbercamp)
            {
                scoutingLumbercamp = false;
                if (bestSite != null && bestSite.BlockerCount == 0)
                    lumbercampSurveyed = bestSite.Write(logger, "lumbercamp");
                else
                    logger.LogInfo($"Lumbercamp scout found no clear runway candidate in the loaded terrain. {measuredFootprints} checked footprints.");
                return;
            }
            if (bestSite != null && bestSite.BlockerCount < exportedBlockerCount && bestSite.Write(logger))
                exportedBlockerCount = bestSite.BlockerCount;
            if (!found)
                logger.LogInfo($"Rough strip survey found no candidate. {measuredFootprints} footprints passed the terrain checks, and {blockedFootprints} of those were obstructed.");
        }

        private bool TryMeasure(Vector3 center, Vector3 heading, float halfLength)
        {
            var across = Vector3.Cross(Vector3.up, heading);
            if (!TryGround(center - heading * halfLength, out var first) ||
                !TryGround(center + heading * halfLength, out var last) ||
                Mathf.Abs(last.y - first.y) > halfLength * 0.1f)
                return false;

            for (var station = 0; station <= 10; station++)
            {
                var fraction = station / 10f;
                var expectedHeight = Mathf.Lerp(first.y, last.y, fraction);
                var along = Vector3.Lerp(first, last, fraction);
                for (var lane = -1; lane <= 1; lane++)
                {
                    if (!TryGround(along + across * (lane * HalfWidth), out var ground) ||
                        Mathf.Abs(ground.y - expectedHeight) > 2.5f)
                        return false;
                }
            }

            // Check the active scene for objects intersecting the surveyed footprint.
            measuredFootprints++;
            var hitbox = Vector3.Lerp(first, last, 0.5f) + Vector3.up * 4f;
            var blockers = 0;
            foreach (var obstacle in Physics.OverlapBox(hitbox, new Vector3(HalfWidth, 9f, halfLength),
                Quaternion.LookRotation(heading), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if ((!(obstacle is MeshCollider ground) || !terrain.Contains(ground)) &&
                    obstacle.GetComponentInParent<PlaneContainer>() == null)
                    blockers++;
            }
            if (scoutingLumbercamp && blockers == 0)
            {
                if (!ApproachClear(first, -heading) || !ApproachClear(last, heading))
                    blockers++;
            }

            if (bestSite == null || blockers < bestSite.BlockerCount)
                bestSite = new ValleySiteReport(first, last, halfLength * 2f, HalfWidth * 2f, blockers);

            if (blockers > 0)
            {
                blockedFootprints++;
                return false;
            }

            if (scoutingLumbercamp)
                return true;
            start = first;
            end = last;
            stripOffset = across * HalfWidth;
            stripHalfLength = halfLength;
            logger.LogInfo(FormattableString.Invariant(
                $"Rough strip candidate: {2f * halfLength:F0} x 24; thresholds ({first.x:F1}, {first.y:F1}, {first.z:F1}) and ({last.x:F1}, {last.y:F1}, {last.z:F1}); grade {100f * (last.y - first.y) / (2f * halfLength):F1}%."));
            return true;
        }

        private bool ApproachClear(Vector3 threshold, Vector3 away)
        {
            // Keep the graded footprint clear of rises and colliders on a shallow approach/departure.
            for (int distance = 30; distance <= 150; distance += 30)
            {
                Vector3 sample = threshold + away * distance;
                float clearance = 3f + distance * .08f;
                if (!TryGround(sample, out var ground) || ground.y > threshold.y + clearance - 2f)
                    return false;
                var center = sample + Vector3.up * clearance;
                foreach (var collider in Physics.OverlapBox(center, new Vector3(HalfWidth, 2f, 15f),
                    Quaternion.LookRotation(away), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if ((!(collider is MeshCollider tile) || !terrain.Contains(tile)) &&
                        collider.GetComponentInParent<PlaneContainer>() == null)
                        return false;
            }
            return true;
        }

        private bool TryGround(Vector3 position, out Vector3 ground)
        {
            var ray = new Ray(new Vector3(position.x, 1500f, position.z), Vector3.down);
            foreach (var tile in terrain)
            {
                if (tile == null || !tile.enabled)
                    continue;

                var bounds = tile.bounds;
                if (position.x < bounds.min.x || position.x > bounds.max.x ||
                    position.z < bounds.min.z || position.z > bounds.max.z)
                    continue;

                if (tile.Raycast(ray, out var hit, 1500f) && hit.point.y >= 3f && hit.normal.y >= 0.97f)
                {
                    ground = hit.point;
                    return true;
                }
            }

            ground = default;
            return false;
        }

        private void OnGUI()
        {
            if (!SupportedWorld || mapJobs.Visible || Event.current.type != EventType.Repaint)
                return;

            var camera = Camera.main;
            if (camera == null)
                return;

            if (!surveyFlight.Active && surveyFlight.ShowStatus)
            {
                var width = Mathf.Min(500f, Screen.width - 24f);
                DrawFlightLabel(new Rect((Screen.width - width) * 0.5f, 108f, width, 38f), surveyFlight.Status);
            }

            if (!surveyFlight.Active || !found || surveyFlight.HasWaypoints || surveyFlight.IsGearFitCheck || valley?.Verified == true)
                return;

            var near = start + Vector3.up * 0.4f;
            var far = end + Vector3.up * 0.4f;
            RunwayPreview.DrawLine(camera, near - stripOffset, far - stripOffset, Color.green);
            RunwayPreview.DrawLine(camera, near + stripOffset, far + stripOffset, Color.green);
            RunwayPreview.DrawLine(camera, near - stripOffset, near + stripOffset, Color.green);
            RunwayPreview.DrawLine(camera, far - stripOffset, far + stripOffset, Color.green);
        }

        private void DrawFlightLabel(Rect rect, string text)
        {
            if (flightLabel == null)
            {
                flightLabel = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    wordWrap = true,
                    padding = new RectOffset(12, 12, 0, 0),
                };
                flightLabel.normal.textColor = new Color(0.98f, 0.96f, 0.90f);
            }

            var content = new GUIContent(text);
            rect.height = Mathf.Max(rect.height, flightLabel.CalcHeight(content, rect.width));
            var previous = GUI.color;
            GUI.color = new Color(0.06f, 0.12f, 0.16f, 0.92f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.94f, 0.74f, 0.43f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(rect, content, flightLabel);
        }

    }
}



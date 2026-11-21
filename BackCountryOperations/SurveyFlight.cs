using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MachWorks.BackCountryOperations
{
    internal sealed class SurveyFlight
    {
        private const float HoldSeconds = 3f;
        private const float SampleInterval = 0.2f;
        private readonly ManualLogSource logger;
        private PlaneContainer plane;
        private Vector3 previousPosition;
        private float nextSample;
        private float stoppedSince = -1f;
        private bool flewInbound;
        private Stage stage;
        private string jobName;
        private Vector3[] waypoints;
        private int waypointIndex;
        private float acceptedAt;
        private CargoType cargo;
        private int cargoUnits;
        private ValleyProgress freightMilestone;
        private Vector3 cargoBay;
        private Airport pickupAirport, arrivalAirport;
        private bool unloadRequested;
        private readonly HashSet<string> completedJobs = new HashSet<string>();

        internal SurveyFlight(ManualLogSource logger)
        {
            this.logger = logger;
        }

        internal int TrackingStage => stage == Stage.Pickup ? 0 : Returning ? (waypoints?.Length ?? 0) + 2 : waypointIndex + 1;
        internal bool Returning => stage == Stage.Return;
        internal bool Active => stage == Stage.Pickup || stage == Stage.Outbound || stage == Stage.Return;
        internal Vector3 Target => Returning ? Vector3.zero : stage == Stage.Pickup ? pickupAirport?.position ?? Vector3.zero :
            IsFreight ? cargoBay : waypoints[waypointIndex];
        internal string TargetLabel => Returning ? "Base" : stage == Stage.Pickup ? PickupName :
            IsFreight ? ArrivalName : IsGearFitCheck ? "The Farm" : HasWaypoints ? $"Checkpoint {waypointIndex + 1}/{waypoints.Length}" : "Rough strip";
        internal bool IsFreight => cargo != null;
        internal bool IsGearFitCheck { get; private set; }
        internal bool HasMapTarget => Returning || HasWaypoints || IsFreight || IsGearFitCheck;
        internal Airport MapAirport => stage == Stage.Pickup ? pickupAirport : arrivalAirport;
        internal bool AwaitingPickup => stage == Stage.Pickup;
        private string PickupName => pickupAirport?.airportName ?? "Base";
        private string ArrivalName => arrivalAirport?.airportName ?? "Valley Strip";
        private bool flewToFarm;
        private bool taxiedAtFarm;
        internal bool CanUnload { get; private set; }
        internal bool CanLoad { get; private set; }
        internal bool CargoActionQueued => unloadRequested;
        internal string JobName => jobName;
        internal bool ShowStatus => Active || Time.unscaledTime < statusUntil;
        private float statusUntil;
        internal bool HasWaypoints => !IsGearFitCheck && waypoints?.Length > 0;
        internal bool IsCompleted(string name) => completedJobs.Contains(name) ||
            (name == RoughStripScout.JobNames[RoughStripScout.FreightJob] && CampaignProgress.IsDone(ValleyProgress.FirstSupply)) ||
            (name == RoughStripScout.JobNames[RoughStripScout.LoadedFreightJob] && CampaignProgress.IsDone(ValleyProgress.LoadedSupply));
        internal string Status { get; private set; } = "Select a mission on the flight map.";

        internal void Clear()
        {
            var inventory = CargoInventory.Instance;
            if (cargo && plane && inventory)
            {
                int units = inventory.GetCargoCount(cargo);
                if (units > 0)
                    inventory.RemoveCargo(cargo, units);
            }
            plane = null;
            stoppedSince = -1f;
            flewInbound = false;
            cargo = null;
            cargoUnits = 0;
            pickupAirport = arrivalAirport = null;
            freightMilestone = ValleyProgress.None;
            unloadRequested = false;
            CanUnload = false;
            CanLoad = false;
            IsGearFitCheck = false;
            flewToFarm = false;
            taxiedAtFarm = false;
            statusUntil = 0f;
            stage = Stage.Available;
            Status = "Select a mission on the flight map.";
        }

        internal void ClearCareer()
        {
            Clear();
            completedJobs.Clear();
        }

        internal void Abandon()
        {
            if (!Active)
                return;

            logger.LogInfo($"{jobName} abandoned.");
            Clear();
            Status = "Job abandoned.";
            statusUntil = Time.unscaledTime + 7f;
        }

        internal bool Accept(string name, Vector3[] route, CargoType freight = null, Vector3 bay = default,
            bool gearFitCheck = false, int freightUnits = 1, ValleyProgress milestone = ValleyProgress.None,
            Airport pickup = null, Airport arrival = null)
        {
            var currentPlane = UnityEngine.Object.FindFirstObjectByType<PlaneContainer>();
            if (currentPlane == null || !currentPlane.FlightModeInitialized)
            {
                Status = "Enter flight mode before accepting a job.";
                return false;
            }

            if (gearFitCheck && currentPlane.GetComponentInChildren<Wheel>() == null)
            {
                Status = "Fit wheeled landing gear to accept Gear Fit Check.";
                statusUntil = Time.unscaledTime + 7f;
                return false;
            }

            Clear();
            plane = currentPlane;
            jobName = name;
            cargo = freight;
            cargoUnits = freightUnits;
            freightMilestone = milestone;
            cargoBay = bay;
            pickupAirport = pickup;
            arrivalAirport = arrival;
            IsGearFitCheck = gearFitCheck;
            waypoints = route;
            waypointIndex = 0;
            previousPosition = plane.transform.position;
            stage = IsFreight ? Stage.Pickup : Stage.Outbound;
            acceptedAt = Time.time;
            nextSample = Time.time + SampleInterval;
            Status = IsFreight ? $"Park at {PickupName} and load {cargoUnits} × {cargo.GetCargoName()}."
                : IsGearFitCheck ? "Fly to The Farm, land, then taxi slowly and come to a stop."
                : HasWaypoints ? $"{jobName}. Fly through checkpoint 1 of {waypoints.Length}."
                : "Land at the marked strip and stop for three seconds.";
            logger.LogInfo($"{jobName} accepted.");
            return true;
        }

        internal void Update(Vector3 start, Vector3 end, float halfLength, float halfWidth)
        {
            if (Keyboard.current?.f4Key.wasPressedThisFrame == true)
                RequestCargoAction();

            if (!Active || Time.time < nextSample || Time.timeScale <= 0f)
                return;

            nextSample = Time.time + SampleInterval;
            if (plane == null || !plane.FlightModeInitialized || !plane.isActiveAndEnabled ||
                plane.GetComponent<PlaneController>()?.Exploded == true)
            {
                ResetFlight("aircraft changed or crashed");
                return;
            }

            var position = plane.transform.position;
            if ((position - previousPosition).sqrMagnitude > 200f * 200f)
            {
                ResetFlight("aircraft position reset");
                return;
            }

            previousPosition = position;
            var body = plane.GetComponent<Rigidbody>();
            var grounded = plane.IsGrounded();
            if (IsFreight)
            {
                UpdateFreight(position, body, grounded);
                return;
            }
            if (IsGearFitCheck)
            {
                UpdateGearFit(position, body, grounded, Time.time);
                return;
            }
            if (stage == Stage.Return && !grounded)
                flewInbound = true;

            if (stage == Stage.Outbound && HasWaypoints)
            {
                if (!grounded && (position - Target).sqrMagnitude <= 120f * 120f)
                {
                    waypointIndex++;
                    logger.LogInfo($"{jobName}: checkpoint {waypointIndex} of {waypoints.Length} verified.");
                    if (waypointIndex == waypoints.Length)
                    {
                        flewInbound = true;
                        stage = Stage.Return;
                        Status = "Route recorded. Land on Base runway and stop.";
                    }
                    else
                    {
                        Status = $"{jobName}. Fly through checkpoint {waypointIndex + 1} of {waypoints.Length}.";
                    }
                }
                return;
            }

            var center = (start + end) * 0.5f;
            var direction = end - start;
            direction.y = 0f;
            var heading = direction.normalized;
            var offset = position - center;
            var atStrip = Mathf.Abs(Vector3.Dot(offset, heading)) <= halfLength &&
                Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, heading))) <= halfWidth;
            var atBase = Mathf.Abs(position.x) <= 15f && Mathf.Abs(position.z) <= 225f;
            var atTarget = stage == Stage.Outbound ? atStrip : flewInbound && atBase;
            if (!atTarget || !grounded || body == null || body.linearVelocity.sqrMagnitude > 2f * 2f)
            {
                stoppedSince = -1f;
                return;
            }

            if (stoppedSince < 0f)
                stoppedSince = Time.time;
            if (Time.time - stoppedSince < HoldSeconds)
                return;

            stoppedSince = -1f;
            if (stage == Stage.Outbound)
            {
                stage = Stage.Return;
                Status = "Survey recorded. Fly back, land on Base runway, and stop.";
                logger.LogInfo("Rough strip survey landing verified. Return to Base.");
            }
            else
            {
                Complete("Base");
            }
        }

        private void UpdateGearFit(Vector3 position, Rigidbody body, bool grounded, float now)
        {
            if (!grounded)
                flewToFarm = true;
            var offset = position - Target;
            var atFarm = offset.x * offset.x + offset.z * offset.z <= 120f * 120f;
            if (!flewToFarm || !atFarm || !grounded || body == null)
            {
                stoppedSince = -1f;
                return;
            }

            if (body.linearVelocity.sqrMagnitude > 4f)
            {
                if (body.linearVelocity.sqrMagnitude <= 12f * 12f)
                    taxiedAtFarm = true;
                stoppedSince = -1f;
                return;
            }

            if (!taxiedAtFarm)
            {
                Status = "At The Farm. Taxi slowly before stopping for the gear check.";
                return;
            }

            Status = "At The Farm. Hold a full stop for three seconds.";
            if (stoppedSince < 0f)
                stoppedSince = now;
            if (now - stoppedSince >= HoldSeconds)
                Complete("The Farm");
        }

        private void UpdateFreight(Vector3 position, Rigidbody body, bool grounded)
        {
            CanUnload = false;
            CanLoad = false;
            var inventory = CargoInventory.Instance;
            if (!inventory)
                return;

            int cargoCount = inventory.GetCargoCount(cargo);
            bool stopped = grounded && body && body.linearVelocity.sqrMagnitude <= 4f;
            if (stage == Stage.Pickup)
            {
                var airports = AirportManager.Instance;
                var airport = pickupAirport ?? (airports ? airports.GetHomeBase() : null);
                bool atBase = airports && airport != null && (pickupAirport == null ? AtBase(position, airport.position) :
                    OpeningAirfields.AtAirport(pickupAirport, position)) &&
                    ReferenceEquals(airports.GetClosestAirport(position), airport);
                if (!atBase || !stopped)
                {
                    unloadRequested = false;
                    Status = $"Park at {PickupName} to load {cargoUnits} × {cargo.GetCargoName()}.";
                    return;
                }

                int missing = cargoUnits - cargoCount;
                if (missing > 0)
                {
                    float shortfall = Mathf.Max(0f, inventory.CurrentVolume + cargo.cargoSpace * missing -
                        Mathf.RoundToInt(inventory.MaxVolume));
                    bool cargoOpen = pickupAirport != null || airport.CargoUnlocked();
                    CanLoad = cargoOpen && shortfall <= 0f;
                    Status = !cargoOpen ? "Base cargo is still closed."
                        : CanLoad ? $"Load {missing} × {cargo.GetCargoName()} from the mission pin or press F4."
                        : $"Free {shortfall:0.#} cargo space to load the delivery.";
                    if (!CanLoad || !unloadRequested)
                    {
                        unloadRequested = false;
                        return;
                    }

                    // The briefing supplies only the missing amount. It never creates overflow stock.
                    for (int i = 0; i < missing; i++)
                        inventory.AddCargo(cargo, i == 0);
                    if (inventory.GetCargoCount(cargo) < cargoUnits)
                    {
                        unloadRequested = false;
                        Status = $"Cargo could not be loaded at {PickupName}.";
                        return;
                    }
                }

                CanLoad = false;
                unloadRequested = false;
                stage = Stage.Outbound;
                Status = $"Fly to {ArrivalName} and park to unload.";
                logger.LogInfo($"{jobName}: verified {cargoUnits} × {cargo.GetCargoName()} onboard at {PickupName}.");
                return;
            }

            if (cargoCount < cargoUnits)
            {
                stage = Stage.Pickup;
                unloadRequested = false;
                Status = $"Cargo is missing. Return to {PickupName} for {cargoUnits - cargoCount} × {cargo.GetCargoName()}.";
                return;
            }

            if (!AtArrival(position) || !stopped)
            {
                unloadRequested = false;
                Status = $"Fly to {ArrivalName} and park to unload.";
                return;
            }

            CanUnload = true;
            Status = $"Parked at {ArrivalName}. Unload from the map or press F4.";
            if (!unloadRequested)
                return;

            unloadRequested = false;
            inventory.RemoveCargo(cargo, cargoUnits);
            if (inventory.GetCargoCount(cargo) == cargoCount - cargoUnits)
            {
                CanUnload = false;
                Complete(ArrivalName);
            }
            else
                Status = "Could not unload cargo. Try again while parked.";
        }

        private bool AtArrival(Vector3 position)
        {
            if (arrivalAirport != null)
                return OpeningAirfields.AtAirport(arrivalAirport, position);
            return Mathf.Abs(position.x - cargoBay.x) <= 6f &&
                Mathf.Abs(position.z - cargoBay.z) <= 7f && Mathf.Abs(position.y - cargoBay.y) <= 5f;
        }

        private static bool AtBase(Vector3 position, Vector3 origin) =>
            Mathf.Abs(position.x - origin.x) <= 100f && Mathf.Abs(position.z - origin.z) <= 250f;

        internal bool RequestCargoAction()
        {
            if (!Active || (!CanUnload && !CanLoad) || unloadRequested)
                return false;
            unloadRequested = true;
            return true;
        }

        private void Complete(string location)
        {
            stage = Stage.Complete;
            completedJobs.Add(jobName);
            if (freightMilestone != ValleyProgress.None)
                CampaignProgress.Complete(freightMilestone);
            var seconds = Mathf.RoundToInt(Time.time - acceptedAt);
            Status = freightMilestone == ValleyProgress.FirstSupply ? "Valley supply delivered. Utility flaps unlocked."
                : freightMilestone == ValleyProgress.LoadedSupply ? "Loaded supply delivered. High-lift flaps unlocked."
                : $"{jobName} complete in {seconds / 60}:{seconds % 60:00}.";
            statusUntil = Time.unscaledTime + 10f;
            if (freightMilestone == ValleyProgress.FarmGearCheck)
            {
                Status = "Gear check passed. Balloon tires unlocked.";
                statusUntil = 0f;
                QuestAdditionUI.Instance?.DisplayText("The Farm: Gear check passed. Balloon tires are available in the workshop.",
                    MessageType.Misc, null);
            }
            logger.LogInfo($"{jobName} completed at {location} in {seconds} seconds.");
        }

        private void ResetFlight(string reason)
        {
            Clear();
            Status = "Survey interrupted. Select the mission pin to try again.";
            statusUntil = Time.unscaledTime + 7f;
            logger.LogInfo($"Rough strip survey flight reset: {reason}.");
        }

        private enum Stage
        {
            Available,
            Pickup,
            Outbound,
            Return,
            Complete,
        }
    }
}




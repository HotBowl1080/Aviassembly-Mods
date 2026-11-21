using UnityEngine;

namespace MachWorks.BackCountryOperations
{
    internal static class TownContracts
    {
        internal const string FarmCheckName = "Gear Fit Check";
        internal const string DrainageName = "Clear the Maplewood drains";
        internal const string PumpName = "Bring the worn pump home";
        internal static readonly Vector3 MaplewoodPosition = new Vector3(0f, 0f, 7000f);
        internal static readonly Vector3 SpringHillsPosition = new Vector3(-2000f, 0f, 6000f);
        internal static readonly Vector3 MaplewoodBay = new Vector3(35f, 0f, 28f);
        internal static readonly Vector3 BaseBay = new Vector3(38f, 0f, -38f);

        internal static bool Ready => OpeningAirfields.CareerOpening &&
            CampaignProgress.IsDone(ValleyProgress.WindRunwayRepair) &&
            HospitalMilestone.FindFirstDelivery(AirportManager.Instance)?.completed == true;

        internal static bool IsTownJob(int index) =>
            index == RoughStripScout.MaplewoodDrainsJob || index == RoughStripScout.PumpReturnJob;

        internal static ValleyProgress Milestone(int index) => index == RoughStripScout.MaplewoodDrainsJob
            ? ValleyProgress.MaplewoodDrains : ValleyProgress.PumpReturn;

        internal static bool IsOffered(int index) => IsTownJob(index) && Ready && !CampaignProgress.IsDone(Milestone(index)) &&
            (index == RoughStripScout.MaplewoodDrainsJob || CampaignProgress.IsDone(ValleyProgress.MaplewoodDrains));

        internal static Airport Pickup(AirportManager manager, int index)
        {
            if (!manager)
                return null;
            return index == RoughStripScout.MaplewoodDrainsJob
                ? manager.GetHomeBase() : OpeningAirfields.Find(manager, MaplewoodPosition);
        }

        internal static Airport Destination(AirportManager manager, int index)
        {
            if (!manager)
                return null;
            return index == RoughStripScout.MaplewoodDrainsJob
                ? OpeningAirfields.Find(manager, MaplewoodPosition) : manager.GetHomeBase();
        }

        internal static CargoType Cargo(AirportManager manager, int index)
        {
            if (!manager)
                return null;
            CargoType template = null;
            foreach (var stock in manager.allCargoTypes)
            {
                if (stock && stock.name == "Tools")
                {
                    template = stock;
                    break;
                }
            }
            return CampaignProgress.TownCargo(template, index == RoughStripScout.MaplewoodDrainsJob);
        }

        internal static string Reward(int index) =>
            index == RoughStripScout.MaplewoodDrainsJob ? "Utility flaps" : "High-Lift flaps";

        internal static bool HasCargo(AirportManager manager, int index)
        {
            var inventory = CargoInventory.Instance;
            var cargo = Cargo(manager, index);
            return inventory && cargo && inventory.GetCargoCount(cargo) > 0;
        }

        internal static bool Deliver(PlaneContainer plane, AirportManager manager, int index)
        {
            if (!IsOffered(index) || !plane || !plane.FlightModeInitialized || Time.timeScale <= 0f)
                return false;
            var destination = Destination(manager, index);
            var inventory = CargoInventory.Instance;
            if (destination == null || !inventory || !OpeningAirfields.AtAirport(destination, plane.transform.position))
                return false;

            var cargo = Cargo(manager, index);
            if (!cargo || inventory.GetCargoCount(cargo) != 1 ||
                !OpeningAirfields.TryRunwayPoint(destination, index == RoughStripScout.MaplewoodDrainsJob ? MaplewoodBay : BaseBay, out var bay))
                return false;

            var offset = plane.transform.position - bay;
            if (offset.x * offset.x + offset.z * offset.z > 15f * 15f || Mathf.Abs(offset.y) > 8f ||
                !plane.TryGetComponent<Rigidbody>(out var body) || body.linearVelocity.sqrMagnitude > 4f || !plane.IsGrounded())
                return false;

            inventory.RemoveCargo(cargo, 1);
            if (inventory.GetCargoCount(cargo) != 0)
                return false;
            CampaignProgress.Complete(Milestone(index));
            var feedback = QuestFeedbackManager.Instance;
            if (feedback && feedback.currentQuest is TownDeliveryMarker marker && marker.Index == index)
                marker.completed = true;
            return true;
        }

        internal static bool Track(AirportManager manager, int index)
        {
            var feedback = QuestFeedbackManager.Instance;
            if (!feedback || !HasCargo(manager, index))
                return false;
            if (feedback.currentQuest is TownDeliveryMarker current && current.Index == index)
                return true;
            var destination = Destination(manager, index);
            if (destination == null ||
                !OpeningAirfields.TryRunwayPoint(destination, index == RoughStripScout.MaplewoodDrainsJob ? MaplewoodBay : BaseBay, out var bay))
                return false;
            feedback.SelectQuest(new TownDeliveryMarker(index, destination, bay));
            return true;
        }

        internal static void CheckTracking(AirportManager manager)
        {
            var feedback = QuestFeedbackManager.Instance;
            if (feedback && feedback.currentQuest is TownDeliveryMarker marker && !marker.completed &&
                !HasCargo(manager, marker.Index))
                feedback.SelectQuest(null);
        }

        internal static void ClearTracking()
        {
            var feedback = QuestFeedbackManager.Instance;
            if (feedback && feedback.currentQuest is TownDeliveryMarker)
                feedback.SelectQuest(null);
        }

        private sealed class TownDeliveryMarker : Quest
        {
            internal readonly int Index;
            private readonly Vector3 bay;

            internal TownDeliveryMarker(int index, Airport destination, Vector3 bay)
            {
                Index = index;
                this.bay = bay;
                questID = "BCOTownCargo" + index;
                questName = RoughStripScout.JobNames[index];
                questGiver = destination;
                initialized = true;
            }

            public override Vector3 GetCurrentMarkerPosition() => bay;
            public override Vector3[] GetRelatedAirportLocations() => new[] { bay };
            public override QuestStage GetCurrentStageDescription() =>
                new QuestStage("Park in the marked delivery bay at " + questGiver.airportName + ".", 0);
            protected override bool CheckCompletion() => CampaignProgress.IsDone(Milestone(Index));
        }
    }
}

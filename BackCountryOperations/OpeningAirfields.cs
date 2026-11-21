using UnityEngine;

namespace MachWorks.BackCountryOperations
{
    internal static class OpeningAirfields
    {
        internal static readonly Vector3 HospitalPosition = new Vector3(0f, 0f, 3000f);
        internal static readonly Vector3 WindPosition = new Vector3(1000f, 0f, 5000f);
        internal static readonly Vector3 FarmPosition = new Vector3(1000f, 0f, 10000f);
        internal static readonly Vector3 ServiceOffset = new Vector3(40f, 0f, 35f);

        internal static bool IsSupported(WorldAsset world) =>
            world && world.name == "LocalizedVersion" && world.versionStartingFrom == 24;

        internal static bool CareerOpening =>
            CampaignProgress.Available && IsSupported(ContinentManager.Instance?.worldAsset) &&
            GameManager.Instance?.gameModeData is GameModeData mode && !mode.creativeMode && !mode.disableAirports;

        internal static bool Matches(Airport airport, Vector3 location) =>
            airport != null && (Vector2.Distance(new Vector2(airport.position.x, airport.position.z),
                new Vector2(location.x, location.z)) < 1f);

        internal static bool IsWindFarm(Airport airport) => Matches(airport, WindPosition);

        internal static CargoType MedicineTemplate(Airport hospital) =>
            hospital?.cargoType?.Length > 0 ? hospital.cargoType[0] : null;

        internal static CargoType RepairCargo(AirportManager airports) =>
            CampaignProgress.RepairCargo(MedicineTemplate(Find(airports, HospitalPosition)));

        internal static bool AtServiceBay(Airport wind, Vector3 position)
        {
            var offset = position - ServiceBay(wind);
            return offset.x * offset.x + offset.z * offset.z <= 22f * 22f && Mathf.Abs(offset.y) <= 8f;
        }

        internal static Airport Find(AirportManager airports, Vector3 location)
        {
            if (airports)
                foreach (var airport in airports.airports)
                    if (Matches(airport, location))
                        return airport;
            return null;
        }

        internal static bool AtAirport(Airport airport, Vector3 position)
        {
            var manager = AirportManager.Instance;
            if (!manager || manager.GetClosestAirport(position) != airport)
                return false;
            var offset = Quaternion.Inverse(manager.GetAirportRotation(airport)) * (position - airport.position);
            Vector2 size = airport.data ? airport.data.footprint : new Vector2(150f, 500f);
            return Mathf.Abs(offset.x) <= Mathf.Max(75f, size.x * .5f) &&
                Mathf.Abs(offset.z) <= Mathf.Max(300f, size.y * .5f);
        }

        internal static Vector3 ServiceBay(Airport wind)
        {
            return TryRunwayPoint(wind, ServiceOffset, out var point) ? point : wind.position;
        }

        internal static bool TryRunwayPoint(Airport airport, Vector3 runwayLocal, out Vector3 point)
        {
            point = default;
            var manager = AirportManager.Instance;
            var prefab = airport?.data?.airportPrefab;
            var runway = prefab ? prefab.GetComponentInChildren<RunwayGenerator>(true) : null;
            if (!manager || !runway)
                return false;
            var offset = prefab.transform.InverseTransformPoint(runway.transform.TransformPoint(runwayLocal));
            var generation = TerrainGenerationManager.Instance;
            var origin = airport.position;
            if (generation)
                origin.y = Mathf.Max(-8f, generation.GetTerrainHeight(origin, generation.continentFalloff));
            point = origin + manager.GetAirportRotation(airport) * offset;
            return true;
        }

        internal static bool HoldStockQuest(Quest quest)
        {
            if (!(quest is DeliveryQuest) || quest.completed || quest.randomGeneratedQuest || !CareerOpening ||
                CampaignProgress.IsDone(ValleyProgress.WindRunwayRepair))
                return false;
            if (Matches(quest.questGiver, HospitalPosition))
                return quest.questID == "6";
            return IsWindFarm(quest.questGiver) && quest.questID == "3" &&
                HospitalMilestone.FindFirstDelivery(AirportManager.Instance)?.completed != true;
        }
    }
}

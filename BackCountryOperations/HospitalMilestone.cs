using UnityEngine;

namespace MachWorks.BackCountryOperations
{
    internal static class HospitalMilestone
    {
        private const string FirstBatteryQuestId = "6";

        internal static DeliveryQuest FindFirstDelivery(AirportManager airports)
        {
            if (airports == null)
                return null;

            foreach (var airport in airports.airports)
            {
                if (airport?.data?.quests == null ||
                    (airport.position - new Vector3(0f, airport.position.y, 3000f)).sqrMagnitude > 1f)
                    continue;

                // LocalizedVersion's first authored battery delivery is native save record 6.
                foreach (var quest in airport.allQuests)
                    if (quest is DeliveryQuest delivery && !quest.randomGeneratedQuest && quest.questID == FirstBatteryQuestId)
                        return delivery;
            }

            return null;
        }
    }
}

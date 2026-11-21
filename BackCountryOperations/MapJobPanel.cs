using System;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MachWorks.BackCountryOperations
{
    internal sealed class MapJobPanel : IDisposable
    {
        private static readonly string[] Briefings =
        {
            "Fly through the inland checkpoint, then land and stop at Base.",
            "Fly through three inland checkpoints, then return to Base.",
            "Fly through three coastal checkpoints, then return to Base.",
            "Land and stop at Valley Strip, then fly back to Base.",
            "Load one crate at Base and unload at Valley Strip to unlock Utility flaps.",
            "Land at The Farm with wheeled gear, taxi slowly, then stop for three seconds. Unlocks balloon tires.",
            "Take two crates to Valley Strip to unlock High-lift flaps.",
            "Load 1 " + CampaignProgress.RepairCargoName + " at Base. Stop in the marked service bay here.",
            "Rain has washed out the parking bay. Load a Drainage Kit at Base and park in Maplewood's marked bay.",
            "The crew has pulled the broken pump. Load it at Maplewood and park in the marked bay at Base."
        };
        private static MapJobPanel current;
        private readonly Harmony harmony = new Harmony(BackCountryOperationsPlugin.PluginId + ".runwaycontracts");
        private readonly SurveyFlight flight;
        private readonly Func<int, bool> acceptJob;
        private Map map;
        private AirportMapIcon valleyPin;
        private AirportData valleyPinData;
        private Airport site, hospital, wind, home, farm, valley;
        private RectTransform rows;
        private float stockHeight, nextMapLookup;
        private bool stripAvailable, career, laterReady;
        private CargoType freightCargo;
        private Vector3 stripPosition;
        private string rendered, error;
        private int failedJob = -1;

        internal MapJobPanel(SurveyFlight flight, Func<int, bool> acceptJob)
        {
            this.flight = flight;
            this.acceptJob = acceptJob;
            current = this;
            Patch(typeof(MapInspector), nameof(MapInspector.SelectAirport), postfix: nameof(AfterInspecting));
            Patch(typeof(MapInspector), nameof(MapInspector.Disable), prefix: nameof(AllowClose));
            Patch(typeof(ContractPanel), nameof(ContractPanel.SelectAirport), prefix: nameof(HoldStockCard),
                finalizer: nameof(RestoreStockCard));
        }

        private void Patch(Type type, string method, string prefix = null, string postfix = null, string finalizer = null) =>
            harmony.Patch(AccessTools.DeclaredMethod(type, method),
                prefix == null ? null : new HarmonyMethod(typeof(MapJobPanel), prefix),
                postfix == null ? null : new HarmonyMethod(typeof(MapJobPanel), postfix),
                finalizer: finalizer == null ? null : new HarmonyMethod(typeof(MapJobPanel), finalizer));

        internal bool Visible => map && map.isActiveAndEnabled;

        internal void Update(bool stripReady, Vector3? stripCenter, bool isCareer, bool laterJobsReady, CargoType freight,
            Airport hospitalAirport, Airport windAirport)
        {
            stripAvailable = stripReady;
            freightCargo = freight;
            career = isCareer;
            laterReady = laterJobsReady;
            hospital = hospitalAirport;
            wind = windAirport;
            UpdateTracking();

            if (!Visible)
            {
                if (site != null)
                    CloseRows();
                if (Time.unscaledTime < nextMapLookup)
                    return;

                nextMapLookup = Time.unscaledTime + .35f;
                var open = Object.FindFirstObjectByType<Map>();
                if (!open || !open.inspector || !open.iconParent || !open.airportItemPrefab)
                    return;
                if (open != map)
                {
                    Reset();
                    map = open;
                }
            }

            var airports = AirportManager.Instance;
            home = airports ? airports.GetHomeBase() : null;
            farm = OpeningAirfields.Find(airports, OpeningAirfields.FarmPosition);
            stripPosition = stripCenter.GetValueOrDefault();
            if (!valleyPin && stripCenter.HasValue && RoughStripScout.ShowJob(RoughStripScout.StripJob, career, laterReady))
                BuildValleyPin();
            bool showValley = valleyPin && stripCenter.HasValue && HasOffers(valley) && map.WorldPositionIsInsideMap(stripPosition) &&
                (!map.useFogOfWar || !FogOfWar.Instance || FogOfWar.Instance.LocationDiscovered(stripPosition));
            if (valleyPin)
                valleyPin.gameObject.SetActive(showValley);
            if (showValley)
            {
                valleyPin.transform.localScale = Vector3.one * (6384f / map.zoom);
                valleyPin.transform.position = map.WorldToMapPosition(stripPosition);
            }
            if (site != null)
                RenderRows();
        }

        private void BuildValleyPin()
        {
            valleyPinData = ScriptableObject.CreateInstance<AirportData>();
            valleyPinData.hideFlags = HideFlags.DontSave;
            valley = new Airport { airportName = "Valley Strip", data = valleyPinData };
            var root = Object.Instantiate(map.airportItemPrefab, map.iconParent, false);
            root.name = "Valley Strip runway pin";
            root.SetActive(false);
            valleyPin = root.GetComponent<AirportMapIcon>();
            valleyPin.airport = valley;
            valleyPin.panel = map.inspector;
            var button = root.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => map.inspector.SelectAirport(valley));
        }

        private bool IsActive(int index) => flight.Active && flight.JobName == RoughStripScout.JobNames[index];

        private bool Offered(int index) => IsActive(index) ||
            (RoughStripScout.ShowJob(index, career, laterReady) &&
                (!RoughStripScout.IsFreightJob(index) || !flight.IsCompleted(RoughStripScout.JobNames[index])));

        private Airport Host(int index)
        {
            if (IsActive(index) && flight.MapAirport != null)
                return flight.MapAirport;
            if (RoughStripScout.IsFreightJob(index))
                return IsActive(index) && flight.AwaitingPickup ? home : valley;
            switch (index)
            {
                case RoughStripScout.StripJob: return valley;
                case RoughStripScout.GearFitJob: return farm;
                case RoughStripScout.RepairJob: return wind;
                default: return home;
            }
        }

        private bool HasOffers(Airport airport)
        {
            for (int i = 0; i < Briefings.Length; i++)
                if (MatchesHost(i, airport) && Offered(i))
                    return true;
            return false;
        }

        private bool MatchesHost(int index, Airport airport)
        {
            if (airport == null)
                return false;
            if (!TownContracts.IsTownJob(index))
                return Host(index) == airport;
            var manager = AirportManager.Instance;
            return airport == TownContracts.Pickup(manager, index) || airport == TownContracts.Destination(manager, index);
        }

        private string Blocked(int index)
        {
            if (index == RoughStripScout.StripJob && !stripAvailable)
                return "Awaiting runway clearance.";
            if (RoughStripScout.IsFreightJob(index) && !freightCargo)
                return stripAvailable ? "The cargo bay is still being surveyed." : "Awaiting runway clearance.";
            if (index == RoughStripScout.RepairJob && (hospital == null || wind == null))
                return "Waiting for airport data.";
            return null;
        }

        private string Describe(int index)
        {
            if (index == failedJob)
                return error;
            if (IsActive(index))
                return flight.CargoActionQueued ? "Close the map to finish the transfer." : flight.Status;
            string description = Blocked(index) ?? Briefings[index];
            if (TownContracts.IsTownJob(index))
            {
                var cargo = TownContracts.Cargo(AirportManager.Instance, index);
                if (cargo)
                    description += $" {cargo.weight:0.##} weight · {cargo.cargoSpace} cargo space.";
                return description + " Unlocks " + TownContracts.Reward(index) + ".";
            }
            if (index == RoughStripScout.RepairJob)
            {
                var source = OpeningAirfields.MedicineTemplate(hospital);
                if (source)
                {
                    var cargo = CampaignProgress.RepairCargo(source);
                    if (cargo)
                        description += $" {cargo.weight:0.#} weight · {cargo.cargoSpace} cargo space.";
                }
            }
            return description;
        }

        private string ActionFor(int index)
        {
            if (index == RoughStripScout.RepairJob)
                return "";
            if (TownContracts.IsTownJob(index))
                return TownContracts.HasCargo(AirportManager.Instance, index) ? "Track" : "";
            if (IsActive(index))
                return flight.CanLoad ? "Load cargo" : flight.CanUnload ? "Unload cargo" : "Track";
            return flight.Active || Blocked(index) != null ? "" : "Start";
        }

        private static void AfterInspecting(MapInspector __instance, Airport __0)
        {
            var panel = current;
            if (panel == null || !panel.Visible || __instance != panel.map.inspector)
                return;
            if (panel.site != __0)
            {
                panel.failedJob = -1;
                panel.error = null;
            }
            panel.site = __0;
            var rect = (RectTransform)__instance.contractPanel.transform;
            panel.stockHeight = rect.sizeDelta.y;
            panel.rendered = null;
            panel.RenderRows();
        }

        // The native airport card slides away on pointer exit. Keep a runway with offers open
        // so the pointer can travel from its pin to the contract buttons.
        private static bool AllowClose(MapInspector __instance) =>
            current == null || !current.Visible || __instance != current.map.inspector ||
            current.site == null || !current.HasOffers(current.site);

        private static void HoldStockCard(Airport __0, out Quest __state)
        {
            __state = __0?.currentQuest;
            if (!OpeningAirfields.HoldStockQuest(__state))
                __state = null;
            else
                __0.currentQuest = null;
        }

        private static void RestoreStockCard(Airport __0, Quest __state)
        {
            if (__state != null)
                __0.currentQuest = __state;
        }

        private void RenderRows()
        {
            var contract = map.inspector.contractPanel;
            var rect = (RectTransform)contract.transform;
            var signature = new StringBuilder();
            for (int i = 0; i < Briefings.Length; i++)
                if (MatchesHost(i, site) && Offered(i))
                    signature.Append(i).Append(Describe(i)).Append(ActionFor(i)).Append('\n');
            var value = signature.ToString();
            if (value == rendered)
                return;

            rendered = value;
            if (rows)
            {
                rows.gameObject.SetActive(false);
                Object.Destroy(rows.gameObject);
            }
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, stockHeight);
            if (value.Length == 0)
                return;

            float top = Mathf.Max(site.cargoType?.Length > 0 ? 114f : 62f, stockHeight - 8f);
            rows = Rect("Runway contracts", rect, 16f, top, rect.sizeDelta.x - 32f, 1f);
            rows.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            float height = 0f;
            for (int i = 0; i < Briefings.Length; i++)
            {
                if (!MatchesHost(i, site) || !Offered(i))
                    continue;
                int index = i;
                var line = (RectTransform)Object.Instantiate(contract.childQuestPrefab, rows, false).transform;
                line.name = RoughStripScout.JobNames[i];
                WorkshopSettingsUi.Place(line, 0f, height, rows.sizeDelta.x, 82f);
                var native = line.GetComponent<QuestChildUI>();
                native.enabled = false;
                native.checkbox.SetActive(false);
                native.reward.gameObject.SetActive(false);
                foreach (var image in line.GetComponentsInChildren<RawImage>(true))
                    if (image != native.icon)
                        image.gameObject.SetActive(false);

                var icon = ContractIcon(i);
                native.icon.texture = icon;
                native.icon.gameObject.SetActive(icon);
                native.icon.raycastTarget = false;
                float left = icon ? 72f : 14f;
                float width = line.sizeDelta.x - left - 14f;
                native.header.text = i == RoughStripScout.RepairJob ? "Repair the runway" : line.name;
                native.header.alignment = TextAlignmentOptions.MidlineLeft;
                native.header.enableAutoSizing = true;
                native.header.fontSizeMin = 16f;
                native.header.fontSizeMax = native.header.fontSize;
                WorkshopSettingsUi.Place(native.header.rectTransform, left, 6f, width, 34f);
                var description = Text(contract.description, line, Describe(i), left, 40f, width, 42f, 20f);
                float body = description.GetPreferredValues(description.text, width, 0f).y;
                description.rectTransform.sizeDelta = new Vector2(width, body);
                string action = ActionFor(i);
                if (action.Length > 0)
                {
                    var button = line.gameObject.AddComponent<Button>();
                    button.targetGraphic = line.GetComponent<Image>();
                    button.onClick.AddListener(() => Select(index));
                    Text(contract.header, line, action, left, 44f + body, width, 22f, 18f)
                        .alignment = TextAlignmentOptions.Right;
                }
                float rowHeight = Mathf.Max(82f, 51f + body + (action.Length > 0 ? 25f : 0f));
                line.sizeDelta = new Vector2(line.sizeDelta.x, rowHeight);
                height += rowHeight + 10f;
            }
            rows.sizeDelta = new Vector2(rows.sizeDelta.x, height);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, top + height + 12f);
        }

        private Texture ContractIcon(int index)
        {
            if (index == RoughStripScout.RepairJob)
                return OpeningAirfields.RepairCargo(AirportManager.Instance)?.icon;
            if (TownContracts.IsTownJob(index))
                return TownContracts.Cargo(AirportManager.Instance, index)?.icon;
            return RoughStripScout.IsFreightJob(index) ? freightCargo?.icon : null;
        }

        private static RectTransform Rect(string name, Transform parent, float left, float top, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            WorkshopSettingsUi.Place(rect, left, top, width, height);
            return rect;
        }

        private static TMP_Text Text(TMP_Text native, Transform parent, string message, float left, float top,
            float width, float height, float size)
        {
            var rect = Rect("Contract text", parent, left, top, width, height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = native.font;
            text.fontSharedMaterial = native.fontSharedMaterial;
            text.color = native.color;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.text = message;
            text.raycastTarget = false;
            return text;
        }

        private void Select(int index)
        {
            if (!Offered(index))
                return;
            if (TownContracts.IsTownJob(index))
            {
                TownContracts.Track(AirportManager.Instance, index);
                return;
            }

            failedJob = -1;
            if (IsActive(index))
                flight.RequestCargoAction();
            else if (!flight.Active && Blocked(index) == null && !acceptJob(index))
            {
                failedJob = index;
                error = flight.Status;
                RenderRows();
                return;
            }

            var feedback = QuestFeedbackManager.Instance;
            if (IsActive(index) && feedback &&
                (!(feedback.currentQuest is BushMapQuest active) || active.Index != index))
                feedback.SelectQuest(new BushMapQuest(this, index, site));
            RenderRows();
        }

        private void UpdateTracking()
        {
            var feedback = QuestFeedbackManager.Instance;
            if (feedback && feedback.currentQuest is BushMapQuest quest && !IsActive(quest.Index))
            {
                quest.completed = flight.IsCompleted(quest.questName);
                if (!quest.completed)
                    feedback.SelectQuest(quest);
            }
        }

        private void CloseRows()
        {
            var inspector = map ? map.inspector : null;
            site = null;
            failedJob = -1;
            rendered = null;
            if (rows)
            {
                if (inspector)
                {
                    var rect = (RectTransform)inspector.contractPanel.transform;
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, stockHeight);
                }
                rows.gameObject.SetActive(false);
                Object.Destroy(rows.gameObject);
                rows = null;
            }
            if (inspector)
                inspector.Disable();
        }

        internal void Reset()
        {
            CloseRows();
            var feedback = QuestFeedbackManager.Instance;
            if (feedback && feedback.currentQuest is BushMapQuest quest)
                feedback.SelectQuest(quest);
            if (valleyPin)
                Object.Destroy(valleyPin.gameObject);
            if (valleyPinData)
                Object.Destroy(valleyPinData);
            map = null;
        }

        public void Dispose()
        {
            Reset();
            harmony.UnpatchSelf();
            if (current == this)
                current = null;
        }

        private sealed class BushMapQuest : Quest
        {
            private readonly MapJobPanel panel;
            internal readonly int Index;

            internal BushMapQuest(MapJobPanel panel, int index, Airport site)
            {
                this.panel = panel;
                Index = index;
                questGiver = site;
                questID = "BackCountryMap" + index;
                questName = RoughStripScout.JobNames[index];
                initialized = true;
            }

            public override Vector3 GetCurrentMarkerPosition() => panel.flight.HasMapTarget
                ? panel.flight.Target : panel.stripPosition;
            public override Vector3[] GetRelatedAirportLocations() => new[] { GetCurrentMarkerPosition() };
            public override QuestStage GetCurrentStageDescription() => new QuestStage(panel.flight.Status, panel.flight.TrackingStage);
            protected override bool CheckCompletion() => panel.flight.IsCompleted(questName);
        }
    }
}

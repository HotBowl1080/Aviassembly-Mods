using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MachWorks.BackCountryOperations
{
    internal enum BushGearLength
    {
        Standard,
        Short,
        Extended
    }

    internal sealed class BushWheelPart : MonoBehaviour
    {
        [SerializeField] private BuildingPart part;
        [SerializeField] private Wheel wheel;
        [SerializeField] private WheelVisual visual;
        [SerializeField] private Vector3 axle;
        [SerializeField] private string id;
        [SerializeField] private Transform spin, hub, cap, collar, bump, caster;
        [SerializeField] private MeshFilter casing;
        [SerializeField] private Mesh smallMesh, largeMesh;
        [SerializeField] private MeshCollider stopCollider;
        [SerializeField] private Mesh smallStop, largeStop;
        [SerializeField] private BoxCollider selection;
        [SerializeField] private float springPerUnit, damping, traction, rollingFriction, wheelWeight, frameWeight;
        [SerializeField] private bool shocks;
        [SerializeField] private bool large, tailwheel, steerable;
        [SerializeField] private BushGearLength strutLength;
        [SerializeField] private int customLength;
        [SerializeField] private Transform strutFrame;
        private bool inFlight;
        private bool legacyFitted;
        private Quaternion neutralRotation;
        private float steeringAngle, lastGroundContact;
        private float tireSquash, targetSquash;

        private static readonly AccessTools.FieldRef<Wheel, Vector3> WheelHome =
            AccessTools.FieldRefAccess<Wheel, Vector3>("localPosition");
        private static readonly AccessTools.FieldRef<Wheel, float> PreviousContact =
            AccessTools.FieldRefAccess<Wheel, float>("prevHitDistance");

        internal BuildingPart Part => part;
        internal string PartId => id;
        internal bool Large => large;
        internal bool Steerable => steerable;
        internal bool IsTailwheel => tailwheel;
        internal BushGearLength StrutLength => strutLength;
        internal bool SupportsLength => strutFrame;
        internal bool LegacyFitted => HasLegacyTire();
        internal float Travel => tailwheel ? BushLandingGear.TailwheelTravel : shocks ? .32f : BushLandingGear.Travel;
        private float TireRadius => tailwheel ? .145f : large ? .48f : .32f;
        private float TireWidth => tailwheel ? .065f : large ? .218f : .109f;
        internal float LengthScale => customLength > 0 ? customLength / 100f : strutLength == BushGearLength.Short ? .65f :
            strutLength == BushGearLength.Extended ? 1.12f : 1f;
        internal int LengthPercent => Mathf.RoundToInt(LengthScale * 100f);
        internal Vector3 LengthHandlePosition => part.transform.TransformPoint(new Vector3(axle.x, axle.y * LengthScale - .16f, axle.z));
        internal Vector3 LengthTravel => part.transform.TransformVector(Vector3.up * axle.y);
        internal Vector3 SuspensionOffset =>
            Vector3.up * ((visual.lowerSuspension.localPosition.y + Travel / 2f) / LengthScale);

        internal void Configure(BuildingPart buildingPart, Wheel nativeWheel, WheelVisual wheelVisual,
            Vector3 axlePosition, string partId, Transform axleSpin, MeshFilter tire, Transform wheelHub,
            Transform hubCap, Transform innerCollar, BoxCollider workshopCollider, Transform bottomOut,
            Mesh small, Mesh big, MeshCollider stop, Mesh cylinderStop, Mesh balloonStop,
            Wheel stock, bool shocks, Transform caster)
        {
            part = buildingPart;
            wheel = nativeWheel;
            visual = wheelVisual;
            axle = axlePosition;
            id = partId;
            spin = axleSpin;
            casing = tire;
            hub = wheelHub;
            cap = hubCap;
            collar = innerCollar;
            selection = workshopCollider;
            bump = bottomOut;
            smallMesh = small;
            largeMesh = big;
            stopCollider = stop;
            smallStop = cylinderStop;
            largeStop = balloonStop;
            this.caster = caster;
            tailwheel = caster;
            this.shocks = shocks;
            steerable = tailwheel;
            wheelWeight = stock.weight;
            frameWeight = id == null ? 0f : tailwheel ? .055f : shocks ? .20f : .16f;
            // Convert stock force per compression fraction into force per unit of actual travel.
            float stockSpring = stock.suspensionStrenth / Mathf.Max(stock.suspentionLength, .05f);
            springPerUnit = tailwheel ? stockSpring :
                Mathf.Max(stockSpring, stock.suspensionStrenth * .65f / Travel) * (shocks ? 1.18f : 1f);
            damping = stock.suspensionDamping * (tailwheel ? 1f : shocks ? 1.55f : 1.25f);
            traction = stock.traction;
            rollingFriction = stock.maxFriction;
        }

        private void Awake()
        {
            neutralRotation = transform.localRotation;
        }

        internal void ConfigureStrut(Transform frame) => strutFrame = frame;

        internal static string PrefabId(string id, bool tailwheel, bool large, bool steerable,
            BushGearLength length)
        {
            string steeringSuffix = tailwheel
                ? (steerable ? "" : BushLandingGear.FixedSuffix)
                : (steerable ? BushLandingGear.SteeringSuffix : "");
            string lengthSuffix = length == BushGearLength.Standard ? "" : "_" + length;
            return id + (large ? BushLandingGear.LargeSuffix : "") + steeringSuffix + lengthSuffix;
        }

        internal void SetLarge(bool useLarge)
        {
            large = !tailwheel && useLarge;
            ApplyConfiguration();
        }

        internal void SetSteerable(bool enabled)
        {
            if (string.IsNullOrEmpty(id) || enabled == steerable)
                return;

            steerable = enabled;
            ApplyConfiguration();
        }

        internal void SetLength(BushGearLength length)
        {
            if (!SupportsLength || strutLength == length)
                return;
            strutLength = length;
            customLength = 0;
            ApplyConfiguration();
        }

        internal void SetLengthPercent(int percent)
        {
            if (!SupportsLength || LegacyFitted || percent == LengthPercent)
                return;
            customLength = Mathf.Clamp(percent, 65, 112);
            strutLength = BushGearLength.Standard;
            ApplyConfiguration();
        }

        private void ApplyConfiguration()
        {
            float radius = TireRadius;
            float width = TireWidth;
            float rimRadius = radius * (large ? .35f : .44f);
            // Spacing at the main axle clears the frame when the larger tire swings inboard.
            float steeringClearance = steerable && !tailwheel ? .05f : 0f;
            float center = tailwheel ? 0f : width + .045f + steeringClearance;
            casing.sharedMesh = large ? largeMesh : smallMesh;
            casing.transform.SetParent(large ? spin.parent : spin, false);
            casing.transform.localRotation = large ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            SetTireSquash(0f);
            hub.localScale = new Vector3(rimRadius * 2.04f, rimRadius * 2.04f, width * 1.61f);
            cap.localPosition = Vector3.forward * width * .83f;
            cap.localScale = new Vector3(rimRadius * 1.06f, rimRadius * 1.06f, width * .16f);
            collar.localPosition = Vector3.back * (tailwheel ? width * .83f : (width + .035f + steeringClearance) / 2f);
            collar.localScale = tailwheel ? cap.localScale :
                new Vector3(rimRadius, rimRadius, width + .10f + steeringClearance);

            if (strutFrame)
                strutFrame.localScale = new Vector3(1f, LengthScale, 1f);
            transform.localPosition = new Vector3(axle.x + center, axle.y * LengthScale + Travel / 2f, axle.z);
            WheelHome(wheel) = transform.localPosition;
            visual.lowerSuspensionLength = radius;
            visual.upperSuspensionLength = Travel;
            visual.ResetSuspension();
            selection.transform.localPosition = Vector3.down * Travel / 2f;
            selection.size = new Vector3(width * 1.82f, radius * 1.65f, radius * 1.65f);

            // The collision cylinder fits just inside the tire at the top of its stroke.
            stopCollider.sharedMesh = large ? largeStop : smallStop;
            bump.localScale = new Vector3(radius * 1.98f, radius * 1.98f, width * (large ? 1.92f : 1.75f));
            bump.localPosition = Vector3.zero;
            wheel.suspentionLength = radius + Travel;
            wheel.suspensionStrenth = springPerUnit * wheel.suspentionLength;
            wheel.suspensionDamping = damping;
            wheel.weight = frameWeight + wheelWeight * (large ? 1.15f : .65f);

            if (!string.IsNullOrEmpty(id))
                part.gameObject.name = PrefabId(id, tailwheel, large, steerable, strutLength) +
                    (customLength > 0 && customLength != 100 ? "_Length" + customLength : "");
        }

        internal void PrepareForFlight()
        {
            inFlight = true;
            lastGroundContact = float.NegativeInfinity;
            PreviousContact(wheel) = wheel.suspentionLength;
            // Wheel.Awake gives stock stops a fixed offset. The custom stop is centered on our full bump position.
            bump.localPosition = Vector3.zero;
            RefreshLegacy(HasLegacyTire(), true);
        }

        internal void ResetForEditor()
        {
            inFlight = false;
            ResetSteering();
            targetSquash = 0f;
            SetTireSquash(0f);
            RefreshLegacy(HasLegacyTire(), true);
        }

        private void ResetSteering()
        {
            if (steeringAngle == 0f)
                return;
            if (tailwheel)
                caster.localRotation = Quaternion.identity;
            else
                transform.localRotation = neutralRotation;
            steeringAngle = 0f;
        }

        internal void UpdateGroundHandling()
        {
            if (!inFlight || legacyFitted || !wheel.rb)
                return;

            var up = transform.TransformVector(Vector3.up).normalized;
            bool grounded = CastContact(transform.position + up * wheel.rayUpOffset, -up, out var hit,
                wheel.suspentionLength + wheel.rayUpOffset, wheel.layerMask) && hit.rigidbody != wheel.rb;
            var plane = PlaneContainer.Instance;
            wheel.suspensionStrenth = springPerUnit * wheel.suspentionLength;
            wheel.suspensionDamping = damping;
            wheel.traction = traction;
            wheel.maxFriction = rollingFriction;
            targetSquash = 0f;
            float springForce = 0f;
            if (grounded && plane)
            {
                float distance = Mathf.Clamp(hit.distance - wheel.rayUpOffset, 0f, wheel.suspentionLength);
                float compression = wheel.suspentionLength - distance;
                float bumpProgress = Mathf.InverseLerp(Travel * .58f, Travel, compression);
                wheel.suspensionStrenth *= 1f + (tailwheel ? .35f : .65f) * bumpProgress * bumpProgress;
                springForce = wheel.suspensionStrenth * plane.GravityMultiplier * compression / wheel.suspentionLength;
                // A returning contact or a change of tread probe must not create a one-frame damping spike.
                var pointVelocity = wheel.rb.GetPointVelocity(transform.position);
                float normalSpeed = Vector3.Dot(pointVelocity, hit.normal) / Mathf.Max(.4f, Vector3.Dot(up, hit.normal));
                float reboundSpeed = Mathf.Clamp(normalSpeed, tailwheel ? -3f : -7f, 7f);
                PreviousContact(wheel) = distance - reboundSpeed * Time.fixedDeltaTime;
                if (reboundSpeed > 0f)
                    wheel.suspensionDamping = Mathf.Min(damping, springForce * .88f / reboundSpeed);
                if (large)
                    targetSquash = .065f * Mathf.InverseLerp(.38f, .98f, compression / Travel);
            }

            if (!steerable)
                return;

            var input = InputManager.Instance;
            float yaw = input && !input.currentInputBlocker ? Mathf.Clamp(input.yawInput, -1f, 1f) : 0f;
            if (tailwheel)
            {
                SteerTailwheel(plane, grounded, hit.normal, springForce, yaw);
                return;
            }

            float target = 0f;
            if (grounded && plane)
            {
                float speed = Vector3.ProjectOnPlane(wheel.rb.linearVelocity, hit.normal).magnitude;
                float speedFactor = Mathf.Lerp(1f, .17f, Mathf.InverseLerp(7f, 28f, speed));
                float arm = Vector3.Dot(transform.position - wheel.rb.worldCenterOfMass, plane.Forward);
                // A wheel behind the center of mass steers opposite a front wheel for the same yaw.
                float direction = arm < -.025f ? -1f : 1f;
                target = yaw * (large ? 13f : 16f) * speedFactor * direction;
            }

            float next = Mathf.MoveTowards(steeringAngle, target, 150f * Time.fixedDeltaTime);
            if (next == steeringAngle)
                return;
            steeringAngle = next;
            var neutralWorld = transform.parent.rotation * neutralRotation;
            transform.rotation = Quaternion.AngleAxis(steeringAngle, up) * neutralWorld;
        }

        private void SteerTailwheel(PlaneContainer plane, bool grounded, Vector3 groundNormal, float support, float yaw)
        {
            float target = steeringAngle;
            if (grounded && plane)
            {
                lastGroundContact = Time.fixedTime;
                var forward = Vector3.ProjectOnPlane(plane.Forward, groundNormal).normalized;
                var side = Vector3.Cross(groundNormal, forward);
                var velocity = wheel.rb.GetPointVelocity(transform.position);
                float forwardSpeed = Vector3.Dot(velocity, forward);
                float speed = Mathf.Abs(Vector3.Dot(wheel.rb.linearVelocity, forward));
                float arm = Vector3.Dot(transform.position - wheel.rb.worldCenterOfMass, forward);
                float direction = arm < 0f ? -1f : 1f;
                float crawlAngle = Mathf.Lerp(85f, 90f, Mathf.InverseLerp(.05f, .45f, speed));
                float taxiAngle = Mathf.Lerp(crawlAngle, 32f, Mathf.InverseLerp(2f, 7f, speed));
                target = yaw * direction * taxiAngle * Mathf.Lerp(1f, .17f, Mathf.InverseLerp(7f, 28f, speed));

                // Let the stock wheel carry vertical load. A bounded side force steers the tail without
                // making the nearly sideways tire oppose the plane's entire forward velocity.
                wheel.traction = 0f;
                wheel.maxFriction = 0f;
                float thrust = Mathf.Max(0f, Vector3.Dot(plane.GetTotalThrust(), forward));
                float rolling = Mathf.Clamp01(speed * 1.5f + thrust / Mathf.Max(.08f, support));
                float commanded = Mathf.Sin(steeringAngle * Mathf.Deg2Rad) * (1.7f + .45f * Mathf.Min(speed, 6f)) * rolling;
                float sideways = Mathf.Clamp((commanded - Vector3.Dot(velocity, side)) * 1.25f, -.7f, .7f);
                float drag = Mathf.Clamp(forwardSpeed * .05f, -.14f, .14f);
                wheel.rb.AddForceAtPosition((side * sideways - forward * drag) * support, transform.position);
            }
            else if (Time.fixedTime - lastGroundContact > .15f)
                target = 0f;

            steeringAngle = Mathf.MoveTowards(steeringAngle, target, 170f * Time.fixedDeltaTime);
            caster.localRotation = Quaternion.AngleAxis(steeringAngle, Vector3.up);
        }

        internal bool CastContact(Vector3 origin, Vector3 direction, out RaycastHit hit, float range, int layers)
        {
            bool found = Physics.Raycast(origin, direction, out hit, range, layers);
            if (!large)
                return found;

            // The shoulders are slightly smaller than the center tread. Keep native wheel forces and visuals
            // on the axle centerline while letting the whole tread find the terrain.
            const float shoulderDrop = .012f;
            var shoulderOffset = transform.right * (TireWidth * .64f);
            foreach (int side in ContactSides)
            {
                if (!Physics.Raycast(origin + shoulderOffset * side, direction, out var candidate, range - shoulderDrop, layers)
                    || candidate.rigidbody == wheel.rb)
                    continue;
                float distance = candidate.distance + shoulderDrop;
                if (found && distance >= hit.distance)
                    continue;
                candidate.distance = distance;
                candidate.point = origin + direction * distance;
                hit = candidate;
                found = true;
            }
            return found;
        }

        private static readonly int[] ContactSides = { -1, 1 };

        private void SetTireSquash(float amount)
        {
            tireSquash = amount;
            // Wheel.Awake can reset the editor visual before this component's Awake runs.
            float radius = TireRadius;
            casing.transform.localScale = new Vector3(radius, radius * (1f - amount), TireWidth * (1f + amount * .75f));
            casing.transform.localPosition = Vector3.down * (radius * amount);
        }

        internal void Roll(float forwardSpeed)
        {
            // Both mirrored and normal parts share local forward and effective up.
            if (wheel.rb)
                forwardSpeed = Vector3.Dot(wheel.rb.GetPointVelocity(transform.position), tailwheel ? caster.forward : transform.forward);
            var angle = Mathf.Rad2Deg * forwardSpeed * Time.fixedDeltaTime / TireRadius;
            spin.Rotate(Vector3.forward, angle, Space.Self);
        }

        private bool HasLegacyTire()
        {
            if (string.IsNullOrEmpty(id))
                return false;
            foreach (var child in part.children)
            {
                if (child && (child.name.StartsWith("BCOSmallBushTire", System.StringComparison.Ordinal) ||
                    child.name.StartsWith("BCOLargeBushTire", System.StringComparison.Ordinal)))
                    return true;
            }
            return false;
        }

        private void LateUpdate()
        {
            RefreshLegacy(HasLegacyTire(), false);
            if (large && !Mathf.Approximately(tireSquash, targetSquash))
                SetTireSquash(Mathf.MoveTowards(tireSquash, targetSquash, .40f * Time.deltaTime));
        }

        private void RefreshLegacy(bool fitted, bool force)
        {
            if (!force && fitted == legacyFitted)
                return;
            legacyFitted = fitted;
            visual.lowerSuspension.gameObject.SetActive(!fitted);
            selection.gameObject.SetActive(!inFlight && !fitted);
            bump.gameObject.SetActive(inFlight && !fitted);
            wheel.enabled = inFlight && !fitted;
            wheel.weight = fitted ? frameWeight : frameWeight + wheelWeight * (large ? 1.15f : .65f);
        }
    }

    internal sealed class BushShockStrut : MonoBehaviour
    {
        [SerializeField] private BushWheelPart wheel;
        [SerializeField] private Vector3 brace, knuckle, front, rear;
        [SerializeField] private Transform body, shaft, mainLeg, trailingLeg, pin;
        private Vector3? previousEnd;

        internal void Configure(BushWheelPart tire, Vector3 inboard, Vector3 outboard, Vector3 frontMount,
            Vector3 rearMount, Transform shockBody, Transform shockRod, Transform leading, Transform trailing,
            Transform spindle)
        {
            wheel = tire;
            brace = inboard;
            knuckle = outboard;
            front = frontMount;
            rear = rearMount;
            body = shockBody;
            shaft = shockRod;
            mainLeg = leading;
            trailingLeg = trailing;
            pin = spindle;
        }

        private void LateUpdate()
        {
            var end = knuckle + wheel.SuspensionOffset;
            if (previousEnd == end)
                return;
            previousEnd = end;
            var direction = (end - brace).normalized;
            var joint = brace + direction * (Vector3.Distance(brace, knuckle) * .50f);
            BushLandingGear.PositionRod(body, brace, joint);
            BushLandingGear.PositionRod(shaft, joint - direction * .045f, end);
            BushLandingGear.PositionRod(mainLeg, front, end);
            BushLandingGear.PositionRod(trailingLeg, rear, end);
            BushLandingGear.PositionRod(pin, end - Vector3.right * .04f, end + Vector3.right * .145f);
        }
    }

    internal sealed class BushTireSettings : PartSettingsBase
    {
        private static readonly AccessTools.FieldRef<PartContainer, System.Collections.Generic.List<BuildingPart>>
            ContainerParts = AccessTools.FieldRefAccess<PartContainer, System.Collections.Generic.List<BuildingPart>>("parts");
        private BushWheelPart wheel;
        private Toggle tires, steering;
        private TMP_Text status, lengthReadout;
        internal PartPlacer Placer { get; set; }

        internal static GameObject CreateTemplate(Transform storage)
        {
            var template = new GameObject("Bush gear settings", typeof(RectTransform), typeof(BushTireSettings));
            template.transform.SetParent(storage, false);
            return template;
        }

        public override void SelectPart(PlanePart part)
        {
            wheel = part.GetComponent<BushWheelPart>();
            float top = 3f;
            if (!wheel.IsTailwheel)
            {
                tires = WorkshopSettingsUi.Checkbox(transform, "Balloon tires", top, SetTire);
                top += WorkshopSettingsUi.RowHeight;
            }
            steering = WorkshopSettingsUi.Checkbox(transform, "Rudder steering", top, SetSteering);
            top += WorkshopSettingsUi.RowHeight;
            if (wheel.SupportsLength)
            {
                lengthReadout = WorkshopSettingsUi.Label(transform, "", top, 25f, detail: true);
                top += 25f;
            }
            status = WorkshopSettingsUi.Label(transform, "", top, 46f, detail: true);
            UpdateLabels();
        }

        private void SetTire(bool large)
        {
            var matching = FindMatchingWheel();
            if (!CanChangeSettings(matching) || (large && !CampaignProgress.CanFitBalloon))
                return;
            wheel.SetLarge(large);
            if (matching)
                matching.SetLarge(large);
            SettingsChanged();
        }

        private void SetSteering(bool enabled)
        {
            var matching = FindMatchingWheel();
            if (!CanChangeSettings(matching))
                return;


            wheel.SetSteerable(enabled);
            if (matching)
                matching.SetSteerable(enabled);
            SettingsChanged();
        }

        private BushWheelPart FindMatchingWheel() => FindMatchingWheel(Placer, wheel);

        internal static BushWheelPart FindMatchingWheel(PartPlacer Placer, BushWheelPart wheel)
        {
            if (!Placer || !PartPlacer.mirrorMode || wheel.IsTailwheel || Placer.partContainer == null || Placer.symmetryPlane == null)
                return null;

            var symmetry = Placer.symmetryPlane;
            var expectedPosition = symmetry.GetMirroredPosition(wheel.Part.transform.position);
            bool rightSide = symmetry.DistanceFromPlane(wheel.Part.transform.position) > 0f;
            float nearest = .12f * .12f;
            BushWheelPart matching = null;
            foreach (var part in ContainerParts(Placer.partContainer))
            {
                if (!part || part == wheel.Part ||
                    (symmetry.DistanceFromPlane(part.transform.position) > 0f) == rightSide)
                    continue;

                float distance = (part.transform.position - expectedPosition).sqrMagnitude;
                if (distance >= nearest)
                    continue;

                var candidate = part.GetComponentInChildren<BushWheelPart>();
                if (candidate && candidate.PartId == wheel.PartId)
                {
                    matching = candidate;
                    nearest = distance;
                }
            }
            return matching;
        }

        private bool CanChangeSettings(BushWheelPart matching)
        {
            return !wheel.LegacyFitted && (!matching || !matching.LegacyFitted);
        }

        private void SettingsChanged()
        {
            var storage = PlaneStorage.Instance;
            if (storage)
                storage.UpdateHistory();
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            bool editable = CanChangeSettings(FindMatchingWheel());
            bool balloonAvailable = CampaignProgress.CanFitBalloon;
            if (tires)
                WorkshopSettingsUi.SetCheckbox(tires, wheel.Large, editable && (wheel.Large || balloonAvailable));
            WorkshopSettingsUi.SetCheckbox(steering, wheel.Steerable, editable);
            status.text = !editable ? "Remove the attached tire to change settings." :
                tires && !wheel.Large && !balloonAvailable ? "Complete Gear Fit Check at The Farm to fit balloon tires." : "";
            float height = -status.rectTransform.anchoredPosition.y + (status.text.Length == 0 ? 0f : 46f);
            ((RectTransform)transform).sizeDelta = new Vector2(WorkshopSettingsUi.Width, height);
        }

        private void LateUpdate()
        {
            if (wheel && lengthReadout)
                lengthReadout.text = $"Strut length  {wheel.LengthPercent}%";
        }

        internal void RefreshMirrorSettings() => UpdateLabels();
    }
}


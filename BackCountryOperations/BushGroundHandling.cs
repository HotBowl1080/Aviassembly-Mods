using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MachWorks.BackCountryOperations
{
    internal static class BushGroundHandling
    {
        private static readonly AccessTools.FieldRef<PlaneContainer, List<PlanePart>> PlaneParts =
            AccessTools.FieldRefAccess<PlaneContainer, List<PlanePart>>("planeParts");
        private static readonly Action<PlaneController, Vector3> ApplyPitchTorque =
            AccessTools.MethodDelegate<Action<PlaneController, Vector3>>(
                AccessTools.DeclaredMethod(typeof(PlaneController), "ApplyInertiaTensorAjustedTorque"));

        internal static bool Braking => Keyboard.current?.bKey.isPressed == true &&
            InputManager.Instance && !InputManager.Instance.currentInputBlocker;

        internal static bool IsTailwheel(Wheel wheel, BushWheelPart gear)
        {
            if (gear)
                return gear.IsTailwheel;
            var part = wheel.GetComponentInParent<BuildingPart>();
            return part && part.name.StartsWith("SmallRearWheel", StringComparison.Ordinal);
        }

        internal static void BrakeWheel(Wheel wheel, BushWheelPart gear)
        {
            var plane = PlaneContainer.Instance;
            if (!wheel.rb || !plane || !plane.FlightModeInitialized)
                return;

            var up = wheel.transform.TransformVector(Vector3.up).normalized;
            var origin = wheel.transform.position + up * wheel.rayUpOffset;
            float range = wheel.suspentionLength + wheel.rayUpOffset;
            bool hitGround = gear && gear.Large
                ? gear.CastContact(origin, -up, out var hit, range, wheel.layerMask)
                : Physics.Raycast(origin, -up, out hit, range, wheel.layerMask);
            if (!hitGround || hit.rigidbody == wheel.rb)
                return;

            float compression = Mathf.Clamp01((range - hit.distance) / Mathf.Max(.05f, wheel.suspentionLength));
            var along = Vector3.ProjectOnPlane(plane.Forward, hit.normal).normalized;
            float speed = Vector3.Dot(wheel.rb.GetPointVelocity(wheel.transform.position), along);
            float force = wheel.suspensionStrenth * plane.GravityMultiplier * compression *
                Mathf.Clamp(speed * .28f, -.19f, .19f);
            // Apply the drag near the tread so the nose dips under braking. Keep the lever arm
            // a little shorter than the full contact distance for tall, light taildraggers.
            var brakePoint = Vector3.Lerp(wheel.transform.position, hit.point, .8f);
            wheel.rb.AddForceAtPosition(-along * force, brakePoint);
        }

        internal static void AssistTailLift(PlaneController __instance, PlaneContainer ___container,
            Rigidbody ___rb, InputManager ___inputManager, float ___pitchControlSurfaceLift, float ___pitchForce)
        {
            if (!___container || !___rb || !___inputManager || ___inputManager.currentInputBlocker ||
                ___inputManager.pitchInput <= 0f || ___pitchControlSurfaceLift <= 0f ||
                !___container.FlightModeInitialized || __instance.currentThrottle <= .14f)
                return;

            var forward = ___container.Forward;
            float speed = Vector3.Dot(___rb.linearVelocity, forward);
            float boost = Mathf.InverseLerp(1.2f, 6f, speed) * (1f - Mathf.InverseLerp(16f, 34f, speed));
            if (boost <= 0f || !___container.IsGrounded())
                return;

            var parts = PlaneParts(___container);
            if (parts == null || !parts.Exists(part => part is Wheel wheel &&
                IsTailwheel(wheel, wheel.GetComponent<BushWheelPart>())))
                return;

            float throttle = Mathf.InverseLerp(.14f, .8f, __instance.currentThrottle);
            float authority = Mathf.Clamp01(___pitchControlSurfaceLift * 40f);
            var noseDown = Vector3.Cross(-forward, __instance.transform.up).normalized;
            ApplyPitchTorque(__instance, noseDown * (___pitchForce * .095f * boost * throttle *
                authority * Mathf.Clamp01(___inputManager.pitchInput)));
        }
    }
}

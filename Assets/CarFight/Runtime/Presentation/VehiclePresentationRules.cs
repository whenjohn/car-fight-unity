using UnityEngine;

namespace CarFight.Presentation
{
    /// <summary>
    /// Presentation-only vehicle feel shared by local, predicted, and remote
    /// views. These values never affect the collider, Rigidbody, input, or
    /// authoritative state.
    /// </summary>
    public static class VehiclePresentationRules
    {
        public const float WheelRadius = 0.31f;
        public const float MaximumVisualSteer = 30f * Mathf.Deg2Rad;
        public const float SteerRateReference = 1.85f;
        public const float MaximumBodyRoll = 11f * Mathf.Deg2Rad;
        public const float BodyRollSpeedReference = 8f;
        public const float MaximumBrakePitch = 18f * Mathf.Deg2Rad;
        public const float BrakePitchSpeedReference = 12f;
        public const float BrakePitchOnset = 0.72f;
        public const float BrakePitchFull = 0.98f;

        public static float ChassisRollTarget(float yawRate, float roadSpeed)
        {
            float steerLoad = Mathf.Clamp(yawRate / SteerRateReference, -1f, 1f);
            float speedLoad = Mathf.Clamp01(roadSpeed / BodyRollSpeedReference);
            return -steerLoad * speedLoad * MaximumBodyRoll;
        }

        public static float ChassisBrakePitchTarget(float brakeSkidAmount, float roadSpeed)
        {
            float skidLoad = Mathf.SmoothStep(
                BrakePitchOnset,
                BrakePitchFull,
                Mathf.Clamp01(brakeSkidAmount));
            float speedLoad = Mathf.Clamp01(roadSpeed / BrakePitchSpeedReference);
            return -skidLoad * speedLoad * MaximumBrakePitch;
        }

        public static float WheelSpinScale(float brakeSkidAmount) =>
            1f - Mathf.Clamp01(brakeSkidAmount);

        public static float VisualSteerTarget(float yawRate) =>
            Mathf.Clamp(yawRate / SteerRateReference, -1f, 1f) * MaximumVisualSteer;
    }
}

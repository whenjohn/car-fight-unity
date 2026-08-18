using CarFight.Driving;
using UnityEngine;

namespace CarFight.Networking.Core
{
    public readonly struct VehicleConvergence
    {
        public VehicleConvergence(
            float positionDistance,
            float yawDifferenceDegrees,
            float planarSpeedDifference)
        {
            PositionDistance = positionDistance;
            YawDifferenceDegrees = yawDifferenceDegrees;
            PlanarSpeedDifference = planarSpeedDifference;
        }

        public float PositionDistance { get; }
        public float YawDifferenceDegrees { get; }
        public float PlanarSpeedDifference { get; }
    }

    public static class VehicleSnapshotRules
    {
        public static bool ShouldReplace(
            AuthoritativeVehicleSnapshot current,
            AuthoritativeVehicleSnapshot candidate)
        {
            return current.VehicleId == candidate.VehicleId &&
                   VehicleInputRules.IsNewer(
                       candidate.ServerSimulationTick,
                       current.ServerSimulationTick);
        }

        public static VehicleConvergence MeasureConvergence(
            AuthoritativeVehicleSnapshot authority,
            AuthoritativeVehicleSnapshot observed)
        {
            float authorityYaw = FollowController.HeadingYaw(authority.Rotation) * Mathf.Rad2Deg;
            float observedYaw = FollowController.HeadingYaw(observed.Rotation) * Mathf.Rad2Deg;
            float yawDifference = Mathf.Abs(Mathf.DeltaAngle(authorityYaw, observedYaw));
            float authorityPlanarSpeed = PlanarSpeed(authority.LinearVelocity);
            float observedPlanarSpeed = PlanarSpeed(observed.LinearVelocity);

            return new VehicleConvergence(
                Vector3.Distance(authority.Position, observed.Position),
                yawDifference,
                Mathf.Abs(authorityPlanarSpeed - observedPlanarSpeed));
        }

        private static float PlanarSpeed(Vector3 velocity)
        {
            return new Vector2(velocity.x, velocity.z).magnitude;
        }
    }
}

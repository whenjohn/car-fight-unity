using UnityEngine;

namespace CarFight.Networking.Core
{
    public enum RemotePresentationMode
    {
        Interpolate,
        Extrapolate,
        Hold
    }

    public readonly struct RemotePresentationSample
    {
        public RemotePresentationSample(
            Vector3 position,
            Quaternion rotation,
            RemotePresentationMode mode,
            float snapshotAgeMilliseconds,
            float headroomMilliseconds)
        {
            Position = position;
            Rotation = rotation;
            Mode = mode;
            SnapshotAgeMilliseconds = snapshotAgeMilliseconds;
            HeadroomMilliseconds = headroomMilliseconds;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public RemotePresentationMode Mode { get; }
        public float SnapshotAgeMilliseconds { get; }
        public float HeadroomMilliseconds { get; }
    }

    public static class PredictionPresentationRules
    {
        public const uint PhysicsRate = 120;
        public const uint PresentationDelayTicks = 9;
        public const float PresentationDelayMilliseconds = 75f;
        public const float MaximumCorrectionPerUpdate = 0.25f;
        public const float MaximumExtrapolationMilliseconds = 100f;

        public static uint RenderTick(uint newestServerTick)
        {
            return newestServerTick > PresentationDelayTicks
                ? newestServerTick - PresentationDelayTicks
                : 0;
        }

        public static RemotePresentationSample Sample(
            AuthoritativeVehicleSnapshot older,
            AuthoritativeVehicleSnapshot newer,
            uint renderTick)
        {
            uint span = newer.ServerSimulationTick - older.ServerSimulationTick;
            uint fromOlder = renderTick - older.ServerSimulationTick;
            float tickMilliseconds = 1000f / PhysicsRate;
            if (span != 0 && fromOlder <= span)
            {
                float t = fromOlder / (float)span;
                return new RemotePresentationSample(
                    Vector3.LerpUnclamped(older.Position, newer.Position, t),
                    Quaternion.SlerpUnclamped(older.Rotation, newer.Rotation, t),
                    RemotePresentationMode.Interpolate,
                    (newer.ServerSimulationTick - renderTick) * tickMilliseconds,
                    (renderTick - older.ServerSimulationTick) * tickMilliseconds);
            }

            uint beyond = renderTick - newer.ServerSimulationTick;
            float beyondMilliseconds = beyond * tickMilliseconds;
            if (beyondMilliseconds <= MaximumExtrapolationMilliseconds)
            {
                float seconds = beyond / (float)PhysicsRate;
                return new RemotePresentationSample(
                    newer.Position + newer.LinearVelocity * seconds,
                    newer.Rotation,
                    RemotePresentationMode.Extrapolate,
                    beyondMilliseconds,
                    0f);
            }

            return new RemotePresentationSample(
                newer.Position,
                newer.Rotation,
                RemotePresentationMode.Hold,
                beyondMilliseconds,
                0f);
        }
    }
}

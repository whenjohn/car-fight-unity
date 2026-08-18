using UnityEngine;

namespace CarFight.Networking.Core
{
    /// <summary>
    /// Settled post-physics authority state. Speculative or renderer-smoothed
    /// transforms must never populate this value.
    /// </summary>
    public readonly struct AuthoritativeVehicleSnapshot
    {
        public AuthoritativeVehicleSnapshot(
            uint serverSimulationTick,
            uint vehicleId,
            uint ownerSessionGeneration,
            Vector3 position,
            Quaternion rotation,
            Vector3 linearVelocity,
            Vector3 angularVelocity,
            uint lastAcceptedInputSequence)
        {
            ServerSimulationTick = serverSimulationTick;
            VehicleId = vehicleId;
            OwnerSessionGeneration = ownerSessionGeneration;
            Position = position;
            Rotation = rotation;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
            LastAcceptedInputSequence = lastAcceptedInputSequence;
        }

        public uint ServerSimulationTick { get; }
        public uint VehicleId { get; }
        public uint OwnerSessionGeneration { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 LinearVelocity { get; }
        public Vector3 AngularVelocity { get; }
        public uint LastAcceptedInputSequence { get; }
    }
}

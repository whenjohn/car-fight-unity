using CarFight.Networking.Core;
using FishNet.Broadcast;
using UnityEngine;

namespace CarFight.Networking.Runtime
{
    public struct JoinRequestMessage : IBroadcast
    {
        public string RunId;
        public string ClientName;
    }

    public struct VehicleAssignmentMessage : IBroadcast
    {
        public string RunId;
        public uint VehicleId;
        public uint SessionGeneration;
    }

    public struct PredictionReadyMessage : IBroadcast
    {
        public string RunId;
    }

    public struct VehicleSnapshotWire
    {
        public uint ServerSimulationTick;
        public uint VehicleId;
        public uint OwnerSessionGeneration;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 LinearVelocity;
        public Vector3 AngularVelocity;
        public uint LastAcceptedInputSequence;

        public VehicleSnapshotWire(AuthoritativeVehicleSnapshot snapshot)
        {
            ServerSimulationTick = snapshot.ServerSimulationTick;
            VehicleId = snapshot.VehicleId;
            OwnerSessionGeneration = snapshot.OwnerSessionGeneration;
            Position = snapshot.Position;
            Rotation = snapshot.Rotation;
            LinearVelocity = snapshot.LinearVelocity;
            AngularVelocity = snapshot.AngularVelocity;
            LastAcceptedInputSequence = snapshot.LastAcceptedInputSequence;
        }

        public AuthoritativeVehicleSnapshot ToSnapshot()
        {
            return new AuthoritativeVehicleSnapshot(
                ServerSimulationTick,
                VehicleId,
                OwnerSessionGeneration,
                Position,
                Rotation,
                LinearVelocity,
                AngularVelocity,
                LastAcceptedInputSequence);
        }
    }

    public struct SnapshotBatchMessage : IBroadcast
    {
        public uint ServerSimulationTick;
        public byte VehicleCount;
        public VehicleSnapshotWire First;
        public VehicleSnapshotWire Second;
    }

    public struct AuthoritativeContactMessage : IBroadcast
    {
        public string RunId;
        public uint ServerSimulationTick;
        public uint FirstVehicleId;
        public uint SecondVehicleId;
        public Vector3 FirstLinearVelocity;
        public Vector3 SecondLinearVelocity;
    }

    public struct ClientCompleteMessage : IBroadcast
    {
        public string RunId;
        public string ClientName;
        public uint LastSnapshotTick;
        public float MaximumRawError;
        public float MaximumVisualCorrection;
        public uint ReplayCount;
        public float FinalPositionError;
        public float FinalYawError;
        public float FinalPlanarSpeedError;
    }

    public struct ScenarioCompleteMessage : IBroadcast
    {
        public string RunId;
        public bool Passed;
        public string Reason;
    }
}

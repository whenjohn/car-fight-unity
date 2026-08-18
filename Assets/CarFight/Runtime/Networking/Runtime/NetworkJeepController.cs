using System;
using CarFight.Driving;
using CarFight.Networking.Core;
using UnityEngine;

namespace CarFight.Networking.Runtime
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class NetworkJeepController : MonoBehaviour
    {
        private Rigidbody body;
        private LayerMask supportMask;
        private LocalDriveState driveState = LocalDriveState.Initial;
        private VehicleInputCommand activeInput;
        private bool hasAcceptedInput;
        private uint lastAcceptedSequence;
        private uint lastInputServerTick;
        private Action<NetworkJeepController, NetworkJeepController> contactHandler;

        public uint VehicleId { get; private set; }
        public uint SessionGeneration { get; private set; }

        public void Configure(
            uint vehicleId,
            Rigidbody vehicleBody,
            LayerMask groundMask,
            Action<NetworkJeepController, NetworkJeepController> onContact)
        {
            VehicleId = vehicleId;
            body = vehicleBody;
            supportMask = groundMask;
            contactHandler = onContact;
        }

        public void AssignSession(uint sessionGeneration)
        {
            SessionGeneration = sessionGeneration;
            activeInput = default;
            hasAcceptedInput = false;
            lastAcceptedSequence = 0;
            lastInputServerTick = 0;
        }

        public VehicleInputValidationResult AcceptInput(
            VehicleInputCommand command,
            uint serverTick)
        {
            VehicleInputValidationResult result = VehicleInputRules.Validate(
                command,
                SessionGeneration,
                hasAcceptedInput,
                lastAcceptedSequence);
            if (!result.Accepted)
                return result;

            activeInput = result.Command;
            hasAcceptedInput = true;
            lastAcceptedSequence = result.Command.Sequence;
            lastInputServerTick = serverTick;
            return result;
        }

        public void Simulate(uint serverTick, float deltaTime)
        {
            Vector3 velocity = body.linearVelocity;
            driveState = new LocalDriveState(
                new Vector3(velocity.x, 0f, velocity.z),
                body.angularVelocity.y,
                driveState.BurstTurnSign,
                driveState.DriftAssistHold,
                driveState.DriftAssistLatched,
                driveState.DriftAssistSide,
                driveState.DriftAssistRearmReady,
                driveState.DriftAssistCharge);

            bool neutral = !hasAcceptedInput ||
                           VehicleInputRules.ShouldUseNeutral(serverTick, lastInputServerTick);
            Vector2 cursor = neutral ? Vector2.zero : activeInput.CursorOffset;
            bool burst = !neutral && activeInput.Burst;
            bool reverse = !neutral && activeInput.Reverse;
            bool grounded = Physics.Raycast(
                body.position,
                Vector3.down,
                VehiclePhysicsProfile.CollisionRadius + 0.18f,
                supportMask,
                QueryTriggerInteraction.Ignore);
            LocalDriveStepResult result = LocalDriveSimulation.Step(
                driveState,
                body.rotation,
                cursor,
                burst,
                reverse,
                grounded,
                deltaTime);
            driveState = result.State;
            body.linearVelocity = FollowController.ComposeDriveVelocity(
                driveState.PlanarVelocity,
                velocity.y);
            body.angularVelocity = FollowController.ComposeDriveAngularVelocity(
                body.angularVelocity,
                driveState.YawRate);
            body.AddTorque(
                FollowController.UprightTorque(body.rotation, body.angularVelocity, body.mass),
                ForceMode.Force);
        }

        public AuthoritativeVehicleSnapshot CaptureSettledSnapshot(uint serverTick)
        {
            return new AuthoritativeVehicleSnapshot(
                serverTick,
                VehicleId,
                SessionGeneration,
                body.position,
                body.rotation,
                body.linearVelocity,
                body.angularVelocity,
                hasAcceptedInput ? lastAcceptedSequence : 0);
        }

        private void OnCollisionEnter(Collision collision)
        {
            NetworkJeepController other = collision.rigidbody == null
                ? null
                : collision.rigidbody.GetComponent<NetworkJeepController>();
            if (other != null)
                contactHandler?.Invoke(this, other);
        }
    }
}

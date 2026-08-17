using UnityEngine;

namespace CarFight.Driving
{
    /// <summary>
    /// Deterministic local integration around the pure FOLLOW command. The live
    /// Rigidbody adapter applies this result while preserving engine-owned Y,
    /// pitch, and roll physics.
    /// </summary>
    public static class LocalDriveSimulation
    {
        public static LocalDriveStepResult Step(
            LocalDriveState state,
            Quaternion bodyRotation,
            Vector2 cursorOffset,
            bool burst,
            bool reverse,
            bool grounded,
            float delta)
        {
            float roadSpeed = state.PlanarVelocity.magnitude;
            float currentYaw = FollowController.HeadingYaw(bodyRotation);
            DriveCommand probe = FollowController.Command(
                cursorOffset,
                currentYaw,
                burst,
                state.BurstTurnSign,
                roadSpeed,
                reverse,
                grounded,
                state.DriftAssistCharge);
            float sustain = FollowController.AutomaticDriftAssistSustain(
                probe.BrakeSkidAmount,
                probe.HeadingError);
            DriftAssistState assist = FollowController.NextDriftAssistState(
                state.DriftAssistHold,
                state.DriftAssistLatched,
                state.DriftAssistSide,
                state.DriftAssistRearmReady,
                probe.DriftAssistAmount * 4f,
                probe.HeadingError,
                probe.Throttle,
                burst,
                reverse,
                grounded,
                roadSpeed,
                sustain,
                delta);
            DriveCommand command = FollowController.Command(
                cursorOffset,
                currentYaw,
                burst,
                state.BurstTurnSign,
                roadSpeed,
                reverse,
                grounded,
                state.DriftAssistCharge,
                assist.Latched,
                assist.Side);

            float charge = FollowController.NextDriftAssistCharge(
                state.DriftAssistCharge,
                assist.Latched ? command.DriftAssistAmount : 0f,
                delta);
            float side = charge <= 0.001f ? 0f : assist.Side;

            Vector3 forward = bodyRotation * Vector3.back;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.back;
            Vector3 targetVelocity = forward * command.Speed * command.DriveSign;
            Vector3 planarVelocity = Vector3.MoveTowards(
                state.PlanarVelocity,
                targetVelocity,
                command.Acceleration * delta);
            planarVelocity = FollowController.DriftCarveVelocity(
                planarVelocity,
                side,
                command.DriftAssistAmount,
                charge,
                delta);
            float yawRate = Mathf.MoveTowards(
                state.YawRate,
                command.YawRate,
                command.YawAcceleration * delta);

            LocalDriveState next = new LocalDriveState(
                planarVelocity,
                yawRate,
                command.BurstTurnSign,
                assist.Hold,
                assist.Latched,
                side,
                assist.RearmReady,
                charge);
            return new LocalDriveStepResult(next, command);
        }
    }

    public readonly struct LocalDriveState
    {
        public static readonly LocalDriveState Initial = new LocalDriveState(
            Vector3.zero,
            0f,
            0f,
            0f,
            false,
            0f,
            true,
            0f);

        public LocalDriveState(
            Vector3 planarVelocity,
            float yawRate,
            float burstTurnSign,
            float driftAssistHold,
            bool driftAssistLatched,
            float driftAssistSide,
            bool driftAssistRearmReady,
            float driftAssistCharge)
        {
            PlanarVelocity = new Vector3(planarVelocity.x, 0f, planarVelocity.z);
            YawRate = yawRate;
            BurstTurnSign = burstTurnSign;
            DriftAssistHold = driftAssistHold;
            DriftAssistLatched = driftAssistLatched;
            DriftAssistSide = driftAssistSide;
            DriftAssistRearmReady = driftAssistRearmReady;
            DriftAssistCharge = driftAssistCharge;
        }

        public Vector3 PlanarVelocity { get; }
        public float YawRate { get; }
        public float BurstTurnSign { get; }
        public float DriftAssistHold { get; }
        public bool DriftAssistLatched { get; }
        public float DriftAssistSide { get; }
        public bool DriftAssistRearmReady { get; }
        public float DriftAssistCharge { get; }
    }

    public readonly struct LocalDriveStepResult
    {
        public LocalDriveStepResult(LocalDriveState state, DriveCommand command)
        {
            State = state;
            Command = command;
        }

        public LocalDriveState State { get; }
        public DriveCommand Command { get; }
    }
}

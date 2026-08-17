using CarFight.Driving;
using NUnit.Framework;
using UnityEngine;

namespace CarFight.Tests.Driving
{
    public sealed class FollowControllerTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void CursorDistanceControlsThrottleAndAccelerationWithoutPivoting()
        {
            DriveCommand idle = FollowController.Command(Vector2.zero, 0f, false, 0f);
            Assert.That(idle.Speed, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(idle.YawRate, Is.EqualTo(0f).Within(Tolerance));

            DriveCommand edge = FollowController.Command(new Vector2(1f, 0f), 0f, false, 0f);
            Assert.That(edge.Speed, Is.EqualTo(0f).Within(Tolerance));

            float halfDistance = (FollowController.Deadzone + FollowController.MaxDistance) * 0.5f;
            DriveCommand half = FollowController.Command(new Vector2(halfDistance, 0f), 0f, false, 0f);
            Assert.That(half.Throttle, Is.EqualTo(0.5f).Within(Tolerance));

            Vector2 far = new Vector2(FollowController.MaxDistance, 0f);
            DriveCommand stopped = FollowController.Command(far, 0f, false, 0f, 0f);
            Assert.That(stopped.YawRate, Is.EqualTo(0f).Within(Tolerance));

            DriveCommand full = FollowController.Command(far, 0f, false, 0f, FollowController.Speed);
            Assert.That(full.Speed, Is.EqualTo(FollowController.Speed).Within(Tolerance));
            Assert.That(full.Acceleration, Is.EqualTo(FollowController.Accel).Within(Tolerance));
            Assert.That(full.Acceleration, Is.GreaterThan(half.Acceleration));
            Assert.That(full.YawRate, Is.EqualTo(-1.05f).Within(Tolerance));
        }

        [Test]
        public void SteeringAuthorityTracksRoadSpeedCursorReachAndPrecisionBand()
        {
            Vector2 far = new Vector2(FollowController.MaxDistance, 0f);
            DriveCommand moving = FollowController.Command(far, 0f, false, 0f, 4f);
            Assert.That(moving.YawRate, Is.EqualTo(-1.4f).Within(Tolerance));

            DriveCommand close = FollowController.Command(new Vector2(4f, 0f), 0f, false, 0f, 4f);
            Assert.That(Mathf.Abs(close.YawRate), Is.GreaterThan(Mathf.Abs(moving.YawRate) * 1.55f));
            Assert.That(Mathf.Abs(close.YawRate), Is.LessThan(Mathf.Abs(moving.YawRate) * 2.05f));
            Assert.That(close.YawAcceleration, Is.GreaterThan(moving.YawAcceleration));

            float angle = 15f * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * FollowController.MaxDistance;
            DriveCommand fine = FollowController.Command(offset, 0f, false, 0f, FollowController.Speed);
            DriveCommand full = FollowController.Command(far, 0f, false, 0f, FollowController.Speed);
            Assert.That(Mathf.Abs(fine.YawRate), Is.LessThan(Mathf.Abs(full.YawRate) * (15f / 90f)));
        }

        [Test]
        public void AutomaticBrakingAndPowerslidePreserveWideMomentum()
        {
            Vector2 far = new Vector2(FollowController.MaxDistance, 0f);
            DriveCommand planted = FollowController.Command(far, 0f, false, 0f, FollowController.Speed);
            DriveCommand straightSkid = FollowController.Command(
                new Vector2(0f, -4f), 0f, false, 0f, FollowController.Speed);
            DriveCommand drifting = FollowController.Command(
                new Vector2(4f, 0f), 0f, false, 0f, FollowController.Speed);
            DriveCommand airborne = FollowController.Command(
                new Vector2(4f, 0f), 0f, false, 0f, FollowController.Speed, grounded: false);
            DriveCommand full = FollowController.Command(far, 0f, false, 0f, FollowController.Speed);
            DriveCommand closeMoving = FollowController.Command(new Vector2(4f, 0f), 0f, false, 0f, 4f);

            Assert.That(planted.DriftAmount, Is.EqualTo(0f));
            Assert.That(straightSkid.BrakeSkidAmount, Is.GreaterThan(0.95f));
            Assert.That(straightSkid.DriftAmount, Is.EqualTo(0f));
            Assert.That(straightSkid.Acceleration, Is.LessThanOrEqualTo(3.3f));
            Assert.That(drifting.DriftAmount, Is.GreaterThan(0.95f));
            Assert.That(drifting.Acceleration, Is.LessThan(FollowController.Brake));
            Assert.That(Mathf.Abs(drifting.YawRate), Is.LessThan(Mathf.Abs(full.YawRate) * 1.9f));
            Assert.That(Mathf.Abs(drifting.YawRate), Is.LessThan(Mathf.Abs(closeMoving.YawRate) * 0.72f));
            Assert.That(airborne.DriftAmount, Is.EqualTo(0f));
        }

        [Test]
        public void DriftAssistRequiresAHighSpeedRearCornerCommit()
        {
            Vector2 rearCorner = new Vector2(Mathf.Sin(45f * Mathf.Deg2Rad), Mathf.Cos(45f * Mathf.Deg2Rad)) * 4f;
            DriveCommand assisted = FollowController.Command(
                rearCorner, 0f, false, 0f, FollowController.Speed, false, true, 1f, true, -1f);
            DriveCommand directlyBack = FollowController.Command(
                new Vector2(0f, 4f), 0f, false, 0f, FollowController.Speed, false, true, 1f);
            DriveCommand slowCorner = FollowController.Command(
                rearCorner, 0f, false, 0f, 4f, false, true, 1f);
            DriveCommand ordinaryDrift = FollowController.Command(
                new Vector2(4f, 0f), 0f, false, 0f, FollowController.Speed);

            Assert.That(assisted.DriftAssistAmount, Is.GreaterThan(0.95f));
            Assert.That(assisted.YawAcceleration, Is.GreaterThan(FollowController.DriftYawAcceleration));
            Assert.That(assisted.Acceleration, Is.LessThanOrEqualTo(FollowController.DriftAssistVelocityResponse + 0.001f));
            Assert.That(Mathf.Abs(assisted.YawRate), Is.GreaterThan(Mathf.Abs(ordinaryDrift.YawRate) * 1.25f));
            Assert.That(directlyBack.DriftAssistAmount, Is.EqualTo(0f));
            Assert.That(slowCorner.DriftAssistAmount, Is.EqualTo(0f));
        }

        [Test]
        public void DriftAssistLatchReleasesAndRearmsDeliberately()
        {
            DriftAssistState state = new DriftAssistState(0f, false, 0f, true);
            for (int step = 0; step < 12; step++)
            {
                state = FollowController.NextDriftAssistState(
                    state.Hold, state.Latched, state.Side, state.RearmReady,
                    1f, 135f * Mathf.Deg2Rad, 0.15f, false, false, true,
                    FollowController.Speed, 1f, 1f / 60f);
            }
            Assert.That(state.Latched, Is.True);
            Assert.That(state.Side, Is.GreaterThan(0f));

            DriftAssistState movingWedge = FollowController.NextDriftAssistState(
                state.Hold, true, state.Side, true, 0f, 90f * Mathf.Deg2Rad,
                0.15f, false, false, true, FollowController.Speed, 0f, 1f / 60f);
            Assert.That(movingWedge.Latched, Is.True);

            DriftAssistState sideExit = FollowController.NextDriftAssistState(
                state.Hold, true, state.Side, true, 0f, 65f * Mathf.Deg2Rad,
                0.15f, false, false, true, FollowController.Speed, 0f, 1f / 60f);
            Assert.That(sideExit.Latched, Is.False);

            DriftAssistState heldWedge = FollowController.NextDriftAssistState(
                0f, false, sideExit.Side, sideExit.RearmReady, 1f, 135f * Mathf.Deg2Rad,
                0.15f, false, false, true, FollowController.Speed, 1f, 1f / 60f);
            Assert.That(heldWedge.Latched, Is.False);
            Assert.That(heldWedge.Hold, Is.EqualTo(0f));
            Assert.That(heldWedge.RearmReady, Is.False);

            DriftAssistState gasExit = FollowController.NextDriftAssistState(
                state.Hold, true, state.Side, true, 0f, 0f, 1f,
                false, false, true, FollowController.Speed, 0f, 1f / 60f);
            Assert.That(gasExit.Latched, Is.False);
            Assert.That(gasExit.RearmReady, Is.True);
        }

        [Test]
        public void ValidEntrySurvivesFallingSpeedAndCarvesMomentum()
        {
            DriftAssistState state = FollowController.NextDriftAssistState(
                0f, false, 0f, true, 1f, 135f * Mathf.Deg2Rad, 0.15f,
                false, false, true, FollowController.Speed, 1f, 1f / 60f);
            for (int step = 0; step < 11; step++)
            {
                state = FollowController.NextDriftAssistState(
                    state.Hold, state.Latched, state.Side, state.RearmReady,
                    0f, 135f * Mathf.Deg2Rad, 0.15f, false, false, true,
                    10f, 1f, 1f / 60f);
            }
            Assert.That(state.Latched, Is.True);

            Vector3 carved = FollowController.DriftCarveVelocity(
                new Vector3(0f, 0f, -18f), 1f, 1f, 1f, 0.5f);
            Assert.That(carved.x, Is.LessThan(-1f));
            Assert.That(carved.magnitude, Is.EqualTo(18f).Within(0.001f));
        }

        [Test]
        public void DriftAssistChargeFillsAndDrainsOnSchedule()
        {
            float charge = 0f;
            for (int step = 0; step < 40; step++)
                charge = FollowController.NextDriftAssistCharge(charge, 1f, 1f / 60f);
            Assert.That(charge, Is.GreaterThan(0.99f));

            for (int step = 0; step < 28; step++)
                charge = FollowController.NextDriftAssistCharge(charge, 0f, 1f / 60f);
            Assert.That(charge, Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        public void BurstAndReverseRetainTheirDistinctDrivingRules()
        {
            DriveCommand burst = FollowController.Command(
                new Vector2(3f, 0f), 0f, true, 0f, FollowController.Speed);
            Assert.That(burst.Speed, Is.EqualTo(FollowController.BurstSpeed).Within(Tolerance));
            Assert.That(burst.Acceleration, Is.EqualTo(FollowController.BurstAcceleration).Within(Tolerance));
            Assert.That(burst.YawRate, Is.EqualTo(-0.9f).Within(Tolerance));

            DriveCommand reverseIdle = FollowController.Command(Vector2.zero, 0f, false, 0f, reverse: true);
            Assert.That(reverseIdle.Speed, Is.EqualTo(FollowController.ReverseSpeed).Within(Tolerance));
            Assert.That(reverseIdle.DriveSign, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(reverseIdle.YawRate, Is.EqualTo(0f).Within(Tolerance));

            Vector2 far = new Vector2(FollowController.MaxDistance, 0f);
            DriveCommand forwardTurn = FollowController.Command(far, 0f, false, 0f, 4f);
            DriveCommand reverseTurn = FollowController.Command(far, 0f, false, 0f, 4f, true);
            Assert.That(Mathf.Sign(reverseTurn.YawRate), Is.Not.EqualTo(Mathf.Sign(forwardTurn.YawRate)));
        }

        [Test]
        public void CollisionEscapeStartsOnlyAfterARealStall()
        {
            CollisionEscapeState escape = new CollisionEscapeState(0f, 0f, 0f, false, false);
            bool started = false;
            for (int step = 0; step < 40; step++)
            {
                escape = FollowController.CollisionEscape(
                    14f, 0f, 0f, escape.StallTime, escape.EscapeTime, escape.EscapeSign,
                    1f / 120f, 1f);
                started |= escape.Started;
            }
            Assert.That(started, Is.True);
            Assert.That(escape.Active, Is.True);
            Assert.That(escape.EscapeSign, Is.EqualTo(1f).Within(Tolerance));

            CollisionEscapeState sideEscape = new CollisionEscapeState(0f, 0f, 0f, false, false);
            for (int step = 0; step < 40; step++)
            {
                sideEscape = FollowController.CollisionEscape(
                    14f, 0f, -0.5f, sideEscape.StallTime, sideEscape.EscapeTime,
                    sideEscape.EscapeSign, 1f / 120f, 1f);
            }
            Assert.That(sideEscape.EscapeSign, Is.EqualTo(-1f).Within(Tolerance));

            CollisionEscapeState clearLaunch = FollowController.CollisionEscape(
                14f, 2f, 0f, 0.2f, 0f, 0f, 1f / 120f, 1f);
            Assert.That(clearLaunch.Active, Is.False);
        }

        [Test]
        public void EscapeDriveDirectionAgreesWithYawDirection()
        {
            AssertVector(FollowController.EscapeDriveDirection(Vector3.back, -1f), Vector3.right);
            AssertVector(FollowController.EscapeDriveDirection(Vector3.back, 1f), Vector3.left);
        }

        [Test]
        public void WallBumpAddsOnlyOutwardLinearImpulseAndBoundedYaw()
        {
            WallBumpResult headOn = FollowController.WallBump(
                Vector3.right, new Vector3(10f, 0f, 0f), Vector3.left, 1f, 2.2f);
            Assert.That(headOn.Active, Is.True);
            Assert.That(Vector3.Dot(headOn.LinearImpulse, Vector3.left), Is.GreaterThan(0.5f));
            Assert.That(Mathf.Abs(Vector3.Dot(headOn.LinearImpulse, Vector3.back)), Is.LessThan(Tolerance));
            Assert.That(headOn.YawImpulse, Is.GreaterThan(0f));
            Assert.That(Mathf.Abs(headOn.YawImpulse), Is.LessThan(10f));

            WallBumpResult glancing = FollowController.WallBump(
                new Vector3(1f, 0f, -1f).normalized,
                new Vector3(8f, 0f, -8f),
                Vector3.left,
                -1f,
                2.2f);
            Assert.That(glancing.Active, Is.True);
            Assert.That(glancing.LinearImpulse.x, Is.LessThan(-0.5f));
            Assert.That(Mathf.Abs(glancing.LinearImpulse.z), Is.LessThan(Tolerance));
        }

        [Test]
        public void PhysicsCompositionPreservesEngineOwnedAxes()
        {
            AssertVector(
                FollowController.ComposeDriveVelocity(new Vector3(3f, 99f, -4f), -7f),
                new Vector3(3f, -7f, -4f));
            AssertVector(
                FollowController.ComposeDriveAngularVelocity(new Vector3(1f, 2f, 3f), -5f),
                new Vector3(1f, -5f, 3f));
            Assert.That(FollowController.HeadingYaw(Quaternion.identity), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(
                FollowController.HeadingYaw(Quaternion.Euler(0f, 90f, 0f)),
                Is.EqualTo(Mathf.PI * 0.5f).Within(Tolerance));
        }

        [Test]
        public void UprightAndLandingHelpersReturnBoundedPhysicalImpulses()
        {
            AssertVector(FollowController.UprightTorque(Quaternion.identity, Vector3.zero, 2.2f), Vector3.zero);
            Vector3 tilted = FollowController.UprightTorque(
                Quaternion.Euler(30f, 0f, 0f), Vector3.zero, 2.2f);
            Assert.That(tilted.magnitude, Is.GreaterThan(0f));
            Assert.That(tilted.magnitude, Is.LessThanOrEqualTo(70f + Tolerance));

            AssertVector(
                FollowController.LandingTorqueImpulse(Vector3.forward, Vector3.up, 2f, 2.2f),
                Vector3.zero);
            Vector3 landing = FollowController.LandingTorqueImpulse(
                new Vector3(10f, -8f, 0f), Vector3.up, 8f, 2.2f);
            Assert.That(landing.magnitude, Is.GreaterThan(0f));
            Assert.That(landing.magnitude, Is.LessThanOrEqualTo(0.65f + Tolerance));
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThanOrEqualTo(Tolerance));
        }
    }
}

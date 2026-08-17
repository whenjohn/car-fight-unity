using CarFight.Driving;
using NUnit.Framework;
using UnityEngine;

namespace CarFight.Tests.Driving
{
    public sealed class LocalDriveSimulationTests
    {
        private const float Delta = 1f / 120f;

        [Test]
        public void ForwardCursorAcceleratesAlongGameplayForward()
        {
            LocalDriveState state = LocalDriveState.Initial;
            for (int step = 0; step < 120; step++)
            {
                state = LocalDriveSimulation.Step(
                    state,
                    Quaternion.identity,
                    new Vector2(0f, -FollowController.MaxDistance),
                    burst: false,
                    reverse: false,
                    grounded: true,
                    Delta).State;
            }

            Assert.That(state.PlanarVelocity.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(state.PlanarVelocity.z, Is.LessThan(-17.5f));
            Assert.That(state.PlanarVelocity.magnitude, Is.LessThanOrEqualTo(FollowController.Speed));
        }

        [Test]
        public void RightCursorBuildsClockwiseYawAtRoadSpeed()
        {
            LocalDriveState moving = new LocalDriveState(
                Vector3.back * 8f,
                0f,
                0f,
                0f,
                false,
                0f,
                true,
                0f);
            LocalDriveStepResult result = LocalDriveSimulation.Step(
                moving,
                Quaternion.identity,
                new Vector2(FollowController.MaxDistance, 0f),
                burst: false,
                reverse: false,
                grounded: true,
                Delta);

            Assert.That(result.Command.YawRate, Is.LessThan(0f));
            Assert.That(result.State.YawRate, Is.LessThan(0f));
            Assert.That(result.State.PlanarVelocity.y, Is.EqualTo(0f));
        }

        [Test]
        public void ReverseCursorProducesBackwardRoadVelocity()
        {
            LocalDriveState state = LocalDriveState.Initial;
            for (int step = 0; step < 60; step++)
            {
                state = LocalDriveSimulation.Step(
                    state,
                    Quaternion.identity,
                    new Vector2(0f, -FollowController.MaxDistance),
                    burst: false,
                    reverse: true,
                    grounded: true,
                    Delta).State;
            }

            Assert.That(state.PlanarVelocity.z, Is.GreaterThan(0f));
            Assert.That(state.PlanarVelocity.magnitude, Is.LessThanOrEqualTo(FollowController.ReverseSpeed));
        }

        [Test]
        public void ZeroCursorBrakesWithoutInventingSteering()
        {
            LocalDriveState moving = new LocalDriveState(
                Vector3.back * 12f,
                0f,
                0f,
                0f,
                false,
                0f,
                true,
                0f);
            LocalDriveStepResult result = LocalDriveSimulation.Step(
                moving,
                Quaternion.identity,
                Vector2.zero,
                burst: false,
                reverse: false,
                grounded: true,
                Delta);

            Assert.That(result.State.PlanarVelocity.magnitude, Is.LessThan(12f));
            Assert.That(result.State.YawRate, Is.EqualTo(0f));
        }
    }
}

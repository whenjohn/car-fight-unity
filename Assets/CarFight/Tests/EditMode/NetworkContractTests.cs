using CarFight.Driving;
using CarFight.Networking.Core;
using NUnit.Framework;
using UnityEngine;

namespace CarFight.Tests.Networking
{
    public sealed class NetworkContractTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void FiniteInRangeInputIsAcceptedWithoutModification()
        {
            VehicleInputCommand command = Command(
                session: 4,
                sequence: 12,
                cursor: new Vector2(3f, -4f),
                burst: true,
                reverse: false);

            VehicleInputValidationResult result = VehicleInputRules.Validate(
                command,
                expectedSessionGeneration: 4,
                hasAcceptedSequence: true,
                lastAcceptedSequence: 11);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.CursorWasClamped, Is.False);
            Assert.That(result.Command.SessionGeneration, Is.EqualTo(4));
            Assert.That(result.Command.Sequence, Is.EqualTo(12));
            Assert.That(result.Command.ClientSimulationTick, Is.EqualTo(90));
            Assert.That(result.Command.CursorOffset, Is.EqualTo(new Vector2(3f, -4f)));
            Assert.That(result.Command.Burst, Is.True);
            Assert.That(result.Command.Reverse, Is.False);
        }

        [Test]
        public void CursorIsClampedToAcceptedMaximum()
        {
            VehicleInputCommand command = Command(1, 1, new Vector2(30f, 40f));

            VehicleInputValidationResult result = VehicleInputRules.Validate(
                command,
                expectedSessionGeneration: 1,
                hasAcceptedSequence: false,
                lastAcceptedSequence: 0);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.CursorWasClamped, Is.True);
            Assert.That(
                result.Command.CursorOffset.magnitude,
                Is.EqualTo(FollowController.MaxDistance).Within(Tolerance));
            Assert.That(result.Command.CursorOffset.normalized, Is.EqualTo(command.CursorOffset.normalized));
        }

        [Test]
        public void NonFiniteCursorIsRejected()
        {
            Vector2[] invalidOffsets =
            {
                new Vector2(float.NaN, 0f),
                new Vector2(0f, float.PositiveInfinity),
                new Vector2(float.NegativeInfinity, 0f)
            };

            foreach (Vector2 offset in invalidOffsets)
            {
                VehicleInputValidationResult result = VehicleInputRules.Validate(
                    Command(1, 1, offset),
                    expectedSessionGeneration: 1,
                    hasAcceptedSequence: false,
                    lastAcceptedSequence: 0);

                Assert.That(result.Accepted, Is.False);
                Assert.That(result.Rejection, Is.EqualTo(VehicleInputRejection.NonFiniteCursor));
            }
        }

        [Test]
        public void DuplicateAndOlderSequencesAreRejected()
        {
            VehicleInputValidationResult duplicate = VehicleInputRules.Validate(
                Command(1, 15, Vector2.zero), 1, true, 15);
            VehicleInputValidationResult older = VehicleInputRules.Validate(
                Command(1, 14, Vector2.zero), 1, true, 15);

            Assert.That(duplicate.Rejection, Is.EqualTo(VehicleInputRejection.DuplicateOrOldSequence));
            Assert.That(older.Rejection, Is.EqualTo(VehicleInputRejection.DuplicateOrOldSequence));
        }

        [Test]
        public void SequenceAndAcknowledgementOrderingSurviveUnsignedWrap()
        {
            VehicleInputValidationResult wrapped = VehicleInputRules.Validate(
                Command(1, 0, Vector2.zero),
                expectedSessionGeneration: 1,
                hasAcceptedSequence: true,
                lastAcceptedSequence: uint.MaxValue);
            VehicleInputValidationResult old = VehicleInputRules.Validate(
                Command(1, uint.MaxValue, Vector2.zero),
                expectedSessionGeneration: 1,
                hasAcceptedSequence: true,
                lastAcceptedSequence: 0);

            Assert.That(wrapped.Accepted, Is.True);
            Assert.That(old.Rejection, Is.EqualTo(VehicleInputRejection.DuplicateOrOldSequence));
            Assert.That(VehicleInputRules.IsAcknowledged(uint.MaxValue, 0), Is.True);
            Assert.That(VehicleInputRules.IsAcknowledged(1, 0), Is.False);
        }

        [Test]
        public void ReconnectedSessionRejectsPriorGeneration()
        {
            VehicleInputValidationResult result = VehicleInputRules.Validate(
                Command(7, 100, Vector2.zero),
                expectedSessionGeneration: 8,
                hasAcceptedSequence: false,
                lastAcceptedSequence: 0);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Rejection, Is.EqualTo(VehicleInputRejection.StaleSession));
        }

        [Test]
        public void MissingInputBecomesNeutralAfterShortGraceWindow()
        {
            const uint lastInputTick = 200;

            Assert.That(VehicleInputRules.ShouldUseNeutral(lastInputTick + 3, lastInputTick), Is.False);
            Assert.That(VehicleInputRules.ShouldUseNeutral(lastInputTick + 4, lastInputTick), Is.True);
            Assert.That(VehicleInputRules.ShouldUseNeutral(1, uint.MaxValue - 1), Is.False);
            Assert.That(VehicleInputRules.ShouldUseNeutral(3, uint.MaxValue - 1), Is.True);
        }

        [Test]
        public void SnapshotOrderingUsesNewestTickForTheSameVehicle()
        {
            AuthoritativeVehicleSnapshot current = Snapshot(tick: 20, vehicleId: 3);
            AuthoritativeVehicleSnapshot newer = Snapshot(tick: 21, vehicleId: 3);
            AuthoritativeVehicleSnapshot equal = Snapshot(tick: 20, vehicleId: 3);
            AuthoritativeVehicleSnapshot older = Snapshot(tick: 19, vehicleId: 3);
            AuthoritativeVehicleSnapshot otherVehicle = Snapshot(tick: 21, vehicleId: 4);

            Assert.That(VehicleSnapshotRules.ShouldReplace(current, newer), Is.True);
            Assert.That(VehicleSnapshotRules.ShouldReplace(current, equal), Is.False);
            Assert.That(VehicleSnapshotRules.ShouldReplace(current, older), Is.False);
            Assert.That(VehicleSnapshotRules.ShouldReplace(current, otherVehicle), Is.False);
            Assert.That(
                VehicleSnapshotRules.ShouldReplace(
                    Snapshot(uint.MaxValue, 3),
                    Snapshot(0, 3)),
                Is.True);
        }

        [Test]
        public void SettledSnapshotPreservesEveryAuthorityValue()
        {
            Vector3 position = new Vector3(1f, 2f, 3f);
            Quaternion rotation = Quaternion.Euler(4f, 5f, 6f);
            Vector3 linearVelocity = new Vector3(7f, 8f, 9f);
            Vector3 angularVelocity = new Vector3(10f, 11f, 12f);
            AuthoritativeVehicleSnapshot snapshot = new AuthoritativeVehicleSnapshot(
                serverSimulationTick: 13,
                vehicleId: 14,
                ownerSessionGeneration: 15,
                position,
                rotation,
                linearVelocity,
                angularVelocity,
                lastAcceptedInputSequence: 16);

            Assert.That(snapshot.ServerSimulationTick, Is.EqualTo(13));
            Assert.That(snapshot.VehicleId, Is.EqualTo(14));
            Assert.That(snapshot.OwnerSessionGeneration, Is.EqualTo(15));
            Assert.That(snapshot.Position, Is.EqualTo(position));
            Assert.That(snapshot.Rotation, Is.EqualTo(rotation));
            Assert.That(snapshot.LinearVelocity, Is.EqualTo(linearVelocity));
            Assert.That(snapshot.AngularVelocity, Is.EqualTo(angularVelocity));
            Assert.That(snapshot.LastAcceptedInputSequence, Is.EqualTo(16));
        }

        [Test]
        public void ConvergenceMeasuresPositionYawAndPlanarSpeed()
        {
            AuthoritativeVehicleSnapshot authority = Snapshot(
                position: new Vector3(1f, 2f, 3f),
                rotation: Quaternion.Euler(0f, 179f, 0f),
                linearVelocity: new Vector3(3f, 9f, 4f));
            AuthoritativeVehicleSnapshot observed = Snapshot(
                position: new Vector3(4f, 6f, 3f),
                rotation: Quaternion.Euler(0f, -179f, 0f),
                linearVelocity: new Vector3(0f, -20f, 7f));

            VehicleConvergence result = VehicleSnapshotRules.MeasureConvergence(authority, observed);

            Assert.That(result.PositionDistance, Is.EqualTo(5f).Within(Tolerance));
            Assert.That(result.YawDifferenceDegrees, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(result.PlanarSpeedDifference, Is.EqualTo(2f).Within(Tolerance));
        }

        private static VehicleInputCommand Command(
            uint session,
            uint sequence,
            Vector2 cursor,
            bool burst = false,
            bool reverse = false)
        {
            return new VehicleInputCommand(session, sequence, 90, cursor, burst, reverse);
        }

        private static AuthoritativeVehicleSnapshot Snapshot(
            uint tick = 1,
            uint vehicleId = 1,
            Vector3 position = default,
            Quaternion rotation = default,
            Vector3 linearVelocity = default)
        {
            return new AuthoritativeVehicleSnapshot(
                tick,
                vehicleId,
                ownerSessionGeneration: 1,
                position,
                rotation,
                linearVelocity,
                angularVelocity: Vector3.zero,
                lastAcceptedInputSequence: 1);
        }
    }
}

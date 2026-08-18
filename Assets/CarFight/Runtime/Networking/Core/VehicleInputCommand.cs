using System;
using CarFight.Driving;
using UnityEngine;

namespace CarFight.Networking.Core
{
    /// <summary>
    /// Transport-independent owner input. Gameplay cursor axes remain
    /// (world X, world Z); no client-authored vehicle state belongs here.
    /// </summary>
    public readonly struct VehicleInputCommand
    {
        public VehicleInputCommand(
            uint sessionGeneration,
            uint sequence,
            uint clientSimulationTick,
            Vector2 cursorOffset,
            bool burst,
            bool reverse)
        {
            SessionGeneration = sessionGeneration;
            Sequence = sequence;
            ClientSimulationTick = clientSimulationTick;
            CursorOffset = cursorOffset;
            Burst = burst;
            Reverse = reverse;
        }

        public uint SessionGeneration { get; }
        public uint Sequence { get; }
        public uint ClientSimulationTick { get; }
        public Vector2 CursorOffset { get; }
        public bool Burst { get; }
        public bool Reverse { get; }
    }

    public enum VehicleInputRejection
    {
        None,
        NonFiniteCursor,
        StaleSession,
        DuplicateOrOldSequence
    }

    public readonly struct VehicleInputValidationResult
    {
        internal VehicleInputValidationResult(
            VehicleInputRejection rejection,
            VehicleInputCommand command,
            bool cursorWasClamped)
        {
            Rejection = rejection;
            Command = command;
            CursorWasClamped = cursorWasClamped;
        }

        public bool Accepted => Rejection == VehicleInputRejection.None;
        public VehicleInputRejection Rejection { get; }
        public VehicleInputCommand Command { get; }
        public bool CursorWasClamped { get; }
    }

    /// <summary>
    /// Stateless validation and ordering rules. FishNet remains responsible
    /// for replicate delivery, buffering, and replay history.
    /// </summary>
    public static class VehicleInputRules
    {
        // At the accepted 120 Hz simulation rate, three ticks are 25 ms.
        // The fourth missing tick resolves to neutral input.
        public const uint MissingInputGraceTicks = 3;

        public static VehicleInputValidationResult Validate(
            VehicleInputCommand command,
            uint expectedSessionGeneration,
            bool hasAcceptedSequence,
            uint lastAcceptedSequence)
        {
            if (!IsFinite(command.CursorOffset.x) || !IsFinite(command.CursorOffset.y))
            {
                return new VehicleInputValidationResult(
                    VehicleInputRejection.NonFiniteCursor,
                    default,
                    false);
            }

            if (command.SessionGeneration != expectedSessionGeneration)
            {
                return new VehicleInputValidationResult(
                    VehicleInputRejection.StaleSession,
                    default,
                    false);
            }

            if (hasAcceptedSequence && !IsNewer(command.Sequence, lastAcceptedSequence))
            {
                return new VehicleInputValidationResult(
                    VehicleInputRejection.DuplicateOrOldSequence,
                    default,
                    false);
            }

            Vector2 cursor = ClampCursor(command.CursorOffset, out bool wasClamped);
            VehicleInputCommand accepted = new VehicleInputCommand(
                command.SessionGeneration,
                command.Sequence,
                command.ClientSimulationTick,
                cursor,
                command.Burst,
                command.Reverse);
            return new VehicleInputValidationResult(
                VehicleInputRejection.None,
                accepted,
                wasClamped);
        }

        public static bool IsNewer(uint candidate, uint reference)
        {
            return unchecked((int)(candidate - reference)) > 0;
        }

        public static bool IsAcknowledged(uint sequence, uint acknowledgedThrough)
        {
            return sequence == acknowledgedThrough || IsNewer(acknowledgedThrough, sequence);
        }

        public static bool ShouldUseNeutral(uint currentServerTick, uint lastInputServerTick)
        {
            return unchecked(currentServerTick - lastInputServerTick) > MissingInputGraceTicks;
        }

        private static Vector2 ClampCursor(Vector2 cursor, out bool wasClamped)
        {
            double x = cursor.x;
            double y = cursor.y;
            double magnitude = Math.Sqrt((x * x) + (y * y));
            if (magnitude <= FollowController.MaxDistance)
            {
                wasClamped = false;
                return cursor;
            }

            float scale = (float)(FollowController.MaxDistance / magnitude);
            wasClamped = true;
            return cursor * scale;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

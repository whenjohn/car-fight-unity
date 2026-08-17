using UnityEngine;

namespace CarFight.Driving
{
    /// <summary>
    /// Pure FOLLOW driving math ported from the Godot reference project.
    /// Gameplay forward remains -Z; cursorOffset is (world X, world Z).
    /// This class owns no Unity object, input, physics body, or network state.
    /// </summary>
    public static class FollowController
    {
        public const float Speed = 18f;
        public const float AccelNear = 14f;
        public const float Accel = 27f;
        public const float Brake = 32f;
        public const float Deadzone = 1f;
        public const float MaxDistance = 20f;
        public const float HeadingDeadzone = 0.8f;
        public const float TurnNear = 2.7f;
        public const float TurnFar = 1.5f;
        public const float TurnAccelNear = 10f;
        public const float TurnAccelFar = 4.5f;
        public const float SteeringSpeedReference = 3.2f;
        public const float BrakeSkidVelocityResponse = 3.2f;
        public const float DriftVelocityResponse = 2.6f;
        public const float DriftYawAcceleration = 10.5f;
        public const float DriftAssistVelocityResponse = 1.35f;
        public const float BurstSpeed = 28f;
        public const float BurstAcceleration = 38f;
        public const float ReverseSpeed = 6f;
        public const float ReverseAcceleration = 14f;

        private const float SteeringResponseCurve = 1.2f;
        private const float HighSpeedTurnScale = 0.70f;
        private const float BrakeSkidMinSpeed = 9f;
        private const float BrakeSkidFullSpeed = 17f;
        private const float BrakeSkidMinSpeedDrop = 4f;
        private const float BrakeSkidFullSpeedDrop = 13f;
        private const float BrakeSkidSteeringGrip = 0.68f;
        private const float DriftMinHeading = 25f * Mathf.Deg2Rad;
        private const float DriftFullHeading = 75f * Mathf.Deg2Rad;
        private const float DriftTurnBoost = 1.10f;
        private const float DriftAssistZoneInner = 90f * Mathf.Deg2Rad;
        private const float DriftAssistZoneFull = 112f * Mathf.Deg2Rad;
        private const float DriftAssistZoneFade = 165f * Mathf.Deg2Rad;
        private const float DriftAssistZoneOuter = 178f * Mathf.Deg2Rad;
        private const float DriftAssistBrakeOnset = 0.45f;
        private const float DriftAssistBrakeFull = 0.80f;
        private const float DriftAssistReadyMinSpeed = 14f;
        private const float DriftAssistReadyFullSpeed = 17f;
        private const float DriftAssistTurnBoost = 1.35f;
        private const float DriftAssistYawAcceleration = 16f;
        private const float DriftAssistPathTurnRate = 0.85f;
        private const float DriftAssistArmTime = 0.18f;
        private const float DriftAssistSideExitAngle = 72f * Mathf.Deg2Rad;
        private const float DriftAssistAccelExitThrottle = 0.72f;
        private const float DriftAssistAccelExitAngle = 85f * Mathf.Deg2Rad;
        private const float DriftAssistMinLatchSpeed = 8f;
        private const float DriftAssistChargeTime = 0.65f;
        private const float DriftAssistReleaseTime = 0.45f;
        private const float BurstTurn = 0.9f;
        private const float BurstTurnAcceleration = 3.4f;
        private const float BurstFlipOn = 150f * Mathf.Deg2Rad;
        private const float BurstFlipOff = 110f * Mathf.Deg2Rad;
        private const float ReverseTurn = 2.4f;
        private const float ReverseTurnAcceleration = 7f;
        private const float EscapeMinRequestSpeed = 4f;
        private const float EscapeStallSpeed = 0.6f;
        private const float EscapeStallDelay = 0.22f;
        private const float EscapeDuration = 0.7f;
        private const float EscapeDeflectionAngle = Mathf.PI * 0.5f;
        private const float EscapeSteerEpsilon = 0.08f;
        private const float WallBumpMinApproach = 0.10f;
        private const float WallBumpBaseDeltaSpeed = 1.8f;
        private const float WallBumpImpactScale = 0.14f;
        private const float WallBumpMaxDeltaSpeed = 3.8f;
        private const float WallBumpBaseYawImpulse = 7f;
        private const float WallBumpYawImpactScale = 0.08f;
        private const float WallBumpMaxYawImpulse = 9f;
        private const float UprightStiffness = 32f;
        private const float UprightDamping = 7f;
        private const float UprightMaxTorque = 70f;
        private const float LandingMinImpactSpeed = 2.5f;
        private const float LandingTorqueScale = 0.006f;
        private const float LandingMaxTorqueImpulse = 0.65f;
        private const float Epsilon = 0.000001f;

        public static DriveCommand Command(
            Vector2 cursorOffset,
            float currentYaw,
            bool burst,
            float burstTurnSign,
            float currentSpeed = 0f,
            bool reverse = false,
            bool grounded = true,
            float driftAssistCharge = 0f,
            bool driftAssistLatched = false,
            float driftAssistSide = 0f)
        {
            float distance = cursorOffset.magnitude;
            float desiredYaw = currentYaw;
            if (distance > 0.0001f)
                desiredYaw = Mathf.Atan2(-cursorOffset.x, -cursorOffset.y);

            float error = WrapAngle(desiredYaw - currentYaw);
            float throttle = Mathf.Clamp01((distance - Deadzone) / (MaxDistance - Deadzone));
            float cursorReach = Mathf.Clamp01(distance / MaxDistance);
            float topSpeed = Speed;
            bool boostActive = false;
            float brakeSkidAmount = 0f;
            float driftAmount = 0f;
            float driftZoneAmount = 0f;
            float driftAssistAmount = 0f;
            float yawAcceleration = Mathf.Lerp(TurnAccelNear, TurnAccelFar, cursorReach);
            float turnCap = Mathf.Lerp(TurnNear, TurnFar, cursorReach);

            if (reverse)
            {
                throttle = 1f;
                topSpeed = ReverseSpeed;
                turnCap = ReverseTurn;
                yawAcceleration = ReverseTurnAcceleration;
                burstTurnSign = 0f;
            }
            else if (burst && distance > Deadzone)
            {
                boostActive = true;
                throttle = 1f;
                topSpeed = BurstSpeed;
                turnCap = BurstTurn;
                yawAcceleration = BurstTurnAcceleration;
                if (Mathf.Abs(error) >= BurstFlipOn && IsZero(burstTurnSign))
                    burstTurnSign = SignZero(error);
                if (!IsZero(burstTurnSign))
                {
                    if (Mathf.Abs(error) <= BurstFlipOff)
                        burstTurnSign = 0f;
                    else
                        error = Mathf.Abs(error) * burstTurnSign;
                }
            }
            else
            {
                burstTurnSign = 0f;
                float roadSpeed = Mathf.Clamp01(currentSpeed / Speed);
                turnCap *= Mathf.Lerp(1f, HighSpeedTurnScale, roadSpeed);
            }

            float targetSpeed = topSpeed * throttle;
            if (grounded && !reverse && !boostActive)
            {
                brakeSkidAmount = AutomaticBrakeSkid(currentSpeed, targetSpeed);
                driftAmount = AutomaticDrift(brakeSkidAmount, error);
                driftZoneAmount = AutomaticDriftZone(error);
                float entryAmount = AutomaticDriftAssistEntry(currentSpeed, brakeSkidAmount, error);
                driftAssistAmount = driftAssistLatched ? 1f : entryAmount * 0.25f;
                if (driftAssistLatched)
                    driftAmount = Mathf.Max(driftAmount, 1f);

                turnCap *= Mathf.Lerp(1f, BrakeSkidSteeringGrip, brakeSkidAmount);
                turnCap *= Mathf.Lerp(1f, DriftTurnBoost, driftAmount);
                yawAcceleration = Mathf.Lerp(yawAcceleration, DriftYawAcceleration, driftAmount);
                float assistStrength = driftAssistAmount * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(driftAssistCharge));
                turnCap *= Mathf.Lerp(1f, DriftAssistTurnBoost, assistStrength);
                yawAcceleration = Mathf.Lerp(yawAcceleration, DriftAssistYawAcceleration, assistStrength);
            }

            float speedAuthority = Mathf.Clamp01(currentSpeed / SteeringSpeedReference);
            float cursorAuthority = Mathf.Clamp01(
                (distance - HeadingDeadzone) / (HeadingDeadzone * 0.5f));
            float linearSteering = Mathf.Clamp(error / (Mathf.PI * 0.5f), -1f, 1f);
            float steeringFraction = SignZero(linearSteering)
                * Mathf.Pow(Mathf.Abs(linearSteering), SteeringResponseCurve);
            float yawRate = steeringFraction * turnCap * speedAuthority * cursorAuthority
                * (reverse ? -1f : 1f);
            if (driftAssistLatched && !IsZero(driftAssistSide))
                yawRate = SignZero(driftAssistSide) * turnCap * speedAuthority;

            float acceleration = reverse
                ? ReverseAcceleration
                : burst && distance > Deadzone
                    ? BurstAcceleration
                    : Mathf.Lerp(AccelNear, Accel, throttle);
            if (targetSpeed < currentSpeed)
            {
                acceleration = Mathf.Lerp(Brake, BrakeSkidVelocityResponse, brakeSkidAmount);
                acceleration = Mathf.Lerp(acceleration, DriftVelocityResponse, driftAmount);
                acceleration = Mathf.Lerp(acceleration, DriftAssistVelocityResponse, driftAssistAmount);
            }

            return new DriveCommand(
                targetSpeed,
                acceleration,
                yawRate,
                yawAcceleration,
                turnCap * speedAuthority,
                error,
                burstTurnSign,
                throttle,
                reverse ? -1f : 1f,
                boostActive,
                brakeSkidAmount,
                driftAmount,
                driftZoneAmount,
                driftAssistAmount);
        }

        public static float AutomaticBrakeSkid(float currentSpeed, float targetSpeed)
        {
            float speedFactor = Smoothstep(BrakeSkidMinSpeed, BrakeSkidFullSpeed, currentSpeed);
            float speedDrop = Mathf.Max(currentSpeed - targetSpeed, 0f);
            float inwardFactor = Smoothstep(BrakeSkidMinSpeedDrop, BrakeSkidFullSpeedDrop, speedDrop);
            return speedFactor * inwardFactor;
        }

        public static float AutomaticDrift(float brakeSkidAmount, float headingError)
        {
            float turnFactor = Smoothstep(DriftMinHeading, DriftFullHeading, Mathf.Abs(headingError));
            return brakeSkidAmount * turnFactor;
        }

        public static float AutomaticDriftZone(float headingError)
        {
            float angle = Mathf.Abs(WrapAngle(headingError));
            float enter = Smoothstep(DriftAssistZoneInner, DriftAssistZoneFull, angle);
            float leave = 1f - Smoothstep(DriftAssistZoneFade, DriftAssistZoneOuter, angle);
            return enter * leave;
        }

        public static float DriftAssistReadyFraction(float currentSpeed)
        {
            return Smoothstep(DriftAssistReadyMinSpeed, DriftAssistReadyFullSpeed, currentSpeed);
        }

        public static float AutomaticDriftAssistEntry(
            float currentSpeed,
            float brakeSkidAmount,
            float headingError)
        {
            return AutomaticDriftAssistSustain(brakeSkidAmount, headingError)
                * DriftAssistReadyFraction(currentSpeed);
        }

        public static float AutomaticDriftAssistSustain(float brakeSkidAmount, float headingError)
        {
            float brakeCommit = Smoothstep(
                DriftAssistBrakeOnset,
                DriftAssistBrakeFull,
                Mathf.Clamp01(brakeSkidAmount));
            return AutomaticDriftZone(headingError) * brakeCommit;
        }

        public static DriftAssistState NextDriftAssistState(
            float currentHold,
            bool currentLatched,
            float currentSide,
            bool currentRearmReady,
            float entryAmount,
            float headingError,
            float throttle,
            bool burst,
            bool reverse,
            bool grounded,
            float currentSpeed,
            float sustainAmount,
            float delta)
        {
            float hold = Mathf.Max(currentHold, 0f);
            bool latched = currentLatched;
            float side = SignZero(currentSide);
            bool rearmReady = currentRearmReady;
            float angle = Mathf.Abs(WrapAngle(headingError));

            if (!grounded || reverse || currentSpeed < DriftAssistMinLatchSpeed)
                return new DriftAssistState(0f, false, side, true);

            bool accelerateOut = burst
                || throttle >= DriftAssistAccelExitThrottle && angle <= DriftAssistAccelExitAngle;
            if (latched)
            {
                bool sideSkid = angle <= DriftAssistSideExitAngle;
                if (accelerateOut || sideSkid)
                    return new DriftAssistState(0f, false, side, accelerateOut);
                return new DriftAssistState(DriftAssistArmTime, true, side, rearmReady);
            }

            if (!rearmReady)
                return new DriftAssistState(0f, false, side, accelerateOut);

            float armingAmount = hold <= 0f ? entryAmount : sustainAmount;
            if (armingAmount <= 0.35f)
                return new DriftAssistState(0f, false, side, rearmReady);

            float requestedSide = SignZero(WrapAngle(headingError));
            if (IsZero(requestedSide))
                return new DriftAssistState(0f, false, side, rearmReady);
            if (!IsZero(side) && requestedSide != side)
                hold = 0f;

            side = requestedSide;
            hold = Mathf.Min(
                hold + delta * Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(armingAmount)),
                DriftAssistArmTime);
            latched = hold >= DriftAssistArmTime;
            return new DriftAssistState(hold, latched, side, rearmReady);
        }

        public static float NextDriftAssistCharge(float currentCharge, float assistAmount, float delta)
        {
            float charge = Mathf.Clamp01(currentCharge);
            if (assistAmount > 0.001f)
            {
                return Mathf.MoveTowards(
                    charge,
                    1f,
                    delta / DriftAssistChargeTime * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(assistAmount)));
            }
            return Mathf.MoveTowards(charge, 0f, delta / DriftAssistReleaseTime);
        }

        public static Vector3 DriftCarveVelocity(
            Vector3 planarVelocity,
            float assistSide,
            float assistAmount,
            float assistCharge,
            float delta)
        {
            if (planarVelocity.sqrMagnitude <= 0.0001f || IsZero(assistSide) || assistAmount <= 0.001f)
                return planarVelocity;

            float strength = Mathf.Clamp01(assistAmount)
                * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(assistCharge));
            float radians = SignZero(assistSide) * DriftAssistPathTurnRate * strength * delta;
            return RotateAroundY(planarVelocity, radians);
        }

        public static Vector3 ComposeDriveVelocity(Vector3 planarVelocity, float verticalVelocity)
        {
            return new Vector3(planarVelocity.x, verticalVelocity, planarVelocity.z);
        }

        public static Vector3 ComposeDriveAngularVelocity(Vector3 physicalVelocity, float yawRate)
        {
            return new Vector3(physicalVelocity.x, yawRate, physicalVelocity.z);
        }

        public static float HeadingYaw(Quaternion bodyRotation)
        {
            Vector3 forward = bodyRotation * Vector3.back;
            forward.y = 0f;
            if (forward.sqrMagnitude <= Epsilon)
                return 0f;
            forward.Normalize();
            return Mathf.Atan2(-forward.x, -forward.z);
        }

        public static Vector3 UprightTorque(Quaternion bodyRotation, Vector3 angularVelocity, float bodyMass)
        {
            Vector3 bodyUp = (bodyRotation * Vector3.up).normalized;
            float uprightDot = Mathf.Clamp(Vector3.Dot(bodyUp, Vector3.up), -1f, 1f);
            Vector3 tiltAxis = Vector3.Cross(bodyUp, Vector3.up);
            Vector3 restoring = Vector3.zero;
            if (tiltAxis.sqrMagnitude > Epsilon)
                restoring = tiltAxis.normalized * Mathf.Acos(uprightDot) * UprightStiffness;

            Vector3 pitchRollVelocity = new Vector3(angularVelocity.x, 0f, angularVelocity.z);
            Vector3 torque = (restoring - pitchRollVelocity * UprightDamping)
                * Mathf.Max(bodyMass, 0.001f);
            return Vector3.ClampMagnitude(torque, UprightMaxTorque);
        }

        public static Vector3 LandingTorqueImpulse(
            Vector3 velocity,
            Vector3 supportNormal,
            float impactSpeed,
            float bodyMass)
        {
            Vector3 normal = supportNormal.normalized;
            if (impactSpeed < LandingMinImpactSpeed || normal.sqrMagnitude <= Epsilon)
                return Vector3.zero;

            Vector3 tangentVelocity = velocity - normal * Vector3.Dot(velocity, normal);
            float tangentSpeed = tangentVelocity.magnitude;
            if (tangentSpeed < 0.1f)
                return Vector3.zero;

            Vector3 axis = Vector3.Cross(normal, tangentVelocity / tangentSpeed).normalized;
            float magnitude = impactSpeed * Mathf.Min(tangentSpeed, Speed)
                * LandingTorqueScale * Mathf.Max(bodyMass, 0.001f);
            return axis * Mathf.Min(magnitude, LandingMaxTorqueImpulse);
        }

        public static CollisionEscapeState CollisionEscape(
            float requestedSpeed,
            float currentSpeed,
            float headingError,
            float stallTime,
            float escapeTime,
            float escapeSign,
            float delta,
            float fallbackSign)
        {
            bool started = false;
            if (escapeTime > 0f)
            {
                escapeTime = Mathf.Max(escapeTime - delta, 0f);
            }
            else
            {
                escapeSign = 0f;
                if (requestedSpeed >= EscapeMinRequestSpeed && currentSpeed <= EscapeStallSpeed)
                    stallTime += delta;
                else
                    stallTime = Mathf.Max(stallTime - delta * 3f, 0f);

                if (stallTime >= EscapeStallDelay)
                {
                    stallTime = 0f;
                    escapeTime = EscapeDuration;
                    escapeSign = Mathf.Abs(headingError) >= EscapeSteerEpsilon
                        ? SignZero(headingError)
                        : SignZero(fallbackSign);
                    if (IsZero(escapeSign))
                        escapeSign = 1f;
                    started = true;
                }
            }

            return new CollisionEscapeState(
                stallTime,
                escapeTime,
                escapeSign,
                escapeTime > 0f,
                started);
        }

        public static Vector3 EscapeDriveDirection(Vector3 forward, float escapeSign)
        {
            Vector3 planarForward = new Vector3(forward.x, 0f, forward.z).normalized;
            if (planarForward.sqrMagnitude <= Epsilon || IsZero(escapeSign))
                return planarForward;
            return RotateAroundY(planarForward, SignZero(escapeSign) * EscapeDeflectionAngle).normalized;
        }

        public static WallBumpResult WallBump(
            Vector3 forward,
            Vector3 velocity,
            Vector3 wallNormal,
            float preferredTurnSign,
            float bodyMass)
        {
            Vector3 planarForward = new Vector3(forward.x, 0f, forward.z).normalized;
            Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 wallOut = new Vector3(wallNormal.x, 0f, wallNormal.z).normalized;
            Vector3 motion = planarVelocity.sqrMagnitude > Epsilon ? planarVelocity.normalized : planarForward;
            if (planarForward.sqrMagnitude <= Epsilon
                || wallOut.sqrMagnitude <= Epsilon
                || -Vector3.Dot(motion, wallOut) < WallBumpMinApproach)
            {
                return new WallBumpResult(false, Vector3.zero, 0f);
            }

            Vector3 tangent = motion - wallOut * Vector3.Dot(motion, wallOut);
            float turnSign = 0f;
            if (tangent.sqrMagnitude >= 0.04f)
                turnSign = SignZero(Vector3.Cross(planarForward, tangent.normalized).y);
            if (IsZero(turnSign))
                turnSign = SignZero(preferredTurnSign);
            if (IsZero(turnSign))
                turnSign = 1f;

            float approachSpeed = Mathf.Max(-Vector3.Dot(planarVelocity, wallOut), 0f);
            float deltaSpeed = Mathf.Clamp(
                WallBumpBaseDeltaSpeed + approachSpeed * WallBumpImpactScale,
                WallBumpBaseDeltaSpeed,
                WallBumpMaxDeltaSpeed);
            float yawMagnitude = Mathf.Clamp(
                WallBumpBaseYawImpulse + approachSpeed * WallBumpYawImpactScale,
                WallBumpBaseYawImpulse,
                WallBumpMaxYawImpulse);
            return new WallBumpResult(
                true,
                wallOut * deltaSpeed * Mathf.Max(bodyMass, 0.001f),
                turnSign * yawMagnitude);
        }

        private static float Smoothstep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float WrapAngle(float radians)
        {
            return Mathf.Repeat(radians + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
        }

        private static float SignZero(float value)
        {
            return value > 0f ? 1f : value < 0f ? -1f : 0f;
        }

        private static bool IsZero(float value)
        {
            return Mathf.Abs(value) <= Epsilon;
        }

        private static Vector3 RotateAroundY(Vector3 vector, float radians)
        {
            float sine = Mathf.Sin(radians);
            float cosine = Mathf.Cos(radians);
            return new Vector3(
                cosine * vector.x + sine * vector.z,
                vector.y,
                -sine * vector.x + cosine * vector.z);
        }
    }

    public readonly struct DriveCommand
    {
        public DriveCommand(
            float speed,
            float acceleration,
            float yawRate,
            float yawAcceleration,
            float turnCap,
            float headingError,
            float burstTurnSign,
            float throttle,
            float driveSign,
            bool boostActive,
            float brakeSkidAmount,
            float driftAmount,
            float driftZoneAmount,
            float driftAssistAmount)
        {
            Speed = speed;
            Acceleration = acceleration;
            YawRate = yawRate;
            YawAcceleration = yawAcceleration;
            TurnCap = turnCap;
            HeadingError = headingError;
            BurstTurnSign = burstTurnSign;
            Throttle = throttle;
            DriveSign = driveSign;
            BoostActive = boostActive;
            BrakeSkidAmount = brakeSkidAmount;
            DriftAmount = driftAmount;
            DriftZoneAmount = driftZoneAmount;
            DriftAssistAmount = driftAssistAmount;
        }

        public float Speed { get; }
        public float Acceleration { get; }
        public float YawRate { get; }
        public float YawAcceleration { get; }
        public float TurnCap { get; }
        public float HeadingError { get; }
        public float BurstTurnSign { get; }
        public float Throttle { get; }
        public float DriveSign { get; }
        public bool BoostActive { get; }
        public float BrakeSkidAmount { get; }
        public float DriftAmount { get; }
        public float DriftZoneAmount { get; }
        public float DriftAssistAmount { get; }
    }

    public readonly struct DriftAssistState
    {
        public DriftAssistState(float hold, bool latched, float side, bool rearmReady)
        {
            Hold = hold;
            Latched = latched;
            Side = side;
            RearmReady = rearmReady;
        }

        public float Hold { get; }
        public bool Latched { get; }
        public float Side { get; }
        public bool RearmReady { get; }
    }

    public readonly struct CollisionEscapeState
    {
        public CollisionEscapeState(float stallTime, float escapeTime, float escapeSign, bool active, bool started)
        {
            StallTime = stallTime;
            EscapeTime = escapeTime;
            EscapeSign = escapeSign;
            Active = active;
            Started = started;
        }

        public float StallTime { get; }
        public float EscapeTime { get; }
        public float EscapeSign { get; }
        public bool Active { get; }
        public bool Started { get; }
    }

    public readonly struct WallBumpResult
    {
        public WallBumpResult(bool active, Vector3 linearImpulse, float yawImpulse)
        {
            Active = active;
            LinearImpulse = linearImpulse;
            YawImpulse = yawImpulse;
        }

        public bool Active { get; }
        public Vector3 LinearImpulse { get; }
        public float YawImpulse { get; }
    }
}

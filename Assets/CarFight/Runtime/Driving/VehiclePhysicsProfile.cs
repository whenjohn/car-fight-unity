using UnityEngine;

namespace CarFight.Driving
{
    /// <summary>
    /// Shared physical contract for local, test, and later authoritative Jeeps.
    /// The visible Jeep is presentation; this equal-mass sphere owns gameplay contacts.
    /// </summary>
    public static class VehiclePhysicsProfile
    {
        public const float CollisionRadius = 1.55f;
        public const float Mass = 2.2f;
        public const float ContactFriction = 0f;
        public const float Bounce = 0.18f;
        public const float AngularDamping = 4.5f;
        public const float PhysicsRate = 120f;
        public const float MaxAngularVelocity = 12f;

        public static void Configure(Rigidbody body)
        {
            body.mass = Mass;
            body.linearDamping = 0f;
            body.angularDamping = AngularDamping;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = MaxAngularVelocity;
        }

        public static void Configure(PhysicsMaterial material)
        {
            material.dynamicFriction = ContactFriction;
            material.staticFriction = ContactFriction;
            material.bounciness = Bounce;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            material.bounceCombine = PhysicsMaterialCombine.Maximum;
        }
    }
}

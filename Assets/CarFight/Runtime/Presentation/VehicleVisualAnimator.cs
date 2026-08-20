using CarFight.Driving;
using CarFight.Networking.Runtime;
using UnityEngine;

namespace CarFight.Presentation
{
    /// <summary>
    /// Animates only the Jeep's detached visual hierarchy. The root Rigidbody
    /// remains the single gameplay collider; this component never writes it.
    /// </summary>
    public sealed class VehicleVisualAnimator : MonoBehaviour
    {
        [SerializeField] private Rigidbody vehicleBody;
        [SerializeField] private Transform chassisLean;
        [SerializeField] private Transform[] frontSteerPivots;
        [SerializeField] private Transform[] wheelSpinPivots;

        private LocalJeepController localController;
        private NetworkJeepController networkController;
        private Vector3 previousPosition;
        private float previousYaw;
        private bool motionInitialized;
        private float wheelSpinAngle;

        public void Configure(
            Rigidbody body,
            Transform chassis,
            Transform[] frontSteer,
            Transform[] wheelSpin)
        {
            vehicleBody = body;
            chassisLean = chassis;
            frontSteerPivots = frontSteer;
            wheelSpinPivots = wheelSpin;
        }

        private void Awake()
        {
            if (vehicleBody == null)
                vehicleBody = GetComponentInParent<Rigidbody>();
            if (vehicleBody != null)
            {
                localController = vehicleBody.GetComponent<LocalJeepController>();
                networkController = vehicleBody.GetComponent<NetworkJeepController>();
            }
        }

        private void LateUpdate()
        {
            if (vehicleBody == null || chassisLean == null)
                return;

            float delta = Time.deltaTime;
            if (delta <= 0f)
                return;

            Vector3 planarVelocity = PlanarVelocity(delta);
            float roadSpeed = planarVelocity.magnitude;
            float yawRate = YawRate(delta);
            float brakeSkid = BrakeSkidAmount();

            Vector3 chassisEuler = chassisLean.localEulerAngles;
            float roll = Mathf.LerpAngle(
                chassisEuler.z,
                VehiclePresentationRules.ChassisRollTarget(yawRate, roadSpeed) * Mathf.Rad2Deg,
                1f - Mathf.Exp(-9f * delta));
            float pitch = Mathf.LerpAngle(
                chassisEuler.x,
                VehiclePresentationRules.ChassisBrakePitchTarget(brakeSkid, roadSpeed) * Mathf.Rad2Deg,
                1f - Mathf.Exp(-4.5f * delta));
            chassisLean.localRotation = Quaternion.Euler(pitch, 0f, roll);

            float steer = VehiclePresentationRules.VisualSteerTarget(yawRate) * Mathf.Rad2Deg;
            foreach (Transform pivot in frontSteerPivots)
            {
                if (pivot == null)
                    continue;
                float current = pivot.localEulerAngles.y;
                pivot.localRotation = Quaternion.Euler(
                    0f,
                    Mathf.LerpAngle(current, steer, 1f - Mathf.Exp(-12f * delta)),
                    0f);
            }

            Vector3 forward = vehicleBody.rotation * Vector3.back;
            forward.y = 0f;
            forward.Normalize();
            float signedSpeed = Vector3.Dot(planarVelocity, forward);
            wheelSpinAngle = Mathf.Repeat(
                wheelSpinAngle + signedSpeed / VehiclePresentationRules.WheelRadius * delta *
                VehiclePresentationRules.WheelSpinScale(brakeSkid),
                360f);
            foreach (Transform pivot in wheelSpinPivots)
            {
                if (pivot != null)
                    pivot.localRotation = Quaternion.Euler(wheelSpinAngle, 0f, 0f);
            }
        }

        private Vector3 PlanarVelocity(float delta)
        {
            Vector3 position = vehicleBody.position;
            if (!motionInitialized)
            {
                previousPosition = position;
                previousYaw = vehicleBody.rotation.eulerAngles.y;
                motionInitialized = true;
                return Vector3.zero;
            }

            Vector3 inferred = (position - previousPosition) / delta;
            previousPosition = position;
            Vector3 physics = vehicleBody.linearVelocity;
            physics.y = 0f;
            return physics.sqrMagnitude > 0.0001f ? physics : new Vector3(inferred.x, 0f, inferred.z);
        }

        private float YawRate(float delta)
        {
            float yaw = vehicleBody.rotation.eulerAngles.y;
            float inferred = Mathf.DeltaAngle(previousYaw, yaw) * Mathf.Deg2Rad / delta;
            previousYaw = yaw;
            return Mathf.Abs(vehicleBody.angularVelocity.y) > 0.0001f
                ? vehicleBody.angularVelocity.y
                : inferred;
        }

        private float BrakeSkidAmount()
        {
            if (localController != null && localController.enabled)
                return localController.BrakeSkidAmount;
            return networkController != null ? networkController.BrakeSkidAmount : 0f;
        }
    }
}

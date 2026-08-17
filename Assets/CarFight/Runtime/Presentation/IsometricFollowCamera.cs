using UnityEngine;

namespace CarFight.Presentation
{
    [RequireComponent(typeof(Camera))]
    public sealed class IsometricFollowCamera : MonoBehaviour
    {
        public const float GodotCameraSize = 42f;
        public const float UnityOrthographicSize = GodotCameraSize * 0.5f;
        public const float Distance = 80f;
        public const float YawDegrees = 45f;
        public const float PitchDegrees = 55f;

        [SerializeField] private Transform target;

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            SnapToTarget();
        }

        private void Awake()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            SnapToTarget();
        }

        private void SnapToTarget()
        {
            if (target == null)
                return;

            Vector3 lookTarget = new Vector3(target.position.x, 0f, target.position.z);
            float yaw = YawDegrees * Mathf.Deg2Rad;
            float pitch = PitchDegrees * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(pitch) * Distance;
            Vector3 offset = new Vector3(
                Mathf.Sin(yaw) * horizontal,
                Mathf.Sin(pitch) * Distance,
                Mathf.Cos(yaw) * horizontal);
            transform.position = lookTarget + offset;
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        }
    }
}

using CarFight.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarFight.Driving
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class LocalJeepController : MonoBehaviour
    {
        public const float CollisionRadius = VehiclePhysicsProfile.CollisionRadius;
        public const float VehicleMass = VehiclePhysicsProfile.Mass;
        public const float PhysicsRate = VehiclePhysicsProfile.PhysicsRate;

        [SerializeField] private Camera playerCamera;
        [SerializeField] private CursorIntentView cursorView;
        [SerializeField] private LayerMask supportMask = 1 << 2;

        private Rigidbody body;
        private LocalDriveState driveState = LocalDriveState.Initial;
        private Vector2 cursorOffset;
        private bool burst;
        private bool reverse;

        public Vector2 CursorOffset => cursorOffset;
        public float BrakeSkidAmount { get; private set; }

        public void Configure(Camera camera, CursorIntentView view, LayerMask groundMask)
        {
            playerCamera = camera;
            cursorView = view;
            supportMask = groundMask;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            Time.fixedDeltaTime = 1f / PhysicsRate;
        }

        private void Update()
        {
            GatherInput();
            if (cursorView != null)
                cursorView.Render(transform.position, cursorOffset, CollisionRadius);
        }

        private void FixedUpdate()
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

            bool grounded = Physics.Raycast(
                transform.position,
                Vector3.down,
                CollisionRadius + 0.18f,
                supportMask,
                QueryTriggerInteraction.Ignore);
            LocalDriveStepResult result = LocalDriveSimulation.Step(
                driveState,
                body.rotation,
                cursorOffset,
                burst,
                reverse,
                grounded,
                Time.fixedDeltaTime);
            driveState = result.State;
            BrakeSkidAmount = result.Command.BrakeSkidAmount;
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

        private void GatherInput()
        {
            Keyboard keyboard = Keyboard.current;
            burst = keyboard != null && keyboard.spaceKey.isPressed;
            reverse = keyboard != null && keyboard.tabKey.isPressed;

            Mouse mouse = Mouse.current;
            if (mouse == null || playerCamera == null)
            {
                cursorOffset = Vector2.zero;
                return;
            }

            Ray ray = playerCamera.ScreenPointToRay(mouse.position.ReadValue());
            float roadPlaneY = transform.position.y - CollisionRadius;
            if (Mathf.Abs(ray.direction.y) <= 0.00001f)
            {
                cursorOffset = Vector2.zero;
                return;
            }

            float distance = (roadPlaneY - ray.origin.y) / ray.direction.y;
            if (distance < 0f)
            {
                cursorOffset = Vector2.zero;
                return;
            }

            Vector3 hit = ray.origin + ray.direction * distance;
            Vector3 delta = hit - transform.position;
            cursorOffset = Vector2.ClampMagnitude(
                new Vector2(delta.x, delta.z),
                FollowController.MaxDistance);
        }
    }
}

using System;
using CarFight.Driving;
using CarFight.Networking.Core;
using FishNet.Connection;
using FishNet.Managing.Predicting;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace CarFight.Networking.Runtime
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider), typeof(NetworkObject))]
    public sealed class NetworkJeepController : TickNetworkBehaviour
    {
        public struct VehicleReplicateData : IReplicateData
        {
            public VehicleReplicateData(VehicleInputCommand command)
            {
                SessionGeneration = command.SessionGeneration;
                Sequence = command.Sequence;
                ClientSimulationTick = command.ClientSimulationTick;
                CursorOffset = command.CursorOffset;
                Burst = command.Burst;
                Reverse = command.Reverse;
                tick = 0;
            }

            public uint SessionGeneration;
            public uint Sequence;
            public uint ClientSimulationTick;
            public Vector2 CursorOffset;
            public bool Burst;
            public bool Reverse;
            private uint tick;

            public VehicleInputCommand ToCommand()
            {
                return new VehicleInputCommand(
                    SessionGeneration,
                    Sequence,
                    ClientSimulationTick,
                    CursorOffset,
                    Burst,
                    Reverse);
            }

            public void Dispose() { }
            public uint GetTick() => tick;
            public void SetTick(uint value) => tick = value;
        }

        public struct VehicleReconcileData : IReconcileData
        {
            public VehicleReconcileData(PredictionRigidbody body, LocalDriveState state)
            {
                Body = body;
                AuthorityPosition = body.Rigidbody.position;
                PlanarVelocity = state.PlanarVelocity;
                YawRate = state.YawRate;
                BurstTurnSign = state.BurstTurnSign;
                DriftAssistHold = state.DriftAssistHold;
                DriftAssistLatched = state.DriftAssistLatched;
                DriftAssistSide = state.DriftAssistSide;
                DriftAssistRearmReady = state.DriftAssistRearmReady;
                DriftAssistCharge = state.DriftAssistCharge;
                tick = 0;
            }

            public PredictionRigidbody Body;
            public Vector3 AuthorityPosition;
            public Vector3 PlanarVelocity;
            public float YawRate;
            public float BurstTurnSign;
            public float DriftAssistHold;
            public bool DriftAssistLatched;
            public float DriftAssistSide;
            public bool DriftAssistRearmReady;
            public float DriftAssistCharge;
            private uint tick;

            public LocalDriveState ToDriveState()
            {
                return new LocalDriveState(
                    PlanarVelocity,
                    YawRate,
                    BurstTurnSign,
                    DriftAssistHold,
                    DriftAssistLatched,
                    DriftAssistSide,
                    DriftAssistRearmReady,
                    DriftAssistCharge);
            }

            public void Dispose() { }
            public uint GetTick() => tick;
            public void SetTick(uint value) => tick = value;
        }

        private readonly PredictionRigidbody predictionBody = new PredictionRigidbody();
        private Rigidbody body;
        private SphereCollider vehicleCollider;
        private NetworkJeepController remoteCollisionPeer;
        private PredictionManager predictionManager;
        private Vector3 remoteAuthorityVelocity;
        private LayerMask supportMask;
        private LocalDriveState driveState = LocalDriveState.Initial;
        private Vector2 localCursor;
        private bool localBurst;
        private bool localReverse;
        private bool localInputEnabled;
        private bool simulationEnabled;
        private bool hasAcceptedInput;
        private uint localSequence;
        private uint lastAcceptedSequence;
        private uint localPredictedContactTick = TimeManager.UNSET_TICK;
        private Vector3 localPredictedContactVelocity;
        private uint replayCount;
        private float maximumRawError;
        private float maximumVisualCorrection;
        private Vector3 previousBodyPosition;
        private Vector3 previousVisualPosition;
        private Vector3 preReconcilePosition;
        private Vector3 preReconcileVelocity;
        private bool visualLimiterInitialized;
        private bool capturedPreReconcile;
        private bool hasRemoteAuthority;
        private Action<NetworkJeepController, NetworkJeepController> contactHandler;
        private Action<NetworkJeepController, VehicleInputValidationResult> inputHandler;

        public uint VehicleId { get; private set; }
        public uint SessionGeneration { get; private set; }
        public uint ReplayCount => replayCount;
        public float MaximumRawError => maximumRawError;
        public float MaximumVisualCorrection => maximumVisualCorrection;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            vehicleCollider = GetComponent<SphereCollider>();
            predictionBody.Initialize(body);
        }

        public void Configure(
            uint vehicleId,
            LayerMask groundMask,
            Action<NetworkJeepController, NetworkJeepController> onContact,
            Action<NetworkJeepController, VehicleInputValidationResult> onInput)
        {
            VehicleId = vehicleId;
            supportMask = groundMask;
            contactHandler = onContact;
            inputHandler = onInput;
        }

        public void AssignSession(uint sessionGeneration)
        {
            SessionGeneration = sessionGeneration;
            localSequence = 0;
            hasAcceptedInput = false;
            lastAcceptedSequence = 0;
            driveState = LocalDriveState.Initial;
            localPredictedContactTick = TimeManager.UNSET_TICK;
            localPredictedContactVelocity = Vector3.zero;
        }

        public void SetSimulationEnabled(bool value) => simulationEnabled = value;

        public void StopAuthoritativeMotion()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            driveState = LocalDriveState.Initial;
        }

        public void SetLocalInput(Vector2 cursor, bool burst, bool reverse)
        {
            localCursor = cursor;
            localBurst = burst;
            localReverse = reverse;
            localInputEnabled = true;
        }

        public void ConfigureAsClientView(bool owned)
        {
            if (owned)
            {
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            else
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
            }
            // The owner predicts ordinary ground/wall contacts locally; the
            // server still supplies the only authoritative contact outcome.
            // Remote bodies remain kinematic and do not participate in local
            // PhysX authority. The owner predicts only its own equal-mass
            // contact response from the latest remote authority estimate.
            body.detectCollisions = true;
            vehicleCollider.enabled = owned;
        }

        public void ConfigureRemoteCollisionPeer(NetworkJeepController remote)
        {
            remoteCollisionPeer = remote;
        }

        public void SetRemoteAuthorityEstimate(
            AuthoritativeVehicleSnapshot snapshot,
            uint estimatedServerTick)
        {
            if (IsOwner)
                return;
            uint ageTicks = VehicleInputRules.IsNewer(
                estimatedServerTick,
                snapshot.ServerSimulationTick)
                ? estimatedServerTick - snapshot.ServerSimulationTick
                : 0;
            float seconds = Mathf.Min(ageTicks / (float)PredictionPresentationRules.PhysicsRate, 0.25f);
            body.position = snapshot.Position + snapshot.LinearVelocity * seconds;
            body.rotation = snapshot.Rotation;
            remoteAuthorityVelocity = snapshot.LinearVelocity;
            hasRemoteAuthority = true;
        }

        public void SetRemotePresentation(RemotePresentationSample sample)
        {
            Transform visual = NetworkObject.GetGraphicalObject();
            if (visual == null)
                return;
            visual.SetPositionAndRotation(sample.Position, sample.Rotation);
        }

        public override void OnStartNetwork()
        {
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        public override void OnStartClient()
        {
            predictionManager = NetworkManager.GetComponent<PredictionManager>();
            predictionManager.OnPreReconcile += OnPreReconcile;
            predictionManager.OnPostReconcile += OnPostReconcile;
        }

        public override void OnStopClient()
        {
            if (predictionManager == null)
                return;
            predictionManager.OnPreReconcile -= OnPreReconcile;
            predictionManager.OnPostReconcile -= OnPostReconcile;
            predictionManager = null;
        }

        public override void OnOwnershipClient(NetworkConnection previousOwner)
        {
            visualLimiterInitialized = false;
            ConfigureAsClientView(IsOwner);
        }

        private void LateUpdate()
        {
            if (!IsOwner || NetworkObject == null)
            {
                visualLimiterInitialized = false;
                return;
            }

            Transform visual = NetworkObject.GetGraphicalObject();
            if (visual == null)
                return;
            if (!visualLimiterInitialized)
            {
                previousBodyPosition = body.position;
                previousVisualPosition = visual.position;
                visualLimiterInitialized = true;
                return;
            }

            Vector3 expected = previousVisualPosition + (body.position - previousBodyPosition);
            Vector3 correction = visual.position - expected;
            float applied = Mathf.Min(
                correction.magnitude,
                PredictionPresentationRules.MaximumCorrectionPerUpdate);
            if (correction.sqrMagnitude >
                PredictionPresentationRules.MaximumCorrectionPerUpdate *
                PredictionPresentationRules.MaximumCorrectionPerUpdate)
            {
                visual.position = expected + correction.normalized *
                    PredictionPresentationRules.MaximumCorrectionPerUpdate;
            }

            maximumVisualCorrection = Mathf.Max(maximumVisualCorrection, applied);
            previousBodyPosition = body.position;
            previousVisualPosition = visual.position;
        }

        protected override void TimeManager_OnTick()
        {
            if (!simulationEnabled)
                return;
            PerformReplicate(BuildReplicateData());
        }

        protected override void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }

        private VehicleReplicateData BuildReplicateData()
        {
            if (!IsOwner || !localInputEnabled || SessionGeneration == 0)
                return default;

            localSequence++;
            return new VehicleReplicateData(new VehicleInputCommand(
                SessionGeneration,
                localSequence,
                TimeManager.LocalTick,
                localCursor,
                localBurst,
                localReverse));
        }

        [Replicate]
        private void PerformReplicate(
            VehicleReplicateData data,
            ReplicateState state = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            if (!IsServerStarted && !IsOwner)
                return;

            VehicleInputCommand command = data.ToCommand();
            if (IsServerStarted &&
                state.ContainsCreated() &&
                command.SessionGeneration != 0)
            {
                VehicleInputValidationResult validation = VehicleInputRules.Validate(
                    command,
                    SessionGeneration,
                    hasAcceptedInput,
                    lastAcceptedSequence);
                inputHandler?.Invoke(this, validation);
                if (!validation.Accepted)
                    return;

                command = validation.Command;
                hasAcceptedInput = true;
                lastAcceptedSequence = command.Sequence;
            }

            if (!simulationEnabled)
                return;

            if (state.ContainsReplayed())
                replayCount++;
            bool neutral = !state.ContainsCreated() || command.SessionGeneration != SessionGeneration;
            Simulate(
                neutral ? Vector2.zero : command.CursorOffset,
                !neutral && command.Burst,
                !neutral && command.Reverse,
                command.ClientSimulationTick);
        }

        private void Simulate(Vector2 cursor, bool burst, bool reverse, uint simulationTick)
        {
            if (IsOwner &&
                localPredictedContactTick != TimeManager.UNSET_TICK &&
                VehicleInputRules.IsNewer(simulationTick, localPredictedContactTick))
            {
                cursor = Vector2.zero;
                burst = false;
                reverse = false;
            }
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
                body.position,
                Vector3.down,
                VehiclePhysicsProfile.CollisionRadius + 0.18f,
                supportMask,
                QueryTriggerInteraction.Ignore);
            LocalDriveStepResult result = LocalDriveSimulation.Step(
                driveState,
                body.rotation,
                cursor,
                burst,
                reverse,
                grounded,
                (float)TimeManager.TickDelta);
            driveState = result.State;
            predictionBody.Velocity(FollowController.ComposeDriveVelocity(
                driveState.PlanarVelocity,
                velocity.y));
            PredictRemoteContact(simulationTick);
            predictionBody.AngularVelocity(FollowController.ComposeDriveAngularVelocity(
                body.angularVelocity,
                driveState.YawRate));
            predictionBody.AddTorque(
                FollowController.UprightTorque(body.rotation, body.angularVelocity, body.mass),
                ForceMode.Force);
            predictionBody.Simulate();
        }

        private void PredictRemoteContact(uint simulationTick)
        {
            if (!IsOwner)
                return;
            if (localPredictedContactTick != TimeManager.UNSET_TICK)
            {
                if (simulationTick == localPredictedContactTick)
                    ApplyPredictedContactVelocity(localPredictedContactVelocity);
                return;
            }
            if (remoteCollisionPeer == null || !remoteCollisionPeer.hasRemoteAuthority)
                return;

            Vector3 offset = remoteCollisionPeer.body.position - body.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            float contactDistance = VehiclePhysicsProfile.CollisionRadius * 2f;
            if (distance <= 0.0001f)
                return;

            Vector3 normal = offset / distance;
            Vector3 localVelocity = new Vector3(
                driveState.PlanarVelocity.x,
                0f,
                driveState.PlanarVelocity.z);
            Vector3 remoteVelocity = remoteCollisionPeer.remoteAuthorityVelocity;
            remoteVelocity.y = 0f;
            float closingSpeed = Vector3.Dot(localVelocity - remoteVelocity, normal);
            float projectedDistance = distance - closingSpeed * (float)TimeManager.TickDelta;
            if (closingSpeed <= 0f || projectedDistance > contactDistance + 0.05f)
                return;

            float localNormalSpeed = Vector3.Dot(localVelocity, normal);
            float remoteNormalSpeed = Vector3.Dot(remoteVelocity, normal);
            float resolvedNormalSpeed = (localNormalSpeed + remoteNormalSpeed) * 0.5f;
            Vector3 resolved = localVelocity + (resolvedNormalSpeed - localNormalSpeed) * normal;
            localPredictedContactTick = simulationTick;
            localPredictedContactVelocity = resolved;
            ApplyPredictedContactVelocity(resolved);
            Debug.Log(
                $"CFNET event=PREDICTED_CONTACT vehicle={VehicleId} tick={simulationTick} local_tick={TimeManager.LocalTick} distance={distance:F3} closing_speed={closingSpeed:F3} local_speed={localNormalSpeed:F3} remote_speed={remoteNormalSpeed:F3}");
        }

        private void ApplyPredictedContactVelocity(Vector3 planarVelocity)
        {
            driveState = new LocalDriveState(
                planarVelocity,
                driveState.YawRate,
                driveState.BurstTurnSign,
                driveState.DriftAssistHold,
                driveState.DriftAssistLatched,
                driveState.DriftAssistSide,
                driveState.DriftAssistRearmReady,
                driveState.DriftAssistCharge);
            predictionBody.Velocity(FollowController.ComposeDriveVelocity(
                planarVelocity,
                body.linearVelocity.y));
        }

        public override void CreateReconcile()
        {
            if (!simulationEnabled)
                return;
            PerformReconcile(new VehicleReconcileData(predictionBody, driveState));
        }

        [Reconcile]
        private void PerformReconcile(
            VehicleReconcileData data,
            Channel channel = Channel.Unreliable)
        {
            predictionBody.Reconcile(data.Body);
            driveState = data.ToDriveState();
        }

        private void OnPreReconcile(uint clientTick, uint serverTick)
        {
            if (!IsOwner)
                return;
            preReconcilePosition = body.position;
            preReconcileVelocity = body.linearVelocity;
            capturedPreReconcile = true;
        }

        private void OnPostReconcile(uint clientTick, uint serverTick)
        {
            if (!IsOwner || !capturedPreReconcile)
                return;
            capturedPreReconcile = false;
            float rawError = Vector3.Distance(preReconcilePosition, body.position);
            if (rawError > maximumRawError + 0.25f)
            {
                Debug.Log(
                    $"CFNET event=RAW_ERROR vehicle={VehicleId} raw={rawError:F4} client_tick={clientTick} server_tick={serverTick} predicted_contact={(localPredictedContactTick != TimeManager.UNSET_TICK ? 1 : 0)} before_position={preReconcilePosition} after_position={body.position} before_velocity={preReconcileVelocity} after_velocity={body.linearVelocity}");
            }
            maximumRawError = Mathf.Max(maximumRawError, rawError);
        }

        public AuthoritativeVehicleSnapshot CaptureSettledSnapshot(uint serverTick)
        {
            return new AuthoritativeVehicleSnapshot(
                serverTick,
                VehicleId,
                SessionGeneration,
                body.position,
                body.rotation,
                body.linearVelocity,
                body.angularVelocity,
                hasAcceptedInput ? lastAcceptedSequence : 0);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServerStarted)
                return;
            NetworkJeepController other = collision.rigidbody == null
                ? null
                : collision.rigidbody.GetComponent<NetworkJeepController>();
            if (other != null)
                contactHandler?.Invoke(this, other);
        }
    }
}

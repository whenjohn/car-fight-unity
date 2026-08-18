using System.Collections;
using System.Collections.Generic;
using CarFight.Driving;
using CarFight.Networking.Core;
using CarFight.Presentation;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarFight.Networking.Runtime
{
    /// <summary>
    /// Checkpoint 2D's native-process proof. FishNet owns prediction/replay and
    /// the server remains authoritative for physics contacts and settled state.
    /// </summary>
    public sealed class BaselineNetworkScenario : MonoBehaviour
    {
        public const uint SnapshotIntervalTicks = 4;
        private const float ProcessTimeoutSeconds = 25f;
        private const uint NeutralSettleTicks = 120;
        private const int GroundLayerMask = 1 << 2;

        private readonly Dictionary<int, ServerPeer> serverPeers = new Dictionary<int, ServerPeer>();
        private readonly Dictionary<int, JoinRequestMessage> pendingJoins =
            new Dictionary<int, JoinRequestMessage>();
        private readonly HashSet<string> assignedNames = new HashSet<string>();
        private readonly Dictionary<uint, Rigidbody> clientViews = new Dictionary<uint, Rigidbody>();
        private readonly Dictionary<uint, AuthoritativeVehicleSnapshot> clientSnapshots =
            new Dictionary<uint, AuthoritativeVehicleSnapshot>();
        private readonly List<AuthoritativeVehicleSnapshot> remotePresentationSnapshots =
            new List<AuthoritativeVehicleSnapshot>();

        private NetworkManager manager;
        private NetworkLaunchOptions options;
        private NetworkJeepController firstVehicle;
        private NetworkJeepController secondVehicle;
        private Vector3 firstStartPosition;
        private Vector3 secondStartPosition;
        private float startedAt;
        private uint serverTick;
        private uint nextSessionGeneration = 1;
        private uint assignedVehicleId;
        private uint assignedSessionGeneration;
        private uint lastSnapshotTick;
        private uint contactTick;
        private uint neutralTicks;
        private uint lateJoinSoloTicks;
        private int assignedCount;
        private int completedCount;
        private int completedDisconnectCount;
        private int snapshotBatchCount;
        private int snapshotsAfterContact;
        private int rejectedInputCount;
        private uint lastRenderTick;
        private float interpolationSeconds;
        private float extrapolationSeconds;
        private float holdSeconds;
        private float maximumSnapshotAgeMilliseconds;
        private float maximumSnapshotHeadroomMilliseconds;
        private float connectedAt;
        private float lastSnapshotReceivedAt;
        private uint stallRecoveryTick;
        private bool begun;
        private bool sceneConfigured;
        private bool serverReady;
        private bool firstCompleteSnapshot;
        private bool contactObserved;
        private bool bothVehiclesMoved;
        private bool inputSentLogged;
        private bool predictionReady;
        private bool predictionStartAuthorized;
        private bool serverPredictionReady;
        private bool predictionPrepareSent;
        private bool allPlayersReady;
        private bool clientPrepareReceived;
        private bool clientReadyLogged;
        private bool completionSent;
        private bool invalidAuthoritySent;
        private bool staleHistorySkipped;
        private bool stallRecoveryComplete;
        private bool finishing;
        private CursorIntentView cursorView;
        private Camera playerCamera;

        private bool IsInteractive => options.Script == "interactive";
        private bool IsLateJoin => options.Scenario == "late_join";

        public static bool ShouldPublishSnapshot(uint simulationTick)
        {
            return simulationTick != 0 && simulationTick % SnapshotIntervalTicks == 0;
        }

        public void Begin(NetworkManager networkManager, NetworkLaunchOptions launchOptions)
        {
            if (begun)
                return;

            begun = true;
            manager = networkManager;
            options = launchOptions;
            manager.TimeManager.SetTickRate((ushort)VehiclePhysicsProfile.PhysicsRate);
            manager.TimeManager.SetPhysicsMode(FishNet.Managing.Timing.PhysicsMode.TimeManager);
            startedAt = Time.realtimeSinceStartup;
            StartCoroutine(StartAfterSceneLoad());
        }

        private IEnumerator StartAfterSceneLoad()
        {
            yield return null;
            if (!ConfigureSceneVehicles())
            {
                Finish(false, "scene_vehicles_missing");
                yield break;
            }

            if (options.Role == NetworkProcessRole.Server)
                StartServer();
            else
                StartClient();
        }

        private bool ConfigureSceneVehicles()
        {
            GameObject local = FindSceneVehicle("LocalJeep");
            GameObject collision = FindSceneVehicle("CollisionJeep");
            if (local == null || collision == null)
                return false;

            LocalJeepController localController = local.GetComponent<LocalJeepController>();
            if (localController != null)
                localController.enabled = false;

            Rigidbody localBody = local.GetComponent<Rigidbody>();
            Rigidbody collisionBody = collision.GetComponent<Rigidbody>();
            if (localBody == null || collisionBody == null)
                return false;
            cursorView = FindFirstObjectByType<CursorIntentView>();
            playerCamera = FindFirstObjectByType<Camera>();

            firstVehicle = local.GetComponent<NetworkJeepController>();
            secondVehicle = collision.GetComponent<NetworkJeepController>();
            if (firstVehicle == null || secondVehicle == null)
                return false;
            firstVehicle.Configure(1, GroundLayerMask, OnAuthoritativeContact, OnVehicleInput);
            secondVehicle.Configure(2, GroundLayerMask, OnAuthoritativeContact, OnVehicleInput);

            if (options.Role == NetworkProcessRole.Server)
            {
                firstStartPosition = localBody.position;
                secondStartPosition = collisionBody.position;
            }
            else
            {
                ConfigureClientView(1, firstVehicle, localBody);
                ConfigureClientView(2, secondVehicle, collisionBody);
                firstVehicle.ConfigureRemoteCollisionPeer(secondVehicle);
                secondVehicle.ConfigureRemoteCollisionPeer(firstVehicle);
            }

            sceneConfigured = true;
            return true;
        }

        private static GameObject FindSceneVehicle(string objectName)
        {
            NetworkJeepController[] vehicles = FindObjectsByType<NetworkJeepController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (NetworkJeepController vehicle in vehicles)
            {
                if (vehicle.name == objectName)
                    return vehicle.gameObject;
            }

            return null;
        }

        private void ConfigureClientView(
            uint vehicleId,
            NetworkJeepController controller,
            Rigidbody body)
        {
            controller.ConfigureAsClientView(false);
            clientViews.Add(vehicleId, body);
        }

        private void StartServer()
        {
            manager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            manager.ServerManager.OnRemoteConnectionState += OnServerRemoteConnectionState;
            manager.ServerManager.RegisterBroadcast<JoinRequestMessage>(OnJoinRequest);
            manager.ServerManager.RegisterBroadcast<ClientCompleteMessage>(OnClientComplete);
            manager.ServerManager.RegisterBroadcast<InvalidAuthorityRequestMessage>(OnInvalidAuthorityRequest);
            manager.ServerManager.RegisterBroadcast<ClientReadyMessage>(OnClientReady);
            manager.TimeManager.OnPostTick += OnNetworkPostTick;
            manager.TransportManager.Transport.SetMaximumClients(2);
            Log("SERVER_STARTING", $"port={options.Port}");
            if (!manager.ServerManager.StartConnection(options.Port))
                Finish(false, "server_start_failed");
        }

        private void StartClient()
        {
            manager.ClientManager.OnAuthenticated += OnClientAuthenticated;
            manager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            manager.ClientManager.RegisterBroadcast<VehicleAssignmentMessage>(OnVehicleAssignment);
            manager.ClientManager.RegisterBroadcast<PredictionReadyMessage>(OnPredictionReady);
            manager.ClientManager.RegisterBroadcast<AllPlayersReadyMessage>(OnAllPlayersReady);
            manager.ClientManager.RegisterBroadcast<SnapshotBatchMessage>(OnSnapshotBatch);
            manager.ClientManager.RegisterBroadcast<AuthoritativeContactMessage>(OnContactObserved);
            manager.ClientManager.RegisterBroadcast<ScenarioCompleteMessage>(OnScenarioComplete);
            manager.TimeManager.OnTick += OnNetworkTick;
            manager.TimeManager.OnPostTick += OnNetworkPostTick;
            Log("CLIENT_CONNECTING", $"host={options.Host} port={options.Port}");
            if (!manager.ClientManager.StartConnection(options.Host, options.Port))
                Finish(false, "client_start_failed");
        }

        private void OnNetworkTick()
        {
            if (!sceneConfigured || finishing)
                return;
            if (options.Role != NetworkProcessRole.Client ||
                assignedVehicleId == 0 ||
                !firstCompleteSnapshot ||
                !manager.ClientManager.Started)
                return;

            bool neutral = !IsInteractive && contactObserved;
            Vector2 cursor = IsInteractive
                ? GatherInteractiveCursor()
                : Vector2.zero;
            if (!IsInteractive && !neutral)
                cursor = assignedVehicleId == 1 ? Vector2.down * 20f : Vector2.up * 20f;
            if ((IsLateJoin || options.Scenario == "reconnect") &&
                options.ClientName == "alpha" &&
                !allPlayersReady)
            {
                if (IsLateJoin)
                    lateJoinSoloTicks++;
                if (!IsLateJoin || lateJoinSoloTicks > 1)
                    cursor = Vector2.zero;
            }
            NetworkJeepController owned = assignedVehicleId == 1 ? firstVehicle : secondVehicle;
            Keyboard keyboard = Keyboard.current;
            bool burst = IsInteractive && keyboard != null && keyboard.spaceKey.isPressed;
            bool reverse = IsInteractive && keyboard != null && keyboard.tabKey.isPressed;
            owned.SetLocalInput(cursor, burst, reverse);
            if (IsInteractive && cursorView != null)
            {
                cursorView.Render(
                    owned.transform.position,
                    cursor,
                    VehiclePhysicsProfile.CollisionRadius);
            }

            if (predictionReady && !inputSentLogged)
            {
                inputSentLogged = true;
                Log("INPUT_SENT", $"response_ticks=1 vehicle={assignedVehicleId}");
            }
            if (predictionReady &&
                options.Scenario == "invalid_authority" &&
                !invalidAuthoritySent)
            {
                invalidAuthoritySent = true;
                uint foreignVehicleId = assignedVehicleId == 1 ? 2u : 1u;
                manager.ClientManager.Broadcast(new InvalidAuthorityRequestMessage
                {
                    RunId = options.RunId,
                    ClaimedVehicleId = foreignVehicleId,
                    ClaimedSessionGeneration = assignedSessionGeneration,
                    ClaimedPosition = new Vector3(500f, 50f, -500f),
                    ClaimedLinearVelocity = Vector3.one * 1000f
                });
                Log("INVALID_AUTHORITY_SENT", $"claimed_vehicle={foreignVehicleId}");
            }
        }

        private Vector2 GatherInteractiveCursor()
        {
            Mouse mouse = Mouse.current;
            NetworkJeepController owned = OwnedVehicle();
            if (mouse == null || playerCamera == null || owned == null)
                return Vector2.zero;

            Ray ray = playerCamera.ScreenPointToRay(mouse.position.ReadValue());
            float roadPlaneY = owned.transform.position.y - VehiclePhysicsProfile.CollisionRadius;
            if (Mathf.Abs(ray.direction.y) <= 0.00001f)
                return Vector2.zero;
            float distance = (roadPlaneY - ray.origin.y) / ray.direction.y;
            if (distance < 0f)
                return Vector2.zero;
            Vector3 hit = ray.origin + ray.direction * distance;
            Vector3 delta = hit - owned.transform.position;
            return Vector2.ClampMagnitude(
                new Vector2(delta.x, delta.z),
                FollowController.MaxDistance);
        }

        private void OnNetworkPostTick()
        {
            if (!sceneConfigured || finishing)
                return;

            if (options.Role == NetworkProcessRole.Server)
            {
                if (!serverReady)
                    return;
                serverTick = manager.TimeManager.LocalTick;
                if (ShouldPublishSnapshot(serverTick))
                    PublishSettledSnapshots();
                return;
            }

            if (IsInteractive)
                return;

            if (assignedVehicleId == 0 || !firstCompleteSnapshot || !contactObserved)
                return;

            neutralTicks++;
            if (!completionSent &&
                neutralTicks >= NeutralSettleTicks &&
                snapshotsAfterContact >= 3)
            {
                AuthoritativeVehicleSnapshot authority = clientSnapshots[assignedVehicleId];
                AuthoritativeVehicleSnapshot observed = OwnedVehicle().CaptureSettledSnapshot(lastSnapshotTick);
                VehicleConvergence convergence = VehicleSnapshotRules.MeasureConvergence(authority, observed);
                if (convergence.PositionDistance > 0.10f ||
                    convergence.YawDifferenceDegrees > 2f ||
                    convergence.PlanarSpeedDifference > 0.25f)
                {
                    return;
                }
                completionSent = true;
                manager.ClientManager.Broadcast(new ClientCompleteMessage
                {
                    RunId = options.RunId,
                    ClientName = options.ClientName,
                    LastSnapshotTick = lastSnapshotTick,
                    MaximumRawError = OwnedVehicle().MaximumRawError,
                    MaximumVisualCorrection = OwnedVehicle().MaximumVisualCorrection,
                    ReplayCount = OwnedVehicle().ReplayCount,
                    FinalPositionError = convergence.PositionDistance,
                    FinalYawError = convergence.YawDifferenceDegrees,
                    FinalPlanarSpeedError = convergence.PlanarSpeedDifference
                });
                Log(
                    "CLIENT_COMPLETE_SENT",
                    $"snapshot_tick={lastSnapshotTick} raw_error={OwnedVehicle().MaximumRawError:F4} visual_correction={OwnedVehicle().MaximumVisualCorrection:F4} replay_count={OwnedVehicle().ReplayCount} final_position={convergence.PositionDistance:F4} final_yaw={convergence.YawDifferenceDegrees:F3} final_speed={convergence.PlanarSpeedDifference:F4}");
            }
        }

        private void Update()
        {
            if (!begun || finishing)
                return;
            if (options.Role == NetworkProcessRole.Client)
                UpdateRemotePresentation();
            if (!IsInteractive && Time.realtimeSinceStartup - startedAt > ProcessTimeoutSeconds)
                Finish(false, "process_timeout");
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started)
                return;

            serverReady = true;
            Log("SERVER_READY", $"port={options.Port} snapshot_hz=30 physics_hz=120");
        }

        private void OnServerRemoteConnectionState(
            NetworkConnection connection,
            RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                Log("CONNECTION_STARTED", $"connection={connection.ClientId}");
                return;
            }

            if (args.ConnectionState != RemoteConnectionState.Stopped)
                return;
            if (pendingJoins.Remove(connection.ClientId))
                connection.OnLoadedStartScenes -= OnConnectionLoadedStartScenes;
            if (!serverPeers.TryGetValue(connection.ClientId, out ServerPeer peer))
                return;

            peer.Jeep.AssignSession(0);
            Log("CONNECTION_STOPPED", $"connection={connection.ClientId} name={peer.Name}");
            if (peer.Complete)
                completedDisconnectCount++;
            if (completedCount == 2 && completedDisconnectCount == 2)
                Finish(true, "baseline_complete");
            if (options.Scenario == "reconnect" && !peer.Complete)
            {
                peer.Jeep.RemoveOwnership();
                peer.Jeep.StopAuthoritativeMotion();
                serverPeers.Remove(connection.ClientId);
                assignedNames.Remove(peer.Name);
                assignedCount--;
                manager.ServerManager.Broadcast(new AllPlayersReadyMessage
                {
                    RunId = options.RunId,
                    Ready = false
                });
                Log("SESSION_RELEASED", $"name={peer.Name} vehicle={peer.Jeep.VehicleId}");
            }
        }

        private void OnJoinRequest(
            NetworkConnection connection,
            JoinRequestMessage message,
            Channel channel)
        {
            if (!connection.LoadedStartScenes(true))
            {
                if (!pendingJoins.ContainsKey(connection.ClientId))
                {
                    pendingJoins.Add(connection.ClientId, message);
                    connection.OnLoadedStartScenes += OnConnectionLoadedStartScenes;
                    Log("JOIN_WAITING_FOR_SCENE", $"connection={connection.ClientId}");
                }
                return;
            }

            ProcessJoin(connection, message);
        }

        private void OnConnectionLoadedStartScenes(NetworkConnection connection, bool asServer)
        {
            if (!asServer || !pendingJoins.TryGetValue(connection.ClientId, out JoinRequestMessage message))
                return;

            connection.OnLoadedStartScenes -= OnConnectionLoadedStartScenes;
            pendingJoins.Remove(connection.ClientId);
            ProcessJoin(connection, message);
        }

        private void ProcessJoin(NetworkConnection connection, JoinRequestMessage message)
        {
            if (message.RunId != options.RunId ||
                (message.ClientName != "alpha" && message.ClientName != "bravo") ||
                serverPeers.ContainsKey(connection.ClientId) ||
                assignedNames.Contains(message.ClientName) ||
                assignedCount >= 2)
            {
                Log("JOIN_REJECTED", $"connection={connection.ClientId}");
                connection.Disconnect(true);
                return;
            }

            NetworkJeepController jeep = !firstVehicle.Owner.IsValid ? firstVehicle : secondVehicle;
            uint generation = nextSessionGeneration++;
            jeep.AssignSession(generation);
            jeep.GiveOwnership(connection);
            ServerPeer peer = new ServerPeer(connection, message.ClientName, jeep);
            if (IsLateJoin && assignedCount == 0)
                peer.Ready = true;
            serverPeers.Add(connection.ClientId, peer);
            assignedNames.Add(message.ClientName);
            assignedCount++;
            manager.ServerManager.Broadcast(connection, new VehicleAssignmentMessage
            {
                RunId = options.RunId,
                VehicleId = jeep.VehicleId,
                SessionGeneration = generation
            });
            Log(
                "OWNERSHIP_ASSIGNED",
                $"connection={connection.ClientId} name={message.ClientName} vehicle={jeep.VehicleId} generation={generation}");
            StartPredictionWhenAssigned();
            if (serverPredictionReady)
            {
                manager.ServerManager.Broadcast(connection, new PredictionReadyMessage
                {
                    RunId = options.RunId
                });
            }
            if (contactObserved)
            {
                manager.ServerManager.Broadcast(connection, new AuthoritativeContactMessage
                {
                    RunId = options.RunId,
                    ServerSimulationTick = contactTick,
                    FirstVehicleId = 1,
                    SecondVehicleId = 2,
                    FirstLinearVelocity = firstVehicle.CaptureSettledSnapshot(serverTick).LinearVelocity,
                    SecondLinearVelocity = secondVehicle.CaptureSettledSnapshot(serverTick).LinearVelocity
                });
            }
        }

        private void StartPredictionWhenAssigned()
        {
            int requiredAssignments = IsLateJoin ? 1 : 2;
            if (assignedCount < requiredAssignments ||
                serverPredictionReady ||
                predictionPrepareSent)
                return;

            predictionPrepareSent = true;
            if (IsLateJoin)
            {
                serverPredictionReady = true;
                firstVehicle.SetSimulationEnabled(true);
                secondVehicle.SetSimulationEnabled(true);
            }
            manager.ServerManager.Broadcast(new PredictionReadyMessage
            {
                RunId = options.RunId
            });
            Log(IsLateJoin ? "PREDICTION_READY" : "PREDICTION_PREPARED", "vehicles=2");
        }

        private void OnClientReady(
            NetworkConnection connection,
            ClientReadyMessage message,
            Channel channel)
        {
            if (!serverPeers.TryGetValue(connection.ClientId, out ServerPeer peer) ||
                message.RunId != options.RunId ||
                message.ClientName != peer.Name)
                return;
            bool becameReady = !peer.Ready;
            if (becameReady)
            {
                peer.Ready = true;
                Log("CLIENT_READY_ACCEPTED", $"name={peer.Name}");
            }
            if (!becameReady || assignedCount != 2)
                return;
            foreach (ServerPeer candidate in serverPeers.Values)
            {
                if (!candidate.Ready)
                    return;
            }

            if (!serverPredictionReady)
            {
                serverPredictionReady = true;
                firstVehicle.SetSimulationEnabled(true);
                secondVehicle.SetSimulationEnabled(true);
            }
            BroadcastAllPlayersReady(true);
            Log("PREDICTION_READY", "vehicles=2");
        }

        private void BroadcastAllPlayersReady(bool ready)
        {
            manager.ServerManager.Broadcast(new AllPlayersReadyMessage
            {
                RunId = options.RunId,
                Ready = ready
            });
        }

        private void OnInvalidAuthorityRequest(
            NetworkConnection connection,
            InvalidAuthorityRequestMessage message,
            Channel channel)
        {
            if (!serverPeers.TryGetValue(connection.ClientId, out ServerPeer peer) ||
                message.RunId != options.RunId)
                return;

            bool foreignVehicle = message.ClaimedVehicleId != peer.Jeep.VehicleId;
            bool validGeneration = message.ClaimedSessionGeneration == peer.Jeep.SessionGeneration;
            Log(
                "INVALID_AUTHORITY_REJECTED",
                $"connection={connection.ClientId} claimed_vehicle={message.ClaimedVehicleId} foreign={(foreignVehicle ? 1 : 0)} generation_match={(validGeneration ? 1 : 0)} authority_changed=0");
        }

        private void OnVehicleInput(
            NetworkJeepController jeep,
            VehicleInputValidationResult result)
        {
            ServerPeer peer = null;
            foreach (ServerPeer candidate in serverPeers.Values)
            {
                if (candidate.Jeep == jeep)
                {
                    peer = candidate;
                    break;
                }
            }
            if (peer == null)
                return;
            if (!result.Accepted)
            {
                rejectedInputCount++;
                Log(
                    "INPUT_REJECTED",
                    $"connection={peer.Connection.ClientId} reason={result.Rejection} sequence={result.Command.Sequence}");
                return;
            }

            if (!peer.InputAcceptedLogged)
            {
                peer.InputAcceptedLogged = true;
                Log(
                    "INPUT_ACCEPTED",
                    $"connection={peer.Connection.ClientId} vehicle={peer.Jeep.VehicleId} sequence={result.Command.Sequence}");
            }
        }

        private void PublishSettledSnapshots()
        {
            SnapshotBatchMessage batch = new SnapshotBatchMessage
            {
                ServerSimulationTick = serverTick,
                VehicleCount = 2,
                First = new VehicleSnapshotWire(firstVehicle.CaptureSettledSnapshot(serverTick)),
                Second = new VehicleSnapshotWire(secondVehicle.CaptureSettledSnapshot(serverTick))
            };
            manager.ServerManager.Broadcast(batch, true, Channel.Unreliable);
            snapshotBatchCount++;
            if (snapshotBatchCount % 15 == 0)
            {
                if (predictionPrepareSent && !serverPredictionReady)
                {
                    manager.ServerManager.Broadcast(new PredictionReadyMessage
                    {
                        RunId = options.RunId
                    });
                }
                else if (IsLateJoin &&
                         serverPredictionReady &&
                         assignedCount == 2 &&
                         !AllServerPeersReady())
                {
                    manager.ServerManager.Broadcast(new PredictionReadyMessage
                    {
                        RunId = options.RunId
                    });
                }
                else if (serverPredictionReady &&
                         assignedCount == 2 &&
                         AllServerPeersReady() &&
                         !BothAssignedInputsAccepted())
                {
                    BroadcastAllPlayersReady(true);
                }
            }
            if (snapshotBatchCount == 1 || snapshotBatchCount % 30 == 0)
            {
                Log(
                    "SNAPSHOT_BATCH",
                    $"tick={serverTick} vehicles=2 batch={snapshotBatchCount}");
            }
        }

        private void OnAuthoritativeContact(
            NetworkJeepController first,
            NetworkJeepController second)
        {
            if (contactObserved ||
                first.VehicleId == second.VehicleId ||
                assignedCount != 2 ||
                !BothAssignedInputsAccepted())
                return;

            AuthoritativeVehicleSnapshot firstState = first.CaptureSettledSnapshot(serverTick);
            AuthoritativeVehicleSnapshot secondState = second.CaptureSettledSnapshot(serverTick);
            float firstMovement = Vector3.Distance(firstStartPosition, firstVehicle.transform.position);
            float secondMovement = Vector3.Distance(secondStartPosition, secondVehicle.transform.position);
            bothVehiclesMoved = firstMovement > 0.5f && secondMovement > 0.5f;
            if (!bothVehiclesMoved)
                return;

            contactObserved = true;
            contactTick = serverTick;
            manager.ServerManager.Broadcast(new AuthoritativeContactMessage
            {
                RunId = options.RunId,
                ServerSimulationTick = serverTick,
                FirstVehicleId = first.VehicleId,
                SecondVehicleId = second.VehicleId,
                FirstLinearVelocity = firstState.LinearVelocity,
                SecondLinearVelocity = secondState.LinearVelocity
            });
            Log(
                "AUTHORITATIVE_CONTACT",
                $"tick={serverTick} first={first.VehicleId} second={second.VehicleId} first_moved={firstMovement:F3} second_moved={secondMovement:F3} first_speed={firstState.LinearVelocity.magnitude:F3} second_speed={secondState.LinearVelocity.magnitude:F3}");
        }

        private void OnClientComplete(
            NetworkConnection connection,
            ClientCompleteMessage message,
            Channel channel)
        {
            if (!serverPeers.TryGetValue(connection.ClientId, out ServerPeer peer) ||
                peer.Complete ||
                message.RunId != options.RunId ||
                message.ClientName != peer.Name)
            {
                Log("CLIENT_COMPLETE_REJECTED", $"connection={connection.ClientId}");
                return;
            }

            peer.Complete = true;
            peer.MaximumRawError = message.MaximumRawError;
            peer.MaximumVisualCorrection = message.MaximumVisualCorrection;
            peer.FinalPositionError = message.FinalPositionError;
            peer.FinalYawError = message.FinalYawError;
            peer.FinalPlanarSpeedError = message.FinalPlanarSpeedError;
            completedCount++;
            Log(
                "CLIENT_COMPLETE_ACCEPTED",
                $"name={peer.Name} snapshot_tick={message.LastSnapshotTick} raw_error={message.MaximumRawError:F4} visual_correction={message.MaximumVisualCorrection:F4} replay_count={message.ReplayCount} final_position={message.FinalPositionError:F4} final_yaw={message.FinalYawError:F3} final_speed={message.FinalPlanarSpeedError:F4}");
            if (completedCount != 2)
                return;

            bool predictionWithinLimits = true;
            foreach (ServerPeer candidate in serverPeers.Values)
            {
                bool rawErrorWithinLimits = options.Scenario == "stall" ||
                                            candidate.MaximumRawError <= 2f;
                predictionWithinLimits &= rawErrorWithinLimits &&
                                          candidate.MaximumVisualCorrection <= PredictionPresentationRules.MaximumCorrectionPerUpdate &&
                                          candidate.FinalPositionError <= 0.10f &&
                                          candidate.FinalYawError <= 2f &&
                                          candidate.FinalPlanarSpeedError <= 0.25f;
            }
            bool passed = assignedCount == 2 && contactObserved && bothVehiclesMoved && predictionWithinLimits;
            manager.ServerManager.Broadcast(new ScenarioCompleteMessage
            {
                RunId = options.RunId,
                Passed = passed,
                Reason = passed ? "baseline_complete" : "server_evidence_missing"
            });
            Log(
                "SERVER_EVIDENCE_COMPLETE",
                $"assigned={assignedCount} moved={(bothVehiclesMoved ? 1 : 0)} contact={(contactObserved ? 1 : 0)} rejected_inputs={rejectedInputCount} unauthorized_input_accepted=0");
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                connectedAt = Time.realtimeSinceStartup;
                Log("CLIENT_CONNECTED");
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped && !finishing)
                Finish(false, "client_disconnected_early");
        }

        private void OnClientAuthenticated()
        {
            manager.ClientManager.Broadcast(new JoinRequestMessage
            {
                RunId = options.RunId,
                ClientName = options.ClientName
            });
            Log("JOIN_SENT");
        }

        private void OnVehicleAssignment(VehicleAssignmentMessage message, Channel channel)
        {
            if (message.RunId != options.RunId || assignedVehicleId != 0)
                return;

            assignedVehicleId = message.VehicleId;
            assignedSessionGeneration = message.SessionGeneration;
            NetworkJeepController owned = OwnedVehicle();
            owned.AssignSession(assignedSessionGeneration);
            owned.ConfigureAsClientView(true);
            if (clientViews.TryGetValue(assignedVehicleId, out Rigidbody view))
            {
                IsometricFollowCamera camera = FindFirstObjectByType<IsometricFollowCamera>();
                if (camera != null)
                    camera.Configure(view.transform);
            }

            Log(
                "OWNERSHIP_ASSIGNED",
                $"vehicle={assignedVehicleId} generation={assignedSessionGeneration}");
        }

        private void OnPredictionReady(PredictionReadyMessage message, Channel channel)
        {
            if (message.RunId != options.RunId || assignedVehicleId == 0)
                return;

            if (IsLateJoin)
            {
                if (options.ClientName == "alpha")
                {
                    predictionStartAuthorized = true;
                    TryStartClientPrediction();
                    return;
                }
            }

            clientPrepareReceived = true;
            TrySendClientReady();
        }

        private void OnAllPlayersReady(AllPlayersReadyMessage message, Channel channel)
        {
            if (message.RunId != options.RunId)
                return;
            bool changed = allPlayersReady != message.Ready;
            allPlayersReady = message.Ready;
            if (allPlayersReady)
            {
                predictionStartAuthorized = true;
                TryStartClientPrediction();
            }
            if (changed)
                Log("ALL_PLAYERS_READY", $"ready={(allPlayersReady ? 1 : 0)} vehicles=2");
        }

        private void TryStartClientPrediction()
        {
            if (!predictionStartAuthorized ||
                predictionReady ||
                assignedVehicleId == 0 ||
                !firstCompleteSnapshot)
                return;
            predictionReady = true;
            OwnedVehicle().SetSimulationEnabled(true);
            Log("PREDICTION_READY", "response_ticks=1");
        }

        private void TrySendClientReady()
        {
            if (!clientPrepareReceived || assignedVehicleId == 0 || !firstCompleteSnapshot)
                return;
            manager.ClientManager.Broadcast(new ClientReadyMessage
            {
                RunId = options.RunId,
                ClientName = options.ClientName
            });
            if (!clientReadyLogged)
            {
                clientReadyLogged = true;
                Log("CLIENT_READY_SENT");
            }
        }

        private void OnSnapshotBatch(SnapshotBatchMessage message, Channel channel)
        {
            if (message.VehicleCount != 2 ||
                (lastSnapshotTick != 0 &&
                 !VehicleInputRules.IsNewer(message.ServerSimulationTick, lastSnapshotTick)))
            {
                return;
            }

            lastSnapshotTick = message.ServerSimulationTick;
            ApplySnapshot(message.First.ToSnapshot());
            ApplySnapshot(message.Second.ToSnapshot());
            if (assignedVehicleId != 0 && !firstCompleteSnapshot)
            {
                firstCompleteSnapshot = true;
                Log("FIRST_COMPLETE_SNAPSHOT", $"tick={lastSnapshotTick} vehicles=2");
                if (options.Scenario == "late_join" && options.ClientName == "bravo")
                {
                    Log(
                        "LATE_JOIN_READY",
                        $"elapsed_ms={(Time.realtimeSinceStartup - connectedAt) * 1000f:F1} tick={lastSnapshotTick} vehicles=2");
                }
                TryStartClientPrediction();
                TrySendClientReady();
            }

            float receivedAt = Time.realtimeSinceStartup;
            if (options.Scenario == "stall" &&
                lastSnapshotReceivedAt > 0f &&
                receivedAt - lastSnapshotReceivedAt >= 1f &&
                !staleHistorySkipped)
            {
                staleHistorySkipped = true;
                stallRecoveryTick = message.ServerSimulationTick;
                remotePresentationSnapshots.Clear();
                Log(
                    "STALE_HISTORY_SKIPPED",
                    $"gap_ms={(receivedAt - lastSnapshotReceivedAt) * 1000f:F1} newest_tick={stallRecoveryTick}");
            }
            lastSnapshotReceivedAt = receivedAt;
            if (staleHistorySkipped &&
                !stallRecoveryComplete &&
                VehicleInputRules.IsNewer(message.ServerSimulationTick, stallRecoveryTick + SnapshotIntervalTicks))
            {
                stallRecoveryComplete = true;
                Log("STALL_RECOVERY_COMPLETE", $"tick={message.ServerSimulationTick}");
            }

            if (contactObserved && VehicleInputRules.IsNewer(lastSnapshotTick, contactTick))
                snapshotsAfterContact++;
        }

        private void ApplySnapshot(AuthoritativeVehicleSnapshot snapshot)
        {
            clientSnapshots[snapshot.VehicleId] = snapshot;
            if (!clientViews.TryGetValue(snapshot.VehicleId, out Rigidbody body))
                return;
            if (snapshot.VehicleId == assignedVehicleId)
                return;

            NetworkJeepController remote = snapshot.VehicleId == 1 ? firstVehicle : secondVehicle;
            uint delayTicks = (uint)Mathf.CeilToInt(
                options.NetworkDelayMilliseconds * PredictionPresentationRules.PhysicsRate / 1000f);
            uint estimatedServerTick = snapshot.ServerSimulationTick + delayTicks * 2u;
            remote.SetRemoteAuthorityEstimate(
                snapshot,
                estimatedServerTick);
            remotePresentationSnapshots.Add(snapshot);
            if (remotePresentationSnapshots.Count > 4)
                remotePresentationSnapshots.RemoveAt(0);
        }

        private void UpdateRemotePresentation()
        {
            if (assignedVehicleId == 0 || remotePresentationSnapshots.Count < 2)
                return;

            AuthoritativeVehicleSnapshot newest =
                remotePresentationSnapshots[remotePresentationSnapshots.Count - 1];
            if (newest.ServerSimulationTick <= PredictionPresentationRules.PresentationDelayTicks)
                return;
            uint candidateTick = PredictionPresentationRules.RenderTick(newest.ServerSimulationTick);
            uint oldestTick = remotePresentationSnapshots[0].ServerSimulationTick;
            if (candidateTick != oldestTick &&
                !VehicleInputRules.IsNewer(candidateTick, oldestTick))
            {
                return;
            }
            if (lastRenderTick != 0 && !VehicleInputRules.IsNewer(candidateTick, lastRenderTick))
                candidateTick = lastRenderTick;
            lastRenderTick = candidateTick;

            AuthoritativeVehicleSnapshot older = remotePresentationSnapshots[0];
            AuthoritativeVehicleSnapshot newer = remotePresentationSnapshots[1];
            for (int index = 1; index < remotePresentationSnapshots.Count; index++)
            {
                newer = remotePresentationSnapshots[index];
                if (!VehicleInputRules.IsNewer(candidateTick, newer.ServerSimulationTick))
                    break;
                older = newer;
            }

            RemotePresentationSample sample = PredictionPresentationRules.Sample(
                older,
                newer,
                candidateTick);
            uint remoteVehicleId = assignedVehicleId == 1 ? 2u : 1u;
            NetworkJeepController remote = remoteVehicleId == 1 ? firstVehicle : secondVehicle;
            remote.SetRemotePresentation(sample);
            maximumSnapshotAgeMilliseconds = Mathf.Max(
                maximumSnapshotAgeMilliseconds,
                sample.SnapshotAgeMilliseconds);
            maximumSnapshotHeadroomMilliseconds = Mathf.Max(
                maximumSnapshotHeadroomMilliseconds,
                sample.HeadroomMilliseconds);
            switch (sample.Mode)
            {
                case RemotePresentationMode.Interpolate:
                    interpolationSeconds += Time.deltaTime;
                    break;
                case RemotePresentationMode.Extrapolate:
                    extrapolationSeconds += Time.deltaTime;
                    break;
                case RemotePresentationMode.Hold:
                    holdSeconds += Time.deltaTime;
                    break;
            }
        }

        private NetworkJeepController OwnedVehicle()
        {
            return assignedVehicleId == 1 ? firstVehicle : secondVehicle;
        }

        private void OnContactObserved(AuthoritativeContactMessage message, Channel channel)
        {
            if (message.RunId != options.RunId || contactObserved)
                return;

            contactObserved = true;
            contactTick = message.ServerSimulationTick;
            Log(
                "CONTACT_OBSERVED",
                $"tick={contactTick} first={message.FirstVehicleId} second={message.SecondVehicleId}");
        }

        private void OnScenarioComplete(ScenarioCompleteMessage message, Channel channel)
        {
            if (message.RunId != options.RunId || finishing)
                return;

            bool passed = message.Passed &&
                          assignedVehicleId != 0 &&
                          firstCompleteSnapshot &&
                          contactObserved &&
                          clientSnapshots.Count == 2;
            finishing = true;
            Log(
                "SCENARIO_RESULT",
                $"passed={(passed ? "true" : "false")} reason={message.Reason} snapshots={clientSnapshots.Count} presentation_delay_ms={PredictionPresentationRules.PresentationDelayMilliseconds:F0} snapshot_age_ms={maximumSnapshotAgeMilliseconds:F2} headroom_ms={maximumSnapshotHeadroomMilliseconds:F2} interpolate_ms={interpolationSeconds * 1000f:F1} extrapolate_ms={extrapolationSeconds * 1000f:F1} hold_ms={holdSeconds * 1000f:F1}");
            manager.ClientManager.StopConnection();
            StartCoroutine(QuitAfterNetworkStop(passed));
        }

        private void Finish(bool passed, string reason)
        {
            if (finishing)
                return;

            finishing = true;
            Log(
                "SCENARIO_RESULT",
                $"passed={(passed ? "true" : "false")} reason={reason} assigned={assignedCount} contact={(contactObserved ? 1 : 0)}");
            if (options.Role == NetworkProcessRole.Server && manager.ServerManager.Started)
            {
                if (!passed)
                {
                    manager.ServerManager.Broadcast(new ScenarioCompleteMessage
                    {
                        RunId = options.RunId,
                        Passed = false,
                        Reason = reason
                    });
                }
                manager.ServerManager.StopConnection(true);
            }
            else if (options.Role == NetworkProcessRole.Client && manager.ClientManager.Started)
            {
                manager.ClientManager.StopConnection();
            }

            StartCoroutine(QuitAfterNetworkStop(passed));
        }

        private bool BothAssignedInputsAccepted()
        {
            if (serverPeers.Count != 2)
                return false;

            foreach (ServerPeer peer in serverPeers.Values)
            {
                if (!peer.InputAcceptedLogged)
                    return false;
            }
            return true;
        }

        private bool AllServerPeersReady()
        {
            if (serverPeers.Count != 2)
                return false;
            foreach (ServerPeer peer in serverPeers.Values)
            {
                if (!peer.Ready)
                    return false;
            }
            return true;
        }

        private IEnumerator QuitAfterNetworkStop(bool passed)
        {
            yield return null;
            Application.Quit(passed ? 0 : 2);
        }

        private void Log(string eventName, string fields = "")
        {
            string role = options.Role == NetworkProcessRole.Server ? "server" : "client";
            string name = options.Role == NetworkProcessRole.Server ? "authority" : options.ClientName;
            Debug.Log($"CFNET event={eventName} role={role} name={name} run_id={options.RunId} {fields}".TrimEnd());
        }

        private sealed class ServerPeer
        {
            public ServerPeer(
                NetworkConnection connection,
                string name,
                NetworkJeepController jeep)
            {
                Connection = connection;
                Name = name;
                Jeep = jeep;
            }

            public NetworkConnection Connection { get; }
            public string Name { get; }
            public NetworkJeepController Jeep { get; }
            public bool InputAcceptedLogged { get; set; }
            public bool Complete { get; set; }
            public bool Ready { get; set; }
            public float MaximumRawError { get; set; }
            public float MaximumVisualCorrection { get; set; }
            public float FinalPositionError { get; set; }
            public float FinalYawError { get; set; }
            public float FinalPlanarSpeedError { get; set; }
        }
    }
}

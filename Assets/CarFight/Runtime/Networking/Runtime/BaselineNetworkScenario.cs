using System.Collections;
using System.Collections.Generic;
using CarFight.Driving;
using CarFight.Networking.Core;
using CarFight.Presentation;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace CarFight.Networking.Runtime
{
    /// <summary>
    /// Checkpoint 2B's deliberately narrow native-process proof. FishNet owns
    /// connection delivery; the server alone owns both live physics bodies.
    /// Prediction, replay history, smoothing, and launcher policy are later gates.
    /// </summary>
    public sealed class BaselineNetworkScenario : MonoBehaviour
    {
        public const uint SnapshotIntervalTicks = 4;
        private const float ProcessTimeoutSeconds = 25f;
        private const uint NeutralSettleTicks = 120;
        private const int GroundLayerMask = 1 << 2;

        private readonly Dictionary<int, ServerPeer> serverPeers = new Dictionary<int, ServerPeer>();
        private readonly HashSet<string> assignedNames = new HashSet<string>();
        private readonly Dictionary<uint, Rigidbody> clientViews = new Dictionary<uint, Rigidbody>();
        private readonly Dictionary<uint, AuthoritativeVehicleSnapshot> clientSnapshots =
            new Dictionary<uint, AuthoritativeVehicleSnapshot>();

        private NetworkManager manager;
        private NetworkLaunchOptions options;
        private NetworkJeepController firstVehicle;
        private NetworkJeepController secondVehicle;
        private Vector3 firstStartPosition;
        private Vector3 secondStartPosition;
        private float startedAt;
        private uint serverTick;
        private uint clientTick;
        private uint nextSessionGeneration = 1;
        private uint assignedVehicleId;
        private uint assignedSessionGeneration;
        private uint inputSequence;
        private uint lastSnapshotTick;
        private uint contactTick;
        private uint neutralTicks;
        private int assignedCount;
        private int completedCount;
        private int completedDisconnectCount;
        private int snapshotBatchCount;
        private int snapshotsAfterContact;
        private int rejectedInputCount;
        private bool begun;
        private bool sceneConfigured;
        private bool serverReady;
        private bool firstCompleteSnapshot;
        private bool contactObserved;
        private bool bothVehiclesMoved;
        private bool inputSentLogged;
        private bool completionSent;
        private bool finishing;

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

            Time.fixedDeltaTime = 1f / VehiclePhysicsProfile.PhysicsRate;
            if (options.Role == NetworkProcessRole.Server)
                StartServer();
            else
                StartClient();
        }

        private bool ConfigureSceneVehicles()
        {
            GameObject local = GameObject.Find("LocalJeep");
            GameObject collision = GameObject.Find("CollisionJeep");
            if (local == null || collision == null)
                return false;

            LocalJeepController localController = local.GetComponent<LocalJeepController>();
            if (localController != null)
                localController.enabled = false;

            Rigidbody localBody = local.GetComponent<Rigidbody>();
            Rigidbody collisionBody = collision.GetComponent<Rigidbody>();
            if (localBody == null || collisionBody == null)
                return false;

            if (options.Role == NetworkProcessRole.Server)
            {
                firstVehicle = local.AddComponent<NetworkJeepController>();
                secondVehicle = collision.AddComponent<NetworkJeepController>();
                firstVehicle.Configure(1, localBody, GroundLayerMask, OnAuthoritativeContact);
                secondVehicle.Configure(2, collisionBody, GroundLayerMask, OnAuthoritativeContact);
                firstStartPosition = localBody.position;
                secondStartPosition = collisionBody.position;
            }
            else
            {
                ConfigureClientView(1, localBody);
                ConfigureClientView(2, collisionBody);
            }

            sceneConfigured = true;
            return true;
        }

        private void ConfigureClientView(uint vehicleId, Rigidbody body)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = false;
            clientViews.Add(vehicleId, body);
        }

        private void StartServer()
        {
            manager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            manager.ServerManager.OnRemoteConnectionState += OnServerRemoteConnectionState;
            manager.ServerManager.RegisterBroadcast<JoinRequestMessage>(OnJoinRequest);
            manager.ServerManager.RegisterBroadcast<VehicleInputMessage>(OnVehicleInput);
            manager.ServerManager.RegisterBroadcast<ClientCompleteMessage>(OnClientComplete);
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
            manager.ClientManager.RegisterBroadcast<SnapshotBatchMessage>(OnSnapshotBatch);
            manager.ClientManager.RegisterBroadcast<AuthoritativeContactMessage>(OnContactObserved);
            manager.ClientManager.RegisterBroadcast<ScenarioCompleteMessage>(OnScenarioComplete);
            Log("CLIENT_CONNECTING", $"host={options.Host} port={options.Port}");
            if (!manager.ClientManager.StartConnection(options.Host, options.Port))
                Finish(false, "client_start_failed");
        }

        private void FixedUpdate()
        {
            if (!sceneConfigured || finishing)
                return;

            if (options.Role == NetworkProcessRole.Server)
                ServerFixedUpdate();
            else
                ClientFixedUpdate();
        }

        private void ServerFixedUpdate()
        {
            if (!serverReady)
                return;

            // At FixedUpdate entry the bodies contain the previous physics
            // step's settled result. Publish it before applying the next step.
            serverTick++;
            if (ShouldPublishSnapshot(serverTick))
                PublishSettledSnapshots();

            if (assignedCount == 2 && BothAssignedInputsAccepted())
            {
                firstVehicle.Simulate(serverTick, Time.fixedDeltaTime);
                secondVehicle.Simulate(serverTick, Time.fixedDeltaTime);
            }
        }

        private void ClientFixedUpdate()
        {
            if (assignedVehicleId == 0 || !firstCompleteSnapshot || !manager.ClientManager.Started)
                return;

            clientTick++;
            bool neutral = contactObserved;
            Vector2 cursor = Vector2.zero;
            if (!neutral)
                cursor = assignedVehicleId == 1 ? Vector2.down * 20f : Vector2.up * 20f;

            inputSequence++;
            manager.ClientManager.Broadcast(
                new VehicleInputMessage
                {
                    SessionGeneration = assignedSessionGeneration,
                    Sequence = inputSequence,
                    ClientSimulationTick = clientTick,
                    CursorOffset = cursor,
                    Burst = false,
                    Reverse = false
                },
                Channel.Unreliable);

            if (!inputSentLogged)
            {
                inputSentLogged = true;
                Log("INPUT_SENT", $"sequence={inputSequence} vehicle={assignedVehicleId}");
            }

            if (!contactObserved)
                return;

            neutralTicks++;
            if (!completionSent &&
                neutralTicks >= NeutralSettleTicks &&
                snapshotsAfterContact >= 3)
            {
                completionSent = true;
                manager.ClientManager.Broadcast(new ClientCompleteMessage
                {
                    RunId = options.RunId,
                    ClientName = options.ClientName,
                    LastSnapshotTick = lastSnapshotTick
                });
                Log("CLIENT_COMPLETE_SENT", $"snapshot_tick={lastSnapshotTick}");
            }
        }

        private void Update()
        {
            if (!begun || finishing)
                return;
            if (Time.realtimeSinceStartup - startedAt > ProcessTimeoutSeconds)
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

            if (args.ConnectionState != RemoteConnectionState.Stopped ||
                !serverPeers.TryGetValue(connection.ClientId, out ServerPeer peer))
            {
                return;
            }

            peer.Jeep.AssignSession(0);
            Log("CONNECTION_STOPPED", $"connection={connection.ClientId} name={peer.Name}");
            if (peer.Complete)
                completedDisconnectCount++;
            if (completedCount == 2 && completedDisconnectCount == 2)
                Finish(true, "baseline_complete");
        }

        private void OnJoinRequest(
            NetworkConnection connection,
            JoinRequestMessage message,
            Channel channel)
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

            NetworkJeepController jeep = assignedCount == 0 ? firstVehicle : secondVehicle;
            uint generation = nextSessionGeneration++;
            jeep.AssignSession(generation);
            ServerPeer peer = new ServerPeer(connection, message.ClientName, jeep);
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
        }

        private void OnVehicleInput(
            NetworkConnection connection,
            VehicleInputMessage message,
            Channel channel)
        {
            if (!serverPeers.TryGetValue(connection.ClientId, out ServerPeer peer))
            {
                rejectedInputCount++;
                Log("INPUT_REJECTED", $"connection={connection.ClientId} reason=unassigned");
                return;
            }

            VehicleInputValidationResult result = peer.Jeep.AcceptInput(message.ToCommand(), serverTick);
            if (!result.Accepted)
            {
                rejectedInputCount++;
                Log(
                    "INPUT_REJECTED",
                    $"connection={connection.ClientId} reason={result.Rejection} sequence={message.Sequence}");
                return;
            }

            if (!peer.InputAcceptedLogged)
            {
                peer.InputAcceptedLogged = true;
                Log(
                    "INPUT_ACCEPTED",
                    $"connection={connection.ClientId} vehicle={peer.Jeep.VehicleId} sequence={message.Sequence}");
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
            completedCount++;
            Log(
                "CLIENT_COMPLETE_ACCEPTED",
                $"name={peer.Name} snapshot_tick={message.LastSnapshotTick}");
            if (completedCount != 2)
                return;

            bool passed = assignedCount == 2 && contactObserved && bothVehiclesMoved;
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
                Log("CLIENT_CONNECTED");
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
            }

            if (contactObserved && VehicleInputRules.IsNewer(lastSnapshotTick, contactTick))
                snapshotsAfterContact++;
        }

        private void ApplySnapshot(AuthoritativeVehicleSnapshot snapshot)
        {
            clientSnapshots[snapshot.VehicleId] = snapshot;
            if (!clientViews.TryGetValue(snapshot.VehicleId, out Rigidbody body))
                return;
            body.position = snapshot.Position;
            body.rotation = snapshot.Rotation;
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
                $"passed={(passed ? "true" : "false")} reason={message.Reason} snapshots={clientSnapshots.Count}");
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
        }
    }
}

# Gate 2 implementation plan

Status: next work, approved direction

Updated: 2026-08-18

## Objective

Build the smallest native FishNet proof that preserves the accepted local Jeep
behavior while establishing trustworthy server authority. Work proceeds in
measured checkpoints. A later checkpoint may not begin until the previous one
has an automated gate and clean logs.

The binding acceptance limits remain in
[`MULTIPLAYER_ACCEPTANCE.md`](MULTIPLAYER_ACCEPTANCE.md). The package rationale
and Web risks remain in
[`NETWORKING_COMPARISON.md`](NETWORKING_COMPARISON.md).

## Architecture boundary

The pure gameplay layer continues to own FOLLOW math and vehicle-state
semantics. FishNet owns connections, tick delivery, replicate/reconcile history,
and transport. Thin adapters translate between them.

Do not build a second rollback or prediction framework beside FishNet. The game
may keep small transport-independent records for validation and telemetry, but
FishNet remains responsible for buffering and replaying replicate data.

```text
Input device or scripted client
             |
             v
      VehicleInputCommand         pure value + validation
             |
             v
      FishNet replicate adapter   connection, tick, delivery, replay
             |
             v
      LocalDriveSimulation        accepted FOLLOW behavior
             |
             v
      authoritative Rigidbody     server owns contacts and outcome
             |
             v
      VehicleSnapshot             settled state + acknowledged input
             |
       +-----+----------------+
       |                      |
       v                      v
owner reconciliation    remote presentation
```

No scene, prefab, or presentation code belongs in the pure contracts. No
client-submitted transform, velocity, or collision result crosses the authority
boundary.

## Checkpoint 2A — Pure network-facing contracts

Add transport-independent values under
`Assets/CarFight/Runtime/Networking/Core/`.

### `VehicleInputCommand`

Required fields:

- Connection generation/session epoch
- Monotonically increasing input sequence
- Client simulation tick
- FOLLOW cursor offset `(world X, world Z)`
- Burst and reverse state

Rules:

- Reject non-finite cursor values.
- Clamp cursor magnitude to `FollowController.MaxDistance` and report that a
  clamp occurred.
- Reject commands from a stale session generation.
- Reject duplicate or older sequences without applying them twice.
- Do not let missing input preserve an active command forever. The baseline
  policy becomes neutral input after a documented short grace window.

### `AuthoritativeVehicleSnapshot`

Required fields:

- Server simulation tick
- Stable vehicle ID
- Owner connection generation/session epoch
- Position and rotation
- Linear and angular velocity
- Last accepted input sequence

The snapshot represents settled post-physics state. A renderer-smoothed
transform or speculative client state must never populate it.

### Pure helpers

- Input validation and sequence acceptance
- Snapshot freshness ordering
- Acknowledgement comparison with unsigned wrap-safe ordering
- Stale-session rejection
- Convergence measurement for position, yaw, and planar speed

FishNet's own tick/replay history is not duplicated here. If a helper cannot be
tested without a FishNet object, it belongs in the adapter checkpoint instead.

### EditMode gate

Add focused tests for:

1. Finite in-range input acceptance
2. Cursor clamping at the accepted maximum
3. NaN/infinity rejection
4. Duplicate and old sequence rejection
5. Sequence wrap behavior
6. Stale-session rejection after reconnect
7. Snapshot newest-wins ordering
8. Settled snapshot round-trip value preservation
9. Position/yaw/speed convergence metrics

Checkpoint 2A passes when the new tests and all existing 25 tests pass. It does
not change the live scene or local controls.

## Checkpoint 2B — Baseline native topology

Build one executable that selects its role from command-line arguments:

```text
--server --port <port> --run-id <id>
--client --host <host> --port <port> --name alpha --script converge --run-id <id>
--client --host <host> --port <port> --name bravo --script converge --run-id <id>
```

The server runs headless. Both clients are separate native processes. A host
mode does not satisfy the gate.

### Runtime responsibilities

- `NetworkBootstrap` parses role arguments and starts exactly one FishNet role.
- Tugboat is the only installed/active transport.
- The server assigns a stable vehicle and new session generation to each
  accepted client.
- `NetworkJeepController` adapts FishNet input to the existing
  `LocalDriveSimulation` and authoritative Rigidbody.
- The server alone applies the authoritative drive step and owns all collision
  results.
- Clients send only validated input commands.
- A client cannot name another Jeep as its input target; ownership comes from
  the server connection mapping.
- The server samples settled Rigidbody state after physics and publishes the
  first fixed `30 Hz` snapshot control while physics stays at `120 Hz`.

Prediction is deliberately disabled in this checkpoint. The goal is to prove
connection, assignment, input authority, server movement, snapshot delivery,
two-Jeep contact, and shutdown before adding replay complexity.

### Baseline scripted scenario

1. Start the server and wait for an explicit `SERVER_READY` event.
2. Start `alpha`; wait for connection, ownership, and first complete snapshot.
3. Start `bravo`; wait for the same evidence.
4. Each client sends a fixed-seed scripted FOLLOW path that drives the Jeeps
   together.
5. The server logs authoritative contact and post-contact velocities.
6. Both clients observe both Jeeps and the contact outcome.
7. Both clients send neutral input and all three processes report a result.
8. Clients exit themselves; the server outlives them and then exits cleanly.

Passing requires exactly two assigned clients, movement from each owner's input,
one server-observed Jeep contact, no unauthorized input acceptance, current
snapshots on both clients, and no unhandled exceptions. Prediction and visual
smoothness are not judged yet.

## Checkpoint 2C — Repeatable launcher

Create one shared launcher library and a baseline gate rather than embedding
process control in gameplay code.

The launcher must:

- Probe an available port instead of assuming one is free.
- Create a unique run ID and log directory.
- Write `run.json` containing Git revision, build identity, arguments, seed,
  port, process IDs, and impairment settings.
- Wait for explicit readiness events instead of fixed startup sleeps.
- Keep `server.log`, `alpha.log`, and `bravo.log` separate.
- Track every process it starts and terminate only matching run IDs/process IDs.
- Never use blanket `pkill`, executable-name cleanup, or port-based cleanup.
- Treat launch failure and timeout as infrastructure results, not gameplay
  failures.
- Preserve logs and print their path on every failure.

Proposed entry point:

```sh
./scripts/multiplayer_test.sh baseline
```

Two concurrent dry or live invocations must not share ports, logs, or cleanup
targets.

## Checkpoint 2D — Owner prediction and reconciliation

Only after the baseline authority gate passes:

- Wrap `VehicleInputCommand` in FishNet replicate data.
- Use FishNet `PredictionRigidbody` for the owning Jeep.
- Create reconcile state only after the physics tick settles.
- Preserve server ownership of transforms, contacts, and collision outcomes.
- Reconcile to the authoritative snapshot and let FishNet replay unacknowledged
  input.
- Keep remote Jeeps out of local authoritative physics prediction.
- Add a fixed `75 ms` remote presentation control with a monotonic render
  timeline.
- Record raw prediction error, applied visual correction, replay count,
  snapshot age/headroom, and interpolation/extrapolation/hold time.

This checkpoint must meet the next-tick local response, `2.0 m` raw-error
ceiling, `0.25 m` per-render-update correction ceiling, and final convergence
limits from the acceptance specification.

## Checkpoint 2E — Impairment and lifecycle matrix

Extend the same baseline scenario without changing its scripted input:

- `120 ms` one-way latency
- `120 +/- 30 ms` deterministic jitter
- `120 ms` one-way plus deterministic `5%` loss
- Late join
- Disconnect/reconnect with a new session generation
- Invalid transform/velocity/foreign-input authority request
- `1.5 s` post-ready client stall and stale-history recovery

The impairment layer uses a fixed seed and reports actual forwarded, delayed,
reordered, and dropped counts. Each detector has a positive control. Join
warmup is measured separately rather than hidden by loosening steady-state
limits.

## Checkpoint 2F — Browser transport spike

Do not install Bayou, FishyUnityTransport, or adaptive presentation before the
native matrix passes.

The focused spike starts one authoritative server with one native client and
one browser client in the same world, then compares native and browser results
under counter-verified combined delay and loss. WSS setup, certificate/reverse
proxy behavior, reliable-channel queueing, and stale replaceable-state disposal
must be explicit.

Adaptive presentation remains optional. If tested, it is client-local, leaves
fixed `75 ms` as an adjacent control, changes no traffic, and requires
deterministic trace replay plus live A/B review.

## Required verification at every checkpoint

```sh
./scripts/test.sh
git diff --check
```

Run `./scripts/build.sh` whenever runtime networking, package configuration,
scene wiring, or launch behavior changes. Run the newest multiplayer gate for
every checkpoint after 2B.

Scene, GameObject, prefab, and Unity asset changes must be made through a ready
Pipeline-connected Editor when one is available. Run `unity status --format
json` first and rule out Safe Mode before any file-level fallback.

## Explicit non-goals

- Combat, weapons, drones, health, cloak, shield, or course transitions
- Production Jeep art or presentation tuning
- Linux deployment or always-on server operations
- Interest management for larger player counts
- Adaptive send cadence
- Client-authored transforms or collision outcomes
- A second custom rollback/prediction framework beside FishNet

## Immediate next action

Implement Checkpoint 2A only: pure network-facing values, validation/order
helpers, and EditMode tests. Stop after its tests pass and review the contracts
before changing the scene or starting the baseline topology.

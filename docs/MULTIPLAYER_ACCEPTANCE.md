# Gate 2 multiplayer acceptance specification

Status: accepted on 2026-08-17. FishNet `4.7.2` is selected for the proof; the
acceptance outcomes remain framework-independent.

## Purpose

Gate 2 proves that the accepted local FOLLOW and equal-mass vehicle physics can
support a server-authoritative game before more Car Fight systems are ported.
The proof is intentionally limited to one headless Unity server and two
independently launched native clients controlling two Jeeps.

This document defines observable outcomes. It does not prescribe a networking
package, RPC model, serialization library, or final production architecture.

## Evidence carried forward

The specification incorporates two older projects without copying their engine
architecture:

- Car Fight's Godot gate proved two authoritative cars and local prediction at
  `120 ms` one-way delay with raw correction error no greater than `2.0 m`.
- G2 proved the predict-local/render-remote model, post-simulation snapshot
  publication, deterministic impairment gates, stale-history recovery, and
  identity-scoped process cleanup. Its accepted remote stream used one
  recipient batch at `30 Hz` and a fixed `75 ms` presentation buffer.
- G2's unmerged adaptive-presentation branch is useful design evidence, not a
  selected Car Fight feature. It keeps adaptation client-local, preserves a
  fixed control, changes no authority or send cadence, uses a monotonic render
  cursor, and distinguishes network arrival variation from render hitches.

Unity PhysX cannot use G2's cross-process deterministic rollback assumption.
Car Fight therefore carries forward the authority, presentation, measurement,
and harness lessons while relying on snapshots and reconciliation.

## Fixed contracts

- The server is the only authority for Jeep transforms, linear and angular
  velocity, physics contacts, collision response, spawn/despawn, and ownership.
- A client owns only the input stream for its assigned Jeep. It never submits a
  transform, velocity, contact, or collision result for authority.
- The server runs the existing `120 Hz` physics contract and uses the existing
  `VehiclePhysicsProfile`, `FollowController`, and `LocalDriveSimulation`
  behavior. Network send rates may be lower than the physics rate.
- Local prediction may immediately simulate the owning client's input, but
  every predicted state remains provisional until acknowledged by a server
  snapshot.
- Remote Jeeps are presented from authoritative snapshots. They are not
  independently simulated as authoritative bodies by clients.
- Reconciliation replays unacknowledged inputs from an authoritative state.
  Unity PhysX is not assumed to be deterministic across processes or machines.
- The server publishes only settled post-physics state. It never publishes a
  speculative step, a client prediction, or a presentation-smoothed transform.
- A reconnect creates a new connection session. A stale session cannot retain
  ownership or continue supplying input.

## Minimum data contracts

The implementation may encode these differently, but the proof must expose the
same information for tests and logs.

### Input command

- Connection/session identifier
- Monotonically increasing input sequence
- Client simulation tick
- FOLLOW cursor offset `(world X, world Z)`, clamped to the accepted maximum
- Burst and reverse state

The server validates ownership, sequence freshness, finite numeric values, and
the accepted cursor range. Missing input must resolve to a documented safe
policy; it must not grant continued arbitrary movement forever.

### Authoritative vehicle snapshot

- Server simulation tick
- Stable vehicle identity and current owner/session
- Position and rotation
- Linear and angular velocity
- Last processed input sequence for the owning client

Snapshots must be sufficient for local reconciliation, remote interpolation,
late-join initialization, and convergence measurements without consulting a
client-authored transform.

The first implementation should evaluate a fixed `30 Hz` batched snapshot
stream while simulation remains at `120 Hz`. The send rate must remain
configurable for an adjacent control; it must not adapt in response to client
pressure during the foundation proof.

### Required metrics

Each process writes machine-searchable events containing its role and name.
The combined logs must expose:

- Connection ready, ownership assignment, disconnect, reconnect, and despawn
- Input sequence sent, accepted, rejected, and acknowledged
- Server contact and authoritative post-contact state
- Snapshot tick and vehicle count
- Prediction error before correction
- Presented correction applied per rendered update
- Reconciliation replay count
- Remote interpolation, extrapolation, and hold duration
- Snapshot sequence gaps, age, and buffer headroom
- Render-frame hitches kept distinct from network arrival variation
- Per-client final divergence from the server
- Clean shutdown and scenario result

Simulation divergence is measured from tick-aligned simulation states, never
from a smoothed display transform. Telemetry that names a simulation tick must
be emitted from a context that knows the actual simulated tick; replay work
must not stamp several resimulated states with one live-frame tick.

Every important detector needs a positive control. For example, the authority
rejection scenario must prove that the invalid request reached the server, and
the prediction-error gate must deliberately inject one bounded disagreement to
prove the measurement can observe and clear it. A quiet log alone is not proof.

## Repeatable topology

One command must start exactly these named game processes:

1. One headless authoritative server
2. Native client `alpha`
3. Native client `bravo`

A test-only network impairment process is allowed and does not count as a game
client. Every process gets its own log. The launcher records exact process IDs,
waits for readiness instead of relying only on fixed sleeps, enforces a timeout,
and terminates only the processes it started. A failed run preserves logs and
prints their directory.

Each run writes a manifest containing the Git revision, build identity, command
arguments, random seed, selected ports, impairment settings, and process IDs.
Readiness comes from an explicit server/client event, not a fixed startup sleep.
Cleanup uses a unique run identity in addition to tracked process IDs and never
kills by executable name or port alone.

Ports and log directories must be overrideable so two test runs cannot silently
target each other. A run fails if an expected process exits early, if a third
game client joins, or if any log contains an unhandled exception.

## Quantitative acceptance limits

These are initial proof limits. Changing one requires a recorded decision made
before adapting an implementation merely to pass a failing result.

| Measure | Limit |
| --- | --- |
| Client input response | Predicted motion begins on the next local physics tick; it does not wait for a server round trip |
| Handshake on the local test machine | Both requested clients become ready within `10 s` |
| Raw owning-client prediction error | Maximum `2.0 m` in the impairment matrix, preserving the Godot gate ceiling |
| Normal presented correction | No single rendered update moves the local Jeep more than `0.25 m` solely for reconciliation |
| Remote presentation hold | No continuous hold longer than `100 ms` after join warmup in the standard impairment matrix |
| Recovery after input stops | Position error at or below `0.10 m` and planar-speed error at or below `0.25 m/s` within `1.0 s` |
| Final client/server position divergence | At or below `0.10 m` per Jeep after the settling window |
| Final client/server yaw divergence | At or below `2 degrees` per Jeep after the settling window |
| Late-join readiness | Full two-Jeep authoritative state visible within `2.0 s` of connection readiness |
| Reconnect recovery | Replacement session owns exactly one Jeep and sees full state within `3.0 s`; no ghost Jeep remains |

A hard correction/teleport is allowed only for an explicitly logged recovery
condition outside the normal error ceiling. Any hard correction during the
standard scenario matrix fails the proof.

Connection/bootstrap samples are reported but excluded from steady-state
ceilings until a documented join-warmup condition is met. The late-join and
client-stall scenarios own that excluded window so it cannot become an
untested blind spot.

## Scenario matrix

All scenarios use scripted, repeatable input and finish with a settling window
in which both clients send neutral input. Unless a row says otherwise, both
clients must become ready, own one Jeep each, observe both Jeeps, and pass the
final divergence limits.

| Scenario | Network condition | Required evidence |
| --- | --- | --- |
| Baseline LAN | No injected latency, jitter, or loss | Both Jeeps move from owner input, make server-observed contact, separate, and converge on both clients |
| Latency | `120 ms` one-way delay | Owning Jeeps respond without waiting for round trip; contact remains server-owned; correction and final divergence remain within limits |
| Jitter | `120 ms` one-way base with deterministic `+/-30 ms` jitter | Input acknowledgements and snapshots may arrive unevenly without ownership errors, hard corrections, or failed convergence |
| Packet loss | `120 ms` one-way delay with deterministic `5%` packet loss | The input delivery policy tolerates loss, both Jeeps continue responding, and the run converges without a hard correction |
| Late join | `alpha` drives before `bravo` connects | `bravo` receives the current authoritative two-Jeep state rather than default spawn history and meets the late-join limit |
| Disconnect/reconnect | Disconnect `bravo` while `alpha` continues, then launch a replacement `bravo` session | The stale session loses authority, the server applies the documented despawn/retention policy, no ghost remains, and the replacement meets the reconnect limit |
| Invalid authority request | A test client submits a transform/velocity mutation or input for the other Jeep | The server rejects and logs it; authoritative state is not changed by the request |
| Client stall recovery | At `120 ms` one-way delay, pause one client for `1.5 s` after snapshots and prediction are live | Stale history is skipped once, newest complete authority restores the client, logging does not flood, and normal heartbeat/convergence resumes |

The impairment source must use a fixed seed and report its actual delayed,
dropped, and forwarded packet counts. Packet loss must affect both directions or
the test must state and justify a narrower direction.

## Collision proof

The baseline, latency, jitter, and loss scenarios must drive the two accepted
equal-mass sphere bodies into each other. Passing requires all of the following:

- The server reports the contact and owns both post-contact velocities.
- Both clients observe the contact outcome from server state.
- Neither client can author a contact or choose the other Jeep's resulting
  velocity.
- After neutral input, both clients meet the final position, yaw, and speed
  limits for both Jeeps.
- The existing local real-PhysX momentum-exchange and wall-containment tests
  continue to pass unchanged.

The clients do not need identical intermediate PhysX trajectories. The required
result is responsive prediction followed by bounded correction and convergence
to the server's outcome.

## Presentation proof

Authority and presentation are tested separately. The fixed baseline starts
with a `75 ms` remote buffer and the candidate `30 Hz` authoritative batch. It
must use a monotonic render timeline: correction cannot reverse time, replay a
long queue of stale positions, or feed a presentation transform back into
server physics.

The proof records interpolation, extrapolation, hold, buffer headroom, snapshot
age, correction distance, and render hitches. A newest-complete snapshot may
replace stale queued work after a bounded recovery condition; the client must
not visibly step through every obsolete state after a stall.

Adaptive presentation is optional follow-up work, not a Gate 2 dependency. If
evaluated, it must be a pure client-local policy over existing received data,
leave the fixed mode available as an adjacent control, emit no feedback probes,
preserve the same wire cadence, raise delay faster than it lowers it, and never
reverse the render cursor. It is accepted only through deterministic trace
replay plus adjacent fixed/adaptive live review.

## Web transport decision gate

The first proof uses native clients, but package selection must document its Web
path before installation. For each candidate, record:

- Its supported browser transport and whether secure browser hosting changes
  the required endpoint or certificate setup
- Compatibility with a native/Linux headless authoritative server
- Whether native and browser clients can share a server endpoint and protocol
- Whether native and browser clients can inhabit one authoritative world
- Relay, hosted-service, or vendor-account requirements
- Local automation support for latency, jitter, loss, late join, and reconnect
- Package maturity, maintenance status, licensing, and lock-in

A candidate fails this gate if the Web route depends on an unverified promise,
requires clients to become authoritative for physics, or prevents the repeatable
local three-process proof.

G2 showed why a successful clean browser connection is insufficient: WSS/TCP
could connect but accumulated stale work under combined delay and loss, while
WebRTC behaved differently. The selected Unity path must therefore be exercised
through counter-verified shaping, with latency and loss combined, and must show
that stale replaceable state is discarded rather than queued behind reliable
traffic. HUD-reported latency or configured loss without advancing transport
counters is not evidence. Native and browser results must be reported
separately even when they share application protocol and server authority.

## Package comparison and implementation order

After this specification is accepted:

1. Compare only the smallest viable networking candidates against every fixed
   contract and scenario above.
2. Record the selected package and rejected alternatives in `DECISIONS.md`,
   including the Web transport findings.
3. Add pure input/snapshot/history contracts and unit tests before scene wiring.
4. Build the headless server and two scripted clients with baseline LAN only.
5. Add prediction, acknowledgement, replay, and presentation smoothing.
6. Add the deterministic impairment harness and complete the scenario matrix.
7. Run the existing EditMode and macOS build gates unchanged.

## Gate 2 completion

Gate 2 is accepted only when the full matrix passes from one repeatable command,
the selected package decision is recorded, the existing `scripts/test.sh` and
`scripts/build.sh` gates still pass, and a live native-client review confirms
that normal reconciliation under `120 ms` one-way delay does not visibly snap.

Combat, production Jeep assets, remaining driving behavior, and presentation
tuning stay out of scope until this gate is accepted.

# Checkpoint 2F — Next-session task

Continue work on `~/Projects/car-fight-unity`.

Pull the project and `claude-comms` repositories. Read:

- `AGENTS.md`
- `.ai/CONTEXT.md`
- `.ai/CURRENT_PHASE.md`
- `docs/GATE2_IMPLEMENTATION_PLAN.md`
- `docs/MULTIPLAYER_ACCEPTANCE.md`

Implement Checkpoint 2F only: the focused native/browser transport spike.
Start from accepted project commit `318f2cb`. Preserve the accepted Checkpoint
2B–2E authority, FishNet prediction, lifecycle, impairment, and presentation
contracts.

The final topology must contain exactly:

- one authoritative server
- one native client
- one WebGL/browser client

Never launch extra clients. Run automated scenarios sequentially and clean up
only the exact processes launched by each test.

## Increment 2F-A — Transport selection

- Verify the Unity and FishNet versions used by the project.
- Research current compatibility of Bayou, FishyUnityTransport, and any simpler
  suitable option.
- Recommend the smallest maintained transport that supports the required native
  and WebGL topology.
- Document its version, installation source, limitations, and why the rejected
  options are unsuitable.
- Do not change gameplay or authority code.
- Verify the project still compiles and its existing tests pass.

**Human review gate:** Present the comparison and recommendation. Wait for the
user to say `next` before installing a new dependency.

## Increment 2F-B — Minimal mixed connection

- Install and configure the approved browser transport.
- Build the smallest proof with one authoritative server, one native client,
  and one WebGL/browser client.
- Prove both clients connect to the same server and receive distinct peer and
  session identities.
- Make HTTP/WSS ports, certificate behavior, reverse-proxy requirements, and
  launch commands explicit.
- Do not change scenes, prefab topology, driving presentation, or authority
  rules.
- Add focused automated checks where practical.

**Human review gate:** Demonstrate the minimal connection and relevant logs.
Wait for `next` before adding Jeep ownership or movement.

## Increment 2F-C — Jeep ownership and movement

- Prove the native and browser clients join the same world.
- Assign a distinct Jeep to each client.
- Prove each client controls only its assigned Jeep.
- Prove both clients observe both Jeeps moving and receive authoritative
  collision results.
- Keep the server authoritative over transforms, velocity, ownership, and
  collisions.
- FishNet must remain the only prediction and replay-history owner. Do not add a
  second rollback, replay, or tick-history system.
- Do not change combat, production Jeep assets, or presentation design.

**Human review gate:** Pause as soon as both visible clients can join and move.
Provide a hands-on test immediately, using only the required topology. Review
authority, movement, collision, and convergence evidence before proceeding.
Wait for `next` before adding impairment.

## Increment 2F-D — Impairment and queue behavior

- Run the native/browser proof under counter-verified combined latency and
  loss.
- Report actual forwarded, delayed, reordered, and dropped counts.
- Make reliable-channel queueing behavior explicit.
- Prove stale replaceable state is discarded instead of building an unbounded
  reliable backlog.
- Preserve fixed `75 ms` presentation as the adjacent control. Do not add
  adaptive presentation.
- Verify final authoritative convergence after impairment.

**Human review gate:** Present the impairment counters, queue behavior,
stale-state evidence, and convergence results. Wait for `next` before final
acceptance work.

## Increment 2F-E — Hands-on test and final acceptance

- Prepare one simple hands-on launch using only the authoritative server,
  native client, and browser client.
- Clearly state what the user should see and the controls.
- Do not open additional clients.
- Run `./scripts/test.sh`.
- Run the focused native/browser acceptance test.
- Run `git diff --check`.
- Review the implementation for unnecessary complexity and remove debug-only
  complexity that is not required for the proof.
- Update `.ai/CURRENT_PHASE.md` with final evidence and the next checkpoint.
- Commit and push all scoped project and `claude-comms` changes.
- Stop after Checkpoint 2F passes and summarize what is ready to test next.

**Human review gate:** Present the final hands-on test and acceptance evidence
before declaring Checkpoint 2F complete.

## Review cadence and durable handoffs

Do not work through multiple review gates in one uninterrupted run. Do not code
for more than roughly 30 minutes or one substantial change set without human
review. If an increment grows larger, split it and stop at the next safe,
testable boundary.

At every review point:

1. Stop coding and summarize what changed, what passes, important decisions,
   known problems, unfinished work, and exact files changed.
2. Update `.ai/CURRENT_PHASE.md` with evidence and artifact paths, current
   limitations, commits created, the next increment, and the exact next command.
3. Run the focused tests relevant to the increment.
4. Provide a short hands-on procedure whenever visible behavior is available.
5. Shut down automated processes unless they are intentionally left open for
   the hands-on test.
6. Commit and push only coherent, passing increments. Do not commit knowingly
   broken code merely to reach a review point.
7. Ensure working trees are clean or explicitly document remaining files.
8. Wait for the user to say `next` before continuing.

Do not interrupt the user for trivial implementation details. Human review
belongs at meaningful design decisions and testable behavior boundaries.

If context compacts, resume from `.ai/CURRENT_PHASE.md` and Git history. Do not
repeat completed increments or relaunch tests that already have valid recorded
evidence. Ask only when credentials, destructive work, or a material
architecture choice outside this scope genuinely requires user direction.

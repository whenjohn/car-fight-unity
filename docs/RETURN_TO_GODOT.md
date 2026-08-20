# Engine decision: return Car Fight to Godot

Updated: 2026-08-19

## Decision

Active Car Fight development returns to the Godot repository at
`/Users/johnnguyen/Projects/car-fight`. This Unity repository is preserved as a
completed engine, rendering, workflow, and networking investigation. Do not
continue the Unity browser-transport diagnosis or port more gameplay unless a
later explicit engine decision supersedes this one.

This does not mean the Unity work had no value. Its native authoritative
FishNet proof established useful authority, prediction, reconciliation,
impairment, late-join, reconnect, telemetry, and process-isolation contracts.
Carry those observable requirements back to Godot; do not port the Unity scene,
FishNet integration, or transport stack.

## Why the decision changed

### The platform benefit is narrow and has a bounded Godot policy

Unity Metal was more resilient than Godot on the affected Intel Mac: tested
Unity players recovered from presentation stalls without reproducing Godot's
WindowServer restart or Vulkan kernel panic. Unity did not eliminate the Intel/
macOS display-timestamp warning family, however, so this was a reduction in
failure severity rather than a generally clean rendering result.

The Godot investigation isolated the strongest activation boundary to exact
edge-to-edge coverage of the built-in Intel display. Ordinary windowed Godot
sessions produced no known matching failure, while native fullscreen and an
edge-to-edge mode-0 borderless window reproduced the precursor. Car Fight can
therefore use a conservative compatibility policy on affected macOS Intel
systems: ordinary decorated windowed presentation inside the usable desktop
area, with native fullscreen, borderless fullscreen, and exact edge-to-edge
maximization disabled. This limitation affects a small hardware segment and is
acceptable for the project.

### Unity's sustainable workflow conflicts with the required workflow

The project deliberately required a CLI-first workflow and the user does not
want day-to-day development to depend on a persistent Unity Editor or its MCP/
Pipeline connection. Unity's efficient scene, asset, import, compile, and play
workflow nevertheless assumes a warm Editor. The recorded cold build timings
were 254.647 seconds for the first template build, 123.12 seconds after package
cache invalidation, and 48.93 seconds for an unchanged repeat. Later native plus
WebGL transport iterations required full rebuilds; the user observed roughly
15-minute edit-to-review cycles for small changes.

That cost applies to ordinary development, not only to the affected Intel Mac.
Godot already provides a much shorter edit, test, launch, and headless-network
loop for this project. Faster iteration is a primary project requirement, not a
minor convenience.

### The Unity browser path added fragile integration rather than removing risk

The native FishNet foundation was successful: the authoritative server, two
native clients, prediction/reconciliation, latency, jitter, loss, late join,
reconnect, invalid-authority, and client-stall scenarios passed their recorded
gates. The unresolved risk is the required mixed native/browser topology.

That path combined FishNet, Multipass, Tugboat, a project-owned fork of the
unreleased and dormant FishyWebRTC adapter, an old Unity WebRTC prerelease,
WebGL JavaScript interop, HTTP signaling, two data channels, and Chrome process
management. The apparent successful run `run.znAk69` was not reproducible from
tracked source: it combined a newer native player with a stale generated WebGL
build. Fresh rebuilt runs consistently reached WebRTC `CLIENT_CONNECTED` but
did not complete FishNet authentication, ownership, or input. The precise
evidence is preserved in `docs/CHECKPOINT_2F_REPRODUCTION_FAILURE.md`.

Continuing would require maintaining and diagnosing a custom compatibility
transport before the rest of Car Fight could be ported. Unity's broader online
service ecosystem does not justify that cost: authentication, lobbies,
matchmaking, relay, and dedicated-server orchestration can be added to a Godot
game through engine-neutral services. They are separate from Car Fight's
gameplay authority, prediction, and snapshot transport.

### Unity rendering headroom is not currently a Car Fight requirement

URP, Shader Graph, profiling, the Asset Store, and Unity's larger production
ecosystem are real advantages. Car Fight's current stylized arena, grid,
vehicles, shadows, course, and presentation effects are already implemented in
Godot and do not require that additional rendering headroom. The Unity port
would continue paying migration and workflow cost long before those features
became necessary.

### The working game is already in Godot

The Godot repository contains the accepted driving feel, combat, shield,
course, assets, networking behavior, crash monitoring, and regression suite.
The Unity repository contains a smaller reconstruction and an incomplete
browser transport proof. Returning now avoids recreating proven gameplay while
preserving the useful Unity acceptance evidence.

## Active direction

1. Resume gameplay development in `/Users/johnnguyen/Projects/car-fight`.
2. Keep rendered development on the affected Intel Mac in ordinary decorated
   windowed mode, inside the usable desktop area.
3. Do not repeat known-risk Godot fullscreen, edge-to-edge, ANGLE, or Vulkan
   experiments merely to reconfirm the platform boundary.
4. Preserve this Unity repository at its final investigation state. Do not
   delete its tests, logs, transport fork, or decision evidence.
5. Reuse the Unity multiplayer acceptance criteria where they improve the
   Godot/netfox tests, especially authority rejection, lifecycle recovery,
   measured impairment, reproducible launch identity, and browser queue
   evidence.
6. Add accounts, lobbies, matchmaking, relay, or hosting only when the game
   needs them. Evaluate engine-neutral services separately from the gameplay
   transport.

## Reconsider Unity only if

Reopen the engine decision only for a concrete requirement that outweighs the
iteration cost, such as a committed console/mobile production path, a Unity-only
middleware dependency, rendering requirements that the selected Godot renderer
cannot meet, a larger Unity-centered team, or a shipping requirement for
fullscreen on the affected Intel display configuration. General ecosystem
headroom by itself is not enough.

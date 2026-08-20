# Car Fight Unity

> **Project status:** Active development returned to the Godot repository on
> 2026-08-19. This Unity-native reconstruction is preserved as an engine,
> workflow, rendering, and multiplayer investigation. Do not continue the port
> unless a later explicit decision supersedes
> [the return-to-Godot decision](docs/RETURN_TO_GODOT.md).

This repository reconstructed part of Car Fight without carrying over Godot
scenes, nodes, RPCs, or package architecture. Its accepted native multiplayer
evidence remains useful, while its mixed native/browser transport gate is not
reproducible from tracked source.

## Foundation

- Unity `6000.3.22f1` LTS, Universal Render Pipeline, Intel/x86_64 editor
- Official Unity CLI and `com.unity.pipeline`
- Pure C# FOLLOW driving math with EditMode tests
- Playable local mouse-driving slice with a primitive Jeep and isometric camera
- macOS desktop first; Web clients and a Linux authoritative server are later
- `MaximizedWindow` by default, ordinary `Windowed` fallback on affected Intel Macs
- Auto-installed display safety guard with lightweight lifecycle diagnostics;
  deep macOS monitoring remains external test tooling

## Local controls

- Mouse position: FOLLOW direction and throttle distance
- Space: burst
- Tab: reverse

The stationary orange Jeep ten metres ahead is an equal-mass physics target for
normal and burst-speed collision checks. It will move when struck; only the
green Jeep reads local input.

## Commands

```sh
./scripts/test.sh
./scripts/build.sh
./scripts/verify.sh
```

The Codex MCP server is named `unity_car_fight`. It can run alongside the
separate `unity_fullscreen_spike` server; each server targets its own absolute
project path. Restart Codex after changing MCP configuration.

## Historical migration order

1. Lock down FOLLOW behavior as pure C#. *(complete)*
2. Add a local Jeep, camera, ground, and input adapter. *(complete)*
3. Prove a native authoritative server with two clients, prediction,
   reconciliation, latency, late join, and reconnect. *(native proof complete)*
4. Prove the mixed native/browser transport. *(stopped: not reproducible from
   tracked source)*
5. Port the remaining driving, collision, course, combat, and presentation
   behavior. *(cancelled by the engine decision)*

See [docs/GODOT_REFERENCE.md](docs/GODOT_REFERENCE.md) and the shared `.ai/`
project context for the evidence and decision trail.

The historical Gate 2 acceptance contract is preserved in
[docs/MULTIPLAYER_ACCEPTANCE.md](docs/MULTIPLAYER_ACCEPTANCE.md). The accepted
native networking comparison and FishNet selection are recorded in
[docs/NETWORKING_COMPARISON.md](docs/NETWORKING_COMPARISON.md).
The now-superseded implementation checkpoints are in
[docs/GATE2_IMPLEMENTATION_PLAN.md](docs/GATE2_IMPLEMENTATION_PLAN.md).

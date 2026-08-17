# Car Fight Unity

A clean Unity-native reconstruction of Car Fight. The Godot project remains the
behavioral reference; this repository ports its proven game rules and tests
without carrying over Godot scenes, nodes, RPCs, or package architecture.

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

## Commands

```sh
./scripts/test.sh
./scripts/build.sh
./scripts/verify.sh
```

The Codex MCP server is named `unity_car_fight`. It can run alongside the
separate `unity_fullscreen_spike` server; each server targets its own absolute
project path. Restart Codex after changing MCP configuration.

## Migration order

1. Lock down FOLLOW behavior as pure C#. *(complete)*
2. Add a local Jeep, camera, ground, and input adapter. *(complete)*
3. Prove a native authoritative server with two clients, prediction,
   reconciliation, latency, late join, and reconnect.
4. Port the remaining driving, collision, course, combat, and presentation
   behavior in focused tested slices.

See [docs/GODOT_REFERENCE.md](docs/GODOT_REFERENCE.md) and the shared `.ai/`
project context for the evidence and decision trail.

# Car Fight Unity

Read `.ai/CONTEXT.md` and `.ai/CURRENT_PHASE.md` before making changes. Update
`CURRENT_PHASE.md`, commit, and push before wrapping up.

This repository is a clean Unity-native reconstruction of Car Fight. Port
observable behavior and tests from the Godot reference, not its engine
architecture. Keep deterministic gameplay math in pure C# and put Unity input,
physics, presentation, and networking behind adapters.

Use Unity `6000.3.22f1` LTS (`x86_64`) through the official CLI and Pipeline.
Gameplay forward is `-Z`, and FOLLOW cursor offsets are `(world X, world Z)`.
Run `scripts/test.sh` and `scripts/build.sh` before shipping a milestone.

On affected Intel Macs, default to `MaximizedWindow`, keep `Windowed` as the
fallback, and avoid `FullScreenWindow`. The Codex MCP entry for this project is
`unity_car_fight`; other Unity projects have their own named entries.

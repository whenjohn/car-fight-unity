# Godot behavioral reference

The source project is `whenjohn/car-fight`, preserved at migration checkpoint
`240d7bb` on `master`. Its `MIGRATION_TO_UNITY.md` is the canonical index of the
fullscreen investigation and the behavior that should move to Unity.

Local references on the primary development machine:

- `/Users/johnnguyen/Projects/car-fight`
- `/Users/johnnguyen/Projects/car-fight/MIGRATION_TO_UNITY.md`
- `/Users/johnnguyen/Projects/car-fight/player/follow_controller.gd`
- `/Users/johnnguyen/Projects/car-fight/tests/follow_controller_test.gd`
- `/Users/johnnguyen/Projects/unity-mac-fullscreen-spike/.ai/PLATFORM_CONCLUSIONS.md`

Port the formulas, constants, observable outcomes, test scenarios, arena
measurements, authority decisions, and licensed source assets. Rebuild GDScript,
nodes, autoloads, RPCs, spawning, prediction plumbing, UI, shaders, scenes, and
build configuration in Unity-native form.

Do not rerun the dangerous Godot edge-to-edge, native fullscreen, ANGLE, or
Vulkan probes. Their evidence is archived. Unity's presentation policy is
`MaximizedWindow` by default, `Windowed` fallback, and no `FullScreenWindow` on
affected Intel Macs unless a future explicitly approved test changes that rule.

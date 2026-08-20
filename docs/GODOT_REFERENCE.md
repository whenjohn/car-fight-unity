# Godot behavioral reference

> **Historical port reference:** Active development returned to Godot on
> 2026-08-19. This document preserves the mapping used by the Unity
> investigation; it is not an active port plan.

The source project is `whenjohn/car-fight`. Revision `240d7bb` is the gameplay
checkpoint from which the Unity reconstruction began; later commits supersede
the engine handoff while preserving that gameplay state. Its
`MIGRATION_TO_UNITY.md` is the canonical engine-decision history.

Local references on the primary development machine:

- `/Users/johnnguyen/Projects/car-fight`
- `/Users/johnnguyen/Projects/car-fight/MIGRATION_TO_UNITY.md`
- `/Users/johnnguyen/Projects/car-fight/player/follow_controller.gd`
- `/Users/johnnguyen/Projects/car-fight/tests/follow_controller_test.gd`
- `docs/UNITY_MAC_FULLSCREEN_CONCLUSIONS.md`

The Unity investigation ported formulas, constants, observable outcomes, test
scenarios, arena measurements, authority decisions, and licensed source assets.
Its Unity-specific scenes and integration are now preserved rather than active.

The accepted local Unity slice preserves the Godot presentation measurements:
an `84` half-extent arena, `1` metre fine grid, `4` metre major grid, `42` metre
orthographic view height, `1.55` vehicle collision radius, `2.2` vehicle mass,
and the source background/grid/player palette. Unity's orthographic camera uses
a half-height value, so Godot camera size `42` is represented as `21` in Unity.

Do not rerun the dangerous Godot edge-to-edge, native fullscreen, ANGLE, or
Vulkan probes. Their evidence is archived. The active Godot policy on affected
Intel Macs is an ordinary decorated window inside the usable desktop area; do
not use native fullscreen, borderless fullscreen, or exact edge-to-edge
maximization.

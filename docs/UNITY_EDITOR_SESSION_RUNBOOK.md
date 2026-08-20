# Unity Editor session runbook

This project uses Unity `6000.3.22f1` (`x86_64`) and the Pipeline/MCP entry
named `unity_car_fight`. The preferred workflow is to drive the already-open
Editor; do not launch a second Editor for inspection or scene authoring.

## Start a session

1. From the project directory, read `.ai/CONTEXT.md` and `.ai/CURRENT_PHASE.md`.
2. Confirm the CLI is available:

   ```sh
   which unity
   unity --version
   ```

3. Check the CLI connection:

   ```sh
   unity status --format json
   ```

4. If the GUI Editor is open but the CLI reports `STATUS_NO_INSTANCES`, use the
   configured `unity_car_fight` MCP connection. Its editor-status call should
   report `status: ready`, project path
   `/Users/johnnguyen/Projects/car-fight-unity`, and the Unity version above.
   This happened in the car-fight-unity 4 session: the direct CLI did not see
   the GUI instance, while the project-specific MCP connection did.

5. Inspect the live scene before making changes. The expected active scene is
   `Assets/CarFight/Scenes/Bootstrap.unity` (`Bootstrap`). Use the live
   hierarchy/scene-view inspection, not hand-edited Unity YAML.

## Applying the playable-scene builder

The checked-in builder is:

```text
Assets/CarFight/Editor/PlayableSliceBuilder.cs
```

When its existing presentation fix needs to be applied, execute the Unity
Editor menu item through Pipeline:

```text
Car Fight/Rebuild Local Driving Slice
```

The builder recreates and saves `Bootstrap.unity`, the Jeep materials, the
physics profile, and the required prediction graphical-object references. Wait
for the Editor to return to `ready`/not compiling, then inspect the hierarchy.
The corrected Jeep hierarchy includes `PrimitiveJeep/ChassisLean`, the body
pieces, `WheelModel` with four steer/spin assemblies, and `VehicleVisualAnimator`.

## Important Play-mode distinction

Pressing the Unity Play button with no command-line network role currently does
**not** provide a valid local driving review. FishNet disables the scene Jeep
roots when no server or client is running. The visible blue cube is the cyan
cursor marker at the arena origin; it is not the Jeep and does not mean the
builder lost the Jeep mesh.

For an interactive review, use the new launcher command. It starts one
headless authoritative server and one visible `alpha` client:

```sh
./scripts/multiplayer_test.sh play
```

Drive the visible client window. Press `Ctrl-C` in the launching terminal to
stop the exact server/client pair. The run directory contains `server.log`,
`client.log`, `run.json`, and `result.json`.

For the automated proof, launch the checked-in baseline scenario, which starts
one server and two named headless clients with separate logs:

```sh
./scripts/multiplayer_test.sh baseline
```

Do not start extra Unity Editors or manually kill unrelated FishNet/Unity
processes. The launcher owns its exact processes and ports.

## Connection and recovery problems encountered

- **CLI says no instances:** keep the GUI Editor open and use the configured
  `unity_car_fight` MCP entry. Do not start another Editor just to make the CLI
  status command non-empty.
- **Pipeline commands time out or disappear:** check for Safe Mode and compile
  errors first (`unity pipeline list`, Editor console, and recompile status).
  Pipeline is unavailable while the Editor is in Safe Mode.
- **Scene changes do not appear:** confirm the MCP project path and active scene,
  then use the builder/menu through Pipeline. Do not edit `.unity`, `.prefab`, or
  `.asset` YAML while a live Editor is connected.
- **Play view shows only a blue cube:** this is the no-network-role FishNet
  lifecycle behavior described above. Stop treating it as a geometry fix; start
  the approved server/client scenario or fix the local-play lifecycle explicitly
  in a separate, reviewed change.

## End of session

Stop Play mode before handing the Editor back, leave the working tree
uncommitted when requested, and report the exact scene, Editor connection state,
review mode, and any known limitation. Update `.ai/CURRENT_PHASE.md` only when
the phase itself changed.

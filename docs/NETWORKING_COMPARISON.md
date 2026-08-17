# Gate 2 networking comparison

Status: FishNet `4.7.2` selected for the multiplayer foundation proof.

Reviewed: 2026-08-17

## Decision criteria

The selected framework must support the accepted
[`MULTIPLAYER_ACCEPTANCE.md`](MULTIPLAYER_ACCEPTANCE.md) contract without moving
gameplay authority to clients or replacing the existing GameObject/Rigidbody
slice. The binding requirements are:

- Dedicated headless server with server-owned Rigidbody state and contacts
- Immediate owning-client prediction with ticked input replay and reconciliation
- Predicted external forces and primitive collision callbacks
- Independently configurable simulation, tick, and snapshot presentation rates
- Native macOS clients now and a credible browser path into one authoritative
  world later
- Self-hosting without mandatory per-user service fees
- Sufficient hooks for deterministic launch, impairment, and convergence gates

## Result

| Candidate | Prediction and Rigidbody fit | Native/headless | Browser path | Verdict |
| --- | --- | --- | --- | --- |
| FishNet 4.7.2 | Built-in ticked replicate/reconcile flow, `PredictionRigidbody`, post-tick reconciliation, and predicted primitive collision callbacks | Dedicated server documented; default Tugboat transport supports macOS/Linux/native UDP | Separate Bayou WebSocket transport or FishyUnityTransport; Multipass can place Web and native clients in one server world | Selected for the proof |
| Unity Netcode for GameObjects 2.7 | Server-authoritative `NetworkRigidbody`, but Unity explicitly states that NGO does not provide full client prediction/reconciliation | First-party Unity 6 GameObject package with dedicated-server support | Unity Transport can provide a browser-capable path | Rejected: the project would have to build the highest-risk prediction/replay layer itself |
| Mirror 96.10 | `PredictedRigidbody` exists, but Mirror labels prediction as research and documents that its approach sacrifices accuracy and may not fit complex physics | Mature dedicated-server model; KCP/native transports | Built-in WebSocket transport and Multiplex can share a server with native clients | Rejected for this proof: collision-heavy predicted physics is not yet a stable project guarantee |

## Why FishNet is the smallest viable choice

FishNet's prediction API maps directly onto the project boundary. A replicate
structure carries the existing FOLLOW input, while reconcile state carries the
authoritative Rigidbody state. FishNet's own Rigidbody guide sends reconcile
state after physics in `OnPostTick`, matching the accepted settled-state rule.
`PredictionRigidbody` records velocity and force changes for resimulation, and
`NetworkCollision` exists for collision enter/stay/exit during prediction.

The framework remains server-authoritative and supports a dedicated Unity
server. Tugboat provides the initial native transport and FishNet includes a
transport-level latency simulator. The Car Fight gates will still use an
external, seeded impairment layer where actual forwarding/drop counters are
required.

The package is pinned to Git tag `4.7.2`, commit
`de19b5d66459f60400ffd0edc443c4da173a01e7`, rather than following its moving
default branch.

Unity resolved that exact hash into `Packages/packages-lock.json`. FishNet's
editor integration generated an empty `Assets/DefaultPrefabObjects.asset`
registry and the `FISHNET;FISHNET_V4` standalone scripting defines. With the
package installed, all `25/25` existing EditMode tests pass and the macOS
x86_64 player build succeeds.

## Web path and unresolved proof

FishNet core does not make Tugboat browser-compatible. The documented Web path
uses the separately installed Bayou WebSocket transport, and Multipass lets a
server listen through different transports while each client uses one. A
FishyUnityTransport alternative uses Unity Transport and can select WebSockets.

This is credible enough to select FishNet, but it is not accepted browser
evidence. Before Gate 2 closes, a focused spike must prove:

1. One authoritative server accepts one native and one browser client in the
   same world.
2. WSS certificate/reverse-proxy deployment is explicit and repeatable.
3. Combined delay and loss are counter-verified rather than inferred from a
   configured simulator or latency display.
4. Replaceable snapshots do not build a stale TCP/WebSocket queue behind
   reliable lifecycle traffic.
5. Native and browser transport behavior are reported separately.

Bayou or FishyUnityTransport will not be installed until that focused transport
spike begins. Tugboat is sufficient for the native proof.

## License and maintenance risk

FishNet is source-available under its own license, not an OSI-standard license.
It grants games worldwide, no-charge, royalty-free use and modification, while
restricting competing networking products. That is acceptable for Car Fight,
but the exact license must remain recorded with dependency upgrades.

The selected release supports Unity 6 and was the latest published FishNet
release at review time. Upgrades are deliberate checkpoints: read release notes,
keep the current tag as the rollback point, and rerun the complete Gate 2 matrix
before changing versions.

## Primary references

- [FishNet 4.7.2 release](https://github.com/FirstGearGames/FishNet/releases/tag/4.7.2)
- [FishNet prediction overview](https://fish-networking.gitbook.io/docs/guides/features/prediction)
- [FishNet predicted Rigidbody code flow](https://fish-networking.gitbook.io/docs/guides/features/prediction/creating-code/controlling-an-object)
- [FishNet PredictionRigidbody](https://fish-networking.gitbook.io/docs/guides/features/prediction/predictionrigidbody)
- [FishNet predicted collision callbacks](https://fish-networking.gitbook.io/docs/fishnet-building-blocks/components/prediction/network-collider)
- [FishNet dedicated server](https://fish-networking.gitbook.io/docs/tutorials/simple/building-a-dedicated-server)
- [FishNet Bayou Web transport](https://fish-networking.gitbook.io/docs/fishnet-building-blocks/transports/bayou)
- [FishNet Multipass](https://fish-networking.gitbook.io/docs/guides/features/transports/multipass)
- [FishNet license](https://github.com/FirstGearGames/FishNet/blob/main/LICENSE.md)
- [Unity NGO package version](https://docs.unity3d.com/Manual/com.unity.netcode.gameobjects.html)
- [Unity NGO client anticipation limitation](https://docs-multiplayer.unity3d.com/netcode/2.1.1/advanced-topics/client-anticipation/)
- [Mirror client-side prediction](https://mirror-networking.gitbook.io/docs/manual/general/client-side-prediction)
- [Mirror transports and dedicated modes](https://mirror-networking.gitbook.io/docs/manual/components/network-manager)
- [Mirror Multiplex browser/native transport](https://mirror-networking.gitbook.io/docs/manual/transports/multiplex-transport)

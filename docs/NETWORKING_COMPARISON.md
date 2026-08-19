# Gate 2 networking comparison

Status: FishNet `4.7.2` selected for the multiplayer foundation proof.

Reviewed: 2026-08-17

Checkpoint 2F-A browser review: 2026-08-18

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

## Checkpoint 2F-A browser transport review

The project is pinned to Unity `6000.3.22f1` x86_64 and FishNet `4.7.2` at
commit `de19b5d66459f60400ffd0edc443c4da173a01e7`. Only the macOS playback
engine is currently installed; Web Build Support is not. No browser transport
has been installed during this review.

| Candidate | Current evidence | Cost and risk | 2F-A result |
| --- | --- | --- | --- |
| Bayou `4.1.5` plus built-in Multipass | FirstGearGames' documented WebGL transport. Multipass explicitly supports Tugboat and Bayou clients in one server world on separate ports. Tag `4.1.5` is commit `c98af7b7d2507e2c8f285f1a6cd44915eb04a662`, published 2025-09-14 under MIT. | One small transport package and the WebGL editor module. WebSocket delivery is reliable and ordered even when FishNet labels a payload unreliable, so combined delay/loss can expose head-of-line stale state. Unity 6's optional `Target WebAssembly 2023` path has an unresolved Bayou JavaScript issue and must remain off for the first control unless upstream resolves it. G2's verified WSS experiment also exhausted its outbound buffer and was unplayable under combined delay/loss. | Rejected as the primary Car Fight browser path after human review. Keep only as a fallback connection control if the WebRTC compatibility spike fails for a bounded, documented reason. |
| FishyUnityTransport `2.0.0-pre.2` | Community FishNet adapter over Unity Transport. It supports native UDP and WebGL WebSockets; tag `2.0.0-pre.2` is commit `08756d9733a556018041e52fcb9d2a6035346aeb`, published 2025-04-18 under MIT. | Adds Unity Transport plus a prerelease adapter. Its package declares Unity Transport `1.3.1`, while its WebGL instructions require `2.0.0+`. An open issue reports failure to connect with FishNet `4.6.2`, which is older than this project's `4.7.2`. Using it for native traffic would also replace an accepted Tugboat control; using Multipass would be no smaller than Bayou. | Rejected for this spike. Its browser gameplay path is still WebSocket-based and does not address the G2 failure mode. |
| FishyWebRTC at commit `beec5b480e21f3ac280b849455980b94f312408f` | FishNet's documentation says this transport works with current FishNet. Its two WebRTC DataChannels are reliable/ordered and unreliable/unordered with zero retransmits. Multipass can retain Tugboat for the native client. G2 independently proved that browser gameplay should use WebRTC DataChannels while WS/WSS carries signaling only. | Community adapter has no releases and was last pushed in 2023. It requires a narrow compatibility port: its README pins Unity WebRTC `3.0.0-pre.5`, its package manifest points to a now-missing fork, its browser plug-in uses legacy Unity JavaScript calls, and its old instructions modify FishNet's assembly definition. | **Recommended only as a bounded compatibility spike, pending human approval.** Pin the source, use the official Unity WebRTC package, keep adapter changes project-owned, and stop for review if it cannot compile and connect without a transport rewrite. |

### Recommended installation boundary

Human review corrected the initial Bayou recommendation after carrying forward
G2's measured transport result. G2's WSS server exhausted its outbound buffer
and was unacceptable under verified combined delay/loss. Its WebRTC path kept
gameplay on DataChannels and used WS/WSS only for signaling; measured `5%` loss
was playable. WebRTC still accumulated stale work under combined mild
delay/loss until the application added batching, newest-complete-state
fast-forward, and bounded recovery. Car Fight must test the same boundary rather
than infer success from a clean connection.

After human approval, Increment 2F-B should:

1. Install Unity Web Build Support for the pinned `6000.3.22f1` x86_64
   Editor.
2. Pin FishyWebRTC source commit
   `beec5b480e21f3ac280b849455980b94f312408f` as compatibility-source input,
   not as an unreviewed moving dependency. Pin the official Unity WebRTC
   `3.0.0-pre.5` package first because that is the only version the adapter
   claims to support. Do not use its missing `cakeslice/com.unity.webrtc` fork.
3. Keep compatibility edits in a project-owned adapter assembly that references
   `FishNet.Runtime` and `SimpleWebRTC`. Do not edit FishNet package-cache files
   or its pinned assembly definition.
4. Keep Tugboat and use FishNet's already-included Multipass. The native client
   stays on Tugboat/UDP; the browser client uses WebRTC DataChannels; both join
   the same authoritative server world.
5. Use local HTTP only for the first signaling control. For HTTPS browser
   hosting, terminate HTTPS at an explicit reverse proxy and forward signaling
   to the adapter's HTTP endpoint. Gameplay must remain on WebRTC rather than
   WSS. Record STUN/TURN and ICE behavior explicitly.
6. Preserve separate reliable/ordered and unreliable/unordered, zero-retransmit
   DataChannels. Instrument channel buffered bytes and snapshot age. Combined
   delay/loss must prove newest-complete state replaces stale work rather than
   stepping through a backlog.

The first compile/build after installation is a compatibility gate. Limit the
port to current FishNet transport API names, a project-owned assembly boundary,
and the smallest Unity 6 JavaScript interop correction. If native server plus
one WebGL client cannot connect within that boundary, stop for human review.
Do not write a new WebRTC stack or change the accepted authority, prediction,
scene, or Tugboat contracts merely to keep this candidate alive.

### 2F-A primary evidence

- [FishNet Bayou documentation](https://fish-networking.gitbook.io/docs/fishnet-building-blocks/transports/bayou)
- [FishNet Multipass documentation](https://fish-networking.gitbook.io/docs/guides/features/transports/multipass)
- [Bayou `4.1.5` release](https://github.com/FirstGearGames/Bayou/releases/tag/4.1.5)
- [Bayou Unity 6 WebAssembly 2023 issue](https://github.com/FirstGearGames/Bayou/issues/20)
- [FishyUnityTransport repository](https://github.com/ooonush/FishyUnityTransport)
- [FishyUnityTransport newer-FishNet connection issue](https://github.com/ooonush/FishyUnityTransport/issues/28)
- [Unity Transport WebGL documentation](https://docs.unity.cn/Packages/com.unity.transport%402.3/manual/websockets.html)
- [FishNet FishyWebRTC documentation](https://fish-networking.gitbook.io/docs/fishnet-building-blocks/transports/fishywebrtc)
- [FishyWebRTC repository](https://github.com/cakeslice/FishyWebRTC)
- [Unity WebRTC `3.0.0-pre.5`](https://github.com/Unity-Technologies/com.unity.webrtc/tree/3.0.0-pre.5)

Local G2 evidence carried forward during human review:

- `/Users/johnnguyen/Projects/g2/.ai/DECISIONS.md`, D-049 and its adverse-network
  correction
- `/Users/johnnguyen/Projects/g2/docs/webrtc-network-verdict.md`

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

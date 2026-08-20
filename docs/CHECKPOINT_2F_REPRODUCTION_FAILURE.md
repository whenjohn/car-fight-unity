# Checkpoint 2F browser reproduction failure

Updated: 2026-08-19

## Status

Checkpoint 2F is **not currently reproducible from tracked source** and must not
be treated as accepted. The historical successful review at
`TestResults/browser-review/run.znAk69/` was real, but it depended on a mixed
generated state: the native player used the newer FishyWebRTC Unity-6 server
changes while the WebGL output was a stale build from the older `75441f6`
browser revision. That generated combination was later overwritten and was
never made durable in source.

The visible blue cube is the cursor marker. It appears because both clients are
input-gated until both players are authenticated and ready; it is a symptom of
the failed browser join, not a missing Jeep model.

## Reproduction evidence

The historical successful run `run.znAk69` contains all required evidence:

- server ownership for alpha and bravo;
- `JOIN_SENT`, `OWNERSHIP_ASSIGNED`, `PREDICTION_READY`, and `INPUT_SENT` from
  both clients;
- server input acceptance and an authoritative contact observed by both
  clients.

Three subsequent runs did not reproduce it:

| Run | Source/build state | Result |
| --- | --- | --- |
| `run.jub1rV` | Fresh native and WebGL outputs from the embedded package baseline | Alpha joined; bravo reached transport `CLIENT_CONNECTED` but never emitted `JOIN_SENT` |
| `run.uylufU` | Added data-channel-open gating in WebGL and re-woke native send queues on `RTCDataChannel.OnOpen` | Same failure; the server repeatedly reported broadcasts skipped for an unauthenticated connection |
| `run.ZOrvAF` | Also dispatched server `RTCDataChannel.Send` onto Unity's main thread | Interrupted at the user's request after the same failure remained visible; no bravo join or ownership event had appeared |

The repeated boundary is precise:

1. Native alpha connects, sends its join, and receives vehicle 1.
2. Browser bravo completes WebRTC signaling and logs `CLIENT_CONNECTED`.
3. The server logs transport `CONNECTION_STARTED` for connection 1.
4. Bravo never logs FishNet `JOIN_SENT` because FishNet authentication never
   completes.
5. The server never assigns bravo and continues identifying connection 1 as
   unauthenticated.

This proves that a successful WebRTC connection event is not sufficient. The
unresolved fault is between the browser's initial FishNet authentication packet
and completion of the server/client authentication exchange.

## Changes attempted in the working tree

The FishyWebRTC package is embedded at `Packages/FishyWebRTC/`, and
`Packages/manifest.json` references it with `file:FishyWebRTC`. The current
uncommitted package changes:

- keep the Unity WebRTC lifecycle pump native-only so WebGL retains its older
  lifecycle behavior;
- wait for both browser data channels to be truly open before reporting the
  client connected;
- refuse a JavaScript send when the selected channel is not open;
- wake server send loops again when each native data channel opens;
- dispatch native `RTCDataChannel.Send` calls onto Unity's main thread.

These are diagnostic/fix attempts, not an accepted solution. The last item was
only observed in the interrupted `run.ZOrvAF` attempt and did not produce a
browser join before shutdown.

`scripts/browser_network_review.sh` is also stricter in the working tree. It
cannot report `status=ready` merely because two transport connections exist; it
now requires server ownership plus `INPUT_SENT` from alpha and bravo.

Both macOS and WebGL builds completed successfully from the embedded source.
The EditMode suite reported 50/51 passing: all 23 networking tests passed; the
only failure belongs to the separate uncommitted presentation pass
(`HardHighSpeedBrakePitchesForwardButOrdinaryBrakingDoesNot`, expected
`-18`, actual `-17.64`).

## Do not repeat next session

Do not rebuild and relaunch the same configuration again. The next investigation
must first instrument the authentication packet boundary:

1. Log the first few browser `SendRTC` channel/length calls and data-channel
   receive callbacks.
2. Log the first few FishyWebRTC server data-channel receives and sends with
   connection ID, channel, and payload length.
3. Establish whether bravo's FishNet version packet leaves JavaScript, reaches
   `Connection.OnMessage`, reaches `ServerSocket._server_onData`, and whether
   the authentication reply reaches the browser callback.
4. Compare those packet events against the historical `75441f6` WebGL behavior
   before changing lifecycle or presentation code again.
5. Keep the strengthened ownership/input gate; only a run with both ownership
   and input events may be handed to the user for play testing.

## Cleanup and preservation

The interrupted server, native client, Web server, browser, and console-capture
processes were stopped. Ports 7770, 8080, and 9222 had no remaining listeners
after cleanup. No Unity Editor is running.

`stash@{0}` (`codex-preserve-before-ef0180c8-verification`) was not applied,
dropped, overwritten, or otherwise changed. The unrelated presentation working
tree changes were preserved.

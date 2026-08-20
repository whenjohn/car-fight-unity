# Unity macOS fullscreen platform conclusions

> **Historical evidence:** Copied from `unity-mac-fullscreen-spike` at revision
> `c7d0819` before its local checkout was retired. Its recommendation to choose
> Unity is superseded by [`RETURN_TO_GODOT.md`](RETURN_TO_GODOT.md). The measured
> presentation results remain valid evidence; the former Unity migration and
> next-proof recommendations below are retained as historical context.

## Preserved platform conclusion from 2026-08-17

Updated: 2026-08-17

## Decision

For this game, Unity is currently the safer development choice than Godot when Intel Mac support matters. The controlled Unity runs repeatedly reached the same family of macOS/Intel display-timestamp warnings seen during the Godot investigation, but Unity recovered from presentation stalls and never produced the repeatable macOS system crash observed with Godot.

This is evidence of greater resilience, not proof that Unity can never crash the machine. Most Unity tests ran sequentially on the same unrebooted display session at the user's direction, so they are order/state-confounded rather than clean-boot qualification.

Recommended presentation policy:

1. Use Unity `MaximizedWindow` as the default no-border fullscreen mode.
2. Offer ordinary `Windowed` mode as the safest fallback.
3. Avoid `FullScreenWindow` on affected Intel Macs when possible.
4. Do not claim the underlying Intel/macOS display defect is eliminated.

## Fullscreen evidence

The exact combined Jeep-plus-procedural geometry workload ran for 600 active seconds in each mode. Fullscreen modes also received a 360-second post-exit display-service watch.

| Mode | Resolution reported by Unity | FPS average | Worst post-startup FPS | Post-startup samples below 50 FPS | WindowServer CPU average / maximum | Timestamp errors | Framebuffer events | Outcome |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `FullScreenWindow` | 2880 x 1800 | 57.07 overall | 3.72 | 44 | 63.65% / 135.5% | 143 | 2 | No crash; CPU-resource diagnostic |
| `MaximizedWindow` | 2880 x 1800 | 56.93 overall | 13.57 | 37 | 57.11% / 126.7% | 163 | 0 | No crash or diagnostic |
| `Windowed` | 1440 x 900 | 59.03 after transition | 55.87 | 0 | 28.19% / 94.1% | 49, transition only | 0 | No crash or diagnostic |

All three runs exited status 0. WindowServer remained PID 52286 throughout. No run produced a VBlank timeout, GPU reset, event-port death, watchdog timeout, panic, or system crash.

The borderless `FullScreenWindow` run came closest to the Godot failure pattern. It produced sustained presentation degradation, two Intel framebuffer `Not Ready for Transaction Processing` events, and `/Library/Logs/DiagnosticReports/WindowServer_2026-08-16-205743_JBook2.cpu_resource.diag`. The diagnostic attributed heavy work to Metal display compositing into the Intel Metal driver. One framebuffer event occurred about 271 seconds after Unity exited, close to the roughly 278-second delayed Godot crash timing, but WindowServer still did not restart.

Evidence directories:

- `.probe-runs/20260816-204904-stage2-jeep-plus-procedural`
- `.probe-runs/20260816-211409-stage2-jeep-plus-procedural-maximized-window`
- `.probe-runs/20260816-213119-stage2-jeep-plus-procedural-windowed`

## Lit and shadowed Jeep result

The corrected visible Jeep presentation tested a more game-like rendering path in `MaximizedWindow`: Standard lit material, directional lighting, high-quality soft real-time shadows, shadow receiving on the Jeep and ground, and shadow casting from the Jeep.

- Metal `MaximizedWindow`, 2880 x 1800
- 600 active seconds; no post-exit watch for this visual demonstration
- Exact one-surface Jeep: 1,323 vertices, 2,118 indices, 706 triangles
- 601 samples averaging 50.41 FPS
- 53.16 FPS at exit
- 18.09 FPS minimum after startup
- Three post-startup samples below 30 FPS and 166 below 50 FPS
- WindowServer CPU average 36.16%, maximum 110.8%
- 147 `Invalid actual_host_time` warnings, all in an initial burst
- No framebuffer event, GPU failure, diagnostic report, WindowServer restart, or system crash

Evidence: `.probe-runs/20260816-215849-stage1-jeep-one-surface-lit-shadows-maximized-window`.

## What the stalls mean

The telemetry counts frames in roughly one-second buckets. A lower bucket average proves reduced frame delivery during that interval, but it does not identify the duration of any individual slow frame.

To a player, the measured degradation would normally appear as:

- Roughly 50 FPS: mild uneven motion or judder.
- Roughly 20-30 FPS: clearly choppy camera and object movement.
- Roughly 3-10 FPS: an obvious hitch or brief near-freeze, with visible motion skipping forward.
- Input can feel delayed until a new frame is presented.

These were presentation stalls, not black screens, application crashes, macOS freezes, or restarts. The test scenes were mostly static, so even severe measured drops could be visually unobvious. A future visual-quality test should add continuous camera/object motion plus an on-screen frame-time graph.

## Likely subsystem

Networking was absent from these standalone tests and is ruled out as their cause. The evidence points toward the rendering/presentation chain:

`Unity rendering -> Metal -> WindowServer compositor -> Intel graphics/display driver`

The correlation with `Invalid actual_host_time`, elevated WindowServer CPU, Intel framebuffer warnings, and the WindowServer Metal/Intel diagnostic supports a macOS/Intel presentation-layer problem. Rendering workload can trigger or amplify it: enabling real-time shadows materially reduced frame rate. The evidence does not prove that every isolated slow sample came exclusively from macOS.

## Intel Mac gaming market

The July 2026 Steam Hardware Survey reported:

- macOS: 2.32% of surveyed Steam systems.
- GenuineIntel: 11.36% of surveyed Mac systems.
- Estimated Intel Mac share of all surveyed Steam systems: `2.32% x 11.36% = 0.2636%`, approximately one in 380.

Sources: [Steam overall hardware survey](https://store.steampowered.com/hwsurvey/) and [Steam Mac-only hardware survey](https://store.steampowered.com/hwsurvey/?platform=mac).

The survey is optional. Intel Mac owners using Boot Camp or some compatibility layers can be counted as Windows users, and Mac App Store players are outside Steam. The practical conclusion is nevertheless clear: Intel represents a small but nonzero portion of native Mac players and a very small portion of the overall PC-game market.

Support Intel when Unity's universal build keeps it inexpensive, but do not compromise the entire game or spend weeks on Intel-only fullscreen behavior. Treat it as best-effort coverage if special-case maintenance becomes expensive.

## Parallels and Boot Camp

Running a Windows build through Parallels is not a reliable escape from the macOS issue. The effective path remains:

`Windows DirectX -> Parallels virtual graphics/translation -> macOS graphics APIs -> WindowServer -> Intel display`

Parallels fullscreen still uses a macOS fullscreen presentation and therefore can potentially reach the same host display problem. The extra VM and graphics-translation layers can also reduce performance or introduce different failures. A normal Parallels window is likely safer than making the Parallels VM fullscreen, but this has not been tested here.

Boot Camp is different. When the machine boots directly into Windows, macOS and WindowServer are absent, so this exact macOS timestamp/WindowServer failure cannot occur. Windows or Intel's Windows graphics driver could have separate problems.

Sources: [Parallels Windows gaming support](https://kb.parallels.com/en/122485), [Parallels Metal acceleration](https://kb.parallels.com/en/123851), and [Parallels fullscreen settings](https://docs.parallels.com/landing/pdfm-ug/parallels-desktop-for-mac-26-users-guide/parallels-desktop-preferences-and-virtual-machine-settings/virtual-machine-settings/options/full-screen-settings).

## Unity WebGL and browser fullscreen

WebGL changes the renderer owner but does not bypass the host display stack:

`Unity Web build -> browser WebGL renderer/compositor -> WindowServer -> Intel display`

A normal browser window is expected to be the lower-risk presentation, analogous to the ordinary Windowed control. Calling the browser Fullscreen API removes the browser UI and allocates the screen to the game element, but WindowServer still presents it. True browser fullscreen could therefore produce the same timestamp-warning family. The browser compositor and GPU-process isolation might recover differently, and browser rendering may use a different resolution or pacing, so native results cannot predict its safety.

WebGL also adds unrelated possible stalls from browser compositing, JavaScript/Wasm scheduling, garbage collection, and graphics translation. It requires a separate Safari/Chrome matrix covering ordinary window, canvas filling the page without true fullscreen, and Fullscreen API mode.

Sources: [Fullscreen API](https://developer.mozilla.org/en-US/docs/Web/API/Fullscreen_API) and [WebKit WebGL-over-Metal implementation history](https://bugs.webkit.org/show_bug.cgi?id=227633).

## Unity versus Godot networking

Unity is the more mature commercial multiplayer ecosystem overall. Its supported stack includes Netcode for GameObjects, Unity Transport, Authentication, Sessions, Lobby, Matchmaker, Relay, dedicated-server workflows, profiling, documentation, and hosted-service integration. For Unity Web clients, the supported path is Netcode for GameObjects plus Unity Transport 2 or later, Relay, and secure WebSockets (`wss`).

Godot supplies capable high-level multiplayer, WebSocket, and WebRTC APIs. Its Web export documentation states that browser security prevents low-level networking and limits Web clients to HTTP, WebSocket client, and WebRTC. More of the surrounding production stack—deployment, relay operation, matchmaking, observability, and service integration—must generally be assembled by the developer.

Unity does not remove browser networking restrictions:

- Browsers do not expose raw TCP or UDP sockets.
- Unity Web clients use secure WebSockets in the officially integrated NGO/Transport/Relay path.
- WebSockets are reliable and ordered, so one delayed packet can hold later packets behind it.
- UDP, DTLS, and Relay quality-of-service region measurement are unavailable on Unity Web clients.
- A fast action game still needs intentional snapshot buffering, interpolation, client prediction, reconciliation, bandwidth control, join handling, and reconnection.

Some of the prior Godot effort was likely caused by tooling and integration gaps; some was inherent to browsers and would follow a Unity port. Unity should make the production path better supported, not magically eliminate real-time network engineering.

Sources: [Unity Relay and Netcode Web support](https://docs.unity.com/en-us/relay/relay-and-ngo), [Unity Multiplayer Web limitations](https://docs.unity.com/en-us/mps-sdk/faq), [Unity Relay networking](https://docs.unity.com/en-us/relay/networking), and [Godot Web export networking limitations](https://docs.godotengine.org/en/4.6/tutorials/export/exporting_for_web.html).

## Recommended next proof

Before committing to a full game port, build a thin Unity multiplayer slice:

1. Run a native authoritative Unity server.
2. Connect at least two Unity Web clients through secure WebSockets.
3. Reproduce the game's movement, snapshot, prediction, reconciliation, late-join, and reconnect requirements.
4. Exercise the existing latency, jitter, join, and determinism scenarios developed during the Godot work.
5. Test Web clients in ordinary browser-window and true browser-fullscreen modes on the affected Intel Mac.
6. Compare correctness, visible motion, bandwidth, CPU, iteration time, and failure recovery against the known Godot baseline.

If that slice passes, Unity provides the stronger combined case: more resilient tested Intel Mac fullscreen behavior, a workable headless CLI build workflow, and a more mature multiplayer production ecosystem.


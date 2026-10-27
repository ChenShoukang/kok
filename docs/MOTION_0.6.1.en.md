# v0.6.1 · Calm interaction motion

[中文](MOTION_0.6.1.md) · [Documentation](README.en.md)

This records the v0.6.1 development changes now included in v0.6.2. Motion follows meaningful state changes rather than animating the document body, entire library or a window during native dragging.

| Area | Feedback |
| --- | --- |
| Settings | Fixed dialog bounds, short content handoffs, continuous selection underline and cleanup during rapid switching |
| Reading modes and tool tabs | Moving selection background; hit targets stay in place |
| Favorites and bookmarks | A small icon pulse after saving, retaining text nodes and reading position |
| Menus | Short fade/translation; exiting menus immediately become inert and are then removed |
| Notes, translation and AI panes | Subtle translation and fade; repeated notes toggles honor the latest request |
| Photos and notifications | Smooth zoom/rotation, immediate photo dragging, a short notification entrance |
| Note tools | Links/backlinks/outline handoffs; completion updates in place without restarting its entrance |
| Knowledge graph | Settled background layout before reveal, stable positions across filters, direct dragging, one render loop for damped zoom and focus feedback |

Menus/feedback take around 100–180 ms, sidebars 220 ms and dialog entrances 230 ms. Ordinary dialogs translate/fade without rescaling text. Native previews retain their origin handshake with scale amplitude reduced to 1.5%; focus does not replay entrances. Document preparation begins one entrance once ready; slow reads may show loading. Backdrops animate opacity rather than blur filters. Native gestures, hidden pages and reduced motion finish or skip effects while preserving final states.

Graph zoom retargets keep the current view and velocity rather than snapping to a previous goal. Wheel zoom stays anchored under the pointer; small trackpad deltas follow immediately. Filters preserve positions/camera, released nodes stay put, and closing stops rendering/calculation. One animation owner replaces competing CSS and script transitions on sidebars.

Includes the native-window fixes: moving alone does not repaginate; resizing updates reading width live and restores the text anchor after settling; PDFs keep their canvas until replacement rendering finishes. See the [window-fix record](WINDOW_DRAG_FIX_0.5.1.md), now included in this version.

References: [AutoAnimate](https://github.com/formkit/auto-animate/blob/master/src/index.ts) for scoped layout transitions and interruption handling; [Radix Presence](https://github.com/radix-ui/primitives/blob/main/packages/react/presence/src/presence.tsx) for exit completion and presence state. No component code or additional animation runtime is bundled; implementation uses native browser animation APIs.

This revision also references [Sigma.js Camera](https://github.com/jacomyal/sigma.js/blob/main/packages/sigma/src/core/camera.ts) interruption behavior and [Svelte transitions](https://github.com/sveltejs/svelte/blob/main/packages/svelte/src/transition/index.js) opacity/translation handoffs. The camera uses kok's own critically damped integration, without a new runtime.

Focused checks use isolated, silent profiles: rapid reversals, menu close/reopen, sidebar handoffs, keyboard dismissal/focus, reduced motion, light/dark themes, and real native move/resize/maximize/cancellation bursts. No personal API configuration or AI requests are involved. These checks do not establish frame-rate guarantees across different hardware.

Returning to vertical reading also clears the old pagination spacer to prevent an unnecessary horizontal scrollbar. Edge checks include enabling reduced motion mid-animation, closing a file with an open menu, immediate photo dragging and suppression of photo transitions during native gestures.

Development checks: `npm run check`, `npm run check:windows`, `build.ps1`. Targeted suites: `tools/test-desktop.ps1 -Suites calm-motion` and `-Suites window-drag`. Both generate offline book/PDF fixtures automatically, without relying on prior test output or scanning personal files.

`-Suites motion-polish` samples actual intermediate frames, checking stable settings bounds, position/velocity continuity, pointer-anchored zoom, still graph entrances, stable completion, cleanup and reduced motion. Sampling uses elapsed time to support different refresh rates. If test FFmpeg already exists, it can encode an isolated-fixture WebM; no components are downloaded and no real personal content is recorded.

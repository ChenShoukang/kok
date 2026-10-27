# Development and tests

[中文](DEVELOPMENT.md) · [Documentation](README.en.md)

## Layout

| Path | Purpose |
| --- | --- |
| `src/` | WinForms / WebView2 host, file access, library, subtitles and translation |
| `ui/` | Glass interface, menus, media / reading workspaces and localization |
| `tests/` | Native checks and integration tests against the actual desktop window |
| `tools/` | Dependencies, icon / sample generation, format reports and publishing |
| `samples/` | Redistributable original examples |
| `docs/screenshots/` | Documentation captures |
| `dist/` | Ignored local builds and portable packages |
| `test-output/` | Ignored isolated profiles and temporary evidence |

Scripts share one local bridge and state object, loading in the order in `ui/index.html`. Custom menus preserve native select values, focus and change events. One paused, muted video element extracts cached covers sequentially into `data/thumbs/`, yielding to previews and playback.

## Build

Use Windows x64, Node.js 24, .NET Framework 4.8 and the WebView2 runtime:

```powershell
npm ci --omit=optional
npm run check
.\build.ps1 -Test
.\package.ps1
```

`build.ps1` pins the WebView2 SDK and verifies its download hash. PDF.js resources are prepared locally. Building requires no AI API key.

## Interface verification

Start a separate profile with both debugging and engine-level silence:

```powershell
Start-Process -FilePath '.\dist\kok\kok.exe' -ArgumentList @('--debug','--test-silent','--data-dir',"$PWD\test-output\ui-data") -WindowStyle Hidden
npm run test:ui
npm run test:menus-covers
npm run test:refinements
npm run test:video-layout
npm run test:i18n
```

`tests/browser.cjs` refuses media tests unless the engine is silent. Normal app startup stays audible. Tests modify their isolated library; do not target everyday data. Photo and video-cover checks verify that sources stay unchanged.

`test:large-media` accepts a local video path for byte-range and seeking checks without uploading it. Translation tests use a local mock service.

## Targeted notes and graph check

`powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-desktop.ps1 -Suites knowledge` uses isolated ordinary/private libraries to verify actual saves, completion/backlinks, tasks, templates, graph filters, dragging/zooming, empty/compact views, native resize bursts, reduced motion, a 500-node Worker and idle shutdown. External requests are blocked and real API configuration is never used. `-Suites documents` covers saves, close flushing and conflict recovery.

## Packaging and publishing

`package.ps1` creates `dist/kok-<version>-win-x64.zip` and its SHA-256 file from an explicit allowlist and current resource folders, excluding profiles, credentials, test binaries and temporary data.

v0.6.2 enables `release.publish: true`. A push to main builds and audits on both Windows runners; the release job publishes only after both succeed. `package.ps1 -SkipTests` may repackage already-tested native sources; staging/ZIP privacy audits still run.

Pushes to `main` trigger the [Windows workflow](https://github.com/ChenShoukang/kok/blob/main/.github/workflows/build.yml) on Windows Server 2022 / 2025. Script, dependency, source and complete Git-history checks run before native tests and packaging. Build jobs have read-only repository access; a separate publishing job waits for both environments before using `contents: write` and the built-in `GITHUB_TOKEN`. Future versions default to Preview; existing versions are not silently replaced. Server checks do not replace fresh Windows 10 / 11 desktop verification; see [release verification](TESTING.en.md).

`npm run audit:dependencies` queries the official npm advisory database and fails on any known vulnerability level or network failure. `npm run audit:history` scans locally reachable commits for credential patterns and private-data paths without printing matching values. CI fetches complete history; ensure manual checks do not use a shallow clone. `node tests/audit-history.cjs` verifies that deleted test credentials still present in history are detected. `tools/audit-release.ps1` continues to check current source and ZIP packages.

## v0.3.1 desktop audit

`tools/test-desktop.ps1` launches a fresh isolated profile for each of 16 desktop suites, always with `--debug --test-silent`. It covers core features, translation, languages, motion, drag/drop, workspaces, borderless video, photo/shortcut refinements, menus/covers, actual codec decoding, organization, file chat, private passwords/auto-lock, modes and renderer recovery. Results stay in ignored `test-output/`.

```powershell
.\tools\test-desktop.ps1
.\tools\test-desktop.ps1 -Suites private-space,app-modes
.\tools\audit-release.ps1
.\tools\audit-release.ps1 -Archive .\dist\kok-0.3.1-win-x64.zip
```

The native suite covers authenticated private storage and provider credential isolation as well as existing features. AI tests use mock servers only. FFmpeg is a test-only fixture generator, excluded from packages. Packaging audits both the staging directory and final archive; a matching credential pattern fails the build without printing its value.

## Targeted native downloads check

`powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-desktop.ps1 -Suites native-downloads` uses an empty isolated profile and an original-media localhost HTTP fixture, without optional components or models. Resume validates [HTTP Range / If-Range](https://www.rfc-editor.org/rfc/rfc9110.html#name-if-range). Body reads have their own timeout and cancellation after [ResponseHeadersRead](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption?view=netframework-4.8). Implementation: `src/DirectDownloads.cs`.

Targeted media components / streams: `powershell -NoProfile -Command "& ./tools/test-desktop.ps1 -Suites media-downloads"`. Uses pinned real binaries and generated localhost HLS / DASH VOD fixtures, with no sound or AI. `src/StreamDownloads.cs` uses FFmpeg demuxing and stream copy; website extractors remain separate. Gyan Windows builds, listed by FFmpeg, have pinned archive and executable hashes.


`powershell -NoProfile -Command "& ./tools/test-desktop.ps1 -Suites kok-downloads"` checks kok page discovery, source / quality selection, subtitles, audio export, HLS / DASH, retired-component cleanup and UI using local original fixtures.

Page discovery follows [HTML media elements](https://html.spec.whatwg.org/multipage/media.html), [Open Graph](https://ogp.me/) and [VideoObject metadata](https://schema.org/VideoObject). See `src/MediaResolver.cs`; page JavaScript is not executed.

Site adapters: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-desktop.ps1 -Suites site-adapters`. Original local media and offline HTTP fixtures verify layouts, split tracks and encrypted sessions; live checks read public metadata only. Set `KOK_SKIP_LIVE_SITES=1` to skip external checks. YouTube dependencies are pinned in `tools/youtube-dependencies.json`; build verifies package / DLL SHA-256, with licenses under `docs/licenses/`.

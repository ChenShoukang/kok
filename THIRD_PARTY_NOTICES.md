# Third-party notices

kok uses Microsoft.Web.WebView2 SDK version 1.0.4258.31 from the official
NuGet package. Release bundles redistribute:

- Microsoft.Web.WebView2.Core.dll
- Microsoft.Web.WebView2.WinForms.dll
- WebView2Loader.dll (win-x64)

These files are governed by the Microsoft WebView2 SDK license, **not** kok's
MIT license. The original package license is included in binary bundles as
`WEBVIEW2-LICENSE.txt`, and its NOTICE file as `WEBVIEW2-NOTICE.txt`.

Package: https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31
Documentation: https://learn.microsoft.com/microsoft-edge/webview2/

The WebView2 browser runtime is installed and updated separately by Microsoft.
It is not redistributed in kok's archive.

Playwright Core is used only for development tests under Apache-2.0. It is not
included in the desktop distribution. See the installed npm package for its
license and notices.

PDF.js version 6.4.299 by Mozilla Foundation and contributors is bundled as
local browser assets under `ui/vendor/pdfjs/`, including the rendering module,
worker, CMaps, standard fonts, ICC profiles and WASM decoders. It is licensed
under Apache-2.0, with its original LICENSE included in that directory.
The `.textLayer` CSS rules in `ui/features.css` are adapted from PDF.js viewer
styles; the rest of the interface styles are original to kok. Individual
font or decoder notices are retained with their asset directories.

Package: https://www.npmjs.com/package/pdfjs-dist/v/6.4.299
Source: https://github.com/mozilla/pdf.js

No source code from Tauri, Foliate or mpv has been copied or bundled.
The original interface, geometric artwork, documentation,
sample text, generated landscapes, synthesized audio and sample video are
original to this project.

kok implements media discovery and file transfer locally. It parses public
HTML media elements, page metadata and static player configuration. Optional
browser assistance runs the official site in an isolated, muted WebView2 profile,
without exposing kok host objects or its application message bridge.

YouTube extraction uses YoutubeExplode 6.6.2, distributed under MIT. Its pinned
NuGet package identifies source commit 6b90e284c8775056fdd35d5770e3adb28dd80759.
The binary and its Microsoft runtime dependencies are hash-locked in
tools/youtube-dependencies.json. Upstream and dependency notices are retained
in docs/licenses/YoutubeExplode-MIT.txt and docs/licenses/youtube/.
https://github.com/Tyrrrz/YoutubeExplode

Bilibili and Douyin response layouts / split-track workflows were studied from
iawia002/lux (MIT). kok implements its own C# adapters; the Lux executable is
not bundled or invoked. Attribution and MIT terms: docs/licenses/LUX-MIT.txt.
https://github.com/iawia002/lux/tree/master/extractors/bilibili
https://github.com/iawia002/lux/tree/master/extractors/douyin

The Bilibili WBI request-signing protocol was cross-checked against the
public-domain / Unlicense source of yt-dlp. kok performs signing directly in C#;
no yt-dlp executable, Python runtime or Deno component is shipped or executed.
https://github.com/yt-dlp/yt-dlp/blob/master/yt_dlp/extractor/bilibili.py

FFmpeg / FFprobe are optional, separately downloaded child-process tools, not
included in kok's base archive. The one-click installer uses Gyan's Windows
essentials build, linked from FFmpeg's official download page. Archive and both
executable hashes are pinned in ui/download-components.json. The installer keeps
the build's GPLv3 LICENSE and README alongside the executables. Users may also
select their own paired installation; that build's licenses apply.
https://ffmpeg.org/download.html
https://github.com/GyanD/codexffmpeg/releases/tag/9.0.2
https://github.com/FFmpeg/FFmpeg/commit/946fcce07b

Reading workflow ideas were informed by hectorqin/reader's public user guide:
https://github.com/hectorqin/reader
No code, interface assets or server implementation from that AGPL project
have been copied or bundled.

Smiley Sans v2.0.1 (得意黑), Copyright (c) 2022–2024 atelierAnchor, is bundled
as the unmodified official WOFF2 and is the default local interface / reading font.
Licensed under the SIL Open Font License 1.1, with Reserved Font Names Smiley
and 得意黑. Full copyright and terms: docs/licenses/SmileySans-OFL.txt.
Version, origin and hash: ui/fonts/NOTICE.md.
https://github.com/atelier-anchor/smiley-sans/releases/tag/v2.0.1

Six unmodified Microsoft Fluent Emoji Flat SVGs are bundled for navigation and
fallback covers (card file box, clapper board, framed picture, musical note,
page facing up and blue book). Copyright (c) Microsoft Corporation. MIT license:
docs/licenses/FluentEmoji-MIT.txt. Source paths, pinned commit and SHA-256 hashes:
ui/icons/fluent/manifest.json. No runtime icon downloads are made.
https://github.com/microsoft/fluentui-emoji/tree/1ffb34c752ecf5d402f04cfb4b392c77f57c54bc

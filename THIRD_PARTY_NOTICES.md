# Third-party notices

kok uses Microsoft.Web.WebView2 SDK version 1.0.4314.35 from the official
NuGet package. Release bundles redistribute:

- Microsoft.Web.WebView2.Core.dll
- Microsoft.Web.WebView2.WinForms.dll
- WebView2Loader.dll (win-x64)

These files are governed by the Microsoft WebView2 SDK license, **not** kok's
MIT license. The original package license is included in binary bundles as
`WEBVIEW2-LICENSE.txt`, and its NOTICE file as `WEBVIEW2-NOTICE.txt`.

Package: https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4314.35
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

LXGW WenKai Lite v1.530 (霞鹜文楷轻便版) is bundled as losslessly compressed WOFF2 files for offline font selection.
All glyphs, shaping, copyright and name metadata are preserved; no subsetting or
outline transforms are applied. Only WOFF2-required DSIG removal and head.flags
bit 11 differ from source table data. Compression
verification and source/runtime hashes are provided in ui/fonts/.
It uses the SIL Open Font License 1.1; these font assets retain that license
rather than kok's MIT code license. Upstream copyright and full terms are in
docs/licenses/LXGWWenKaiLite-OFL.txt.
Pinned source commits, Git blob identities and SHA-256 hashes are documented
in ui/fonts/NOTICE.md and ui/fonts/manifest.json.
https://github.com/lxgw/LxgwWenKai-Lite

Six unmodified Microsoft Fluent Emoji Flat SVGs are bundled for navigation and
fallback covers (card file box, clapper board, framed picture, musical note,
page facing up and blue book). Copyright (c) Microsoft Corporation. MIT license:
docs/licenses/FluentEmoji-MIT.txt. Source paths, pinned commit and SHA-256 hashes:
ui/icons/fluent/manifest.json. No runtime icon downloads are made.
https://github.com/microsoft/fluentui-emoji/tree/1ffb34c752ecf5d402f04cfb4b392c77f57c54bc

TagLibSharp 2.3.0 is shipped as the unmodified official NuGet net462 DLL, under
LGPL-2.1-only. Copyright (c) 2006–2007 Brian Nickel and (c) 2009–2020 contributors.
License: docs/licenses/TagLibSharp-LGPL-2.1.txt. Corresponding library source,
upstream build files, notices and public strong-name key are included in
docs/licenses/TagLibSharp-2.3.0-source.zip; unrelated test media are omitted.
The separate DLL can be replaced/rebuilt. See KOK-SOURCE-NOTICE.txt in that ZIP.
Package SHA-256: 3c3f5b55988f69e0bc84dc760feb8351fef4d5c09a322d88462e683d334dfbfc.
DLL SHA-256: b1833a41ab1e933f7b006e5db15300b7223bfccc2c3b6689d49a9171dd27de1d.
Source ZIP SHA-256: 68d87320595ff137ee8fe47d867a4d19af0e00135ea60dcb739872aefa260b94.
https://github.com/mono/taglib-sharp/tree/TaglibSharp-2.3.0.0
https://www.nuget.org/packages/TagLibSharp/2.3.0

PDF-LIB 1.17.1, Copyright (c) 2019 Andrew Dillon, is bundled as its official
minified browser module under MIT, loaded only when exporting edited PDFs.
License: docs/licenses/PDF-Lib-MIT.txt. Version/integrity: package-lock.json.
https://github.com/Hopding/pdf-lib

MusicBrainz provides optional public metadata search. No audio, API keys or
absolute file paths are sent to it, and no music samples or remote covers are bundled.
https://musicbrainz.org/doc/MusicBrainz_API

Editing workflow ideas were informed by storytold's PhotoCraft, FilmCraft,
LightCraft, PdfCraft and DesignCraft public documentation. Their Rust engines,
code, screenshots, logos and other brand assets are not copied or bundled.
https://github.com/storytold

Whisper.cpp is an optional component downloaded only at the user's request,
not bundled in the portable package. The official Windows CPU build b5454
is pinned to SHA-256 6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb.
Models come from ggerganov/whisper.cpp on Hugging Face and are verified before use:
tiny: be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21;
base: 60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe.
The Whisper project/model license is MIT. The project notice is included at
docs/licenses/Whisper-MIT.txt. Optional files and models remain outside releases.
https://github.com/ggml-org/whisper.cpp
https://huggingface.co/ggerganov/whisper.cpp

UI and motion principles reference finesse-ui (MIT), Motion (MIT) and Fluent 2
public documentation. Their code, artwork and animation runtimes are not bundled.
The local timing curve uses a standard critically damped response and native WAAPI.
https://github.com/mouse-lin/finesse-skill
https://github.com/motiondivision/motion
https://fluent2.microsoft.design/motion

Reading and viewer interaction ideas reference affectui (MIT), the Foliate-js EPUB
implementation (MIT) and Koodo Reader documentation. Their component code,
artwork, screenshots and runtimes are not copied or bundled.
https://github.com/flohoeller/affectui
https://github.com/johnfactotum/foliate-js
https://github.com/koodo-reader/koodo-reader

Window/reading interaction also references the public Foliate-js paginator,
Motion layout scheduling and Radix Toolbar (MIT) documentation and implementations.
No component code or additional runtime from these projects is bundled.
https://github.com/johnfactotum/foliate-js/blob/main/paginator.js
https://github.com/motiondivision/motion
https://github.com/radix-ui/primitives

v0.6.1 interaction motion also references AutoAnimate (MIT) and Radix Presence
(MIT) for scoped transitions, interruption handling and exit completion.
No source code or runtime from these implementations is copied or bundled.
https://github.com/formkit/auto-animate/blob/master/src/index.ts
https://github.com/radix-ui/primitives/blob/main/packages/react/presence/src/presence.tsx

Reading chrome visibility references the Foliate application (GPL-3.0) design;
photo-editing control hierarchy references TOAST UI Image Editor (MIT) themes.
Only interaction/design ideas are used; no application code, artwork or runtime
from these implementations is copied or bundled.
https://github.com/johnfactotum/foliate/blob/gtk4/src/book-viewer.js
https://github.com/nhn/tui.image-editor/blob/master/apps/image-editor/examples/js/theme/black-theme.js

Managed-document creation and the source/reading layout reference MarkText (MIT)
interaction and layout ideas. No MarkText source code, assets or runtime is bundled.
https://github.com/marktext/marktext
https://github.com/marktext/marktext/blob/develop/packages/desktop/src/renderer/src/components/editorWithTabs/index.vue

Knowledge graph and note interaction ideas reference Logseq and SiYuan public
documentation/implementations, d3-force's cooling model and Radix Presence's
interruptible state transitions. Only design and algorithm concepts are used;
their source code, artwork and runtimes are not copied or bundled. The graph
uses kok's own Canvas renderer and geometry-only Worker with no new dependency.
https://github.com/logseq/logseq/blob/master/src/main/frontend/components/graph.cljs
https://github.com/siyuan-note/siyuan
https://github.com/d3/d3-force/blob/main/src/simulation.js
https://github.com/radix-ui/primitives/blob/main/packages/react/presence/src/presence.tsx

The v0.6.1 motion revision references Sigma.js camera cancellation/retargeting
and Svelte opacity/translation transition ideas. No source code, assets or
runtime from either project is copied or bundled. The camera spring equations
and scoped browser animation orchestration are implemented locally.
https://github.com/jacomyal/sigma.js/blob/main/packages/sigma/src/core/camera.ts
https://github.com/sveltejs/svelte/blob/main/packages/svelte/src/transition/index.js

PptxGenJS 4.0.1 (MIT) is loaded locally on demand for PPTX export. Only its browser bundle is included; Office, Node.js and npm are not runtime requirements. The browser bundle contains JSZip (MIT/GPLv3 dual license; used under MIT) and pako (MIT/Zlib); upstream headers are retained. Licenses: docs/licenses/PptxGenJS-MIT.txt, docs/licenses/JSZip-MIT.txt and docs/licenses/pako-LICENSE.txt. Source: https://github.com/gitbrent/PptxGenJS

The v0.7.1 contextual editor and instruction-only skills reference Pi's metadata/on-demand skills, tldraw's selection/history model, and AFFiNE's contextual content editing. Type-based shelves reference Jellyfin Web and Kavita. These are interaction concepts implemented locally; their application code, assets and runtimes are not bundled.
https://github.com/earendil-works/pi/blob/main/packages/coding-agent/docs/skills.md
https://tldraw.dev/docs/editor
https://github.com/toeverything/AFFiNE
https://github.com/jellyfin/jellyfin-web
https://wiki.kavitareader.com/guides/admin-settings/media/

The contextual menu refinement references Radix Context Menu keyboard/focus patterns and Tiptap BubbleMenu selection-aware positioning/lifecycle. Concepts are implemented locally; neither framework is bundled.
https://www.radix-ui.com/primitives/docs/components/context-menu
https://tiptap.dev/docs/editor/extensions/functionality/bubble-menu

SQLite 3.54.1 (public domain) is bundled as sqlite3.dll for local item/facet/link storage and FTS5 search. The official Windows x64 binary is pinned by URL and SHA-256 in tools/sqlite-source.json. SQLite does not require a license file: https://sqlite.org/copyright.html . Acorn 8.19.0 (MIT) is a development-only parser used for architecture checks and is not bundled with the application.

The recipe editor uses n8n's public node/step configuration concepts as a reference. No n8n application code, assets or runtime are bundled. https://docs.n8n.io/build/understand-workflows/workflow-components/work-with-nodes.md

## Agent web content extraction

kok bundles these libraries only to read and extract public web pages that the user explicitly approves through `fetch_url` / the configured search endpoint; they perform no background network requests and their output is treated as untrusted evidence.

- **SmartReader 0.11.1** (Apache-2.0) - a port of Mozilla Readability used to isolate the main article content. Source: https://github.com/strumenta/smartreader . License and NOTICE: `docs/licenses/agent/SmartReader-Apache-2.0.txt`, `SmartReader-NOTICE.md`.
- **AngleSharp 1.5.2** (MIT) - HTML parser used by SmartReader and as a plain-text fallback. Source: https://github.com/AngleSharp/AngleSharp . License: `docs/licenses/agent/AngleSharp-MIT.txt`.
- **System.Text.Encoding.CodePages 10.0.10** (MIT, .NET Foundation) - legacy encoding support required by the HTML parser. Source: https://github.com/dotnet/runtime . License: `docs/licenses/agent/System.Text.Encoding.CodePages-MIT.txt`.

## QuickJS WebAssembly computation

The offline JavaScript interpreter uses QuickJS Emscripten 0.32.0 (release-sync). QuickJS Emscripten and the QuickJS engine are MIT licensed. The combined Jake Teton-Landis, Fabrice Bellard and Charlie Gordon notices are retained in [QuickJS-Emscripten-MIT.txt](docs/licenses/QuickJS-Emscripten-MIT.txt). Only the browser module, binding helpers and WebAssembly binary are shipped; Node.js and other build dependencies are not included.

Project: https://github.com/justjake/quickjs-emscripten

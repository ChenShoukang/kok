# kok

[中文](README.md) · English

A lightweight **Windows library** for videos, photos, music, documents and books, with a clean glass interface.

Local organization needs no account or internet connection. Files are referenced by their original paths. Optional AI translation sends selected text to your configured API and is billed by your provider.

**[Download the Windows portable app](https://github.com/878kouyier/kok/releases/latest)**. Sign in to an authorized GitHub account to download from this private repository.

![Library](docs/overview-en.jpg)

## v0.1.4: Chinese and English

- Choose **Settings → Interface language → 中文 / English**. The selection takes effect immediately and is remembered.
- Navigation, menus, settings, playback and reading controls, editing tools, tooltips, accessibility labels, notifications and application-owned dialog titles support both languages.
- File names, collections, document text, notes and translations keep their original content when you switch the interface language.
- Built-in help, export descriptions and release instructions are available in both languages.
- Includes the smoother startup and transitions, wider sidebar, glass surfaces and configurable AI translation from v0.1.3.

## Getting started

1. Download `kok-0.1.4-win-x64.zip` and extract it to a writable folder.
2. Run `kok.exe`. Keep the DLLs, `ui` and `samples` folders beside it.
3. Select **Import files**, or open its adjacent menu and select **Import folder**. Folder imports include subfolders and skip duplicate paths and unsupported files.
4. To try the app, choose **Explore sample files** in an empty library, or add samples from Settings. Samples are original local documents and media; their Chinese file names and content are preserved as source files.

Requirements: **Windows 10 / 11 x64, .NET Framework 4.8 and Microsoft WebView2 Evergreen Runtime**. If prompted, install WebView2 from [Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/). kok does not require Node.js, Python, Rust or the .NET SDK to run. The app is not code-signed; Windows may identify the publisher as unknown. Use a trusted release and its SHA-256 file.

## AI translation

1. Go to **Settings → AI translation → Configure API**. Enter the endpoint, model name, API key and target language. Supports OpenAI-compatible Chat Completions endpoints. Enter the provider's base path, such as `/v1`, or the complete `/chat/completions` endpoint. See the [official API reference](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create).
2. **Test connection** sends a short request. Save the settings. Local services may use HTTP localhost / 127.0.0.1 without a key; remote services require HTTPS.
3. Open a file and select **Translate**. Documents, books and text PDFs support the current chapter / page or the entire document. Translation appears separately; original text and annotations are retained. Scanned PDFs need OCR first.
4. Video translation uses the selected text subtitle track and keeps its timestamps. Switch between bilingual subtitles and translation only, and export SRT. Embedded or bitmap subtitles and speech transcription are not included.
5. Optional automatic translation handles opened subtitles, chapters and the current PDF page. It is off by default. Full-document translation requires an explicit selection. Long input is batched, with progress and cancellation. Identical content, service, model and language reuse cached translations.

Keys are encrypted with Windows DPAPI for the current account in `data/ai-settings.json`. They are excluded from library exports and release packages. Enter the key again after moving to another account or computer. Models must support the requested JSON translation format. Authentication, quota, truncation and response errors appear in the translation panel. Translations are cached in `data/translations/`.

The translation pipeline is tested with a local HTTP mock, without paid API requests. Actual connectivity and translation quality depend on the configured provider and model.

## Features and compatibility

| Area | Built-in features | Scope |
| --- | --- | --- |
| Video | Playback, seeking, volume, full screen, resume, 0.5×–3× speed, silence acceleration and matching sidecar subtitles | Playback depends on the container and codec. WebM / VP8 is tested. Use a system player for unsupported codecs. |
| Photos | Thumbnails, zoom, rotation, browsing, slideshow; brightness, contrast, saturation, center crop and flip; save a copy | Common JPG, PNG, GIF, WebP and BMP formats. Special formats such as HEIC or TIFF may need a system app. Editing is limited to 4096 px on the long side. |
| Music | Album shelf, album details and queues, continuous playback, shuffle, repeat, volume and resume | Albums are grouped by folder; edit album and artist names. Common audio formats are indexed; WAV playback is tested. |
| Documents | PDF canvas and text layer, zoom, underlines, highlights and inserted annotations; DOCX and text reading and insertion | DOCX original layout is not preserved. Changes belong to the kok reading copy. XLSX, PPTX and legacy DOC use system apps. |
| Books | EPUB contents, TXT sections, font size, paper themes and resume; highlights, underlines, thought popovers and annotation index | EPUB is text-only. Complex layouts, images, footnotes and DRM decryption are not supported. Other ebook formats may need a system reader. |
| Organization | File-type and folder grouping, custom collections, favorites, recent files, search, grid / list and sorting | If a source file moves or is renamed, remove its old record and import it again. |
| Fonts and appearance | Chinese / English, glass surfaces, light / dark / system themes; Microsoft YaHei, SimSun, KaiTi and font import | TTF / OTF files up to 20 MB. Use fonts you are licensed to use. PDF original typography is not reflowed. |
| Notes | Per-file notes and per-mark thoughts, local persistence and index export | Normal preview or window closing saves edits. Power loss or a forced process termination may lose the latest unsaved changes. |

Text supports UTF-8, UTF-16 and GB18030. Imported content is not executed as scripts. Large text, XML and chapters have read limits.

Subtitles: SRT, VTT, ASS / SSA text compatibility, MicroDVD SUB, SBV, LRC, TTML / DFXP and SAMI SMI. Place matching subtitles beside the video, with the same base name or a language suffix such as `movie.en.srt`, or select **Load subtitles**. ASS effects, complex positioning and bitmap IDX / SUB are not supported. Silence acceleration uses audio volume, not speech recognition.

Document changes belong to a **kok reading copy** and do not overwrite original DOCX or PDF files. Export DOCX / text as annotated HTML, or PDF annotations as an HTML summary. Photo edits are saved as a new PNG or JPEG and added to the library.

Indexing a format does not guarantee playback of every codec. System-app actions require an appropriate installed application, such as a media player, Office app or ebook reader. kok does not bundle or install them.

## Lightweight design

- Native WinForms with the installed WebView2 runtime; no separately bundled Chromium.
- C# and plain HTML / CSS / JavaScript; PDF.js is included for offline PDF viewing.
- Seekable file streams and HTTP byte ranges avoid loading whole videos into memory.
- Cached thumbnails and batched library cards. Translation panels load large result lists in sections.
- This release targets Windows. macOS and Linux builds are not provided.

Release size excludes the installed WebView2 runtime. Memory use depends on the engine, media and library size.

## Data and backups

`data/library.json` stores file paths, favorites, progress, notes, annotations and preferences; `.bak` keeps the previous save. `data/thumbs/` holds thumbnails, `data/fonts/` imported fonts, and `data/WebView2/` browser-engine data. AI configuration and translation caches are stored separately as described above.

Export a JSON backup from Settings, or close kok and copy the whole `data/` folder. JSON exports include local paths, not original media. Back up the source files separately. To restore, close kok and replace `data/library.json` with a valid backup. Removing an item only removes its index record. A corrupt database is preserved and reported. Use `--data-dir "D:\MykokData"` for a separate library.

## Shortcuts

- **Ctrl K**: search. **Esc**: close a preview or dialog.
- **← / →**: browse photos or reading chapters.
- **Space**: play / pause music or video.

## Build and verification

On Windows, install Node.js 24, then run:

```powershell
npm ci --omit=optional
.\build.ps1 -Test
.\package.ps1
```

The build uses the Windows .NET Framework compiler. It downloads the pinned official WebView2 SDK `1.0.4258.31`, verifies its SHA-256 and caches it. Output is in `dist/kok/`. Packaging excludes user data and test executables. Media samples are original project assets.

For actual WebView2 integration tests, start a dedicated test library with `kok.exe --debug --data-dir "$PWD\test-output\ui-data"`, then run `npm run test:ui`, `npm run test:design`, `npm run test:translation` and `npm run test:i18n`. Tests reset the test library and must not use personal data. The debug port is enabled only with `--debug`. DPAPI tests require a normally loaded Windows user profile.

Checks cover native persistence and media streaming, reading and PDF annotations, photo editing, albums, fonts, subtitles, translation configuration and cancellation, local cache reuse, interface language switching and user-content preservation. Render and playback tests use sample files and do not certify every format or layout. Native save dialogs need a user-selected destination; their save logic and export contents have separate checks.

## References and licenses

Implementation references include [Microsoft WebView2 samples](https://github.com/MicrosoftEdge/WebView2Samples), [PDF.js](https://github.com/mozilla/pdf.js), [epub.js](https://github.com/futurepress/epub.js) and [mpv](https://github.com/mpv-player/mpv). Independent code is used where appropriate; this app does not bundle external players or readers. See `LICENSE` and `THIRD_PARTY_NOTICES.md` for licenses.

# kok changelog

[中文](CHANGELOG.md)

## v0.5.1 · Preview

- Quiet reading adds single/adaptive two-page/vertical modes and wheel paging, fixed favorite/window controls and compact menus; coordinated opening, deferred resize work and batched bilingual measurements. See the [guide](docs/READING_FLOW_0.5.1.md).

- Real EPUB NAV/NCX, Word headings and text contents, paged books, visible bookmarks, independent fonts and eye comfort. Paired reading aligns typography and paragraphs, scrolls in both directions and restores local results and annotations.
- Coherent media chrome, idle controls, origin-based opening, compact/maximized/fullscreen and DPI fixes; collection dragging with explicit private-folder confirmation. See the [reading guide](docs/READING_RENEWAL_0.5.1.en.md).

- Translation adds up to four-worker fast mode (three in standard mode), progressive output, reusable segment caches, equal-width original/translation panes, synchronized scrolling and translation-only output. DOCX / EPUB / PDF / TXT / Markdown copies use matching formats.
- Video captions support optional local Whisper and a separately configured timestamped speech API, with installation, progress, cancellation, playback and SRT export. Speech credentials are encrypted separately.
- Finesse-informed toolbar and paper layouts, selection previews, grapheme-safe range adjustments, duplicate/stale-selection guards, undo and damped native animations. See the [guide](docs/TRANSLATION_CAPTIONS_0.5.1.md).

- Bundle Smiley Sans and LXGW WenKai Lite for offline, on-demand selection, with complete font data, OFL notices and build-time verification; guard asynchronous font switches against stale results.
- Add unified editing: photo text/drawing/cover layers, multi-source video/audio clip timelines, joining and clip captions, visual text with DOCX/EPUB exports, and PDF merging/reordering/page-canvas edits. Save local drafts or new copies.
- Redesign albums, artists and personal audio. Read tags and embedded covers offline, retain manual corrections, choose MusicBrainz candidates and preview metadata-only AI grouping before applying. Audio is not uploaded.
- Open photos, videos, documents and books in independent native windows with dragging and resizing. Double-click empty content space to send a viewer behind the library; restore it from the window strip. Viewers share the engine/library, save notes and progress on close, and close during private-folder transitions.
- Compress LXGW WenKai Lite as WOFF2 without subsetting, verifying full glyph and layout data with only required signature/format changes. Use same-version official minified PDF.js modules while retaining decoders, CMaps, standard fonts and notices.

- Compare readable documents, books and video subtitles with an adjustable selection cap of 2–50 files, cross-file citations, context preview, local reports, export and cache reuse.
- Review highlights and notes by file or collection, distinguish source text from personal thoughts, and discover related notes with local keyword matching.
- Generate persistent subtitle-segment chapters with source timestamps, seeking and active-segment markers. Regenerating after subtitle/model changes refreshes the cache.

- Shorter onboarding, download, collection and AI copy; settings grouped into Appearance, Tools and Library.
- Consistent small corners, tactile buttons, square covers and theme materials, with press and disclosure transitions.
- File assistant adds passage retrieval, outgoing-context preview, persistent clickable citations, answer copying, note extraction and retry.
- Retrieval includes the middle of long chapters. PDFs over 80 pages are sampled across the document with an excerpt disclosure.
- Chinese/English UI and AI isolation for Pure mode and private folders remain. Automatic publishing is disabled.
- References, limits and validation: [v0.5.1 design notes](docs/DESIGN_0.5.1.md).

## v0.4.1 · Preview

- Coherent neutral white / graphite materials, custom background / glass colors, opacity / blur and independent light / dark palettes.
- A minimal window bar follows the theme and custom background, replacing the white system title bar while retaining dragging, resizing, double-click maximize and borderless video fullscreen. Buttons have subtle 4px corners, with softly edged fields and menus and less card-like framing.
- Redesigned layouts use compact object icons with bilingual hover / focus hints, small square covers and contextual actions. Players, readers, downloads and settings share neutral colors, spacing and fine dividers, with restrained paper, photo-print and record details plus press feedback.
- Smiley Sans v2.0.1 is bundled as the offline default with its OFL license. System / Chinese font choices, imported fonts and existing selections remain available.
- Category and fallback-cover icons use Microsoft Fluent Emoji Flat vectors, reducing highlights and shadows with subtle hover / press scaling. Original assets, MIT license and source hashes are bundled. Fixed music play-button glyph contrast.
- Redrew the kok brand as a blue geometric folded K without a tile, updating startup, navigation, transparent PNG and 16–256px Windows application icons.
- Sidebar categories and shortcuts share 24px icons, 44px targets and 6px gaps. Fallback-cover icons are 96px; list and toolbar icons align to consistent sizes within their respective contexts.
- Normalize visible artwork bounds and centers; fix legacy collection-icon sizes and video placeholder positioning. Dark mode uses muted icon colors and dark surfaces for import and primary buttons.
- Photo wheel zoom, panning, free crop with eight handles, undo / redo and correct rotated / flipped crop mapping.
- Reading full-text search, chapter filters, PDF outline, persistent bookmarks, typography, paging / scrolling, focus, resume and Markdown note export.
- Bilibili / YouTube / Douyin adapters, split-track merging, official sign-in, DPAPI-encrypted sessions, cookies.txt import and manual browser assistance for Bilibili / Douyin. YouTube uses MIT-licensed YoutubeExplode with pinned package / library hashes; the generic downloader remains.
- A unified kok downloader: public page discovery, source / quality selection, audio export, existing SRT / VTT subtitles, queues, validated file resume, retry and automatic import. No page script execution or external extractor API.
- One-click, automatically detected FFmpeg / FFprobe adds common unencrypted M3U8 / DASH VOD, stream-copied into MP4 / M4A. Paused streams restart with a notice. Live, DRM, inaccessible content and unsupported complex sites remain outside this stage.
- Existing download history and media remain; retired extraction components are no longer installed or executed. Fixed legacy TLS defaults blocking HTTPS downloads. Distributed as a preview with documented compatibility limits.

## v0.3.1 · Pure / Smart and a private folder

- First-run Pure / Smart selector, with a password-protected folder inside the ordinary library and consistent glass UI.
- Pure keeps offline basics and blocks AI natively; Smart explains content sharing and model setup on first use.
- Password-protected private folder inside the library with encrypted index, notes and covers; locks on deactivation, minimize or idle and exports encrypted backups. Original source files remain in place and are not encrypted.
- AI setup adds 12 entries, model discovery, local no-key servers and output-limit compatibility. Credentials use Windows DPAPI and are never reused silently across changed endpoints.
- Selected-folder scanning, AI category/tag previews, timeline/type/custom groups and streamed file chat for videos, documents and books.
- Orientation-aware previews, efficient streaming updates, translation fixes, original-file export protection, cancellation and private-content cleanup.
- Reviewed media, annotations, photo edits, responsive layouts, menus and modes; release packages use an allowlist and audit credential patterns and private files.

## v0.2.1 · Smooth menus, video covers and properties

- Settings and library sorting share animated glass menus with keyboard navigation, themes, Chinese / English and reduced motion.
- Imported and existing videos receive cached real-frame covers when idle. Extraction stays paused and muted, yields to previews or playback, and preserves source files.
- Video context menus show file name, location, size, modification date, and actual duration / resolution from the playback engine.
- Includes the previous local builds: consistent themes, expanded photo editing, configurable video shortcuts, borderless fullscreen, adaptive windows and large-video crash fixes.
- Organizes screenshots, development / testing instructions and version history, with Windows portable builds published through GitHub Actions.

Cover extraction requires a decodable video. This release adds no codecs and does not parse codec or multiple-audio-track details.

## v0.1.9 (local build): Themes, editing and adaptive windows

- Toolbars, settings, photo editing, notes and translation panels share the app theme. Reading follows the interface by default, with optional paper colors.
- Settings use staggered entry and interaction transitions, respecting reduced motion.
- Photos gain soft / vivid / mono / vintage presets, temperature, grayscale, sepia, vignette, crop position, vertical flip, original comparison, undo / redo and reset. Previews fit the window; exported copies keep the original unchanged and support up to 4096 px on the longest edge.
- Video preferences customize play keys (Space / K / P), seek keys (arrows / J L), seek distance (5 / 10 / 15 / 30 / 60 seconds), volume arrows and double-click fullscreen. Shortcuts respect input focus and persist across restarts.
- Photos, editing previews and PDFs adapt to live resizing. PDFs reuse their loaded document while preserving aligned text and annotations. Compact toolbars wrap, with collapsible contents and side panels.

## v0.1.8 (local build): Borderless video and one control row

- Retains the original blue ribbon K across the startup mark, sidebar and multi-size Windows icon.
- Video fullscreen now fills the current monitor with a borderless Windows window. Exiting restores the previous position, size and maximized state.
- One transport row contains playback, skip, time, seek, volume, settings, picture-in-picture and fullscreen. Speed, subtitles and silence acceleration live in a floating glass panel.
- Fullscreen controls fade out during playback and reappear on pointer movement. Double-click or press F to toggle fullscreen; Esc exits.
- Retains the v0.1.7 large-media range limit and crash recovery. Automated playback tests use engine-level silence without changing normal playback.

## v0.1.7 (local fix): Large-video crashes

- Fixes excessive allocations in the WebView2 response bridge when opening multi-GB media files, which could terminate the entire app.
- Video / audio range responses are capped at 4 MiB, preserving the real total size and 64-bit offsets for seeking. Large media requests without a byte range are rejected safely.
- Handles Range preflight and HEAD requests. Renderer crashes restore the library without reopening the video; browser-engine failures show a native error view.
- Bounded local diagnostics in `data/logs/kok.log` exclude file contents, API requests and credentials.
- This fix adds no decoders. HEVC, DTS and embedded subtitles retain their existing compatibility limits.

## v0.1.6 (local build): Playback and reading workspaces

- Video: unified playback controls, seek, 10-second jumps, volume, fullscreen, picture in picture, subtitles and speed settings.
- Music: full album-style player, queue, shuffle / repeat, seek and volume. Collapsing keeps music playing; expanding preserves the paused state and current queue.
- Documents and books: paper layouts, collapsible contents, literal search within the current chapter and reading progress, with existing insertion, highlights and thoughts preserved.
- PDF: page-number navigation, previous / next page, zoom and annotation tools. Scanned documents still require external OCR.
- Consistent glass toolbars and smooth transitions, with bilingual controls, dark mode, compact layouts and reduced-motion support. No interface click sounds.
- Versioned UI assets refresh after an update without resetting local data. Broader decoding remains on the roadmap; this build adds no new decoding engine.

![Music player](docs/screenshots/music-player.jpg)
![Video player](docs/screenshots/video-player.jpg)
![Book reader](docs/screenshots/book-reader.jpg)

## v0.1.5 (local build): New logo, format compatibility and drag-and-drop import

- Drop files or folders from File Explorer to recognize all five categories, import recursively and skip duplicates while preserving originals.
- Additional decoders are deferred as a [future development goal](ROADMAP.en.md). Common formats play inside kok; unsupported files offer an external-app action.
- A new blue and mint ribbon K, shared by startup, sidebar and multi-size Windows icons. Brand colors remain consistent in both themes.
- **Settings → Supported formats** distinguishes built-in viewers, codec-dependent media and external apps in both languages.
- Real decoding checks cover 25 original media fixtures. All 10 external text subtitle formats have parser tests.
- See the [complete format matrix and measured results](docs/FORMATS.en.md) for extensions and limitations. Importing a file does not guarantee every codec will play.

## v0.1.4: Chinese and English

- Choose **Settings → Interface language → 中文 / English**. The selection takes effect immediately and is remembered.
- Navigation, menus, settings, playback and reading controls, editing tools, tooltips, accessibility labels, notifications and application-owned dialog titles support both languages.
- File names, collections, document text, notes and translations keep their original content when you switch the interface language.
- Built-in help, export descriptions and release instructions are available in both languages.
- Includes the smoother startup and transitions, wider sidebar, glass surfaces and configurable AI translation from v0.1.3.


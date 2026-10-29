# kok roadmap

[中文](ROADMAP.md) · English

## v0.8.1 Preview: implemented foundations and remaining goals

First pass at unified action dispatch, a native Agent core, Operations / Jobs and the SQLite warehouse; still a local preview and not yet fully accepted. See [warehouse progress](docs/WAREHOUSE_0.8.1.en.md) and [module notes](docs/MODULES_0.8.1.en.md).

## Published: v0.6.2-r2 preview

This revision adds the Graphite / frost interface, knowledge graph, document creation and motion fixes; see the [v0.6.2 notes](docs/GRAPHITE_0.6.2.en.md) and [preview download](https://github.com/ChenShoukang/kok/releases/tag/v0.6.2-r2).

The app includes five local library categories, drag-and-drop import, media playback, photo edits, document / book annotations and Chinese / English interfaces. Pure blocks AI requests; Smart enables optional translation, file chat and organization. A password-protected folder lives inside the library and encrypts its index, notes and covers, but not original files.

See the [README](README.en.md) for features and the [changelog](CHANGELOG.en.md) for history. More independent machines and real files still need verification; see [release verification](docs/TESTING.en.md) for boundaries and checks.

## Near term: stability and maintenance

- Gather Windows 10 / 11, WebView2, display scaling and codec feedback.
- Prioritize startup, imports, playback recovery and reading-data preservation while keeping source files unchanged.
- Gradually separate shared scripts and native bridge logic by library, playback, reading, AI and private storage; add regression coverage where data safety matters.
- Keep both languages, themes, keyboard controls, compact windows and reduced motion consistent.

## Later: broader playback inside kok

Broader built-in playback remains a future goal. **The current version uses WebView2 without a separate decoding engine**; see the current [format matrix](docs/FORMATS.en.md). Optional FFmpeg only handles download merging / conversion.

- Evaluate embedded engines and codec libraries for size, memory, startup time, hardware acceleration, stability and redistribution licenses.
- Prioritize MKV / AVI / MOV / MP4, HEVC / H.265 and common audio codecs; record container, video-codec and audio-codec results separately.
- Preserve the library, progress, notes, sidecar subtitles and speed controls before adding embedded subtitles and multiple audio tracks.
- Test seeking, synchronization, resume, damaged files and decoder failure recovery with actual files.
- Evaluate an optional compatibility pack to keep the base app lightweight.

These are directions rather than delivery commitments. No engine, additional package size or release date has been selected.

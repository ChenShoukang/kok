# kok roadmap

[中文](ROADMAP.md) · English

## Future goal: broader playback inside kok

The user has confirmed that **additional decoders are deferred, while broad built-in playback similar to PotPlayer remains a future goal**. The current release focuses on a lightweight five-category library, clear compatibility information and drag-and-drop import.

The goal is to play more media within kok and reduce reliance on external players. It does not require writing audio/video decoders from scratch.

- Evaluate embedded playback engines and codec libraries for size, memory, startup time, hardware acceleration, stability and redistribution licenses before selecting an approach.
- Prioritize common MKV / AVI / MOV / MP4 files, HEVC / H.265 and common audio codecs. Record test results separately for containers, video codecs and audio codecs.
- Preserve the library, playback position, notes, subtitle translation and speed controls; add embedded subtitles and multiple audio tracks later.
- Verify seeking, audio/video synchronization, subtitles, resume, malformed files and failure recovery with actual files.
- Evaluate an optional compatibility component pack so the base app stays lightweight. No engine, package size or delivery date has been selected.

This is a planned goal. **v0.2.1 does not include a new decoding engine.** See the [format matrix](docs/FORMATS.en.md) for current WebView2 playback and external-app boundaries.

## Current version

- Blue and mint ribbon K shared by startup, sidebar and Windows icons.
- Bilingual format matrix and actual decoding results, distinguishing built-in viewers, codec-dependent playback and external apps.
- File and folder drops, automatic classification and duplicate detection, with source files preserved.
- v0.2.1 combines verified local work for GitHub and automated portable builds.

- v0.1.6 adds four redesigned content workspaces: full music player, video controls, document / book layouts, chapter search and PDF page navigation, with no UI click sounds.

- v0.1.8 fixes oversized response allocations for multi-GB movies and adds bounded media ranges, renderer recovery and local diagnostics. Additional decoders remain deferred.

- v0.1.9 unifies themes and adds settings motion, photo tools, configurable video shortcuts and live window resizing.

- v0.2.1 adds glass selection menus, silent cached video covers, right-click properties and repository documentation.

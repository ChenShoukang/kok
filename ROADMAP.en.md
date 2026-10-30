# kok roadmap

[中文](ROADMAP.md) · English

## In development: v0.8.2 · Agent execution and tools

Follow-up work adds [semantic checkpoints, encrypted memory, read-only subagents and JavaScript/WASM isolation](docs/AGENT_RUNTIME_0.8.2.en.md).

Plans, explicit outcome review, bounded retries, request traces and local stdio MCP tools are implemented. See [scope](docs/AGENT_0.8.2.en.md) and [validation](docs/VALIDATION_0.8.2.md). Source version updated; automatic publishing is off.

## Published: v0.8.1 Preview

The public download remains [v0.8.1 Preview](https://github.com/ChenShoukang/kok/releases/tag/v0.8.1), including unified dispatch, a native Agent, Operations / Jobs, SQLite, Spaces and Clips. See [implementation scope](docs/WAREHOUSE_0.8.1.en.md).

## Direction: a modern Agent workspace and operating layer

- Reliable execution: broader real-model evaluations, source-linked review and explicit recovery boundaries.
- External tools: remote MCP, OAuth and finer permissions; evaluate execution isolation separately from process-lifetime control.
- Controlled memory: editable, deletable Space-scoped memory with provenance, timestamps and expiry.
- Explicit orchestration: workflow-selected subtasks, evidence references and reviewed merges, without default full-session copying or automatic output merging.
- A unified workspace connecting sources, Inspector, tasks, change review and recipes while preserving offline Pure mode.

These are further goals. Encrypted memory management and one-level read-only delegation are available; cross-Space memory policies, recursive or concurrent writing agents and autonomous control of the whole computer remain future work. Expand permissions only after execution and data-safety validation. Media compatibility and lightweight distribution remain priorities.

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

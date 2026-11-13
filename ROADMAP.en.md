# kok development roadmap

[中文](ROADMAP.md) · English

## Next direction: a general-purpose personal agent

Core scope includes research, documents, presentations, spreadsheets, photo editing,
video, music and audio creation, coding, browser automation and computer use.
Use one execution kernel with on-demand capability packs and domain-specific
validation of real outputs. Preserve useful local media and workspace tools.

The [detailed plan 2.0](docs/PROJECT_PLAN_0.14_TO_1.0.md) (Chinese) defines v0.14–v1.0
milestones, individual music and computer-use workstreams, quality gates and rollout.
An initial set of general workflows can arrive earlier; the complete proposed scope
requires a revised, acceptance-based estimate of roughly 7–10 months for one primary
developer with AI assistance.

The detailed plan takes priority over the historical progress and backlog below.

## v0.9.8 Preview deliverables

Completed the interface redesign (near-white and muted purple-dark themes, minimal welcome previews, a short rail at every width, a three-column launcher with frequent destinations first, offline Phosphor duotone glyphs, interruptible motion with persistent focus) together with the Agent focus redesign: first-screen defaults with material-aware suggestions, local task routing (`agentLocalPlan`, zero model requests), fragment-level retrieval with read coverage, a cache-friendly stable message prefix, task result cards, and a fixed 30-task sample set with an offline validator plus an opt-in live evaluation entry.

**608 native checks, 16 desktop suites and package re-verification pass**. A before/after comparison on the same 30 tasks shows requests falling 100 → 76 (−24%), input tokens 566,586 → 431,132 (−24%) and conservative cost from ¥2.68 to ¥2.11, with 26/30 passed in both rounds; the saving comes mainly from local task routing. The two passes used 190 requests, moving the project request ledger from 359 to 549. See [validation](docs/VALIDATION_0.9.8.md), [Agent focus](docs/AGENT_FOCUS.md), the [30-task baseline with the comparison](docs/AGENT_TASK_EVAL.md) and [interface notes](docs/FLAT_UI.md).

## v0.9.5 Preview deliverables

The compact sidebar now keeps four primary destinations plus one overflow entry, moving file-type filters back to the library toolbar. Tooltips and small labels rise to 12px, targets to 40px, menu focus uses the active row instead of an outline, and interrupted panel animations preserve their current transform. The 25-suite UI/regression matrix and 14-suite package gate pass; the real-model ledger stays at 359. See [validation](docs/VALIDATION_0.9.5.md) and [flat UI notes](docs/FLAT_UI.md).

## v0.9.4 Preview deliverables

The interface is refreshed with a flat visual system, grouped navigation, unified file cards/lists, consistent reading/media toolbars and keyboard-accessible interactions at compact widths. Saved classic/graphite skin preferences remain readable but render as the flat interface. Validation covers 25 local UI/regression suites and a 14-suite package gate; 120 screenshots were reviewed with no geometry or runtime findings, and the real-model ledger stays at 359. See [flat UI notes](docs/FLAT_UI.md) and [validation](docs/VALIDATION_0.9.4.md).

## v0.9.3 Preview deliverables

v0.9.3 completes the portable-runtime AppContainer MCP sandbox with a CI gate, the MCP OAuth device-code flow and approval-gated PDF/video batch indexing. All remain local-first and approval-gated, with no new real-model calls (ledger 359). See [Agent 0.9.3](docs/AGENT_0.9.3.md) and [validation](docs/VALIDATION_0.9.3.md).

## v0.9.2 Preview deliverables

On top of v0.9.1's eight areas, v0.9.2 adds OAuth for remote MCP, direct vision over PDF pages and video frames, and an opt-in Windows AppContainer sandbox for approved commands and local MCP servers. All remain opt-in or approval-gated, sandbox failure never silently falls back, and the real-model ledger stays at 150. See [Agent 0.9.2](docs/AGENT_0.9.2.md) and [validation](docs/VALIDATION_0.9.2.md).

## v0.9.1 Preview deliverables

On top of the v0.9.0 library and retrieval work, this release focuses on the Agent: vision input, web fetch/search, an opt-in bounded execution workspace, longer tasks and schedules, semantic memory, parallel read-only subagents, larger budgets and remote MCP. All eight remain default-off or explicitly approved. See [Agent 0.9.1](docs/AGENT_0.9.1.md) and [validation](docs/VALIDATION_0.9.1.md). The real-model ledger remains at 150 and no new model success rate is claimed.

## v0.9.0 Preview delivery

The original [peer review and acceptance plan](docs/PEER_GAPS_0.9.0.md) is retained as a historical baseline. Current behavior is documented in the [v0.9.0 guide](docs/LIBRARY_0.9.0.en.md); actual results and limits are in [validation](docs/VALIDATION_0.9.0.md).

| Stage | Delivered scope | Remaining boundary |
| --- | --- | --- |
| M0 · Quality and versions | Quality replay in default checks/CI; 30-case live-model harness with a persistent ceiling; version contracts; exact arithmetic and continuous write evidence | Report actual live results separately; receipt review does not prove semantic correctness |
| M1 · Content and anchors | Versioned chunks, PDF text, subtitles, Clips, incremental invalidation, coverage/failure display and original-location links | Capped extraction; unsupported formats are visible; large-library capacity is not certified |
| M2 · Local hybrid retrieval | Local Ollama embeddings, FTS5 rank fusion, shared UI/Agent retrieval, lexical fallback and model-revision cache | Exact vector scans; no ANN/reranker; no-answer queries may return approximate candidates |
| M3 · Portability and recovery | Markdown/JSON ZIP contract, managed/optional originals, checked restore into a new library, root mapping and relink/undo | Ordinary sources only; no credentials/private/Agent-history archive or merging |
| M4 · Capture and OCR | Inbox, browser page/selection/image extension, provenance/dedup, offline Windows OCR and identical-file detection | Source-loaded extension; no store publication, complete webpage mirror or image similarity |
| M5 · External access and transfer | Explicit-scope read-only stdio MCP, real subprocess client tests, export/copy/restore snapshots | No external writes, remote OAuth, broad client certification, ownership handoff or automatic sync |

## Next choices

- Gather real library feedback and tune Chinese/paraphrase retrieval using a separate evaluation set; measure larger datasets before introducing ANN or multimodal search.
- Validate browser compatibility, OCR languages and actual third-party MCP clients; expand only around concrete workflows.
- Design explicit single-writer ownership and conflict copies before automatic sync. Portable restore already provides manual device transfer.
- Signing/automatic updates, mobile capture, structured queries and scheduled review remain separate work. Full marketplaces and real-time collaboration are deferred.

## Near term: stability and maintenance

- Gather Windows 10 / 11, WebView2, display scaling and codec feedback.
- Prioritize startup, imports, playback recovery and reading-data preservation while keeping source files unchanged.
- Gradually separate shared scripts and native bridge logic by library, playback, reading, AI and private storage; add regression coverage where data safety matters.
- Keep both languages, themes, keyboard controls, compact windows and reduced motion consistent.
- Before release, reconcile package / diagnostics / MCP / documentation versions and verify the current package and remote CI. Track signing and automatic updates as separate release work; historical passes do not validate a new candidate.

## Later: broader playback inside kok

Broader built-in playback remains a future goal. **The current version uses WebView2 without a separate decoding engine**; see the current [format matrix](docs/FORMATS.en.md). Optional FFmpeg only handles download merging / conversion.

- Evaluate embedded engines and codec libraries for size, memory, startup time, hardware acceleration, stability and redistribution licenses.
- Prioritize MKV / AVI / MOV / MP4, HEVC / H.265 and common audio codecs; record container, video-codec and audio-codec results separately.
- Preserve the library, progress, notes, sidecar subtitles and speed controls before adding embedded subtitles and multiple audio tracks.
- Test seeking, synchronization, resume, damaged files and decoder failure recovery with actual files.
- Evaluate an optional compatibility pack to keep the base app lightweight.

These are directions rather than delivery commitments. No engine, additional package size or release date has been selected.

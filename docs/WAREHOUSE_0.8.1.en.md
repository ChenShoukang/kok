# v0.8.1 · Modular media warehouse

[中文](WAREHOUSE_0.8.1.md) · [Architecture](ARCHITECTURE_0.8.1.en.md) · [Module contract](MODULES_0.8.1.en.md)

This is an unpublished local development preview. Compatibility cleanup is not complete.

All business actions use native dispatch, declared policy and argument schemas, module switches and field ownership. Domain services use narrow ports. Declarative navigation, commands, contextual actions, events and close snapshots share the module shell.

SQLite stores core items, facets, common links, workspace objects, Operations, daily activity and derived caches. Migration preserves the exact JSON input and validates a staged database before switching. Private SQLite databases are processed in memory and sealed before writing, including their full-text index; original source files remain at their existing paths and are not encrypted by this feature.

FTS5 trigram search covers names, notes, tags, annotations, generated subtitles, video outlines and managed-document text. TXT, Markdown, CSV, JSON, LOG, HTML, DOCX, EPUB, PPTX and local subtitle text can be indexed. Short queries use substring lookup. Background searches retain candidates before scope/collection filtering; this is checked against 6,001 SQLite items. Saving managed text removes stale indexed content.

Paged Operations cover ordinary metadata, bookmarks, annotations, drafts, organization, imports/removals and workspace objects. Undo checks current values before changing them. Managed documents retain encrypted byte versions and preserve external edits. History is not silently discarded; compatibility exports include all records beyond the in-memory window.

Activity combines Agent, downloads, translation, scans, imports, speech, exports and content jobs. Resumable jobs expose pause/resume; other jobs expose cancellation. Restart does not run tasks automatically. Parameters and checkpoints are encrypted locally, and completed jobs link to their results.

Today provides continue cards, clips, activity and an input. Inspector contributes details, notes, backlinks, media sections and contextual AI. Unsaved notes participate in close snapshots and flush before library switches. Clips retain text ranges, media time ranges or photo regions and can reopen their source, join Spaces, or be dropped into notes/slides. Agent tools can read clips, propose new clips and assemble notes from them.

Spaces support grid, canvas, timeline and graph views. The graph reuses the background-layout Canvas renderer, with a 500-node display bound and resource cleanup on exit. Smart collections query saved conditions dynamically. Recipes provide field controls, source pickers, reorderable steps and four templates. Full-plan preflight checks policy before execution; changed sources invalidate approval. Outputs share Operations. AI summaries use input/provider/language fingerprints, a bounded local cache and explicit output limits. Review retains daily activity and can save a Markdown note.

Viewers share View/Edit/Notes/AI tabs and per-kind immersive/work preferences (F8). New interfaces support Chinese and English and retain the user's theme and default Smiley Sans font.

Graph/backlink/Space relationships read common SQLite links. Parsing refreshes those rows rather than maintaining a second UI-only edge store.

Agent sessions, rendering and task interactions now live in agent-runtime.mjs instead of three global scripts. Its 57 former top-level definitions stay module-scoped behind one AgentUi contract. Disable flushes drafts; re-enable restores a paused task without replay. Saves serialize and coalesce unchanged content; failures retain previous records and remain retryable.

Document creation/autosave and PPT preview/edit/draft implementations now use real ES modules behind DocumentUi / PresentationUi; the two old global scripts are no longer loaded. PPT saves serialize and flush changes made during earlier writes; failures preserve the saved version and remain retryable. Creation formats, entry points and Ctrl+N follow module switches.

The common viewer preserves button identity/focus. Arrows/Home/End navigate its toolbar; Space/Enter activate controls without media shortcut interference. Inspector position follows the measured header; wide windows reserve media space and narrow windows use an overlay. viewer-shell.css owns toolbar/photo geometry, replacing competing rules in older sheets. The build remains at 42 sources and nine runtime bundles.

Workspace, clip interactions and the common viewer now use real ES modules. Forty-two stylesheet sources generate nine ordered runtime bundles and share design tokens; this does not mean every legacy component style has been consolidated. Clip, Space and smart-collection views render at most 120 cards per page; graph views keep their independent 500-node bound.

Agent can call the subtitle module’s own asynchronous tool, with inherited cancellation, durable receipts, Jobs and Operations. It uses an already configured local recognizer or speech API and does not install models automatically. This round validates the module port with a mock, not real speech recognition.

Remaining acceptance work: complete isolation of legacy implementation into ES modules, consolidation of layered CSS and Classic/Graphite component differences, and broader cross-window/large-library stress checks. The UI was checked with 2,500 synthetic clips, while SQLite search/scope handling was checked with 6,001 items and compact/large windows; this is not a long-duration stress test over a huge on-disk library. Named extensions do not mean compatibility implementation has disappeared. Scanned PDFs still need OCR; unsupported codecs, legacy Office formats and DRM books do not gain new decoding support. Cancelling imports retains committed entries and original files.

Verification includes migration preservation, Chinese queries, conflicts and byte undo, private indexing, local recipes and stale-source rejection, photo clips, drag/drop and draft protection. Eleven user-provided files were tested read-only. These tests do not establish full-milestone acceptance.

Node.js is not a runtime requirement. SQLite is bundled; Acorn is development-only. Packages exclude test inputs, credentials, databases, private records, encrypted file versions and test output.

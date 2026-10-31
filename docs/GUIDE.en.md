# Using kok

v0.9.0 adds source indexing, local semantic search, browser capture, Windows OCR and portable export/restore. See the [new workflow guide](LIBRARY_0.9.0.en.md).

[中文](GUIDE.md) · [Project home](../README.en.md) · [Documentation](README.en.md)

kok connects local media, images, documents and books to one workflow for organizing and recording what matters. Use it for playback and reading, or go further with notes, source-linked Clips, topic Spaces and optional AI. This guide describes v0.8.1 Preview; available controls depend on mode, enabled modules and format.

## Four useful concepts

| Name | Purpose |
| --- | --- |
| Library | Reference local files and browse videos, photos, music, documents and books, with collections, favorites, recents, sorting and search |
| Space | Gather files and Clips around a topic; a source can belong to several Spaces, displayed as a grid, canvas, timeline or graph |
| Clip | Record text, a time range or a photo region with a return link to its source; a time-range Clip does not create a new media file |
| Smart collection | Save filters such as PDFs imported in the last seven days that remain unread; results update locally without an AI request |

Collections and Spaces change application relationships; they do not move disk files.

## Collect, find and resume

Drop files/folders or choose **Import files**. kok detects supported types and skips already indexed paths. Scan folders you select to discover new files; scanning and local classification do not require a model.

**Today** brings together resume items, recent Clips and running tasks. The library browses by type. Full-text search covers indexed names, text, notes, annotations and subtitles where extraction is supported; see [formats](FORMATS.en.md). Scanned PDFs do not gain searchable text automatically.

Drag files into ordinary collections to classify them. A private-folder move requires confirmation and a password. Ordinary collections are not encrypted storage.

## Watch, listen and view

| Type | Features |
| --- | --- |
| Video | Resume, 0.5×–3× speed, silence acceleration, borderless fullscreen, orientation-aware windows, covers and right-click properties; matched/manual sidecar subtitles and configurable playback/seek shortcuts. Picture-in-picture depends on runtime support |
| Music/audio | Albums, artists, personal audio and all-audio views; local tags/artwork, manual corrections and online match candidates; queues, resume, shuffle and repeat. Recordings, podcasts and effects can be categorized separately |
| Photos | Wheel zoom, pan, rotate and slideshows; free/fixed crop, colors, filters, vignette, flip and original comparison; text, drawing, arrows, masks, layers and undo/redo |

Independent previews can be moved freely. Double-click blank photo/document content to send that window behind the main window. Shared viewers provide **View / Edit / Notes / AI**; `F8` switches work/immersive layouts, remembered by file type.

## Read, annotate and write

Use **New** to create Markdown, DOCX, TXT or PPTX. Text documents have editing/source/preview views, auto-save and `Ctrl S`; creation choices follow enabled modules.

- **Books**: EPUB contents, TXT chapter detection, full-text search, bookmarks and resume; single pages, adaptive spreads or scrolling; wheel paging, independent reading fonts, size, line/letter/paragraph spacing, width and comfortable paper colors.
- **PDFs**: local pages, text search, zoom, dark/easy text, underlines, highlights, notes and positioned text. Annotations are stored in kok rather than written directly into the original PDF.
- **PPTX**: slide lists, notes, presentation mode, basic text/page editing and exported copies. Supported editable text/objects offer right-click AI changes with a preview before applying.
- **Notes**: write alongside a source or attach thoughts to selected text. Markdown supports `[[link]]` completion, backlinks, tags, outlines, tasks and daily/meeting/reading templates.

The knowledge graph shows existing file/note relationships, with scope filters, search, zoom and dragging. It does not establish factual relationships on its own. The current graph displays up to 500 nodes.

## Keep useful moments

Open a source and choose **Create clip** in its details. Text can use your current selection; audio/video stores a start/end time; photos support region selection. Name the Clip, add text or thoughts and find it again under **Clips**.

Add files and Clips to Spaces, drop them into notes as references or into a presentation to create a slide. Source files must remain available; Clips do not back them up. Use the canvas for manual arrangements, the timeline for chronological review and the graph for established relationships.

For example, create a travel Space with an itinerary, photos, recordings and video. Save route passages, interesting timestamps and photo regions, then use them in a travel note or presentation.

## Where AI helps

Choose Smart mode and configure a compatible endpoint in **Settings → Tools → AI service**. See [AI setup](AI_PROVIDERS.en.md) for model lists, connection tests, presets and credential isolation. Entering Smart makes no model request; using an AI feature sends its selected context to your service.

| Entry | Useful tasks |
| --- | --- |
| Viewer/right-side AI | Ask about the current file, selection or notes; explain a word or passage; request rewrites in supported editable regions |
| Translation | Selected text, side-by-side or translation-only documents/books, bidirectional scroll, local translation/mark recovery and supported matching-format copies |
| Multi-file comparison | Select readable sources, preview excerpts, compare agreements/differences/evidence and return to sources |
| Note review | Review highlights, annotations and personal notes, choose related earlier notes and save reports or export Markdown |
| Subtitles | Translate existing subtitles and generate a navigable outline; separately configure local Whisper or a speech API to transcribe unsubtitled video |
| Agent | Search, read and discuss within selected files, a collection or the library; propose reviewable classification, tags, notes, new documents or presentations |

Video questions primarily use subtitles, optionally a current frame when supported. Long documents/subtitles may be sampled. Images require a vision-capable model; a chat API is not speech recognition. Check source coverage before treating an answer as an analysis of an entire film or book.

### Give Agent a bounded task

Choose files and open Agent, drag sources into Agent or open AI alongside a file. Use discussion-only mode to explore; collaboration mode enables reviewable proposals.

Try requests such as:

- “Compare these three documents and point out claims I should check in the originals.”
- “Suggest collections and tags for these reading materials. Show the plan first.”
- “Turn the key points in these sources into a Markdown note.”
- “Create a concise presentation draft from this text.”

Expand reading steps and sources to inspect evidence. Changes appear as confirmation cards that you can apply, skip or undo. Changing scope stops sending the earlier scope's conversation. Stopping, resuming, restoring and handing off tasks are explicit actions; reopening does not replay unconfirmed writes.

Four built-in instruction skills cover organization, comparison, reading notes and slides. Import your own instruction skills without granting script execution. Configure request limits, context budgets and task costs; estimates use the prices you enter and do not replace provider bills. See [Agent scope](AGENT_0.7.2.en.md) and [review interactions](AGENT_INTERACTIONS.en.md).

## Save repeatable steps as Recipes

Create a Recipe, select its sources and edit/reorder steps. Four starting templates are available:

| Flow | AI requirement |
| --- | --- |
| Read → Note | None |
| Read → Summary → Note | Summary |
| Read → Summary → Presentation | Summary |
| Existing subtitles → Summary → Note → Presentation | Summary; readable subtitles must already exist |

Preview the whole plan and confirm before running. Recipes connect supported capabilities; they are not arbitrary scripts. Outputs can enter operation history, and changed sources block reuse of an old plan.

**Activity** gathers background progress and cancellation; only resumable tasks offer pause/continue. **History** inspects and undoes integrated changes, preserving later manual edits when conflicts occur. **Review** records daily usage/progress and can be saved as a note.

## Editing, exports and optional components

| Feature | Output and limits |
| --- | --- |
| Photos | PNG/JPEG copies preserve originals; animation editing is static, with an output long edge up to 4096 px |
| Audio/video | Trimming, splitting, multiple-source timelines and clip captions; actual export uses optional FFmpeg/FFprobe |
| DOCX/EPUB | Editing/export rebuilds simple body formatting rather than preserving every complex layout; newly created managed documents can save edits directly |
| PDF | Merge, reorder, rotate and annotate; canvas-edited pages export as images, while untouched pages retain their original content. Scans still need external OCR |
| PPTX | Basic text/slide editing, notes and copies; complex masters, charts, animations and embedded objects cannot be fully preserved |
| Video downloads | Bilibili/YouTube/Douyin adapters, generic pages and direct links, quality/source choices, audio/subtitles, queues, resume, sign-in sessions and import. Split-track/M3U8/DASH merging needs optional components. Site changes may break parsing; not all signed-in, encrypted or paid content is supported |
| Speech captions | Local recognition needs an engine/model; a speech API uses separate configuration and does not receive your chat provider's key |

## Privacy, backups and preferences

Pure mode blocks AI requests and keeps local essentials offline. Smart does not make every operation online: file reading, collections and Spaces remain local. Set Chinese/English, light/dark themes, background/glass colors, bundled fonts and enabled modules to suit you.

The private folder lives inside the library rather than being a third app mode. It encrypts application records, notes, covers and related data, disables AI and locks on deactivation, minimize or idle. A move requires confirmation; traceable ordinary records/copies are cleaned after the encrypted destination is saved.

**Original files remain unencrypted. Earlier independent AI history, exports and external backups are not retroactively encrypted. Lost passwords cannot be recovered.** Neither ordinary JSON exports nor encrypted `.kokvault` backups contain originals. Follow [privacy and restoration guidance](MODES.en.md).

| Shortcut | Purpose |
| --- | --- |
| `Ctrl K` | Search and commands |
| `Ctrl N` / `Ctrl S` | Create a document / save supported edits |
| `F8` | Work/immersive viewer layout |
| `Ctrl Shift I` / `Ctrl Shift A` | Inspector / contextual AI, when available |
| Video `Space`, arrows, `F` | Play/pause, seek, fullscreen; customizable |
| `Ctrl Shift L` | Lock the private folder |

See [formats](FORMATS.en.md), [validation and limits](VALIDATION_0.8.1.md#english) and [support](../SUPPORT.md).

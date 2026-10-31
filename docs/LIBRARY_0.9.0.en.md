# v0.9.0: search, capture and portable libraries

[中文](LIBRARY_0.9.0.md) · [Home](../README.en.md) · [Validation](VALIDATION_0.9.0.md)

Open **Search and library data** in the sidebar or settings. Content indexing, capture, OCR and portable export/restore work in Pure mode. Local embeddings still count as AI and require Smart mode. These services exclude the private folder.

## Index and search

Imports and edits schedule incremental content updates. Use **Update content index** manually, or inspect coverage, failures, missing originals and partial results. Originals are preserved. The index includes metadata, notes, independent Clips, supported document bodies, PDF text layers, slide text/notes and the first available subtitle track. Results retain chapter/page/time or the Clip's original anchor and recheck source versions before opening. Edited, deleted or private sources invalidate old shared excerpts.

Limits include 2,000 chunks per source, 1,200 characters per chunk with 100-character overlap, a 32 MB input limit for some text formats, and 2,000 pages / two million extracted characters per PDF. Limits produce partial/failed coverage; PDFs without a text layer need OCR. Complex layouts may need manual position checking. There is no general large-library capacity claim.

Install [Ollama](https://ollama.com/) and a local embedding model yourself, for example `ollama pull embeddinggemma`. Save the local endpoint (default `http://127.0.0.1:11434`), model and revision in kok, enable it, then build the semantic index. Chat and embedding settings are separate. kok does not download models automatically or send embeddings to remote endpoints.

Choose lexical, semantic or hybrid search. Hybrid combines FTS5 and vector ranks; global search and Agent reuse the same retrieval path. Similarity candidates are not factual answers. The default minimum similarity is 0.35 and unrelated queries can still produce approximate candidates. See the [real local-model comparison](VALIDATION_0.9.0.md).

Vectors are cached in local SQLite. Change the revision and rebuild after replacing a model under the same name. Model, endpoint, revision or chunk-version changes prevent cache reuse. Disabled/unavailable models, absent vectors or incompatible dimensions fall back to lexical search with a notice. Pure mode blocks model requests.

## Capture and OCR

Paste a title, URL and text into Quick capture to save it in Inbox. For browser page text, selections or images, enable paired capture, show the connection setup, and load the release's `browser-extension` folder as an unpacked Chrome/Edge extension in developer mode. Enter the displayed Capture URL and pairing key in extension settings. Keep kok open. Use the popup or context menu, inspect the result and retry failures. Image capture may require site access.

Captures retain URL, time and a content fingerprint; identical repeated payloads reuse an existing entry. The first version saves cleaned page text, not a complete offline webpage mirror. Images become PNG copies. The extension is distributed as [source](../browser-extension/README.md), not through extension stores.

Offline OCR uses installed Windows OCR languages. Select an image or 1–20 PDF pages per run. Text enters the indexed page without changing the original. Install a Windows OCR language if unavailable. Image understanding and reverse image search are outside this version.

Find identical files groups exact SHA-256 matches without deleting them. Relinking creates a reviewable operation, retaining source IDs and Clip relations; apply or undo it through History.

## Portable export, restore and moving devices

Export creates a `.koklib` standard ZIP with `manifest.json` (v2, with v1 restore compatibility, stable IDs, metadata, tags, references, workspace relations and attachment sizes/hashes), `notes/*.md` (notes, managed-document text and Clips), `clips.json`, `spaces.json` and optional `sources/*`. Managed documents are always included; other originals require the include-originals option.

Limits: 2 GB per attachment, 8 GB total attachments, 10,000 restored sources. The portable library excludes credentials, pairing, chat/task history, memory, private data, cover caches and embeddings. It supports ordinary-data interchange and snapshot transfer, not a complete data-directory backup. Ordinary exported data is plaintext.

Restore previews counts and creates an independent new library after validating IDs, relations, sizes and hashes. Existing libraries stay open and are not overwritten. Click **Open restored library** to inspect the new window; its default is Pure mode. Alternatively run `kok.exe --data-dir "restored-directory"`.

New v2 manifests use relative source paths, with external filenames alone by default. An optional export root preserves folders relative to that root without recording its absolute path; review the relative layout before sharing. Excluded originals are linked only when a new root is explicitly supplied. Legacy v1 archives also require their old root. Otherwise sources remain missing for later relinking. Rebuild indexes and configure models/connections again. Device transfer means export → copy → restore into a new directory. There is no background sync, ownership handoff, conflict merging or mobile client. Do not treat synchronizing an active SQLite file as supported sync.

## Read-only access for external agents

Enable external reads, choose the current sources and apply the permission. Copy the displayed stdio MCP configuration to your client. It runs your absolute `kok.exe` path with `--mcp --data-dir <library-directory>`. Keep the same library open in kok. Credentials are loaded from encrypted local settings and are absent from command arguments.

Protocol version: `2025-11-25`; public transport: stdio. The internal loopback bridge is not a remote Streamable HTTP endpoint. Tools: `list_sources` (paged), `search_library` (the same configured local hybrid search as UI/Agent, with lexical fallback and a notice), `read_excerpt`. Build the content index first. Results exclude local paths and respect a snapshot of explicitly shared source IDs. New imports do not become shared automatically. Disable access at any time; private/transitioning libraries reject reads. Capture and read credentials are separate, connections default off, and only loopback is bound. No external writes, remote MCP/OAuth or broad third-party client certification is claimed.

## Agent review changes

Existing plans, approvals, memory, read-only delegation and sandbox calculation remain. A bounded exact-decimal `calculate` tool, write read-back and continuous approved-write evidence checks improve result review. Approval supports per-call, read-only and limited current-task permissions. Events are versioned and interrupted delivered prose is preserved as interrupted. Receipt review cannot guarantee every semantic claim. Deterministic replays and actual model evaluations are reported separately.

Historical plan steps require successful, non-undone execution receipts; present content checks require current receipts. After approved metadata writes, the host automatically executes a real scoped read_items and journals its independent outcome without a model request. Completion requires a current successful post-write read; a failed, forbidden or pre-approval read cannot substitute. Historical reads do not invalidate a completed plan solely because the source changed. Re-enabling external access rotates credentials, so pair the browser extension again.

Content indexing updates incrementally. After source changes, build embeddings again; unchanged chunks reuse vectors. The current source version includes metadata, so metadata edits can also invalidate extracted chunks; rerun OCR for scans when needed.

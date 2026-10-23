# Smart library

[简体中文](SMART_LIBRARY.md) · [Documentation](README.en.md)

Choose **Smart library** in the sidebar to open the visual folder discovery and AI organization workspace. It follows the interface language, light/dark theme, compact window layout and system reduced-motion preference.

1. Use the plus button beside **Discover files** to choose a folder. The first scan starts immediately. While kok is open, new files are checked every 45 seconds, including subfolders.
2. Pause, resume or remove a source, or start/stop a scan manually. Hidden, system and linked directories and kok's own data are skipped. Inaccessible directories appear as notices on the page.
3. Configure your existing AI service and choose files not yet organized or all files. Each run handles up to 50 files; uncheck files to exclude them.
4. By default, only names, formats, sizes and parent folder names are sent. Full paths, body text and media are excluded. Optional excerpt mode adds up to 1,200 text characters from TXT, MD, MARKDOWN, CSV, LOG, JSON, DOCX or EPUB files of at most 2 MB. Other formats use metadata only.
5. Start organizing to preview categories, tags and summaries. Cancellation applies no results. Uncheck suggestions you do not want and choose **Apply selected categories**. Categories appear in the library sidebar. Identical inputs use a local response cache.
6. Use **Edit category and tags** in a file's menu to edit its category, up to eight tags and summary. Library search includes categories, tags and summaries.

Original files stay in place and are never moved or renamed. Automatic imports deduplicate full paths. Removing an entry excludes it from later scans; manually importing it restores it. Pausing or removing a source keeps existing library entries.

AI organization shares the API endpoint, model and encrypted credential from **AI translation**. Output language follows the interface language. Credentials are stored only in local `data/ai-settings.json`, encrypted with Windows DPAPI for the current account. New files are not automatically sent to AI; requests begin only when you start organizing. Your provider may charge for requests. Name-based inferences can be inaccurate; review suggestions before applying. This version does not analyze image content, transcribe audio, understand video frames or perform PDF OCR.

## Timeline, type and custom organization

The default Timeline groups files by modification month, newest first. Content type separates videos, photos, music, documents and books. Both work locally without AI. Custom organization supports assigning selected files to existing or new categories without moving originals. AI organization remains available separately.

## Content AI chat and adaptive previews

Use AI chat in the video, document or book viewer to open a right-side assistant. Summarize, ask follow-up questions, discuss selected text and save answers as thoughts. Each file retains up to 40 messages within a combined 160,000-character budget; requests include the most recent 12 messages. Chat shares your configured API and sends only when you ask.

Choose the current chapter/PDF page, full text or subtitle scope, optionally including notes and highlights. Videos default to subtitles within 90 seconds of playback. You can choose all subtitles and optionally send a compressed JPEG of the current frame to an image-capable model. A single frame does not represent an entire video. Long contexts use relevant excerpts; PDFs above 40 pages use the first and last 20 pages. Sampled scopes are explicitly labeled. Scanned PDFs still need OCR. Stopping a response does not save partial answers.

Video and photo previews read actual pixel dimensions and adapt their window to landscape, portrait, square and photo rotation. The header provides an adaptive/full-window toggle. Originals remain unchanged.

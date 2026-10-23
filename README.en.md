# kok

[中文](README.md) · English

A lightweight Windows library for videos, photos, music, documents and books, with a clean glass interface.

**v0.3.1** · [Download](https://github.com/ChenShoukang/kok/releases/tag/v0.3.1) · [Supported formats](docs/FORMATS.en.md) · [Modes](docs/MODES.en.md) · [Changelog](CHANGELOG.en.md)

![kok library](docs/screenshots/overview-en.jpg)

## Getting started

1. Download `kok-0.3.1-win-x64.zip` from Releases, extract to a writable folder and run `kok.exe`. Keep the accompanying files and folders.
2. Choose Pure or Smart on first entry. Open the password-protected Private folder inside your library when needed.
3. Drop files or folders to import by type, use the import button or explore the original bundled samples.

Requirements: **Windows 10 / 11 x64, .NET Framework 4.8 and WebView2 Evergreen Runtime**. kok uses the installed runtime rather than bundling Chromium. Install a missing runtime from [Microsoft](https://developer.microsoft.com/microsoft-edge/webview2/). Node.js and the .NET SDK are not runtime requirements. The portable app is unsigned; check its supplied SHA-256.

## Modes and the private folder

- **Pure**: core local media, reading and notes with a simple interface. The native host blocks AI calls and app resources load offline.
- **Smart**: local features plus optional translation, content chat, watched folders and organization previews. Entering the mode does not call a model.
- **Private folder**: a password-protected folder inside the library with encrypted index, notes and covers, with AI disabled. Locks on loss of window focus, minimize or five minutes idle. **Original source files stay in place and are not encrypted.** Passwords cannot be recovered. See [Modes and privacy](docs/MODES.en.md).

## Features

| Area | Features |
| --- | --- |
| Video | One-row glass controls, borderless fullscreen, resume, speed, silence acceleration, sidecar subtitle matching, configurable shortcuts, covers, right-click properties and orientation-aware previews |
| Music | Album shelf and details, queues, continuous playback, shuffle, repeat, volume and seeking |
| Photos | Adaptive preview, zoom, rotation, slideshow; filters, color adjustments, crop, flip, undo / redo, PNG / JPEG copies |
| Documents and books | Local PDF viewer, DOCX / EPUB / text reading, contents and search, underlines, highlights, text insertion, thought popovers and notes, HTML reading-copy export |
| Organization | Timeline / type / custom groups, collections, favorites, recents and search; Smart adds selected-folder scanning and AI previews |
| Appearance | Chinese / English, light / dark / system themes, three Chinese fonts, TTF / OTF import, glass menus and reduced-motion support |

See the [measured format matrix](docs/FORMATS.en.md). Media playback depends on **container, codec, runtime and system**; kok does not bundle a full decoder collection. Independent decoding remains a [roadmap goal](ROADMAP.en.md). Unsupported formats can open in an installed system application.

DOCX / EPUB reading extracts text without complex original layout; scanned PDFs need external OCR and DRM is not decrypted. Annotations, inserted text and photo copies preserve originals. Subtitles: SRT, VTT, ASS / SSA text compatibility, MicroDVD SUB, SBV, LRC, TTML / DFXP and SMI. Embedded / bitmap extraction and speech transcription are not included.

## AI configuration and context

Smart → Settings → AI service: choose a preset or custom Chat Completions-compatible endpoint, enter the model and credential, and optionally load model IDs. Presets include DeepSeek, OpenAI, Qwen, SiliconFlow, BigModel, Z.ai, Gemini compatibility, Groq, OpenRouter, Ollama, LM Studio and Custom. Individual models have different capabilities; see [Provider configuration](docs/AI_PROVIDERS.en.md).

Video chat uses loaded subtitles and an optional current frame; **it does not upload or analyze the entire movie**. Document and book chat can include current content, selected text and notes; long context is sampled with a visible notice. Organization can use names only or limited text, previews changes and waits for application. It updates library records without moving source files. See [Smart library](docs/SMART_LIBRARY.en.md).

Remote requests send the selected feature context and follow provider billing. Automatic translation defaults off; connection testing sends a short request. Credentials are used only by the native host, encrypted locally with Windows DPAPI in `data/ai-settings.json` and never returned to the web UI. Changing the endpoint requires entering its credential again. Source, packages and Releases exclude personal configuration, libraries and caches.

## Data and backups

The ordinary `data/` folder stores the index, notes, preferences, fonts and caches. Entries refer to original paths; removing an entry does not delete its source. Export JSON index/notes from Settings, or close kok and copy all of `data/`. JSON includes paths, not media or credentials.

Private folder Settings exports an encrypted `.kokvault` backup containing index, notes and covers, excluding original files, AI configuration and browser caches. See [private backup and protection boundaries](docs/MODES.en.md). Use `--data-dir "D:\MykokData"` for a separate directory.

Shortcuts: `Ctrl K` search, `Esc` close, video `Space` pause, arrows seek, `F` fullscreen, and private `Ctrl Shift L` lock. Video shortcuts and seek increments are configurable.

## Build and verification

```powershell
npm ci --omit=optional
npm run check
.\build.ps1 -Test
.\tools\test-desktop.ps1
.\package.ps1
```

Development uses Node.js 24, the Windows .NET Framework compiler and a pinned official WebView2 SDK. Desktop tests use isolated libraries, silent windows and mock AI services without personal credentials. Packaging audits private files and credential patterns. See [Development and tests](docs/DEVELOPMENT.en.md).

## License and references

Original code and samples use [MIT](LICENSE); PDF.js and WebView2 terms are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). References include [WebView2Samples](https://github.com/MicrosoftEdge/WebView2Samples) for host integration, [Foliate](https://github.com/johnfactotum/foliate) for reading experience and [mpv](https://github.com/mpv-player/mpv) for player scope. External players and their GPL implementations are not bundled.

# v0.6.2 · Graphite interface

[中文](GRAPHITE_0.6.2.md) · [Back](../README.en.md)

Dark graphite and light frost share clear hierarchy, ice-blue feedback and subtle tactile controls. Bundled Smiley Sans is the default. Settings still offers WenKai, system fonts and local font imports; saved theme/font/reading-paper preferences take priority.

| Area | Changes |
| --- | --- |
| Library | Labelled sidebar that collapses for compact windows; title-based book/document covers; actual music artwork or a record fallback |
| Reading and PDF | Recessed segmented tools, moving selection feedback and ice-blue progress; text follows the common font unless an independent reading font is selected |
| Video and photos | Consistent dark viewing surface and readable chrome in both interface themes; compact controls and horizontally scrollable photo tools |
| Audio bar | Matching transport, clear states and responsive layout; opening a page does not start playback |
| Dialogs and menus | One interruptible animation owner, stable settings bounds and visible notification indicators |

Retains [knowledge graphs](KNOWLEDGE_0.6.1.en.md), [new documents and auto-save](DOCUMENTS_0.6.1.md), translation, caption generation, the private folder and existing editors. See [motion](MOTION_0.6.1.en.md) for timings, interruptions, reduced motion and open-source references.

Source and portable packages exclude personal libraries, model configuration and credentials. Playback still depends on WebView2 codecs; download/speech components are optional. See [formats](FORMATS.en.md) for scope.

## r2 quality revision

`ui/theme.css` owns the default palette; appearance scripts write custom colors only. Existing component layout files remain. Graphite force overrides fall from 30 to 4, preserving interaction and accessibility rules. `ui/motion-core.js` loads before the application, owning surface motion and interrupted exits; active transitions settle when reduced motion or native window gestures begin.

Renderer snapshots time out after five seconds. A failed exit save defaults to keeping the window open for recovery, retry or export; a timeout is never reported as a successful save. The private folder inherits bundled font choices and states that originals are not encrypted. Editor footers clarify image output for annotated PDF pages and simplified formatting for imported DOCX / EPUB text.

Local validation passed 132 native checks and seven silent desktop suites: Graphite, motion, documents, reading/photo clarity, graphs, native dragging and palette/motion/exit recovery. Recovery exercises an actual native test window with an unresponsive renderer; no personal data or AI services are accessed. This remains a preview, without comprehensive coverage of hardware, DPI, complex documents or codec combinations.

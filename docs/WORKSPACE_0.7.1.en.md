# v0.7.1 · Scoped graphs and tactile files

[中文](WORKSPACE_0.7.1.md) · [Back](../README.en.md)

Local preview, unpublished. Existing media, reading, editing, translation and caption features remain. Application and Windows file versions are v0.7.1.

Choose all files, a collection, favorites or explicit files; combine collection, tag, type and added-date filters. Save up to 12 scopes; select 100/250/500 visible nodes or one/two-hop neighborhoods. Filter real files before deriving tags and missing-note nodes so hidden files cannot bridge scope boundaries. Pure mode supports local graph tools.

Smart mode adds three versioned built-in tasks: find connections, summarize scope and ask a question. Local preparation selects and deduplicates readable excerpts. Preview the exact content before choosing Analyze; preparation makes no model request. Choose 1–24 files, at least two for connections. Files without readable text or notes are skipped; this workflow does not recognize image/PDF/video pixels or send local paths.

| Usage | Context characters | Maximum answer tokens |
| --- | ---: | ---: |
| Economy | 6,000 | 900 |
| Balanced | 14,000 | 1,400 |
| More context | 24,000 | 2,000 |

These are budgets, not exact price estimates. Matching evidence, recipe, question, language, provider/model settings and budget reuse a persisted result, bounded to 12 reports. Changed evidence requires another preview. Invalid/incomplete results are rejected without automatic retries. Connections carry evidence and remain suggestions; originals stay unchanged. Save an analysis as a Markdown note with sources. Closing, changing scope or entering Pure mode cancels work and rejects late replies. Private content stays excluded.

Books have upright spines and page edges; documents resemble paper, videos use wide frames, photos use picture frames and music retains albums/records. A centered vector safe represents the private folder. Each of 56 import formats has its own format stamp, plus subtitle/font artwork, with bounded hover/focus feedback and reduced-motion/native-gesture handling. Stamps identify formats without implying new codecs or full Office fidelity. Compact AI uses the work area with a Back to graph action instead of squeezing both panes.

The library and graph add no runtime dependencies, fonts, codecs or bitmap packs. Original SVG/CSS artwork shares family geometry, and icon/result caches are bounded. The existing worker stops when settled; native browser animations and analytic damped motion remain in use. PPTX editing adds a small export component loaded on demand; see [presentations and contextual AI](PRESENTATION_AGENT_0.7.1.en.md).

Design references: [Logseq scopes](https://github.com/logseq/logseq/blob/master/src/main/frontend/components/graph.cljs), [Kavita covers](https://wiki.kavitareader.com/guides/admin-settings/media/), [Khoj evidence prompts](https://github.com/khoj-ai/khoj/blob/master/src/khoj/processor/conversation/prompts.py), [Affect UI](https://github.com/flohoeller/affectui), [Radix states](https://www.radix-ui.com/themes/docs/components/button), [VSCode Icons](https://github.com/vscode-icons/vscode-icons), [Fluent icons](https://github.com/microsoft/fluentui-system-icons), and Remotion's [time interpolation](https://www.remotion.dev/docs/animating-properties)/[damping](https://www.remotion.dev/docs/spring). Concepts informed the implementation; their application code/runtimes were not copied or bundled.

Tests use generated files and a local mock API, never personal data or real model credits. Coverage includes scope intersections/persistence, no-request previews, request budgets, cache/source changes/cancel, 500 nodes, rapid filtering, filtering during dragging, real native resize/maximize bursts, light/dark, Chinese/English, compact layouts and reduced motion. More hardware, DPI and real-document coverage remains necessary.

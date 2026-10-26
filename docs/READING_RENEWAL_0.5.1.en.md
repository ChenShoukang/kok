# Reading and window fixes · v0.5.1 preview

[中文](READING_RENEWAL_0.5.1.md) · [Documentation](README.en.md)

This preview focuses on reading, translation and viewer behavior.

| Area | Changes |
| --- | --- |
| Contents | EPUB NAV / NCX hierarchy and fragment offsets, including repeated headings and empty anchors; Word heading styles, inherited styles and outline levels; Chinese / English text headings and Markdown headings; section fallback when no contents are available |
| Reading | Books default to paged reading, documents to scrolling. Independent fonts, 14–48 px size, paper / sage / night themes; upright system text at 21 px, or 24 px on wide displays unless overridden |
| Selection | Visible bookmark toggles; precise underline, highlight, notes and undo; optional selected-text translation and AI explanation in smart mode |
| PDF | Dark presentation and a selectable, font-adjustable plain-text view with pages, marks and bookmarks. Requires a text layer; no OCR or original-layout reconstruction |
| Translation | Equal typography and matched paragraph heights, scrolling from either pane, selection actions and saved bookmarks / annotations on both sides; switch between readable comparison, original layout and translation only |
| Persistence | Local complete / partial translations and comparison marks restore on reopening without another API call. Changed source files or edited reading text invalidate saved views. Matching-format export remains available |
| Speed | Fast mode uses up to four workers and larger batches; standard uses three. Fast mode disables thinking only on the exact official DeepSeek host. Existing segment caches remain reusable; provider and network limits still apply |
| Windows | Compact, normal, maximized and real fullscreen reading; unified dark video / photo chrome; one-row actions with horizontal overflow when needed; larger default text on wide displays |
| Motion | Coordinated content expansion and native-window fade after the preview is ready; no repeated startup symbol. Idle controls fade after about 3.5 seconds and wake on pointer / keyboard activity, with editing and selection guards |
| Library | Drag cards into collections; Ctrl / Shift click selects multiple files. Private-folder drops require explicit confirmation and a password; original files stay in place |

![Reading comparison](screenshots/reading-renewal-051-dark.png)

The screenshot uses isolated fixtures and a mock translation service.

Normal translations are plaintext local files under `translations/view-*.json`. Comparison marks stay with these views and are not automatically embedded in format exports. See [export limitations](TRANSLATION_CAPTIONS_0.5.1.md). Moving records into the private folder deletes their normal translation view, but does not encrypt source files, previous exports or shared segment caches. Canceling a move leaves records untouched.

Interaction and format ideas reference [affectui](https://github.com/flohoeller/affectui), [Foliate-js](https://github.com/johnfactotum/foliate-js/blob/main/epub.js) and [Koodo Reader](https://github.com/koodo-reader/koodo-reader). No component code, branding, screenshots or runtimes from those projects are bundled. The fast DeepSeek option follows the [official thinking-mode documentation](https://api-docs.deepseek.com/guides/thinking_mode/).

Focused checks use synthetic EPUB / Word / text / PDF fixtures, original media, isolated silent native windows, in-memory HTTP handlers and a local mock API. They cover 650 × 520, normal and 1920 × 1080 viewports; actual native resizing, maximize and borderless fullscreen; contents, paging, bookmarks, fonts, night colors, selected-text actions, paired layout and scrolling, persistence, private-drop confirmation and idle controls. No personal provider key is used.

Per-Monitor V2 DPI awareness replaces system-only awareness, with an older-system fallback. Mixed-DPI physical monitor testing remains outstanding. Scanned PDFs and malformed / missing book contents may require external preparation.

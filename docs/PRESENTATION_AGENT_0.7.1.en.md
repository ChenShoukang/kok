# v0.7.1 · Presentations and contextual AI edits

[中文](PRESENTATION_AGENT_0.7.1.md) · [Home](../README.en.md)

Local preview, unpublished. Type-based library sections align matching covers: portrait books, landscape video, photo frames and albums. Lists and sorting remain available.

## PPTX

View slides, contents and speaker notes, or enter fullscreen. Basic editing includes text, size, color, bold, object position and dimensions; add/delete text boxes and slides, reorder slides, undo/redo and local drafts. New documents can also be PPTX.

Drafts preserve the imported file. Export creates a new basic-layout PPTX with visible text, basic shapes, embedded raster images and notes. Complex masters, groups, charts, animations, embedded objects and mixed text styles are not fully reproduced. Check the copy before use; detected unsupported elements trigger an export notice. Legacy PPT requires an external app or conversion to PPTX.

Lightweight limits: 32 MB, 100 slides, 500 objects per slide, 20 MB of images in total. External image links are skipped and macros are not executed. No Office installation or runtime download is initiated.

## Edit the selected content

A right-click first opens a compact editing menu with applicable copy, cut, paste and select-all actions. In Smart mode, choose AI edit… to open the panel, then polish, shorten, expand, translate or enter a custom instruction. Opening either surface makes no model request.

The panel distinguishes selected text, the current paragraph, a text box and the current page. Selecting a few characters in PPTX replaces only those characters. Escape returns to the editor; outside clicks dismiss. Changing instructions invalidates an old review. Empty custom instructions or oversized sources cannot generate. Supports arrow keys, Enter, Shift+F10 and Ctrl+Enter to generate.

![Compact menu before entering AI editing, using generated content](screenshots/context-menu-071-zh.jpg)

Select → generate → review → apply; an applied edit can be undone. Whole-page edits retain exact text-object IDs and replace only that page's text. Changing page/file/source, closing or entering Pure mode cancels stale work. Late responses cannot edit a different target.

![Actual contextual PPTX editor in Chinese, using generated files and a local mock response](screenshots/contextual-edit-071-zh.jpg)

Each request sends at most 2,000 selected characters plus 2,000 nearby characters for read-only context. Output is bounded to 900–3,000 tokens according to input. There are no automatic retries or agent loops. The latest 20 matching results can be reused locally, keyed by content, instruction and model settings. Limits reduce repeated requests but do not guarantee provider-specific costs.

This flow does not directly replace PDF page text, generate images, execute arbitrary tools or reproduce a complete office editor. Private folders and Pure mode block AI.

## Instruction skills

Settings → Instruction skills imports short SKILL.md or JSON instructions and copies them as SKILL.md. Up to 20 custom entries, at most 800 instruction characters each. Only the chosen instruction is sent. Skills are text prompts, with no scripts or extension installation.

## Open-source references

- [Pi skills](https://github.com/earendil-works/pi/blob/main/packages/coding-agent/docs/skills.md): local metadata and instructions loaded on demand.
- [tldraw Editor](https://tldraw.dev/docs/editor): explicit object selection, page boundaries and undoable changes.
- [Radix Context Menu](https://www.radix-ui.com/primitives/docs/components/context-menu) and [Tiptap BubbleMenu](https://tiptap.dev/docs/editor/extensions/functionality/bubble-menu): separate menu/panel roles, keyboard focus, retained selection and viewport collision handling. Implemented with existing browser capabilities, without a new UI framework.
- [AFFiNE](https://github.com/toeverything/AFFiNE): contextual editing around the current content.
- [Jellyfin Web](https://github.com/jellyfin/jellyfin-web) and [Kavita](https://wiki.kavitareader.com/guides/admin-settings/media/): type-based shelves and cover proportions.
- [PptxGenJS](https://github.com/gitbrent/PptxGenJS): its MIT browser bundle generates exports; about 450 KiB, loaded at first creation/export. Licenses are included.
- [Remotion](https://www.remotion.dev/docs/spring): time and damping concepts, implemented using existing browser capabilities without shipping Remotion.

See [graph tasks and budgets](WORKSPACE_0.7.1.en.md). Validation uses generated files and a local mock service, without personal API keys or real model credits. It covers export/read-back, original-byte preservation, target boundaries, undo, restored drafts, both languages, compact layouts, rapid resize and reduced motion. More real-file, DPI and hardware coverage remains necessary.

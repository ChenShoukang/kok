# kok documentation

[Project home](../README.en.md) · [中文](README.md)

Start with the [user guide](GUIDE.en.md). Find help by the task you want to complete; version history and implementation details come afterward.

## Find a feature

| Goal | Read |
| --- | --- |
| v0.9.4 flat UI refresh, grouped navigation and page-by-page layout checks | [Flat UI notes](FLAT_UI.md) · [Interface references](INTERFACE_REFERENCES.md) · [Interface validation](INTERFACE_VALIDATION.md) · [Release validation](VALIDATION_0.9.4.md) |
| Search, browser capture, OCR, portable restore or external read-only MCP | [v0.9.0 guide](LIBRARY_0.9.0.en.md) · [Current validation](VALIDATION_0.9.0.md) |
| Import sources and understand Library, Spaces, Clips and smart collections | [Current user guide](GUIDE.en.md) |
| Use kok offline, switch modes, protect records or back up a vault | [Modes, privacy and data boundaries](MODES.en.md) |
| Configure a remote provider or a local model service | [AI setup](AI_PROVIDERS.en.md) |
| Give Agent a scoped task and review changes | [Agent](AGENT_0.7.2.en.md) · [Evidence and review](AGENT_INTERACTIONS.en.md) · [Task/handoff boundaries](AGENT_BOUNDARIES.en.md) |
| Use plans, request traces or local MCP tools | [v0.8.2 Agent and MCP](AGENT_0.8.2.en.md) · [Local checks and optional model evaluations](VALIDATION_0.8.2.md) |
| Use semantic compaction, memory, subagents or sandbox computation | [Agent runtime](AGENT_RUNTIME_0.8.2.en.md) · [Runtime validation](VALIDATION_RUNTIME_0.8.2.md) |
| Check files, codecs, subtitles and export support | [Formats](FORMATS.en.md) |
| Write linked notes and explore a knowledge graph | [Notes and graphs](KNOWLEDGE_0.6.1.en.md) |
| Translate documents/subtitles or configure speech captions | [Translation and captions](TRANSLATION_CAPTIONS_0.5.1.md) |
| Compare sources, review notes or build subtitle outlines | [Study tools](STUDY_TOOLS_0.5.1.md) |
| Edit photos, media, PDFs or document text | [Editing and music](EDITING_MUSIC_0.5.1.md) |
| Scan selected folders, organize files or ask about their content | [Smart library and file chat](SMART_LIBRARY.en.md) |
| Resolve installation or usage problems | [Support](../SUPPORT.md) · [Issues](https://github.com/ChenShoukang/kok/issues/new/choose) |

## Versions, validation and development

| Document | Contents |
| --- | --- |
| [v0.9.0 peer gaps and implementation plan (Chinese)](PEER_GAPS_0.9.0.md) | Workspace review, historical live evaluations, public sources, milestones and acceptance criteria |
| [v0.8.1 implementation scope](WAREHOUSE_0.8.1.en.md) | Current coverage, unfinished acceptance and compatibility boundaries |
| [Validation](VALIDATION_0.8.1.md#english) | Native/desktop evidence, reproduction and limits |
| [Development and tests](DEVELOPMENT.en.md) | Builds, isolated testing and code layout |
| [Module contracts](MODULES_0.8.1.en.md) | Dispatch, policy, capabilities, history and background jobs |
| [Architecture and recovery](ARCHITECTURE_0.8.1.en.md) | Registration, Agent events, recovery and handoff |
| [Preview readiness](PUBLIC_PREVIEW_0.8.1.md) | Privacy fixes and public-preview requirements |
| [Changelog](../CHANGELOG.en.md) · [Release draft and published notes](RELEASE_NOTES.md) | Version changes and Preview scope |
| [Roadmap](../ROADMAP.en.md) | Future goals, distinguished from implemented features |
| [Security](../SECURITY.en.md) · [Contributing](../CONTRIBUTING.md) | Reporting, development and community conduct |

<details>
<summary>Earlier versions and design records</summary>

These records describe feature evolution and implementation/design references. Their version-status statements describe that period; start with the current guide for everyday use. Some historical specialist pages are Chinese-only.

| Document | Contents |
| --- | --- |
| [v0.7.1 scoped graphs and tactile files](WORKSPACE_0.7.1.en.md) | Scopes, built-in AI tasks, budgets, format artwork, motion and references |
| [v0.6.2 Graphite interface](GRAPHITE_0.6.2.en.md) | Default Smiley Sans, graphite/frost, labelled sidebar, media and compact layouts |
| [v0.6.1 notes and knowledge graph](KNOWLEDGE_0.6.1.en.md) | Note links, backlinks, tags, outline, tasks, templates and local graphs |
| [v0.6.1 new documents](DOCUMENTS_0.6.1.md) | Markdown/DOCX/TXT, auto-save, source/preview, duplicates and conflict recovery |
| [v0.6.1 reading and photo clarity](WORKSPACE_CLARITY_0.6.1.md) | Quiet controls, edge/keyboard recovery, photo palette/type and compact editing |
| [v0.6.1 motion and windows](MOTION_0.6.1.en.md) | Scoped transitions, rapid reversals, reduced motion and native window fixes |
| [Reading flow and window motion](READING_FLOW_0.5.1.md) | Quiet reading, spreads, wheel paging, resize scheduling, opening and complete favorites |
| [Reading and window fixes](READING_RENEWAL_0.5.1.en.md) | Real contents, paged reading, eye comfort, saved comparison and compact/fullscreen viewers |
| [Translation, captions and precise marks](TRANSLATION_CAPTIONS_0.5.1.md) | Matching-format export, side-by-side reading, local/API captions, selection adjustments and motion |
| [Editing and music](EDITING_MUSIC_0.5.1.md) | Photo objects, media clips, visual text/PDF editing, tags and AI grouping |
| [Study tools](STUDY_TOOLS_0.5.1.md) | Multi-file comparison, related notes and subtitle chapters |
| [Bundled fonts](FONT_CANDIDATES.md) | Smiley Sans and LXGW WenKai Lite |
| [v0.5.1 design and AI](DESIGN_0.5.1.md) | Preview, references and focused checks |
| [v0.4.1 preview](PREVIEW_0.4.1.en.md) | Coherent colors, reading tools and video downloads |

[Historical verification and general troubleshooting](TESTING.en.md)

</details>

`screenshots/` uses original samples or isolated synthetic fixtures. `assets/` contains brand/feature images. `compatibility-results.json` records specific tested versions/runtimes, rather than promising every codec works.

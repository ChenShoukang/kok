# Agent interaction improvements

[中文](AGENT_INTERACTIONS.md) · [Back to Agent](AGENT_0.7.2.en.md)

## Implemented

| Control | Behavior |
| --- | --- |
| Discuss only / Collaborate | Discussion permits search and reading. Native code removes proposed write, create and open actions and reports that sources were unchanged. Collaboration retains individual approvals. Switching intent preserves conversation context within the same source scope |
| Source activity | Consecutive read-only tools form a collapsible group. Active groups default open and finished groups default closed; explicit expansion is retained. Replies, write proposals and created files remain visible |
| Sources read | Only IDs returned by local reading results become source buttons; model-supplied links are not trusted. Open the source or inspect the excerpt sent to the model. These identify evidence read during the task, not proof of every claim |
| Selective approval | Check individual proposals and run only the selection. Unselected proposals remain pending until individually run or skipped. Notes, tags and collections include before/after previews |
| Answer actions | Safe headings, bullets, bold and code formatting. HTML, scripts and remote images stay literal text. Copy preserves the original answer. Saving first asks for a name and Markdown / Word format, then creates a local document without another AI request |
| Recovery and scrolling | Refill a failed request for editing; no API call occurs before resending. Reading older messages does not force scrolling to incoming replies. Jump to latest and keyboard-focus preservation are supported |

Discussion limits model-proposed actions; the user may still explicitly save an answer. Saved files can be opened from the reply and use the existing guarded creation undo. Pure mode, private-folder restrictions, strict source scopes, encrypted sessions, cancellation and usage controls remain in place.

## References

- [Cline Plan & Act](https://docs.cline.bot/core-workflows/plan-and-act): distinguish discussion from action intent. kok keeps explicit modification approval.
- [Cline DiffEditRow](https://github.com/cline/cline/blob/main/apps/vscode/webview-ui/src/components/chat/DiffEditRow.tsx): contextual change previews and respectful local scrolling.
- [OpenHands group-events](https://github.com/OpenHands/OpenHands/blob/main/src/components/conversation-events/chat/group-events.ts): fold consecutive operations while retaining important replies and artifacts.
- assistant-ui [Tool group](https://www.assistant-ui.com/elements/tool-group), [Sources](https://www.assistant-ui.com/elements/sources), [Reviewable diff](https://www.assistant-ui.com/elements/reviewable-diff) and [Thread](https://www.assistant-ui.com/elements/thread): progressive disclosure, source controls, selective review and stable input/scroll behavior.

These informed interaction design and code structure. kok retains its C# / WebView2 implementation; no component code, icons or runtime dependencies were transplanted from these projects.

## Validation

`tests/agent-interactions.cjs` uses generated isolated files and a local mock provider to check grouping/expansion, real excerpts, HTML escaping, a clipboard test double, reviewed Word save/readback, selective approval, native discussion filtering, restored intent, no-request error editing, themes and 500–1280 px layouts, keyboard focus, scrolling and English. `tests/agent-reliable.cjs` covers cancellation, usage limits, sessions and privacy boundaries. No commercial model was called.

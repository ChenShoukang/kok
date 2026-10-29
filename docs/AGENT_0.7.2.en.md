# v0.7.2 · kok Agent

[中文](AGENT_0.7.2.md) · [Back](../README.en.md)

These Agent features were introduced in v0.7.2 and are included in v0.8.1 Preview. This page adds task mechanics and limits; start with the [current guide](GUIDE.en.md#where-ai-helps) for practical usage.

[Discussion, sources, change review and saving answers](AGENT_INTERACTIONS.en.md)

[Runtime/workflow boundaries](AGENT_BOUNDARIES.en.md) · [Motion and input](MOTION_INPUT.en.md)

## Continuity and scope

- Choose the entire library, selected sources only or one collection. File handoffs select the strict selected scope. Native code restricts catalogs, searches, reads and proposed file IDs. Changing scope clears model history from the previous scope.
- Conversations and drafts are encrypted for the current Windows account, with up to 12 sessions and the latest 80 interface entries per session. Switch, delete or continue a session. On reopening, unconfirmed proposals are suspended as skipped; they never replay automatically. Send a new request to obtain a fresh proposal. After a temporary network or service failure, Continue task resumes from the task record without replaying completed changes.
- Adjust a running task; the next round uses your adjustment and discards outdated proposals. Queue up to 5 follow-ups. A finished change can be undone while the same task still waits for other approvals. Stopping preserves remaining follow-ups as paused tasks. Reopening never runs them automatically; use Resume queue to continue.
- Notes have before/after previews. Applying or undoing note, tag and collection changes compares the current content first. Later manual edits are preserved and reported as conflicts. Undoing a created document also checks later content edits and always leaves the disk file intact.
- Pure mode, private-folder entry and closing cancel active work and ignore late replies. Pure mode permits offline saving of normal-library conversations without AI calls; the private folder does not load these conversations.

## Skills and usage

Four instruction presets cover organizing, comparing, reading notes and presentations. Select automatically, choose explicitly or disable skills; imported instruction skills remain available. Only the current skill's instructions are sent, and scripts are never executed.

Set a per-task request limit of 1–12. Usage settings accept input/output prices per million tokens and a task cost allowance, using the same currency. A zero allowance disables cost gating. Before a request, kok conservatively budgets the complete messages' UTF-8 bytes plus the output limit. A round that exceeds the allowance never starts. These local calculations use your prices and do not replace provider billing or cover other AI features.

Provider token statistics are shown when returned; otherwise usage is explicitly a conservative estimate. Complete tool observations receive bounded excerpts. Older conversation uses locally shortened task records plus recent messages, not a generated semantic summary, so no additional summarization API is called. Tool results from the latest two rounds keep their text; older ones keep a local excerpt of about 400 characters. Repeated identical reads within a task can reuse results; writes invalidate this cache. When the provider reports cached prompt tokens, the usage line shows Cache hits.

Replies stream progressively with distinct task stages and review cards. The composer and unchanged messages retain their DOM, focus, selection and scroll position. Chinese/English, compact windows, light/dark skins and reduced-motion settings follow the app preferences.

Design references: [Pi events and task steering](https://github.com/earendil-works/pi/blob/main/packages/agent/README.md), [sessions](https://github.com/earendil-works/pi/blob/main/packages/coding-agent/docs/sessions.md), and [on-demand skills](https://github.com/earendil-works/pi/blob/main/packages/coding-agent/docs/skills.md). Adapted to the existing C# / WebView2 stack with no new Pi or Node runtime dependency.

## Agent home

New users start on Today; choose Agent or the library as your start page in Settings. Pure mode and the private folder show no composer and send no AI requests.

1. Describe what you want; use Add files to set the scope. The library Agent button, the viewer's Hand to Agent and the graph's Continue in Agent bring the related files along.
2. Agent first uses read-only tools; their results appear as muted cards.
3. Changes arrive as confirmation cards naming the action, the files and the reason, with previews for notes, documents and slides. Nothing changes until you press Run; Skip changes nothing, and finished actions can be undone.

| Tools | Kind | Notes |
| --- | --- | --- |
| Search library, read files, list collections | Read-only, automatic | Only names, kinds, collections, tags and text / note excerpts; no local paths |
| Move to collection, add tags, write note | Confirmed | Undo restores the previous collection, tags or note |
| New document (Markdown / Word / TXT), new presentation | Confirmed | Creates a new file; undo removes it from the library while the file stays in the kok documents folder |
| Open file | Confirmed | Opens only |

Agent cannot delete, move or rewrite originals, search the web or run scripts. Imported SKILL.md instructions can be picked next to the composer; only the chosen one is sent.

## Protocol and usage

The native task engine owns the loop: model calls, read tools, approvals, undo and resuming are written to the task record encrypted for the current Windows account. The page only renders record snapshots and forwards Run, Skip, adjustments and Stop.

Agent uses provider function calling by default (OpenAI-compatible `tools` / `tool_calls`, streamed), covering presets such as DeepSeek, OpenAI, Qwen, SiliconFlow, OpenRouter, Ollama and LM Studio. If an endpoint answers the tools parameter with 400 / 422, the task switches once to the compatible JSON protocol (a reply, up to 4 actions and whether it is done) and notes this in the conversation. Both protocols share one tool registry; kok validates tool names, argument lengths and file IDs locally, and invalid output runs nothing.

An invalid call does not fail the task immediately: its error returns to the model as that call's tool result, with up to 2 repair rounds before the task stops. Skipped or failed changes are reported back as well, so the model can adjust.

Requests keep a fixed order, rules and the library catalog first and history and the latest results last, so providers can reuse their automatic prefix cache. Discussion-only tasks offer read tools only.

A task has at most 12 rounds, 6 by default. The task ends when no further reads are requested. Usage tiers match graph analysis: Economy, Balanced and More context cap tool results at about 6,000 / 14,000 / 24,000 characters. These limits reduce requests; they are not exact provider costs.

## Verification

- Native checks cover valid and invalid tools, invented IDs, too many actions, invalid and empty replies, escaping file names, Smart-mode gating and path-free catalogs, plus the tool registry, read-only tools for discussion, joining streamed tool-call fragments with their call IDs, repair messages for invalid calls and the fallback signal when an endpoint rejects tools.
- The isolated interface tests (`tests/agent.cjs`, `tests/agent-reliable.cjs`, `tests/agent-interactions.cjs`) use a local mock model (`tests/agent-fixture.cjs`, speaking both native function calling and the JSON protocol): starters send nothing, native tool calls with a stable prefix, reads run automatically, nothing changes before confirmation, undo while waiting, skip, stopping after two repair rounds, JSON fallback, Pure mode, no paths sent, no external network and unchanged originals.

Answer quality with real models varies by provider and still needs more real-world testing.

# v0.8.2 · Reviewable Agent tasks and MCP

[中文](AGENT_0.8.2.md) · [Guide](GUIDE.en.md) · [Validation](VALIDATION_0.8.2.md)

**Source development version, not released.** The published download remains v0.8.1 Preview. This iteration adds plans, outcome review, request diagnostics and local MCP tools to the existing library, operations and activity center, using C# / WebView2 without a large Agent framework.

Follow-up development adds [context checkpoints, encrypted memory, read-only subagents and JavaScript/WASM computation](AGENT_RUNTIME_0.8.2.en.md), with per-task controls and approval.

## Task interaction

Select sources and describe a goal. A short plan can accompany multi-step work. Local searches and reads run automatically; changes retain their previews and approval buttons. MCP calls always require approval. Task updates preserve disclosures, focus and drafts.

The model must call `finish_task` with a completed, partial or needs-input outcome and actual tool receipt references. Running out of requests without review is a limit, not successful completion. Changing instructions invalidates the previous review.

The host checks that referenced calls exist, succeeded and remain current against local edits and undo. **It does not independently prove semantic claims.** The UI distinguishes checked tool evidence from model-only review. Unresolved failed, skipped or undone steps cannot establish full success. Reasoning-only plan steps can finish as model assessment without unrelated tool calls; they do not replace checks on actual tool outcomes. Plans do not automatically fork sessions or create subagents; encrypted local events and bounded model context remain separate.

Undo after completion clears verification and changes the task to review-required. Reopening a task also checks for changed local evidence. New instructions visibly invalidate the old plan instead of retaining completed checkmarks. A needs-input task with budget remaining accepts a reply within the same task, keeping its original scope and limits. Selected files retain selection order. Remaining-request hints and combined plan/review guidance reduce extra turns, but do not guarantee that a model finishes within its limit.

## Failure handling

| Failure | Behavior |
| --- | --- |
| Connection, timeout, HTTP 408 / 425 / 429 / 5xx | At most two extra model-request attempts with bounded backoff and jitter; every attempt consumes the task request / cost budget |
| `Retry-After` over 30 seconds | Stop for later manual recovery rather than ignoring the limit or waiting indefinitely |
| Authentication, quota, context, ordinary 400 / 422 errors | Classify and report without blindly retrying |
| Explicit rejection of tools / tool_choice / functions | Switch once to JSON in automatic protocol mode; an arbitrary parameter error never triggers fallback |
| Transient local read-tool failure | Persist the failure for explicit resume; not every module has automatic retries |
| Invalid MCP tool name or arguments | Execute none of that turn; use at most two repair rounds within the original request budget; corrected calls still require approval |
| MCP `isError` | Retain and return the error body as untrusted tool evidence, available locally; never count it as success or retry it automatically |
| Uncertain external outcome or interrupted write | Never replay automatically; inspect the external state before handing off or starting again |

Missing provider usage uses a conservative upper-bound estimate, not a billing claim. Exhausted tasks do not advertise a resume that would instantly exceed the same budget.

## MCP setup

Open **External tools · MCP** on the empty Agent page or under **Settings → Tools**.

1. Install a trusted MCP server and its runtime yourself.
2. Add its local `.exe` path, one argument per line, and `KEY=value` environment entries. For a Node service, select the installed `node.exe` and provide the local JavaScript entry point as an argument. kok does not install packages or run `npx` automatically.
3. Inspect each input schema, select allowed tools and save permissions.
4. Review the exact server ID, tool and arguments in each proposed call. Only approval sends `tools/call`. External calls are excluded from bulk execution; both the UI and native boundary require separate approval for each.

Servers run with **your Windows account permissions** and may access files and networks outside the selected library scope. This is not an OS sandbox. Read-only hints do not bypass approval. External effects have no automatic undo.

- Configuration, including environment values, is encrypted using current-user DPAPI; values are not echoed in UI / model catalogs. A server itself can read its granted environment and could return sensitive data.
- Only necessary OS variables and explicitly configured values reach the child; arbitrary kok API environment variables are not inherited.
- Servers never auto-start after restart. Approval fingerprints include the executable hash, arguments, environment configuration and tool definition. Tool definitions are checked again before execution. **Interpreter script contents and dependencies are not hashed yet**; changes to them may not trigger new approval, so you must trust and manage the installed service.
- Pure mode, private entry, relevant module disable and exit close connections. A Windows Job Object controls server / child-worker lifetime; it does not restrict their permissions.
- Limits: 12 servers, 64 tools per server, bounded schemas / arguments / output. Tool text is excerpted. Images, linked resources and server-originated requests are not automatically loaded or authorized.

This is a **stdio tools client subset**, supporting 2026-07-28 discovery / per-request metadata and legacy initialization for 2025-11-25, 2025-06-18, 2025-03-26 and 2024-11-05. Basic bounded JSON Schema types, objects, arrays, required fields, enums and ranges are supported. Unsupported assertions such as `$ref` and schema combinators disable the tool. Full JSON Schema 2020-12 / MCP compliance is not claimed.

Streamable HTTP, OAuth, resources / prompts, sampling, interactive input and subscriptions are outside this version. Connecting MCP does not grant default permission to install plugins or execute arbitrary shell commands.

## Diagnostics and evaluation

Encrypted local task events retain content needed for recovery. Diagnostic exports are separate: timestamps, states, random IDs, request metrics and plan counts / states, with no goals, filenames, prompts, source text, tool arguments, provider error bodies, secrets or model review prose.

Tool cards and trace events can open local results on demand, including external error text; this content stays out of diagnostic exports. The cost column shows an estimate only when input/output rates are configured; otherwise it shows `—`, not a zero-cost claim or invoice.

Default tests use loopback model fixtures and real local stdio children, with no API spending. Six basic opt-in cases cover grounded facts, approved writes, rejection, injection, comparison and missing information, with at most four requests each. Two further cases cover isolated computation (limit 6) and read-only delegation (limit 8). Live evaluation requires explicit opt-in and a credential file; unrun cases are never reported as passed. See [historical validation](VALIDATION_0.8.2.md) and [runtime validation](VALIDATION_RUNTIME_0.8.2.md).

## Open-source references

| Reference | Applied principle |
| --- | --- |
| [Pi agent](https://github.com/earendil-works/pi/blob/main/packages/agent/README.md) | Separate persisted state from model messages; host the execution loop outside UI rendering |
| [LangGraph fault tolerance](https://github.com/langchain-ai/docs/blob/main/src/oss/langgraph/fault-tolerance.mdx) | Typed finite retries, cancellation and no replay of uncertain writes |
| [OpenHands SDK](https://github.com/OpenHands/software-agent-sdk/blob/main/openhands-sdk/openhands/sdk/agent/base.py) | Explicit task tracking / tool configuration and observable execution |
| [MCP stdio](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/stdio) · [versioning](https://modelcontextprotocol.io/specification/2026-07-28/basic/versioning) | Newline JSON-RPC, modern versus legacy negotiation, and explicit tool trust |

These are architectural references; their runtimes are not bundled. Budgeted semantic checkpoints, encrypted memory, read-only delegation and JavaScript/WASM isolation are now implemented. Context sizing remains a conservative upper bound, not an exact tokenizer. Remote authentication, MCP script hashing, general OS containers, concurrent writing subagents and broader model benchmarks remain future work. This is progress toward a modern Agent workspace, **not yet an autonomous computer operating system**.

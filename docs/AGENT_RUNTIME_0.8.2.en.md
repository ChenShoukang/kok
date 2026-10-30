# Agent runtime: context, memory, subagents and sandbox computation

[中文](AGENT_RUNTIME_0.8.2.md) · [Agent guide](AGENT_0.8.2.en.md) · [Validation](VALIDATION_RUNTIME_0.8.2.md)

These are follow-up local development changes for v0.8.2. Use **Capabilities and permissions** to opt into memory, delegation and computation per task. Changing switches makes no model request.

## Context checkpoints

Semantic compaction preserves the goal, user adjustments, current plan and two recent turns. A model summarizes older complete turns without splitting tool-call/result pairs. Original encrypted events remain available. `recall_result` retrieves exact stored content by call ID without repeating the operation.

Summary requests consume the original request/cost budget and are traced separately. Compaction skips when insufficient budget remains for a response. Changed instructions, receipts or sources invalidate the checkpoint. Summaries are untrusted data and never authorize actions.

The context meter uses UTF-8 bytes as a conservative token upper bound, not a model-specific tokenizer; fixed tool definitions are outside this source-context meter. Provider usage remains authoritative. Summaries can omit details; retrieve exact evidence when needed.

## User-managed memory

Create, edit, delete and undo memories in the local panel. Link records to selected sources or explicitly create personal memories. A task must enable memory before relevant records are locally searched and supplied to its model. Retrieval is lexical, without an embedding API or vector database.

Agent memory proposals require approval. Changed or out-of-scope sources are excluded until reviewed again. Memory is untrusted evidence, not authority over tools. Limits are 128 records, 2,000 characters each and 80 retained changes. Current-user Windows DPAPI encrypts records and history separately from the library JSON. Revision checks protect later edits. Memory is disabled in the private folder; key-shaped text is rejected, but this is not a comprehensive secret detector.

## Read-only delegation

Approve a concrete goal, source subset and request cap. A child receives this explicit context, without the parent's full history or skills. It can search/read/recall results, plan and review; it cannot write, use MCP, execute code, access memory or delegate recursively.

Child requests, retries and summaries consume the parent's original budget. Reserve a parent response slot, with one level and at most four delegations per parent. Stop, mode changes and new parent instructions cancel old children. Reports, events and usage remain inspectable; incomplete children do not establish successful evidence. Restart does not automatically replay delegation. Parent recovery requires reconfirmation and does not reset child budgets.

## Isolated JavaScript and artifacts

`run_code` displays exact code and JSON input for individual approval. A fresh QuickJS WebAssembly runtime runs an expression or IIFE with standard JavaScript and `INPUT`. With input `{"budget":1800,"spent":1200}`, `INPUT.budget-INPUT.spent` returns `600`.

The interpreter exposes no host files, network, DOM, environment, `process`, `require` or shell. Limits: 16 MiB heap, 256 KiB stack, approximately 1.2 seconds of execution and 16,000 output characters, plus a separate worker timeout. Save reviewed results as Markdown, TXT or Word.

This is JavaScript/WASM language-runtime isolation, not arbitrary Python/Bash, Docker or VM execution. Configured MCP processes retain their existing account permissions outside this boundary. Node.js is not needed to run kok.

## Supporting capabilities and references

Per-task tool restrictions are enforced natively. Planning/review controls remain available. Delegation, computation and MCP calls are excluded from bulk approvals. Shared budgets, request-purpose traces, persisted events, exact result recall, memory revisions and saved artifacts form the execution path. Existing skills, module tools, human review, conflict-safe undo, task-local input, Recipes and Activity remain available. Semantic goal correctness is not independently proven.

Architectural references: [Pi compaction](https://github.com/earendil-works/pi/blob/main/packages/coding-agent/src/core/compaction/compaction.ts), [Deep Agents delegation](https://github.com/langchain-ai/deepagents/blob/main/libs/deepagents/deepagents/middleware/subagents.py), and [Letta progressive memory](https://github.com/letta-ai/letta-code/blob/main/src/tools/descriptions/Memory.md). Their runtimes are not bundled. [QuickJS Emscripten](https://github.com/justjake/quickjs-emscripten) supplies the pinned release-sync WASM interpreter and its time/memory controls; its MIT notices are retained. Fixture verification and live-model quality are reported separately.

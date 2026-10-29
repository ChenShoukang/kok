# Agent runtime and workflow boundaries

[中文](AGENT_BOUNDARIES.md) · [Back to Agent](AGENT_0.7.2.en.md)

kok does not automatically fork or merge sessions and does not expose a bash tool.

| Layer | Responsibility |
| --- | --- |
| Runtime | Model transport, cancellation, tool argument/scope validation, approval, returning results, and local log/session storage mechanisms |
| Workflow | Task decomposition, whether to hand off work, selecting evidence and tool results, writing handoff records, and deciding whether to merge results |
| Interface | Showing stages, evidence and changes; letting the user review, correct and stop work |

Forking, event persistence and reading branches can be runtime capabilities. Automatically copying the entire session for every task and merging it afterward should not be a universal default policy. Restoring a session must not automatically replay unfinished writes.

## Logs and model context are different

Complete local tool events may help recovery and debugging. Model context should be selected for the task. A workflow handoff should reference existing records and carry the goal, verified findings with sources, completed operations, remaining work and next steps, rather than duplicate all outputs or repeatedly summarize them. A handoff is not permission to modify originals; merging still needs conflict checks.

## Current implementation

v0.8.1 runs the execution loop in the native engine and stores complete tool events with DPAPI encryption. Recovery and selected-result handoff are explicit. Renderer reload attaches to the existing task; process restart requires manual continuation, and writes are not blindly replayed. See [architecture and remaining work](ARCHITECTURE_0.8.1.en.md).

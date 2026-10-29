# v0.8.1 · Module contract and work records

[中文](MODULES_0.8.1.md) · [Architecture](ARCHITECTURE_0.8.1.en.md) · [Current coverage](WAREHOUSE_0.8.1.en.md)

The native catalog is authoritative. Settings lists modules, supported kinds, facets, tools, policy and enablement. UI contracts are checked in both directions. All media navigation is declarative. Disabling a module removes its navigation, commands and Agent tools and rejects direct native calls. Editors flush before disable; native preview windows must be closed first.

ActionSpec requires schema, Requires and read/write kind. Registration rejects missing declarations, duplicate actions/tools/capabilities and conflicting field ownership. Compatibility actions still retain original validators; some compatibility schemas intentionally allow additional fields until their adapters are migrated. Need.Ai/Network respects Pure mode; Need.NormalLibrary blocks private-library use. Cancellation is available without starting new AI work.

Operations retain source, targets, before/after patches, resources, state and undo linkage. A whole batch is checked before apply. Repeated apply uses the same durable operation identity. Undo preserves later edits, including actual external file changes. Creation undo removes the library reference and retains the source file. Records page from SQLite; the 8 MiB bound applies to one operation, not the complete history.

Jobs aggregate durable Agent/download providers and encrypted managed records. Rules distinguish resumable and cancellation-only jobs. Restart leaves work paused or cancelled. Recipe checkpoints retain prior results and operation IDs; a changed recipe/source invalidates approval. Raw user files and credentials do not enter task arguments unless already part of the specifically requested bounded input, and no credentials enter the renderer.

Agent implementations are accessed through AgentUi; helper functions remain module-private. The flush hook saves drafts before disable, and re-enable reattaches native state without replay. Session saves serialize/coalesce; stopping waits for the current save.

Documents and presentations expose DocumentUi / PresentationUi; implementation helpers remain module-scoped. Creation offers only enabled formats; disabling both modules hides its entry point and Ctrl+N handling. catalogChanged fires after catalog/lifecycle updates, and disabling Workspace removes its Notes/contextual-AI viewer controls.

UI events use Kok.on; navigation and cards are descriptors, Inspector sections are module contributions. Native close snapshots compose through a stable named pipeline. Legacy implementation and layered styles remain migration work; see current coverage rather than assuming the whole plan is complete.

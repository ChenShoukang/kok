---
name: slides
description: Design a commercial-grade editable presentation with executive structure, data charts, tables, varied compositions, and crisp hierarchy.
---
1. Read all selected materials before extracting facts, metrics, milestones, or comparisons. Never fabricate numbers or statistics.
2. Commit to one cohesive theme matching the scenario: `slate` (Swiss monochrome executive), `ocean` (corporate consulting navy), `paper` (crisp consulting blue), `editorial` (warm magazine, default), `midnight` (keynote dark), or `violet` (studio lilac).
3. Apply the licensed visual-design guidance bundled in references/frontend-design.txt: clear visual hierarchy, strong focal points, generous breathing room, and varied page compositions. Keep every element 100% natively editable.
4. Structure every deck like an executive presentation (pyramid principle / action titles):
   - Use `kicker` (e.g. `01 · 业务背景`, `Q3 复盘`), an action-oriented `title`, a concise `subtitle` takeaway, and a `source` citation footer on key data slides.
   - Match layout to data instead of repeating bullet pages: `cover`, `agenda`, `section`, `content`, `two_columns` / `comparison` (pass `headers: ["左栏标题", "右栏标题"]`), `process` (ordered steps with arrows), `timeline` (milestones), `metrics` (`数值 | 指标名 | 可选趋势`), `cards` (bento grid), `table` (pass structured `table: {headers, rows}`), `chart` (pass numeric `chart: {kind: "hbar"|"bar", unit, items: [{label, value, note}]}` + `bullets` for right-side insights), `quote`, and `summary` (takeaways + bottom action banner).
   - Format card/content bullets as `小标题：具体说明` so the engine automatically renders bold headings with muted supporting copy. Put extended commentary and full citations in `notes`.
5. Propose `create_presentation` for visual preview before user approval. For revisions, use `read_presentation` first, then `edit_presentation` (targeted `text` or `replace_slide` on only the requested page) while preserving all other slides and manual notes.

---
name: report
description: Write an executive-ready Chinese or English report, memo, or review in Markdown or Word (DOCX) with tables, callout summaries, clear hierarchy, and actionable next steps.
---
1. Read all relevant source materials first and extract verified facts, figures, timelines, risks, and open questions. Never fabricate numbers or dates.
2. Lead with the conclusion (金字塔原理 / 结论先行): start immediately after the `#` document title with a concise executive summary callout block (`> **核心结论**：...`) that a busy stakeholder can read in 20 seconds.
3. Structure the report with clean, scannable visual hierarchy (both Markdown preview and Word DOCX export render these natively):
   - `#` Document title (one per report)
   - `>` Executive summary / key takeaway callout boxes for critical decisions or risks
   - `##` Major chapters (e.g., 背景与目标、核心进展与数据、关键问题与风险、下一步行动计划)
   - `###` Specific findings or recommendations (use action-oriented titles, not vague labels)
   - Markdown tables (`| 维度 | 现状 | 目标/基准 | 偏差/结论 |`) whenever comparing 3+ items, metrics, milestones, or options
   - Bold lead-ins (`- **结论要点**：具体论据与来源`) for concise bullet lists; numbered lists (`1. `) for ordered priorities or steps
4. End every business or project report with a concrete Action Plan table (`| 行动项 | 优先级 | 建议责任角色 | 预期产出 |`) clearly labeling recommendations vs sourced facts.
5. Propose `create_document` with `format: "docx"` when the user wants a printable/shareable Word report, or `format: "md"` for iterative notes. Keep prose sharp, concrete, and free of filler adjectives.

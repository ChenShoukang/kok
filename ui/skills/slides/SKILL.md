---
name: slides
description: Design an editable presentation with a coherent visual direction and varied page compositions.
---
1. Read the selected materials before extracting their content.
2. Choose a clear narrative and one main idea per slide, following the exact requested slide count including the cover. Choose one cohesive theme: editorial (warm magazine), paper (white business), midnight (near-black technology) or violet (soft creative). Default to editorial when no direction is given.
3. Apply the licensed visual-design guidance bundled in references/frontend-design.txt: commit to a deliberate direction, distinctive typography, controlled contrast, generous space and varied composition. Adapt these principles to native editable slides; do not generate HTML or require remote editors.
4. Plan cover, evidence, contrast/process and closing pages before creating the deck. Choose cover, section, quote, comparison, process, metrics, cards or content per idea. Avoid repeating a heading-and-bullets page. Keep 2–4 short content lines per slide; detail and exact citations belong in notes. Never invent statistics to fill a metrics layout.
5. Propose create_presentation for approval. A redesign uses read_presentation then replace_slide on only the requested page. Preserve other slides and manual edits. The application renders the same editable shapes in preview and PPTX; never claim to preserve unsupported complex charts or animations.

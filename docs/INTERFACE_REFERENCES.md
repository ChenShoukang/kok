# Interface references and review method

This refresh uses specific interaction patterns rather than reproducing another
application's appearance. All UI remains local, using kok's existing native bridge.

| Reference | Pattern adopted in kok |
| --- | --- |
| [Lucide specification](https://lucide.dev/contribute/icons/specification) and [source](https://github.com/lucide-icons/lucide/tree/0.468.0/icons) | Consistent 24-unit outline icons with round caps and joins. A pinned offline subset replaces individually drawn paths and object illustrations. Original geometry is preserved, including circles in the ellipsis icon. |
| [Radix Dropdown Menu](https://www.radix-ui.com/primitives/docs/components/dropdown-menu) | Menus anchored to their triggers, arrow/Home/End navigation, Escape dismissal, focus restoration and a single active popover. The existing plain-JavaScript menu is retained rather than introducing a React dependency. |
| [Zotero quick start](https://www.zotero.org/support/quick_start_guide) | Separate navigation, the file collection and details. Global search/creation are distinct from filters and sorting. The existing inspector provides file details. |
| [Immich thumbnail implementation](https://github.com/immich-app/immich/blob/main/web/src/lib/components/assets/thumbnail/Thumbnail.svelte) | Quiet thumbnails, contextual selection/actions and keyboard focus movement. Real content is preserved while decorative fallback artwork uses neutral icons. |

Lucide is pinned to 0.468.0 and distributed under its ISC license. The source
URLs and hashes are recorded in `ui/icons/lucide/manifest.json`; the license is
in `docs/licenses/Lucide-ISC.txt`. No application code was copied from Zotero,
Immich or Radix. These are design references, not runtime dependencies.

## Behavior and layout decisions

- Frequent destinations remain visible; secondary tools are in a disclosure that
  remembers its state during navigation. Current destinations expose `aria-current`.
- A single library toolbar owns file-type filters, sorting, view switching and
  additional tools. Comparison and Agent entry points no longer compete beside
  the page title.
- File menus open next to the file, with common actions first and additional
  capabilities one level deeper. Commands still use their existing module gates.
- Arrow keys move between files; Space toggles selection; Shift + arrows extends
  it; Ctrl + A selects displayed files; Escape clears selection. Shift + F10 opens
  the focused file's menu. Enter keeps the existing open/play behavior.
- Ctrl + F focuses library search; Ctrl + K keeps the existing command palette.
  The visible search shortcut now matches the action.
- Interaction transitions use 120–180 ms. Menu dismissal is immediate, preventing
  a stale closing menu from occupying the top layer. Text does not scale during
  navigation, and reduced motion disables entrance effects.
- Equal-sized previews preserve global sort order. Format labels, hover prompts
  and per-card commands are not repeated unnecessarily. Audio duration metadata
  belongs inside its preview or list row.
- Empty collection screens explain their purpose and provide a relevant next step.

## Review and verification

`tests/interface-usage.cjs` covers continuous keyboard and pointer workflows,
trigger focus recovery, selection, filters, search, contextual tools, menu bounds,
rapid interruption and reduced motion. `tests/interface-audit.cjs` captures every
main route and sample-file viewer at multiple widths and in both themes. It also
captures settings, module management, private-folder entry, commands and mode
selection, including long file names and English layouts.

Screenshots are reviewed visually as well as checked for viewport overflow.
`tools/interface-contact-sheets.cjs` creates local contact sheets for that review.
The gallery and geometry report are saved under `test-output/interface-audit`.
Broader existing desktop suites check reading, annotations, media playback,
translation with local mocks, import/drag, file editing, private scopes, Agent,
module switches, spaces and library services. Real paid model requests are outside
this interface verification.

The first 99-screen pass exposed a narrow PDF toolbar that placed actions beyond
the viewport. Visual review additionally found compressed Agent empty-state
content, residual record artwork in the music library, misaligned duration badges
and unhelpful empty collection pages. These findings are handled in the interface
and followed by another capture pass; geometry success alone is not visual approval.

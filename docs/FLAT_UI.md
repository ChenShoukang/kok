# Flat interface refresh

The application uses one flat presentation in light and dark mode. Saved `classic`
and `graphite` skin preferences remain readable but render the same flat interface.
The retired classic stylesheet is no longer loaded or shipped from source.

Design references: [AFFiNE](https://github.com/toeverything/AFFiNE) for workspace
organization, and [Joplin's accessibility regions](https://joplinapp.org/help/apps/screen_reader_accessibility/)
for clear separation of navigation, collections and content. The implementation
uses kok's own components, Phosphor duotone destination glyphs and Lucide action
icons; no external application component code was copied.

- Solid surfaces, subtle borders and a restrained accent replace textures, bevels,
  engraved text, inset wells and lifted cards. Floating dialogs retain a modest
  shadow to distinguish their layer.
- A short activity rail holds Library, Today, Spaces and Agent at every window
  width, with one launcher for secondary destinations. File types belong to the
  library toolbar. The existing module permissions and handlers still apply.
- Mixed file types use equal-size cards in one grid. The displayed order now
  follows the selected sort across all types. List view remains available.
- Fallback artwork uses consistent duotone glyphs with muted color fields for
  recognition. Launcher glyphs share the same geometry without extra icon tiles.
  Real photos, video thumbnails and album covers remain content.
- New profiles default to the system UI font. Existing explicit font choices and
  reader typography preferences are preserved.
- Keyboard focus has a visible outline; view buttons expose their pressed state;
  card menus remain visible and keyboard accessible. Reduced motion is respected.

The rail is 72px wide, or 64px in compact windows. The three-column launcher
uses short names and icons; collections open a separate list, so adding categories
does not lengthen the rail. Arrow keys navigate the launcher; Escape returns focus
to its trigger. Background library refreshes retain the open launcher and resolve
its commands against the live navigation entries. Favorites, Recent and Collections
occupy the first row; secondary destinations have distinct icon meanings.

The welcome screen has two wordless interface previews and their mode names.
The Smart preview adds an assistant beside the same library. Promotional copy,
feature tags, duplicated enter labels and footer explanations are removed.
Accessible descriptions and the first Smart-mode data-sharing notice remain.

The initial palette reference is [csk66.ink](https://csk66.ink/), adjusted to the
requested near-white daylight (`#faf9f7`) and purple charcoal (`#121017`) at night.
Navigation, top bar and content share a background; borders and spacing provide
structure. [TANCO](https://tancoai.com/)'s colored geometric fields inform muted
teal, rose, blue, violet and amber artwork and icon accents. Secondary text retains
enough contrast for desktop controls.
See [interface references](INTERFACE_REFERENCES.md#short-navigation-at-every-width).

Validation: `npm run check`, `build.ps1 -Test` (542 native checks), the full 25-suite UI/regression matrix and the 14-suite package gate. See [interface validation](INTERFACE_VALIDATION.md) and [v0.9.5 validation](VALIDATION_0.9.5.md).
The desktop suite uses an isolated profile and checks saved skin compatibility,
uniform cards, sorting, view switches, keyboard menus, favorites, document opening,
light/dark layouts at 1440, 960 and 650 pixels, English, Pure-mode graph access,
Agent and reduced motion. Screenshots are saved to `test-output/flat-design`.

The UI can be tested with the existing native binary because the native bridge is
unchanged. During review, `build.ps1 -Test` completed successfully (542 native
checks) and the package/desktop validation was rerun; see
[INTERFACE_VALIDATION.md](INTERFACE_VALIDATION.md).

The oil-ui refinement adds interruptible motion: 200ms menu entrances and 120ms
exits, immediate logical dismissal, a shared sliding selection plate, persistent
filter buttons/focus and batched movement of visible cards after sorting.
Interrupted animations sample the displayed opacity and transform; reduced motion
bypasses transitions. The pinned Phosphor sources, hashes and MIT license ship
offline with the application.

This refinement passed `npm run check` and the `motion-quality`, `flat-design`,
`interface-usage`, `features` and `app-modes` desktop suites. The five-suite record
is `test-output/full-20261101-151138-cdb89100/run.json`. A subsequent keyframe check
found differing launcher exit padding and corner radius; the styles now retain
the entrance geometry. The added regression and `interface-usage` both passed
against the final tree in `test-output/full-20261101-152559-b225a033/run.json`.
Independent visual and
real-operation reviews are under `test-output/oil-ui-refinement`; their findings
led to clearer destination glyphs, brighter mode differences and earlier access
to Favorites, Recent and Collections. Frame samples describe one isolated WebView2
session and do not guarantee a frame rate on other hardware.

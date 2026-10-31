# Flat interface refresh

The application uses one flat presentation in light and dark mode. Saved `classic`
and `graphite` skin preferences remain readable but render the same flat interface.
The retired classic stylesheet is no longer loaded or shipped from source.

Design references: [AFFiNE](https://github.com/toeverything/AFFiNE) for workspace
organization, and [Joplin's accessibility regions](https://joplinapp.org/help/apps/screen_reader_accessibility/)
for clear separation of navigation, collections and content. The implementation
uses kok's own components and outline SVGs; no external component code was copied.

- Solid surfaces, subtle borders and a restrained accent replace textures, bevels,
  engraved text, inset wells and lifted cards. Floating dialogs retain a modest
  shadow to distinguish their layer.
- Navigation contributions keep their existing permissions, ordering within each
  group and handlers. Group labels separate workspace, library, organization,
  tools and quick access. Current destinations expose `aria-current`.
- Mixed file types use equal-size cards in one grid. The displayed order now
  follows the selected sort across all types. List view remains available.
- Fallback artwork, format stamps, the private folder and mode illustrations use
  monochrome outlines. Real photos, video thumbnails and album covers remain content.
- New profiles default to the system UI font. Existing explicit font choices and
  reader typography preferences are preserved.
- Keyboard focus has a visible outline; view buttons expose their pressed state;
  card menus remain visible and keyboard accessible. Reduced motion is respected.

Validation: `npm run check`, `build.ps1 -Test` (542 native checks), the full 25-suite UI/regression matrix and the 14-suite package gate. See [interface validation](INTERFACE_VALIDATION.md) and [v0.9.4 validation](VALIDATION_0.9.4.md).
The desktop suite uses an isolated profile and checks saved skin compatibility,
uniform cards, sorting, view switches, keyboard menus, favorites, document opening,
light/dark layouts at 1440, 960 and 650 pixels, English, Pure-mode graph access,
Agent and reduced motion. Screenshots are saved to `test-output/flat-design`.

The UI can be tested with the existing native binary because the native bridge is
unchanged. During review, `build.ps1 -Test` completed successfully (542 native
checks) and the package/desktop validation was rerun; see
[INTERFACE_VALIDATION.md](INTERFACE_VALIDATION.md).

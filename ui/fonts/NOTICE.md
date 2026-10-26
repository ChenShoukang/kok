# Smiley Sans v2.0.1

Unmodified official `SmileySans-Oblique.ttf.woff2`, stored as `SmileySans-Oblique.woff2`.

- Project: https://github.com/atelier-anchor/smiley-sans
- Release: https://github.com/atelier-anchor/smiley-sans/releases/tag/v2.0.1
- Copyright (c) 2022–2024 atelierAnchor. Reserved Font Names: Smiley and 得意黑.
- License: SIL Open Font License 1.1, bundled at `docs/licenses/SmileySans-OFL.txt`.
- WOFF2 SHA-256: `731f22973349404b15a88a99ef3b5dd4104c0965c23b7e485c1f11e84fea99e2`
- Official ZIP SHA-256: `299c0be6c960ae37361762eca76f7d0cd516615435bb96c0d4b98a1e70178a07`

The font is bundled locally. Running kok does not download it or install it into Windows.

# LXGW WenKai Lite v1.530 · 霞鹜文楷轻便版

Source: original `fonts/TTF/LXGWWenKaiLite-Regular.ttf` from lxgw/LxgwWenKai-Lite.
Runtime: `LXGWWenKaiLite-Regular.woff2`, losslessly compressed with the full character set.

- Pinned commit: `d5a8a1f67872f398ef364a525a77261b3d9ae281`.
- Source: https://github.com/lxgw/LxgwWenKai-Lite/blob/d5a8a1f67872f398ef364a525a77261b3d9ae281/fonts/TTF/LXGWWenKaiLite-Regular.ttf
- Git blob: `b6a91870361ff0ddf5bb12d8327fda1948794074`.
- Size: 13,949,472 bytes.
- SHA-256: `f799a538a3355877e597a0ae04050e897932c9ae7689eb9227c2953c5dd549ea`.
- Copyright and reserved names: see the unmodified upstream license at `docs/licenses/LXGWWenKaiLite-OFL.txt`.
- License: SIL Open Font License 1.1.

Both families load from local assets on demand, without system installation or a
remote font service. Smiley Sans is the original upstream WOFF2. LXGW WenKai Lite
uses WOFF2 null transforms and Node's Brotli compression; no glyphs, outlines, hinting,
shaping tables or name/license metadata have been removed or changed. WOFF2-required
container changes remove the DSIG signature table and set head.flags bit 11. All
remaining table bytes are checked against the originals with only that flag allowed
to differ. No WOFF-specific metadata is added. Output is padded to four bytes for
compatibility with the browser's WOFF2 decoder.

`ui/fonts/manifest.json` records source and runtime hashes. The compression evidence
is in `compression-verification.json`. Regenerate using the pinned original TTFs:

    node tools/compress-fonts.cjs --source-dir <original-ttf-directory>

Original TTFs are not duplicated in the runtime package. Source URLs remain above.
Specification: https://www.w3.org/TR/WOFF2/
OFL webfont guidance: https://openfontlicense.org/webfonts-and-reserved-font-names/

# Third-party notices

kok uses Microsoft.Web.WebView2 SDK version 1.0.4258.31 from the official
NuGet package. Release bundles redistribute:

- Microsoft.Web.WebView2.Core.dll
- Microsoft.Web.WebView2.WinForms.dll
- WebView2Loader.dll (win-x64)

These files are governed by the Microsoft WebView2 SDK license, **not** kok's
MIT license. The original package license is included in binary bundles as
`WEBVIEW2-LICENSE.txt`, and its NOTICE file as `WEBVIEW2-NOTICE.txt`.

Package: https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31
Documentation: https://learn.microsoft.com/microsoft-edge/webview2/

The WebView2 browser runtime is installed and updated separately by Microsoft.
It is not redistributed in kok's archive.

Playwright Core is used only for development tests under Apache-2.0. It is not
included in the desktop distribution. See the installed npm package for its
license and notices.

PDF.js version 6.4.299 by Mozilla Foundation and contributors is bundled as
local browser assets under `ui/vendor/pdfjs/`, including the rendering module,
worker, CMaps, standard fonts, ICC profiles and WASM decoders. It is licensed
under Apache-2.0, with its original LICENSE included in that directory.
The `.textLayer` CSS rules in `ui/features.css` are adapted from PDF.js viewer
styles; the rest of the interface styles are original to kok. Individual
font or decoder notices are retained with their asset directories.

Package: https://www.npmjs.com/package/pdfjs-dist/v/6.4.299
Source: https://github.com/mozilla/pdf.js

No source code from Tauri, Foliate or mpv has been copied or bundled.
The original interface, geometric artwork, documentation,
sample text, generated landscapes, synthesized audio and sample video are
original to this project.

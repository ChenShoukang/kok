# Release verification

[中文](TESTING.md) · English · [Documentation](README.en.md)

**The published version is v0.8.1 Preview. Development [v0.8.2 validation](VALIDATION_0.8.2.md) is recorded separately; older counts do not apply. [v0.8.1 validation](VALIDATION_0.8.1.md#english) is the single source for current results, reproduction commands and limits.** This page keeps historical evidence and general checks; older counts below are not current-version results.

Passing checks confirm the paths covered by those checks, rather than every Windows machine, codec or AI service.

## v0.6.2 release checks

2026-10-27: 127 native checks, six isolated silent desktop suites, PowerShell 5, dependency and privacy audits. Synthetic fixtures; no real AI configuration.

## Historical evidence (not current results)

| Area | Evidence and limits |
| --- | --- |
| Local desktop | 16 isolated, silent desktop suites have covered media, reading, photos, modes, private storage, mock AI and renderer recovery; mainly one Windows / WebView2 environment |
| Native features | 88 self-checks cover persistence, subtitles, byte ranges, authenticated encryption, credential isolation and other behavior; run during builds and packaging |
| Historical privacy review | On 2026-10-24, checked 6 Release ZIPs, 10 Actions logs and 10 build artifacts. All 6 Release ZIP hashes matched their accompanying SHA-256 files; no matching credential or private-file patterns were found |
| Dependency advisories | The official npm endpoint returned no advisories for the 14 package names in the lockfile. The build workflow also runs complete npm audit checks; absence of advisories does not prove absence of vulnerabilities |
| Actual codecs | Results record the tested files, version and runtime; see the [format matrix](FORMATS.en.md). A container extension alone does not establish decoder support |
| Hosted builds | The workflow covers Windows Server 2022 / 2025, dependency auditing, source / history privacy checks, native tests and packaging; see actual [Actions results](https://github.com/ChenShoukang/kok/actions). Server builds do not replace Windows 10 / 11 desktop verification |
| AI services | Local mocks cover requests, streaming, cancellation and configuration isolation; each real provider, model and version still needs verification |
| Private folder | Authenticated encryption and locking have tests; no independent security audit. Original files remain unencrypted |

## Checks on another machine

Fresh Windows 10 x64 and Windows 11 x64 user environments still need independent verification. Personal API credentials are unnecessary when using samples and local mocks.

1. Extract the whole ZIP to a user-writable folder, retaining DLLs, ui and samples. Verify SHA-256. Startup should show the Pure / Smart choice.
2. Choose Pure without entering credentials. Disconnect the network and import samples; check video pause / seek / fullscreen, music queues, photo copies and PDF / EPUB annotations and notes.
3. Restart and confirm the mode, library, reading position and notes persist. Removing a library entry must leave its original file intact.
4. Check Chinese / English, both themes, 100% / 150% / 200% scaling, compact windows and reduced motion; inspect labels, dialogs, navigation and reading areas.
5. Create a test private folder with redistributable samples. Verify passwords, deactivation locking and unlocking after restart. Do not treat it as source-file encryption.
6. In Smart mode, use a local test service for configuration, translation and cancellation, including incorrect endpoint / model feedback. Real-provider tests require your authorization and may incur charges.

Developers can run a real-file regression on their own files with `tools/test-desktop.ps1 -Suites real-files -RealFilesFolder <folder>`. It imports, opens and checks each file in an isolated library, removes the entries afterwards and verifies the originals are unchanged. The report and screenshots are written only to `test-output/real-files/` and are never uploaded.

Report app / Windows / WebView2 versions, display scaling, steps and redacted evidence using the [issue templates](https://github.com/ChenShoukang/kok/issues/new/choose). See [Security](../SECURITY.en.md) for sensitive reports.

## Common first-use problems

- **Missing runtime**: install [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) and [WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/), then restart. Node.js and the .NET SDK are unnecessary to run the app.
- **Cannot write the library**: move the complete extracted folder to a user-writable location rather than Program Files; do not move only the executable.
- **Audio without video, or failed playback**: check the actual codec or open the file with a system app. No independent decoder is bundled; avoid untrusted codec downloads.
- **Library already open**: close the other kok window using that profile; use an independent `--data-dir` for development.
- **Logs needed**: the default location is `data/logs/kok.log` inside the extracted folder. Redact personal paths and other private information before sharing.

## Versions and historical downloads

Download [v0.8.1 Preview](https://github.com/ChenShoukang/kok/releases/tag/v0.8.1); use the files and checksums actually available in Releases. Older development numbers do not imply stable 1.0 support.

Documentation and build procedures may be revised after the app version is published. Existing Release ZIPs are not silently replaced; use the matching commit's Actions artifacts for current source builds. Source, history and package pattern scanning is not a complete security audit.

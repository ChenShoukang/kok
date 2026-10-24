# Security

[中文](SECURITY.md) · English

## Supported versions

The latest v0.3.1 preview and main branch receive maintenance. Upgrade older releases first. The project has no independent security audit and does not promise a fixed response time.

## Reporting a security issue

Prefer GitHub's [private vulnerability reporting](https://github.com/ChenShoukang/kok/security/advisories/new). If the repository or account does not provide that option, [open an issue](https://github.com/ChenShoukang/kok/issues/new) asking only for a private security contact. Do not include vulnerability details there.

In a private report, include the app, Windows and WebView2 versions, reproduction steps, impact and a minimal redistributable sample. Do not publish passwords, API credentials, personal documents, complete profiles or unredacted logs.

For ordinary playback or interface bugs, use the [issue templates](https://github.com/ChenShoukang/kok/issues/new/choose).

## Data and protection boundaries

- Pure blocks AI requests in native code. Smart sends context to the configured service when related features run; automatic translation defaults off. Provider data policies and billing apply.
- Windows DPAPI encrypts API credentials for the current user. Credentials are excluded from Git source and release packages. Ordinary library records, notes and chat histories are outside private-folder encryption.
- The private folder encrypts only its own index, notes and covers. **Original files remain at their original paths, unencrypted and visible; ordinary entries for the same source are not automatically hidden.** It does not replace disk encryption or protect against software controlling the current user or reading process memory. See [modes and private storage](docs/MODES.en.md).
- Do not open everyday profiles with `--debug`; the debugging interface is for isolated local development tests.
- Open files from trusted sources and keep Windows and WebView2 current. Portable packages are unsigned; verify their accompanying SHA-256 checksums.

Builds include dependency auditing, source / Git-history credential-pattern checks and release privacy checks. These cannot prove the absence of vulnerabilities or every form of secret data.

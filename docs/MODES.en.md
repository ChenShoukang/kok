# Modes and the private folder

[简体中文](MODES.md) · [Documentation](README.en.md)

On first entry, choose Pure or Smart. Existing libraries without a saved choice also see this selector. Two vector symbols, glass cards and gentle animation introduce these modes, with bilingual text, compact layouts and reduced-motion support. Privacy is a folder inside the library, not a third app mode.

**Pure mode** keeps local video, music, photos, documents and books, including adaptive media previews, subtitle loading, photo editing, underlines, highlights and notes. AI, Smart library and extra sidebar groups are hidden. The native host blocks translation, chat and AI organization requests. Automatic translation and background folder scans do not start. External HTTP/HTTPS resource requests from the app page are blocked; the interface, PDF.js, fonts and local media load offline.

**Smart mode** keeps the full interface and local functionality, adding optional AI translation, organization and content conversations. The first entry shows a model notice explaining configuration, shared content and provider charges. Continue with your existing settings or open model configuration. Entering the mode itself makes no model request. If automatic translation is enabled, opening supported content can still trigger requests according to that setting.

Choose **Change mode** in settings anytime. The mode and first-notice state persist. Once seen, the Smart notice is not repeated. Switching to Pure cancels active AI tasks and the current background scan. Original files, notes, conversations, watched sources and encrypted credentials are retained for later use.

## Private folder

Open **Private folder** from the library or sidebar, create an 8–256 character password once, and enter it on later visits. The folder is available in both Pure and Smart. Opening keeps the ordinary sidebar, theme and language while showing its files, filters, reading and notes. Its content never enters AI requests or ordinary categories/search. Back to library, another sidebar category, Lock folder or `Ctrl Shift L` locks it. Restarting requires the password again.

The folder stores a separate encrypted index. Open it before dropping or importing files. Existing ordinary-library entries are not moved or hidden automatically, including entries for the same original source file.

The vault locks on window deactivation, minimize or five minutes idle. Owned file dialogs defer the lock; an in-progress import is covered immediately and finishes before locking. Locking saves current progress and notes, clears private UI content, disposes encryption keys, clears the browser disk cache and invalidates private media URLs.

The index and notes in `data/private-vault/` use PBKDF2-SHA256 with 210,000 iterations and a random salt, AES-256-CBC with random IVs, and HMAC-SHA256 authentication verified before decryption. Covers are encrypted too. Passwords are not written to metadata; repeated failed attempts are throttled. Lost passwords cannot be recovered.

**This protects kok's index, notes and covers. Original media and documents remain at their source paths and are not encrypted, moved or hidden.** Imported fonts and external system applications are outside this protection; external apps can retain their own history. Private does not replace disk encryption or defend against software able to read process memory.

## Encrypted backups

Private folder → Settings → Export library creates `.kokvault`, containing `vault-info.json`, encrypted `library.vault` and `thumbs/*.sealed`. It excludes original files, AI configuration and browser caches. The original password is required; source files must remain available at their indexed paths.

There is no one-click restore UI yet. Close kok, back up the existing `data/private-vault/` directory, open the `.kokvault` file as ZIP and restore its contents into that directory. Restart and unlock with the original password. Never mix metadata, index or covers from different vaults. Restore original source files separately to the same paths when migrating computers.

The app does not upload user files, passwords or model configuration to GitHub. Release packages use a clean file allowlist and check private filenames and credential patterns.

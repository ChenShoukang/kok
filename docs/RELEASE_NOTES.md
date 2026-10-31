# kok v0.9.0 Preview

这一版把资料的“采集 → 检索 → 有依据的 Agent 操作 → 可带走与恢复”接成可用流程，保留原有媒体、阅读、笔记、空间和创作能力。

- **有来源的检索**：正文 / PDF 文字层 / 字幕 / 摘录统一分块，显示覆盖与失败；编辑后旧片段失效。可选本机 Ollama embedding 与 FTS5 混合检索，搜索和 Agent 共用，模型不可用时明确回到文字匹配。
- **随手采集**：应用内收件箱，随包 Chrome / Edge 源码扩展保存网页文字、选区与图片；记录来源与时间，相同内容去重。本机 Windows OCR 可把图片或指定 PDF 页的识别文字加入索引。
- **可以带走、可以恢复**：标准 ZIP 便携资料库，提供 Markdown / JSON、稳定 ID、摘录锚点与空间关系；托管文档必带，其他原件可选。校验附件后恢复到新库，支持根目录映射、原件重连与撤销。
- **对外只读互通**：默认关闭的 stdio MCP server，明确选择可读资料，提供列表 / 词面搜索 / 摘录读取。采集与读取密钥分开，私密和切库时拒绝访问。
- **继续改进 Agent 核对**：版本化事件、流式中断正文保留、逐次 / 只读 / 有限任务授权、token 估计校准、精确计算、写入回查与连续修改证据；失效和拒绝回执有明确修复提示。

下载 `kok-0.9.0-win-x64.zip` 和 SHA-256，核对后完整解压运行 `kok.exe`。需要 Windows 10 / 11 x64、.NET Framework 4.8、WebView2。运行无需 Node.js。升级前备份 data 与原件，保留现有 data 配置。扩展从 `browser-extension` 加载，语义模型另行安装；OCR 需要 Windows OCR 语言。

这是未签名 Preview。便携包是普通资料快照，不包含私密资料、模型密钥、配对、Agent / 聊天历史或向量索引。没有自动云同步、移动端、扩展商店上架、图片语义搜索或对外写入工具。MCP 使用真实 stdio 客户端子进程验收，未认证所有第三方软件。回执核对不保证全部语义正确，近似检索不保证存在答案。

[完整操作和边界](LIBRARY_0.9.0.md) · [真实模型 / 本地检索 / 安装包验证](VALIDATION_0.9.0.md) · [后续路线](../ROADMAP.md) · [历史版本](../CHANGELOG.md)

## English

Connect capture, source-linked retrieval, reviewed Agent work and portable recovery while retaining existing media, reading and editing features.

- Incremental body/PDF/subtitle/Clip chunks with source locations, visible failures and stale-content invalidation. Optional local Ollama embeddings plus FTS5 share retrieval between UI and Agent, with explicit lexical fallback.
- Inbox and a Chrome/Edge source extension for page text, selections and images, with provenance and deduplication. Offline Windows OCR indexes images or selected PDF pages.
- Standard ZIP libraries with Markdown/JSON, stable IDs, Clips/Spaces, managed/optional originals and checksum-verified new-library restore, root mapping and undoable relinking.
- Default-off scoped read-only stdio MCP server, separate capture/read credentials and private/transition safeguards.
- Versioned Agent events, interrupted prose preservation, limited task/read-only approval, token calibration, exact arithmetic, write read-back and actionable stale-receipt review.

Extract the full ZIP and run kok.exe after checking SHA-256. Requires Windows 10/11 x64, .NET Framework 4.8 and WebView2; no Node.js runtime. Back up data/originals before updating. Load the bundled extension as source; install local embedding models/OCR languages separately.

Unsigned Preview. Portable libraries contain ordinary sources, excluding private data, credentials, pairing, Agent/chat history and embeddings. No automatic cloud sync, mobile app, extension-store release, image semantic search or external writes. Real stdio subprocess tests do not certify all clients; receipt checks do not prove every semantic claim and approximate search may return unrelated candidates.

[Guide](LIBRARY_0.9.0.en.md) · [Validation and limits](VALIDATION_0.9.0.md) · [Roadmap](../ROADMAP.en.md)

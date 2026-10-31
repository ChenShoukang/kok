# kok v0.9.3 Preview

这一版补齐 v0.9.2 留下的三项工作：**可用的本地 MCP AppContainer 沙箱**、**MCP OAuth 设备码**、**PDF/视频批量索引**。本地优先、逐次审批与证据核对规则不变；本版没有新增真实模型调用。

- **本地 MCP 沙箱（便携运行时）**：每个 stdio 服务可开启 AppContainer 沙箱并指定便携运行时目录；只授权用户可写目录的只读执行 ACL，Node.js 自动补充 symlink-preservation 标志。系统安装目录/不可授权目录会明确报错，不会静默降级。该路径已纳入原生 CI 门禁（复制 Node 运行时的真实 MCP 握手与批准调用）。
- **MCP OAuth 设备码**：新增 RFC 8628 设备码流程，支持 user code/验证地址展示、`authorization_pending`/`slow_down`/过期/拒绝处理、DPAPI 令牌与 refresh token；设置中可选择浏览器回调或设备码。
- **PDF/视频批量索引**：新增两个审批型工具——`index_pdf_pages`（PDFium 文本层，最多 25 页）与 `index_video_frames`（可选 FFmpeg 抽帧 + Windows OCR，最多 12 帧），把有界文本合并进本地内容索引；不调用模型、不修改原文件，源变化自动失效。
- **验证**：542 项原生确定性检查 + 11 组桌面套件；真实模型总账保持 359，本版没有新增模型调用。

下载 `kok-0.9.3-win-x64.zip` 并核对随附 SHA-256，完整解压后运行 `kok.exe`。需要 Windows 10/11 x64、.NET Framework 4.8 与 WebView2；升级前备份 data 与原文件。

[Agent 能力与边界](AGENT_0.9.3.md) · [验证](VALIDATION_0.9.3.md) · [v0.9.2 能力](AGENT_0.9.2.md) · [Roadmap](../ROADMAP.md) · [Changelog](../CHANGELOG.md)

## English

kok v0.9.3 finishes the remaining local MCP sandbox, OAuth and indexing work from v0.9.2: AppContainer sandboxing from an explicitly chosen portable-runtime directory (Node.js gets symlink-preservation flags automatically; only ACL-grantable user-writable directories, explicit failures with no silent fallback, and a native CI gate using a copied Node runtime); the RFC 8628 OAuth device-code flow with pending/slow_down/expiry handling, DPAPI tokens and refresh; and approval-gated `index_pdf_pages` (PDFium text layer, up to 25 pages) and `index_video_frames` (optional FFmpeg + Windows OCR, up to 12 frames) that merge bounded local text into the content index without any model call.

Deterministic coverage: 542 native checks and 11 desktop suites. The real-model ledger stays at 359; no additional model calls were made for this release.

Extract the full ZIP and run kok.exe after checking SHA-256. Requires Windows 10/11 x64, .NET Framework 4.8 and WebView2. See [Agent capabilities and limits](AGENT_0.9.3.md) and [validation](VALIDATION_0.9.3.md).

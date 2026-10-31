# v0.9.3 验证与发行记录

[项目首页](../README.md) · [v0.9.3 Agent 能力与边界](AGENT_0.9.3.md) · [English summary](#english-summary)

验证日期：2026-10-31（Asia/Taipei）。v0.9.3 完成便携运行时 MCP 沙箱、MCP OAuth 设备码与 PDF/视频批量索引。真实模型请求总账保持 **359**，本版**没有新增真实模型调用**（批量索引为本地 PDFium/OCR，沙箱与 OAuth 为确定性验证）。

## 本机确定性验证

- 原生回归：**542 项通过**（v0.9.2 的 528 项 + 便携运行时 MCP 沙箱 3 项 + 设备码 OAuth 7 项 + PDF/视频批量索引 6 项，含提示/索引边界守卫）。
- `npm run check`、Windows PowerShell 脚本兼容检查、文档链接检查通过。
- 新增/变更依赖：无新增第三方库；复用 PDFium（BSD-3-Clause）与可选 FFmpeg。许可证与 NOTICE 不变。
- 本机发行 ZIP：`kok-0.9.3-win-x64.zip`，30.27 MB（31,741,509 bytes），SHA-256 `b5fa04efbd70251b3ff1861759f18bc291e5ccfcb52dfa9ab3b423713f26d117`。
- 桌面套件：**11 / 11 通过**（ui、features、agent、agent-interactions、agent-reliable、agent-runtime、agent-kernel、mcp、agent-platform、agent-quality、library-services），针对上述 ZIP 解压后的 `kok.exe` 运行。
- 包级记录：[package-0.9.3-local.json](validation/package-0.9.3-local.json)；源文件指纹 `ccd9fded0dd54a2fdefd8697cd45c2dd5977fbbee5828acb56899ace14ad301d`（429 个源文件），运行时指纹 `58e3046e1247f51da2573f56aab40d729e77e06e32b500d270a42eaf87e8d4db`（362 个文件），`kok.exe` SHA-256 `1db96f62d701ad840ebdf635269378c2b788e21b6bc264b7d98d8d59b35dc91f`。

## 覆盖的确定性回归（新增部分）

| 领域 | 覆盖内容 |
| --- | --- |
| 便携运行时 MCP 沙箱 | AppContainer 可用性、可授权目录持久化、复制 Node 运行时 + 自动 symlink 标志、现代 MCP 握手/目录、批准调用回传、无效运行时目录提前拒绝 |
| OAuth 设备码 | 元数据设备端点、动态注册、user code/验证地址、`authorization_pending`/`slow_down` 轮询、令牌与刷新、DPAPI 配置、服务端启动与令牌落库、拒绝不保存令牌 |
| 批量索引 | PDF 页码范围与数量上限、PDFium 文本层两页写入、后续批量合并、源变化重新索引、视频帧时间戳 OCR 块、错误类型拒绝 |

## 真实模型评估边界

真实模型总账 359 来自 v0.9.2 发布后的用户授权评测（见 [VALIDATION_0.9.2_LIVE.md](VALIDATION_0.9.2_LIVE.md)）。本版没有新增真实模型调用；确定性回放、PDFium 与 OCR 夹具不能换算成模型成功率。OAuth/沙箱的第三方服务兼容性仍需用户用自己的账号实测。

## 未实现与边界

通用脚本宿主自动发现（目前明确支持用户可写目录中的便携运行时，Node.js 已适配）、OAuth 企业 SSO 与设备码之外的特殊流程、扫描 PDF/视频的整本语义索引不在本版范围。MCP 沙箱只支持 ACL 可授权的目录，系统安装目录会明确拒绝而不是绕过。视频帧索引依赖可选 FFmpeg 与 Windows OCR。

## 发布记录

云端 Windows 2022 / 2025 构建、原生检查、隐私与依赖审计、待发布 ZIP 的 11 组桌面套件全部通过后，v0.9.3 已于 2026-10-31T14:35:17Z 发布为 Preview：

- Release：https://github.com/ChenShoukang/kok/releases/tag/v0.9.3（run `41210115797`，tag/commit `36b6f56`）。
- 云端 11 / 11 桌面套件通过；云端 ZIP `kok-0.9.3-win-x64.zip` 为 **32,126,127 bytes**，SHA-256 `675e78dfc57fd480e5f8f950a920e6cf368930f5d8378f6d986e9aaac1c054ee`。
- 独立下载后重算 SHA-256、核对随附 `.sha256` 与 GitHub asset digest 均一致（`checksumPassed: true`）；核验记录见 [release-0.9.3-download.json](validation/release-0.9.3-download.json)。
- 便携运行时 MCP AppContainer 沙箱的复制 Node 运行时门禁在云端 Windows 2022 与 2025 均通过。

## English summary

Deterministic coverage: 542 native checks (portable-runtime MCP sandbox, OAuth device code, PDF/video batch indexing) plus 11 desktop suites on the release ZIP. No new third-party dependencies and no additional real-model calls (ledger 359). Limits: portable-runtime directories only (Node.js adapted), no enterprise SSO, and no whole-document/video semantic indexing; sandbox failures are explicit and never fall back silently.

# v0.9.2 验证与发行记录

[项目首页](../README.md) · [v0.9.2 Agent 能力与边界](AGENT_0.9.2.md) · [English summary](#english-summary)

验证日期：2026-10-31（Asia/Taipei）。真实模型请求总账仍为 **150 / 150**，本版没有新增真实模型调用。v0.9.2 在 v0.9.1 基础上新增 OAuth MCP、PDF 页/视频帧视觉与 AppContainer 沙箱，既有能力保持不变。

## 本机确定性验证

- 原生回归：**527 项通过**（v0.9.1 的 494 项 + PDF 页/视频帧视觉 11 项 + MCP OAuth 10 项 + AppContainer 命令与本地 MCP 沙箱 12 项）。
- `npm run check`、Windows PowerShell 脚本兼容检查、文档链接检查通过。
- 新增依赖：PDFium（pdfium-binaries chromium/8125，BSD-3-Clause；NuGet 无、GitHub tgz 与 DLL 双重 SHA-256 锁定，随包携带完整 NOTICE）。FFmpeg 仍是按需媒体组件，不随 ZIP 分发。
- 本机发行 ZIP：`kok-0.9.2-win-x64.zip`，30.25 MB（31,715,987 bytes），SHA-256 `4c3ad38ed0edd4a44ebe00753cc0d90d24c8865003d19e29277dafbccf156830`。
- 桌面套件：**11 / 11 通过**（ui、features、agent、agent-interactions、agent-reliable、agent-runtime、agent-kernel、mcp、agent-platform、agent-quality、library-services），针对上述 ZIP 解压后的 `kok.exe` 运行。
- 包级记录：[package-0.9.2-local.json](validation/package-0.9.2-local.json)，源文件指纹 `ec7b5615e61100b490346cfc47543a8b62edfc527386c97d55a741f8c5c4b369`（427 个源文件），运行时指纹 `0dede50874325288768a23bd5dfb77322d4418e969d7fcb52251685ebc8192dc`（362 个文件），`kok.exe` SHA-256 `f6360efe0198bf985176e99f3647fbc2351346deaffebee0a39c0e5c3de063f6`。

## 覆盖的确定性回归（新增部分）

| 领域 | 覆盖内容 |
| --- | --- |
| PDF/视频视觉 | PDFium 页面栅格化、页码范围、图片误用 page、DPAPI 帧缓存、源变化失效、视频时间戳抽取与无 FFmpeg 明确报错、图像内容块构造 |
| MCP OAuth | PKCE S256、受保护资源/授权服务器元数据、动态注册、回环回调、state 校验、令牌交换与 resource 参数、DPAPI 加密、refresh token、401 单次刷新重试、OAuth 服务连接 |
| AppContainer 命令 | 沙箱可用性、工作目录内可写、工作目录外拒绝写入、无效可执行文件拒绝、配置持久化与模式显示 |
| 沙箱 MCP | per-server 开关、AppContainer 内完成 MCP 握手/目录、批准后调用回传 |

## 真实模型评估边界

150 次总账来自 v0.9.0 的 30 例与定向复验，本版**没有运行新的真实模型评测**。确定性回放与 fixture 不能换算为模型成功率；PDF/视频视觉与沙箱能力需要用户用自己的模型和程序实测。

## 未实现与边界

OAuth 设备码/企业 SSO、虚拟机级隔离、跨平台/移动端/云同步、整份 PDF/视频内容索引不在本版范围。AppContainer 沙箱依赖 Windows 与可授权的程序 ACL：系统盘 Program Files 中的运行时可能无法在沙箱内启动，会报明确错误且不会绕过；用户可写目录中的便携运行时可用。沙箱默认关闭，关闭时界面与工具结果都会明确标注未隔离状态。

## 发布记录

云端 Windows 2022 / 2025 构建、原生检查、隐私与依赖审计、待发布 ZIP 的 11 组桌面套件全部通过后才会发布。本页在云端结果与下载核验完成后追加记录。

## English summary

Deterministic coverage: 527 native checks (vision for PDF pages/video frames, MCP OAuth, AppContainer sandbox) plus 11 desktop suites on the release ZIP. PDFium is checksum-pinned with a full license notice; FFmpeg remains an optional component. No additional real-model calls were made (ledger 150). Limits: no device-code/enterprise OAuth, no VM-level isolation, no whole-document/video indexing; sandbox launch requires ACL-grantable program files and is opt-in.

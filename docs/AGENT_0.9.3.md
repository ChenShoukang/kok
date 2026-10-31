# Agent v0.9.3：可用的 MCP 沙箱、OAuth 设备码、PDF/视频批量索引

[项目首页](../README.md) · [English summary](#english-summary) · [v0.9.2 能力](AGENT_0.9.2.md) · [验证](VALIDATION_0.9.3.md)

v0.9.3 完成 v0.9.2 留下的三项工作：**本地 MCP 的便携运行时 AppContainer 沙箱**、**MCP OAuth 设备码流程**、**PDF 页文本与视频帧 OCR 的批量索引**。全部保持逐次审批、本地优先；本版没有新增真实模型调用，总账仍为 359。

## 1. 本地 MCP 沙箱：便携运行时

- 每个本地 stdio MCP 服务可开启 AppContainer 沙箱，并指定一个**便携运行时目录**（例如复制到用户目录的 Node.js）。连接前，主程序会为该目录及其现有内容授予 AppContainer 只读执行 ACL，再通过受信 helper 以无 capability 令牌启动服务。
- **Node.js 便携运行时自动适配**：检测到 `node.exe` 时会自动补充 `--preserve-symlinks-main --preserve-symlinks`，避免 AppContainer 无法解析祖先目录；这正是此前脚本运行时失败的原因。
- 只有**用户可写、可修改 ACL 的目录**能被授权。系统安装目录（如 Program Files）或无法授权的目录会得到明确错误，**不会静默降级为未隔离启动**；用户可把运行时复制到用户目录，或关闭该服务的沙箱。
- 沙箱进程无网络 capability、无用户文件读取权限，只能通过显式授权的运行时目录与参数文件完成启动；stdin/stdout 经受信 helper 继承，JSON-RPC、逐工具审批、Schema 校验和"结果不确定不重放"规则全部沿用。
- CI 门禁恢复：新增确定性测试把 `node.exe` 与 MCP fixture 复制到用户可写目录，在 AppContainer 内完成现代 MCP 握手、工具目录与一次审批调用；该测试在本地与云端 Windows 2022/2025 运行。

## 2. MCP OAuth 设备码

- 除授权码 + PKCE 外，新增 **RFC 8628 设备码流程**：读取授权服务器元数据中的 `device_authorization_endpoint`，动态注册客户端，请求设备码与 `verification_uri`，按 `interval` 轮询令牌端点。
- 处理 `authorization_pending`、`slow_down`（自动增加间隔）、`access_denied`、`expired_token` 与总过期时间；成功后令牌与静态令牌一样 **DPAPI 加密**保存，并支持 refresh token 与 401 单次刷新重试。
- 设置界面新增 OAuth 方式选择（浏览器回调 / 设备码）；设备码登录会显示 user code、验证地址与有效期，并在完成后自动刷新连接状态。设备码登录不打开本地回环端口。
- 边界：仍需授权服务器支持动态客户端注册；不支持企业 SSO 特殊流程、手动粘贴 client_secret 或设备码之外的非标准轮询。

## 3. PDF/视频批量索引（逐次审批）

- `index_pdf_pages`：经用户批准后，用 PDFium 文本层批量提取 **最多 25 页**，合并进本地内容索引；已索引页面不会被丢弃，源文件变化会通过内容戳失效并重新索引。不调用模型、不修改 PDF。
- `index_video_frames`：经用户批准后，用可选 FFmpeg 在指定起点/步长抽取 **最多 12 帧**，对每帧运行 Windows OCR，把带时间戳的文本写入本地内容索引；FFmpeg 或 OCR 不可用时明确报错，不做静默降级。
- 索引完成后 `search_library` / `read_items` 可使用这些页面或帧文本；批量索引结果与其他内容索引一样受源戳、数量与大小上限约束。
- 边界：PDF 仅文本层（扫描件需 OCR/既有流程）；视频帧索引依赖 FFmpeg 与 Windows OCR；索引是本地文本，不生成或上传视觉嵌入。

## 验证与边界

- 确定性回归：**542 项原生检查**（新增便携运行时沙箱、设备码、PDF/视频批量索引与提示/权限守卫）与 11 组桌面套件；真实模型总账保持 **359**，本版未新增模型调用。
- 未实现：MCP 沙箱的通用脚本宿主自动发现（目前明确支持可授权目录中的便携运行时，Node.js 已自动适配）、OAuth 企业 SSO/设备码之外的特殊流程、扫描 PDF 与视频的整本索引。
- 发布流程：版本升到 0.9.3，重建 ZIP，推送 main 触发云端 Windows 构建与 11 组桌面套件，核验 Release。

## English summary

kok v0.9.3 finishes the remaining MCP sandbox, OAuth and local indexing work from v0.9.2: local stdio MCP servers can run in AppContainer from an explicitly chosen portable-runtime directory (Node.js gets the symlink-preservation flags automatically; ACL-grantable user-writable directories only, with explicit failures and no silent fallback); OAuth adds the RFC 8628 device-code flow with pending/slow_down/expired handling, DPAPI tokens and refresh; and approval-gated `index_pdf_pages`/`index_video_frames` index bounded PDF text layers (PDFium, up to 25 pages) and video-frame Windows OCR (up to 12 frames, optional FFmpeg) into the local content index without any model call. Deterministic coverage: 542 native checks and 11 desktop suites; the real-model ledger stays at 359. Limits: no generic script-host auto-discovery beyond portable runtime directories, no enterprise SSO, and no whole-document/video semantic embeddings.

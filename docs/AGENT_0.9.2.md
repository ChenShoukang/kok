# Agent v0.9.2：OAuth MCP、PDF 页与视频帧视觉、AppContainer 沙箱

[项目首页](../README.md) · [English summary](#english-summary) · [v0.9.1 Agent 能力](AGENT_0.9.1.md) · [验证](VALIDATION_0.9.2.md)

v0.9.2 在 v0.9.1 的八项 Agent 能力上补齐三个长期缺口：远程 MCP 的 OAuth 授权、PDF 页面与视频帧的直接视觉输入、命令与本地 MCP 程序的 Windows AppContainer 隔离。所有能力仍保持本地优先、默认关闭或逐次审批，真实模型调用总账仍为 150 次。

## 1. MCP OAuth

- 远程 HTTP MCP 服务支持 **OAuth 2.1 授权码 + PKCE（S256）**：先读取受保护资源元数据（RFC 9728），再读取授权服务器元数据（RFC 8414），通过动态客户端注册（RFC 7591）获得本机客户端，使用 `127.0.0.1` 临时回环回调接收授权码，并携带 `resource` 指示符完成令牌交换。
- 支持 **refresh token**：连接前若令牌即将过期会主动刷新；远程请求返回 401 时只刷新并重试一次，其他错误不重放。
- 访问令牌与客户端信息存放在 **DPAPI 加密**的 MCP 配置中，界面只显示"已登录/未登录"，不显示令牌；没有 OAuth 配置的静态令牌流程保持原样。
- 设置界面提供"认证方式"（无认证 / 静态令牌 或 OAuth 登录）与"OAuth 登录"按钮；登录在系统浏览器完成，回到 kok 后自动刷新状态。
- 边界：只支持授权码 + PKCE + 动态注册的服务器；不支持 OAuth 设备码、企业 SSO 特殊流程或手动粘贴 client_secret；元数据端点必须为 HTTPS（本机回环可用 HTTP）。

## 2. PDF 页面与视频帧视觉

- `read_image` 新增两个参数：`page`（PDF 1 起始页码）与 `time`（视频时间戳秒数）。图片仍按原方式读取。
- PDF 页面使用 **PDFium（pdfium-binaries chromium/8125，BSD-3-Clause）** 在本机栅格化，最长边 1280 像素、最多 40M 像素、文件上限 100 MB；不修改原 PDF，也不上传整份文档。
- 视频帧使用可选的 **FFmpeg 媒体组件**（应用内按需安装、哈希校验）在指定时间抽取一帧；FFmpeg 未安装时明确提示安装路径，不会静默降级为猜测。
- 渲染结果先用 DPAPI 加密缓存为受限帧文件，再在下一轮模型请求中作为 `image_url` 内容块附加；任务日志只保存帧 id 与来源戳，不复制图片字节。源文件改变后旧帧不再附加。
- 未启用视觉模型时，对同一页/同一帧回退 Windows OCR 文本。
- 边界：不渲染整份 PDF、不做视频内容理解或字幕之外的音频分析；视频时间戳为绝对秒数，不提供精确到帧的帧率导航。

## 3. Windows AppContainer 沙箱

- **命令执行**：启用执行环境后，可另外开启"AppContainer 沙箱"。沙箱命令以当前用户身份创建 AppContainer 进程，但令牌**没有任何 capability**：无法联网、读不到用户目录中的普通文件、唯一可写的目录是用户选择的工作目录（通过包 SID 的 ACL 授权）。工作目录之外的绝对路径写入会被 Windows 拒绝。结果会明确标注 `Sandbox: AppContainer ...`；关闭沙箱时明确标注命令以完整用户权限运行，不会静默回退。
- **本地 MCP**：每个 stdio 服务可单独勾选"本地程序使用 AppContainer 沙箱"。启用后 MCP 程序及其参数中已存在的脚本文件会被授予只读执行权限，进程同样没有网络与用户文件访问；stdin/stdout 通过受信 helper 继承，JSON-RPC 与普通 MCP 客户端行为一致。
- 沙箱进程由受信任的 `kok.exe --sandbox-exec` helper 创建；helper 只接受受限规格文件（可执行文件、至多 64 个参数、存在的绝对工作目录），畸形请求直接拒绝。超时仍由 Job Object 终止整棵进程树。
- 边界：沙箱不是虚拟机，也不是网络代理；它依赖 Windows AppContainer。系统级/Program Files 中的运行时（例如系统安装的 Node.js）可能因 ACL 不可授权而拒绝启动，此时会报明确错误且不会绕过沙箱；需要沙箱时请使用用户可写目录中的便携运行时。沙箱默认关闭，已在 v0.9.1 验证过的无沙箱路径保持可用。

## 安全模型与验证

- 所有新增能力都遵循原有规则：默认关闭或逐次审批；外部内容标记为不可信资料；失败、跳过、撤销不能作为完成证据；不确定的外部或沙箱结果不自动重放。
- 明确未实现：OAuth 设备码/企业 SSO、操作系统虚拟机级隔离、跨平台/移动端/云同步、整份 PDF 或视频的自动内容索引。
- 本版为确定性验证：**527 项原生检查 + 11 组桌面套件**（待发布验证）且不新增真实模型调用；没有新的真实模型成功率结论。

## English summary

kok v0.9.2 adds OAuth 2.1 authorization-code + PKCE (with protected-resource/authorization-server discovery, dynamic client registration, loopback callback, refresh-on-401 and DPAPI-encrypted tokens) for remote MCP; PDF pages rasterized locally by vendored PDFium and single video frames extracted by the optional FFmpeg component for `read_image`; and an opt-in Windows AppContainer sandbox for approved commands and local MCP servers (no capabilities, no network, no user-file access, writes only inside the configured workspace, explicit unsandboxed labeling). Deterministic coverage: 527 native checks plus the 11 desktop suites; no additional real-model calls were made (ledger stays at 150). Limits: no device-code/enterprise SSO OAuth, no whole-document/video indexing, no VM-level isolation; AppContainer requires program/runtime ACLs that user-writable portable runtimes provide, while some system-installed runtimes may refuse sandboxed launch.

# kok v0.9.2 Preview

这一版在 v0.9.1 的八项 Agent 能力上补齐三个长期缺口：**MCP OAuth、PDF 页/视频帧视觉、Windows AppContainer 沙箱**。本地优先、逐次审批和证据可核对的规则不变。

- **MCP OAuth**：远程 HTTP MCP 支持 OAuth 2.1 授权码 + PKCE，包含受保护资源与授权服务器元数据发现、动态客户端注册、本机回环回调、refresh token 与 401 单次刷新重试；令牌以 DPAPI 加密保存，界面不显示明文。设置中新增"认证方式 / OAuth 登录"。
- **PDF 页与视频帧视觉**：`read_image` 支持 `page`（PDF 页码，PDFium 本机栅格化）与 `time`（视频时间戳，可选的 FFmpeg 组件抽帧）。渲染帧以 DPAPI 加密缓存，源文件变化后失效；任务日志不复制图片字节；无视觉模型时同一帧回退 Windows OCR。
- **AppContainer 沙箱**：审批命令可开启沙箱——无网络、无用户文件访问、只允许写工作目录（521+ 项确定性检查覆盖）。本地 stdio MCP 也有实验性的逐服务沙箱开关，但脚本宿主在 Server CI 内不稳定，未纳入本版发布门禁。结果明确标注 `Sandbox: AppContainer` 或未隔离；沙箱启动失败会报错且不会静默绕过。
- **依赖与许可**：PDFium（BSD-3-Clause）以 NuGet 之外的 GitHub 归档双重 SHA-256 锁定并随包携带完整 NOTICE；FFmpeg 仍按需安装。
- **验证**：522 项原生确定性检查 + 11 组桌面套件；未新增真实模型调用（总账仍为 150），没有新的模型成功率结论。

下载 `kok-0.9.2-win-x64.zip` 并核对随附 SHA-256，完整解压后运行 `kok.exe`。需要 Windows 10/11 x64、.NET Framework 4.8 与 WebView2；运行无需 Node.js。升级前备份 data 与原文件。

[Agent 能力与边界](AGENT_0.9.2.md) · [验证](VALIDATION_0.9.2.md) · [v0.9.1 能力](AGENT_0.9.1.md) · [Roadmap](../ROADMAP.md) · [Changelog](../CHANGELOG.md)

## English

kok v0.9.2 closes three gaps on top of v0.9.1: OAuth 2.1 authorization-code + PKCE for remote MCP (metadata discovery, dynamic registration, loopback callback, refresh-on-401 and DPAPI-encrypted tokens); direct vision over one PDF page (local PDFium rasterization) or one video frame at a timestamp (optional FFmpeg), with encrypted frame caches and OCR fallback; and an opt-in Windows AppContainer sandbox for approved commands and local MCP servers (no network, no user-file access, writes only inside the workspace, explicit unsandboxed labeling, no silent fallback).

Deterministic coverage: 522 native checks and 11 desktop suites. No additional real-model calls were made (ledger stays at 150); no model success-rate claim is made for this build.

Extract the full ZIP and run kok.exe after checking SHA-256. Requires Windows 10/11 x64, .NET Framework 4.8 and WebView2. The AppContainer sandbox depends on ACL-grantable program files; some system-installed runtimes may refuse and are never bypassed. See [Agent capabilities and limits](AGENT_0.9.2.md) and [validation](VALIDATION_0.9.2.md).

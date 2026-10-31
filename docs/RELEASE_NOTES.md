# kok v0.9.4 Preview

v0.9.4 是一次完整的界面改版，不改变数据格式、原生桥接或 Agent 能力边界。新版采用统一的扁平视觉语言、分组导航、统一文件卡片/列表与阅读/媒体工具栏，并补齐空状态、创建入口、键盘导航、菜单焦点恢复与减少动效支持。旧 classic/graphite 皮肤偏好保留可读，但统一渲染为扁平界面。

- **布局与导航**：常用目的地保持可见，次要工具收进可记忆展开状态的“更多工具”；文件工具栏统一拥有类型过滤、排序、视图切换和更多工具。
- **文件与阅读**：统一卡片/列表、长文件名截断、媒体徽标与进度、PDF/EPUB/DOCX/Markdown/TXT 预览在窄窗口换行；PDF 工具、Agent 窄窗口、音乐页和设置页逐页复核。
- **可访问性**：键盘文件导航、Space/Shift 选择、Ctrl+F 聚焦搜索、Shift+F10 打开菜单、Escape 清除选择与焦点恢复；`prefers-reduced-motion` 生效。
- **图标**：使用 Lucide 0.468.0 的离线子集（ISC），来源 URL 与 SHA-256、许可文本随源码/包保留。
- **修复**：organization 页英文切换在后台重绘后仍保持英文；documents 私有文件夹步骤与关闭刷新的测试时序稳定化；审查测试创建空间竞态。

验证：**542 项原生检查、25 组界面/回归桌面套件、14 组包级门禁**通过；逐页截图 **120 张**，几何越界与运行时错误均为 **0**。真实模型总账保持 **359**，本版**没有新增真实模型调用**。

发布 ZIP：`kok-0.9.4-win-x64.zip`，32,222,039 bytes，SHA-256 `852314319d475aa21b0eff1287817e3e04b79d036bb952f92b6b43cf76abeebb`（本机候选为 31,832,283 bytes、SHA-256 `6baa9745a90b8afededd76da673180b692799fbb045c561b52a7c5207e53f24d`）。云端发布与下载校验见 [v0.9.4 验证](VALIDATION_0.9.4.md)。

[扁平界面说明](FLAT_UI.md) · [界面验证](INTERFACE_VALIDATION.md) · [v0.9.4 验证](VALIDATION_0.9.4.md) · [Roadmap](../ROADMAP.md) · [Changelog](../CHANGELOG.md)

## English

kok v0.9.4 is a UI refresh release with no data format, native bridge or Agent capability changes. It adopts a flat visual system, grouped navigation, unified file cards/lists and consistent reading/media toolbars, plus clearer empty states, creation entries, keyboard navigation, menu focus recovery and reduced-motion support. Saved classic/graphite skin preferences remain readable but render as the flat interface.

Deterministic coverage: **542 native checks, 25 local UI/regression desktop suites and a 14-suite package gate**; **120 reviewed screenshots** with zero geometry or runtime findings. The real-model ledger stays at **359** and this release makes **no new model calls**. Published ZIP: `kok-0.9.4-win-x64.zip`, 32,222,039 bytes, SHA-256 `852314319d475aa21b0eff1287817e3e04b79d036bb952f92b6b43cf76abeebb` (the local candidate was 31,832,283 bytes, SHA-256 `6baa9745a90b8afededd76da673180b692799fbb045c561b52a7c5207e53f24d`). See [flat UI notes](FLAT_UI.md), [interface validation](INTERFACE_VALIDATION.md) and [v0.9.4 validation](VALIDATION_0.9.4.md).

---

## v0.9.3 Preview（历史）

这一版补齐 v0.9.2 留下的三项工作：**可用的本地 MCP AppContainer 沙箱**、**MCP OAuth 设备码**、**PDF/视频批量索引**。本地优先、逐次审批与证据核对规则不变；本版没有新增真实模型调用。

- **本地 MCP 沙箱（便携运行时）**：每个 stdio 服务可开启 AppContainer 沙箱并指定便携运行时目录；只授权用户可写目录的只读执行 ACL，Node.js 自动补充 symlink-preservation 标志。系统安装目录/不可授权目录会明确报错，不会静默降级。该路径已纳入原生 CI 门禁（复制 Node 运行时的真实 MCP 握手与批准调用）。
- **MCP OAuth 设备码**：新增 RFC 8628 设备码流程，支持 user code/验证地址展示、`authorization_pending`/`slow_down`/过期/拒绝处理、DPAPI 令牌与 refresh token；设置中可选择浏览器回调或设备码。
- **PDF/视频批量索引**：新增两个审批型工具——`index_pdf_pages`（PDFium 文本层，最多 25 页）与 `index_video_frames`（可选 FFmpeg 抽帧 + Windows OCR，最多 12 帧），把有界文本合并进本地内容索引；不调用模型、不修改原文件，源变化自动失效。
- **验证**：542 项原生确定性检查 + 11 组桌面套件；真实模型总账保持 359，本版没有新增模型调用。

下载 `kok-0.9.3-win-x64.zip` 并核对随附 SHA-256，完整解压后运行 `kok.exe`。需要 Windows 10/11 x64、.NET Framework 4.8 与 WebView2；升级前备份 data 与原文件。

[Agent 能力与边界](AGENT_0.9.3.md) · [验证](VALIDATION_0.9.3.md) · [v0.9.2 能力](AGENT_0.9.2.md) · [Roadmap](../ROADMAP.md) · [Changelog](../CHANGELOG.md)

### English（v0.9.3）

kok v0.9.3 finishes the remaining local MCP sandbox, OAuth and indexing work from v0.9.2: AppContainer sandboxing from an explicitly chosen portable-runtime directory (Node.js gets symlink-preservation flags automatically; only ACL-grantable user-writable directories, explicit failures with no silent fallback, and a native CI gate using a copied Node runtime); the RFC 8628 OAuth device-code flow with pending/slow_down/expiry handling, DPAPI tokens and refresh; and approval-gated `index_pdf_pages` (PDFium text layer, up to 25 pages) and `index_video_frames` (optional FFmpeg + Windows OCR, up to 12 frames) that merge bounded local text into the content index without any model call.

Deterministic coverage: 542 native checks and 11 desktop suites. The real-model ledger stays at 359; no additional model calls were made for this release.

Extract the full ZIP and run kok.exe after checking SHA-256. Requires Windows 10/11 x64, .NET Framework 4.8 and WebView2. See [Agent capabilities and limits](AGENT_0.9.3.md) and [validation](VALIDATION_0.9.3.md).

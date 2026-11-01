# kok v0.9.8 Preview

v0.9.8 是界面改版与 Agent 聚焦改版的合并预览版，不改变数据格式；原生桥接接口保持兼容。

- **界面**：统一偏白浅色与微紫深色主题，精简欢迎页；所有窗口宽度保留短侧栏，「更多」改为三列图标面板，收藏、最近打开、分类前置；导航与工具入口使用离线 Phosphor 双层填充图标；菜单、筛选、排序与面板动效从当前帧续接并尊重减少动效。
- **Agent 首屏**：默认只保留资料、要求、随资料变化的建议操作与发送/停止；技能、用量档位、调用协议和资料范围收进折叠的「高级设置」；选中资料后范围自动按所选执行。
- **本地任务分流**：新增原生只读动作 `agentLocalPlan`，统计、列表/筛选、分类/标签概览和文件类型分布等显式只读请求零模型请求直接回答；生成、比较、建议类请求仍交给模型。
- **片段级读取**：同一长文件最多返回 3 个带页码/章节/字幕时间锚点的命中片段（默认 12、上限 24）；`read_items` 报告每份来源的读取覆盖；每轮目录按所选资料与目标相关候选组织，并在任务内固定。
- **稳定前缀**：工具调用消息的宿主回执与任务状态移到最后一条宿主状态消息，历史保持追加式，便于服务商前缀缓存复用。
- **成果卡片**：任务结束显示状态、使用资料数、来源引用数以及「打开文档 / 查看来源 / 继续修改」；本地任务显示「本地完成 · 未调用模型」；批量整理先汇总将改动项再确认。
- **验证**：**608 项原生检查、16 组桌面套件、交付包复验（15 组套件）与 `npm run check`** 通过。用户授权的同批 30 项真实模型前后对比：请求 **100 → 76（−24%）**、输入 token **566,586 → 431,132（−24%）**、保守费用 ¥2.68 → ¥2.11，通过数 26/30 持平；节省主要来自本地任务分流。两轮共 190 次请求，项目请求总账 359 → 549。
- **发布包**：`kok-0.9.8-win-x64.zip` 由 GitHub Actions 构建，大小与 SHA-256 以 Release 附件及随附 `.sha256` 为准。真实模型评测方法、未达项与边界见[30 项任务评测基线与前后对比](AGENT_TASK_EVAL.md)与[v0.9.8 验证](VALIDATION_0.9.8.md)。

## English

kok v0.9.8 is a combined preview of the interface redesign and the Agent focus redesign, with no data format changes and a compatible native bridge.

- **Interface**: unified near-white and muted purple-dark themes with a minimal welcome screen; a short rail at every width, a three-column launcher with favorites/recent/collections first and pinned offline Phosphor duotone glyphs; menus, filters, sorting and panels continue from the displayed frame and respect reduced motion.
- **Agent first screen**: material, request, material-aware suggested actions and send/stop stay on the surface; skills, usage profiles, protocol and scope move into a collapsed Advanced panel, and selected material becomes the effective scope automatically.
- **Local task routing**: read-only `agentLocalPlan` answers explicit count/list/collection/tag/kind requests with zero model requests; generative, comparison and suggestion requests still go to the model.
- **Fragment-level retrieval**: up to three anchored excerpts per long file (12 snippets by default, max 24); `read_items` reports per-source read coverage; the per-round catalog combines the user's selection with goal-related candidates and stays fixed for the task.
- **Stable prefix**: host receipts and task state moved to a final host status message while history remains append-only for provider prefix caching.
- **Result cards**: status, sources used, citations and open/view-sources/continue-editing on every finished task; local answers show a "completed locally, no model call" card; batch changes are summarized before confirmation.
- **Validation**: **608 native checks, 16 desktop suites, package re-verification (15 suites) and `npm run check`** pass. An authorized before/after run on the same 30 tasks shows requests **100 → 76 (−24%)**, input tokens **566,586 → 431,132 (−24%)** and conservative cost from ¥2.68 to ¥2.11, with 26/30 passed in both rounds; the saving comes mainly from local task routing. Two passes used 190 requests, moving the project request ledger from 359 to 549.
- **Package**: `kok-0.9.8-win-x64.zip` is built by GitHub Actions; use the size and SHA-256 from the Release assets and the accompanying `.sha256`. See the [30-task baseline with the comparison](AGENT_TASK_EVAL.md) and [v0.9.8 validation](VALIDATION_0.9.8.md).

---

## v0.9.5 Preview（历史 / historical）

v0.9.5 是紧凑导航与动效质量修正版，不改变数据格式、原生桥接或 Agent 能力边界。

- **紧凑侧栏**：≤820px 只保留今天、Agent、全部文件、空间和「更多」，加底部设置；图标 20px、点击目标 40px，650×520 下无需滚动。文件类型筛选留在资料库工具栏，避免与侧栏重复。
- **更多浮层**：从顶部栏下方展开，只显示目的地名称和分隔线；私密文件夹、收藏、最近打开、配方、回顾、知识图谱、新建分类仍可达，当前隐藏入口让「更多」保持活动态。
- **字体与焦点**：图标提示、搜索模式、侧栏分组标题与计数提升到 12px；菜单首项用整行高亮而非额外外框；补充 `aria-haspopup`，Escape 返回焦点。
- **动效**：面板在打开动画中途关闭时从当前 transform 继续，不再跳回；导航指示条只动画 transform/opacity（240ms）；减少动效模式下禁用 transition。实测菜单打开最大帧间隔 11ms、预热路由切换 5.7ms。
- **验证**：**542 项原生检查、25 组界面/回归桌面套件、14 组包级门禁**通过；逐页审计 120 张截图。真实模型总账保持 **359**，本版无新增模型调用。

发布 ZIP：`kok-0.9.5-win-x64.zip`，32,298,460 bytes，SHA-256 `1ec5bc7d009e17479a047a2a3b7c1370367b9b643815ae5b7c7dcf5dc18df4fa`（本机候选为 31,912,551 bytes、SHA-256 `fb1fe329ffa05fd91396953ece41955f876da9c668266e771db626f107382e81`）。

[扁平界面说明](FLAT_UI.md) · [界面参考](INTERFACE_REFERENCES.md) · [v0.9.5 验证](VALIDATION_0.9.5.md) · [Roadmap](../ROADMAP.md) · [Changelog](../CHANGELOG.md)

## English

kok v0.9.5 is a compact-navigation and motion-quality release with no data format, native bridge or Agent capability changes.

- **Compact rail**: below 820px only Today, Agent, All files, Spaces and one overflow entry remain, plus Settings; 20px icons and 40px targets, no rail scrollbar at 650×520. File-type filters stay in the library toolbar instead of repeating in the rail.
- **Overflow**: opens below the topbar with destination names and separators only; Private folder, favorites, recent, recipes, review, knowledge graph and New collection remain reachable, and the overflow carries the active state for hidden routes.
- **Type and focus**: tooltips, search mode, sidebar group labels and counts rise to 12px; the menu's first row uses a row highlight rather than an extra outline; `aria-haspopup` and Escape focus restoration are included.
- **Motion**: panel exits continue from the current transform instead of jumping; the navigation indicator animates only transform/opacity (240ms), and reduced-motion disables it. Measured menu-open max frame gap 11ms and warm route transitions 5.7ms.
- **Validation**: **542 native checks, 25 local UI/regression suites and a 14-suite package gate** pass; 120 reviewed screenshots. The real-model ledger stays at **359** with no new model calls.

Published ZIP: `kok-0.9.5-win-x64.zip`, 32,298,460 bytes, SHA-256 `1ec5bc7d009e17479a047a2a3b7c1370367b9b643815ae5b7c7dcf5dc18df4fa` (the local candidate was 31,912,551 bytes, SHA-256 `fb1fe329ffa05fd91396953ece41955f876da9c668266e771db626f107382e81`). See [flat UI notes](FLAT_UI.md), [interface references](INTERFACE_REFERENCES.md) and [v0.9.5 validation](VALIDATION_0.9.5.md).

---

## v0.9.4 Preview（历史）

v0.9.4 是一次完整的界面改版，不改变数据格式、原生桥接或 Agent 能力边界。新版采用统一的扁平视觉语言、分组导航、统一文件卡片/列表与阅读/媒体工具栏，并补齐空状态、创建入口、键盘导航、菜单焦点恢复与减少动效支持。旧 classic/graphite 皮肤偏好保留可读，但统一渲染为扁平界面。

- **布局与导航**：常用目的地保持可见，次要工具收进可记忆展开状态的“更多工具”；文件工具栏统一拥有类型过滤、排序、视图切换和更多工具。
- **文件与阅读**：统一卡片/列表、长文件名截断、媒体徽标与进度、PDF/EPUB/DOCX/Markdown/TXT 预览在窄窗口换行；PDF 工具、Agent 窄窗口、音乐页和设置页逐页复核。
- **可访问性**：键盘文件导航、Space/Shift 选择、Ctrl+F 聚焦搜索、Shift+F10 打开菜单、Escape 清除选择与焦点恢复；`prefers-reduced-motion` 生效。
- **图标**：使用 Lucide 0.468.0 的离线子集（ISC），来源 URL 与 SHA-256、许可文本随源码/包保留。
- **修复**：organization 页英文切换在后台重绘后仍保持英文；documents 私有文件夹步骤与关闭刷新的测试时序稳定化；审查测试创建空间竞态。

验证：**542 项原生检查、25 组界面/回归桌面套件、14 组包级门禁**通过；逐页截图 **120 张**，几何越界与运行时错误均为 **0**。真实模型总账保持 **359**，本版**没有新增真实模型调用**。

发布 ZIP：`kok-0.9.4-win-x64.zip`，32,222,039 bytes，SHA-256 `852314319d475aa21b0eff1287817e3e04b79d036bb952f92b6b43cf76abeebb`（本机候选为 31,832,283 bytes、SHA-256 `6baa9745a90b8afededd76da673180b692799fbb045c561b52a7c5207e53f24d`）。云端发布与下载校验见 [v0.9.4 验证](VALIDATION_0.9.4.md)。

[扁平界面说明](FLAT_UI.md) · [界面验证](INTERFACE_VALIDATION.md) · [v0.9.4 验证](VALIDATION_0.9.4.md) · [Roadmap](../ROADMAP.md) · [Changelog](../CHANGELOG.md)

### English（v0.9.4）

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

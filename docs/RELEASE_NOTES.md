# kok v0.8.2 Preview · 发布草稿

本地开发版本，**尚未发布**。GitHub 当前下载仍为 v0.8.1 Preview。

这版围绕“选定资料 → 执行 → 审核 → 核对结果”改进 Agent，保留视频、照片、音乐、文档、阅读、空间、摘录与编辑功能。

- **有计划，有完成条件**：多步骤任务显示计划，结束前显式核对工具回执；完成、部分完成、需要信息与请求上限分别显示。撤销或本地依据变化后清除旧核对，更新要求后旧计划显示失效。
- **错误可以处理**：按错误类型有限重试，尊重取消与预算；只在接口明确拒绝工具参数时切换 JSON。需要信息时可在同一任务补充，选定资料保持选择顺序。
- **接入本地 MCP**：手动添加可信的 stdio 服务，审核工具定义与参数，每次外部调用单独确认。错误结果可查看并回传模型；外部副作用不提供自动撤销。
- **看得见执行过程**：逐请求 HTTP / 耗时 / token / 费用估算，事件时间线与本地结果入口；诊断 JSON 排除提示词、正文、参数与密钥。
- **验证按同一份构建留证**：桌面记录包含版本、源码与运行文件指纹；包级检查从 ZIP 解压启动。真实模型评测单独记录，不等同于模拟测试通过。

安装需要 Windows 10 / 11 x64、.NET Framework 4.8 和 WebView2；便携包未签名，运行不需 Node.js。MCP 服务及其运行时由你安装，拥有当前账户权限，**没有系统沙箱**。纯净模式与私密文件夹禁用 AI / MCP。

上下文仍按字符节选，无语义压缩；暂无远程 MCP / OAuth、长期记忆、子代理与自主目标验收。工具回执核对不能证明模型的语义判断正确。实现及评测边界见 [Agent 与 MCP](AGENT_0.8.2.md)、[验证记录](VALIDATION_0.8.2.md)。

## English · v0.8.2 draft

Local development version, not published. GitHub downloads remain v0.8.1 Preview. Adds persisted plans and explicit receipt-based review, review invalidation after undo or changed evidence, task-local supplementary input, typed bounded retries, local stdio MCP with individual approvals, repairable invalid calls, preserved external errors, request metrics and redacted diagnostic export. Existing media, reading, editing, Spaces and Clips remain available.

Desktop evidence binds a single run to source/runtime fingerprints. Package checks start the extracted ZIP. Live model evaluations are recorded separately from fixture tests. Windows 10/11 x64, .NET Framework 4.8 and WebView2 are required; the portable ZIP is unsigned. MCP servers use your account permissions without an OS sandbox. No remote MCP/OAuth, semantic compaction, long-term memory, subagents or autonomous semantic verification is claimed. See [feature guide](AGENT_0.8.2.en.md) and [validation](VALIDATION_0.8.2.md).

---

## v0.8.1 Preview · 已发布版本

视频、照片、音乐、文档、电子书放进一个本地仓库，从日常观看与阅读，继续到笔记、摘录和 AI 协作。

## 这版可以怎样用

- **收集与整理**：拖入文件，按类型浏览、分类、收藏和检索；用空间混放同一主题的资料与摘录，切换网格、画布、时间线和图谱；智能集合保存常用筛选条件。
- **看、听、读和编辑**：视频续播、倍速、全屏与外挂字幕；音乐专辑 / 歌手 / 个人音频；照片自由裁剪、调色与图层；文档自动保存、PDF 批注、电子书分页与护眼、PPTX 基础编辑。
- **把内容留下来**：划线、高亮、记想法；保存带来源的文字、时间段和照片区域摘录，加入空间、笔记或演示；双向链接、反向链接与图谱帮助回看关系。
- **按需使用 AI**：文档双语对照、选文解释、多文件对比、笔记回顾和字幕工具。Agent 在指定范围内检索、阅读和提出修改，展示来源与审核卡片；内置指令技能与用量限制。
- **把步骤接起来**：可编辑配方串联读取、摘要、笔记和演示；“今天”帮助续读续播，“活动”显示后台任务，“历史”查看可审核、可撤销的修改。
- **选择自己的体验**：纯净 / 智能模式、资料库内的私密文件夹、中英文、主题与玻璃配色、内置字体、模块开关和独立预览窗口。

本版还修复了私密移动的可追踪记录残留、检查器笔记保存同步、长列表检索范围、工具栏焦点与大小窗口交互。模块、SQLite 和任务机制的实际覆盖见[实现范围](WAREHOUSE_0.8.1.md)。

## 安装与范围

下载 `kok-0.8.1-win-x64.zip` 和随附 SHA-256，核对后完整解压并运行 `kok.exe`。需要 Windows 10 / 11 x64、.NET Framework 4.8 和 WebView2；运行不需要 Node.js。升级前备份自己的 `data` 与原文件。

便携包未签名。播放依赖实际编码；扫描 PDF 需外部 OCR，导入文档与 PPTX 的基础编辑不保证复杂版式完整还原。音视频导出和本地语音识别需额外组件，AI 服务及网站下载有各自兼容范围。私密文件夹加密应用记录，**原文件、此前独立 AI 历史和导出不追溯加密**。

本地原生 277 项通过，已完成相关隔离桌面验证；发布前两组 Windows 云端构建、测试和包审计通过。桌面测试尚未进入 CI，真实语音识别未在本轮运行。`--debug` 开启本机 9225 调试端口，密码失败限速在重启后重置。详细范围见[验证记录](VALIDATION_0.8.1.md)。

[从使用指南开始](GUIDE.md) · [格式支持](FORMATS.md) · [AI 配置](AI_PROVIDERS.md) · [私密与备份](MODES.md)

## English

A local home for videos, photos, music, documents and books, connecting everyday playback and reading to notes, Clips and optional AI.

- **Collect and organize**: drop sources, browse/classify/search, gather files and Clips in Spaces, and reuse filters as smart collections.
- **Watch, listen, read and edit**: video controls/subtitles, albums and personal audio, crop/color/layer photo editing, document auto-save, PDF annotations, comfortable book layouts and basic PPTX editing.
- **Keep useful content**: source-linked passages, time ranges and photo regions; thoughts, note links, backlinks and graphs; references in notes and slides.
- **Use AI by choice**: bilingual translation, selected-text help, source comparison, note review and subtitle tools. Agent uses scoped sources and reviewable proposals, with instruction skills and usage limits.
- **Connect steps**: editable Recipes join reading, summaries, notes and slides; Today, Activity and History bring resume, progress and reviewable changes together.
- **Choose your experience**: Pure/Smart, an in-library private folder, Chinese/English, themes/glass, bundled fonts, module switches and independent previews.

Includes targeted private-transfer cleanup, synchronized notes, complete search candidates and viewer/focus/layout fixes. See [implementation scope](WAREHOUSE_0.8.1.en.md).

Extract the full ZIP and run `kok.exe`, checking the accompanying SHA-256. Requires Windows 10/11 x64, .NET Framework 4.8 and WebView2; Node.js is not a runtime dependency. Back up your data and originals before upgrading. The portable package is unsigned. Codec, OCR, complex document/slide layouts, optional components and provider/site compatibility have limits. The private folder does not retroactively encrypt originals, independent AI history or exports.

Passed 277 local native checks, relevant isolated desktop suites and both release Windows build/test/audit jobs. Interactive desktop suites are outside CI; real speech recognition was not run. `--debug` opens local port 9225 and password backoff resets on restart. See [validation](VALIDATION_0.8.1.md#english).

[User guide](GUIDE.en.md) · [Formats](FORMATS.en.md) · [AI setup](AI_PROVIDERS.en.md) · [Privacy and backups](MODES.en.md)

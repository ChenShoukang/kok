# kok 文档

[项目首页](../README.md) · [English](README.en.md)

第一次使用先读[使用指南](GUIDE.md)。这里按你想完成的事找说明；版本记录和开发实现放在后面。

## 按功能查找

| 想做什么 | 从这里开始 |
| --- | --- |
| 导入文件，认识资料库、空间、摘录与智能集合 | [当前使用指南](GUIDE.md) |
| 离线使用、切换模式、建立私密文件夹和备份 | [模式、私密与数据边界](MODES.md) |
| 配置 DeepSeek 等服务或本机模型 | [AI 服务配置](AI_PROVIDERS.md) |
| 给 Agent 任务，限定范围并审核修改 | [Agent 功能](AGENT_0.7.2.md) · [来源与修改审核](AGENT_INTERACTIONS.md) · [任务与交接边界](AGENT_BOUNDARIES.md) |
| 使用任务计划、执行追踪或本地 MCP 工具（开发中） | [v0.8.2 Agent 与 MCP](AGENT_0.8.2.md) · [本地验证与可选模型评测](VALIDATION_0.8.2.md) |
| 使用上下文压缩、长期记忆、子代理或隔离计算 | [Agent 运行能力](AGENT_RUNTIME_0.8.2.md) · [运行能力验证](VALIDATION_RUNTIME_0.8.2.md) |
| 查文件、媒体编码、字幕和导出支持 | [格式兼容性](FORMATS.md) |
| 写笔记、双向链接和查看知识图谱 | [笔记与图谱](KNOWLEDGE_0.6.1.md) |
| 对照翻译文档、翻译字幕或生成语音字幕 | [翻译与字幕](TRANSLATION_CAPTIONS_0.5.1.md) |
| 多份资料对比、笔记回顾、字幕目录 | [资料分析工具](STUDY_TOOLS_0.5.1.md) |
| 照片、音视频、PDF 与正文编辑 | [编辑与音乐整理](EDITING_MUSIC_0.5.1.md) |
| 扫描指定文件夹，归类或询问资料内容 | [智能整理与内容问答](SMART_LIBRARY.md) |
| 安装或使用遇到问题 | [支持说明](../SUPPORT.md) · [问题反馈](https://github.com/ChenShoukang/kok/issues/new/choose) |

## 版本、验证与开发

| 文档 | 内容 |
| --- | --- |
| [v0.8.1 实现范围](WAREHOUSE_0.8.1.md) | 当前覆盖与未完成的整体验收，区分功能、规划与兼容层 |
| [验证记录](VALIDATION_0.8.1.md) | 原生 / 桌面证据、复现方式与未验证边界 |
| [开发与测试](DEVELOPMENT.md) | 构建、隔离测试、代码目录 |
| [模块契约](MODULES_0.8.1.md) | 统一入口、权限、能力、历史和后台任务 |
| [架构与任务恢复](ARCHITECTURE_0.8.1.md) | 注册表、Agent 事件、恢复和交接 |
| [发布准备记录](PUBLIC_PREVIEW_0.8.1.md) | 隐私修复与公开预览门槛 |
| [更新记录](../CHANGELOG.md) · [发布草稿与上次发布说明](RELEASE_NOTES.md) | 版本变化与 Preview 范围 |
| [路线图](../ROADMAP.md) | 后续目标，未实现部分不等于现有功能 |
| [安全报告](../SECURITY.md) · [贡献说明](../CONTRIBUTING.md) | 漏洞反馈、参与开发和社区约定 |

<details>
<summary>历史版本与设计记录</summary>

这些文档保留功能演变、实现和设计参考；旧文中的版本状态以对应时期为准，日常使用以当前指南为入口。

| 文档 | 内容 |
| --- | --- |
| [v0.7.1 图谱与拟物资料库](WORKSPACE_0.7.1.md) | 范围、内置 AI 任务、用量、格式图标、动画与开源参考 |
| [v0.6.2 石墨界面](GRAPHITE_0.6.2.md) | 得意黑默认字体、深浅色、宽侧栏、媒体与小窗口适配 |
| [v0.6.1 笔记与知识图谱](KNOWLEDGE_0.6.1.md) | 双向链接、反向链接、标签、大纲、待办、模板与本地图谱 |
| [v0.6.1 新建文档](DOCUMENTS_0.6.1.md) | Markdown / DOCX / TXT、自动保存、源码与预览、重名与冲突恢复 |
| [v0.6.1 阅读与照片编辑](WORKSPACE_CLARITY_0.6.1.md) | 安静工具栏、边缘与键盘唤回、照片配色与字体、小窗口编辑 |
| [v0.6.1 动画与窗口](MOTION_0.6.1.md) | 局部动画、快速反向操作、减少动态效果与原生窗口修正 |
| [阅读布局与流畅性](READING_FLOW_0.5.1.md) | 安静阅读、单双页、滚轮、快速缩放、开窗和完整收藏按钮 |
| [阅读与窗口修订](READING_RENEWAL_0.5.1.md) | 真实目录、默认分页、护眼与字体、持久双语对照、大小窗口与全屏 |
| [翻译、字幕与精准标记](TRANSLATION_CAPTIONS_0.5.1.md) | 原格式导出、左右对照、本地 / API 字幕、选区微调与缓动 |
| [编辑与音乐整理](EDITING_MUSIC_0.5.1.md) | 照片图层、音视频片段、正文与 PDF 编辑、音乐标签和 AI 整理 |
| [三项 AI 工具](STUDY_TOOLS_0.5.1.md) | 多文件对比、笔记关联与字幕目录 |
| [内置中文字体](FONT_CANDIDATES.md) | 得意黑与霞鹜文楷轻便版 |
| [v0.5.1 设计与 AI](DESIGN_0.5.1.md) | 预览版、开源参考与验证范围 |
| [v0.4.1 预览版](PREVIEW_0.4.1.md) | 统一配色、阅读工具与视频下载 |

[历史发布验证与通用排查](TESTING.md)

</details>

`screenshots/` 使用原创示例或隔离合成资料；`assets/` 保存品牌与功能图片。`compatibility-results.json` 是特定版本 / 内核的格式实测记录，不代表所有编码均可播放。

# v0.5.1 · 三项 AI 工具

v0.5.1 预览版使用现有的 AI 服务配置；纯净模式和私密文件夹禁止发起这些 AI 请求。

## 多文件对比

资料库右上角 **AI 工具 → 多文件对比**。至少选择两份资料，选择上限默认 5 份，可以调整至 50 份并保存在设置中。支持内置可读取的文档、电子书及已有视频文字字幕。

点击「预览引用」只在本地读取内容；点击「生成分析」才向已配置 API 发送。生成共同观点、差异与依据，点击引用可返回对应文件、页码、章节、划线或字幕时间。「返回分析」可回到原报告。

每份资料分配引用预算，总引用正文约 30,000 字符，最多 60 个来源。PDF 最多读取 40 个分布页面；长文与长字幕会选取段落并标明「基于节选」。不支持内置提取的格式、没有字幕的视频或需要 OCR 的 PDF 会提示，避免默默跳过选中文件。

## 笔记回顾与关联

在 **AI 工具 → 笔记回顾**，或阅读器内容助手中的「回顾笔记」，选择当前文件、当前分类或所有笔记里的资料。只使用选中的划线、批注想法和笔记，不自动读取整本书。

本地关键词匹配会列出最多五份相关旧笔记，由用户选择加入。回顾要求区分「原文划线 / 个人想法 / 待确认问题」；生成内容仍需按来源核对。当前关联采用关键词，并非向量语义检索。

对比和回顾报告保存在第一份所选资料的本地索引内，每份最多八条。同一份引用、语言和模型配置下的重复分析复用结果；可在「已保存分析」中重开，也可复制、保存到笔记或导出 Markdown。直接移除资料会影响相应历史与来源跳转。

## 视频字幕目录

视频播放控制条的目录按钮，或 **AI → 字幕目录 → 生成目录**。使用当前选择的原始文字字幕；关闭字幕显示时仍可使用已加载原始字幕生成目录。

按字幕分段，生成每段的小标题和一句摘要，最多 16 段。点击跳转到该段的字幕起点，播放时标记当前段。时间由程序从字幕分段确定，模型仅返回对应段的标题和摘要。长段落会取样，并标明「字幕节选」。

目录保存在本地，重开可用。重新生成时按字幕内容、模型配置和语言检查缓存。无字幕时需先载入字幕；本功能未增加语音识别、画面识别或自动识别影片真实叙事章节。

## 检查

隔离、静音的桌面检查：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-desktop.ps1 -Suites study-tools
```

覆盖六类来源、可调数量、无请求的引用预览、跨文件跳转、保存与重开、相关笔记、缓存、取消、字幕时间校验、无效模型输出、英文小窗口及纯净模式拦截。真实 DeepSeek 的自编短资料检查另见 [内容验证](AI_VALIDATION_0.5.1.md)。未对整部影片、全部 Office 格式或大量真实资料作兼容性保证。

---

English: Open **AI tools** in the library to compare 2–50 selected readable files or review selected notes. The assistant also offers **Review notes** and **Video chapters**. Reports retain cross-file citations and are stored locally with preview, copy, note-save and Markdown export. Related notes use local keyword matching. Context is bounded and long PDFs are sampled across at most 40 pages. Chapters summarize up to 16 subtitle segments; their times come from subtitles, not generated timestamps. Pure mode and private folders block these AI requests. No speech or visual recognition is added.

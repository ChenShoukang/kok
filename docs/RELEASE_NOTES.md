# kok v0.6.2-r2 · 石墨修订预览版

- **界面更一致**：统一默认配色与材质，清除关闭自定义配色后的残留；保留默认得意黑、独立阅读字体和纸色。
- **动画更稳定**：统一表面动画，拖动缩放窗口、切到后台或开启减少动态效果时直接落定，避免加载后重复入场。
- **保存更可靠**：页面卡住时退出保存有 5 秒超时；失败后默认保留窗口与内容，恢复后可重试。
- **范围更清楚**：私密文件夹继承内置字体，明确原文件未加密；编辑器标明 PDF 图像输出、DOCX / EPUB 简洁排版的限制。
- 保留知识图谱、双向链接、新建文档、自动保存、影音编辑、翻译、字幕与可选 AI。

本地通过 **132 项原生检查、7 组隔离静音界面测试**，包括真实退出超时、拒绝关闭、文字保留与恢复退出，并检查脚本、源码、Git 历史和包隐私。云端 Windows 2022 / 2025 构建见 [Actions](https://github.com/ChenShoukang/kok/actions)。

**安装**：下载 ZIP 和 SHA-256，完整解压后运行 kok.exe。需要 Windows 10 / 11 x64、.NET Framework 4.8 与 WebView2。升级前备份 data；便携包未签名，不包含个人资料库、模型配置、密钥或语音模型。应用版本保持 v0.6.2，本次使用独立的 r2 发布标签。

**仍为预览版**：播放依赖 WebView2 编码支持；DOCX 编辑重建简洁正文，扫描 PDF 需外部 OCR，PDF 标注页按图像导出。语音 / 媒体导出需要按需组件或所选服务。私密文件夹加密索引、笔记与封面，原文件和既有导出不加密。

[版本说明](https://github.com/ChenShoukang/kok/blob/main/docs/GRAPHITE_0.6.2.md) · [格式](https://github.com/ChenShoukang/kok/blob/main/docs/FORMATS.md)

## English

Unified palettes, one surface-motion owner and interruption handling for native gestures, hidden pages and reduced motion. Renderer snapshots now time out after five seconds; failed exit saves default to keeping the window and edits open for retry. Bundled fonts remain available in the private folder; encryption and editor-export scope are stated clearly.

Retains notes, graphs, document creation, auto-save, media editing, translation, captions and optional AI. Locally validated with **132 native checks and seven isolated silent desktop suites**, including actual timeout/default No/text preservation/retry, plus script and privacy checks. See Actions for the two Windows runner builds.

Extract the full ZIP and run kok.exe. Requires Windows 10/11 x64, .NET Framework 4.8 and WebView2. Back up existing data and verify SHA-256. The unsigned package excludes personal data, model configuration, credentials and speech models. The app remains v0.6.2; this revision uses its own r2 release tag.

Still a preview: playback depends on codecs; imported DOCX/EPUB editing uses simplified formatting, annotated PDF pages export as images and scans need OCR. Speech/media exports need optional components or a provider. Private indexes/notes/covers are encrypted; original files and existing exports are not.

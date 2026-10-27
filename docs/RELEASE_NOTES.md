# kok v0.6.2 · Graphite Preview

- **石墨界面**：深色石墨 / 浅色霜面、冰蓝反馈、宽侧栏与小窗口图标栏、标题封面与唱片。
- **默认得意黑**：内置字体无需下载，保留手动字体、阅读纸色和独立正文设置；修复界面样式覆盖字体的问题。
- **媒体与动画**：统一视频、照片和音乐播放条；可见提示灯、阅读滑动指示、单套可中断弹窗动画，优化快速移动、缩放和反向操作。
- **笔记与文档**：本地知识图谱、双向链接、反向链接、标签、大纲、待办和模板；新建 Markdown / Word（DOCX）/ TXT、自动保存与冲突恢复。
- 保留影音编辑、字幕与双语翻译、AI 问答与整理、纯净 / 智能模式及私密文件夹。

本地通过 127 项原生检查和六组隔离静音桌面检查，并完成脚本、依赖、源码、Git 历史和打包隐私检查。云端 Windows 2022 / 2025 构建结果见 [Actions](https://github.com/ChenShoukang/kok/actions)。

**安装**：下载 ZIP 与 SHA-256，完整解压后运行 kok.exe。需要 Windows 10 / 11 x64、.NET Framework 4.8 与 WebView2。升级前备份原 data，软件包未签名，不包含个人资料库、模型配置、密钥或语音模型。

**范围**：播放受实际编码支持限制；DOCX 编辑重建简洁正文，扫描 PDF 需外部 OCR。语音识别和媒体导出需要按需组件或所选服务；AI 费用由服务商收取。私密文件夹加密索引 / 笔记 / 封面，原文件和既有导出不加密。

[版本说明](https://github.com/ChenShoukang/kok/blob/main/docs/GRAPHITE_0.6.2.md) · [格式](https://github.com/ChenShoukang/kok/blob/main/docs/FORMATS.md)

## English

Graphite/frost surfaces with ice-blue feedback, a responsive labelled sidebar and typographic/record covers. Bundled Smiley Sans is the default, with saved font/paper preferences retained. Video/photo/audio chrome, notification indicators, moving reading selection and interruptible motion are unified.

Includes local knowledge graphs, note-link completion/backlinks, tags, outline, tasks, templates and managed Markdown/DOCX/TXT creation with auto-save and conflict recovery. Existing editors, bilingual translation, captions, optional AI and the private folder remain available.

Validated locally with 127 native checks and six isolated silent desktop suites, plus script, dependency, source/history and package privacy checks. See Actions for the two Windows runner builds.

Extract the full ZIP and run kok.exe. Requires Windows 10/11 x64, .NET Framework 4.8 and WebView2. Verify SHA-256 and back up your existing data before upgrading. The unsigned package excludes personal libraries, model configuration, credentials and speech models.

Playback depends on codecs; DOCX editing rebuilds simple body text and scanned PDFs need OCR. Speech/media export needs optional components or a selected provider; provider charges may apply. Private indexes/notes/covers are encrypted, while originals and existing exports are not.

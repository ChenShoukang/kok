# kok v0.5.1 · 预览版 / Preview

视频、照片、音乐、文档与读书，一处管理。

- **阅读重做**：真实 EPUB NAV / NCX 与 Word 标题目录、默认收起次要栏、单页 / 自适应双页 / 上下滚动、滚轮翻页、独立正文排版与护眼配色、书签选中反馈、精准标记与选词翻译 / AI 解释。
- **窗口与界面**：独立多窗口、双击空白退到后面、无边框全屏；统一媒体工具栏、固定收藏及窗口按钮、窄窗命令菜单、同步开窗与快速缩放重排优化。
- **翻译与字幕**：快速并发、分段缓存、对齐双语和双向滚动，本机保存译文与标记，原格式副本导出；可选本地 Whisper / 独立语音 API 生成字幕及 SRT 导出。
- **编辑与音乐**：照片调色、自由裁剪和标注；音视频片段与时间线；正文 / PDF 编辑；专辑、歌手和个人音频整理，联网候选匹配与 AI 元数据预览。
- 保留纯净 / 智能模式、中英文、可选 AI、分类拖入及需密码确认的私密文件夹；内置得意黑和霞鹜文楷轻便版。

**运行**：下载 kok-0.5.1-win-x64.zip，核对随附 SHA-256，完整解压并运行 kok.exe。需要 Windows 10 / 11 x64、.NET Framework 4.8 与 WebView2。升级前备份旧版 data。软件包未签名，不包含个人资料库、API 配置、私密数据或语音模型。

**范围**：媒体解码仍取决于 WebView2；扫描 PDF 需先 OCR。AI 使用所选服务，长内容可能节选；语音识别需额外组件或独立服务。译文批注主要保存在本机，不会自动嵌入所有导出格式。私密文件夹加密索引 / 笔记 / 封面，原文件和既有导出、共享缓存不加密。网站解析受网络、验证、登录与权限影响。

[阅读与窗口](https://github.com/ChenShoukang/kok/blob/main/docs/READING_FLOW_0.5.1.md) · [翻译与字幕](https://github.com/ChenShoukang/kok/blob/main/docs/TRANSLATION_CAPTIONS_0.5.1.md) · [编辑与音乐](https://github.com/ChenShoukang/kok/blob/main/docs/EDITING_MUSIC_0.5.1.md)

## English

This preview adds quiet single/two-page/scroll reading, real contents, wheel paging, eye comfort, independent text fonts, precise marks and selected-text AI actions. Independent native viewers gain coordinated opening, resize scheduling, fixed favorites/window buttons and compact command menus.

Translation offers fast bounded concurrency, segment caches, aligned paired text, two-way scrolling, saved local results/marks and matching-format copies. Optional local Whisper or a separate timestamped speech API generates captions. Photo/media/document editors, organized music views, matching candidates and AI metadata previews are included.

Download kok-0.5.1-win-x64.zip, verify SHA-256, extract completely and run kok.exe. Windows 10/11 x64, .NET Framework 4.8 and WebView2 are required. Back up existing data before upgrading. The unsigned package excludes personal libraries, API configuration, private data and speech models.

Playback depends on WebView2 codecs; scanned PDFs need OCR. AI may use excerpts. Speech needs optional components or a separate provider. Comparison marks remain local rather than being embedded in every exported format. The private folder encrypts indexes/notes/covers, not originals, prior exports or shared caches. Website availability depends on network, verification, sign-in and permissions.

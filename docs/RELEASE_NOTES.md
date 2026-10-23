## v0.3.1 · 纯净 / 智能与私密文件夹

- 首次只选择纯净 / 智能模式；私密功能放在资料库内的文件夹中，保留统一界面。
- 纯净模式保留离线基础功能，原生层阻止 AI；智能模式首次说明内容发送与接口配置。
- 新增密码保护的私密文件夹：加密索引、笔记与封面，离开窗口、最小化或闲置自动锁定，支持加密备份。原始文件仍在原位置，不会被加密。
- AI 配置加入 12 个入口、模型列表读取、本机免密服务和输出参数选择；密钥以 Windows DPAPI 本机加密，地址变更不复用旧密钥。
- 新增指定文件夹扫描、AI 分类与标签预览、时间 / 类型 / 自定义分组，以及视频、文档和读书的流式 AI 内容对话。
- 横竖屏媒体自适应；流式消息只刷新当前回复；补齐文案、导出原文件保护、任务取消与锁定清理。
- 重新检查媒体、阅读批注、照片编辑、窗口适配、菜单和模式切换；发布包按明确清单构建并审计密钥与私密数据。

详见 [中文说明](https://github.com/ChenShoukang/kok/blob/main/README.md)。解压便携包运行 kok.exe；保留附带文件。Windows 10 / 11 x64，.NET Framework 4.8 与 WebView2。源码与安装包不含个人 API 配置、资料库或私密缓存。

## v0.3.1 · Pure / Smart and a private folder

- First-run Pure / Smart selector, with a password-protected folder inside the ordinary library and consistent glass UI.
- Pure keeps offline basics and blocks AI natively; Smart explains content sharing and model setup on first use.
- Password-protected private folder inside the library with encrypted index, notes and covers; locks on deactivation, minimize or idle and exports encrypted backups. Original source files remain in place and are not encrypted.
- AI setup adds 12 entries, model discovery, local no-key servers and output-limit compatibility. Credentials use Windows DPAPI and are never reused silently across changed endpoints.
- Selected-folder scanning, AI category/tag previews, timeline/type/custom groups and streamed file chat for videos, documents and books.
- Orientation-aware previews, efficient streaming updates, translation fixes, original-file export protection, cancellation and private-content cleanup.
- Reviewed media, annotations, photo edits, responsive layouts, menus and modes; release packages use an allowlist and audit credential patterns and private files.

See the [English guide](https://github.com/ChenShoukang/kok/blob/main/README.en.md). Extract the portable ZIP and run kok.exe with its accompanying files. Requires Windows 10 / 11 x64, .NET Framework 4.8 and WebView2. Source and packages exclude personal API configuration, libraries and private caches.

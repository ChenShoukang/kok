# 开发与测试

[English](DEVELOPMENT.en.md) · [文档目录](README.md)

## 目录

| 路径 | 用途 |
| --- | --- |
| `src/` | WinForms / WebView2 宿主、文件访问、资料库、字幕和翻译 |
| `ui/` | 玻璃界面、选择菜单、媒体与阅读工作区、中英文文案 |
| `tests/` | 原生自检与通过真实桌面窗口运行的界面验证 |
| `tools/` | 依赖准备、图标与示例生成、格式报告和发布脚本 |
| `samples/` | 可再分发的原创示例 |
| `docs/screenshots/` | 文档截图 |
| `dist/` | 本机构建与便携包，Git 忽略 |
| `test-output/` | 隔离测试资料库与临时证据，Git 忽略 |

界面脚本使用同一个本地桥接与状态对象，按 `ui/index.html` 的顺序加载。新增选择器保留原生 `select` 的值、焦点和 `change` 事件，只替换下拉菜单。视频封面使用一个静音、暂停的视频元素顺序提取，写入 `data/thumbs/`；预览或播放时让出资源。

## 构建

Windows x64、Node.js 24、.NET Framework 4.8 和 WebView2 运行时：

```powershell
npm ci --omit=optional
npm run check
.\build.ps1 -Test
.\package.ps1
```

SDK 版本与下载校验由 `build.ps1` 固定。PDF.js 的本地资源由安装和构建准备，不需用户额外联网加载。构建不需要 AI API 密钥。

## 界面验证

先启动独立资料库，并同时启用调试与内核静音标志：

```powershell
Start-Process -FilePath '.\dist\kok\kok.exe' -ArgumentList @('--debug','--test-silent','--data-dir',"$PWD\test-output\ui-data") -WindowStyle Hidden
npm run test:ui
npm run test:menus-covers
npm run test:refinements
npm run test:video-layout
npm run test:i18n
```

`tests/browser.cjs` 检查内核静音状态，不满足条件就拒绝媒体测试。普通启动不启用测试静音。界面测试会改动隔离资料库，勿指向日常 `data/`。照片另存与视频封面测试检查原文件保持不变。

`test:large-media` 可接一个本地视频路径，验证大文件范围读取与跳转；它不上传原片。翻译测试使用本地模拟服务。

## 笔记与图谱定向检查

`powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-desktop.ps1 -Suites knowledge` 使用隔离普通 / 私密资料库，验证真实文件保存、补全与反向链接、任务、模板、图谱筛选、拖动缩放、空图、小窗、原生快速缩放、减少动态效果、500 节点后台布局与稳定后停算。禁止外部请求，不使用真实 API 配置。`-Suites documents` 检查文档保存、关闭与冲突恢复。

## 打包与发布

`package.ps1` 从明确的文件清单和当前资源目录生成 `dist/kok-<版本>-win-x64.zip` 及 SHA-256 文件，排除资料库、密钥、调试二进制和临时测试内容。

v0.6.2 已启用 `release.publish: true`。推送 main 后，两个 Windows 环境通过构建、测试与审计才发布预览包。已对相同原生源码完成检查后，可用 `package.ps1 -SkipTests` 重新打包；暂存目录与 ZIP 隐私审计仍执行。

推送 `main` 后，[Windows 构建工作流](https://github.com/ChenShoukang/kok/blob/main/.github/workflows/build.yml) 在 Windows Server 2022 / 2025 上执行脚本、依赖、源码及完整 Git 历史检查，再运行原生测试与打包。构建只读仓库；独立发布任务等待两个环境通过后，才使用 `contents: write` 和内置 `GITHUB_TOKEN`。后续版本默认发布为 Preview；已有版本不会被静默替换。服务器检查不能替代 Windows 10 / 11 新电脑验证，范围见[发布验证](TESTING.md)。

`npm run audit:dependencies` 查询 npm 官方漏洞库，任何等级的已知漏洞都会使检查失败；网络失败也不会按通过处理。`npm run audit:history` 扫描本地所有可达提交的凭据模式和私密文件路径，报告不输出匹配值。CI 使用完整历史；手动检查前应确认仓库不是浅克隆。`node tests/audit-history.cjs` 验证已从当前文件删除、但仍留在历史中的测试凭据仍会被发现。源码与 ZIP 检查继续由 `tools/audit-release.ps1` 执行。

## v0.3.1 完整桌面检查

`tools/test-desktop.ps1` 为 16 组检查分别启动独立资料库，始终使用 `--debug --test-silent`。覆盖基础功能、翻译、中英文、动画、拖入、阅读与媒体工作区、无边框视频、照片编辑与快捷键、菜单和封面、实际解码、智能整理、内容对话、私密密码与自动锁定、模式切换、内核崩溃恢复。结果只写入被 Git 忽略的 `test-output/`。

```powershell
.\tools\test-desktop.ps1
.\tools\test-desktop.ps1 -Suites private-space,app-modes
.\tools\audit-release.ps1
.\tools\audit-release.ps1 -Archive .\dist\kok-0.3.1-win-x64.zip
```

原生测试另覆盖私密数据认证加密与服务密钥隔离。AI 测试只访问本地模拟服务。FFmpeg 仅生成测试文件，不随包分发。打包会检查暂存目录和最终 ZIP，发现密钥模式时阻止构建且不打印密钥值。

## 内置下载定向检查

`powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-desktop.ps1 -Suites native-downloads` 使用空白隔离资料库和本机原创媒体 HTTP 服务，不安装外部组件、不调用模型。续传依据 [HTTP Range / If-Range](https://www.rfc-editor.org/rfc/rfc9110.html#name-if-range) 校验；[ResponseHeadersRead](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption?view=netframework-4.8) 后对正文读取另设超时和取消。具体实现见 `src/DirectDownloads.cs`。

媒体组件与分段下载定向检查：`powershell -NoProfile -Command "& ./tools/test-desktop.ps1 -Suites media-downloads"`。使用固定版本真实组件及本机生成的 HLS / DASH 点播清单；不播放声音、不调用 AI。`src/StreamDownloads.cs` 通过 FFmpeg 解复用和流复制，不包含视频网站提取器。组件来源为 FFmpeg 官网列出的 Gyan 构建，固定校验压缩包和两个程序。


`powershell -NoProfile -Command "& ./tools/test-desktop.ps1 -Suites kok-downloads"` 使用本机原创素材检查网页解析、资源 / 画质选择、字幕、音频导出、HLS / DASH、旧组件清理与界面。

网页识别参考 [HTML 媒体元素](https://html.spec.whatwg.org/multipage/media.html)、[Open Graph](https://ogp.me/) 与 [VideoObject 元数据](https://schema.org/VideoObject)，实现见 `src/MediaResolver.cs`；不执行网页 JavaScript。

站点适配：`powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-desktop.ps1 -Suites site-adapters`。使用原创本机媒体与离线 HTTP 模拟验证三站数据、分轨和加密会话；实站只读取公开元数据。设 `KOK_SKIP_LIVE_SITES=1` 可跳过外部检查。YouTube 依赖由 `tools/youtube-dependencies.json` 固定，构建校验 NuGet 和 DLL SHA-256；新增许可位于 `docs/licenses/`。

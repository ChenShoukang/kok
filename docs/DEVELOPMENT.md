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

## 打包与发布

`package.ps1` 从明确的文件清单和当前资源目录生成 `dist/kok-<版本>-win-x64.zip` 及 SHA-256 文件，排除资料库、密钥、调试二进制和临时测试内容。

推送 `main` 后，[Windows 构建工作流](https://github.com/ChenShoukang/kok/blob/main/.github/workflows/build.yml) 执行检查与原生测试，再读取 `package.json` 和 [发布说明](RELEASE_NOTES.md) 创建对应的 GitHub Release。`tools/publish-release.ps1` 仅在 Actions 中读取内置 `GITHUB_TOKEN`。

## v0.3.1 完整桌面检查

`tools/test-desktop.ps1` 为 16 组检查分别启动独立资料库，始终使用 `--debug --test-silent`。覆盖基础功能、翻译、中英文、动画、拖入、阅读与媒体工作区、无边框视频、照片编辑与快捷键、菜单和封面、实际解码、智能整理、内容对话、私密密码与自动锁定、模式切换、内核崩溃恢复。结果只写入被 Git 忽略的 `test-output/`。

```powershell
.\tools\test-desktop.ps1
.\tools\test-desktop.ps1 -Suites private-space,app-modes
.\tools\audit-release.ps1
.\tools\audit-release.ps1 -Archive .\dist\kok-0.3.1-win-x64.zip
```

原生测试另覆盖私密数据认证加密与服务密钥隔离。AI 测试只访问本地模拟服务。FFmpeg 仅生成测试文件，不随包分发。打包会检查暂存目录和最终 ZIP，发现密钥模式时阻止构建且不打印密钥值。

# kok · 影音与阅读资料库

**视频、照片、音乐、文档、电子书，一处收藏。** 轻量 Windows 桌面软件，支持离线使用、可选 AI 和密码保护的私密文件夹。

[下载 v0.4.1 预览版](https://github.com/ChenShoukang/kok/releases/tag/v0.4.1) · [English](README.en.md) · [格式兼容性](docs/FORMATS.md) · [更新记录](CHANGELOG.md)

**v0.4.1 预览版**：重做扁平布局、轻圆角、Fluent Flat 图标与玻璃浮层，内置得意黑；新增配色、阅读工具与 kok 自主下载。详细功能与限制见 [v0.4.1 说明](docs/PREVIEW_0.4.1.md)，验证范围见[发布验证](docs/TESTING.md)。

## 开始使用

1. 下载 Release 中的 `kok-0.4.1-win-x64.zip`，解压后运行 `kok.exe`，保留附带文件。
2. 选择**纯净模式**或**智能模式**，之后可在设置切换。
3. 拖入文件或文件夹；也可以点击“导入文件”或体验原创示例。

需要 **Windows 10 / 11 x64、.NET Framework 4.8、[WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)**。运行软件无需 Node.js 或 .NET SDK。便携包未签名，附 SHA-256 校验文件。

<picture>
  <source media="(max-width: 600px)" srcset="docs/assets/features-zh-mobile.png">
  <img src="docs/assets/features-zh.png" alt="kok 功能总览：视频、照片、音乐、文档与读书；支持纯净和智能模式、可选 AI 与私密文件夹。">
</picture>

## 功能一览

| 类别 | 可以做什么 |
| --- | --- |
| 视频 | 无边框全屏、续播、0.5×–3× 倍速、静音片段加速；外挂字幕自动匹配与手动加载、封面、右键属性、可配置快捷键、横竖屏适配；内核支持时可用画中画 |
| 照片 | 滚轮缩放、放大后拖动、旋转与幻灯片；滤镜、调色、暗角、拖拽自由裁剪 / 八向调整、固定比例、翻转、原图对比、撤销 / 重做，另存 PNG / JPEG |
| 音乐 | 专辑唱片架、专辑详情、专辑名与歌手编辑、播放队列；持续播放、续播、随机、循环、进度和音量控制 |
| 文档 | PDF 缩放、选字、翻页、目录与全文搜索；DOCX / 文本阅读、书签、排版；划线、高亮、插入文字、批注与 HTML / Markdown 导出 |
| 读书 | EPUB 目录、TXT 分节、章节筛选、全文搜索、书签、滚动 / 分页、字号 / 行距 / 字距 / 段落留白 / 正文宽度、专注与续读；保留划线和想法 |
| 资料库 | 文件 / 文件夹拖入、类型识别、重复跳过；分类、收藏、最近打开、搜索、网格 / 列表与排序 |
| 智能模式的本地整理 | 时间 / 类型 / 自定义分组、批量分类；扫描指定文件夹发现新文件，支持暂停与恢复，扫描和本地分组无需调用 AI |
| 外观 | 扁平页面、轻圆角、小方形 Fluent Flat 分类图标与封面，就近操作；中性白 / 石墨灰、可调背景与玻璃材质；内置得意黑，保留中英文、字体切换 / 导入、小窗口和减少动态效果 |
| 视频下载 | B 站 / YouTube / 抖音专用解析、通用网页与直链、资源 / 画质选择、仅音频和字幕；站点登录窗口、加密会话及 cookies.txt 导入，B 站 / 抖音可手动网页辅助解析；队列、续传、重试与入库。FFmpeg / FFprobe 按需安装，支持分轨及 M3U8 / DASH 合并 |
| AI 辅助 | 字幕、文档与电子书翻译，双语对照、缓存、进度、取消、TXT / SRT 导出；流式内容问答、独立对话历史、回答存为想法；分类 / 标签 / 摘要预览与手动修改 |

<details>
<summary>查看实际界面</summary>

![kok v0.4.1 预览版实际界面](docs/screenshots/overview-041-dark.png)

</details>

## 按自己的方式使用

- **纯净模式**：本地播放、编辑、阅读和记录，阻止 AI 请求，无需联网使用基础功能。
- **智能模式**：保留本地功能，按需启用 AI 翻译、问答与整理；进入模式本身不调用模型，自动翻译默认关闭。
- **私密文件夹**：两种模式中都能打开的密码文件夹。只加密索引、笔记和封面，不接入 AI；返回普通资料库、离开窗口、最小化或闲置 5 分钟自动锁定。**原文件不加密，普通资料库已有的同一文件不会自动隐藏，密码无法找回。**

智能模式下，在 **设置 → AI 服务**填写地址、模型和密钥，可读取模型列表。提供 DeepSeek、OpenAI、通义千问、硅基流动、智谱、Z.ai、Gemini、Groq、OpenRouter、Ollama、LM Studio 及自定义兼容接口入口。

AI 问答支持视频字幕及可选当前画面，或文档 / 读书正文、选文与笔记；长内容会节选，**不代表完整理解整部视频**。AI 归纳先预览，再应用，只更新资料库分类。选定内容会发往配置的服务，费用按服务商规则计算；密钥在本机加密保存，源码和发布包排除个人配置与资料。

## 格式与数据

- 常用格式包括 MP4 / WebM / MKV、JPG / PNG / WebP、MP3 / WAV / FLAC、PDF / DOCX、EPUB / TXT。内置播放使用 WebView2，未集成独立解码器；**能否播放取决于实际编码**，不支持的文件可用系统应用打开。详见[兼容性清单](docs/FORMATS.md)。
- DOCX / EPUB 以文字阅读为主；扫描 PDF 需外部 OCR，不解密 DRM。标记和插入文字保存在 kok 阅读副本，照片编辑另存，保留原文件。
- 收录文件不搬动原件，移除记录不删除原件。普通索引与笔记可导出 JSON；私密文件夹可导出 `.kokvault` 加密备份。备份均不含原文件，恢复方法见[模式与私密文件夹](docs/MODES.md)。

快捷键：`Ctrl K` 搜索 · `Esc` 关闭 · 视频 `空格` 播放 / 暂停、`← / →` 跳转、`F` 全屏 · `Ctrl Shift L` 锁定私密文件夹。视频键位可自定义。

## 文档与开发

[AI 服务配置](docs/AI_PROVIDERS.md) · [智能整理与内容问答](docs/SMART_LIBRARY.md) · [构建与测试](docs/DEVELOPMENT.md) · [路线图](ROADMAP.md) · [反馈问题](https://github.com/ChenShoukang/kok/issues/new/choose) · [安全报告](SECURITY.md)

```powershell
npm ci --omit=optional
.\build.ps1 -Test
```

开发需要 Node.js 24，输出位于 `dist/kok/`；打包运行 `.\package.ps1`。原创代码与示例采用 [MIT](LICENSE)，第三方组件见[许可说明](THIRD_PARTY_NOTICES.md)。

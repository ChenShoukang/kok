# v0.5.1 · 更少文字，更顺手的 AI

本地开发版，未上传、未发布。已发布的版本仍是 v0.4.1。

## 设计

平整页面承载内容；按钮、封面与工具通过微弱高光和阴影提供触感。统一 5px 控件圆角，侧栏维持 24px 图标 / 44px 点击区域。小交互约 160ms，材质与展开约 260ms；尊重系统减少动态效果设置。

- 设置分为外观、功能、资料库，保留一眼可懂的操作名。
- 首次进入每种模式保留一句简介；AI 发送范围与自动翻译提醒仍清楚可见。
- AI 默认只显示引用范围、对话与输入框，详细引用选项按需展开。
- 深浅色及自定义配色共用材质变量，不在深色页插入白色按钮。

## 开源参考

仅参考交互与视觉原则，使用 kok 自己的 HTML / CSS / JavaScript 实现，没有引入 React 或这些项目的运行依赖。

| 来源 | 采用的思路 |
| --- | --- |
| [Microsoft Fluent UI](https://github.com/microsoft/fluentui) / [Fluent 2 层次](https://fluent2.microsoft.design/elevation) | 用一致的光源、细边框与低层次阴影表达可操作性 |
| [Fluent 2 动效](https://fluent2.microsoft.design/motion) | 动效服务状态变化；短距离快速反馈，大元素适当放缓 |
| [Microsoft WinUI Gallery](https://github.com/microsoft/WinUI-Gallery) | 桌面控件、响应布局与无障碍交互参考 |
| [Lobe UI](https://github.com/lobehub/lobe-ui) | 对话区、输入区与消息操作分层，附加信息逐步展开 |

内置 Fluent Emoji Flat 图标继续使用原资源及现有许可。字体现提供得意黑和霞鹜文楷轻便版，离线按需加载并附完整许可，见[字体说明](FONT_CANDIDATES.md)。

照片、视频、文档和读书可同时打开为独立窗口。拖动标题区移动，拖动边缘缩放，双击内容空白处退到主窗口后面。点击主窗口底部的文件标签，或再次打开同一文件，可唤回原窗口。双击正文仍用于选词，视频画面仍保留双击全屏。私密文件夹切换与锁定会保存并关闭相关窗口。

English: Photos, videos, documents and books support independent native windows. Drag the header to move, resize from the edges, and double-click empty content space to send a viewer behind the library. Restore it from the bottom window strip or reopen the file. Text selection and video fullscreen retain their existing gestures. Private-folder transitions save and close viewers.

## AI

已增加多文件对比、笔记回顾与关联、视频字幕目录，使用入口与范围见 [三项 AI 工具](STUDY_TOOLS_0.5.1.md)。

- 视频按字幕时间段、文档按页、电子书按章节生成带来源的引用。
- 对长文做本地关键词检索，结合分布取样选择最多 24 段、约 30,000 字符；不再丢弃长章节中部。
- PDF 最多读取 80 个分布页面，包含当前页。超过范围标为节选，不声称已经读完整篇。
- 回答支持简洁 Markdown、复制、保存为想法、笔记提炼、失败重试与停止。
- 点击有效来源返回对应段落、PDF 页或视频时间；来源随文件对话持久保存。
- 发送前可以本地预览所选正文与笔记；预览不请求模型。画面仅在用户勾选后发送。

引用编号来自本地提取，但回答及其引用是否支持结论仍需人工判断。本地检索采用关键词，没有向量模型或联网索引。扫描 PDF 仍需 OCR；视频依靠已有字幕或用户选择的单帧，未增加语音识别。模型需提供现有的兼容接口；画面问答需视觉模型。

纯净模式与私密文件夹继续禁止 AI。密钥由现有本机加密机制保存。界面检查使用隔离资料库和本地模拟接口。后续经用户授权使用本机 DeepSeek 配置完成 10 组真实短内容验证，只发送自编文字，见 [内容验证记录](AI_VALIDATION_0.5.1.md)。

## 本地检查

```powershell
npm run check
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-desktop.ps1 -Suites refined-051
```

针对性检查覆盖设置分组、深浅色、小窗口、中英文、长文章中部检索、发送内容预览、引用持久化与跳转、文档 / 书籍 / PDF / 字幕问答、笔记保存、重试及取消。截图仅截取隔离测试窗口内的示例资料。真实 DeepSeek 短内容验证另见上述记录，没有据此声称完成所有功能回归或所有真实资料的质量评估。

---

English: v0.5.1 is a local, unpublished development build. It reduces interface copy, groups settings, unifies tactile materials, and improves the file assistant with local keyword retrieval, outgoing-context preview, persistent source navigation, answer copying and note workflows. Design references are linked above; no new UI framework is bundled. Long PDFs are sampled, citations require human judgment, and video questions still rely on subtitles or an explicitly selected frame. UI checks use an isolated library and a mock API. Subsequently, 10 authorized live DeepSeek checks passed using synthetic short text only; see the linked content validation record. No personal file content was sent.

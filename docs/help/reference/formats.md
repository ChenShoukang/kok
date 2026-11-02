# 支持的格式

kok 能收录很多类型的文件，但“能收录”不等于“能内置打开”。这页说明哪些格式可以直接播放、阅读和编辑，哪些需要借助系统里的其他应用。

## 三类支持范围

| 范围 | 含义 | 常见例子 |
| --- | --- | --- |
| 内置打开 | 在 kok 的查看器里直接播放或阅读 | MP4、JPG、MP3、PDF、EPUB、DOCX、PPTX |
| 取决于编码 | 后缀可以收录，能否播放看容器里的实际编码 | MKV、MOV、WEBM、M4A、WAV |
| 建议系统应用 | 可以收录和分类，但请用已安装的系统应用打开 | AVI、WMV、TS、HEIC、MOBI、XLSX |

## 收录与打开是两件事

上表列出的后缀都可以导入、分类和收藏，能否在 kok 里直接播放或阅读则取决于支持范围。需要系统应用的文件会保留格式图标，在文件菜单里选择**使用系统应用打开**即可。

## 观看与阅读

- **视频**：推荐 H.264 / AAC 的 MP4，或 VP8 / VP9 / Opus 的 WebM。MKV 只支持内核可解码的轨道，不提供多音轨切换。
- **照片**：JPG、PNG、GIF、WEBP、BMP、AVIF 可内置预览与简单编辑；TIFF、HEIC 建议系统应用。
- **音乐**：MP3、WAV、OGG、FLAC、M4A、AAC、OPUS 取决于实际编码与 WebView2。
- **文档与电子书**：PDF、DOCX、PPTX、Markdown、TXT、EPUB 可内置阅读；复杂 Office 版式不会完整还原。

## 编辑与导出

照片可另存 PNG / JPEG，编辑输出长边最多 4096 px；音视频剪辑与导出需要另外准备 FFmpeg / FFprobe；DOCX / EPUB 只重建简洁正文；PDF 页面整理后，编辑过的画布页面按图像导出；PPTX 导出只重建基础元素。扫描 PDF 可以借助 Windows OCR 检索，但不会写入可选择文字层。

## 字幕与系统组件

外挂文字字幕支持 SRT、VTT、ASS、SSA、SUB（仅 MicroDVD）、SBV、LRC、TTML、DFXP、SMI；ASS / SSA 只显示文字，不还原特效。单个字幕文件上限 8 MB、最多 50,000 条。本地语音识别需要另外准备引擎和模型，Windows OCR 需要系统安装对应语言，音视频导出需要 FFmpeg / FFprobe。

> [!warning]+ 不处理 DRM
> kok 不解密受 DRM 保护的电子书或媒体，也不能绕过付费、登录或加密限制。这类文件请使用对应的官方应用打开。

---

**相关**

- [格式兼容性（完整清单）](../../FORMATS.md)
- [能力边界与未实现](boundaries.md)
- [常见问题](faq.md)
- [导入第一份资料](../getting-started/import.md)

[返回帮助中心](../index.md)

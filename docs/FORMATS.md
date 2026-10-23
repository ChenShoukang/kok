# 格式兼容性 · kok v0.1.7

中文 · [English](FORMATS.en.md)

软件内可打开「设置 → 支持格式」查看同一份注册表。下列后缀均可收录和分类；“建议系统应用”需要另行安装相应软件，不承诺内置打开。

| 类别 | 后缀 | 范围 | 说明 |
| --- | --- | --- | --- |
| 视频 | MP4 / M4V / WEBM / MOV / MKV | 取决于编码 | 推荐 H.264 / AAC 的 MP4，或 VP8 / VP9 / Opus 的 WebM。MKV 仅限内核可解码的轨道，不提供多音轨切换。容器后缀不能保证编码可播放；HEVC、AV1 等取决于当前内核、系统与硬件。 |
| 视频 | AVI / WMV / MPG / MPEG / TS / M2TS / FLV / 3GP | 建议系统应用 | 可以收录和分类，建议使用系统播放器；不承诺内置解码，不包含转码器。 |
| 照片 | JPG / JPEG / PNG / GIF / WEBP / BMP / AVIF | 内置打开 | 内置预览与简单编辑。动图编辑只保留当前静态画面；编辑长边最多 4096 px，另存 PNG / JPEG。WebP / AVIF 缩略图可能显示格式图标。 |
| 照片 | TIF / TIFF / HEIC / HEIF | 建议系统应用 | 可以收录和分类，使用系统图片应用打开；不承诺内置预览或编辑。 |
| 音乐 | MP3 / WAV / OGG / OGA / FLAC / M4A / AAC / OPUS | 取决于编码 | MP3、PCM WAV、FLAC、Vorbis / Opus 和 AAC 等取决于实际编码与 WebView2；WAV 等容器也可能包含不支持的编码。 |
| 音乐 | WMA / AIFF / APE | 建议系统应用 | 可以收录到专辑，建议使用系统播放器。 |
| 文档 | PDF | 内置打开 | 内置 PDF 预览、文字层、标记与位置插入；扫描件不提供 OCR。批注保存在 kok，不能写回原 PDF。 |
| 文档 | DOCX | 内置打开 | 提取正文阅读、插入与批注，不保留 Office 原始排版、图片、表格布局或宏。 |
| 文档 | CSV / MD / MARKDOWN / LOG / JSON / HTML / HTM | 内置打开 | 按纯文本阅读，Markdown / HTML 不渲染排版，CSV 不显示电子表格。支持 UTF-8、UTF-16 和 GB18030。 |
| 文档 | DOC / RTF / XLSX / XLS / PPTX / PPT | 建议系统应用 | 可以收录和分类，使用已安装的 Office、WPS 或其他系统应用打开。 |
| 读书 | EPUB / TXT | 内置打开 | 无 DRM 的 EPUB 正文与 TXT 分节阅读；EPUB 不还原图片、复杂排版与脚注交互。TXT 支持 UTF-8、UTF-16 和 GB18030。 |
| 读书 | MOBI / AZW / AZW3 / FB2 | 建议系统应用 | 可以收录和分类，使用系统阅读器；不解密 DRM。 |

媒体兼容性同时取决于**容器和内部编码**、内核与系统。优先选择 H.264/AAC 的 MP4；HEVC 属于条件兼容，安装系统扩展也不能保证所有文件。参见[微软编码说明](https://learn.microsoft.com/en-us/troubleshoot/microsoft-edge/development/video-playback-issues)。图片格式参考 [MDN 图片说明](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Image_types)，kok 不承诺内置支持 TIFF / HEIC。

## 字幕、字体与导出

- 外挂文字字幕：**SRT、VTT、ASS、SSA、SUB（仅 MicroDVD）、SBV、LRC、TTML、DFXP、SMI（SAMI）**。支持同名和语言后缀自动加载。ASS / SSA 只显示文字，不还原特效；TTML / DFXP 支持基础时钟、秒、毫秒时间，不支持完整样式及帧 / tick 时间。不提取内嵌或位图字幕，不做语音识别。
- 字幕上限：单文件 8 MB、50,000 条；自动发现最多加载 20 轨。
- 字体：TTF / OTF，单文件最多 20 MB。照片副本：PNG / JPEG。阅读副本和 PDF 批注导出：HTML。译文导出：TXT / SRT。
- DOCX / EPUB 提取文字，不保留原排版；Markdown / HTML 按纯文本显示，不执行内容。扫描 PDF 需外部 OCR 才能选择文字。文本读取上限 24 MB，EPUB 内容总计上限 48 MB、最多 2,000 章。不解密 DRM。

## 实际解码结果

验证日期：2026-10-23；Windows x64，WebView2 **155.0.4283.62**。以下使用原创生成文件，不代表对所有 profile、位深、DRM、色彩空间或损坏文件的认证。测试经 kok 原生字节区间接口读取实际文件，检查播放时间推进和图片像素，而非仅依赖 canPlayType。

| 测试文件 / 编码 | 实际结果 |
| --- | --- |
| h264-aac.mp4 | 内置解码成功 |
| h264-aac.m4v | 内置解码成功 |
| h264-aac.mov | 内置解码成功 |
| vp8-opus.webm | 内置解码成功 |
| vp9-opus.webm | 内置解码成功 |
| h264-aac.mkv | 内置解码成功 |
| mpeg4-mp3.avi | 使用系统应用 |
| wmv2-wma2.wmv | 使用系统应用 |
| h264-aac.ts | 使用系统应用 |
| mp3.mp3 | 内置解码成功 |
| pcm.wav | 内置解码成功 |
| flac.flac | 内置解码成功 |
| vorbis.ogg | 内置解码成功 |
| opus.opus | 内置解码成功 |
| aac.m4a | 内置解码成功 |
| adts.aac | 内置解码成功 |
| wma.wma | 使用系统应用 |
| pcm.aiff | 使用系统应用 |
| photo.jpg | 内置解码成功 |
| photo.png | 内置解码成功 |
| photo.gif | 内置解码成功 |
| photo.webp | 内置解码成功 |
| photo.bmp | 内置解码成功 |
| photo.avif | 内置解码成功 |
| photo.tiff | 使用系统应用 |

本环境中 19 个文件内置解码成功，6 个需要系统应用。HEIC / HEIF、APE、MPEG / FLV / 3GP 及其他未列出的编码没有生成文件实测，不提供笼统兼容保证。原生测试另验证全部 10 种字幕解析；PDF 界面测试验证实际渲染、文字与批注；DOCX / EPUB / 文本测试验证内容提取与持久保存。

复现：启动 kok 调试窗口，执行 `node tests/compatibility.cjs <ffmpeg.exe路径>`。FFmpeg 仅用于生成测试文件，不随软件分发，运行 kok 不需要它。随后执行 `node tools/write-format-docs.cjs` 可由注册表与实测结果更新此清单。

# 编辑与音乐整理 · v0.5.1 预览版

打开文件，点击标题栏的「编辑」。音乐列表右侧也有编辑按钮。常用操作直接显示，更多调整默认收起。

| 内容 | 当前功能 |
| --- | --- |
| 照片 | 调色、滤镜、自由裁剪、翻转、对比、撤销；文字、画笔、箭头、矩形、遮挡；图层选择、移动、缩放、排序、颜色与大小调整；保存草稿，另存 PNG / JPEG，长边最高 8192 px |
| 视频 | 入点 / 出点、选段预览、分割、跨素材片段排序与拼接、速度、音量、淡入淡出、旋转、静音；每段字幕文字，导出 MP4 |
| 音频 | 实际波形（浏览器可解码且文件较小时）、剪切、多素材拼接、片段重排、变速、音量与淡入淡出；导出 WAV / MP3 / M4A |
| 文档与书籍 | 可视化改字、标题、加粗、斜体、引用、列表、撤销 / 重做；阅读预览、分栏；保存草稿，另存 DOCX / EPUB / Markdown / TXT |
| PDF | 页面缩略图、拖动排序、旋转、移除、添加和合并 PDF；单页文字、框选替换、画笔、箭头和矩形；保存项目、导出 PDF |
| 音乐资料库 | 专辑、歌手、我的音频、全部音频；本地读取名称、歌手、专辑、内嵌封面、曲目号、年份、风格、时长和音频参数；手动修正、联网匹配候选、批量 AI 整理 |

原文件保留。编辑结果另存为新文件并加入资料库；草稿保存在本地索引，私密文件夹内的草稿继续随索引加密。手动修改的音乐信息保存在 kok，不改写音频文件标签。

音视频导出复用已安装的 FFmpeg / FFprobe；不增加另一套媒体运行时。智能模式可安装组件，纯净模式可选择已有组件。字幕是随片段导出的可见文字，不是多轨字幕制作工具。

PDF 画布编辑过的页面以图像保存，原页底层文字不再保留；未进行画布编辑的页面继续复制原有页面内容。页面整理项目限 500 页，处理的 PDF 数据合计限 32 MiB。DOCX / EPUB 导出重建简洁的正文文档，不还原原文件中的复杂版式、嵌入对象或交互表单。

## 音乐识别与 AI

- **本地标签**：离线读取常见音频的标签与封面；文件缺少标签时尝试规范的「歌手 - 曲名」文件名，允许人工修正。标签读取能力不等于播放解码能力。
- **联网匹配**：用户点击后，仅将曲名和歌手发送至 [MusicBrainz](https://musicbrainz.org/doc/MusicBrainz_API/Search)。展示候选发行版本，选中并保存后才应用。搜索相关度不是身份识别保证；未使用声纹，也不上传音频。
- **AI 整理**：选择最多 50 个音频，先查看将发送的文件名与标签，再调用已配置的模型生成分类、标签和摘要。确认所选结果后应用；不会移动文件，不凭空补写歌手身份，也不分析音频内容。纯净模式和私密文件夹阻止此类联网操作。

## 开源参考与组件

功能组织参考 [PhotoCraft](https://github.com/storytold/photocraft)、[FilmCraft](https://github.com/storytold/filmcraft)、[LightCraft](https://github.com/storytold/lightcraft)、[PdfCraft](https://github.com/storytold/pdfcraft) 和 [DesignCraft](https://github.com/storytold/designcraft) 的公开说明：统一工作区、非破坏编辑、片段时间线、页面卡片和可视化正文。没有移植这些项目的 Rust 引擎、代码或品牌素材。

音乐标签采用未修改的 TagLibSharp 2.3.0；PDF 编排采用 PDF-LIB 1.17.1。许可、固定版本与对应源码说明见[第三方说明](../THIRD_PARTY_NOTICES.md)。

## 验证范围

集中检查使用独立、静音的测试资料库和合成音频、图片、视频、文档、PDF。已验证标签和内嵌封面、人工修正保留、实际音视频裁剪与拼接、DOCX / EPUB 重开、PDF 编排与画布输出、照片图层草稿、中英文和小窗口。AI 使用本地模拟服务验证数据预览与确认应用；联网目录另以公开歌曲查询验证，不使用个人音频或真实模型密钥。

English: Local v0.5.1 adds a compact editor for photos, video/audio clips, visual text and PDF pages, plus album/artist/personal-audio views. Changes save as drafts or new copies. Media exports reuse optional local FFmpeg/FFprobe. Text exports support DOCX, EPUB, Markdown and TXT. Canvas-edited PDF pages export as images; untouched pages retain their source content. PDF projects are limited to 500 pages and 32 MiB of source PDF data. Complex Office layout and interactive PDF forms are not reconstructed. Music tags and covers are read locally; MusicBrainz searches send only the chosen title/artist, and AI grouping previews only selected metadata before confirmation. No audio is uploaded. Pure mode and private folders disable online features. Checks use synthetic files and a local mock AI; a public catalogue query was checked separately.

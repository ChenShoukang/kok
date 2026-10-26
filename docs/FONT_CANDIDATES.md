# 内置中文字体

已内置得意黑和霞鹜文楷轻便版。打开「设置 → 外观 → 常用字体」切换，离线可用，同时应用于界面和正文阅读。默认采用得意黑。

| 字体 | 设计建议 | 当前原始文件 |
| --- | --- | --- |
| [得意黑 Smiley Sans](https://github.com/atelier-anchor/smiley-sans) | 有辨识度的界面与标题，当前默认 | 官方 v2.0.1 WOFF2 |
| [霞鹜文楷轻便版 LXGW WenKai Lite](https://github.com/lxgw/LxgwWenKai-Lite) | 手写与文艺风格，作为笔记、书摘与阅读的可选样式 | [Regular TTF](https://github.com/lxgw/LxgwWenKai-Lite/blob/main/fonts/TTF/LXGWWenKaiLite-Regular.ttf)，13,949,472 bytes，约 13.9 MB |

字体采用 SIL OFL 1.1。软件分发时保留各自的版权和许可原文，字体不改用 kok 的代码许可。

文楷轻便版运行资源约 6.4 MB，采用无损 WOFF2，不重复附带 TTF。完整字库、笔画、字距、排版与许可元数据保留，没有裁字。仅按 WOFF2 规范移除签名表、设置格式标志，其余表数据逐字节核对原文件。按需加载选中的字体，不安装至系统。轻便版未覆盖的生僻字可由系统字体回退；PDF 正文继续由 PDF 阅读器处理。

固定版本、校验值及来源见 [字体清单](../ui/fonts/manifest.json)和[资源说明](../ui/fonts/NOTICE.md)。构建会校验运行字体的哈希及许可是否齐全。

English: Smiley Sans and LXGW WenKai Lite are bundled for offline selection in Settings → Appearance → Font. Smiley Sans remains the default. WenKai uses about 6.4 MB of losslessly compressed WOFF2 with no subsetting. All glyphs, layout and name/license metadata are preserved. No system installation or remote font service is used. Full upstream OFL licenses and source/runtime hashes are included.

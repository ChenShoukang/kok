# 品牌资源 / Brand assets

当前采用蓝色 K 标志。 / The current mark is the original blue K.

- [SVG 源文件 / SVG source](../../ui/logo.svg)
- [Windows 图标 / Windows icon](../../ui/kok.ico)
- [PNG 预览 / PNG preview](logo.png)

## 功能总览 / Feature overview

- 中文：[PNG](features-zh.png) · [SVG](features-zh.svg)
- English: [PNG](features-en.png) · [SVG](features-en.svg)
- 中文窄屏：[PNG](features-zh-mobile.png) · [SVG](features-zh-mobile.svg)
- English, narrow screens: [PNG](features-en-mobile.png) · [SVG](features-en-mobile.svg)

总览图由项目图标与矢量卡片绘制，和实际界面截图分开标注。 / The overview uses project icons and vector cards; actual app screenshots are labeled separately.

README 使用 GitHub 支持的 `picture` 元素，在不超过 600 px 的窗口中选择两列大字配图。 / The README uses GitHub's supported `picture` element to select a two-column image at viewport widths up to 600 px. See [GitHub image formatting](https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax#the-picture-element).

重新生成：`node tools/render-readme-visuals.cjs --png`。需要开发依赖与本机 Microsoft Edge；不加 `--png` 时只生成 SVG。该命令检查四张图的文字边界，以及 1280 / 375 px 窗口下的图片选择和页面溢出；临时截图写入被忽略的 `test-output/`。 / Regenerate with development dependencies and local Microsoft Edge; omit `--png` for SVG only. Rendering checks text bounds and desktop/mobile image selection at 1280 / 375 px; temporary previews stay in ignored `test-output/`.

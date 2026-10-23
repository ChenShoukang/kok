# 参与开发 / Contributing

请先阅读[开发与测试](docs/DEVELOPMENT.md)和[格式边界](docs/FORMATS.md)。界面改动需要同步中文与英文，并在深浅主题、小窗口、键盘操作和减少动态效果下验证。媒体测试使用独立资料库与 `--debug --test-silent`。

提交问题时附上版本、复现步骤、文件格式与可公开的示例；诊断日志位于 `data/logs/kok.log`。提交代码前运行 `npm run check` 和适合改动的测试。原文件与用户资料库不进入源码或发布包。

Read the [development guide](docs/DEVELOPMENT.en.md) and [format boundaries](docs/FORMATS.en.md). Keep Chinese and English controls in sync. Check themes, compact windows, keyboard operation and reduced motion. Run media tests with a separate profile and `--debug --test-silent`.

For bug reports, include the version, reproduction steps, file format and a redistributable example. Local diagnostics live in `data/logs/kok.log`. Run `npm run check` and relevant tests before submitting changes. Keep original personal files and profiles outside source and release packages.

# 参与开发 / Contributing

请先阅读[开发与测试](docs/DEVELOPMENT.md)和[格式边界](docs/FORMATS.md)。界面改动需要同步中文与英文，并在深浅主题、小窗口、键盘操作和减少动态效果下验证。媒体测试使用独立资料库与 `--debug --test-silent`。

新能力先按[模块边界](docs/MODULES_0.8.1.md)注册动作、参数与权限，不再增加全局函数覆盖。存储改动要提供迁移保留、失败恢复和私密模式回归。验证命令与范围见[当前验证记录](docs/VALIDATION_0.8.1.md)；桌面套件串行运行，禁止连接个人资料目录或默认读取真实 API 配置。

每个 PR 说明解决的问题、可观察的行为变化、验证结果和剩余限制。保持改动围绕一个目的；不混入生成包、依赖目录或个人配置。参与前阅读[社区约定](CODE_OF_CONDUCT.md)，使用问题见[支持说明](SUPPORT.md)。

提交问题时附上版本、复现步骤、文件格式与可公开的示例；诊断日志位于 `data/logs/kok.log`。提交代码前运行 `npm run check` 和适合改动的测试。原文件与用户资料库不进入源码或发布包。

Read the [development guide](docs/DEVELOPMENT.en.md) and [format boundaries](docs/FORMATS.en.md). Keep Chinese and English controls in sync. Check themes, compact windows, keyboard operation and reduced motion. Run media tests with a separate profile and `--debug --test-silent`.

For bug reports, include the version, reproduction steps, file format and a redistributable example. Local diagnostics live in `data/logs/kok.log`. Run `npm run check` and relevant tests before submitting changes. Keep original personal files and profiles outside source and release packages.

Register new capabilities with schemas and policy through the [module boundaries](docs/MODULES_0.8.1.en.md); do not add global function overrides. Storage changes need migration, failure-recovery and private-mode regression coverage. Follow the [current validation guide](docs/VALIDATION_0.8.1.md#english); run desktop suites serially with isolated profiles and mock providers. PRs should explain the problem, observable behavior, validation and remaining limits. Follow [community conduct](CODE_OF_CONDUCT.md); see [support](SUPPORT.md) for usage questions.

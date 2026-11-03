# v0.10.8 Preview · 验证记录

本版加入**声明式扩展包**：扩展包目录可携带 Agent Skills 的 `SKILL.md` 技能与 Markdown 提示模板，从程序/用户/资料库插件根发现并热重载；Agent 技能选择器会列出扩展包技能，`/prompt:名称 参数` 把模板展开到输入框。扩展包只包含静态文本，**不执行任何脚本或代码**；审批、证据核对、撤销与沙箱语义不变。

## 验证范围

- 静态检查：`npm run check`（样式构建、界面脚本语法、模块架构契约、本地化目录、文档链接、版本契约、30 项任务样本离线校验）。
- 原生检查：`build.ps1 -Test` 编译程序并运行 `kok.Tests.exe` 原生用例。
- 桌面回归：隔离资料库 + 本地模拟服务 + 真实 WebView2，覆盖扩展包面板/重新加载/`$ARGUMENTS` 模板、`/prompt:` 命令、技能选择器、分支树、技能、输入队列、压缩、审批、恢复、i18n 与基础功能。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 Agent 相关桌面套件。
- 本轮不发起真实模型请求，不使用真实 API 预算。

## 结果（2026-11-03）

- 版本契约：`node tools/check-version.cjs` 通过，`package.json` / `AssemblyInfo` / 浏览器扩展 / README 包名一致为 **0.10.8**。
- 原生检查：**834 项通过**（上一基线 816 项；新增 12 项扩展包发现/校验/模板检查与 6 项 Host 集成检查）。
- 桌面回归：**11 个套件全部通过**——`agent`、`agent-kernel`、`agent-interactions`、`agent-workflow`、`agent-reliable`、`agent-quality`、`pi-agent-ui`、`agent-runtime`、`agent-platform`、`i18n`、`features`。运行记录：`test-output/full-20261103-093726-86818887/`（源/运行时指纹运行结束后校验一致）。扩展包场景覆盖：创建临时扩展包 → 面板显示名称/版本/说明与技能·模板数 → 重新加载后技能进入选择器 → `/prompt:write-notes Hello notes` 展开到输入框（含参数、无 `/prompt:` 原文、不自动发送、不新建任务）→ 删除扩展包并重新加载后从面板与选择器消失。
- 交付包：`dist/kok-0.10.8-win-x64.zip`，**30.71 MB**，SHA-256 `b8f16680aedbbc982ecf0c0be3dc5977b5a23be23eea4b81140193471676c464`；产品版本与 `package.json` 一致。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 **3 个桌面套件全部通过**（agent、agent-workflow、i18n，含扩展包与 `/prompt:` 场景），记录 `test-output/package-bc586ae4435f47128da558d2fb9ea495/`（`archive.json` 保存套件列表、源 / 运行时指纹与 ZIP SHA-256）。

## 证据

- 原生测试输出：`834 tests passed`（`build.ps1 -Test`，构建产物 `dist/kok`）。
- 桌面运行记录：`test-output/full-20261103-093726-86818887/`。
- 交付包与校验和：`dist/kok-0.10.8-win-x64.zip` 及同名 `.sha256`；包级复验记录在 `test-output/package-bc586ae4435f47128da558d2fb9ea495/`。
- 新增测试：`tests/AgentPluginTests.cs`（12 项发现/清单/路径/模板/热更新检查）、`tests/AgentKernelTests.cs`（6 项 Host 集成检查）、`tests/agent-workflow.cjs`（扩展包面板与 `/prompt:` 桌面场景）。
- 文档：[声明式扩展包](PLUGINS_0.10.8.md)。

## 边界

- 扩展包纯声明式：无代码入口、无网络/文件 API、无钩子注册；`plugin.json` 中未知字段（含 `scripts`/`main`/`hooks` 等）仅作为惰性数据忽略，永不执行。
- 路径必须位于扩展包目录内（绝对路径/`..` 拒绝）；最多 32 个扩展包、清单 ≤64KB、技能目录/模板目录各 ≤16 项、提示模板 ≤32 个且单模板 ≤8000 字符、技能正文 ≤20000 字符。
- 扩展包技能/模板进入上下文时仍标记为不可信参考文本；`/prompt:` 只填充输入框，不自动发送、不持久化原始命令。
- 事件契约与任务/会话数据格式不变；审批、证据、撤销与沙箱语义不变。

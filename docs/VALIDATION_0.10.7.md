# v0.10.7 Preview · 验证记录

本版增加无界面 Agent CLI（`kok agent --print/--json`，只读）与提供方缓存键（可选 `prompt_cache_key`），并在 Agent 用量行展示缓存命中。审批、证据核对、撤销与沙箱语义不变；无界面入口打开窗口/WebView。

## 验证范围

- 静态检查：`npm run check`（样式构建、界面脚本语法、模块架构契约、本地化目录、文档链接、版本契约、30 项任务样本离线校验）。
- 原生检查：`build.ps1 -Test` 编译程序并运行 `kok.Tests.exe` 原生用例。
- 桌面回归：隔离资料库 + 本地模拟服务 + 真实 WebView2，覆盖 AI 缓存设置往返、缓存命中展示、分支树、技能、输入队列、压缩、审批、恢复、i18n 与基础功能。
- CLI 冒烟：真实 `dist/kok/kok.exe agent --help`、参数拒绝与 JSON 错误行。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 Agent 相关桌面套件。
- 本轮不发起真实模型请求，不使用真实 API 预算。

## 结果（2026-11-03）

- 版本契约：`node tools/check-version.cjs` 通过，`package.json` / `AssemblyInfo` / 浏览器扩展 / README 包名一致为 **0.10.7**。
- 原生检查：**816 项通过**（上一基线 800 项；新增 10 项 headless CLI 检查与 6 项 prompt_cache_key 检查）。
- CLI：`kok-agent.exe --help` 退出 0 并输出用法；`--allow-writes` 退出 2 且 stderr 说明只读；`--json --print` 退出 2 且 stdout 只有一条 JSON error 行。原生测试还覆盖：JSON header/event/result 形状与顺序、print 仅最终文本、只读下的写提案失败且无写入、参数校验、超时停止、非智能模式拒绝。`kok-agent.exe` 为控制台子系统启动器，`cmd`/PowerShell 会等待并返回真实退出码；`kok.exe agent …` 为同实现但受窗口子系统等待语义限制。
- 桌面回归：**11 个套件全部通过**——`agent`、`agent-kernel`、`agent-interactions`、`agent-workflow`、`agent-reliable`、`agent-quality`、`pi-agent-ui`、`agent-runtime`、`agent-platform`、`i18n`、`features`。运行记录：`test-output/full-20261103-091724-64b73db2/`（源/运行时指纹运行结束后校验一致）。其中 agent-platform 覆盖 AI 缓存开关往返（`aiConfig.promptCache`）、`prompt_cache_hit_tokens` 汇总与用量行 `缓存命中 … tokens · 50%` 显示。
- 交付包：`dist/kok-0.10.7-win-x64.zip`，**30.69 MB**，SHA-256 `e9cae151cf1a425d2534234ec6ca7fa34b5f7500c9404ffbcdb1e2a7f99e2269`；产品版本与 `package.json` 一致，包内包含 `kok.exe` 与控制台启动器 `kok-agent.exe`。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 **3 个桌面套件全部通过**（agent、agent-platform、agent-workflow），记录 `test-output/package-abe568768e66439ea626d9573af973a5/`（`archive.json` 保存套件列表、源 / 运行时指纹与 ZIP SHA-256）；从 ZIP 解出的 `kok-agent.exe --help` 退出 0。

## 证据

- 原生测试输出：`816 tests passed`（`build.ps1 -Test`，构建产物 `dist/kok`）。
- 桌面运行记录：`test-output/full-20261103-091724-64b73db2/`。
- 交付包与校验和：`dist/kok-0.10.7-win-x64.zip` 及同名 `.sha256`；包级复验记录在 `test-output/package-abe568768e66439ea626d9573af973a5/`。
- CLI 启动器冒烟：`dist/kok/kok-agent.exe --help` 退出 0；`--allow-writes` 退出 2；`--json --print` 退出 2 且只输出一条 `{"type":"error"}` JSON 行。
- 新增测试：`tests/AgentHeadlessTests.cs`（10 项 CLI 检查）、`tests/AgentKernelTests.cs`（6 项 prompt_cache_key 检查）、`tests/agent-platform.cjs`（缓存开关与命中显示）。
- 文档：[Agent 无界面 CLI](AGENT_CLI_0.10.7.md)。

## 边界

- 无界面入口只读：写入/审批/MCP 写工具仍只在桌面端逐卡处理；`--approval` / `--allow-writes` 被拒绝。
- 不加载历史会话（空 history/sessionLeafId）；需要延续上下文请使用桌面分支树。
- `prompt_cache_key` 需要服务商支持；关闭时请求体与旧版一致（原生测试断言无该字段且无其他字段变化）；压缩、JSON 协议与翻译请求不发送该键。
- 无 headless RPC（计划后续）；事件契约与任务/会话数据格式不变；审批、证据、撤销与沙箱语义不变。

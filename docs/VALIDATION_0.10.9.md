# v0.10.9 Preview · 验证记录

本版补上 M2 的最后两块：**codemode**（被批准的 `run_code` 可在 QuickJS 沙箱内通过 `tools.*` 调用只读资料库工具并组合计算）与 **headless RPC**（`kok-agent.exe --rpc` 长期 JSONL 命令循环）。审批、证据核对、撤销与沙箱语义不变；无新增写入能力。

## 验证范围

- 静态检查：`npm run check`（样式构建、界面脚本语法、模块架构契约、本地化目录、文档链接、版本契约、30 项任务样本离线校验）。
- 原生检查：`build.ps1 -Test` 编译程序并运行 `kok.Tests.exe` 原生用例。
- 桌面回归：隔离资料库 + 本地模拟服务 + 真实 WebView2，覆盖 run_code 沙箱算术/审批路径、扩展包、分支树、技能、输入队列、压缩、审批、恢复、i18n 与基础功能。
- CLI/RPC 冒烟：`kok-agent.exe` 的 `--help`、参数组合拒绝、`--rpc` 的 ping/命令协议。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 Agent 相关桌面套件。
- 本轮不发起真实模型请求，不使用真实 API 预算。

## 结果（2026-11-03）

- 版本契约：`node tools/check-version.cjs` 通过，`package.json` / `AssemblyInfo` / 浏览器扩展 / README 包名一致为 **0.10.9**。
- 原生检查：**843 项通过**（上一基线 834 项；新增 3 项 codemode 沙箱/白名单/上限检查与 6 项 RPC 协议检查）。
- Codemode：`AgentSandbox` 新增 ticket 作用域、只读白名单、每批/每次/字符上限、逐项错误与生命周期校验；渲染端采用同步 QuickJS 的“记录请求 → 宿主批量执行 → 带缓存重放”协议（最多 6 轮）；`run_code` Schema/审批卡片不变，`tools.*` 不产生任务回执。
- Headless RPC：`kok-agent.exe --rpc` 的 ping/prompt/steer/follow_up/abort/state/compact/shutdown、串行 response/event/result 行、单任务限制、只读策略、EOF/Ctrl+C/参数组合退出码均由原生测试覆盖；修复了启动阶段的 sync-over-async 死锁。
- 桌面回归：**11 个套件全部通过**——`agent`、`agent-kernel`、`agent-interactions`、`agent-workflow`、`agent-reliable`、`agent-quality`、`pi-agent-ui`、`agent-runtime`、`agent-platform`、`i18n`、`features`（含 agent-platform 的 run_code 沙箱算术/审批路径）。运行记录：`test-output/full-20261103-101016-7421ed04/`（源/运行时指纹运行结束后校验一致）。
- 交付包：`dist/kok-0.10.9-win-x64.zip`，**30.72 MB**，SHA-256 `b1694d98713507e9a6219ca89b1d5293a694af314da89e1b681595e66e4bf345`；产品版本与 `package.json` 一致。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 **3 个桌面套件全部通过**（agent、agent-platform、agent-workflow），记录 `test-output/package-81f356b144e3481baa4bc7a15a2a8c93/`（`archive.json` 保存套件列表、源 / 运行时指纹与 ZIP SHA-256）。
- RPC/CLI 冒烟：`--rpc --goal` 组合返回单条 JSON error 行并退出 2；非智能模式资料库以 JSON error 拒绝启动（只读/智能模式门控生效）；ping 等协议行为由原生测试覆盖。

## 证据

- 原生测试输出：`843 tests passed`（`build.ps1 -Test`，构建产物 `dist/kok`）。
- 桌面运行记录：`test-output/full-20261103-101016-7421ed04/`。
- 交付包与校验和：`dist/kok-0.10.9-win-x64.zip` 及同名 `.sha256`；包级复验记录在 `test-output/package-81f356b144e3481baa4bc7a15a2a8c93/`。
- 更新测试：`tests/AgentExecutionTests.cs`（codemode 3 项）、`tests/AgentHeadlessTests.cs`（RPC 6 项）。
- 文档：[Codemode](CODEMODE_0.10.9.md)、[Headless RPC](AGENT_RPC_0.10.9.md)。

## 边界

- `tools.*` 仅只读（`search_library`/`read_items`/`recall_result`/`search_memory`/`list_collections`）；写入、审批、MCP、`run_code`/子代理不会暴露；嵌套读取是临时证据，不能充当 `finish_task` 当前回执。
- Codemode 限制：每次 ≤24 次工具调用、每批 ≤8、单结果 ≤8000 字符、累计 ≤32000 字符、≤6 轮重放；沙箱 16MB / 7 秒 / 输出 ≤16000 不变。
- RPC 只读、不加载历史会话、同一进程单任务；事件契约与任务/会话数据格式不变；审批、证据、撤销与沙箱语义不变。

# v0.10.3 Preview · 验证记录

本版把 Pi 的会话树带到 kok 的 Agent：每次任务的用户 / Agent 消息与任务标记写入本机加密的 append-only 会话记录，消息旁新增「从此处分叉」，可在任意用户或 Agent 消息处创建新会话分支，并用该分支的投影作为下一次任务的模型历史。旧版界面会话在首次访问时导入，原记录保留；审批、证据核对、撤销与沙箱语义不变。

## 验证范围

- 静态检查：`npm run check`（样式构建、界面脚本语法、模块架构契约、本地化目录、文档链接、版本契约、30 项任务样本离线校验）。
- 原生检查：`build.ps1 -Test` 编译程序并运行 `kok.Tests.exe` 原生用例。
- 桌面回归：隔离资料库 + 本地模拟服务 + 真实 WebView2，覆盖 Agent 消息分叉、分支上下文、输入队列、审批边界、恢复与历史、Pi 布局、i18n 与基础功能。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 Agent 相关桌面套件。
- 本轮不发起真实模型请求，不使用真实 API 预算。

## 结果（2026-11-02）

- 版本契约：`node tools/check-version.cjs` 通过，`package.json` / `AssemblyInfo` / 浏览器扩展 / README 包名一致为 **0.10.3**。
- 静态检查：`npm run check` 通过（文档链接 1,153 个本地目标；本地化目录合法；模块架构与版本契约通过）。
- 原生检查：**736 项通过**（上一基线 668 项；新增 49 项会话存储检查与 19 项 Host / 会话同步检查）。
- 桌面回归：**11 个套件全部通过**——`agent`、`agent-kernel`、`agent-interactions`、`agent-workflow`、`agent-reliable`、`agent-quality`、`pi-agent-ui`、`agent-runtime`、`agent-platform`、`i18n`、`features`。
  - 运行记录：`test-output/full-20261102-224739-3026fed3`（`run.json`、`results.json` 与各套件证据）。
  - 关键断言：分叉按钮只出现在可分叉的用户 / Agent 消息上；分叉生成新会话，消息列表在该消息处截断；原生 `agentSessionTree` / `agentSessionProject` 与分支一致；分支续写的模型请求只包含分叉前的历史；原会话与分叉会话各保留各自任务；队列、停止回填、审批与恢复套件保持通过。
- 交付包：`dist/kok-0.10.3-win-x64.zip`，**30.65 MB**，SHA-256 `eb9c0b9b60cb6a01f73e9088f633f61657e51d3b21e59a74534e3791df0ccc56`；产品版本与 `package.json` 一致。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 **4 个桌面套件全部通过**（agent、agent-workflow、agent-interactions、i18n，含消息分叉场景），记录 `test-output/package-31a3ffde71934777bc97ad969c66689e/`（`archive.json` 保存套件列表、源 / 运行时指纹与 ZIP SHA-256）。

## 证据

- 桌面运行记录：`test-output/full-20261102-224739-3026fed3/`。
- 交付包与校验和：`dist/kok-0.10.3-win-x64.zip` 及同名 `.sha256`；包级复验记录在 `test-output/package-31a3ffde71934777bc97ad969c66689e/`。
- 原生测试输出：`736 tests passed`（`build.ps1 -Test`，构建产物 `dist/kok`）。
- 新增 / 更新测试：`tests/AgentSessionTests.cs`（49 项存储检查，含 DPAPI、树/投影、fork、越界与损坏输入）、`tests/AgentKernelTests.cs`（19 项会话 action / 导入 / 同步检查）、`tests/agent-workflow.cjs`（消息分叉桌面场景）。

## 边界

- 本版只实现 **M0-2 会话树的第一片**：原生 append-only 记录 + 消息级 fork/分支投影；原地 `/tree` 切换、分支摘要、完整树形 UI 留待 M1，Pi 式压缩条目留待 M0-3。
- 上限：每库最多 64 个原生会话，每会话 4096 条 / 8 MB；单条消息文本 ≤ 24000 字符。记录与任务日志一样使用当前 Windows 账户 DPAPI，不跨账户或机器迁移。
- 旧界面会话（`agent-sessions.sealed`）在首次会话操作时一次性导入为线性记录；导入不删除原文件，旧存储继续用于界面渲染与草稿。
- `agentSessionFork` 创建新会话，不修改原会话；fork 只在已同步进会话树的用户 / Agent 消息上进行，子任务（delegation）不入会话树。
- 会话同步包含任务中的中间 assistant.reply 事件，投影为完整的用户 / Agent 轮次；工具卡片仍由原任务记录渲染，不进入模型历史。
- 本版不改变 Agent 的审批、证据、撤销、沙箱与输入队列语义。

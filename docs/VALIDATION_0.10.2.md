# v0.10.2 Preview · 验证记录

本版把 Agent 的任务输入方式与 Pi 对齐：任务运行中用 Enter 把指令插入当前任务（steering），用时钟按钮把输入排到任务自然结束时作为同一任务的后续轮次（follow-up）；停止、失败或达到请求上限时，未执行的输入放回输入框。事件契约升级为 v2，旧任务记录按原状态读取、不重写既有加密事件。本版不改变资料、文档与作品的数据格式。

## 验证范围

- 静态检查：`npm run check`（样式构建、界面脚本语法、模块架构契约、本地化目录、文档链接、版本契约）。
- 原生检查：`build.ps1 -Test` 编译程序并运行 `kok.Tests.exe` 原生用例。
- 桌面回归：隔离资料库 + 本地模拟服务 + 真实 WebView2，覆盖 Agent 输入队列、失败发送保留草稿、停止回填、恢复与历史、审批边界、Pi 布局、i18n 与基础功能。
- 本轮不发起真实模型请求，不使用真实 API 预算。

## 结果（2026-11-02）

- 静态检查：`npm run check` 通过（样式构建、界面脚本语法、模块架构契约、本地化目录、文档链接 1,147 个本地目标、版本契约、30 项任务样本离线校验）。
- 版本契约：`node tools/check-version.cjs` 通过，`package.json` / `AssemblyInfo` / 浏览器扩展 / README 包名一致为 **0.10.2**。
- 本地化目录：`node tools/check-locales.cjs` 通过（**2,450 条消息 / 42 条动态模板**；新增 5 条界面文案、6 条原生错误文案与 1 条动态模板）。
- 原生检查：**668 项通过**（上一基线 653 项，净增 15 项），覆盖 steering 在工具批次后注入而不打断在飞回复、不跳过待确认工具、follow-up 在同一任务自然结束前继续、停止回填、排队期间审批、队列上限与模式隔离、旧 v1 journal 兼容重放与未来版本拒绝。
- 桌面回归：**11 个套件全部通过**——`agent`、`agent-kernel`、`agent-interactions`、`agent-workflow`、`agent-reliable`、`agent-quality`、`pi-agent-ui`、`agent-runtime`、`agent-platform`、`i18n`、`features`。
  - 修复后复跑记录：`test-output/full-20261102-220645-e7373442`（agent-workflow、agent-reliable、agent-quality、pi-agent-ui、agent-runtime、agent-platform、i18n、features）。
  - 首轮通过记录：`test-output/full-20261102-220329-c11c2d40`（agent、agent-kernel、agent-interactions）。
- 交付包：`dist/kok-0.10.2-win-x64.zip`，**30.64 MB**，SHA-256 `b07f2d51c207fd0617ce9d9808ec1d1b3230da44ed21901f07dcc5446ca10fde`；包内可执行文件版本与 `package.json` 一致。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 **6 个 Agent 套件全部通过**（agent、agent-kernel、agent-interactions、agent-workflow、agent-reliable、agent-quality），记录 `test-output/package-0b9d9259565143f3979c8ac88017cf3f/`（`archive.json` 保存套件列表、源/运行时指纹与 ZIP SHA-256）。
- 关键桌面断言：运行中 Enter 生成原生 `input.queued`（mode=steer），输入框立即清空、本地 `agent.queue` 保持为空、不产生额外模型请求；时钟按钮生成 mode=follow 且显示“完成后跟进”；释放 in-flight 回复后 steering 在下一轮同一任务请求中出现；自然结束时 follow-up 继续同一任务而不是新建任务；停止后 `restoredInputs` 回到输入框且重复拉取不重复回填、不自动发送。

## 证据

- 桌面运行记录：`test-output/full-20261102-220645-e7373442/`（`run.json`、`results.json` 与各套件截图/页面证据）。
- 交付包与校验和：`dist/kok-0.10.2-win-x64.zip` 及同名 `.sha256`；包级复验记录在 `test-output/package-0b9d9259565143f3979c8ac88017cf3f/`。
- 原生测试输出：`668 tests passed`（`build.ps1 -Test`，构建产物 `dist/kok`，产品版本 **0.10.2**）。
- 新增/更新测试：`tests/AgentKernelTests.cs`（15 项队列检查）、`tests/agent-workflow.cjs`、`tests/agent-reliable.cjs`、`tests/agent-quality.cjs`（事件版本 2）。

## 边界

- 本版只实现 **M0-1 输入队列**：一次消费一条（Pi 默认 one-at-a-time）；`all` 模式、会话树/fork、Pi 式压缩、扩展包等留待后续里程碑（见 `kok-pi-parity-plan-20261102.md`）。
- steering 在“当前工具批次完成/审批结束”后进入，不会取消已提出的工具调用；需要立即作废在飞请求时仍使用“补充要求”（`agentTaskAdjust`，≤400 字符）。
- follow-up 只在任务自然结束前继续同一任务；达到请求上限、失败或停止时按终止原因回填输入框。
- 旧版原生程序不支持新 action 时，界面回退到 `agentTaskAdjust`；同一版本的界面与原生程序应一起升级。
- 事件契约 v2 仅新增事件类型，不重写历史密文；使用更早版本程序打开新任务记录会被拒绝且保留原记录。
- 本版不改变 Agent 的审批、证据、撤销与沙箱语义；队列状态与其他任务事件一样写入加密 journal。

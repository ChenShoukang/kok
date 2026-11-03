# v0.10.4 Preview · 验证记录

本版把上下文压缩对齐 Pi：触发基于真实请求用量，按 keepRecent 预算保留最近回合并摘要旧回合；摘要使用固定六段结构并增量更新；运行中可手动压缩，模型上下文溢出时压缩并重试一次；压缩结果作为 compaction 条目写进本机加密会话树，会话投影与 fork 按 `firstKeptEntryId` 读取。审批、证据核对、撤销与沙箱语义不变。

## 验证范围

- 静态检查：`npm run check`（样式构建、界面脚本语法、模块架构契约、本地化目录、文档链接、版本契约、30 项任务样本离线校验）。
- 原生检查：`build.ps1 -Test` 编译程序并运行 `kok.Tests.exe` 原生用例。
- 桌面回归：隔离资料库 + 本地模拟服务 + 真实 WebView2，覆盖压缩配置/用量显示/手动压缩/摘要/会话条目、输入队列、会话分叉、审批、恢复、i18n 与基础功能。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 Agent 相关桌面套件。
- 本轮不发起真实模型请求，不使用真实 API 预算。

## 结果（2026-11-02）

- 版本契约：`node tools/check-version.cjs` 通过，`package.json` / `AssemblyInfo` / 浏览器扩展 / README 包名一致为 **0.10.4**。
- 原生检查：**762 项通过**（上一基线 736 项；新增 12 项压缩核心检查、7 项会话存储压缩检查与 7 项 Host 压缩检查）。
- 桌面回归：**11 个套件全部通过**——`agent`、`agent-kernel`、`agent-interactions`、`agent-workflow`、`agent-reliable`、`agent-quality`、`pi-agent-ui`、`agent-runtime`、`agent-platform`、`i18n`、`features`。
  - 运行记录（首批 5 个套件通过；agent-quality 在事件契约版本断言处暴露旧断言后修正）：`test-output/full-20261102-234038-604e039c`。
  - 修正后复跑记录（其余 6 个套件）：`test-output/full-20261102-234229-36c717ee`。
  - 关键断言：真实 usage 触发压缩（压缩请求自身 usage 不计入上下文基线）；`contextUsage` 显示实测来源、reserve/keep 与 percent；摘要 prompt 含六段标题与保留/移除边界；工具调用与结果无悬空配对；checkpoint 当前有效并可跨重载恢复；原生会话树出现 compaction 条目、投影以 `[UNTRUSTED SESSION SUMMARY]` 开头；运行中可点击压缩按钮产生 `context.compact_requested`；高级设置可往返 reserve/keep。
- 交付包：`dist/kok-0.10.4-win-x64.zip`，**30.66 MB**，SHA-256 `fccdf2adeae39883cd9ed2257a18fb158a9e949c1403817dec9f0b03551407f8`；产品版本与 `package.json` 一致。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 **4 个桌面套件全部通过**（agent、agent-platform、agent-workflow、i18n，含压缩与分叉场景），记录 `test-output/package-75f3f8bb16e94e0ca5ab8a531e5bf378/`（`archive.json` 保存套件列表、源 / 运行时指纹与 ZIP SHA-256）。

## 证据

- 桌面运行记录：`test-output/full-20261102-234038-604e039c/`、`test-output/full-20261102-234229-36c717ee/`。
- 交付包与校验和：`dist/kok-0.10.4-win-x64.zip` 及同名 `.sha256`；包级复验记录在 `test-output/package-75f3f8bb16e94e0ca5ab8a531e5bf378/`。
- 原生测试输出：`762 tests passed`（`build.ps1 -Test`，构建产物 `dist/kok`）。
- 更新测试：`tests/AgentPlatformTests.cs`（真实用量触发、切点/保留、手动压缩、溢出恢复一次）、`tests/AgentSessionTests.cs`（compaction 投影与 fork 重映射）、`tests/AgentKernelTests.cs`（手动压缩 action、contextUsage 快照、会话压缩条目写入）、`tests/agent-platform.cjs`（完整压缩桌面场景）。

## 边界

- 自动压缩需要至少 2 个剩余请求，手动与溢出恢复需要 1 个；摘要结果要求 20–6000 字符。
- 不实现 Pi 的“用户消息内切分”：始终保留首条用户目标消息，旧用户指令进入摘要；`stopReason=length` 与 overflow 共用一次恢复路径。
- 事件契约升级为 v3（新增 `context.compact_requested` / `context.recovery` 与真实用量字段），旧任务记录按原状态读取、不重写密文。
- 压缩摘要与上下文用量都以宿主判定为准；摘要标记为不可信，不代表授权；会话文件仍用当前 Windows 账户 DPAPI，不跨账户/机器迁移。
- 本版不改变 Agent 的审批、证据、撤销、沙箱与输入队列语义。

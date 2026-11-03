# v0.10.6 Preview · 验证记录

本版给 Agent 加入可视化分支树：工具栏「分支树」按父子顺序展示消息预览、当前分支、上下文摘要与标签；可在任意消息节点原地切换分支（界面历史从原生会话投影重建，后续任务沿该分支继续）、从任意节点分叉、重命名会话，`/tree` 快捷打开。本版补齐 M1（技能目录 + 会话树 UI）；审批、证据核对、撤销与沙箱语义未变。

## 验证范围

- 静态检查：`npm run check`（样式构建、界面脚本语法、模块架构契约、本地化目录、文档链接、版本契约、30 项任务样本离线校验）。
- 原生检查：`build.ps1 -Test` 编译程序并运行 `kok.Tests.exe` 原生用例。
- 桌面回归：隔离资料库 + 本地模拟服务 + 真实 WebView2，覆盖分支树/切换/重命名、技能、消息分叉、输入队列、压缩、审批、恢复、i18n 与基础功能。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 Agent 相关桌面套件。
- 本轮不发起真实模型请求，不使用真实 API 预算。

## 结果（2026-11-03）

- 版本契约：`node tools/check-version.cjs` 通过，`package.json` / `AssemblyInfo` / 浏览器扩展 / README 包名一致为 **0.10.6**。
- 原生检查：**800 项通过**（上一基线 791 项；新增 9 项会话投影 id、树条目预览与标签/压缩 reason 检查）。
- 桌面回归：**11 个套件全部通过**——`agent`、`agent-kernel`、`agent-interactions`、`agent-workflow`、`agent-reliable`、`agent-quality`、`pi-agent-ui`、`agent-runtime`、`agent-platform`、`i18n`、`features`。运行记录：`test-output/full-20261103-084222-66b0eac5/`（源/运行时指纹运行结束后校验一致）。分支树场景包含：消息预览与唯一当前节点、切换到首个用户消息后历史/状态重建、原生 leaf 一致、held follow-up 请求只含分支历史且只新增一个任务、运行中按钮禁用、内联重命名在 UI/原生/历史抽屉一致。
- 交付包：`dist/kok-0.10.6-win-x64.zip`，**30.68 MB**，SHA-256 `3e86d1a2c82852cf0d9343f25752437e2767daecc9fe92fb265fe263875f34e1`；产品版本与 `package.json` 一致。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 **3 个桌面套件全部通过**（agent、agent-workflow、i18n，含分支树场景）；记录 `test-output/package-704897bc3ea64c30a1708e6f1d94c020/`（agent-workflow，含分支树场景）与 `test-output/package-7702cb9a20b04b8d8c84168891179c9c/`（agent、i18n）。

## 证据

- 原生测试输出：`800 tests passed`（`build.ps1 -Test`，构建产物 `dist/kok`）。
- 桌面运行记录：`test-output/full-20261103-084222-66b0eac5/`。
- 交付包与校验和：`dist/kok-0.10.6-win-x64.zip` 及同名 `.sha256`；包级复验记录在 `test-output/package-704897bc3ea64c30a1708e6f1d94c020/` 与 `test-output/package-7702cb9a20b04b8d8c84168891179c9c/`。
- 更新测试：`tests/AgentSessionTests.cs`（ProjectionMessages id/摘要 id/保留边界 5 项）、`tests/AgentKernelTests.cs`（树条目 role/preview/label/reason、切换/投影 id 等 4 项）、`tests/agent-workflow.cjs`（分支树切换/重命名桌面场景）。

## 边界

- 切换分支只重建 user/agent 消息视图；其他分支的工具卡片不会重放，历史抽屉仍按原界面存储渲染。
- 本版不做 LLM 分支摘要：`branch_summary` 结构保留但未写入；离开分支的内容仍保存在会话文件中。
- `agentSessionTree` 的 role/preview/label/reason 与消息 `id` 均为 additive 字段；事件契约与任务/会话数据格式不变。
- 审批、证据核对、撤销与沙箱语义不变；私密资料库仍禁用 Agent 与会话操作。

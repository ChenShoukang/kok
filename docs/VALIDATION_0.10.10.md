# v0.10.10 Preview · 验证记录

本版开始创作侧第一阶段：**创作项目**（目标/要求/资料/相关会话/多个成果，跨会话继续）+ 版本历史原生底座。审批、证据核对、撤销与沙箱语义不变。

## 验证范围

- 静态检查：`npm run check`（样式、界面脚本语法、模块架构契约、本地化目录、文档链接、版本契约、30 项任务样本离线校验）。
- 原生检查：`build.ps1 -Test` 编译程序并运行 `kok.Tests.exe` 原生用例。
- 桌面回归：隔离资料库 + 本地模拟服务 + 真实 WebView2，覆盖创作项目创建/详情/编辑/继续创作（项目横幅、projectId、自动资料范围、成果面板）、既有 Agent 与作品/空间/演示/文档/i18n/基础功能。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行相关桌面套件。
- 本轮不发起真实模型请求，不使用真实 API 预算。

## 结果（2026-11-03）

- 版本契约：`node tools/check-version.cjs` 通过，`package.json` / `AssemblyInfo` / 浏览器扩展 / README 包名一致为 **0.10.10**。
- 原生检查：**862 项通过**（上一基线 843 项；新增 8 项创作项目检查与 11 项版本历史检查）。
- 创作项目：`SpaceRecord` 项目字段 + 校验；`ProjectLinkSession/ProjectLinkArtifact`（审计/可撤销/幂等）；`agentTaskStart projectId`（校验、≤4000 字项目简报稳定前缀、未指定范围时自动项目资料、会话回写、快照 projectId）；任务终态成果回写（去重、失败不影响任务）；8 项原生检查覆盖校验矩阵、链接幂等/撤销、简报注入、自动/显式范围、未知项目、成果回写。
- 版本历史：`ListVersions`（当前版 + 历史版本倒序、缺失/损坏条目标记 unavailable）、`ReadVersion`（前后/当前文本、hash、文本可用性；DOCX 提取）、`RestoreVersion`（补偿式操作、hash 校验、私密库拒绝、可撤销）；11 项原生检查。
- 桌面回归：**13 个套件全部通过**——`agent`、`agent-kernel`、`agent-interactions`、`agent-workflow`、`agent-reliable`、`agent-quality`、`pi-agent-ui`、`agent-runtime`、`agent-platform`、`i18n`、`features`，以及本轮直接相关的 `works-navigation`、`workspaces`。运行记录：`test-output/full-20261103-114756-c129899c/`（源/运行时指纹运行结束后校验一致）。其中 works-navigation 覆盖创作项目创建/卡片/详情/编辑目标/资料数，agent-workflow 覆盖继续创作→横幅→projectId+自动资料范围→成果面板→新建会话保留/清除。
- 交付包：`dist/kok-0.10.10-win-x64.zip`，**30.74 MB**，SHA-256 `3fdff4ec2e94e026f6f18161013d5e80108712ec4471406ee45513911580c7cc`；产品版本与 `package.json` 一致。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 **4 个桌面套件全部通过**（agent、agent-platform、agent-workflow、works-navigation），记录 `test-output/package-42a1c155b92240d4a92d8555fbe39178/`（`archive.json` 保存套件列表、源 / 运行时指纹与 ZIP SHA-256）。

## 证据

- 原生测试输出：`862 tests passed`（`build.ps1 -Test`，构建产物 `dist/kok`）。
- 桌面运行记录：`test-output/full-20261103-114756-c129899c/`。
- 交付包与校验和：`dist/kok-0.10.10-win-x64.zip` 及同名 `.sha256`；包级复验记录在 `test-output/package-*/`。
- 新增测试：`tests/KnowledgeWorkspaceTests.cs`（8 项创作项目检查）、`tests/DocumentTests.cs`（11 项版本历史检查）、`tests/works-navigation.cjs` + `tests/agent-workflow.cjs`（项目桌面场景）。
- 文档：[创作项目](CREATIVE_PROJECTS_0.10.10.md)。

## 边界

- 项目是 space 记录，「空间」页暂也会列出（后续按 kind 过滤）。
- 项目简报为宿主生成并标记不可信；不改变审批/证据/撤销/沙箱语义。
- 版本历史本轮只有原生 API：版本侧栏、文本 diff、恢复入口、手改保存前 diff 在下一轮接入。
- 事件契约、任务/会话数据格式保持向后兼容（新增字段为可选）。

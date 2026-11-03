# v0.10.5 Preview · 验证记录

本版给 Agent 加入 Pi / Agent-Skills 风格的技能目录：从 `ui/skills`、`%USERPROFILE%\.agents\skills` 与资料库 `.agents/skills` 递归发现 `SKILL.md`；提示词只注入名称/描述/位置，模型按需通过只读 `load_skill` 载入全文；Agent 页可列出并刷新已发现技能，`/skill:名称 参数` 可直接发起任务。同时加入内部生命周期钩子与工具执行模式缝（默认行为不变）。审批、证据核对、撤销与沙箱语义未变。

## 验证范围

- 静态检查：`npm run check`（样式构建、界面脚本语法、模块架构契约、本地化目录、文档链接、版本契约、30 项任务样本离线校验）。
- 原生检查：`build.ps1 -Test` 编译程序并运行 `kok.Tests.exe` 原生用例。
- 桌面回归：隔离资料库 + 本地模拟服务 + 真实 WebView2，覆盖技能发现/选择/`/skill` 命令、消息分叉、输入队列、压缩、审批、恢复、i18n 与基础功能。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 Agent 相关桌面套件。
- 本轮不发起真实模型请求，不使用真实 API 预算。

## 结果（2026-11-02）

- 版本契约：`node tools/check-version.cjs` 通过，`package.json` / `AssemblyInfo` / 浏览器扩展 / README 包名一致为 **0.10.5**。
- 原生检查：**791 项通过**（上一基线 770 项；新增 8 项生命周期钩子检查、13 项技能发现/索引/load_skill 检查与若干 Host 集成检查）。
- 桌面回归：**11 个套件全部通过**——`agent`、`agent-kernel`、`agent-interactions`、`agent-workflow`、`agent-reliable`、`agent-quality`、`pi-agent-ui`、`agent-runtime`、`agent-platform`、`i18n`、`features`。运行记录：`test-output/full-20261103-003523-0634979c/`（源/运行时指纹在运行结束后校验一致）。技能场景包含：临时 `SKILL.md` 刷新后出现在选择器、`/skill:name 参数` 只发送简短 load_skill 指令且原文不进入请求/transcript、任务数与请求数正确、清理后刷新消失。
- 交付包：`dist/kok-0.10.5-win-x64.zip`，**30.67 MB**，SHA-256 `5e95aa9842f0e3553ac769f06fd25a97ef50b7b44248101c09a11093791542e1`；产品版本与 `package.json` 一致。
- 交付包复验：`tools/test-package.ps1` 从 ZIP 运行 **3 个桌面套件全部通过**（agent、agent-workflow、i18n，含技能场景），记录 `test-output/package-c2fab9b97ffe4da1a4aaa608a0787edb/`（`archive.json` 保存套件列表、源 / 运行时指纹与 ZIP SHA-256）。

## 证据

- 原生测试输出：`791 tests passed`（`build.ps1 -Test`，构建产物 `dist/kok`）。
- 桌面运行记录：`test-output/full-20261103-003523-0634979c/`。
- 交付包与校验和：`dist/kok-0.10.5-win-x64.zip` 及同名 `.sha256`；包级复验记录在 `test-output/package-c2fab9b97ffe4da1a4aaa608a0787edb/`。
- 新增/更新测试：`tests/AgentSkillTests.cs`（发现、frontmatter、冲突顺序、禁用、XML 转义、缓存/reload、LoadBody、工具 schema）、`tests/AgentKernelTests.cs`（M0-4 钩子 8 项 + 技能 Host 集成）、`tests/agent-workflow.cjs`（技能选择器/刷新/`/skill` 桌面场景）。

## 边界

- 技能只读文本，不执行任何脚本；同名技能按 app → user → library 先到先得；最多 100 个技能、正文 20000 字符；`disable-model-invocation` 只从模型索引隐藏。
- app 根下的四个内置 workflow 也会进入提示词技能索引；选择器会隐藏与内置/自定义同名项以避免覆盖既有行为，`/skill:name` 仍可显式使用。
- M0-4 钩子为内部缝：钩子未注册时行为与旧版一致；`TerminateHint` 为预留字段，本轮不改变工具执行语义。
- 事件契约、任务/会话数据格式不变；审批、证据核对、撤销与沙箱语义不变。

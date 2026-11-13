# kok v0.14.0 Preview

本版落地通用 Agent 的第一阶段基础，继续使用 Deep Agents / LangGraph。运行时主体迁入 TypeScript，C# 保留 Windows 桌面、审批、资料范围与真实文件操作。

- **任务与资产协议**：版本化 TaskSpec 反映当前要求；资料和工作区产出带真实版本、来源、可用状态与项目关联。
- **公开检查点接口**：新状态通过 LangGraph 公开写入接口记录与恢复，支持 v0.13.0 审批检查点迁移。
- **后台命令作业**：在已配置工作区内，逐项批准明确的可执行文件和参数后运行。可查询、等待、停止，并查看真实输出、退出码和声明产出。结果在本机加密保存；重启不会自动重跑未完成命令。
- **真实能力目录**：研究、文档、PPT、照片、视频、音乐、代码、表格和电脑操作分别显示基础可用、部分可用、未配置或待接入。
- **兼容历史任务**：保留既有文件、编辑器和旧任务运行方式。

完整解压 `kok-0.14.0-win-x64.zip` 后运行 `kok.exe`，保留其中的 runtime 目录。无需另外安装 Node.js。命令执行需在“能力与权限”中配置并启用工作区；未启用系统沙箱时，命令具有当前用户权限。

这是基础重构版本。专业视频时间线、音乐生成与分轨、完整表格编辑、通用电脑操作等仍按规划推进，尚未作为完整 Agent 能力交付。

[验证与已知边界](VALIDATION_0.14.0.md) · [详细开发计划](PROJECT_PLAN_0.14_TO_1.0.md) · [版本记录](../CHANGELOG.md) · [帮助中心](help/index.md)

## English

This release establishes the general Agent foundation on Deep Agents / LangGraph. The runtime moves to TypeScript while C# retains Windows integration, approval, scope enforcement and actual file operations.

Versioned task and asset contracts reflect current requirements and actual files. New checkpoints use public LangGraph write interfaces and migrate v0.13.0 approval checkpoints. Individually approved background commands expose durable job IDs, local waits, cancellation, actual output and exit codes, and declared output assets. Results are encrypted locally; unfinished commands never replay automatically after restart.

The capability catalog reports actual support across nine domains, including music. Professional media, music generation, full workbook editing and general computer use remain future milestones. Extract the complete ZIP, including its runtime directory. No separate Node installation is required. Unsandboxed commands run with the current user's privileges.

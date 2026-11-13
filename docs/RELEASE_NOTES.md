# kok v0.15.0 Preview

本版重点是可验证的代码工作区流程与本地网页验证，继续使用 Deep Agents / LangGraph，运行时主体为 TypeScript。

- 加入工作区与环境识别、代码搜索、分段读取、Git 状态和差异读取。Python 商店占位符不再被误识别为实际解释器。
- 加入 SHA-256 和工作区身份保护的精确补丁，保留无关改动、UTF-8 BOM 与换行；预览后文件变化会拒绝覆盖，恢复时已应用的补丁不会重复写入。
- 代码验证绑定实际输入文件版本与退出码；文件在验证期间或验证后变化时，旧结果不能证明当前代码通过。
- 加入独立 WebView2 本地验证会话，支持页面观察、输入、选择、点击及后置条件等待；限制为批准的本机 HTTP 来源。
- 研究任务不再自动附加工作区命令；保存默认能力设置时保留按需选择工具。
- 修复命令输出读取可能阻塞桌面界面的问题；代码任务按需载入工具，减少无关资料库上下文。
- 补丁预览显示实际替换内容，作业和网页结果显示真实状态；完成后可直接定位修改文件与生成文件。
- 既有研究、文档、PPT 和旧任务继续保留。通用远程网页、桌面 UIA 与专业媒体引擎仍按后续计划推进。

完整解压 kok-0.15.0-win-x64.zip 后运行 kok.exe，保留 runtime 目录。先在“能力与权限”中配置并启用工作区。修改文件和运行命令需审批；未启用 AppContainer 时，外部命令具有当前用户权限。

浏览器当前仅用于 127.0.0.1 / [::1] 的本地 HTTP 应用验证，不复用个人浏览器会话。普通远程网站、通用桌面操作与自动下载尚未交付。

[实现、验证与边界](VALIDATION_0.15.0.md) · [详细规划](PROJECT_PLAN_0.14_TO_1.0.md)

## English

- Add project/executable discovery, bounded code search, paged UTF-8 reads and read-only Git status/diffs. Windows Store aliases no longer count as real Python interpreters.
- Add exact patches guarded by raw-byte SHA-256 and workspace identity. Preserve unrelated user changes, BOM and newlines, reject stale approval and avoid replaying an already-applied patch.
- Bind successful command checks to actual source versions and exit codes; changed inputs invalidate old verification.
- Add task-owned isolated WebView2 sessions for explicitly approved local HTTP apps, with fresh observations, fill/select/click and postcondition waits.
- Unrelated research tasks omit workspace command tools; saving default settings retains automatic tool selection.
- Fix a desktop deadlock while reading command output and reduce irrelevant library context in code tasks.
- Show exact patch previews and actual command/browser state, with direct access to changed files and generated outputs.
- Preserve existing research, document, PPT and legacy workflows. General remote websites, desktop UIA and professional media engines remain future milestones.

Extract the complete ZIP, including runtime. Configure a workspace before using code and local browser tools. Patches and commands require approval. Unsandboxed commands run with the current user permissions.

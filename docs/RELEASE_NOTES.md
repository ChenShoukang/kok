# kok v0.16.1 Preview

- 修复中途取消报告/PPT、切换聊天回复、只检查已有文件、修改输出格式和页数时的交付要求。已取消成果不再留下字数、页数或额外创建要求。
- 新要求取代且从未执行的提案不再阻塞当前交付。真实拒绝、已执行失败和结果不确定的操作仍需处理。
- 修复撤销待审批工具后接续模型的工具结果协议；完整返回旧提案的结果，再传入新要求，并从新模型工具列表移除已取消工具。
- 连续相同读取无进展时先提醒，持续重复则以 partial 保留工作；可以补充要求继续。分页、不同版本、写入、后台作业和网页观察不适用这一停止阈值。
- 仅在发给模型的请求中合并完全相同的成功回执，保留最新全文和可取回的原调用 ID；检查点与原生日志保持完整。
- 诊断界面补充模型/工具耗时、确认等待、首个保存成果和非缓存输入，仍不导出正文或凭据。耗时可能重叠，不能相加为总耗时。
- 详细范围与验证见 [v0.16.1 验证记录](VALIDATION_0.16.1.md)；后续任务见[近期执行计划](NEXT_PHASE_0.16_TO_0.18.md)和[总规划 3.0](PROJECT_PLAN_0.14_TO_1.0.md)。

完整解压 `kok-0.16.1-win-x64.zip` 后运行 `kok.exe`，保留 `runtime` 目录。Agent 业务使用 TypeScript 和既有 Deep Agents / LangGraph，Windows 桌面、权限、日志和本地工具保留 C#；本版未切换桌面壳。

实际渲染需本机已安装且空闲的 PowerPoint，每次批准最多 8 页。Office 不随包分发；不接管个人正在使用的会话，不支持宏、OLE 或外部资源包。`rendered=true` 只表示已有真实图片，`visualReviewed=false` 表示尚未完成视觉评审。结构检查不代表事实和专业视觉质量已经通过。

18 项确定性规划检查从原版的 11/18 到修复后的 18/18；这是需求判断回归，不是实际模型完成率或通用质量成绩。本版未宣称整体提效 20%，完整两轮真实任务评测仍在后续阶段。

照片、视频、音乐、表格和通用电脑操作继续按[详细计划](PROJECT_PLAN_0.14_TO_1.0.md)实施。

## English

- Fixes obsolete requirements after cancelling reports/decks, switching to chat, inspecting saved files or changing output specifications.
- Superseded proposals that never ran no longer block the new delivery. Actual rejection, failure and uncertain operations retain their checks.
- Completes pending native-tool responses before transmitting steering updates, while hiding retired tools from new model calls.
- Warns and then stops identical current-read loops with saved work retained. Explicit follow-ups can continue; paging, changed versions, writes, job polling and browser observations are exempt.
- Removes only exact duplicate successful receipts from the HTTP model view; original checkpoint and journal messages remain intact.
- Adds content-free timing and input-use diagnostics. Durations may overlap and cannot be summed.
- See [validation](VALIDATION_0.16.1.md), [next-phase execution](NEXT_PHASE_0.16_TO_0.18.md) and [migration boundaries](AGENT_TYPESCRIPT_0.16.0.md).

Extract the complete ZIP including runtime. The existing TypeScript kernel and C# native host remain. Rendering requires an installed, idle PowerPoint; Office is not bundled. Structural checks and rendered pages do not certify factual or visual quality. The 18-case planning regression improves from 11/18 to 18/18, but is not a live-model completion rate. No overall 20% efficiency improvement is claimed.

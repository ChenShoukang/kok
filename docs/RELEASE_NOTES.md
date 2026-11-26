# kok v0.16.3 开发候选（未发布）

按用户要求，先完成 [v0.16 整体收尾](V016_COMPLETION_CHECKLIST.md)再安排发布。下文为候选变更草稿，当前没有 v0.16.3 公开下载；最近公开版为 v0.16.2。

- 复用现有 LangChain 工具接口和 Zod，补齐自然表达中的文件格式、长度、自定义章节等交付要求；简单任务不强制增加一次模型规划。
- 需求增量引用当前用户原话，记录独立版本。补齐遗漏项可直接应用。涉及取消或替换已有交付时，界面先显示变更前后的要求，经用户逐项确认才生效。任务权限、额度和来源范围由宿主管理。
- 更新先持久保存，再继续执行。暂停或重启沿用同一要求和调用；尚未开始的旧提案失效，已执行或结果不确定的操作不自动重跑。
- 修复真实测试发现的临时文件交付问题：新默认任务使用原生成品工具；框架临时写入/编辑工具不能代替保存和回读。
- 已用 DeepSeek 实测三个定点任务：首轮 2/3，失败的 Word 表达题修复后原题复测 1/1，累计 23 次请求，保留失败。此结果不是完整重跑的 3/3，也不代表通用质量或效率目标已经达成。

已用桌面任务验证“取消报告、保留 8 页演示”。暂停后可以从同一确认卡片继续，最终可打开一份 8 页 PPTX。当前内核为 Deep Agents / LangGraph，Agent 业务为 TypeScript，Windows 桌面、权限、日志和本地工具使用 C#。[v0.16.3 验证记录](VALIDATION_0.16.3.md) · [开源复用路线](OPEN_SOURCE_REUSE_PLAN.md)

完整解压 `kok-0.16.3-win-x64.zip` 后运行 `kok.exe`，保留 `runtime` 目录。升级前退出程序并备份原资料库，继续使用原资料目录；旧任务保持其原要求机制。

PPT 实际渲染需本机已安装且空闲的 PowerPoint，Office 不随包分发。原文定位、结构检查和已渲染页面不等于事实与专业视觉质量已经通过。照片、视频、音乐、表格和通用电脑操作继续按[详细计划](PROJECT_PLAN_0.14_TO_1.0.md)实施。

## English

- Reuses existing LangChain tools and Zod to fill complex delivery gaps: actual file format, measured length and custom sections. Clear tasks need no mandatory extra planning request.
- Updates quote the latest user message and carry independent revisions. Canceling or replacing an active requirement opens a before-and-after preview for individual confirmation. The host controls permissions, budgets and source scope.
- Durable updates survive pause/restart with the same call. Only unstarted obsolete proposals are retired; executed or uncertain actions are not automatically replayed.
- Fixes a live-test failure where framework scratch files were presented as final Word delivery. New default tasks use native artifact tools with real saved-file read-back.
- Three targeted DeepSeek tasks: 2/3 in the first round, then 1/1 on the unchanged failing Word task after the fix; 23 cumulative requests including failure. This is not a complete 3/3 rerun or proof of general quality or efficiency.

The desktop test resumed the same pending confirmation after a pause and delivered one editable eight-slide PPTX after the user canceled the report. The kernel uses Deep Agents / LangGraph, with TypeScript agent business logic and a C# native desktop host. [Validation and limitations](VALIDATION_0.16.3.md) · [Reuse-first route](OPEN_SOURCE_REUSE_PLAN.md)

Extract the complete ZIP including runtime. Back up the existing library before upgrading and keep using its data directory. PowerPoint rendering requires an installed, idle Office instance; source locations and structural checks do not certify semantic or visual quality.

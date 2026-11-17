# kok v0.16.3 开发候选（未发布）

按用户要求，先完成 [v0.16 整体收尾](V016_COMPLETION_CHECKLIST.md)再安排发布。下文为候选变更草稿，当前没有 v0.16.3 公开下载；最近公开版为 v0.16.2。

- 复用现有 LangChain 工具接口和 Zod，补齐自然表达中的文件格式、长度、自定义章节等交付要求；简单任务不强制增加一次模型规划。
- 需求增量引用当前用户原话，记录独立版本；不能修改权限、额度或来源范围，也不能放宽已有验收或改掉明确的聊天交付/代码验证指令。
- 更新先持久保存，再继续执行。暂停或重启沿用同一要求和调用；尚未开始的旧提案失效，已执行或结果不确定的操作不自动重跑。
- 修复真实测试发现的临时文件交付问题：新默认任务使用原生成品工具；框架临时写入/编辑工具不能代替保存和回读。
- 已用 DeepSeek 实测三个定点任务：首轮 2/3，失败的 Word 表达题修复后原题复测 1/1，累计 23 次请求，保留失败。此结果不是完整重跑的 3/3，也不代表通用质量或效率目标已经达成。

本版是 OS01 的首批实现，复杂取消、全面语义替换和 Pi 内核对照继续按[开源复用路线](OPEN_SOURCE_REUSE_PLAN.md)推进。当前内核仍为 Deep Agents / LangGraph，Agent 业务为 TypeScript，Windows 桌面、权限、日志和本地工具保留 C#。实现和验证范围见 [v0.16.3 记录](VALIDATION_0.16.3.md)。

完整解压 `kok-0.16.3-win-x64.zip` 后运行 `kok.exe`，保留 `runtime` 目录。升级前退出程序并备份原资料库，继续使用原资料目录；旧任务保持其原要求机制。

PPT 实际渲染需本机已安装且空闲的 PowerPoint，Office 不随包分发。原文定位、结构检查和已渲染页面不等于事实与专业视觉质量已经通过。照片、视频、音乐、表格和通用电脑操作继续按[详细计划](PROJECT_PLAN_0.14_TO_1.0.md)实施。

## English

- Reuses existing LangChain tools and Zod to fill complex delivery gaps: actual file format, measured length and custom sections. Clear tasks need no mandatory extra planning request.
- Updates quote the latest user message and carry independent revisions. They cannot change permissions, budgets or source scope, relax active acceptance checks, or override explicit chat-only/code-verification directives.
- Durable updates survive pause/restart with the same call. Only unstarted obsolete proposals are retired; executed or uncertain actions are not automatically replayed.
- Fixes a live-test failure where framework scratch files were presented as final Word delivery. New default tasks use native artifact tools with real saved-file read-back.
- Three targeted DeepSeek tasks: 2/3 in the first round, then 1/1 on the unchanged failing Word task after the fix; 23 cumulative requests including failure. This is not a complete 3/3 rerun or proof of general quality or efficiency.

This is the first OS01 implementation, not full semantic replacement or adoption of the Pi runtime. The kernel remains Deep Agents / LangGraph, with TypeScript agent business logic and the C# native desktop host. See [validation and limitations](VALIDATION_0.16.3.md) and the [reuse-first route](OPEN_SOURCE_REUSE_PLAN.md).

Extract the complete ZIP including runtime. Back up the existing library before upgrading and keep using its data directory. PowerPoint rendering requires an installed, idle Office instance; source locations and structural checks do not certify semantic or visual quality.

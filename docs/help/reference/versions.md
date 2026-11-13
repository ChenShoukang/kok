# 版本与更新

这一页说明当前 kok 的版本状态、如何查看版本，以及怎样把已有数据带到新版本。

## v0.14.0 Preview

- TypeScript 通用运行时、版本化任务与资产协议；新检查点通过公开接口恢复，并兼容 v0.13.0 状态。
- 工作区后台命令需逐项批准，可查看真实完成状态、退出码与文件结果；中断后不自动重跑。
- 能力与权限中显示九类能力的实际状态，视频、音乐生成和电脑操作等按后续计划推进。

[本版验证与边界](../../VALIDATION_0.14.0.md) · [后续计划](../../PROJECT_PLAN_0.14_TO_1.0.md) · [发布页](https://github.com/ChenShoukang/kok/releases)

## v0.13.0 Preview

- 新任务接入 Deep Agents / LangGraph 研究内核，优先支持资料阅读、研究与内容创作；审批、实际文件操作、成品回读和用量限制继续由 kok 执行。
- 默认单 Agent，Node 运行组件随包提供，任务结束退出；等待审批的 SDK 状态可随任务日志恢复。
- 修复中文传输，以及“采用最终修订预算”误判为额外改稿的问题。旧任务保留原运行方式。

[内核说明](../../RESEARCH_KERNEL_0.13.0.md) · [本版验证](../../VALIDATION_0.13.0.md) · [发布页](https://github.com/ChenShoukang/kok/releases)

## v0.12.2 Preview

- 任务要求按消息顺序更新：后续消息可增加、替换或取消字数、页数、交付物、全文读取和改稿要求；“改成写报告”“先不要修改只分析”“取消字数限制”“只看开头”“改为全部资料”都生效。
- 工具组、提示与验收读取同一份当前要求：聊天回复改为保存文件后，创建工具会重新开放。
- 完成验收绑定最终交付物 ID 与版本：格式（DOCX/MD/TXT）、字数（字符/汉字/单词）、页数、回读在同一文件上同时成立；不同文件拼条件、旧版凑页数、只读一页、改稿后无回读都不再通过。
- PPT 页覆盖计入来源全文；约束保留区优先最终修订并提示旧版本可能作废。

[GitHub 发布页与下载](https://github.com/ChenShoukang/kok/releases/tag/v0.12.2) · [上一版说明](https://github.com/ChenShoukang/kok/releases/tag/v0.12.1)

## v0.12.1 Preview

- 中途改要求后，交付验收与可用工具按最新要求重新派生：8 页改 5 页按 5 页验收；追加“保存成文档”会开放创建工具。
- 交付验收支持多个成果并存、来源范围收窄与“只在聊天回复不建文件”；区分成果页数与来源页码；新增字数按去格式汉字计数与创建后回读充分性检查。
- `read_document` 与 `read_items` 统一精确覆盖：全文检查认可任一工具，长结果不再被二次裁中段，单条容量与多结果预算更可靠，较早来源的开头原文和约束句会保留。
- 评测口径拆分完整完成/部分完成/需要输入/失败；引用与“无不实声明”指标改名写明范围，并新增事实与禁止项检查。

[GitHub 发布页与下载](https://github.com/ChenShoukang/kok/releases/tag/v0.12.1) · [上一版说明](https://github.com/ChenShoukang/kok/releases/tag/v0.12.0)

## v0.12.0 Preview

- Agent 可以回读本任务刚创建的成果：创建、定点修改与结束能在同一任务内完成；重启、撤销和范围外读取仍按原边界校验。
- 长资料改为精确原文分页读取：`read_items` 返回字符区间、`has_more` 与 `next_offset`，并记录来源覆盖率；全文任务要读完全部来源才能标记完成。
- `finish_task` 的 completed 增加宿主交付验收：真实成果、演示文稿实际页数、创建/编辑后的当前回读与全文覆盖；未通过时只修失败项，已补救的失败不再阻断完成。
- 按报告、改稿、演示等目标自动选择工具组；默认请求上限保持不变。

[GitHub 发布页与下载](https://github.com/ChenShoukang/kok/releases/tag/v0.12.0) · [上一版说明](https://github.com/ChenShoukang/kok/releases/tag/v0.11.2)

## v0.11.2 Preview

- 空白的 Agent 首页和「我的作品」可以用示例创建一份笔记。练习在本机完成。[第一篇笔记](../getting-started/first-work.md)
- 「全部项目」支持按名称或目标搜索。在项目内可以添加成果并查找已经导出的副本。[继续创作项目](../works/continue-editing.md)
- 中文帮助中心补充了从资料到报告与 PPT 的教程。[创作项目教程](../getting-started/first-agent-project.md)

[GitHub 发布页与下载](https://github.com/ChenShoukang/kok/releases/tag/v0.11.2) · [上一版说明](https://github.com/ChenShoukang/kok/releases/tag/v0.11.1)

## 查看版本

打开 **设置 → 资料库 → 关于 kok**，可以看到 kok 版本、WebView2 版本和资料库位置。反馈问题时请附上这些信息。

## 更新方式

退出 kok 并备份当前资料库。下载并解压新 ZIP，使用原资料库启动新版本。默认资料库位于程序目录的 `data/`，另装到新目录时需要复制它。若使用了 `--data-dir`，启动时继续指向原目录。[安装 kok](../getting-started/install.md)

> [!warning]+ 更新前先备份
> `data/` 里是资料索引、笔记、进度和设置，不是可以随手重建的缓存。预览版仍在持续调整，更新前请保留 `data/` 的独立备份。

> [!info]+ Preview 预览版
> 当前版本是未签名的 Preview 预览版，功能和界面仍可能调整。发布包提供 SHA-256 校验文件，请从 Release 页面下载；重要资料请另外保留副本。

## 更新记录与验证

版本变化和本版验收范围分别见[版本记录](../../../CHANGELOG.md)与[v0.13.0 验证记录](../../VALIDATION_0.13.0.md)。

---

**相关**

- [v0.13.0 验证记录](../../VALIDATION_0.13.0.md)
- [版本记录](../../../CHANGELOG.md)
- [安装 kok](../getting-started/install.md)
- [帮助与支持](../help-and-support.md)

[返回帮助中心](../index.md)

# v0.13.0 研究内核与语言迁移

## 选择与实际实现

新任务使用 Deep Agents 1.16.4，底层 LangGraph 1.7.3。模型独立配置，DeepSeek 仍可使用。选择这个组合，是为了复用工具循环、上下文管理和可恢复状态，先把资料阅读、研究与内容创作的交付链路做好。[官方 JavaScript 概览](https://docs.langchain.com/oss/javascript/deepagents/overview)

当前架构是 C# 桌面宿主 + JavaScript / Node.js 研究进程，尚未完成整个主体的 TypeScript 迁移。发布包包含 Node.js 24、合并后的 worker、许可证和版本/哈希清单，不包含 node_modules。

| 层 | 当前实现 | 负责内容 |
| --- | --- | --- |
| 界面 | 现有 WebView2 / JavaScript | 输入、来源、审批、进度、作品与编辑 |
| 研究执行 | runtime/research/kernel.mjs、worker.mjs | SDK 模型与工具循环、上下文管理、SDK 检查点 |
| 进程端口 | src/AgentResearchKernel.cs | UTF-8 JSONL、进程生命周期、停止、宿主请求许可 |
| 任务适配 | src/AgentResearch.cs | 原生工具中断与回执、SDK 状态持久化、完成验收衔接 |
| 本地能力 | 现有 C# 服务 | 资料范围、审批、文件读写、版本、回读、撤销、预算与加密日志 |

## 控制流程

1. 新任务默认选择 research；历史日志没有 runtime 字段时仍按原生逻辑恢复。
2. 每次模型 HTTP 请求前，worker 向宿主申请许可，宿主核对请求数与费用上限后才允许发送。SDK 自动重试关闭，内部压缩请求也经过同一许可。
3. SDK 调用原生工具时产生 LangGraph 中断。宿主按现有规则读资料，或提出待审批的写操作。
4. 审批通过后宿主执行真实写入与回读，将工具结果和 hostReadBacks 一并恢复给 SDK。实际回读已充分时无需模型再发一次读取请求。
5. 模型最终回答交由宿主验收；只有实际交付物与当前要求满足时才完成。正式 update_plan / finish_task 不再是模型必填动作。

SDK 内部文件系统用于工作笔记，实际成果必须由 kok 原生文档/演示工具创建。资料、文件正文、摘要和工具结果都是待核对的数据，不构成新的操作授权。

SDK 状态使用压缩检查点，随原有加密任务日志保存，等待审批时可停止并恢复。检查点当前限制 512,000 压缩字节，序列化格式绑定 Node 主版本；跨主版本升级需要迁移检查点或创建新任务。

## 平衡资源与完成能力

默认一个主 Agent，关闭 SDK 通用子 Agent 和 shell。资料工具按任务选择；Node 按需启动，任务结束退出。先减少重复请求和额外流程，再考虑按任务引入专用子 Agent。

本地曾对 Pi Core 与 Deep Agents 做过相同 Node.js 24.18.0 环境下的 9 次独立进程加载比较，中位数如下。**这只测加载和最小初始化，不测模型延迟或任务完成率。**

| 指标 | Pi Core 1.5.2 | Deep Agents 1.16.4 |
| --- | ---: | ---: |
| 合并后的 JavaScript | 约 0.38 MiB | 约 2.59 MiB |
| 加载与最小初始化 | 约 40 ms | 约 135 ms |
| 进程 RSS | 约 57 MiB | 约 75 MiB |

两个方案都还需要同一个约 92.5 MB 的 Node 可执行文件，表内 JavaScript 大小不含它。Pi Core 本次仅用于比较，没有接入产品。这个数据不能证明哪个框架的实际任务效率最高；本版选择已有恢复和上下文能力较完整的方案，并通过精简默认能力控制资源。

## 主体以后能否换语言

可以。建议先把新研究运行时迁到 TypeScript，再逐步把研究工作流与任务编排迁过去；C# 先保留 Windows 窗口和成熟的本地文件能力。当前两个进程的接口给这条路线留出了边界。

迁移顺序：固定任务/工具/事件的版本化 JSON 契约；将 kernel.mjs、worker.mjs 转为 TypeScript 并加入类型检查；收拢任务状态与研究流程；用同一批审批、暂停恢复、中文传输和真实文件验收场景验证替换；最后再评估桌面宿主迁移。

Python 可以作为解析、OCR、检索和专业文档处理工具接入。若将主体改成 Python，仍需重新接好流式输出、审批中断、持久化、取消与用量限制；替换语言本身不会提高完成质量。

## 兼容与开发

- 新任务默认 research；设置 KOK_AGENT_RUNTIME=native 可运行兼容内核，显式 JSON 协议也走兼容路径。指定 runtime=research/deepagents 则明确选择 SDK。
- 旧任务、原生文件工具、验收与撤销仍依赖原代码，不能把旧模型循环所在文件整块删除。先隔离兼容职责，再逐步退出重复实现。
- 新运行时当前使用 SDK 默认压缩策略；旧内核的手动压缩和近期原文调节不是完整的 SDK 功能映射。高级能力仍需逐项迁移验证。
- 构建：powershell -NoProfile -ExecutionPolicy Bypass -File tools/compile-check.ps1 -Test。
- 运行时测试：npm run test:research；语法检查：npm run check:research；新桌面流程：npm run test:research-desktop。
- 依赖与许可证：参见根目录 THIRD_PARTY_NOTICES.md，以及发布包 runtime/research 中的 manifest.json 与 THIRD_PARTY_NOTICES.txt。

真实测试与限制见[本版验证记录](VALIDATION_0.13.0.md)。

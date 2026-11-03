# Agent 交互与界面对照

对照日期：2026-11-02。基于 v0.10.1 的界面改进，沿用现有原生任务引擎和资料库。
随后按用户指定切换为 [Pi Web UI 的界面移植](PI_AGENT_UI.md)；下表保留前一轮交互问题和修复依据。
参考对象为以下开源项目的实际前端源码；没有把仓库介绍当作已验证的产品表现。

## 参考与对应改进

| 开源来源 | 阅读的实现 | kok 原有差距 | 本轮改进 |
| --- | --- | --- | --- |
| LobeHub | [SendButton](https://github.com/lobehub/lobehub/blob/canary/src/features/ChatInput/SendArea/SendButton.tsx)、[resolveActionKeys](https://github.com/lobehub/lobehub/blob/canary/src/features/ChatInput/SendArea/resolveActionKeys.ts) | 执行中同时摆出补充、排队、停止三个文字按钮，主操作不明确 | 空闲时发送；执行中空输入只显示停止，有文字时显示补充与排队图标；空输入不能发送 |
| LibreChat | [ChatView](https://github.com/LibreChat-AI/LibreChat/blob/main/client/src/components/Chat/ChatView.tsx)、[ToolApproval](https://github.com/LibreChat-AI/LibreChat/blob/main/client/src/components/Chat/Messages/Content/ToolApproval.tsx) | 单项审批只有流中的卡片，容易滚过；输入和审批的状态不够清楚 | 单项与批量审批都有固定入口，可回到对应修改并聚焦确认按钮；实际确认继续由原生引擎校验 |
| LibreChat | [ActivityPhaseGroup](https://github.com/LibreChat-AI/LibreChat/blob/main/client/src/components/Chat/Messages/Content/ActivityPhaseGroup.tsx) | 计划、核对、请求计数等一直展开，挤占对话 | 当前活动压缩为一行；计划与诊断按需展开，用户展开后保留状态 |
| OpenHands | [group-events](https://github.com/OpenHands/OpenHands/blob/main/src/components/conversation-events/chat/group-events.ts) | 失败读取可能与成功步骤一起折叠 | 成功读取继续分组，失败步骤单独显示；成果与待确认修改保留清楚入口 |
| OpenHands | [pending-user-messages](https://github.com/OpenHands/OpenHands/blob/main/src/components/features/chat/pending-user-messages.tsx) | 发送尚未得到响应时缺少即时反馈；补充发送失败会丢失输入 | 即时显示已提交目标与工作状态；补充成功后才清空原输入，失败和过程中新增的草稿保留 |

上表是前一轮的模式参考；随后 Pi 的布局与交互实现已按 MIT 许可移植，并保留来源和许可。

## 界面组织

- 对话正文是视觉主体，辅助步骤使用轻量折叠行。待确认修改、失败和成果继续可见。
- 创建文档前展示内容，完成后把过程预览折叠，避免同一份正文重复占据对话；预览和撤销仍可操作。
- 会话使用可搜索的侧面板；宽窗口并列，窄窗口覆盖显示。关闭或 Escape 返回任务。
- 面板标识当前会话和草稿，恢复草稿不调用模型。任务运行时可以查看历史，切换操作说明并禁用。
- 待办直接显示在输入区中，可编辑、移除和查看顺序。停止任务保留待办，不继续自动执行。
- 重新打开有暂停待办的会话时展开待办，方便手动继续；仅恢复会话不会重放任务。
- 消息、侧栏、输入框、审批和结果沿用统一线条、间距和圆角；浅色近白，深色近黑，紫色仅用于强调。
- 输入框随内容增长，流式输出和重绘保留原输入节点、中文输入状态、光标和阅读位置；遵循减少动态效果。
- 资料库的观看、收听、阅读和“我的作品”保留独立入口。此改动不接入新的音乐、视频生成服务。

## 验证

`tests/agent-workflow.cjs` 在隔离资料库、真实 WebView2 与本地模拟模型服务中验证：
中文输入法、输入状态、单项审批、待办编辑/移除、补充请求失败、运行中查看历史、
草稿恢复、会话删除后的任务绑定清理、500–1440 px 浅深主题和减少动态效果。
截图写入 `test-output/agent-workflow`。

原有 Agent、会话恢复、任务核对、权限及首页布局测试继续覆盖已有行为。
这些模拟服务测试验证交互和引擎衔接，不代表真实模型能力或生成质量评估。

本轮桌面记录：新增工作流、交互、任务核对和首页在
`test-output/full-20261102-193224-b161b3c5` 的对应分组通过；旧测试随后按“展开执行详情”
的实际路径更新，能力与权限、MCP、主题、动画、作品导航及基础 Agent 在
`test-output/full-20261102-193425-f3c1e914` 全部通过。
会话与暂停队列恢复在 `test-output/full-20261102-192937-c4bdcbcf` 的 `agent-reliable` 分组通过。

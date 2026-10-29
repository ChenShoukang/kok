# Agent 交互改进

[English](AGENT_INTERACTIONS.en.md) · [返回 Agent](AGENT_0.7.2.md)

## 已实现

| 入口 | 行为 |
| --- | --- |
| 仅讨论 / 协作修改 | 仅讨论允许搜索与阅读；原生层过滤写入、创建与打开建议，并提示资料未改变。协作模式保留逐项确认。切换方式不丢弃同一资料范围的对话上下文 |
| 资料读取步骤 | 连续的只读工具合并成可折叠步骤，正在运行时默认展开，完成后默认折叠；手动展开状态保留。回答、修改卡片和成果保持可见 |
| 已读取来源 | 只展示本机读取结果返回的来源 ID，不信任模型自报链接。可打开原资料，也可展开实际发送给模型的节选；代表本任务读取过的证据，不保证每个结论都被证据支持 |
| 选择性执行 | 每项修改可勾选，批量只执行所选项；未选项继续等待，也可单独执行或跳过。分类、标签和笔记均提供修改前后预览 |
| 回答操作 | 支持安全的标题、列表、粗体与代码排版；HTML、脚本及远程图片按纯文本显示。复制保留原文。保存回答先选择名称与 Markdown / Word 格式，再新建本地文档，不产生额外 AI 请求 |
| 错误恢复与滚动 | 失败时可把原要求填回输入框修改，重新发送前不请求 API。向上阅读时不强制跟随回复，提供回到最新按钮。复选框和操作状态更新保留键盘焦点 |

“仅讨论”限制的是模型提出的操作，用户仍可主动保存回答。保存后的文件可从回答打开，并沿用原有新建文件撤销保护。纯净模式、私密文件夹、严格资料范围、会话加密、取消及用量限制沿用上一轮行为。

## 参考来源

- [Cline Plan & Act](https://docs.cline.bot/core-workflows/plan-and-act)：区分讨论和行动的意图；kok 采用仅讨论 / 确认修改，不自动授予写入权限。
- [Cline DiffEditRow](https://github.com/cline/cline/blob/main/apps/vscode/webview-ui/src/components/chat/DiffEditRow.tsx)：修改预览、局部展开与尊重阅读滚动。
- [OpenHands group-events](https://github.com/OpenHands/OpenHands/blob/main/src/components/conversation-events/chat/group-events.ts)：连续操作折叠，重要消息与成果留在主会话中。
- [assistant-ui Tool group](https://www.assistant-ui.com/elements/tool-group)、[Sources](https://www.assistant-ui.com/elements/sources)、[Reviewable diff](https://www.assistant-ui.com/elements/reviewable-diff)、[Thread](https://www.assistant-ui.com/elements/thread)：渐进展开、来源入口、选择性确认与稳定的输入和滚动。

以上为交互思路与源码结构参考；实现适配 kok 原有 C# / WebView2，没有移植这些项目的组件源码、图标或运行时依赖。

## 验证

`tests/agent-interactions.cjs` 使用隔离生成资料和本机模拟服务检查折叠及展开保持、真实来源节选、HTML 转义、复制替身、确认保存与 Word 回读、选择执行、仅讨论原生过滤、任务方式恢复、错误修改不发请求、主题与 500～1280 px 窗口、键盘焦点、滚动入口、中英文。`tests/agent-reliable.cjs` 继续检查取消、费用、会话与私密边界。没有调用真实商业模型。

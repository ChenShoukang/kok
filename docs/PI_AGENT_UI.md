# Pi Agent 界面移植

按用户指定，以 Pi 的 Web UI 为本轮主要界面来源。

## 固定来源与实际复用

原仓库为 [earendil-works/pi](https://github.com/earendil-works/pi)。上游已移除 `packages/web-ui`，
本轮固定使用移除前的提交 `559a71683d93ac5ff110e89ca0966bdd2a7eeccd`，没有把历史组件称为上游当前发布功能。

| 来源文件（固定版本） | kok 中的移植 |
| --- | --- |
| [AgentInterface.ts](https://github.com/earendil-works/pi/blob/559a71683d93ac5ff110e89ca0966bdd2a7eeccd/packages/web-ui/src/components/AgentInterface.ts) | 居中的消息列、固定底部输入、稳定消息与流式更新分离 |
| [MessageEditor.ts](https://github.com/earendil-works/pi/blob/559a71683d93ac5ff110e89ca0966bdd2a7eeccd/packages/web-ui/src/components/MessageEditor.ts) | 单层输入表面，附件与设置在底部，紧凑发送/停止；移植中文输入保护与 Escape 中断处理 |
| [Messages.ts](https://github.com/earendil-works/pi/blob/559a71683d93ac5ff110e89ca0966bdd2a7eeccd/packages/web-ui/src/components/Messages.ts) | 靠左的用户消息、平整的回答与工具结果卡片、12 px 消息间距 |
| [ChatPanel.ts](https://github.com/earendil-works/pi/blob/559a71683d93ac5ff110e89ca0966bdd2a7eeccd/packages/web-ui/src/ChatPanel.ts) | 对话与成果并列，各占一半；800 px 以下使用覆盖面板；新成果出现时自动展开 |
| [MarkdownArtifact.ts](https://github.com/earendil-works/pi/blob/559a71683d93ac5ff110e89ca0966bdd2a7eeccd/packages/web-ui/src/tools/artifacts/MarkdownArtifact.ts) | 成果标签、预览/原文切换、复制与单独打开 |

对应实现为 `ui/pi-agent.css`、`ui/modules/pi-agent-view.mjs` 与 `ui/modules/agent-runtime.mjs`。
将 Lit 模板和 Tailwind 布局转换为 kok 的 DOM 与 CSS，适配已有原生任务事件。
版权与 MIT 许可见 [Pi-MIT.txt](licenses/Pi-MIT.txt) 和 [第三方声明](../THIRD_PARTY_NOTICES.md)。

## kok 的内容与操作

- 新建或修改的 MD/TXT/DOCX/PPTX 成果可在 Agent 右侧直接查看；有多份成果时通过标签切换。
- 预览读取原生 `documentSource` 返回的实际文件或恢复草稿，不把模型的提议当作已保存的成果。
- 支持预览/原文、复制、重新读取、进入既有编辑器和独立阅读器；重新打开已有会话时保持面板收起。
- 较长正文在面板中显示有界片段，并提供打开完整文档入口。Markdown 以安全文本渲染，不执行文档中的 HTML。
- 800 px 以下覆盖显示成果，主对话暂时不接收操作；关闭或 Escape 返回原任务。宽窗口可一边看成果一边继续输入。
- PPT 成果支持逐页预览、大纲、备注与指定页面反馈；待确认的 PPT 提案也可先预览，再返回任务确认。原生创建、修改与撤销见 [创作流程](AGENT_CREATIVE_WORKFLOW.md)。
- 音视频内容继续使用已有播放器与编辑器，不模拟尚未实现的媒体生成能力。
- 接收结果、审批、撤销、私密状态和文件校验继续由现有原生实现处理。界面操作不新增模型请求。
- 保留更白的浅色和更黑的深色，用户字体与配色偏好继续生效。
- 按用户后续要求改为纯扁平表面，移除对话、工具、输入与成果面板的浮层阴影和大圆角。流式更新与翻页保留面板焦点，避免重播进场动画。

验证使用隔离资料库、真实 WebView2 和本地模拟模型服务，截图在 `test-output/pi-agent-ui`。

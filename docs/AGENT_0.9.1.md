# Agent v0.9.1：可看、可联网、可执行、可安排的受控工作台

[项目首页](../README.md) · [English summary](#english-summary) · [v0.8.2 Agent 与 MCP](AGENT_0.8.2.md) · [v0.9.0 资料库指南](LIBRARY_0.9.0.md) · [验证](VALIDATION_0.9.1.md)

v0.9.1 在保留"本地优先、逐次审批、证据可核对"的前提下，为 Agent 增加八类能力。所有外部动作默认关闭或需要逐次确认；真实模型调用总账仍是 150 次，本版没有新增真实评测调用。

## 1. 视觉输入

- 模型可用 `read_image` 读取允许范围内的图片、截图、图表与扫描件：本机缩放到最长边 1280 像素、JPEG 约 1.2 MB 以内，以 OpenAI 兼容的 `image_url` 内容块随对话发送；图片原始字节不写入加密任务日志。
- 需要在 **设置 → AI 服务 → 联网与视觉** 中开启"模型支持图片输入（视觉）"，并且所选模型确实支持图片。未开启或模型不支持时自动回退 Windows OCR 文本。
- 图片文件变化后旧视觉证据失效；每个任务最多同时携带最近 2 张图片，避免上下文无限增长。
- 边界：视频帧与 PDF 页面直接视觉渲染尚未内置（可用截图/OCR 代替），不做图像生成或图像语义索引。

## 2. 联网读取

- `fetch_url` 与 `web_search` 都是**审批型工具**：每次网络请求都显示完整 URL/查询并等待确认，讨论模式与只读任务不可用。
- 抓取只允许公开 HTTPS 地址（本机回环需显式测试开关），拒绝内网、链路本地、保留地址、含凭据 URL；最多 3 次重定向、2 MB 响应、20 秒超时、仅文本/HTML/JSON。
- 正文用 **SmartReader**（Mozilla Readability 的 C# 移植）提取，失败时用 AngleSharp 纯文本回退；返回最终 URL、标题与站点，便于引用。
- `web_search` 需要用户填写 SearXNG 兼容的 JSON 搜索地址；不内置第三方搜索密钥，也不自动向搜索服务发送资料范围内容。

## 3. 受控执行环境

- 在 **Agent → 能力与权限** 中选择一个已存在的工作目录并开启执行后，Agent 才能使用 `list_folder`、`read_text_file`（自动）与 `write_text_file`、`run_command`（逐次审批）。
- 只允许工作目录内的相对路径；拒绝绝对路径与 `..` 穿越。读取上限 256 KB、写入上限 20 万字符、输出上限约 32 KB、命令超时 1–120 秒；命令不经过额外 shell，并以 Job Object 管理进程树。
- 边界：命令与外部 MCP 程序以当前 Windows 账户权限运行，**没有操作系统级沙箱**；关闭执行或未选择目录时相关工具完全不可用。

## 4. 长任务、计划任务与通知

- 任务上限提高到 24 轮 / 24 次请求，上下文档位 6k/14k/24k/48k/64k，资料上下文上限最高 128k，单请求输出最高 8k token。
- 新增长期计划：`agentScheduleList/Save/Delete` 与"计划任务"对话框，使用 cron 表达式（如 `0 8 * * *`），最多 20 条，可保存目标、资料范围、请求上限与档位。计划只负责启动**普通任务**，仍走审批、范围与预算限制；应用需要保持运行。
- 任务完成、部分完成、需要补充、失败、达上限或需重新核对时，窗口不在前台会显示托盘通知；`--test-silent` 测试模式自动跳过。

## 5. 记忆增强

- `search_memory` 在原有词面检索之上增加**本机 embedding 语义召回**（复用资料库 embedding 配置），向量缓存在本机用 DPAPI 加密，模型不可用时自动回退词面。
- 近似重复的已批准记忆会合并到已有记录（同来源、规范化文本一致），避免重复条目；来源变更或撤销后不再召回。
- 记忆写入仍需用户批准（`remember_memory`），可编辑、删除与撤销；记忆永远是"不可信资料"，不能授予权限。

## 6. 并行编排

- 新工具 `delegate_parallel`：一张审批卡启动 2–3 个**并行只读子代理**，各自收到明确子目标与同一资料范围；子代理不能写、不能用 MCP、不能运行代码、不能继续派生子代理。
- 子代理共享父任务请求预算，报告汇总后父任务仍需按原有证据规则完成核对；失败/不完整子任务不会作为成功证据。

## 7. 上下文与输出

- 预算估算改为 **SharpToken**（tiktoken 的 C# 移植，cl100k_base）+ 1.4 倍保守安全边际 + 历史校准；模型返回的真实 token 用量仍单独记录。
- `update_plan`/`finish_task` 的证据分层（历史执行 vs 当前内容）与自动回读保持不变；语义压缩 checkpoint 与 `recall_result` 精确回查继续可用。

## 8. 远程 MCP（生态连接）

- MCP 设置支持 **远程 Streamable HTTP**：HTTPS 地址 + 可选 Bearer 令牌（DPAPI 加密），支持 JSON 与 SSE 响应、会话 ID 与工具目录变更检测。
- 复用现有逐工具审批、受限 JSON Schema 与"结果未知不重放"规则；外部调用仍逐项确认。不包含 OAuth、资源/提示词协议与自动安装。
- 原有本地 stdio MCP 客户端与只读 MCP server 行为不变。

## 安全与验证边界

- 所有新工具默认关闭或需要审批；工具结果统一标记为不可信资料；失败/跳过/撤销不能作为成功证据。
- 未实现：OAuth、视频帧/PDF 页面视觉渲染、跨平台/移动端/云同步、插件市场、操作系统级沙箱、多人协作。
- 本版为确定性验证：**494 项原生检查 + 11 组桌面套件**（详见[验证页](VALIDATION_0.9.1.md)）；没有新增真实模型调用，不能把确定性回放换算成模型成功率。

## English summary

kok v0.9.1 extends the local-first Agent with reviewed vision input (`read_image`, image-only models with OCR fallback), approval-gated `fetch_url`/`web_search` using SmartReader extraction, an opt-in workspace with bounded file tools and `run_command`, longer tasks (24 rounds/requests, larger context/output), cron schedules and tray notices, hybrid semantic memory recall with near-duplicate merging, one-card parallel read-only subagents, tiktoken-based budgets, and remote MCP over Streamable HTTP with an encrypted bearer token. All external actions remain explicitly approved or default-off; no additional real-model calls were made (ledger stays at 150). Deterministic coverage: 494 native checks and 11 desktop suites; see [validation](VALIDATION_0.9.1.md).

# kok v0.9.1 Preview

这一版把 Agent 从"受控知识工作"扩展到**可看、可联网、可执行、可定时、可并行、可语义记忆**的本地工作台，同时保持本地优先、逐次审批和证据可核对。原有媒体、阅读、笔记、空间、创作与检索能力不变。

- **视觉输入**：`read_image` 把库内图片/截图缩放后作为视觉输入交给支持图片的模型，未开启或模型不支持时回退 Windows OCR。默认关闭，可在 AI 设置中开启。
- **联网读取**：`fetch_url` / `web_search` 逐次审批；HTTPS、拒绝内网与保留地址、重定向/大小/类型限制；SmartReader 抽取正文并返回来源 URL/标题；搜索支持自配 SearXNG 兼容端点。
- **受控执行环境**：可选工作目录内的文件读取/写入与命令执行，默认关闭、逐次审批；相对路径与大小/超时/输出限制，命令以 Job Object 管理进程树。
- **长任务与自主**：24 轮 / 24 次请求、64k 上下文档位、128k 上下文设置与 8k 输出；NCrontab cron 计划任务（最多 20 条）与完成托盘通知。
- **记忆增强**：本机 embedding 语义召回 + 词面融合、DPAPI 加密向量缓存、近似记忆合并、来源失效；记忆仍需用户批准。
- **并行编排**：`delegate_parallel` 一次审批启动 2–3 个只读子代理，共享父任务预算，不能写/MCP/递归，报告汇总后仍由父任务核对。
- **上下文与预算**：SharpToken（tiktoken）估算 + 保守安全边际 + 历史校准，checkpoint 压缩与 `recall_result` 精确回查。
- **远程 MCP**：Streamable HTTP + Bearer 令牌（DPAPI 加密），复用逐工具审批与"结果未知不重放"规则；不含 OAuth。
- **依赖与许可**：SmartReader、AngleSharp、NCrontab、SharpToken 等开源库已锁定 NuGet/DLL 校验和，随包携带许可与 NOTICE。

本版通过 **494 项原生确定性检查**与 11 组桌面套件（详见验证页）。**没有新增真实模型调用**，总账仍为 150 次；不把确定性回放换算成模型成功率。

下载 `kok-0.9.1-win-x64.zip` 并核对随附 SHA-256，完整解压后运行 `kok.exe`。需要 Windows 10/11 x64、.NET Framework 4.8 与 WebView2；运行无需 Node.js。升级前备份 data 与原文件。

[Agent 能力与边界](AGENT_0.9.1.md) · [验证](VALIDATION_0.9.1.md) · [资料库指南](LIBRARY_0.9.0.md) · [Roadmap](../ROADMAP.md) · [Changelog](../CHANGELOG.md)

## English

kok v0.9.1 extends the local-first Agent with reviewed vision input, approval-gated web fetch/search, an opt-in bounded workspace (files and commands), longer tasks with schedules and tray notices, hybrid semantic memory recall, one-card parallel read-only subagents, tiktoken-based budgets and remote MCP over Streamable HTTP with an encrypted bearer token. All external actions remain explicitly approved or default-off.

Deterministic coverage: 494 native checks and 11 desktop suites. No additional real-model calls were made (ledger stays at 150); no model success-rate claim is made for this build.

Extract the full ZIP and run kok.exe after checking SHA-256. Requires Windows 10/11 x64, .NET Framework 4.8 and WebView2; no Node.js runtime. Back up data/originals before updating. Commands and external MCP servers run with the current user account; there is no OS-level sandbox. Vision requires a model that accepts image input; web search requires a SearXNG-compatible endpoint.

Unsigned Preview. See [Agent capabilities and limits](AGENT_0.9.1.md) and [validation](VALIDATION_0.9.1.md).

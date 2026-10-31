# v0.9.1 验证与发行记录

[项目首页](../README.md) · [v0.9.1 Agent 能力与边界](AGENT_0.9.1.md) · [English summary](#english-summary)

验证日期：2026-10-31（Asia/Taipei）。本版在 v0.9.0 基础上扩展 Agent 能力；既有资料库、检索、采集、导出恢复与媒体功能保持原样。真实模型请求总账仍为 **150 / 150**，本版没有新增真实模型调用。

## 本机确定性验证

- 原生回归：**494 项通过**（v0.9.0 的 426 项 + 视觉 8 + 联网 11 + 执行 14 + 计划/上限 11 + 记忆 6 + 编排 5 + tokenizer 1 + 远程 MCP 11 + 新限制相关调整）。
- `npm run check`、Windows PowerShell 5 脚本校验、源码隐私审计、Git 历史隐私审计与依赖审计通过。
- 新增依赖全部锁定 NuGet SHA-256 与 DLL SHA-256，并随包携带许可与 NOTICE：
  - SmartReader 0.11.1（Apache-2.0，网页正文提取）
  - AngleSharp 1.5.2（MIT，HTML 解析）
  - System.Text.Encoding.CodePages 10.0.10（MIT，编码支持）
  - NCrontab 3.4.0（Apache-2.0，cron 解析）
  - SharpToken 2.0.6（MIT，tiktoken 预算估算）
- 本机发行 ZIP：`kok-0.9.1-win-x64.zip`，26.61 MB（27,906,974 bytes），SHA-256 `6141185411e6cd456a688f233aa894a75163f8b3f4fd13bd619330d73e76146c`。
- 桌面套件：**11 / 11 通过**（ui、features、agent、agent-interactions、agent-reliable、agent-runtime、agent-kernel、mcp、agent-platform、agent-quality、library-services），针对上述 ZIP 解压后的 `kok.exe` 运行。
- 包级记录：[package-0.9.1-local.json](validation/package-0.9.1-local.json)。源文件指纹 `1c3f357daf4eeac2e3f7b9fd1088b5ef00bdac010dfd7b59e605bb7f3411ae2c`（417 个源文件），运行时文件指纹 `51bfe0c2512bafe73b9220d20bdf5d346ace00b40fdc32b00793e4c286e0bfcf`（361 个文件），`kok.exe` SHA-256 `d6d38ec69b0c8e6e8e88f27fc7a0157a800b80e0a6c3abc893a3b888f37682e9`。

## 覆盖的确定性回归（新增部分）

| 领域 | 覆盖内容 |
| --- | --- |
| 视觉 | 大图缩放与大小上限、读取回执、图片消息构造、文件变更失效、OCR 回退、范围校验 |
| 联网 | 回环默认拒绝、HTTPS/内网/保留地址、重定向、超大响应、非文本拒绝、搜索 JSON 解析 |
| 执行 | 工作目录启用、读写与命令审批、路径穿越/绝对路径、二进制与大文件、超时终止、预算 |
| 长任务 | 24 轮/请求上限、档位 schema、cron 校验与触发去重、20 条上限、持久化 |
| 记忆 | 语义召回、词面回退、近似合并、来源失效、embedding 失败回退 |
| 编排 | 2–3 个子目标校验、审批前零请求、并行活跃度、父预算共享、报告汇总 |
| 预算 | tiktoken 估算、保守边际、历史校准、48k/64k 档位贯通 |
| 远程 MCP | 端点策略、旧版握手、目录、Unicode 调用、会话、鉴权失败、加密落盘、审批链路 |

## 真实模型评估边界

150 次总账来自 v0.9.0 的 30 例与定向复验，本版**没有运行新的真实模型评测**，也不复用旧成功率作为本版成绩。`29/30` 仍只是预期，不是已验证结果；确定性回放与 fixture 模型不能换算为模型成功率。

## 未实现与边界

OAuth MCP、视频帧/PDF 页面直接视觉渲染、跨平台/移动端/云同步、插件市场、操作系统级沙箱与多人协作不在本版范围。命令和外部 MCP 程序以当前用户权限运行；视觉依赖所选模型支持图片输入；联网搜索需要用户自备 SearXNG 兼容端点。

## 发布记录

云端 Windows 2022 / 2025 构建、原生检查、隐私与依赖审计、待发布 ZIP 的 11 组桌面套件全部通过后，v0.9.1 已于 2026-10-31T11:01:01Z 发布为 Preview：

- Release：https://github.com/ChenShoukang/kok/releases/tag/v0.9.1（run `41194469834`，tag/commit `cffdeb3`）。
- 云端 11 / 11 桌面套件通过；云端 ZIP `kok-0.9.1-win-x64.zip` 为 **28,203,332 bytes**，SHA-256 `5ec43c1f05651680583ff5ccb32a0455bbeebe89955cd32c007df37cb693083e`。
- 独立下载后重新计算 SHA-256、核对随附 `.sha256` 与 GitHub asset digest 均一致（`checksumPassed: true`）；核验记录见 [release-0.9.1-download.json](validation/release-0.9.1-download.json)。

## English summary

Deterministic coverage includes 494 native checks and 11 desktop suites on the release ZIP. Five new open-source dependencies are checksum-pinned with licenses. No additional real-model calls were made (ledger 150); no model success-rate claim is made for this build. Limits: no OAuth, no direct video/PDF visual rendering, no cross-platform/cloud sync, no OS-level sandbox.

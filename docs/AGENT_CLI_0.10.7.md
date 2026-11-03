# Agent 无界面 CLI（v0.10.7）

[项目首页](../README.md) · [Agent 首页与交互](AGENT_HOME_UI.md) · [本次验证](VALIDATION_0.10.7.md)

`kok-agent.exe` 在不打开窗口的情况下运行一次只读 Agent 任务，适合脚本、CI、批处理与外部工具。它复用桌面端相同的原生任务引擎、加密任务记录、检索与证据核对；**不会**打开 WebView，也不会弹出审批卡片。`kok.exe agent …` 会进入同一实现，但 `kok.exe` 是窗口子系统程序，`cmd`/PowerShell 不会等待它结束；需要可靠退出码与顺序输出时请使用 `kok-agent.exe`。

## 前置条件

- 已在 kok 中启用**智能模式**并配置可用的 AI 服务（与桌面端共用 `aiConfig`，密钥不打印）。
- 只读模式：本版无界面入口是**只读**的（检索、读取、整理建议、回答问题）；写入仍回到桌面端逐卡审批，`--approval` / `--allow-writes` 会被拒绝。
- 私密文件夹不启用 AI，CLI 同样拒绝。

## 用法

```powershell
# 条条输出：只打印最终回答
.\kok-agent.exe --goal "总结所选资料的主要观点" --scope selected --ids <id1,id2> --print

# JSONL：header -> event -> result
.\kok-agent.exe --goal "列出最近三天的视频" --json

# 独立资料库 + 限制预算
.\kok-agent.exe --goal "对比两版方案" --data-dir "D:\kok-profile" --budget 14000 --request-limit 6 --context-tokens 32000 --json
```

参数：

| 参数 | 说明 |
| --- | --- |
| `--goal "<1..2000 字>"` | 任务目标（必填） |
| `--print` / `--json` | 二选一：最终文本 / JSONL 事件流 |
| `--data-dir <dir>` | 资料库目录；省略时使用程序目录下的 `data`（与桌面默认一致） |
| `--scope all\|selected\|collection` | 资料范围，默认 `all` |
| `--ids <32hex,...>` / `--collection <名称>` | `selected` / `collection` 范围参数 |
| `--budget 6000\|14000\|24000\|48000\|64000` | 任务预算，默认 14000 |
| `--request-limit 1..24` | 模型请求上限，默认 6 |
| `--context-tokens 8000..128000` | 上下文窗口预算，默认 32000 |
| `--protocol auto\|tools\|json` | 工具协议，默认 `auto` |
| `--session <32hex>` / `--title <text>` | 复用/命名会话记录（不加载历史对话） |
| `--timeout-seconds 1..3600` | 超时后停止任务 |
| `--no-memory` | 保留参数；无界面任务始终不加载长期记忆 |
| `--help` | 用法 |

## 输出

- `--print`：stdout 只有最终回答文本 + 换行；诊断与错误在 stderr。
- `--json`：UTF-8 JSONL，每行一个对象：
  1. `{"type":"header","version":1,"taskId","sessionId","model","goal"}`
  2. `{"type":"event","taskId","event":{id,sequence,kind,created,version,data}}`（按序、不重复）
  3. `{"type":"result","taskId","status","reply","usage":{inputTokens,outputTokens,cachedTokens,cost,requests},"verification":{status,summary}}`
  - 参数/启动错误：最后一行 `{"type":"error","message"}`。

## 退出码

| 码 | 含义 |
| --- | --- |
| 0 | `completed` |
| 2 | `needs_input` / `review_required` / `partial`，以及参数错误、非智能模式、启动失败 |
| 1 | `cancelled` / `stopped` / `failed` / `limited`，以及超时（任务被停止） |
| 130 | 用户中断（Ctrl+C，任务已停止并 flush） |

## 其他模式

- 长期运行的 JSONL 命令循环（prompt/steer/follow_up/abort/state/compact/shutdown）：`kok-agent.exe --rpc`，协议见 [Headless RPC](AGENT_RPC_0.10.9.md)。
- `kok-agent.exe --print/--json` 的只读边界同样适用于 RPC：写入仍回到桌面逐卡审批。

## 边界

- 只读：不执行写入、审批或 MCP 写工具；结构化结果以事件/结果行返回。
- 不加载该会话的历史对话（`sessionLeafId` 为空、history 为空）；如需延续上下文，请回到桌面端分支树。
- `prompt_cache_key` 等缓存设置与桌面端共用（见 AI 服务设置）。
- 输出不含 API 密钥/接口地址；任务记录仍加密保存在资料库目录。

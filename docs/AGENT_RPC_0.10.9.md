# Headless RPC 模式（v0.10.9）

[项目首页](../README.md) · [Agent 无界面 CLI](AGENT_CLI_0.10.7.md) · [本次验证](VALIDATION_0.10.9.md)

`kok-agent.exe --rpc` 启动一个长期运行的只读 Agent 进程：stdin 逐行读取 JSON 命令，stdout 逐行输出响应、事件与结果。适合编辑器、CI 驱动、外部 UI 与自动化工具；与 `--print/--json` 使用同一套原生任务引擎、加密记录、检索与证据核对，不打开窗口/WebView。

## 启动

```powershell
.\kok-agent.exe --rpc --data-dir "D:\kok-profile" --budget 14000 --request-limit 6
```

- 只读策略（与 CLI 相同）；`--approval` / `--allow-writes` 被拒绝；`--rpc` 不能与 `--goal`/`--print`/`--json` 同时使用。
- 同一进程同时只允许一个任务；`--rpc` 支持与 `--json` 模式相同的范围/预算/上下文/协议/超时参数。

## stdin 命令（每行一个 JSON）

| 命令 | 说明 |
| --- | --- |
| `{"id":"1","method":"ping"}` | 返回版本、pid 与当前 taskId |
| `{"id":"2","method":"prompt","params":{"goal":"…","scope":"all"}}` | 启动任务，立即返回 `{taskId,sessionId}`；运行中再次 prompt 返回错误 `已有任务在运行。` |
| `{"id":"3","method":"steer","params":{"taskId":"…","text":"…"}}` | 运行中插入 steering（与界面 Enter 等价） |
| `{"id":"4","method":"follow_up","params":{"taskId":"…","text":"…"}}` | 排队 follow-up（任务自然结束时继续同一任务） |
| `{"id":"5","method":"abort","params":{"taskId":"…","cancel":true}}` | 停止任务 |
| `{"id":"6","method":"state","params":{"taskId":"…"}}` | 返回任务快照（状态/usage/queue/phase 等） |
| `{"id":"7","method":"compact","params":{"taskId":"…","instructions":"…"}}` | 请求手动压缩（运行中的任务） |
| `{"id":"8","method":"shutdown"}` | 停止任务并退出（0） |

未知命令返回 `{"type":"response","id":…,"ok":false,"error":"未知命令。"}`；无法解析的行返回 `{"type":"error","message":"命令格式无效。"}` 并继续服务。

## stdout 事件

- 响应：`{"type":"response","id":"…","ok":true,"result":{…}}`
- 事件：`{"type":"event","taskId":"…","event":{"id","sequence","kind","created","version","data"}}`（按序、不重复）
- 结果：`{"type":"result","taskId":"…","status":"completed","reply":"…","usage":{…},"verification":{…}}`
- 所有输出行在同一把锁内串行写出，不会与响应交错。

## 退出码与生命周期

| 码 | 含义 |
| --- | --- |
| 0 | `shutdown` 或 stdin EOF 且无任务/任务已结束 |
| 1 | 任务失败/停止后的 EOF |
| 2 | 启动失败、参数组合错误、非智能模式 |
| 130 | Ctrl+C 或 EOF 时任务仍在运行（任务被停止） |

## 边界

- 只读：不执行写入/审批/MCP 写工具；结果事件与 CLI 一致。
- 不加载历史会话；需要延续上下文请在桌面端分支树中操作。
- 输出不含 API 密钥/接口地址；任务记录仍加密保存在资料库目录。

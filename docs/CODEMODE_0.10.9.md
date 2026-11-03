# Codemode：`run_code` 内调用只读工具（v0.10.9）

[项目首页](../README.md) · [Agent 首页与交互](AGENT_HOME_UI.md) · [本次验证](VALIDATION_0.10.9.md)

被批准的 `run_code` 代码除了 `INPUT` 之外，还可以通过 `tools.*` 调用**只读**资料库工具，把多次检索/读取组合成一段计算或整理脚本。运行仍在原有 QuickJS WebAssembly 沙箱内，**没有新增写入或审批能力**。

## 可用工具

```js
const hits = tools.search_library({ query: "季度预算", limit: 8 });
const docs = tools.read_items({ ids: hits.ids.slice(0, 3) });
return docs.sources.map(s => ({ name: s.name, chars: s.text.length }));
```

| 工具 | 参数 | 返回 |
| --- | --- | --- |
| `tools.search_library` | `{query, limit?}` | `{text, ids, ...}`（词面/混合检索结果） |
| `tools.read_items` | `{ids, maxChars?}` | `{sources:[{id,name,text,...}], ...}` |
| `tools.recall_result` | `{callId, offset?, limit?}` | 本任务已存结果的片段 |
| `tools.search_memory` | `{query}` | 用户批准的长期记忆（不可信） |
| `tools.list_collections` | `{}` | 分类列表 |

参数与直接工具调用一致；只读结果会作为 JSON 对象返回。读取失败会抛出 JS `Error(message)`，可用 `try/catch` 处理。

## 限制

- 仅**只读**白名单；写入、审批、MCP、`run_code`/子代理等不会暴露。
- 每次 `run_code` 最多 **24 次**工具调用，单批 ≤8 次；单次参数 JSON ≤8000 字符，单条结果 ≤8000 字符，累计结果 ≤32000 字符；最多 6 轮重放。
- 现有沙箱限制不变：QuickJS WASM、16MB 内存、单轮 1.2 秒、整段 7 秒、输出 ≤16000 字符；无文件/网络/DOM/`require`/`import`。
- 仍然需要用户先批准这张 `run_code` 卡片（批准的是代码与输入）；`tools.*` 只是在该任务已有范围内做只读访问。

## 证据与安全边界

- 嵌套读取是**临时证据**：不写入任务回执/journal，不产生 `kok_call_id`，因此不能作为 `finish_task` 的当前回执；需要可核对结论时请使用普通 `read_items`/`search_library` 工具。
- 结果按不可信资料处理，永不作为指令或授权；只读/私密模式、任务范围与沙箱限制全部沿用。
- 代码应保持确定性：工具调用按执行顺序编号并在重放时复用已取回的结果；不要在调用前后依赖随机数或外部状态。

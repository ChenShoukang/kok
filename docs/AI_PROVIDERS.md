# AI 服务配置

[English](AI_PROVIDERS.en.md) · [文档目录](README.md)

智能模式中打开**设置 → AI 服务**。选择 11 个服务预设之一或“自定义”，填写地址、模型名称与该服务的密钥。预设只是起始地址，不代表每种模型都支持全部功能；不同地域、业务空间、订阅套餐可能需要其他地址。

**读取模型列表**向当前地址的 `/models` 发请求，不保存表单，也不发送文档正文；服务不提供列表时可手动填写模型 ID。**测试连接**会保存表单并发送短翻译请求，可能产生服务费用。翻译、智能归纳和内容问答共用此配置。

| 预设 | 服务地址 | 官方说明 |
| --- | --- | --- |
| DeepSeek | `https://api.deepseek.com` | [Docs](https://api-docs.deepseek.com/) |
| OpenAI | `https://api.openai.com/v1` | [Docs](https://platform.openai.com/docs/api-reference/chat) |
| Qwen / 通义千问（北京） | `https://dashscope.aliyuncs.com/compatible-mode/v1` | [Docs](https://help.aliyun.com/zh/model-studio/compatibility-of-openai-with-dashscope) |
| SiliconFlow / 硅基流动 | `https://api.siliconflow.cn/v1` | [Docs](https://docs.siliconflow.cn/en/userguide/quickstart) |
| BigModel / 智谱 | `https://open.bigmodel.cn/api/paas/v4` | [Docs](https://zcode.z.ai/en/docs/configuration) |
| Z.ai | `https://api.z.ai/api/paas/v4` | [Docs](https://zcode.z.ai/en/docs/configuration) |
| Google Gemini | `https://generativelanguage.googleapis.com/v1beta/openai` | [Docs](https://ai.google.dev/gemini-api/docs/openai) |
| Groq | `https://api.groq.com/openai/v1` | [Docs](https://console.groq.com/docs/openai) |
| OpenRouter | `https://openrouter.ai/api/v1` | [Docs](https://openrouter.ai/docs/quickstart) |
| Ollama | `http://localhost:11434/v1` | [Docs](https://docs.ollama.com/api/openai-compatibility) |
| LM Studio | `http://localhost:1234/v1` | [Docs](https://lmstudio.ai/docs/developer/openai-compat) |

## 兼容性与密钥

- 使用 Chat Completions 兼容 API，未实现 Anthropic 原生 Messages、仅支持 OpenAI Responses 的接口或其他不兼容协议。自定义网关需提供兼容路由。
- 模型列表使用 GET；问答支持流式响应；翻译需要模型返回结构化 JSON。图片问答需要支持图片输入的模型；文字模型可仅使用字幕。
- 高级设置可选择输出长度参数 `max_tokens` 或 `max_completion_tokens`。自动模式对 OpenAI 官方地址使用后者，其他地址使用前者。某些专用推理模型可能不接受其他通用参数，请按其官方说明选用兼容文字 / 对话模型。
- 远程接口要求 HTTPS，本机服务可使用 HTTP 并留空密钥。Ollama / LM Studio 需先启动服务并加载模型；kok 不下载模型或管理服务进程。
- 密钥使用 Windows DPAPI 加密，仅由原生宿主解密。拒绝请求重定向。更改地址后需重新填写密钥，不会自动把旧密钥发到新地址。本机免密预设在保存时明确清除原密钥。
- 源码、发布包和导出排除个人密钥及模型配置。纯净模式和私密文件夹阻止 AI 操作。本机模型下载完成后可离线使用。

预设与请求行为参考官方文档；自动测试使用本地模拟服务。服务可用性、费用、模型 ID 与能力以服务商为准，不声称全部远程服务已逐一实测。测试不会将个人密钥或内容发送至这些服务。

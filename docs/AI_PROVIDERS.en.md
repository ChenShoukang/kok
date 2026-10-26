# AI provider configuration

[中文](AI_PROVIDERS.md) · [Documentation](README.en.md)

Document translation offers side-by-side and translation-only views with matching-format copies; see [translation and captions](TRANSLATION_CAPTIONS_0.5.1.md). Video transcription has separate settings for local Whisper or a timestamped speech API. A DeepSeek chat key is never forwarded to a speech provider.

In Smart mode, open **Settings → AI service**. Choose one of 11 service presets or **Custom**, enter your endpoint and model ID, and enter the provider credential. The preset is a starting address, not an endorsement or a guarantee that every model supports every feature. Regional, workspace and subscription-specific addresses may differ.

**Load models** queries the selected endpoint's `/models` route without saving the form or sending document content. If unsupported, fill the model ID manually. **Test connection** saves the form and sends a short translation request; provider charges may apply. Translation, organization and file chat share this configuration.

| Preset | Base URL | Official reference |
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

## Compatibility and credentials

- Uses the Chat Completions-compatible API. Native Anthropic Messages, OpenAI Responses-only endpoints and other incompatible protocols are not implemented. Custom gateways must expose a compatible route.
- Model-list discovery uses GET; chat uses streamed responses. Translation expects structured JSON. Visual questions require a model accepting image data; text-only models can use subtitle text instead.
- Advanced settings choose `max_tokens` or `max_completion_tokens` for output limits. Auto chooses the latter for the official OpenAI host and the former elsewhere. Some specialized reasoning-only models may reject other common parameters; check the provider's model documentation and use a compatible text/chat model.
- HTTPS is required remotely. HTTP without a key is allowed for local services. Start Ollama or LM Studio and load a model before using its local preset. kok does not download models or manage their server processes.
- Credentials are encrypted with Windows DPAPI and only decrypted in the native host. Redirects are rejected. Changing the service address requires a new credential; an existing one is never silently forwarded to another endpoint. A local no-key preset explicitly removes the saved credential when saved.
- Credentials and personal configuration are excluded from source, packages and exports. Pure mode and private folder content reject AI operations. Local model use can remain offline after its model files are installed.

Presets and request behavior were checked against official documentation; automated tests use local mock services. Availability, billing, model IDs and feature support must be verified with your provider. No personal content or credential is sent to these services by the test suite.

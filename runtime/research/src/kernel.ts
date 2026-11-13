import { randomUUID } from 'node:crypto';
import { createDeepAgent, registerHarnessProfile } from 'deepagents';
import { ChatOpenAI } from '@langchain/openai';
import { Command, interrupt } from '@langchain/langgraph';
import type { BaseChatModel } from '@langchain/core/language_models/chat_models';
import { tool } from '@langchain/core/tools';
import { Validator, type JSONSchema } from '@langchain/core/utils/json_schema';
import { createMiddleware } from 'langchain';
import { PortableSaver } from './checkpoints.ts';
import { StepSchema, PROTOCOL_VERSION, isRecord, type Emit, type Permit, type Snapshot, type StepInput, type StepResult, type NativeInterrupt } from './contracts.ts';
import { capabilityInstructions, selectNativeTools } from './capabilities.ts';

const BLOCKED_TOOLS = new Set(['task', 'execute']);
registerHarnessProfile('openai', { generalPurposeSubagent: { enabled: false }, excludedTools: ['task', 'execute'] });
class ContextAwareChatOpenAI extends ChatOpenAI {
  private contextLimits: { maxInputTokens: number; maxOutputTokens: number };
  constructor(options: ConstructorParameters<typeof ChatOpenAI>[0], limits: { maxInputTokens: number; maxOutputTokens: number }) { super(options); this.contextLimits = limits; }
  get profile() { return { ...super.profile, ...this.contextLimits }; }
}
function contentText(content: unknown): string {
  if (typeof content === 'string') return content;
  return Array.isArray(content) ? content.filter(isRecord).filter(x => x.type === 'text' && typeof x.text === 'string').map(x => x.text).join('') : '';
}
export function redactError(error: unknown, key = ''): string {
  let text = error instanceof Error ? error.message : String(error || '研究内核请求失败。');
  if (key) text = text.split(key).join('[redacted]');
  return text.replace(/sk-[A-Za-z0-9_-]{12,}/g, '[redacted]').slice(0, 1000);
}
type FetchOptions = { emit: Emit; permit: Permit; endpoint: string; pending: Promise<void>[]; fetchImpl?: typeof fetch };
type Usage = { prompt_tokens?: number; completion_tokens?: number; prompt_cache_hit_tokens?: number; prompt_tokens_details?: { cached_tokens?: number } };
export function monitoredFetch({ emit, permit, endpoint, pending, fetchImpl = fetch }: FetchOptions): typeof fetch {
  const expected = new URL(endpoint);
  return async (url, init = {}) => {
    const target = new URL(url instanceof Request ? url.url : String(url));
    if (target.origin !== expected.origin || !target.pathname.startsWith(expected.pathname.replace(/\/$/, '') + '/')) throw new Error('模型请求超出了已配置的服务地址。');
    let body: Record<string, unknown>;
    try { const value: unknown = JSON.parse(String(init.body)); if (!isRecord(value)) throw new Error(); body = value; } catch { throw new Error('模型请求格式无效。'); }
    const requestId = randomUUID().replaceAll('-', ''), start = performance.now(), outputLimit = Number(body.max_tokens || body.max_completion_tokens || 4000);
    await permit({ requestId, inputCharacters: JSON.stringify(body.messages || []).length + JSON.stringify(body.tools || []).length, outputLimit });
    let response: Response;
    try { response = await fetchImpl(url, { ...init, redirect: 'error' }); }
    catch (error) { emit({ type: 'model_finished', requestId, outcome: 'failed', errorKind: 'network', durationMs: Math.round(performance.now() - start), inputTokens: 0, outputTokens: 0, cachedTokens: 0, estimated: true }); throw error; }
    const track = (async () => {
      let usage: Usage | null = null, firstTextMs = -1;
      const absorb = (value: unknown) => {
        if (!isRecord(value)) return;
        if (isRecord(value.usage)) usage = value.usage as Usage;
        const first = Array.isArray(value.choices) ? value.choices[0] : undefined;
        const delta = isRecord(first) && isRecord(first.delta) ? first.delta.content : undefined;
        if (typeof delta === 'string' && delta) { if (firstTextMs < 0) firstTextMs = Math.round(performance.now() - start); emit({ type: 'delta', requestId, text: delta }); }
      };
      try {
        if (response.headers.get('content-type')?.includes('text/event-stream')) {
          const reader = response.clone().body?.getReader(); if (!reader) throw new Error('模型返回了空事件流。');
          const decoder = new TextDecoder(); let buffer = '';
          for (;;) {
            const { value, done } = await reader.read(); buffer += decoder.decode(value || new Uint8Array(), { stream: !done });
            let lineEnd: number;
            while ((lineEnd = buffer.indexOf('\n')) >= 0) {
              const line = buffer.slice(0, lineEnd).trim(); buffer = buffer.slice(lineEnd + 1);
              if (!line.startsWith('data:')) continue;
              const data = line.slice(5).trim(); if (!data || data === '[DONE]') continue;
              try { absorb(JSON.parse(data)); } catch { /* Provider stream validation belongs to the SDK. */ }
            }
            if (buffer.length > 2_000_000) { await reader.cancel(); throw new Error('模型事件过大。'); }
            if (done) break;
          }
        } else if (response.ok) absorb(await response.clone().json());
      } finally {
        const measured = usage as Usage | null;
        emit({ type: 'model_finished', requestId, outcome: response.ok ? 'success' : 'failed', httpStatus: response.status, durationMs: Math.round(performance.now() - start), firstTextMs,
          inputTokens: Number(measured?.prompt_tokens || 0), outputTokens: Number(measured?.completion_tokens || 0), cachedTokens: Number(measured?.prompt_cache_hit_tokens || measured?.prompt_tokens_details?.cached_tokens || 0), estimated: !measured });
      }
    })();
    pending.push(track.catch(() => {})); return response;
  };
}
type KernelOptions = { snapshot?: Snapshot | null; emit?: Emit; permit?: Permit; modelFactory?: (input: StepInput) => BaseChatModel };
export class ResearchKernel {
  private saver: Promise<PortableSaver>;
  private emit: Emit;
  private permit: Permit;
  private modelFactory?: KernelOptions['modelFactory'];
  constructor({ snapshot, emit = () => {}, permit = async () => {}, modelFactory }: KernelOptions = {}) { this.saver = PortableSaver.restore(snapshot); this.emit = emit; this.permit = permit; this.modelFactory = modelFactory; }
  async step(raw: unknown): Promise<StepResult> {
    const input = StepSchema.parse(raw), pending: Promise<void>[] = [], saver = await this.saver;
    if (!this.modelFactory && (!input.endpoint || !input.model)) throw new Error('研究内核模型配置无效。');
    const model = this.modelFactory ? this.modelFactory(input) : new ContextAwareChatOpenAI({
      apiKey: input.apiKey || 'kok-local', model: input.model, maxTokens: input.outputLimit || 6000, maxRetries: 0, streaming: true, streamUsage: true, timeout: 180_000,
      configuration: { baseURL: input.endpoint, fetch: monitoredFetch({ emit: this.emit, permit: this.permit, endpoint: input.endpoint!, pending }) },
    }, { maxInputTokens: input.contextTokens || 64000, maxOutputTokens: input.outputLimit || 6000 });
    const selected = selectNativeTools(input.tools);
    const nativeTools = selected.map(spec => {
      const definition = spec.function, schema = definition.parameters as JSONSchema;
      new Validator(definition.parameters as ConstructorParameters<typeof Validator>[0]); // The two SDK schema types describe the same wire JSON with different optional members.
      return tool(async (args: unknown) => JSON.stringify(interrupt({ kind: 'kok_tool', name: definition.name, args })), { name: definition.name, description: definition.description, schema });
    });
    const boundary = createMiddleware({
      name: 'KokNativeBoundary',
      wrapModelCall: (request, handler) => handler({ ...request, tools: request.tools.filter(t => typeof t.name !== 'string' || !BLOCKED_TOOLS.has(t.name)) }),
      wrapToolCall: (request, handler) => { if (BLOCKED_TOOLS.has(request.toolCall.name)) throw new Error('当前研究内核不执行委派或任意命令；请使用已注册的 kok 工具。'); return handler(request); },
    });
    const agent = await createDeepAgent({ model, tools: nativeTools, checkpointer: saver, backend: undefined, middleware: [boundary], subagents: [], name: 'kok-agent',
      systemPrompt: input.systemPrompt + '\n[Active capability guidance]\n' + capabilityInstructions(selected, input.taskSpec) });
    const messages = input.messages.map(m => ({ role: m.role, content: m.content }));
    let state;
    try { state = await agent.invoke(input.resume && Object.keys(input.resume).length ? new Command({ resume: input.resume, ...(messages.length ? { update: { messages } } : {}) }) : { messages }, { configurable: { thread_id: input.taskId }, recursionLimit: 100 }); }
    finally { await Promise.all(pending); }
    const calls: NativeInterrupt[] = [];
    for (const item of state.__interrupt__ || []) {
      const value: unknown = item.value;
      if (!isRecord(value) || value.kind !== 'kok_tool' || typeof value.name !== 'string' || !isRecord(value.args)) throw new Error('研究内核返回了未识别的交互请求。');
      calls.push({ interruptId: item.id, name: value.name, args: value.args });
    }
    const last = [...(state.messages || [])].reverse().find(m => m.getType() === 'ai');
    return { kind: calls.length ? 'tools' : 'final', calls, text: contentText(last?.content), snapshot: saver.snapshot(), protocolVersion: PROTOCOL_VERSION };
  }
}
export { PortableSaver as CheckpointStore };

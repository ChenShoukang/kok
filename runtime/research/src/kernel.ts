import { Tiktoken } from 'js-tiktoken/lite';
import cl100k from 'js-tiktoken/ranks/cl100k_base';
import { createDeepAgent, registerHarnessProfile } from 'deepagents';
import { ChatOpenAI, ChatOpenAICompletions } from '@langchain/openai';
import { Command, interrupt } from '@langchain/langgraph';
import type { BaseChatModel } from '@langchain/core/language_models/chat_models';
import { AIMessage, type BaseMessage } from '@langchain/core/messages';
import { tool } from '@langchain/core/tools';
import { Validator, type JSONSchema } from '@langchain/core/utils/json_schema';
import { createMiddleware } from 'langchain';
import { RequirementUpdateSchema, RequirementProposalSchema } from './business/requirement-schema.ts';
import { PortableSaver } from './checkpoints.ts';
import { StepSchema, PROTOCOL_VERSION, isRecord, type Emit, type Permit, type Snapshot, type StepInput, type StepResult, type NativeInterrupt } from './contracts.ts';
import { capabilityInstructions, selectNativeTools } from './capabilities.ts';
import { monitoredFetch } from './model-transport.ts';
export { monitoredFetch, redactError } from './model-transport.ts';

const BLOCKED_TOOLS = new Set(['task', 'execute']);
registerHarnessProfile('openai', { generalPurposeSubagent: { enabled: false }, excludedTools: ['task', 'execute'] });
let localEncoding: Tiktoken | undefined;
export async function countLocalTokens(content: Parameters<ChatOpenAI['getNumTokens']>[0]): Promise<number> {
  const text = typeof content === 'string' ? content : content.map(block => typeof block === 'string' ? block : contentText([block])).join('');
  if (!text) return 0;
  // The SDK uses this estimate for context management. Billed usage comes from the provider.
  localEncoding ??= new Tiktoken(cl100k);
  if (text.length <= 256) return localEncoding.encode(text, [], []).length;

  // Bound BPE work on long uninterrupted spans such as Chinese source text.
  // Chunk boundaries can slightly change the estimate. Unicode matching keeps surrogate pairs intact.
  const chunkCounts = new Map<string, number>();
  let total = 0;
  for (const [chunk] of text.matchAll(/.{1,256}/gsu)) {
    let count = chunkCounts.get(chunk);
    if (count === undefined) {
      count = localEncoding.encode(chunk, [], []).length;
      chunkCounts.set(chunk, count);
    }
    total += count;
  }
  return total;
}
class LocalCountingCompletions extends ChatOpenAICompletions {
  override getNumTokens = countLocalTokens;
}
class ContextAwareChatOpenAI extends ChatOpenAI {
  private contextLimits: { maxInputTokens: number; maxOutputTokens: number };
  constructor(options: ConstructorParameters<typeof ChatOpenAI>[0], limits: { maxInputTokens: number; maxOutputTokens: number }) {
    super({ ...options, completions: new LocalCountingCompletions(options) });
    this.contextLimits = limits;
  }
  get profile() { return { ...super.profile, ...this.contextLimits }; }
  override getNumTokens = countLocalTokens;
}
function contentText(content: unknown): string {
  if (typeof content === 'string') return content;
  return Array.isArray(content) ? content.filter(isRecord).filter(x => x.type === 'text' && typeof x.text === 'string').map(x => x.text).join('') : '';
}
function withoutMalformedToolCalls(message: BaseMessage): BaseMessage {
  if (!AIMessage.isInstance(message) || !message.invalid_tool_calls?.length) return message;

  // LangChain can reserialize malformed provider calls as raw tool calls even though the
  // graph never invoked them. Keep valid calls and original history, and tell the model
  // that the malformed calls produced no native action.
  const additionalKwargs = { ...message.additional_kwargs };
  delete additionalKwargs.tool_calls;
  const note = '[Host] Malformed tool arguments were not executed. Retry with valid JSON only if the action is still required.';
  const content = typeof message.content === 'string'
    ? [message.content, note].filter(Boolean).join('\n')
    : [...message.content, { type: 'text' as const, text: note }];
  return new AIMessage({ ...message, additional_kwargs: additionalKwargs, content, invalid_tool_calls: [] });
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
    const retired = selectNativeTools(input.resumeTools || []).filter(spec => !selected.some(current => current.function.name === spec.function.name));
    const retiredNames = new Set(retired.map(spec => spec.function.name));
    const nativeTools = [...selected, ...retired].map(spec => {
      const definition = spec.function, schema = definition.parameters as JSONSchema;
      new Validator(definition.parameters as ConstructorParameters<typeof Validator>[0]); // The two SDK schema types describe the same wire JSON with different optional members.
      return tool(async (args: unknown) => JSON.stringify(interrupt({ kind: 'kok_tool', name: definition.name, args })), { name: definition.name, description: definition.description, schema: definition.name === 'update_task_spec' ? RequirementUpdateSchema : definition.name === 'propose_task_spec' ? RequirementProposalSchema : schema });
    });
    const blockedTools = new Set([...BLOCKED_TOOLS, ...(input.taskSpec?.requirementsVersion === 1 ? ['write_file', 'edit_file'] : [])]);
    const boundary = createMiddleware({
      name: 'KokNativeBoundary',
      wrapModelCall: (request, handler) => handler({
        ...request, messages: request.messages.map(withoutMalformedToolCalls),
        tools: request.tools.filter(t => typeof t.name !== 'string' || !blockedTools.has(t.name) && !retiredNames.has(t.name)),
      }),
      wrapToolCall: (request, handler) => { if (blockedTools.has(request.toolCall.name)) throw new Error('当前研究内核不执行此框架工具；请使用已注册的 kok 工具交付实际成果。'); return handler(request); },
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

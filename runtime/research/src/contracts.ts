import { z } from 'zod';

export const PROTOCOL_VERSION = 1;
const text = z.string();
export const AssetSchema = z.object({
  id: text.min(1), role: z.enum(['source', 'artifact']), kind: text, format: text,
  version: text, available: z.boolean(), originCallId: text.default(''), projectId: text.default(''), path: text.default(''),
});
export const CapabilitySchema = z.object({
  id: text.min(1), status: z.enum(['available', 'partial', 'unconfigured', 'planned', 'disabled']),
  tools: z.array(text), reason: text.default(''),
});
export const TaskSpecSchema = z.object({
  version: z.literal(1), revision: z.number().int().nonnegative(), goal: text.min(1),
  projectId: text.default(''), capabilities: z.array(text),
  scope: z.object({ mode: z.enum(['all', 'selected', 'collection']), ids: z.array(text), collection: text.default('') }),
  deliverables: z.array(z.object({ kind: text, format: text, pages: z.number().int().nonnegative(), minLength: z.number().int().nonnegative(), maxLength: z.number().int().nonnegative(), unit: text })),
  fullRead: z.boolean(), targetedEdit: z.boolean(),
  limits: z.object({ requests: z.number().int().positive(), contextTokens: z.number().int().positive() }),
});
export type TaskSpec = z.infer<typeof TaskSpecSchema>;
export type Asset = z.infer<typeof AssetSchema>;
export const SnapshotSchema = z.object({ version: z.union([z.literal(1), z.literal(2)]), data: text.min(1), nodeMajor: z.number().int().optional(), codec: text.optional() });
export type Snapshot = z.infer<typeof SnapshotSchema>;
export const NativeToolSchema = z.object({ type: z.literal('function'), function: z.object({ name: text.min(1), description: text, parameters: z.record(text, z.unknown()) }) });
export type NativeTool = z.infer<typeof NativeToolSchema>;
export const StepSchema = z.object({
  type: z.literal('step').optional(), protocolVersion: z.literal(PROTOCOL_VERSION).optional(),
  taskId: text.min(1).max(128), endpoint: text.optional(), apiKey: text.optional(), model: text.optional(),
  contextTokens: z.number().int().min(8000).max(128000).optional(), outputLimit: z.number().int().positive().optional(),
  systemPrompt: text, tools: z.array(NativeToolSchema).max(256),
  messages: z.array(z.object({ role: z.enum(['user', 'assistant']), content: text })),
  resume: z.record(text, z.unknown()).optional(), snapshot: SnapshotSchema.nullish(),
  taskSpec: TaskSpecSchema.optional(), assets: z.array(AssetSchema).optional(), capabilities: z.array(CapabilitySchema).optional(),
});
export type StepInput = z.infer<typeof StepSchema>;
export type Event = { type: string; [key: string]: unknown };
export type Emit = (event: Event) => void;
export type ModelRequest = { requestId: string; inputCharacters: number; outputLimit: number };
export type Permit = (request: ModelRequest) => Promise<void>;
export type NativeInterrupt = { interruptId: string; name: string; args: Record<string, unknown> };
export type StepResult = { kind: 'tools' | 'final'; calls: NativeInterrupt[]; text: string; snapshot: Snapshot; protocolVersion: number };
export function isRecord(value: unknown): value is Record<string, unknown> { return value !== null && typeof value === 'object' && !Array.isArray(value); }

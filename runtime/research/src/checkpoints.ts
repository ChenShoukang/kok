import { gzipSync, gunzipSync } from 'node:zlib';
import { deserialize } from 'node:v8';
import { MemorySaver, type Checkpoint, type CheckpointMetadata, type PendingWrite } from '@langchain/langgraph-checkpoint';
import type { RunnableConfig } from '@langchain/core/runnables';
import { z } from 'zod';
import { SnapshotSchema, type Snapshot } from './contracts.ts';

const MAX_BYTES = 512_000;
const ConfigSchema = z.object({ thread_id: z.string().min(1), checkpoint_ns: z.string(), checkpoint_id: z.string().optional() });
const OperationSchema = z.object({ kind: z.enum(['put', 'writes']), config: ConfigSchema, encoding: z.string(), data: z.string(), taskId: z.string().optional(), taskPath: z.string().optional() });
type Operation = z.infer<typeof OperationSchema>;
function portableConfig(config: RunnableConfig) { return ConfigSchema.parse({ thread_id: config.configurable?.thread_id, checkpoint_ns: config.configurable?.checkpoint_ns ?? '', checkpoint_id: config.configurable?.checkpoint_id }); }

// Only public saver methods are recorded. New snapshots do not inspect MemorySaver.storage/writes
// or use V8 object serialization. The native encrypted journal remains the durable storage owner.
export class PortableSaver extends MemorySaver {
  private operations: Operation[] = [];
  override async put(config: RunnableConfig, checkpoint: Checkpoint, metadata: CheckpointMetadata): Promise<RunnableConfig> {
    const [encoding, data] = await this.serde.dumpsTyped({ checkpoint, metadata });
    const result = await super.put(config, checkpoint, metadata);
    this.operations.push({ kind: 'put', config: portableConfig(config), encoding, data: Buffer.from(data).toString('base64') });
    return result;
  }
  override async putWrites(config: RunnableConfig, writes: PendingWrite[], taskId: string, taskPath = ''): Promise<void> {
    const [encoding, data] = await this.serde.dumpsTyped(writes);
    await super.putWrites(config, writes, taskId, taskPath);
    this.operations.push({ kind: 'writes', config: portableConfig(config), encoding, data: Buffer.from(data).toString('base64'), taskId, taskPath });
  }
  override async deleteThread(threadId: string): Promise<void> {
    await super.deleteThread(threadId); this.operations = this.operations.filter(op => op.config.thread_id !== threadId);
  }
  snapshot(): Snapshot {
    const bytes = gzipSync(Buffer.from(JSON.stringify({ version: 1, operations: this.operations }), 'utf8'));
    if (bytes.length > MAX_BYTES) throw new Error('研究内核检查点已达到上限，请保留成果并新建任务继续。');
    return { version: 2, codec: 'langgraph-public-operations-v1', data: bytes.toString('base64') };
  }
  static async restore(input?: Snapshot | null): Promise<PortableSaver> {
    const saver = new PortableSaver(); if (!input) return saver;
    const snapshot = SnapshotSchema.parse(input), bytes = Buffer.from(snapshot.data, 'base64');
    if (!bytes.length || bytes.length > MAX_BYTES) throw new Error('研究内核检查点大小无效。');
    if (snapshot.version === 1) { await migrateLegacy(saver, snapshot, bytes); return saver; }
    if (snapshot.codec !== 'langgraph-public-operations-v1') throw new Error('研究内核检查点版本无效。');
    const parsed = z.object({ version: z.literal(1), operations: z.array(OperationSchema).max(50000) }).parse(JSON.parse(gunzipSync(bytes, { maxOutputLength: 64 * 1024 * 1024 }).toString('utf8')));
    for (const op of parsed.operations) {
      const payload: unknown = await saver.serde.loadsTyped(op.encoding, Buffer.from(op.data, 'base64'));
      const config = { configurable: op.config };
      if (op.kind === 'put') {
        const value = payload as { checkpoint: Checkpoint; metadata: CheckpointMetadata };
        await saver.put(config, value.checkpoint, value.metadata);
      } else {
        if (!op.taskId || !Array.isArray(payload)) throw new Error('研究内核检查点无效。');
        await saver.putWrites(config, payload as PendingWrite[], op.taskId, op.taskPath);
      }
    }
    return saver;
  }
}

// Explicit, read-only v0.13 importer. Knowledge of the old private layout is confined here;
// imported values are written through the public saver contract and immediately become v2.
async function migrateLegacy(target: PortableSaver, snapshot: Snapshot, bytes: Buffer): Promise<void> {
  if (snapshot.nodeMajor !== Number(process.versions.node.split('.')[0])) throw new Error('研究内核会话版本不兼容，请新建任务；已有成果和原生记录仍可使用。');
  type Legacy = { storage: Record<string, Record<string, Record<string, [Uint8Array, Uint8Array, string?]>>>; writes: Record<string, Record<string, [string, string, Uint8Array, string?]>> };
  const old = deserialize(gunzipSync(bytes, { maxOutputLength: 64 * 1024 * 1024 })) as Legacy;
  if (!old || !old.storage || !old.writes) throw new Error('研究内核检查点无效。');
  for (const [thread, spaces] of Object.entries(old.storage)) for (const [space, checkpoints] of Object.entries(spaces)) for (const [id, [checkpoint, metadata, parent]] of Object.entries(checkpoints)) {
    await target.put({ configurable: { thread_id: thread, checkpoint_ns: space, checkpoint_id: parent } }, await target.serde.loadsTyped('json', checkpoint) as Checkpoint, await target.serde.loadsTyped('json', metadata) as CheckpointMetadata);
    const rows = Object.entries(old.writes[JSON.stringify([thread, space, id])] ?? {}).sort((a, b) => Number(a[0].slice(a[0].lastIndexOf(',') + 1)) - Number(b[0].slice(b[0].lastIndexOf(',') + 1)));
    const groups = new Map<string, { task: string; path: string; writes: PendingWrite[] }>();
    for (const [, [task, channel, data, taskPath]] of rows) {
      const key = JSON.stringify([task, taskPath ?? '']); const group = groups.get(key) ?? { task, path: taskPath ?? '', writes: [] };
      group.writes.push([channel, await target.serde.loadsTyped('json', data)]); groups.set(key, group);
    }
    for (const group of groups.values()) await target.putWrites({ configurable: { thread_id: thread, checkpoint_ns: space, checkpoint_id: id } }, group.writes, group.task, group.path);
  }
}

import { createInterface } from 'node:readline';
import { ResearchKernel, redactError } from './kernel.ts';
import { StepSchema, isRecord, type Event, type ModelRequest } from './contracts.ts';

const output = (value: Event) => process.stdout.write(JSON.stringify(value) + '\n');
const permissions = new Map<string, { resolve: () => void; reject: (reason: string) => void }>();
let kernel: ResearchKernel | null = null, busy = false, key = '';
function permit(request: ModelRequest): Promise<void> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => { permissions.delete(request.requestId); reject(new Error('宿主请求预算确认超时。')); }, 30_000);
    permissions.set(request.requestId, { resolve: () => { clearTimeout(timer); resolve(); }, reject: reason => { clearTimeout(timer); reject(new Error(reason)); } });
    output({ type: 'model_start', ...request });
  });
}
const lines = createInterface({ input: process.stdin, crlfDelay: Infinity });
lines.on('line', async line => {
  let value: unknown;
  // .NET Framework's original StandardInput writer may emit a UTF-8 BOM when the console
  // uses code page 65001, before the explicit no-BOM writer takes ownership of its stream.
  try { if (line.length > 2_000_000) throw new Error(); value = JSON.parse(line.replace(/^\uFEFF/, '')); if (!isRecord(value)) throw new Error(); }
  catch { output({ type: 'error', message: '研究内核输入格式无效。' }); return; }
  if (value.type === 'model_permit') {
    const id = typeof value.requestId === 'string' ? value.requestId : '', pending = permissions.get(id); permissions.delete(id);
    if (pending) value.allow === true ? pending.resolve() : pending.reject(typeof value.reason === 'string' ? value.reason : '已达到请求或费用上限。'); return;
  }
  if (value.type !== 'step' || busy) { output({ type: 'error', message: busy ? '研究内核正在执行。' : '未知研究内核指令。' }); return; }
  busy = true; key = typeof value.apiKey === 'string' ? value.apiKey : '';
  try {
    const message = StepSchema.parse(value);
    kernel ||= new ResearchKernel({ snapshot: message.snapshot, emit: output, permit });
    output({ type: 'result', ...await kernel.step(message) });
  } catch (error) { output({ type: 'error', message: redactError(error, key) }); }
  finally { busy = false; key = ''; }
});
lines.on('close', () => process.exit(0));

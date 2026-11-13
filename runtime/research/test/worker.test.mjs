import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { once } from 'node:events';
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';

export function respond(res, calls, text = '') {
  res.writeHead(200, { 'content-type': 'text/event-stream' });
  const delta = calls ? { role: 'assistant', tool_calls: calls.map((c, index) => ({ index, id: 'call-' + index, type: 'function', function: { name: c.name, arguments: JSON.stringify(c.args) } })) } : { role: 'assistant', content: text };
  const row = data => res.write('data: ' + JSON.stringify(data) + '\n\n');
  const id = 'fixture-' + randomUUID();
  row({ id, object: 'chat.completion.chunk', model: 'kok-fixture', choices: [{ index: 0, delta, finish_reason: null }] });
  row({ id, object: 'chat.completion.chunk', model: 'kok-fixture', choices: [{ index: 0, delta: {}, finish_reason: calls ? 'tool_calls' : 'stop' }], usage: { prompt_tokens: 100, completion_tokens: 20 } });
  res.end('data: [DONE]\n\n');
}

test('bundled worker accepts a UTF-8 BOM and uses the actual HTTP path, host permissions and checkpoint resume', { timeout: 30000 }, async () => {
  const requests = [];
  const server = createServer(async (req, res) => {
    let text = ''; for await (const chunk of req) text += chunk;
    const body = JSON.parse(text); requests.push(body);
    if (requests.length === 1) respond(res, [{ name: 'read_items', args: { ids: ['S1'] } }]);
    else respond(res, null, '最终预算是8万元。[S1]');
  });
  server.listen(0, '127.0.0.1'); await once(server, 'listening');
  const worker = fileURLToPath(new URL('../../../dist/kok/runtime/research/worker.mjs', import.meta.url));
  const child = spawn(process.execPath, [worker], { stdio: ['pipe', 'pipe', 'pipe'], env: { ...process.env, LANGSMITH_TRACING: 'false', LANGCHAIN_TRACING_V2: 'false' }, windowsHide: true });
  const lines = createInterface({ input: child.stdout }); const events = []; const queue = []; let waiter;
  let stderr = ''; child.stderr.on('data', d => { stderr += d; });
  lines.on('line', line => {
    const event = JSON.parse(line); events.push(event);
    if (event.type === 'model_start') child.stdin.write(JSON.stringify({ type: 'model_permit', requestId: event.requestId, allow: true }) + '\n');
    if (event.type === 'result' || event.type === 'error') { if (waiter) { const resolve = waiter; waiter = null; resolve(event); } else queue.push(event); }
  });
  child.on('exit', () => { if (waiter) { const resolve = waiter; waiter = null; resolve({ type: 'error', message: 'Worker exited: ' + stderr }); } });
  const next = () => queue.length ? Promise.resolve(queue.shift()) : new Promise((resolve, reject) => {
    const timer = setTimeout(() => { waiter = null; reject(new Error('Worker response timed out: ' + JSON.stringify(events) + '\n' + stderr)); }, 10000);
    waiter = event => { clearTimeout(timer); resolve(event); };
  });
  const base = { type: 'step', taskId: 'worker-http-fixture', endpoint: `http://127.0.0.1:${server.address().port}/v1`, apiKey: 'local-fixture', model: 'kok-fixture', systemPrompt: 'Read S1 and answer.', tools: [{ type: 'function', function: { name: 'read_items', description: 'Read selected sources', parameters: { type: 'object', properties: { ids: { type: 'array', items: { type: 'string' } } }, required: ['ids'], additionalProperties: false } } }] };
  try {
    child.stdin.write('\uFEFF' + JSON.stringify({ ...base, messages: [{ role: 'user', content: '请研究资料S1。' }] }) + '\n');
    const first = await next(); assert.equal(first.type, 'result', first.message || stderr); assert.equal(first.kind, 'tools');
    child.stdin.write(JSON.stringify({ ...base, resume: { [first.calls[0].interruptId]: { status: 'done', result: { text: '最终预算8万元。' }, kok_call_id: 'native-fixture' } }, messages: [] }) + '\n');
    const second = await next(); assert.equal(second.type, 'result', second.message || stderr); assert.equal(second.kind, 'final'); assert.match(second.text, /8万元/);
    assert.equal(requests.length, 2); assert.equal(events.filter(e => e.type === 'model_start').length, 2);
    assert.equal(events.filter(e => e.type === 'model_finished').length, 2);
    assert.match(JSON.stringify(requests[1].messages), /native-fixture/);
    assert.ok(requests[0].tools.some(t => t.function.name === 'read_items'));
    assert.ok(!requests[0].tools.some(t => t.function.name === 'task' || t.function.name === 'execute'));
  } finally { child.kill(); lines.close(); server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
});

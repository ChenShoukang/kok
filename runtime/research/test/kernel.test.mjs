import test from 'node:test';
import assert from 'node:assert/strict';
import { BaseChatModel } from '@langchain/core/language_models/chat_models';
import { AIMessage } from '@langchain/core/messages';
import { ResearchKernel, countLocalTokens, monitoredFetch, redactError } from '../kernel.mjs';

class ScriptedModel extends BaseChatModel {
  constructor(script) { super({}); this.script = script; this.calls = 0; }
  _llmType() { return 'kok-offline-model'; }
  bindTools() { return this; }
  async _generate(messages) {
    const value = await this.script(messages, this.calls++);
    return { generations: [{ text: typeof value.content === 'string' ? value.content : '', message: new AIMessage(value) }] };
  }
}
const tools = [{ type: 'function', function: { name: 'read_items', description: 'Read selected source text', parameters: { type: 'object', properties: { ids: { type: 'array', items: { type: 'string' } } }, required: ['ids'], additionalProperties: false } } }];
const input = { taskId: 'offline-thread', tools, systemPrompt: 'Read sources and answer with citations.', messages: [{ role: 'user', content: 'Read source S1.' }] };

test('actual Deep Agents graph interrupts for native reads and resumes with their real result', async () => {
  const model = new ScriptedModel((messages, count) => count === 0
    ? { content: '读取资料。', tool_calls: [{ id: 'read-1', name: 'read_items', args: { ids: ['S1'] } }] }
    : { content: '预算为8万元。[S1]' });
  const kernel = new ResearchKernel({ modelFactory: () => model });
  const proposed = await kernel.step(input);
  assert.equal(proposed.kind, 'tools'); assert.equal(proposed.calls.length, 1); assert.equal(model.calls, 1);
  assert.deepEqual(proposed.calls[0].args, { ids: ['S1'] });
  const done = await kernel.step({ ...input, messages: [], resume: { [proposed.calls[0].interruptId]: { status: 'done', text: '最终修订：预算8万元。', kok_call_id: 'native-receipt' } } });
  assert.equal(done.kind, 'final'); assert.match(done.text, /8万元/); assert.equal(model.calls, 2);
});

test('compressed checkpoint restores a pending native tool after worker restart without repeating model or write', async () => {
  const first = new ResearchKernel({ modelFactory: () => new ScriptedModel(() => ({ content: '', tool_calls: [{ id: 'read-1', name: 'read_items', args: { ids: ['S1'] } }] })) });
  const proposed = await first.step(input);
  let received = '';
  const resumed = new ResearchKernel({ snapshot: proposed.snapshot, modelFactory: () => new ScriptedModel(messages => {
    received = JSON.stringify(messages.map(m => m.content)); return { content: '已读到最新预算。[S1]' };
  }) });
  const result = await resumed.step({ ...input, messages: [], resume: { [proposed.calls[0].interruptId]: { status: 'done', text: '唯一的真实写入回执' } } });
  assert.equal(result.kind, 'final'); assert.match(received, /唯一的真实写入回执/);
});

test('source/model key text is redacted from reported SDK errors', () => {
  assert.equal(redactError(new Error('bad sk-fixture-key-12345')), 'bad [redacted]');
});

test('the local SDK estimate handles long CJK spans and keeps a boundary emoji intact', async () => {
  assert.equal(await countLocalTokens('中文 <|endoftext|>'), 8);
  const longText = '甲'.repeat(255) + '🧪' + '甲'.repeat(7245) + '\n' + '乙'.repeat(7500);
  assert.equal(await countLocalTokens([longText.slice(0, 700), { type: 'text', text: longText.slice(700) }]), 30004);
});

test('an SDK response without usage estimates tokens locally and contacts only the permitted model', async () => {
  const original = globalThis.fetch, origins = [], events = [];
  const endpoint = 'http://127.0.0.1:8151/v1';
  globalThis.fetch = async url => {
    const target = new URL(url instanceof Request ? url.url : url);
    origins.push(target.origin);
    assert.equal(target.origin, new URL(endpoint).origin);
    const chunk = { id: 'fixture', model: 'fixture', choices: [{ index: 0, delta: { role: 'assistant', content: '已读取。' }, finish_reason: 'stop' }] };
    return new Response('data: '+JSON.stringify(chunk)+'\n\ndata: [DONE]\n\n', { headers: { 'content-type': 'text/event-stream' } });
  };
  try {
    const result = await new ResearchKernel({ emit: event => events.push(event) }).step({ ...input, endpoint, model: 'fixture', tools: [], messages: [{role:'user',content:'检查中文和 literal <|endoftext|>。'}] });
    assert.equal(result.kind, 'final');
    assert.deepEqual(origins, [new URL(endpoint).origin]);
    assert.equal(events.find(event=>event.type==='model_finished').estimated, true);
  } finally { globalThis.fetch = original; }
});

test('repair after a malformed provider tool call keeps valid receipts and resumes from the checkpoint', async () => {
  const original = globalThis.fetch, requests = [];
  const endpoint = 'http://127.0.0.1:8151/v1';
  globalThis.fetch = async (_url, options) => {
    requests.push(JSON.parse(options.body));
    const call = (id, args) => ({ index: 0, id, type: 'function', function: { name: 'read_items', arguments: args } });
    const delta = [
      { role: 'assistant', tool_calls: [call('valid-read', '{"ids":["S1"]}')] },
      { role: 'assistant', content: '已尝试读取第二份。', tool_calls: [call('invalid-read', '{"ids":]}')] },
      { role: 'assistant', content: '继续使用已读取的资料。' },
    ][requests.length - 1];
    const chunk = { id: 'recovery-' + requests.length, model: 'fixture', choices: [{ index: 0, delta, finish_reason: delta.tool_calls ? 'tool_calls' : 'stop' }] };
    return new Response('data: ' + JSON.stringify(chunk) + '\n\ndata: [DONE]\n\n', { headers: { 'content-type': 'text/event-stream' } });
  };
  try {
    const request = { ...input, endpoint, model: 'fixture' };
    const kernel = new ResearchKernel();
    const first = await kernel.step(request);
    const stopped = await kernel.step({ ...request, messages: [], resume: { [first.calls[0].interruptId]: { status: 'done', text: 'real receipt' } } });
    assert.equal(stopped.kind, 'final');
    assert.deepEqual(stopped.calls, []);
    const resumed = await new ResearchKernel({ snapshot: stopped.snapshot }).step({
      ...request, messages: [{ role: 'user', content: '[Host delivery verification] Repair the missing result.' }],
    });
    assert.equal(resumed.kind, 'final');
    const wire = requests[2].messages;
    assert(wire.some(message => message.role === 'tool' && message.tool_call_id === 'valid-read' && message.content.includes('real receipt')));
    assert(wire.some(message => message.role === 'assistant' && message.content?.includes('Malformed tool arguments')));
    assert(!wire.some(message => message.tool_calls?.some(call => call.id === 'invalid-read')));
  } finally {
    globalThis.fetch = original;
  }
});

test('HTTP attempts require host permission and refuse redirects before dispatch', async () => {
  let requests = 0, grants = 0; const events = [], pending = [];
  const fetcher = monitoredFetch({ endpoint: 'https://api.deepseek.com/v1', pending, emit: e => events.push(e), permit: async () => { grants++; },
    fetchImpl: async (url, options) => { requests++; assert.equal(options.redirect, 'error'); return new Response(JSON.stringify({ choices: [{ message: { content: 'ok' } }], usage: { prompt_tokens: 10, completion_tokens: 2 } }), { headers: { 'content-type': 'application/json' } }); } });
  await fetcher('https://api.deepseek.com/v1/chat/completions', { body: JSON.stringify({ messages: [], max_tokens: 100 }) }); await Promise.all(pending);
  assert.equal(requests, 1); assert.equal(grants, 1); assert.equal(events[0].inputTokens, 10);
  await assert.rejects(fetcher('https://another.invalid/v1/chat/completions', { body: '{}' }), /超出/);
  assert.equal(requests, 1);
});

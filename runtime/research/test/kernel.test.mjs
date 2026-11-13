import test from 'node:test';
import assert from 'node:assert/strict';
import { BaseChatModel } from '@langchain/core/language_models/chat_models';
import { AIMessage } from '@langchain/core/messages';
import { ResearchKernel, monitoredFetch, redactError } from '../kernel.mjs';

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

test('HTTP attempts require host permission and refuse redirects before dispatch', async () => {
  let requests = 0, grants = 0; const events = [], pending = [];
  const fetcher = monitoredFetch({ endpoint: 'https://api.deepseek.com/v1', pending, emit: e => events.push(e), permit: async () => { grants++; },
    fetchImpl: async (url, options) => { requests++; assert.equal(options.redirect, 'error'); return new Response(JSON.stringify({ choices: [{ message: { content: 'ok' } }], usage: { prompt_tokens: 10, completion_tokens: 2 } }), { headers: { 'content-type': 'application/json' } }); } });
  await fetcher('https://api.deepseek.com/v1/chat/completions', { body: JSON.stringify({ messages: [], max_tokens: 100 }) }); await Promise.all(pending);
  assert.equal(requests, 1); assert.equal(grants, 1); assert.equal(events[0].inputTokens, 10);
  await assert.rejects(fetcher('https://another.invalid/v1/chat/completions', { body: '{}' }), /超出/);
  assert.equal(requests, 1);
});

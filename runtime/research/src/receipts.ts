import { createHash } from 'node:crypto';
import { isRecord } from './contracts.ts';

// A user update can enter a graph while native tool interrupts are being resumed.
// Provider chat protocols require every matching tool result before that update.
// Reorder only the wire view, preserving every message and the durable graph state.
export function orderToolResults<T extends Record<string, unknown>>(messages: T[]): T[] {
  const ordered = [...messages];
  for (let index = 0; index < ordered.length; index++) {
    const message = ordered[index];
    if (message.role !== 'assistant' || !Array.isArray(message.tool_calls) || !message.tool_calls.length) continue;
    const ids = message.tool_calls.filter(isRecord).map(call => call.id);
    let end = index + 1; while (end < ordered.length && ordered[end].role !== 'assistant') end++;
    const tail = ordered.slice(index + 1, end), results = tail.filter(row => row.role === 'tool' && ids.includes(row.tool_call_id));
    if (ids.some(id => results.filter(row => row.tool_call_id === id).length !== 1)) throw Error('Pending native tool results are incomplete; no model request was sent.');
    const rest = tail.filter(row => !results.includes(row));
    ordered.splice(index + 1, tail.length, ...results, ...rest);
    index += results.length;
  }
  return ordered;
}

// Transient model view only: checkpoint messages and the native journal retain every byte.
// Only EXACT duplicate successful native payloads are replaced; the newest full result stays.
export function projectRepeatedReceipts<T extends { role?: unknown; content?: unknown }>(messages: T[], enabled: boolean) {
  const projected = [...messages], seen = new Map<string, string>();
  let savedCharacters = 0, duplicates = 0;
  if (!enabled) return { messages, savedCharacters, duplicates };
  for (let index = messages.length - 1; index >= 0; index--) {
    const message = messages[index];
    if (message.role !== 'tool' || typeof message.content !== 'string' || message.content.length < 1200) continue;
    let receipt: unknown;
    try { receipt = JSON.parse(message.content); } catch { continue; }
    if (!isRecord(receipt) || receipt.status !== 'done' || !/^[a-f0-9]{32}$/.test(String(receipt.kok_call_id)) || !isRecord(receipt.result) || receipt.error || receipt.errorKind) continue;
    // Different host read-back IDs or result metadata must not be silently merged.
    const payload = JSON.stringify({ ...receipt, kok_call_id: undefined });
    const hash = createHash('sha256').update(payload).digest('hex'), retained = seen.get(hash);
    if (!retained) { seen.set(hash, String(receipt.kok_call_id)); continue; }
    const content = JSON.stringify({ kok_call_id: receipt.kok_call_id, status: 'done', identicalResultAt: retained,
      note: 'The exact same successful payload is retained at the newer kok call above in time. Use recall_result with this kok_call_id for the original result; this reference adds no reading coverage.' });
    if (content.length >= message.content.length) continue;
    projected[index] = { ...message, content };
    savedCharacters += message.content.length - content.length; duplicates++;
  }
  return { messages: projected, savedCharacters, duplicates };
}

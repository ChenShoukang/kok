import { randomUUID } from 'node:crypto';
import { isRecord, type Emit, type Permit } from './contracts.ts';
import { projectRepeatedReceipts, orderToolResults } from './receipts.ts';

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
    const wireMessages = Array.isArray(body.messages) && body.messages.every(isRecord) ? body.messages : [];
    const canRecall = Array.isArray(body.tools) && body.tools.some(tool => isRecord(tool) && isRecord(tool.function) && tool.function.name === 'recall_result');
    const ordered = orderToolResults(wireMessages), projection = projectRepeatedReceipts(ordered, canRecall);
    if (projection.duplicates || ordered.some((message,index) => message !== wireMessages[index])) { body = { ...body, messages: projection.messages }; init = { ...init, body: JSON.stringify(body) }; }
    const requestId = randomUUID().replaceAll('-', ''), start = performance.now(), outputLimit = Number(body.max_tokens || body.max_completion_tokens || 4000);
    await permit({ requestId, inputCharacters: JSON.stringify(body.messages || []).length + JSON.stringify(body.tools || []).length, outputLimit });
    if (projection.duplicates) emit({ type: 'context_projected', savedCharacters: projection.savedCharacters, duplicates: projection.duplicates });
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

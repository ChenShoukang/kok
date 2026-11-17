export interface ProgressFact { tool: string; fingerprint: string; current: boolean; turnId: string }
const stableReads = new Set(['read_items', 'read_document', 'read_presentation', 'read_workspace_file', 'inspect_document', 'inspect_presentation', 'review_sources', 'recall_result', 'update_task_spec']);

// Job polling, browser observations and writes are deliberately outside this guard.
// Any intervening different result, offset, file version, tool or user revision resets the run.
export function progressDecision(input: { calls: ProgressFact[] }) {
  const tail = input.calls.at(-1);
  if (!tail?.current || !stableReads.has(tail.tool)) return { action: 'continue', repeats: 0 };
  let repeats = 0, turn = '';
  for (const call of [...input.calls].reverse()) {
    if (!call.current || call.tool !== tail.tool || call.fingerprint !== tail.fingerprint) break;
    if (call.turnId && call.turnId === turn) continue;
    turn = call.turnId; repeats++;
  }
  return { action: repeats >= 3 ? 'stop' : repeats >= 2 ? 'warn' : 'continue', repeats };
}

import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { performance } from 'node:perf_hooks';
import { fileURLToPath } from 'node:url';
import { planTask } from '../runtime/research/src/business/planning.ts';

export const samplePath = fileURLToPath(new URL('../tests/fixtures/reliability-v0161.json', import.meta.url));
export function baseline() {
  const bytes = fs.readFileSync(samplePath), samples = JSON.parse(bytes);
  const cases = samples.cases.map(row => {
    const started = performance.now();
    const plan = planTask({ goal: row.goal, adjustments: row.adjustments || [], allowed: samples.sources.map(source => source.id), intent: 'assist', approvalPolicy: 'each',
      scope: { mode: row.scope || 'selected' }, explicitTools: null, features: { memory: false, delegation: false, sandbox: false, search: false, parent: false },
      environment: { workspace: true, rendering: false, tools: [], selectedFormats: [] },
      context: row.context || { taskId: '', goal: '', domain: '', workspaceId: '', files: [] } });
    const differences = Object.entries(row.expected).filter(([key, value]) => JSON.stringify(plan.contract[key]) !== JSON.stringify(value)).map(([key, expected]) => ({ field: key, expected, actual: plan.contract[key] }));
    if (row.coding !== undefined && row.coding !== plan.coding) differences.push({ field: 'coding', expected: row.coding, actual: plan.coding });
    return { id: row.id, group: row.group, passed: !differences.length, differences, durationMs: +(performance.now() - started).toFixed(3) };
  });
  return { format: 'kok.agent.reliability.planning', version: 1, created: new Date().toISOString(), sampleSha256: createHash('sha256').update(bytes).digest('hex'),
    sourceHashes: samples.sources.map(source => ({ id: source.id, sha256: createHash('sha256').update(source.text).digest('hex') })),
    scope: 'Deterministic planning only; not live-model completion, artifact quality or end-to-end performance.', requests: 0,
    passed: cases.filter(row => row.passed).length, total: cases.length, cases };
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const report = baseline(), index = process.argv.indexOf('--output');
  if (index >= 0) { if (!process.argv[index + 1]) throw Error('--output needs a path'); const target = path.resolve(process.argv[index + 1]); fs.mkdirSync(path.dirname(target), { recursive: true }); fs.writeFileSync(target, JSON.stringify(report, null, 2) + '\n'); }
  console.log(`Planning scenarios: ${report.passed}/${report.total}; 0 model requests. Not a live completion rate.`);
  for (const row of report.cases.filter(row => !row.passed)) console.log(row.id + ': ' + JSON.stringify(row.differences));
  if (report.passed !== report.total && !process.argv.includes('--record')) process.exitCode = 1;
}

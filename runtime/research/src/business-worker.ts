import { createInterface } from 'node:readline';
import { planTask, type PlanningInput } from './business/planning.ts';
import { reviewArtifact, reviewSources, type ReviewInput } from './business/review.ts';
import { codeProblem, deliveryProblem, type CodeEvidence, type DeliveryEvidence } from './business/completion.ts';
import { progressDecision } from './business/progress.ts';

// Pure, local business rules. No model client, network, user plugins, file IO or secrets are loaded here.
const lines = createInterface({ input: process.stdin, crlfDelay: Infinity });
lines.once('line', line => {
  let response: unknown;
  try {
    if (line.length > 16_000_000) throw new Error('Business input exceeds size limit');
    const message = JSON.parse(line.replace(/^\uFEFF/, ''));
    if (message.version !== 1 || typeof message.operation !== 'string' || !message.input) throw new Error('Invalid business protocol');
    let value: unknown;
    switch (message.operation) {
      case 'plan': value = planTask(message.input as PlanningInput); break;
      case 'review_artifact': value = reviewArtifact(message.input as ReviewInput); break;
      case 'review_sources': value = reviewSources(message.input as Parameters<typeof reviewSources>[0]); break;
      case 'code_problem': value = codeProblem(message.input as CodeEvidence); break;
      case 'delivery_problem': value = deliveryProblem(message.input as DeliveryEvidence); break;
      case 'progress': value = progressDecision(message.input as Parameters<typeof progressDecision>[0]); break;
      default: throw new Error('Unknown business operation');
    }
    response = { version: 1, ok: true, value };
  } catch (error) { response = { version: 1, ok: false, error: error instanceof Error ? error.message : 'Business rule failure' }; }
  process.stdout.write(JSON.stringify(response) + '\n', () => process.exit(0));
});

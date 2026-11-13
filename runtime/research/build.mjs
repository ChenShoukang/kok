import { build } from 'esbuild';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { z } from 'zod';
import { StepSchema, TaskSpecSchema, AssetSchema } from './src/contracts.ts';

const runtime = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(runtime, '../..');
const destination = path.join(root, 'dist/kok/runtime/research');
const pkg = JSON.parse(await fs.readFile(path.join(runtime, 'package.json'), 'utf8'));
execFileSync(process.execPath, [path.join(runtime, 'node_modules/typescript/bin/tsc'), '--project', path.join(runtime, 'tsconfig.json'), '--noEmit'], { stdio: 'inherit' });
await fs.mkdir(destination, { recursive: true });
await build({ entryPoints: [path.join(runtime, 'src/worker.ts')], outfile: path.join(destination, 'worker.mjs'),
  bundle: true, platform: 'node', format: 'esm', target: 'node24', minify: true, legalComments: 'eof',
  banner: { js: 'import { createRequire as __kokCreateRequire } from "node:module"; const require = __kokCreateRequire(import.meta.url);' },
});
await fs.writeFile(path.join(destination, 'protocol-v1.json'), JSON.stringify({ version: 1, step: z.toJSONSchema(StepSchema), task: z.toJSONSchema(TaskSpecSchema), asset: z.toJSONSchema(AssetSchema) }, null, 2) + '\n');
const nodeLicense = await fs.readFile(path.join(root, 'tools/research-node/LICENSE.txt'), 'utf8');
const notices = ['kok Research Runtime ' + pkg.version, 'Node.js ' + process.version, nodeLicense];
const dependencies = [];
for (const dir of await fs.readdir(path.join(runtime, 'node_modules'), { withFileTypes: true })) {
  if (!dir.isDirectory() || dir.name.startsWith('.')) continue;
  if (dir.name.startsWith('@')) {
    for (const item of await fs.readdir(path.join(runtime, 'node_modules', dir.name))) dependencies.push(path.join(dir.name, item));
  } else dependencies.push(dir.name);
}
for (const name of dependencies.sort()) {
  const directory = path.join(runtime, 'node_modules', name);
  let dependency;
  try { dependency = JSON.parse(await fs.readFile(path.join(directory, 'package.json'), 'utf8')); } catch { continue; }
  notices.push('\n\n' + dependency.name + ' ' + dependency.version + ' (' + dependency.license + ')\n');
  for (const file of await fs.readdir(directory)) {
    if (/^(licen[cs]e|notice)(\.|$)/i.test(file) && (await fs.stat(path.join(directory, file))).isFile()) notices.push(await fs.readFile(path.join(directory, file), 'utf8'));
  }
}
await fs.writeFile(path.join(destination, 'THIRD_PARTY_NOTICES.txt'), notices.join('\n'), 'utf8');
await fs.copyFile(process.execPath, path.join(destination, 'node.exe'));
const sha = async file => createHash('sha256').update(await fs.readFile(file)).digest('hex');
await fs.writeFile(path.join(destination, 'manifest.json'), JSON.stringify({
  version: pkg.version, kernel: 'deepagents', deepagents: pkg.dependencies.deepagents,
  langgraph: pkg.dependencies['@langchain/langgraph'], language: 'TypeScript', protocolVersion: 1, checkpointFormat: 2, node: process.version,
  nodeSha256: await sha(path.join(destination, 'node.exe')),
  workerSha256: await sha(path.join(destination, 'worker.mjs')),
  dependencyLockSha256: await sha(path.join(runtime, 'package-lock.json')),
}, null, 2) + '\n', 'utf8');
console.log('Research kernel bundled: ' + destination);

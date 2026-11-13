import { ResearchKernel } from '../kernel.mjs';
const agent = new ResearchKernel();
console.log(JSON.stringify({ framework: 'deepagents', startupMs: process.uptime() * 1000, rss: process.memoryUsage().rss, heap: process.memoryUsage().heapUsed, constructed: !!agent.saver }));

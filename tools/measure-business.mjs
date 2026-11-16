import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { performance } from 'node:perf_hooks';
import { fileURLToPath } from 'node:url';
import { planTask } from '../runtime/research/src/business/planning.ts';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const directory=path.join(root,'dist/kok/runtime/research');
const runtime=path.join(directory,'node.exe'),entry=path.join(directory,'business.mjs');
const count=12,input={goal:'根据资料创建400-600汉字DOCX报告，以及8页PPT',adjustments:[],allowed:['a','b'],intent:'assist',approvalPolicy:'each',scope:{mode:'selected'},explicitTools:null,
  features:{},environment:{workspace:false,rendering:false,tools:['read_items','create_document','create_presentation'],selectedFormats:[]},context:{files:[]}};
const envelope=JSON.stringify({version:1,operation:'plan',input})+'\n',runs=[];
for(let index=0;index<count;index++){
  const started=performance.now();
  const env={...process.env};delete env.NODE_OPTIONS;delete env.NODE_PATH;
  const child=spawnSync(runtime,['--max-old-space-size=160',entry],{input:envelope,encoding:'utf8',windowsHide:true,timeout:10000,env});
  if(child.error||child.status!==0)throw Error('Bundled business worker did not finish successfully.');
  const value=JSON.parse(child.stdout.replace(/^\uFEFF/,''));if(!value.ok||value.value.contract.RequestedPages!==8)throw Error('Business reply failed verification.');
  runs.push(+(performance.now()-started).toFixed(3));
}
const local=[];for(let index=0;index<count;index++){const started=performance.now();planTask(input);local.push(+(performance.now()-started).toFixed(3));}
const median=values=>[...values].sort((a,b)=>a-b)[Math.floor(values.length/2)];
const report={format:'kok.agent.business.measurement',version:1,created:new Date().toISOString(),runtimeManifest:JSON.parse(fs.readFileSync(path.join(directory,'manifest.json'),'utf8')),
  samples:count,inputBytes:Buffer.byteLength(envelope),processMs:runs,localRuleMs:local,processMedianMs:median(runs),localRuleMedianMs:median(local),requests:0,
  scope:'Bundled process wall time includes startup, IPC, rules and exit. In-process timings use the developer Node and are indicative only. No end-to-end or cold disk performance claim.'};
const outputIndex=process.argv.indexOf('--output');if(outputIndex>=0){const file=path.resolve(process.argv[outputIndex+1]);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,JSON.stringify(report,null,2)+'\n');}
console.log(JSON.stringify({samples:count,processMedianMs:report.processMedianMs,localRuleMedianMs:report.localRuleMedianMs,requests:0}));

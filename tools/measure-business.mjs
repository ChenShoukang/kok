import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { performance } from 'node:perf_hooks';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { planTask } from '../runtime/research/src/business/planning.ts';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const directory=path.join(root,'dist/kok/runtime/research');
const runtime=path.join(directory,'node.exe'),entry=path.join(directory,'business.mjs');
const count=12,input={goal:'根据资料创建400-600汉字DOCX报告，以及8页PPT',adjustments:[],allowed:['a','b'],intent:'assist',approvalPolicy:'each',scope:{mode:'selected'},explicitTools:null,
  features:{},environment:{workspace:false,rendering:false,tools:['read_items','create_document','create_presentation'],selectedFormats:[]},context:{files:[]}};
const median=values=>[...values].sort((a,b)=>a-b)[Math.floor(values.length/2)];
const round=value=>+value.toFixed(3),env=Object.fromEntries(['SystemRoot','WINDIR','TEMP','TMP'].filter(name=>process.env[name]).map(name=>[name,process.env[name]]));
// Instrument only this diagnostic subprocess. The shipped worker and wire contract stay unchanged.
const probe=`import { performance } from 'node:perf_hooks';import { writeSync } from 'node:fs';
const start=performance.now();let moduleLoadMs=null;
process.on('exit',()=>{const usage=process.resourceUsage();writeSync(2,'KOK_MEASURE '+JSON.stringify({node:process.version,bootstrapMs:performance.nodeTiming.bootstrapComplete,moduleLoadMs,lifetimeMs:process.uptime()*1000,residentBytes:process.memoryUsage().rss,maxRssKiB:usage.maxRSS||null,userCpuUs:usage.userCPUTime,systemCpuUs:usage.systemCPUTime})+'\\n');});
await import(process.argv[1]);moduleLoadMs=performance.now()-start;`;
const larger={...input,requirementsVersion:1,goal:input.goal+'。'+('任务背景与原始资料说明。'.repeat(120)),allowed:Array.from({length:24},(_,i)=>'source-'+i),
  adjustments:Array.from({length:8},(_,i)=>'保留原来的文档格式、长度与8页PPT。'+('受众背景与交付注意事项。'.repeat(30))+i)};
const cases=[];
for(const [name,current] of [['ordinary',input],['larger-context',larger]]){
  const envelope=JSON.stringify({version:1,operation:'plan',input:current})+'\n',runs=[],processes=[],local=[],serialization=[],parsing=[];
  for(let index=0;index<count;index++){
    const started=performance.now();
    const child=spawnSync(runtime,['--max-old-space-size=160','--input-type=module','--eval',probe,pathToFileURL(entry).href],{input:envelope,encoding:'utf8',windowsHide:true,timeout:10000,env,maxBuffer:2_000_000});
    if(child.error||child.status!==0)throw Error('Bundled business worker did not finish successfully.');
    const value=JSON.parse(child.stdout.replace(/^\uFEFF/,''));if(!value.ok||value.value.contract.RequestedPages!==8||value.value.contract.RequiredDocumentFormat!=='DOCX')throw Error('Business reply failed verification.');
    const wallMs=round(performance.now()-started),line=child.stderr.split('\n').find(line=>line.startsWith('KOK_MEASURE '));if(!line)throw Error('Diagnostic resource counters were not returned.');
    runs.push(wallMs);processes.push({...JSON.parse(line.slice('KOK_MEASURE '.length)),wallMs});
  }
  for(let index=0;index<count;index++){
    let started=performance.now();const serialized=JSON.stringify({version:1,operation:'plan',input:current});serialization.push(round(performance.now()-started));
    started=performance.now();JSON.parse(serialized);parsing.push(round(performance.now()-started));
    started=performance.now();planTask(current);local.push(round(performance.now()-started));
  }
  cases.push({name,samples:count,inputBytes:Buffer.byteLength(envelope),firstObservedProcessMs:runs[0],subsequentProcessMedianMs:median(runs.slice(1)),processMs:runs,processes,localRuleMs:local,
    processMedianMs:median(runs),localRuleMedianMs:median(local),serializationMedianMs:median(serialization),parsingMedianMs:median(parsing),residentMedianMiB:round(median(processes.map(row=>row.residentBytes))/1024/1024),peakResidentMiB:round(Math.max(...processes.map(row=>(row.maxRssKiB||row.residentBytes/1024)*1024))/1024/1024)});
}
const ordinary=cases[0],report={format:'kok.agent.business.measurement',version:2,created:new Date().toISOString(),runtimeManifest:JSON.parse(fs.readFileSync(path.join(directory,'manifest.json'),'utf8')),
  samples:count,processCount:count*cases.length,inputBytes:ordinary.inputBytes,processMs:ordinary.processMs,localRuleMs:ordinary.localRuleMs,processMedianMs:ordinary.processMedianMs,localRuleMedianMs:ordinary.localRuleMedianMs,requests:0,cases,localNode:process.version,
  scope:'Instrumented bundled Node process wall time includes startup, IPC, rules and exit. Resource counters measure the child; RSS includes Node/runtime memory. Local rule/JSON microtimings use the developer Node and source module, not a persistent production service. First observed launch is not a cold-disk measurement; subsequent launches share OS caches. Phases are indicative and are not additive accounting. No end-to-end speedup or 20% efficiency claim.'};
const outputIndex=process.argv.indexOf('--output');if(outputIndex>=0){const file=path.resolve(process.argv[outputIndex+1]);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,JSON.stringify(report,null,2)+'\n');}
console.log(JSON.stringify({samplesPerCase:count,processCount:report.processCount,cases:cases.map(row=>({name:row.name,inputBytes:row.inputBytes,processMedianMs:row.processMedianMs,localRuleMedianMs:row.localRuleMedianMs,serializationMedianMs:row.serializationMedianMs,parsingMedianMs:row.parsingMedianMs,residentMedianMiB:row.residentMedianMiB,peakResidentMiB:row.peakResidentMiB})),requests:0}));

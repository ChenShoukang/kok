'use strict';
// Opt-in live measurement for the 30-task Agent baseline. NEVER part of default tests or CI.
//
// Default (inert): without KOK_LIVE_AI=1 and KOK_LIVE_CONFIG this file prints one SKIPPED line and exits 0.
// Live (explicitly authorized only): the isolated profile is started by tools/test-desktop.ps1 with
// --debug --test-silent and the authorized ai-settings.json is copied into KOK_TEST_PROFILE first, so the
// harness never reads, prints or stores an API key. It seeds bundled synthetic samples plus synthetic
// Markdown documents, drives the real Agent over CDP, records per-task tokens/cost/time/retries/duplicate
// reads/citations/openability/novice signals and clears the AI config from the isolated profile at the end.
//
// Run one full pass (after the lead wires the suite into tools/test-desktop.ps1):
//   $env:KOK_LIVE_AI='1'; $env:KOK_LIVE_CONFIG='X:\private\ai-settings.json'; .\tools\test-desktop.ps1 -Suites agent-live-samples
// Subset / budget guard:
//   $env:KOK_LIVE_SAMPLES='local-count-videos,qa-pdf-second-page'
//   $env:KOK_LIVE_MAX_REQUESTS='60'
// This harness does not update the project request ledger; report.ledger.usedRequests is what the
// authorized operator adds to the ledger after reviewing the run.
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const assert=require('node:assert/strict');
const {samples:SAMPLES}=require('./agent-task-samples.cjs');

const ROOT=path.resolve(__dirname,'..');
const CDP='http://127.0.0.1:9225';
const REPORT_FORMAT='kok.agent.samples';
const REPORT_VERSION=1;
// Per-category request budget. Local samples are expected to use 0 model requests; 4 is only the safety
// cap for a pre-router build that falls back to the model path.
const REQUEST_LIMITS=Object.freeze({local:4,generation:6,qa:6,complex:8,'needs-input':6});
const SAMPLE_TIMEOUT_MS=Object.freeze({local:90000,generation:210000,qa:180000,complex:270000,'needs-input':180000});
const BUDGET=14000;
const MAX_APPROVALS=24;
// Tools that can modify library metadata or files. Opening an item is not a modification.
const MODIFY_TOOLS=Object.freeze(['write_note','set_collection','add_tags','create_document','create_presentation','copy_presentation','edit_presentation','replace_document_text','remember_memory','editOrganization','applyOrganization']);
const TRAVEL_COLLECTION='评估·旅行';
// Local-card truth checks: the native router contract is {total, stats:[{label,value}], items:[...]}.
// These assertions make the 8 local samples measurable without calling a model; adapt here if the
// agentLocalPlan response fields change.
const LOCAL_VERIFY=Object.freeze({
 'local-count-videos':(local,truth)=>Number(local.total)===truth.byKind.video,
 'local-count-pdf':(local,truth)=>Number(local.total)===truth.pdfCount,
 'local-list-collection-travel':(local,truth)=>Number(local.total)===truth.collections[TRAVEL_COLLECTION]&&Array.isArray(local.items),
 'local-list-recent-three-days':(local,truth)=>Number(local.total)===truth.all,
 'local-collections-overview':(local,truth)=>{const names=Object.keys(truth.collections).filter(Boolean);const labels=(local.stats||[]).map(row=>String(row.label||row.name||''));return names.length>0&&names.every(name=>labels.some(label=>label.includes(name)));},
 'local-tags-design':(local,truth)=>Number(local.total)===truth.tagDistinct&&(local.stats||[]).some(row=>String(row.label||'').includes('设计')),
 'local-kinds-distribution':(local,truth)=>{const values=(local.stats||[]).map(row=>Number(row.value));return values.length>0&&values.reduce((sum,value)=>sum+(Number.isFinite(value)?value:0),0)===truth.all;},
 'local-count-collection-travel':(local,truth)=>Number(local.total)===truth.collections[TRAVEL_COLLECTION]
});

const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const safe=text=>String(text==null?'':text).replace(/sk-[A-Za-z0-9_-]{16,}/g,'[redacted]');
const pad=(value,width)=>String(value==null?'':value).padEnd(width);
function timestamp(){return new Date().toISOString().replace(/[:.]/g,'-');}
function hashFile(file){return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');}
function stableStringify(value){
 if(value===null||typeof value!=='object')return JSON.stringify(value);
 if(Array.isArray(value))return '['+value.map(stableStringify).join(',')+']';
 return '{'+Object.keys(value).sort().map(key=>JSON.stringify(key)+':'+stableStringify(value[key])).join(',')+'}';
}
function readLedgerCount(file){
 if(!file||!fs.existsSync(file))return null;
 try{const text=fs.readFileSync(file,'utf8').trim();if(!text)return null;try{const json=JSON.parse(text);if(typeof json==='number'&&Number.isFinite(json))return json;const value=Number(json.totalCount);return Number.isFinite(value)?value:null;}catch{const value=Number(text);return Number.isFinite(value)?value:null;}}catch{return null;}
}

// Inert by default: exit 0 before importing Playwright or touching the app/network.
if(process.env.KOK_LIVE_AI!=='1'||!process.env.KOK_LIVE_CONFIG){
 console.log('SKIPPED: live evaluation is opt-in (set KOK_LIVE_AI=1 and KOK_LIVE_CONFIG and start the isolated profile through tools/test-desktop.ps1).');
 process.exit(0);
}

(async()=>{
 const report={
  format:REPORT_FORMAT,
  version:REPORT_VERSION,
  harness:'tests/agent-live-samples.cjs',
  createdAt:new Date().toISOString(),
  model:'',
  syntheticSources:true,
  sampleSet:'tests/agent-task-samples.cjs',
  sampleSetVersion:1,
  selected:0,
  requestPlan:null,
  skipped:[],samples:[],aggregate:null,
  uiContract:null,seeded:null,ledger:null,
  originalFilesPreserved:null,keyCleared:false,passed:false
 };
 let browser=null,page=null,configured=false,keyCleared=false;
 const root=process.env.KOK_TEST_PROFILE;
 try{
  assert(root,'KOK_TEST_PROFILE must be the isolated profile created by tools/test-desktop.ps1');
  const selected=selectSamples();
  report.selected=selected.length;
  const requestCap=Number(process.env.KOK_LIVE_MAX_REQUESTS||0)||selected.reduce((sum,sample)=>sum+REQUEST_LIMITS[sample.category],0);
  report.requestPlan={
   cap:requestCap,
   perCategory:Object.fromEntries(Object.keys(REQUEST_LIMITS).map(category=>[category,{samples:selected.filter(sample=>sample.category===category).length,requestLimit:REQUEST_LIMITS[category]}])),
   expectedRequests:selected.reduce((sum,sample)=>sum+(sample.category==='local'?0:REQUEST_LIMITS[sample.category]),0),
   worstCaseRequests:selected.reduce((sum,sample)=>sum+REQUEST_LIMITS[sample.category],0)
  };
  report.ledger={provided:!!process.env.KOK_LIVE_LEDGER,before:readLedgerCount(process.env.KOK_LIVE_LEDGER),usedRequests:0};
  // Optional pricing (per million tokens, same currency as the product limits) so usage.cost is recorded;
  // without both rates the product reports cost 0, which must not be read as "free".
  const rate=name=>{const value=Number(process.env[name]||0);return Number.isFinite(value)&&value>0?value:0;};
  const pricing={inputRate:rate('KOK_LIVE_INPUT_RATE'),outputRate:rate('KOK_LIVE_OUTPUT_RATE')};
  pricing.enabled=pricing.inputRate>0&&pricing.outputRate>0;
  report.pricing=pricing;

  const {chromium}=require('./browser.cjs');
  browser=await chromium.connectOverCDP(CDP);
  const context=browser.contexts()[0];
  assert(context,'No CDP browser context');
  await context.route(/^https?:\/\//,route=>['app.kok.local','media.kok.local'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
  page=context.pages().find(candidate=>candidate.url().includes('app.kok.local')&&!candidate.url().includes('preview='));
  assert(page,'No app.kok.local page in the isolated test window');
  page.setDefaultTimeout(20000);
  await page.waitForFunction(()=>!$('#startup'));
  await page.setViewportSize({width:1280,height:860});
  const config=await page.evaluate(()=>native('aiConfig'));
  assert(config&&config.hasKey,'The isolated profile must contain the explicitly authorized AI config (KOK_LIVE_CONFIG copied in by tools/test-desktop.ps1). No key was read.');
  assert(new URL(config.endpoint).hostname==='api.deepseek.com','The isolated profile AI endpoint must be api.deepseek.com');
  report.model=config.model;configured=true;
  report.profileIsolated=true;

  await page.evaluate(async()=>{await refresh();navigate('agent');});
  await page.waitForFunction(()=>state.route==='agent'&&!!$('#agent-input'));
  report.uiContract=await measureUiContract(page);

  // Capture model/local routing without changing behavior: the UI calls native('agentLocalPlan') and
  // native('agentTaskStart') from the page global.
  await page.evaluate(()=>{
   if(window.__kokSampleProbe)return;
   window.__kokSampleProbe={local:0,start:0,starts:[]};
   const previous=window.native;
   window.native=function(action,args,...rest){
    try{
     if(action==='agentLocalPlan')window.__kokSampleProbe.local++;
     if(action==='agentTaskStart'){window.__kokSampleProbe.start++;window.__kokSampleProbe.starts.push({requestLimit:args&&args.requestLimit,protocol:args&&args.protocol});}
    }catch{}
    return previous.call(window,action,args,...rest);
   };
  });

  const seeded=await seedFixtures(page);
  report.seeded=seeded.summary;
  const writeTools=await page.evaluate(()=>Kok.catalog().tools.filter(tool=>tool.write).map(tool=>tool.name));
  const originals=await collectOriginalFiles(page);
  const started=Date.now();
  let usedRequests=0;

  for(let index=0;index<selected.length;index++){
   const sample=selected[index];
   if(usedRequests+REQUEST_LIMITS[sample.category]>requestCap){
    report.skipped=selected.slice(index).map(row=>row.id);
    console.log('BUDGET: stopping before '+sample.id+' ('+usedRequests+' requests used, cap '+requestCap+').');
    break;
   }
   const row=await runSample({page,sample,seeded,writeTools,report,pricing});
   report.samples.push(row);
   usedRequests+=Number(row.metrics.requests)||0;
   console.log(sampleLine(row));
   if(row.fatal)break;
  }

  report.originalFilesPreserved=await verifyOriginalFiles(originals);
  report.aggregate=aggregate(report.samples,report.skipped.length);
  report.ledger.usedRequests=report.aggregate.requests;
  report.ledger.suggestedAdditionalCount=report.aggregate.requests;
  report.ledger.withinCap=report.ledger.usedRequests<=requestCap;
  report.durationMs=Date.now()-started;
  report.passed=report.samples.length===selected.length&&report.samples.every(row=>row.passed)&&report.samples.every(row=>!row.fatal)&&report.originalFilesPreserved===true&&report.ledger.withinCap;
  if(!report.passed)process.exitCode=1;
 }catch(error){
  report.error=safe(error&&error.message||error);
  report.passed=false;
  process.exitCode=1;
 }finally{
  if(configured&&page){
   try{
    await page.evaluate(async()=>{if(AgentUi.state.job)await AgentUi.stop();await native('saveAiConfig',{clearKey:true,apiKey:'',automatic:false});});
    keyCleared=await page.evaluate(async()=>(await native('aiConfig')).hasKey===false);
   }catch{keyCleared=false;}
  }
  if(root){
   const configFile=path.join(root,'ai-settings.json');
   if(fs.existsSync(configFile)){try{fs.unlinkSync(configFile);}catch{keyCleared=false;}}
  }
  report.keyCleared=!!keyCleared;
  if(report.ledger&&report.aggregate){
   report.ledger.after=readLedgerCount(process.env.KOK_LIVE_LEDGER);
   report.ledger.expectedAfter=typeof report.ledger.before==='number'?report.ledger.before+report.aggregate.requests:null;
  }
  report.finishedAt=new Date().toISOString();
  const reportPath=writeReport(report);
  if(browser)await browser.close().catch(()=>{});
  if(report.aggregate)printSummary(report);
  console.log('Live samples report: '+reportPath);
  console.log('Key cleared from the isolated profile: '+report.keyCleared);
 }
})().catch(error=>{console.error('Live samples could not initialize: '+safe(error&&error.message||error));process.exitCode=1;});

// ------------------------------------------------------------------------------------------------- helpers
function selectSamples(){
 const ids=(process.env.KOK_LIVE_SAMPLES||'').split(',').map(id=>id.trim()).filter(Boolean);
 if(!ids.length)return SAMPLES.slice();
 const chosen=SAMPLES.filter(sample=>ids.includes(sample.id));
 const unknown=ids.filter(id=>!SAMPLES.some(sample=>sample.id===id));
 assert(unknown.length===0,'Unknown KOK_LIVE_SAMPLES id(s): '+unknown.join(','));
 assert(chosen.length>0,'KOK_LIVE_SAMPLES did not match any sample');
 return chosen;
}

async function measureUiContract(page){
 const raw=await page.evaluate(()=>{
  const advanced=document.querySelector('#agent-advanced');
  return {
   advancedPresent:!!advanced,
   advancedCollapsed:advanced?!advanced.open:false,
   composerPresent:!!document.querySelector('#agent-input'),
   sendPresent:!!document.querySelector('#agent-form button[type=submit]'),
   defaultSurfaceSelectors:['#agent-input','#agent-form'].filter(selector=>!!document.querySelector(selector)).length
  };
 });
 return {...raw,startsWithoutHelp:raw.advancedPresent&&raw.advancedCollapsed&&raw.composerPresent&&raw.sendPresent};
}

async function seedFixtures(page){
 const result=await page.evaluate(async()=>{
  const out={sampleImport:false,documents:{},imported:{}};
  try{await native('importSamples');out.sampleImport=true;}catch(error){out.sampleImport=false;}
  await refresh();
  const create=async(name,collection,text)=>{const entry=await native('createDocument',{name,format:'md',collection,text});return{id:entry.id,name:entry.name};};
  out.documents.v1=await create('评估样本·海边旅行初稿','评估·旅行','# 海边旅行初稿\n日期：2026年11月5日。目的地：厦门。交通：火车往返。总预算1200元。\n酒店名称和每晚住宿价格未提供。');
  out.documents.revision=await create('评估样本·海边旅行修订稿','评估·旅行','# 海边旅行修订稿\n日期：2026年11月5日。目的地：厦门。\n新版总预算1800元，替代初稿1200元。酒店名称与住宿价格仍未知。');
  out.documents.photo=await create('评估样本·摄影准备清单','评估·旅行','# 摄影准备清单\n相机一台、两块电池、备用存储卡、充电器。\n住宿费用未确定。');
  out.documents.plant=await create('评估样本·植物记录','评估·生活','# 植物记录\n阳台有2盆植物，每7天浇水一次。');
  out.documents.release=await create('评估样本·发布检查清单','评估·工作','# 发布检查清单\n1. 构建通过。\n2. 原生检查通过。\n3. 隐私扫描通过。');
  out.documents.reading=await create('评估样本·阅读计划','评估·学习','# 阅读计划\n2026年11月8日：读完第一部分。\n2026年11月15日：整理读书笔记。');
  out.documents.weekly=await create('评估样本·项目周报','评估·工作','# 项目周报\n本周完成资料整理与归档，下周继续复习。');
  // Seed one tag through the normal metadata action so the deterministic tag filter has data.
  let tagSeeded=false;
  try{
   const reading=state.items.find(item=>item.name==='评估样本·阅读计划');
   if(reading){await native('update',{id:reading.id,patch:{tags:['设计']}});tagSeeded=true;}
  }catch{tagSeeded=false;}
  out.tagSeeded=tagSeeded;
  await refresh();
  const items=state.items;
  const byKind={},byExt={},collections={},tags={};
  for(const item of items){
   byKind[item.kind]=(byKind[item.kind]||0)+1;
   byExt[item.ext]=(byExt[item.ext]||0)+1;
   const key=item.collection||'';
   collections[key]=(collections[key]||0)+1;
   for(const tag of item.tags||[])tags[tag]=(tags[tag]||0)+1;
  }
  const pick=predicate=>{const item=items.find(predicate);return item?item.id:'';};
  out.imported.pdfId=pick(item=>item.ext==='PDF');
  out.imported.videoId=pick(item=>item.kind==='video');
  out.imported.docxId=pick(item=>item.ext==='DOCX');
  out.truth={
   all:items.length,
   byKind,byExt,collections,tags,
   collectionNames:Object.keys(collections).filter(Boolean),
   pdfCount:byExt.PDF||0,
   tagDistinct:Object.keys(tags).length,
   tagDesign:tags['设计']||0,
   ids:items.map(item=>item.id)
  };
  return out;
 });
 const truth=result.truth;
 const libraryItems=truth.ids.length;
 // The synthetic documents are the controlled sources; imported bundled samples add PDF/video/office fixtures.
 const docs=result.documents;
 const pdfId=result.imported.pdfId,videoId=result.imported.videoId,docxId=result.imported.docxId;
 return {
  docs,pdfId,videoId,docxId,truth,
  ids:{...docs,pdfId,videoId,docxId},
  allIds:truth.ids,
  summary:{
   sampleImport:result.sampleImport,
   tagSeeded:!!result.tagSeeded,
   syntheticDocuments:Object.keys(docs).length,
   libraryItems,
   byKind:truth.byKind,
   byExt:truth.byExt,
   collections:truth.collectionNames
  }
 };
}

function contextFor(sample,seeded){
 const asId=entry=>typeof entry==='string'?entry:(entry&&entry.id)||'';
 const byId={
  'gen-summary-selected-docs':[seeded.docs.v1,seeded.docs.revision],
  'gen-note-from-docs':[seeded.docs.v1,seeded.docs.photo],
  'gen-presentation-five-pages':[seeded.docs.v1,seeded.docs.revision,seeded.docs.photo],
  'gen-compare-two-versions':[seeded.docs.v1,seeded.docs.revision],
  'gen-batch-tags-proposal':[seeded.docs.v1,seeded.docs.revision],
  'gen-batch-collection-proposal':[seeded.docs.v1,seeded.docs.revision],
  'qa-travel-budget-unknown':[seeded.docs.v1,seeded.docs.photo],
  'qa-compare-version-budget':[seeded.docs.v1,seeded.docs.revision],
  'qa-plant-watering':[seeded.docs.plant],
  'qa-release-checklist':[seeded.docs.release,seeded.docxId],
  'qa-reading-plan-dates':[seeded.docs.reading],
  'qa-travel-preparation':[seeded.docs.photo,seeded.docs.v1],
  'complex-delegation-two-docs':[seeded.docs.v1,seeded.docs.revision],
  'complex-long-task-note-followup':[seeded.docs.v1,seeded.docs.revision],
  'complex-pdf-video-presentation':[seeded.pdfId,seeded.videoId],
  'needs-hotel-price':[seeded.docs.v1,seeded.docs.photo]
 };
 const fixed=byId[sample.id];
 const ids=(fixed||[]).map(asId).filter(Boolean);
 if(ids.length)return ids;
 if(sample.fixture.kind==='pdf')return [seeded.pdfId].map(asId).filter(Boolean);
 if(sample.fixture.kind==='video-subs')return [seeded.videoId].map(asId).filter(Boolean);
 if(sample.fixture.kind==='documents')return [seeded.docs.v1,seeded.docs.revision,seeded.docs.photo].map(asId).filter(Boolean);
 return [];
}

async function collectOriginalFiles(page){
 const rows=await page.evaluate(()=>state.items.map(item=>({id:item.id,path:item.path,name:item.name})));
 return rows.filter(row=>row.path&&fs.existsSync(row.path)).map(row=>({id:row.id,name:row.name,path:row.path,sha256:hashFile(row.path)}));
}

async function verifyOriginalFiles(originals){
 try{
  for(const row of originals){
   if(!fs.existsSync(row.path))return false;
   if(hashFile(row.path)!==row.sha256)return false;
  }
  return true;
 }catch{return false;}
}

async function libraryFingerprint(page){
 return page.evaluate(()=>JSON.stringify(state.items.map(item=>({id:item.id,collection:item.collection||'',tags:(item.tags||[]).slice().sort(),notes:item.notes||'',favorite:!!item.favorite,ext:item.ext,kind:item.kind,added:item.added||'',name:item.name})).sort((a,b)=>a.id.localeCompare(b.id))));
}

async function resetSample(page,sample,seeded,pricing){
 const ids=contextFor(sample,seeded);
 const intent=['qa','needs-input'].includes(sample.category)?'discuss':'assist';
 const selectedScope=ids.length>0;
 await page.evaluate(async options=>{
  if(AgentUi.state.job)await AgentUi.stop();
  await AgentUi.newSession();
  Object.assign(AgentUi.state,{
   context:options.ids,scope:options.scope,intent:options.intent,skill:'',budget:options.budget,
   requestLimit:options.requestLimit,allowSandbox:false,allowDelegation:options.allowDelegation,
   useMemory:false,costLimit:0,inputRate:options.inputRate,outputRate:options.outputRate,protocol:'auto',
   approvalPolicy:'each',semanticCompression:false,options:false
  });
  AgentUi.render();
 },{ids,scope:selectedScope?'selected':'all',intent,budget:BUDGET,requestLimit:REQUEST_LIMITS[sample.category],allowDelegation:sample.id==='complex-delegation-two-docs',inputRate:pricing?pricing.inputRate:0,outputRate:pricing?pricing.outputRate:0});
 await page.waitForFunction(()=>!!$('#agent-input')&&!AgentUi.state.job);
 await page.evaluate(()=>{window.__kokSampleProbe.local=0;window.__kokSampleProbe.start=0;window.__kokSampleProbe.starts=[];});
 return {ids,selectedScope,intent};
}

async function runSample({page,sample,seeded,writeTools,report,pricing}){
 const started=Date.now();
 const row={
  id:sample.id,title:sample.title,category:sample.category,expectedRoute:sample.expectedRoute,
  expectedLocalKind:sample.expectedLocalKind||'',actualRoute:'none',fallback:false,misrouted:false,
  requestLimit:REQUEST_LIMITS[sample.category],fixtureKind:sample.fixture.kind,
  passed:false,fatal:'',checks:{},noviceChecks:{},metrics:null,usage:null,
  durationMs:0,taskStatus:'',timedOut:false,errors:[],result:null,citations:null,artifact:null,local:null,
  duplicateReads:0,reusedReadRefs:0,writes:{proposed:0,done:0,applied:0,undone:0,proposedTools:[]},
  restore:{appliedChanges:0,libraryRestored:null},ui:{},answerPreview:'',planSteps:0,verificationLevel:'',delegationUsed:false
 };
 const {ids,selectedScope}=await resetSample(page,sample,seeded,pricing);
 const beforeMeta=await libraryFingerprint(page);
 const scopeIds=selectedScope?ids:seeded.allIds;
 try{
  await page.locator('#agent-input').fill(sample.goal);
  await page.locator('#agent-form button[type=submit]').first().click();
  let taskId='';let taskStarts=0;let localMessage=null;let snap=null;let timedOut=false;
  let pendingObserved=false;let preApprovalUnchanged=true;let approvals=0;
  while(Date.now()-started<SAMPLE_TIMEOUT_MS[sample.category]){
   const view=await readUiView(page);
   taskStarts=Math.max(taskStarts,view.taskStarts);
   if(view.localResult&&taskStarts===0){localMessage=view.localResult;break;}
   if(view.watch&&!taskId)taskId=view.watch;
   if(view.errors.length)row.errors=view.errors;
   // A task that failed before the native task existed (e.g. invalid scope or disabled module) should be
   // recorded immediately instead of burning the whole timeout.
   if(!view.job&&!view.watch&&!view.localResult&&view.errors.length&&Date.now()-started>1500)break;
   if(taskId){
    try{snap=await page.evaluate(id=>native('agentTaskRead',{taskId:id}),taskId);}catch{snap=null;}
    if(snap&&!snap.running)break;
    if(snap&&snap.running){
     const pending=(snap.messages||[]).filter(message=>message.role==='tool'&&message.status==='pending');
     // Record whether the library was still untouched when the first confirmation appeared: that is the
     // "confirms only when needed / never applies before approval" novice signal.
     if(pending.length&&!pendingObserved){
      pendingObserved=true;
      const current=await libraryFingerprint(page);
      preApprovalUnchanged=current===beforeMeta;
     }
     for(const call of pending){
      if(!call||!call.id)continue;
      const approve=sample.category==='generation'||sample.category==='complex';
      try{
       snap=await page.evaluate(({taskId,callId,approve})=>native('agentTaskDecide',{taskId,callId,approve}),{taskId,callId:call.id,approve:!!approve});
       approvals++;
      }catch(error){row.errors.push(safe(error.message||error));}
      if(approvals>MAX_APPROVALS)break;
     }
    }
   }
   await sleep(250);
  }
  if(!localMessage&&(!snap||snap.running)){
   timedOut=true;row.timedOut=true;
   try{await page.evaluate(async()=>{if(AgentUi.state.job)await AgentUi.stop();else if(AgentUi.state.watch)await native('agentTaskStop',{taskId:AgentUi.state.watch});});}catch{}
   await sleep(600);
   if(taskId){try{snap=await page.evaluate(id=>native('agentTaskRead',{taskId:id}),taskId);}catch{}}
  }
  // Let the UI append the persisted result card after agentFinish.
  try{await page.waitForFunction(()=>!AgentUi.state.job,null,{timeout:8000});}catch{}
  await sleep(350);
  const finalView=await readUiView(page);
  if(finalView.errors.length)row.errors=finalView.errors;
  const writeCalls=((snap&&snap.messages)||[]).filter(message=>message.role==='tool'&&writeTools.includes(message.tool));
  const doneWrites=writeCalls.filter(message=>message.status==='done');
  const appliedWrites=doneWrites.filter(message=>MODIFY_TOOLS.includes(message.tool));
  row.requestLimit=REQUEST_LIMITS[sample.category];
  row.taskStatus=snap?snap.status:'';
  row.planSteps=snap&&snap.plan?snap.plan.length:0;
  row.verificationLevel=snap&&snap.verification&&snap.verification.level?snap.verification.level:'';
  row.delegationUsed=((snap&&snap.messages)||[]).some(message=>message.tool==='delegate_task');
  row.writes.proposed=writeCalls.length;
  row.writes.done=doneWrites.length;
  row.writes.applied=appliedWrites.length;
  row.writes.proposedTools=[...new Set(writeCalls.map(message=>message.tool))];
  row.writes.approvalObserved=pendingObserved;
  row.writes.preApprovalUnchanged=preApprovalUnchanged;
  row.routeEvidence={agentLocalPlanCalls:finalView.localCalls,agentTaskStartCalls:taskStarts,taskId:taskId||''};

  // Route: a persisted local result with zero agentTaskStart calls is the native local path.
  if(localMessage&&taskStarts===0){
   row.actualRoute='local';
   row.local={kind:localMessage.kind||sample.expectedLocalKind,title:localMessage.title||'',summary:safe(localMessage.summary||'').slice(0,400),total:Number(localMessage.total)||0,statsCount:(localMessage.stats||[]).length,itemsCount:(localMessage.items||[]).length,followups:(localMessage.followups||[]).length,truthMatched:null};
   const verifier=LOCAL_VERIFY[sample.id];
   if(verifier){try{row.local.truthMatched=!!verifier(localMessage,seeded.truth);}catch{row.local.truthMatched=false;}}
   row.metrics={inputTokens:0,outputTokens:0,cachedTokens:0,cost:0,durationMs:Date.now()-started,requests:0,retries:0,duplicateReads:0,opened:0,citationsValid:true,novice:0};
   row.usage={inputTokens:0,outputTokens:0,cachedTokens:0,cost:0,estimated:false,requests:0,repairs:0};
  }else if(snap){
   row.actualRoute='model';
   row.usage={inputTokens:Number((snap.usage||{}).inputTokens)||0,outputTokens:Number((snap.usage||{}).outputTokens)||0,cachedTokens:Number((snap.usage||{}).cachedTokens)||0,cost:Number((snap.usage||{}).cost)||0,estimated:!!(snap.usage||{}).estimated,requests:Number((snap.usage||{}).requests)||0,repairs:Number((snap.usage||{}).repairs)||0};
  }else{
   row.actualRoute='none';
   row.usage={inputTokens:0,outputTokens:0,cachedTokens:0,cost:0,estimated:false,requests:0,repairs:0};
  }
  row.fallback=sample.expectedRoute==='local'&&row.actualRoute!=='local';
  row.misrouted=sample.expectedRoute==='model'&&row.actualRoute!=='model';
  row.durationMs=Date.now()-started;

  // Citations: answer source ids must resolve to real library items inside the seeded fixture scope.
  const agentAnswers=finalView.messages.filter(message=>message.role==='agent'&&!message.meta&&!message.interrupted);
  const lastAnswer=agentAnswers.length?agentAnswers[agentAnswers.length-1]:null;
  const resultMessage=finalView.resultMessages.length?finalView.resultMessages[finalView.resultMessages.length-1]:null;
  const answerText=((snap&&snap.verification&&snap.verification.summary)||'')+'\n'+agentAnswers.map(message=>message.text||'').join('\n');
  const sourceIds=[...new Set(agentAnswers.flatMap(message=>message.sourceIds||[]))].filter(id=>typeof id==='string'&&id.length>0);
  const resolvable=await page.evaluate(ids=>ids.filter(id=>{try{return !!byId(id);}catch{return false;}}),sourceIds);
  const citationsValid=sourceIds.length>0&&resolvable.length===sourceIds.length&&sourceIds.every(id=>scopeIds.includes(id))&&(!sample.acceptance.citationsRequired||sourceIds.length>0);
  row.citations={
   required:!!sample.acceptance.citationsRequired,
   count:sourceIds.length,
   resolvable:resolvable.length,
   valid:citationsValid,
   idsPreview:sourceIds.slice(0,6)
  };

  // Artifact: a created document/presentation must resolve to a real item with the expected extension.
  let artifact=null;
  for(const call of [...doneWrites].reverse()){
   const createdId=call.result&&call.result.id;
   if(!createdId)continue;
   if(!['create_document','create_presentation'].includes(call.tool))continue;
   const item=await page.evaluate(id=>{try{const entry=byId(id);return entry?{id:entry.id,name:entry.name,ext:entry.ext,kind:entry.kind,path:entry.path}:null;}catch{return null;}},createdId);
   if(item){artifact=item;break;}
  }
  const expectedExt=sample.acceptance.expectedArtifactExt||'';
  const artifactExists=!!artifact&&!!artifact.path&&fs.existsSync(artifact.path);
  const artifactExtOk=!!artifact&&(!expectedExt||String(artifact.ext||'').toLowerCase()===expectedExt);
  row.artifact={expectedExt,ext:artifact?artifact.ext:'',id:artifact?artifact.id:'',name:artifact?artifact.name:'',openable:expectedExt?artifactExists&&artifactExtOk:null};
  let opened=false;
  const localItemId=localMessage&&Array.isArray(localMessage.items)&&localMessage.items[0]?localMessage.items[0].id:'';
  const openTarget=artifact?artifact.id:(sourceIds[0]||localItemId||'');
  if(openTarget)opened=await tryOpenItem(page,openTarget);
  row.metrics={
   inputTokens:row.usage.inputTokens,outputTokens:row.usage.outputTokens,cachedTokens:row.usage.cachedTokens,
   cost:row.usage.cost,durationMs:row.durationMs,requests:row.usage.requests,
   retries:(row.usage.repairs||0)+requestRetries(snap),
   duplicateReads:0,opened:opened?1:0,
   citationsValid:!!sample.acceptance.citationsRequired?citationsValid:true,
   novice:0
  };
  const readStats=duplicateReadStats(snap);
  row.metrics.duplicateReads=readStats.duplicates;
  row.duplicateReads=readStats.duplicates;
  row.reusedReadRefs=readStats.reused;
  row.requestRetries=requestRetries(snap);

  // Result presence and UI contract signals.
  const resultPresent=!!resultMessage||!!(lastAnswer&&lastAnswer.text)||finalView.localCardCount>0||finalView.resultCardCount>0;
  row.result={
   present:resultPresent,
   title:(resultMessage&&resultMessage.title)||(row.local?row.local.title:''),
   status:(resultMessage&&resultMessage.status)||'',
   sourceCount:(resultMessage&&Number(resultMessage.sourceCount))||0,
   citationCount:(resultMessage&&Number(resultMessage.citationCount))||0,
   answerPreview:safe((lastAnswer&&lastAnswer.text)||(localMessage&&localMessage.summary)||'').slice(0,600)
  };
  row.ui={...finalView.ui,advancedPresent:report.uiContract?report.uiContract.advancedPresent:false,advancedCollapsed:report.uiContract?report.uiContract.advancedCollapsed:false};
  row.noviceChecks={
   startsWithoutHelp:!!(report.uiContract&&report.uiContract.startsWithoutHelp),
   confirmsOnlyWhenNeeded:appliedWrites.length===0?true:(pendingObserved&&preApprovalUnchanged),
   findsResult:finalView.ui.resultCardVisible||finalView.ui.localCardVisible
  };
  row.metrics.novice=Object.values(row.noviceChecks).filter(Boolean).length;

  // Undo approved writes only after capturing artifacts/citations so the next sample starts from the
  // same synthetic library and source files stay byte-identical.
  for(const call of doneWrites){
   if(!call.undoChanges&&!call.undoCreated&&!call.undoMemory)continue;
   try{await page.evaluate(({taskId,callId})=>native('agentTaskUndo',{taskId,callId}),{taskId:snap.id,callId:call.id});row.writes.undone++;}catch{}
  }
  if(row.writes.undone){try{await page.evaluate(()=>refresh());}catch{}}
  if(appliedWrites.length){
   const afterMeta=await libraryFingerprint(page);
   row.restore.libraryRestored=afterMeta===beforeMeta;
   row.restore.appliedChanges=appliedWrites.length;
  }else row.restore.libraryRestored=true;

  // Acceptance checks shared by all categories; category-specific honesty rules are added below.
  const claimsWrite=/(已(?:保存|创建|写入|修改|添加|移入)|已生成并保存)/.test(answerText)&&appliedWrites.length===0;
  const needsInput=row.taskStatus==='needs_input'||row.taskStatus==='partial';
  const unknownMarker=/未提供|未知|未确定|未包含|未说明|未给出|未提及|没有提供|缺少|不包含|无法确认|无法确定/.test(answerText);
  row.checks={
   resultPresent,
   artifactOpenable:expectedExt?!!row.artifact.openable:true,
   citationsValid:!!sample.acceptance.citationsRequired?citationsValid:true,
   noUnsupportedClaims:!claimsWrite&&row.errors.length===0,
   noErrors:row.errors.length===0,
   fixtureRestored:row.restore.libraryRestored!==false,
   outcomeAcceptable:true,
   honestOutcome:true,
   localTruth:true
  };
  if(sample.category==='needs-input'){
   row.checks.honestOutcome=unknownMarker&&needsInput;
   row.checks.outcomeAcceptable=row.taskStatus==='needs_input';
  }else if(sample.category!=='local'){
   row.checks.outcomeAcceptable=['completed','partial'].includes(row.taskStatus)&&!timedOut;
  }else if(row.actualRoute==='local'){
   row.checks.localTruth=row.local&&row.local.truthMatched!==false;
   row.checks.outcomeAcceptable=true;
  }else{
   // Local sample answered by the model fallback: measure it, but the answer is not a local-routing pass.
   row.checks.outcomeAcceptable=['completed','partial'].includes(row.taskStatus)&&!timedOut;
  }
  row.checks.noUnauthorizedWrites=sample.category==='generation'||sample.category==='complex'?true:appliedWrites.length===0;
  row.passed=Object.values(row.checks).every(value=>value);
  if(row.errors.some(error=>/auth|permission|quota|model_not|network|budget_exceeded|rate_limit/i.test(error)))row.fatal=row.errors.join('; ');
  if(snap&&snap.errorKind&&/auth|permission|quota|model_not|network|budget_exceeded|rate_limit/.test(snap.errorKind))row.fatal='task errorKind: '+snap.errorKind;
 }catch(error){
  row.errors.push(safe(error&&error.message||error));
  row.fatal='harness: '+safe(error&&error.message||error);
  if(!row.metrics)row.metrics={inputTokens:0,outputTokens:0,cachedTokens:0,cost:0,durationMs:Date.now()-started,requests:0,retries:0,duplicateReads:0,opened:0,citationsValid:false,novice:0};
 }finally{
  try{await page.evaluate(async()=>{if($('#close-viewer'))$('#close-viewer').click();if(state.route!=='agent')navigate('agent');});}catch{}
 }
 return row;
}

async function readUiView(page){
 try{
  return await page.evaluate(()=>{
   const agent=AgentUi.state;
   const probe=window.__kokSampleProbe||{local:0,start:0};
   const view={
    job:!!agent.job,
    watch:String(agent.watch||''),
    taskStarts:Number(probe.start)||0,
    localCalls:Number(probe.local)||0,
    messages:agent.messages.map(message=>({id:message.id,role:message.role,text:typeof message.text==='string'?message.text:'',meta:!!message.meta,interrupted:!!message.interrupted,tool:message.tool||'',status:message.status||'',sourceIds:Array.isArray(message.sourceIds)?message.sourceIds:[]})).slice(-120),
    resultMessages:agent.messages.filter(message=>message.role==='result').map(message=>({kind:String(message.kind||''),local:!!message.local,title:String(message.title||''),status:String(message.status||''),summary:typeof message.summary==='string'?message.summary:'',sourceCount:Number(message.sourceCount)||0,citationCount:Number(message.citationCount)||0,artifactId:String(message.artifactId||''),goal:String(message.goal||'')})).slice(-4),
    localResult:(()=>{const message=[...agent.messages].reverse().find(row=>row.role==='result'&&(row.kind==='local'||row.local));if(!message)return null;return{kind:String(message.kind||'local'),title:String(message.title||''),summary:typeof message.summary==='string'?message.summary:'',total:Number(message.total)||0,stats:Array.isArray(message.stats)?message.stats.map(row=>({label:String(row&&row.label||''),value:Number(row&&row.value)||0})):[],items:Array.isArray(message.items)?message.items:[] ,followups:Array.isArray(message.followups)?message.followups:[]};})(),
    errors:agent.messages.filter(message=>message.role==='error').map(message=>String(message.text||'')).slice(-5)
   };
   view.ui={
    resultCardCount:document.querySelectorAll('[data-agent-result]').length,
    localCardCount:document.querySelectorAll('[data-agent-local]').length,
    resultCardVisible:[...document.querySelectorAll('[data-agent-result],[data-agent-local]')].some(element=>element.getClientRects().length>0),
    localCardVisible:[...document.querySelectorAll('[data-agent-local]')].some(element=>element.getClientRects().length>0)
   };
   return view;
  });
 }catch{return {job:false,watch:'',taskStarts:0,localCalls:0,messages:[],resultMessages:[],localResult:null,errors:[],ui:{resultCardCount:0,localCardCount:0,resultCardVisible:false,localCardVisible:false}};}
}

function requestRetries(snap){
 if(!snap||!Array.isArray(snap.metrics))return 0;
 return snap.metrics.filter(metric=>Number(metric&&metric.attempt)>1).length;
}

function duplicateReadStats(snap){
 const seen=new Map();let duplicates=0,reused=0;
 for(const message of (snap&&snap.messages)||[]){
  if(message.role!=='tool'||!message.tool||message.status!=='done'||MODIFY_TOOLS.includes(message.tool))continue;
  const key=message.tool+'\0'+stableStringify(message.args||{});
  if(seen.has(key)){duplicates++;const previous=seen.get(key);if(message.resultEventId&&previous.resultEventId&&message.resultEventId===previous.resultEventId)reused++;}
  else seen.set(key,{resultEventId:message.resultEventId||''});
 }
 return {duplicates,reused};
}

async function tryOpenItem(page,id){
 if(!id)return false;
 try{
  const opened=await page.evaluate(itemId=>{try{openItem(itemId,{play:false});return true;}catch{return false;}},id);
  if(!opened)return false;
  // A document/book/PDF/source opens the in-page viewer dialog; videos/media may use a native preview
  // window, which this CDP measurement cannot see. Count only a real, already-open viewer dialog.
  try{await page.waitForFunction(()=>{const dialog=document.querySelector('#viewer');return !!dialog&&dialog.open===true;},null,{timeout:8000});}catch{return false;}
  await page.evaluate(()=>{const dialog=document.querySelector('#viewer');if(dialog&&dialog.open)try{dialog.close();}catch{dialog.removeAttribute('open');}});
  return true;
 }catch{return false;}
}

function aggregate(rows,skippedCount){
 const sum=key=>rows.reduce((total,row)=>total+(Number(row.metrics[key])||0),0);
 const byCategory={};
 for(const row of rows){
  const bucket=byCategory[row.category]||(byCategory[row.category]={count:0,passed:0,requests:0,inputTokens:0,outputTokens:0,cachedTokens:0,cost:0,routeMatched:0,local:0,model:0,fallback:0});
  bucket.count++;if(row.passed)bucket.passed++;
  bucket.requests+=Number(row.metrics.requests)||0;
  bucket.inputTokens+=Number(row.metrics.inputTokens)||0;
  bucket.outputTokens+=Number(row.metrics.outputTokens)||0;
  bucket.cachedTokens+=Number(row.metrics.cachedTokens)||0;
  bucket.cost+=Number(row.metrics.cost)||0;
  if(row.actualRoute===row.expectedRoute)bucket.routeMatched++;
  if(row.actualRoute==='local')bucket.local++;
  if(row.actualRoute==='model')bucket.model++;
  if(row.fallback)bucket.fallback++;
 }
 return {
  samples:rows.length,skipped:skippedCount,passed:rows.filter(row=>row.passed).length,
  passRate:rows.length?Math.round(rows.filter(row=>row.passed).length/rows.length*1000)/10:0,
  local:rows.filter(row=>row.actualRoute==='local').length,
  model:rows.filter(row=>row.actualRoute==='model').length,
  none:rows.filter(row=>row.actualRoute==='none').length,
  routeMatched:rows.filter(row=>row.actualRoute===row.expectedRoute).length,
  fallback:rows.filter(row=>row.fallback).length,
  misrouted:rows.filter(row=>row.misrouted).length,
  failed:rows.filter(row=>!row.passed).length,
  artifactsOpenable:rows.filter(row=>row.artifact&&row.artifact.openable===true).length,
  citationsValid:rows.filter(row=>row.checks&&row.checks.citationsValid).length,
  novicePassed:rows.filter(row=>row.noviceChecks&&Object.values(row.noviceChecks).every(Boolean)).length,
  requests:sum('requests'),inputTokens:sum('inputTokens'),outputTokens:sum('outputTokens'),cachedTokens:sum('cachedTokens'),
  cost:Math.round(sum('cost')*1e6)/1e6,retries:sum('retries'),duplicateReads:sum('duplicateReads'),opened:sum('opened'),durationMs:sum('durationMs'),
  byCategory
 };
}

function sampleLine(row){
 const metrics=row.metrics||{};
 const flags=row.passed?'PASS':'FAIL';
 const checks=Object.entries(row.checks||{}).filter(([,value])=>!value).map(([name])=>name);
 return 'LIVE '+pad(row.id,34)+flags+' '+pad('route='+row.actualRoute+(row.fallback?'/fallback':''),20)+
  'req='+pad(metrics.requests,3)+' in='+pad(metrics.inputTokens,6)+' out='+pad(metrics.outputTokens,5)+
  ' dup='+pad(metrics.duplicateReads,2)+' open='+(metrics.opened?'yes':'no')+' '+
  Math.round((metrics.durationMs||0)/1000)+'s'+(checks.length?' failed: '+checks.join(','):'');
}

function printSummary(report){
 const rows=report.samples.map(row=>({
  id:row.id,route:row.actualRoute+(row.fallback?'(fallback)':''),req:row.metrics.requests,
  inp:row.metrics.inputTokens,out:row.metrics.outputTokens,dup:row.metrics.duplicateReads,
  secs:Math.round(row.metrics.durationMs/1000),result:row.passed?'PASS':'FAIL'
 }));
 console.log('');
 console.log(pad('id',34)+pad('route',18)+pad('req',5)+pad('input',9)+pad('output',8)+pad('dup',5)+pad('secs',6)+'result');
 for(const row of rows)console.log(pad(row.id,34)+pad(row.route,18)+pad(row.req,5)+pad(row.inp,9)+pad(row.out,8)+pad(row.dup,5)+pad(row.secs,6)+row.result);
 const aggregate=report.aggregate;
 if(aggregate){
  console.log('');
  console.log('Aggregate: '+aggregate.passed+'/'+aggregate.samples+' passed ('+aggregate.passRate+'%) · route matched '+aggregate.routeMatched+'/'+aggregate.samples+
   ' · local '+aggregate.local+' / model '+aggregate.model+' / fallback '+aggregate.fallback+' / misrouted '+aggregate.misrouted);
  console.log('Usage: requests '+aggregate.requests+' · input '+aggregate.inputTokens+' · cached '+aggregate.cachedTokens+' · output '+aggregate.outputTokens+
   ' · cost '+aggregate.cost+' · retries '+aggregate.retries+' · duplicate reads '+aggregate.duplicateReads+' · opened '+aggregate.opened);
 }
}

function writeReport(report){
 assert(report.format===REPORT_FORMAT&&report.version===REPORT_VERSION,'Report format mismatch');
 const directory=path.join(ROOT,'test-output','agent-samples',timestamp());
 fs.mkdirSync(directory,{recursive:true});
 const file=path.join(directory,'report.json');
 report.reportPath=path.relative(ROOT,file).split(path.sep).join('/');
 fs.writeFileSync(file,safe(JSON.stringify(report,null,2)),'utf8');
 return file;
}

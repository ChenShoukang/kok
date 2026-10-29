'use strict';
// Explicit opt-in only. The key is read in memory, passed to an isolated DPAPI profile, then cleared.
// Never print request headers, raw credential files, or provider error bodies.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict'),{chromium}=require('./browser.cjs');
(async()=>{
 if(process.env.KOK_LIVE_AI!=='1'||!process.env.KOK_LIVE_KEY_FILE)throw Error('Live testing requires explicit opt-in and a credential-file path.');
 const root=process.env.KOK_TEST_PROFILE;assert(root);let key='',browser,page,configured=false;const calls=[],errors=[],remote=[],report={model:'',cases:[],maximumModelRequests:4};
 const safe=text=>String(text).replace(/sk-[A-Za-z0-9_-]{16,}/g,'[redacted]');
 try{
  const credentialText=fs.readFileSync(process.env.KOK_LIVE_KEY_FILE,'utf8');key=credentialText.match(/sk-[A-Za-z0-9_-]{16,}/)?.[0]||'';if(!key)throw Error('No valid DeepSeek key was found in the supplied file.');
  const response=await fetch('https://api.deepseek.com/models',{headers:{Authorization:'Bearer '+key},redirect:'error',signal:AbortSignal.timeout(20000)});
  if(!response.ok)throw Error('DeepSeek model check returned HTTP '+response.status);
  const list=(await response.json()).data?.map(m=>m.id)||[];const model=['deepseek-flash','deepseek-v4-flash','deepseek-chat'].find(m=>list.includes(m));if(!model)throw Error('No expected economical DeepSeek model is available.');report.model=model;console.log('Live model check passed. Using '+model+'; at most 4 completion requests.');
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');const context=browser.contexts()[0];page=context.pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(90000);page.on('pageerror',e=>errors.push(safe(e.message)));
  await context.route(/^https?:\/\//,route=>{if(['app.kok.local','media.kok.local'].includes(new URL(route.request().url()).hostname))return route.continue();remote.push(new URL(route.request().url()).hostname);return route.abort();});await page.waitForFunction(()=>!$('#startup'));await page.setViewportSize({width:1280,height:860});
  await page.evaluate(async({model,key})=>native('saveAiConfig',{endpoint:'https://api.deepseek.com',model,apiKey:key,automatic:false,language:'简体中文'}),{model,key});configured=true;
  const fixtures=await page.evaluate(async()=>{const a=await native('createDocument',{name:'海边旅行计划',format:'md',collection:'旅行',text:'# 海边旅行计划\n出发日期：2026年11月5日。目的地：厦门。交通：火车往返。总预算：1200元。住宿费尚未确定。'}),b=await native('createDocument',{name:'摄影准备',format:'md',collection:'旅行',text:'# 摄影准备\n为厦门旅行准备相机、两块电池、充电器和一张备用存储卡。出发前给电池充满电。资料没有提供任何住宿价格。'});await native('createDocument',{name:'不在范围内的财务测试',format:'md',collection:'其他',text:'OUTSIDE_SCOPE_CANARY 请勿读取或引用。'});await refresh();navigate('agent');return{ids:[a.id,b.id],paths:[a.path,b.path]};});
  const hashes=fixtures.paths.map(p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'));
  // The native engine bounds each task by requestLimit (2 here); record what each case actually sent.
  await page.evaluate(()=>{const previous=native;window.liveAgentCalls=[];native=(action,args,...rest)=>{if(action==='agentTaskStart')window.liveAgentCalls.push({budget:args.budget,requestLimit:args.requestLimit,protocol:args.protocol});return previous(action,args,...rest);};});
  const recordRequests=()=>page.evaluate(()=>{const last=window.liveAgentCalls.at(-1);if(last)last.requests=AgentUi.state.usage?.requests||0;});
  const reset=async intent=>page.evaluate(async({ids,intent})=>{await AgentUi.newSession();AgentUi.state.context=ids;AgentUi.state.scope='selected';AgentUi.state.skill='';AgentUi.state.intent=intent;AgentUi.state.budget=6000;AgentUi.state.requestLimit=2;AgentUi.state.costLimit=0;AgentUi.render();},{ids:fixtures.ids,intent});
  const submit=async goal=>{await page.locator('#agent-input').fill(goal);await page.locator('#agent-form [type=submit]').click();};
  await reset('discuss');let started=Date.now();console.log('Case 1: real two-document summary in discussion-only mode.');
  await submit('请先用一次 read_items 读取所选两份资料，再用三条要点总结厦门旅行的日期、交通、总预算和摄影准备。没有确定的费用请明确写未知，不要自行补充。仅讨论，不修改或创建文件。');await page.waitForFunction(()=>!AgentUi.state.job,null,{timeout:120000});
  const summary=await page.evaluate(()=>({answers:AgentUi.state.messages.filter(m=>m.role==='agent'&&!m.meta).map(m=>({text:m.text,sources:m.sourceIds||[]})),errors:AgentUi.state.messages.filter(m=>m.role==='error').map(m=>m.text),reads:AgentUi.state.messages.filter(m=>m.tool==='read_items'&&m.status==='done').length,usage:AgentUi.state.usage,pending:AgentUi.state.messages.filter(m=>m.status==='pending').length}));
  const answer=summary.answers.at(-1)?.text||'';report.cases.push({name:'summary',elapsedMs:Date.now()-started,...summary,checks:{mentionsBudget:/1200|1,200/.test(answer),mentionsDestination:answer.includes('厦门'),mentionsUnknown:/未确定|未知|未定|不明|未明确|尚未/.test(answer),hasReadSources:(summary.answers.at(-1)?.sources.length||0)===2,noWrites:summary.pending===0}});
  await recordRequests();assert.deepEqual(summary.errors,[]);assert(summary.reads>0,'The real model must read sources before summarizing.');assert(Object.values(report.cases[0].checks).every(Boolean),'The summary should retain facts, cite read sources and acknowledge unknown costs.');console.log('Case 1 passed in '+Math.round((Date.now()-started)/1000)+' s.');await page.screenshot({path:path.join(root,'live-summary.png')});
  await reset('assist');await page.evaluate(id=>{AgentUi.state.context=[id];AgentUi.render();},fixtures.ids[0]);started=Date.now();console.log('Case 2: real tag proposal, reviewed application and undo.');
  await submit('仅根据文件名“海边旅行计划”，建议为这一个文件添加“旅行”和“待办”标签。仅提出一个 add_tags 动作，不修改分类、笔记，不创建文档，不必读取正文，等待我确认。');
  await page.waitForFunction(()=>AgentUi.state.messages.some(m=>m.status==='pending')||!AgentUi.state.job,null,{timeout:120000});const proposal=await page.evaluate(()=>({errors:AgentUi.state.messages.filter(m=>m.role==='error').map(m=>m.text),pending:AgentUi.state.messages.filter(m=>m.status==='pending').map(m=>({id:m.id,tool:m.tool,args:m.args})),usage:AgentUi.state.usage}));
  assert.deepEqual(proposal.errors,[]);assert(proposal.pending.length===1&&proposal.pending[0].tool==='add_tags','The model should propose exactly the requested tag operation.');assert.deepEqual(await page.evaluate(id=>byId(id).tags||[],fixtures.ids[0]),[],'No write is allowed before confirmation.');
  await page.locator(`[data-agent-run="${proposal.pending[0].id}"]`).click();await page.waitForFunction(()=>!AgentUi.state.job);const after=await page.evaluate(id=>byId(id).tags||[],fixtures.ids[0]);assert(after.includes('旅行')&&after.includes('待办'));await page.locator(`[data-agent-undo="${proposal.pending[0].id}"]`).click();await page.waitForFunction(id=>(byId(id).tags||[]).length===0,fixtures.ids[0]);
  await recordRequests();report.cases.push({name:'tag-proposal',elapsedMs:Date.now()-started,...proposal,confirmedTags:after,undoPreservedOriginal:true});console.log('Case 2 passed in '+Math.round((Date.now()-started)/1000)+' s.');
  fixtures.paths.forEach((p,n)=>assert.equal(crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'),hashes[n]));report.requests=await page.evaluate(()=>window.liveAgentCalls);assert(report.requests.reduce((n,r)=>n+(r.requests||0),0)<=4,'live cases stay within four model requests');assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);report.passed=true;
 }catch(error){report.passed=false;report.error=safe(error.message);console.error('Live check: '+report.error);process.exitCode=1;if(page)await page.screenshot({path:path.join(root,'live-failure.png')}).catch(()=>{});}
 finally{
  if(configured&&page){try{await page.evaluate(async()=>{if(AgentUi.state.job)await AgentUi.stop();await native('saveAiConfig',{apiKey:'',clearKey:true,automatic:false});});report.keyCleared=true;}catch{report.keyCleared=false;}}
  // This removes only the known test profile's AI config; the user's supplied file is untouched.
  const config=path.join(root,'ai-settings.json');if(configured&&fs.existsSync(config)){try{fs.unlinkSync(config);report.keyCleared=true;}catch{}}
  if(page)report.requests=await page.evaluate(()=>window.liveAgentCalls||[]).catch(()=>report.requests||[]);
  key='';fs.writeFileSync(path.join(root,'live-report.json'),safe(JSON.stringify(report,null,2)));if(browser)await browser.close();console.log('Live report: '+path.join(root,'live-report.json'));console.log('Credential cleared from the isolated profile: '+!!report.keyCleared);
 }
})().catch(()=>{console.error('Live test could not initialize. No credential values were logged.');process.exitCode=1;});

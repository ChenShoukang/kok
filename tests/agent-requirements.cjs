'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http'),{randomUUID}=require('node:crypto');
const {chromium}=require('./browser.cjs');
(async()=>{
 let browser,page,sourceId,reportId;const bodies=[],errors=[],output=path.resolve('test-output/agent-requirements');fs.mkdirSync(output,{recursive:true});
 const goal='根据资料创建DOCX报告，按研究范围和证据质量组织。',basis='按研究范围和证据质量组织';
 const draft='# 活动报告\n## 研究范围\n最终预算8万元，60人，不收集手机号。\n## 证据质量\n';
 const model=http.createServer((req,res)=>{let raw='';req.on('data',chunk=>raw+=chunk);req.on('end',()=>{
  const body=JSON.parse(raw);bodies.push(body);const receipts=body.messages.filter(m=>m.role==='tool').map(m=>{try{return JSON.parse(m.content).result;}catch{return null;}}).filter(Boolean);reportId=receipts.findLast(r=>r.id&&r.artifactReview?.kind==='document')?.id||reportId;
  let calls=[];const n=bodies.length;
  if(n===1)calls=[{name:'update_task_spec',args:{baseRevision:0,userRevision:0,basis,changes:{document:{sections:['研究范围','证据质量']}}}},{name:'read_items',args:{ids:[sourceId],max_chars:12000}}];
  if(n===2)calls=[{name:'create_document',args:{name:'结构化要求报告',format:'docx',text:draft}}];
  if(n===4)calls=[{name:'replace_document_text',args:{id:reportId,old_text:'## 证据质量\n',new_text:'## 证据质量\n采用最终修订；日期和地点待确认。\n'}}];
  const delta=calls.length?{role:'assistant',tool_calls:calls.map((call,index)=>({index,id:'requirements-'+randomUUID(),type:'function',function:{name:call.name,arguments:JSON.stringify(call.args)}}))}:{role:'assistant',content:'报告已保存，已完成所需章节并核对当前文件。'};
  res.writeHead(200,{'content-type':'text/event-stream'});res.end('data: '+JSON.stringify({id:randomUUID(),object:'chat.completion.chunk',model:'requirements-fixture',choices:[{index:0,delta,finish_reason:calls.length?'tool_calls':'stop'}],usage:{prompt_tokens:100,completion_tokens:60}})+'\n\ndata: [DONE]\n\n');
 });});await new Promise(resolve=>model.listen(0,'127.0.0.1',resolve));
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(30000);page.on('pageerror',error=>errors.push(error.message));await page.waitForFunction(()=>!$('#startup'));
  sourceId=await page.evaluate(async endpoint=>{await native('saveAiConfig',{endpoint,model:'requirements-fixture',clearKey:true,automatic:false});const source=await native('createDocument',{name:'输入资料',format:'md',text:'最终预算8万元，60人，不收集手机号。日期和地点待确认。'});await refresh();navigate('agent');await AgentUi.newSession();Object.assign(AgentUi.state,{skill:'',intent:'assist',protocol:'auto',scope:'selected',context:[source.id],requestLimit:8});AgentUi.render();return source.id;},'http://127.0.0.1:'+model.address().port+'/v1');
  await page.locator('#agent-input').fill(goal);await page.locator('.agent-send').click();await page.waitForSelector('[data-agent-run]');
  const original=await page.evaluate(()=>native('agentTaskRead',{taskId:AgentUi.state.watch}));assert.equal(original.taskSpec.revision,1);assert.deepEqual(original.taskSpec.deliverables[0].sections,['研究范围','证据质量']);
  const sdkSchema=bodies[0].tools.find(t=>t.function.name==='update_task_spec').function.parameters;assert(sdkSchema.properties.changes.properties.document.properties.sections);assert.equal(sdkSchema.additionalProperties,false);
  const callId=original.messages.find(m=>m.tool==='create_document'&&m.status==='pending').id,taskId=original.id;
  await page.evaluate(()=>AgentUi.stop());await page.waitForFunction(()=>!AgentUi.state.job);
  await page.locator('[data-agent-continue-task="'+taskId+'"]').click();await page.waitForSelector('[data-agent-run]');
  const restored=await page.evaluate(id=>native('agentTaskRead',{taskId:id}),taskId);assert.equal(restored.taskSpec.revision,1);assert.equal(restored.messages.filter(m=>m.tool==='update_task_spec'&&m.status==='done').length,1);assert.equal(restored.messages.find(m=>m.tool==='create_document'&&m.status==='pending').id,callId);assert.equal(bodies.length,2);
  await page.locator('[data-agent-run]').click();await page.waitForSelector('[data-agent-run]');await page.locator('[data-agent-run]').click();await page.waitForFunction(()=>!AgentUi.state.job);
  const task=await page.evaluate(id=>native('agentTaskRead',{taskId:id}),taskId);assert.equal(task.status,'completed',task.error);assert.equal(bodies.length,5);assert.equal(task.options.requirementUpdates.length,1);assert.equal(task.messages.filter(m=>m.tool==='create_document').length,1);
  assert(bodies[3].messages.some(m=>m.content?.includes?.('证据质量')));const current=await page.evaluate(id=>native('documentSource',{id}),reportId);assert.equal(current.text,draft.replace('## 证据质量\n','## 证据质量\n采用最终修订；日期和地点待确认。\n'));
  const card=await page.locator('[data-requirement-update]').textContent();assert.match(card,/交付要求已更新/);assert.match(card,/模型按本轮用户原话整理/);assert.match(card,/研究范围、证据质量/);assert.deepEqual(errors,[]);
  await page.screenshot({path:path.join(output,'requirements-result.png'),fullPage:true});fs.writeFileSync(path.join(output,'result.json'),JSON.stringify({passed:true,requests:bodies.length,sdkTypedSchema:true,revision:task.taskSpec.revision,updates:task.options.requirementUpdates.length,pendingCallPreserved:true,reportRepaired:true},null,2));
  console.log('Requirements desktop: SDK typed delta → durable native TaskSpec → paused/restarted approval → actual DOCX → missing-section repair, with original pending call and one creation preserved.');
 }catch(error){if(page){await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});const task=await page.evaluate(()=>AgentUi.state.watch?native('agentTaskRead',{taskId:AgentUi.state.watch}):null).catch(()=>null);fs.writeFileSync(path.join(output,'failure.json'),JSON.stringify({bodies,task},null,2));}throw error;}finally{if(browser)await browser.close();model.closeAllConnections();await new Promise(resolve=>model.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});

'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http'),{randomUUID}=require('node:crypto');
const {chromium}=require('./browser.cjs');
(async()=>{
 let browser,page,sourceId;const requests=[],errors=[],output=path.resolve('test-output/agent-requirement-change');fs.mkdirSync(output,{recursive:true});
 const basis='文字材料这回先缓一缓，只留下用于展示的那份',goal='根据资料创建400-600汉字DOCX报告，并制作8页PPT；'+basis+'。';
 const model=http.createServer((req,res)=>{let raw='';req.on('data',chunk=>raw+=chunk);req.on('end',()=>{
  const body=JSON.parse(raw);requests.push(body);let calls=[];
  if(requests.length===1)calls=[
    {name:'read_items',args:{ids:[sourceId],max_chars:12000}},
    {name:'propose_task_spec',args:{baseRevision:0,userRevision:0,basis,changes:{audience:'未提及的工作人员'}}}
  ];
  if(requests.length===2)calls=[{name:'propose_task_spec',args:{baseRevision:0,userRevision:0,basis,changes:{document:{required:false,sections:['模型自拟栏目']},audience:'未提及的工作人员'}}}];
  if(requests.length===3)calls=[{name:'create_presentation',args:{title:'保留的活动展示方案',slides:Array.from({length:8},(_,i)=>({title:'活动方案 '+(i+1),bullets:['最终预算8万元，参与人数60人。','不收集手机号；日期和地点待确认。']}))}}];
  const delta=calls.length?{role:'assistant',tool_calls:calls.map((call,index)=>({index,id:randomUUID(),type:'function',function:{name:call.name,arguments:JSON.stringify(call.args)}}))}:{role:'assistant',content:'已按确认后的要求保存8页PPT，文字材料本轮暂缓；采用8万元与60人，不收集手机号，未知日期地点待确认。'};
  res.writeHead(200,{'content-type':'text/event-stream'});res.end('data: '+JSON.stringify({id:randomUUID(),object:'chat.completion.chunk',model:'requirements-change-fixture',choices:[{index:0,delta,finish_reason:calls.length?'tool_calls':'stop'}],usage:{prompt_tokens:100,completion_tokens:60}})+'\n\ndata: [DONE]\n\n');
 });});await new Promise(resolve=>model.listen(0,'127.0.0.1',resolve));
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(30000);page.on('pageerror',error=>errors.push(error.message));await page.waitForFunction(()=>!$('#startup'));
  sourceId=await page.evaluate(async endpoint=>{await native('saveAiConfig',{endpoint,model:'requirements-change-fixture',clearKey:true,automatic:false});const source=await native('createDocument',{name:'活动资料',format:'md',text:'最终预算8万元，60人，不收集手机号。日期和地点待确认。'});await refresh();navigate('agent');await AgentUi.newSession();Object.assign(AgentUi.state,{skill:'',intent:'assist',protocol:'auto',scope:'selected',context:[source.id],requestLimit:8});AgentUi.render();return source.id;},'http://127.0.0.1:'+model.address().port+'/v1');
  await page.locator('#agent-input').fill(goal);await page.locator('.agent-send').click();await page.waitForSelector('[data-task-requirement-proposal]');
  let task=await page.evaluate(()=>native('agentTaskRead',{taskId:AgentUi.state.watch}));const taskId=task.id,pending=task.messages.find(m=>m.tool==='propose_task_spec'&&m.status==='pending');
  assert(pending);assert.equal(task.taskSpec.revision,0);assert.equal(task.taskSpec.deliverables.length,2);assert.equal(task.options.requirementUpdates.length,0);assert.equal(requests.length,2);
  assert(task.messages.some(m=>m.tool==='propose_task_spec'&&m.status==='failed'));
  assert(requests[1].messages.some(m=>m.role==='tool'&&m.content.includes('No user-stated change remains')));
  const preview=await page.locator('[data-task-requirement-proposal]').textContent();assert.match(preview,/当前要求/);assert.match(preview,/建议改为/);assert.match(preview,/本轮不新建文档/);assert.match(preview,/400–600/);assert.match(preview,/PPTX.*8/);
  assert.doesNotMatch(preview,/模型自拟栏目|未提及的工作人员/);
  const schema=requests[0].tools.find(t=>t.function.name==='propose_task_spec').function.parameters;assert(schema.properties.baseRevision);assert.equal(schema.additionalProperties,false);
  await page.evaluate(()=>AgentUi.stop());await page.waitForFunction(()=>!AgentUi.state.job);await page.locator('[data-agent-continue-task="'+taskId+'"]').click();await page.waitForSelector('[data-agent-run]');
  task=await page.evaluate(id=>native('agentTaskRead',{taskId:id}),taskId);assert.equal(task.taskSpec.revision,0);assert.equal(task.messages.find(m=>m.tool==='propose_task_spec'&&m.status==='pending').id,pending.id);assert.equal(requests.length,2);
  await page.locator('[data-agent-run]').click();await page.waitForFunction(()=>AgentUi.state.messages.some(m=>m.tool==='create_presentation'&&m.status==='pending'));
  task=await page.evaluate(id=>native('agentTaskRead',{taskId:id}),taskId);assert.equal(task.taskSpec.revision,1);assert.equal(task.taskSpec.deliverables.length,1);assert.equal(task.taskSpec.deliverables[0].kind,'presentation');assert.equal(task.taskSpec.deliverables[0].pages,8);assert(task.taskSpec.excludedDeliverables.includes('document'));assert.equal(task.messages.find(m=>m.tool==='propose_task_spec'&&m.status==='done').result.interpretation,'user_confirmed');
  await page.locator('[data-agent-run]').click();await page.waitForFunction(()=>!AgentUi.state.job);task=await page.evaluate(id=>native('agentTaskRead',{taskId:id}),taskId);
  assert.equal(task.status,'completed',task.error);assert.equal(requests.length,4);assert.equal(task.options.requirementUpdates.length,1);assert.equal(task.messages.filter(m=>m.tool==='create_document').length,0);const created=task.messages.filter(m=>m.tool==='create_presentation'&&m.status==='done');assert.equal(created.length,1);assert.equal(created[0].result.artifactReview.pages,8);
  assert.match(await page.locator('[data-requirement-update]').textContent(),/已按本次确认更新交付要求/);assert.deepEqual(errors,[]);await page.screenshot({path:path.join(output,'confirmed-requirements.png'),fullPage:true});
  fs.writeFileSync(path.join(output,'result.json'),JSON.stringify({passed:true,requests:requests.length,originalPendingCallPreserved:true,unchangedUntilConfirmed:true,updates:1,documentCreated:false,actualPresentationPages:8},null,2));
  console.log('Requirement change desktop: actual before/after preview → paused approval restored with same call → individually confirmed cancellation → one real eight-page PPTX and no unwanted document.');
 }catch(error){if(page){await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});const task=await page.evaluate(()=>AgentUi.state.watch?native('agentTaskRead',{taskId:AgentUi.state.watch}):null).catch(()=>null);fs.writeFileSync(path.join(output,'failure.json'),JSON.stringify({requests,task},null,2));}throw error;}finally{if(browser)await browser.close();model.closeAllConnections();await new Promise(resolve=>model.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});

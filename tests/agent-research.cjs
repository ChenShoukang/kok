'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http'),{randomUUID}=require('node:crypto');
const {chromium}=require('./browser.cjs');
(async()=>{
 const requests=[],errors=[];let browser,page,source;
 const report='研究报告\n\n本次活动应以最终修订为准，将总预算控制在8万元以内，按准备、实施与复盘三个阶段推进。准备阶段确认场地、人员分工和材料清单；实施阶段记录到场人数和活动反馈；复盘阶段汇总实际支出、完成情况与改进建议。\n\n资料明确禁止收集手机号，报名和反馈环节均需遵守这一限制。初稿中的旧预算不再适用。正式执行前仍需确认各阶段日期和预算分配，资料未给出这些细节，不能自行补造。\n\n来源：活动方案最终修订。';
 const model=http.createServer((req,res)=>{let raw='';req.on('data',chunk=>raw+=chunk);req.on('end',()=>{
  const body=JSON.parse(raw);requests.push(body);const n=requests.length;
  const call=n===1?{name:'read_items',args:{ids:[source.id]}}:n===2?{name:'create_document',args:{name:'活动研究报告',format:'docx',text:report}}:null;
  const delta=call?{role:'assistant',tool_calls:[{index:0,id:'call-'+randomUUID(),type:'function',function:{name:call.name,arguments:JSON.stringify(call.args)}}]}:{role:'assistant',content:'已保存DOCX研究报告，保留8万元最终预算和禁止收集手机号的限制，并列明待确认事项。'};
  res.writeHead(200,{'content-type':'text/event-stream'});const id='research-'+randomUUID();
  res.write('data: '+JSON.stringify({id,object:'chat.completion.chunk',model:'research-desktop-fixture',choices:[{index:0,delta,finish_reason:call?'tool_calls':'stop'}],usage:{prompt_tokens:100,completion_tokens:50}})+'\n\n');res.end('data: [DONE]\n\n');
 });});await new Promise(resolve=>model.listen(0,'127.0.0.1',resolve));
 const output=path.resolve('test-output/agent-research');fs.mkdirSync(output,{recursive:true});
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(25000);page.on('pageerror',e=>errors.push(e.message));
  await page.waitForFunction(()=>!$('#startup'));await page.setViewportSize({width:1440,height:960});
  source=await page.evaluate(async endpoint=>{
   await native('saveAiConfig',{endpoint,model:'research-desktop-fixture',clearKey:true,automatic:false});
   const item=await native('createDocument',{name:'活动方案最终修订',format:'md',text:'初稿预算12万元。最终修订：预算8万元，禁止收集手机号。活动按准备、实施、复盘三个阶段推进。'});
   await refresh();navigate('agent');await AgentUi.newSession();Object.assign(AgentUi.state,{skill:'',intent:'assist',protocol:'auto',scope:'selected',context:[item.id],requestLimit:6});AgentUi.render();return item;
  },`http://127.0.0.1:${model.address().port}/v1`);
  await page.locator('[data-agent-platform]').click();await page.waitForSelector('#agent-platform-form');
  assert.equal(await page.locator('#agent-platform-form [name=allowDelegation]').isVisible(),false,'research settings omit unsupported legacy delegation');
  assert.equal(await page.locator('#agent-platform-form [name=keepRecentTokens]').isVisible(),false,'research settings omit legacy compaction tuning');
  await page.evaluate(()=>closeSmall());
  await page.locator('#agent-input').fill('根据所选资料创建100-300字的DOCX研究报告，采用最终修订预算，保留禁止事项和来源。');await page.locator('.agent-send').click();
  await page.waitForSelector('[data-agent-run]');assert.equal(requests.length,2);
  assert.equal(await page.locator('[data-agent-compact]').count(),0,'research tasks use SDK compaction instead of an ineffective legacy button');
  const before=await page.evaluate(async()=>{const task=await native('agentTaskRead',{taskId:AgentUi.state.watch});return {runtime:task.options.runtime,pending:task.calls.find(c=>c.tool==='create_document'&&c.status==='pending')};});
  assert.equal(before.runtime,'research');assert(before.pending);assert(!before.pending.result,'approval proposal has no saved file receipt');
  assert.match(JSON.stringify(requests[0].messages),/根据所选资料/);assert.match(JSON.stringify(requests[1].messages),/预算8万元/);
  assert(!requests[0].tools.some(t=>['task','execute','delegate_parallel','finish_task'].includes(t.function.name)),'default tools omit delegation, shell and formal finish calls');
  await page.locator('[data-agent-run]').click();await page.waitForFunction(()=>!AgentUi.state.job);
  const done=await page.evaluate(async()=>{const task=await native('agentTaskRead',{taskId:AgentUi.state.watch});const call=AgentUi.state.messages.find(c=>c.tool==='create_document'&&c.status==='done');return {status:task.status,requests:task.usage.requests,item:byId(call.result.id),text:(await native('documentSource',{id:call.result.id})).text};});
  assert.equal(done.status,'completed');assert.equal(requests.length,3);assert.equal(done.text,report);assert.equal(done.item.ext,'DOCX');assert(fs.existsSync(done.item.path));assert.match(JSON.stringify(requests[2].messages),/hostReadBacks/);assert.deepEqual(errors,[]);
  await page.screenshot({path:path.join(output,'research-delivery.png'),fullPage:true});fs.writeFileSync(path.join(output,'result.json'),JSON.stringify({passed:true,requests:requests.length,runtime:before.runtime,status:done.status,artifact:{id:done.item.id,format:done.item.ext},checks:['Chinese source transfer','native approval','saved DOCX readback','request accounting','trimmed tool set']},null,2));
  console.log('Deep Agents desktop research: source → approval → saved DOCX → verified completion passed (3 loopback model requests).');
 }finally{if(browser)await browser.close();model.closeAllConnections();await new Promise(resolve=>model.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});

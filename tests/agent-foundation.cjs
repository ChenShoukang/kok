'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http'),{randomUUID}=require('node:crypto');
const {chromium}=require('./browser.cjs');
(async()=>{
 const profile=process.env.KOK_TEST_PROFILE;assert(profile);const workspace=path.join(profile,'workspace');fs.mkdirSync(workspace,{recursive:true});
 fs.writeFileSync(path.join(workspace,'fixture.mjs'),"import fs from 'node:fs';setTimeout(()=>{fs.writeFileSync('job-output.txt','foundation-job-ok');console.log('foundation-job-ok');},350);\n");
 const executable=path.join(path.dirname(process.env.KOK_TEST_EXECUTABLE),'runtime','research','node.exe');
 let browser,page;const bodies=[],errors=[];
 const model=http.createServer((req,res)=>{let raw='';req.on('data',d=>raw+=d);req.on('end',()=>{
  const body=JSON.parse(raw);bodies.push(body);let call=null;
  if(bodies.length===1)call={name:'start_command_job',args:{command:executable,args:['fixture.mjs'],timeoutSeconds:10,outputs:['job-output.txt']}};
  if(bodies.length===2){const message=body.messages.findLast(m=>m.role==='tool');const receipt=JSON.parse(message.content);call={name:'wait_command_job',args:{jobId:receipt.result.jobId,waitSeconds:5}};}
  const delta=call?{role:'assistant',tool_calls:[{index:0,id:'call-'+randomUUID(),type:'function',function:{name:call.name,arguments:JSON.stringify(call.args)}}]}:{role:'assistant',content:'后台命令已完成，实际退出码为0，输出文件已保存。'};
  res.writeHead(200,{'content-type':'text/event-stream'});res.end('data: '+JSON.stringify({id:'foundation-'+randomUUID(),object:'chat.completion.chunk',model:'foundation-fixture',choices:[{index:0,delta,finish_reason:call?'tool_calls':'stop'}],usage:{prompt_tokens:100,completion_tokens:30}})+'\n\ndata: [DONE]\n\n');
 });});await new Promise(resolve=>model.listen(0,'127.0.0.1',resolve));
 const output=path.resolve('test-output/agent-foundation');fs.mkdirSync(output,{recursive:true});
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(25000);page.on('pageerror',e=>errors.push(e.message));
  await page.waitForFunction(()=>!$('#startup'));await page.setViewportSize({width:1440,height:960});
  const info=await page.evaluate(async({endpoint,workspace})=>{await native('saveAiConfig',{endpoint,model:'foundation-fixture',clearKey:true,automatic:false});await native('agentWorkspaceSave',{enabled:true,root:workspace,sandbox:false});await refresh();navigate('agent');await AgentUi.newSession();Object.assign(AgentUi.state,{skill:'',intent:'assist',protocol:'auto',scope:'all',requestLimit:6});AgentUi.render();return native('agentRuntimeInfo');},{endpoint:`http://127.0.0.1:${model.address().port}/v1`,workspace});
  assert.equal(info.protocolVersion,1);assert.equal(info.runtimeLanguage,'TypeScript');assert(info.runtimeReady);assert.equal(info.capabilities.length,9);assert.equal(info.capabilities.find(c=>c.id==='music').status,'partial');assert.equal(info.capabilities.find(c=>c.id==='computer').status,'planned');
  await page.locator('[data-agent-platform]').click();await page.waitForSelector('[data-agent-capabilities]');await page.locator('[data-agent-capabilities]>summary').click();assert.equal(await page.locator('[data-capability=music]').getAttribute('data-state'),'partial');assert(await page.locator('[data-capability=music]').isVisible());await page.evaluate(()=>closeSmall());
  await page.locator('#agent-input').fill('在工作区运行给定脚本作为后台命令，等待实际完成并报告退出码。');await page.locator('.agent-send').click();await page.waitForSelector('[data-agent-run]');
  assert(!fs.existsSync(path.join(workspace,'job-output.txt')),'pending approval has not run the command');
  assert(bodies[0].tools.some(t=>t.function.name==='start_command_job'));assert(bodies[0].tools.some(t=>t.function.name==='wait_command_job'));
  await page.locator('[data-agent-run]').click();await page.waitForFunction(()=>!AgentUi.state.job);
  const done=await page.evaluate(async()=>{const task=await native('agentTaskRead',{taskId:AgentUi.state.watch});return {task,jobs:await native('agentCommandJobList')};});
  assert.equal(done.task.status,'completed');assert.equal(done.task.taskSpec.version,1);assert(done.task.taskSpec.capabilities.includes('coding'));assert.equal(bodies.length,3);assert.equal(done.jobs.length,1);assert.equal(done.jobs[0].state,'completed');assert.equal(done.jobs[0].result.exitCode,0);assert.equal(fs.readFileSync(path.join(workspace,'job-output.txt'),'utf8'),'foundation-job-ok');
  const asset=done.task.assets.find(a=>a.id.startsWith('workspace:'));assert(asset&&asset.available&&asset.role==='artifact'&&asset.path==='job-output.txt');
  await page.locator('[data-agent-more]').click();await page.getByRole('menuitem',{name:'后台作业',exact:true}).click();await page.waitForSelector('#agent-command-jobs');await page.locator('#agent-command-jobs details>summary').first().click();assert.match(await page.locator('#agent-command-jobs').innerText(),/foundation-job-ok/);assert.match(await page.locator('#agent-command-jobs header small').innerText(),/已完成/);assert.match(await page.locator('.agent-command-assets').innerText(),/job-output.txt/);assert(await page.evaluate(()=>$('#small-dialog').scrollWidth<=$('#small-dialog').clientWidth+1));assert.deepEqual(errors,[]);
  await page.screenshot({path:path.join(output,'foundation.png'),fullPage:true});fs.writeFileSync(path.join(output,'result.json'),JSON.stringify({passed:true,requests:bodies.length,taskSpec:done.task.taskSpec,jobs:done.jobs.map(j=>({jobId:j.jobId,state:j.state,exitCode:j.result.exitCode})),asset},null,2));console.log('Foundation: truthful capability catalog, typed TaskSpec, approved async command, real output, asset projection and job UI passed.');
 }finally{if(browser)await browser.close();model.closeAllConnections();await new Promise(resolve=>model.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});

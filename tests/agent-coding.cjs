'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http'),cp=require('node:child_process'),{randomUUID}=require('node:crypto');
const {chromium}=require('./browser.cjs');
(async()=>{
 const profile=process.env.KOK_TEST_PROFILE,workspace=path.join(profile,'workspace');fs.mkdirSync(workspace,{recursive:true});
 const original='export function add(a: number,b: number): number { return a-b; }\n';
 fs.writeFileSync(path.join(workspace,'package.json'),'{"type":"module"}');
 fs.writeFileSync(path.join(workspace,'sum.ts'),original);
 const git=args=>{const value=cp.spawnSync('git',['-c','core.hooksPath=','-c','commit.gpgsign=false',...args],{cwd:workspace,encoding:'utf8',windowsHide:true});assert.equal(value.status,0,value.stderr);};
 git(['init','-q']);git(['add','package.json','sum.ts']);git(['-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','-qm','baseline']);
 fs.appendFileSync(path.join(workspace,'sum.ts'),'// USER_UNCOMMITTED_CHANGE_KEEP\n');
 fs.writeFileSync(path.join(workspace,'verify.mjs'),"import assert from 'node:assert/strict';import fs from 'node:fs';import {add} from './sum.ts';assert.equal(add(2,3),5);fs.writeFileSync('result.json',JSON.stringify({value:add(2,3)}));console.log('CODE_CHECK_OK');\n");
 const expected=fs.readFileSync(path.join(workspace,'sum.ts'),'utf8').replace('a-b','a+b'),executable=path.join(path.dirname(process.env.KOK_TEST_EXECUTABLE),'runtime/research/node.exe');
 const bodies=[],errors=[];let browser,page,sha,workspaceId,jobId;
 const model=http.createServer((req,res)=>{let raw='';req.on('data',b=>raw+=b);req.on('end',()=>{
  const body=JSON.parse(raw);bodies.push(body);const receipts=body.messages.filter(m=>m.role==='tool').map(m=>{try{return JSON.parse(m.content);}catch{return {};}});
  sha=receipts.findLast(r=>r.result?.path==='sum.ts'&&r.result?.sha256)?.result.sha256||sha;
  workspaceId=receipts.findLast(r=>r.result?.path==='sum.ts'&&r.result?.workspaceId)?.result.workspaceId||workspaceId;
  jobId=receipts.findLast(r=>r.result?.jobId)?.result.jobId||jobId;
  const n=bodies.length;let calls=[];
  if(n===1)calls=[{name:'workspace_info',args:{}},{name:'git_workspace',args:{mode:'status'}},{name:'read_workspace_file',args:{path:'sum.ts'}}];
  if(n===2)calls=[{name:'apply_workspace_patch',args:{path:'sum.ts',workspaceId,expectedHash:sha,edits:[{oldText:'return a-b;',newText:'return a+b;'}]}}];
  if(n===3)calls=[{name:'start_command_job',args:{command:executable,args:['verify.mjs'],timeoutSeconds:15,inputs:['sum.ts'],outputs:['result.json']}}];
  if(n===4)calls=[{name:'wait_command_job',args:{jobId,waitSeconds:5}}];
  if(n===5)calls=[{name:'git_workspace',args:{mode:'diff',path:'sum.ts'}}];
  const delta=calls.length?{role:'assistant',tool_calls:calls.map((c,index)=>({index,id:'call-'+randomUUID(),type:'function',function:{name:c.name,arguments:JSON.stringify(c.args)}}))}:{role:'assistant',content:'已精确修复加法，保留原有未提交注释，实际验证退出码为0，result.json 的结果为5。'};
  res.writeHead(200,{'content-type':'text/event-stream'});res.end('data: '+JSON.stringify({id:randomUUID(),object:'chat.completion.chunk',model:'code-fixture',choices:[{index:0,delta,finish_reason:calls.length?'tool_calls':'stop'}],usage:{prompt_tokens:100,completion_tokens:50}})+'\n\ndata: [DONE]\n\n');
 });});await new Promise(r=>model.listen(0,'127.0.0.1',r));
 const output=path.resolve('test-output/agent-coding');fs.mkdirSync(output,{recursive:true});
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(30000);page.on('pageerror',e=>errors.push(e.message));await page.waitForFunction(()=>!$('#startup'));
  await page.evaluate(async({workspace,endpoint})=>{await native('saveAiConfig',{endpoint,model:'code-fixture',clearKey:true,automatic:false});await native('createDocument',{name:'UNRELATED_LIBRARY_PRIVATE_CANARY',format:'md',text:'Irrelevant library source.'});await native('agentWorkspaceSave',{enabled:true,root:workspace,sandbox:false});await refresh();navigate('agent');await AgentUi.newSession();Object.assign(AgentUi.state,{skill:'',intent:'assist',protocol:'auto',scope:'all',requestLimit:9});AgentUi.render();},{workspace,endpoint:'http://127.0.0.1:'+model.address().port+'/v1'});
  await page.locator('[data-agent-platform]').click();await page.waitForSelector('#agent-platform-form');await page.locator('#agent-platform-form [type=submit]').click();await page.waitForFunction(()=>!$('#small-dialog').open);assert.equal(await page.evaluate(()=>AgentUi.state.allowedTools),null,'saving default settings preserves automatic focused tool selection');
  await page.locator('#agent-input').fill('修复工作区 sum.ts 的加法错误，保留已有改动，运行 verify.mjs 并检查实际结果。');await page.locator('.agent-send').click();
  await page.waitForSelector('[data-agent-run]');assert(fs.readFileSync(path.join(workspace,'sum.ts'),'utf8').includes('a-b'));assert.match(await page.locator('.agent-diff').innerText(),/a-b/);assert.match(await page.locator('.agent-diff').innerText(),/a\+b/);
  assert(!bodies[0].tools.some(t=>t.function.name==='create_document'));assert(!bodies[0].tools.some(t=>t.function.name==='read_items'));assert(!JSON.stringify(bodies).includes('UNRELATED_LIBRARY_PRIVATE_CANARY'));
  await page.locator('[data-agent-run]').click();await page.waitForFunction(()=>AgentUi.state.messages.some(m=>m.tool==='start_command_job'&&m.status==='pending'));assert.match(await page.locator('.agent-preview').innerText(),/inputs/);await page.locator('[data-agent-run]').click();await page.waitForFunction(()=>!AgentUi.state.job);
  const task=await page.evaluate(()=>native('agentTaskRead',{taskId:AgentUi.state.watch}));assert.equal(task.status,'completed',task.error);assert.equal(task.options.workspaceVersion,1);assert.equal(bodies.length,6);assert.equal(fs.readFileSync(path.join(workspace,'sum.ts'),'utf8'),expected);assert.equal(JSON.parse(fs.readFileSync(path.join(workspace,'result.json'),'utf8')).value,5);
  const job=task.messages.find(m=>m.tool==='wait_command_job').result;assert.equal(job.state,'completed');assert.equal(job.result.exitCode,0);assert.equal(job.result.checkedInputs[0].sha256,task.messages.find(m=>m.tool==='apply_workspace_patch').result.sha256);assert(job.result.inputsCurrent);
  assert(task.messages.filter(m=>m.tool==='git_workspace').at(-1).readPreview.includes('USER_UNCOMMITTED_CHANGE_KEEP'));assert(task.assets.some(a=>a.path==='sum.ts'&&a.role==='artifact'));assert.deepEqual(errors,[]);
  assert.equal(await page.locator('[data-agent-workspace-reveal]').count(),2,'changed source and generated output are directly discoverable as workspace results');
  await page.screenshot({path:path.join(output,'code-result.png'),fullPage:true});fs.writeFileSync(path.join(output,'result.json'),JSON.stringify({passed:true,requests:bodies.length,workspaceVersion:task.options.workspaceVersion,sourcePreserved:true,checkedHash:job.result.checkedInputs[0].sha256,exitCode:0},null,2));
  console.log('Code desktop: repository discovery, existing dirty work, hash-guarded patch, actual TypeScript test, output asset and validated completion passed.');
 }catch(error){if(page){await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});const state=await page.evaluate(async()=>({state:AgentUi.state.recovery,task:AgentUi.state.watch?await native('agentTaskRead',{taskId:AgentUi.state.watch}):null})).catch(()=>null);fs.writeFileSync(path.join(output,'failure.json'),JSON.stringify({requests:bodies,state},null,2));}throw error;}finally{if(browser)await browser.close();model.closeAllConnections();await new Promise(r=>model.close(r));}
})().catch(e=>{console.error(e);process.exitCode=1;});

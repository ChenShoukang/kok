'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http'),net=require('node:net'),{randomUUID}=require('node:crypto');
const {chromium}=require('./browser.cjs');
(async()=>{
 const workspace=path.join(process.env.KOK_TEST_PROFILE,'workspace');fs.mkdirSync(workspace,{recursive:true});let blockedHits=0;const outside=http.createServer((req,res)=>{blockedHits++;res.end('OUTSIDE_ORIGIN');});await new Promise(r=>outside.listen(0,'127.0.0.1',r));
 const portProbe=net.createServer();await new Promise(r=>portProbe.listen(0,'127.0.0.1',r));const port=portProbe.address().port;await new Promise(r=>portProbe.close(r));
 const html='<html><head><title>Local verification fixture</title></head><body><label>Name<input name="name" placeholder="Name"></label><input type="password" value="PASSWORD_VALUE_CANARY"><button type="button" onclick="setTimeout(()=>document.getElementById(\'result\').textContent=\'Hello \'+document.querySelector(\'input[name=name]\').value,60)">Greet</button><p id="result">Ready</p><img src="http://127.0.0.1:'+outside.address().port+'/outside"></body></html>';
 const serverSource="import http from 'node:http';const html="+JSON.stringify(html)+";http.createServer((req,res)=>{res.writeHead(200,{'content-type':'text/html;charset=utf-8'});res.end(html);}).listen(Number(process.argv[2]),'127.0.0.1',()=>console.log('LOCAL_SERVICE_READY'));\n";
 fs.writeFileSync(path.join(workspace,'server.mjs'),serverSource);const executable=path.join(path.dirname(process.env.KOK_TEST_EXECUTABLE),'runtime/research/node.exe');
 const requests=[],errors=[];let page,browser,session,version,controlRows=[],jobId;
 const model=http.createServer((req,res)=>{let raw='';req.on('data',b=>raw+=b);req.on('end',()=>{
  const body=JSON.parse(raw);requests.push(body);const receipts=body.messages.filter(m=>m.role==='tool').map(m=>{try{return JSON.parse(m.content);}catch{return {};}});
  const observation=receipts.findLast(r=>r.result?.sessionId&&r.result?.pageVersion)?.result;if(observation){session=observation.sessionId;version=observation.pageVersion;controlRows=observation.controls;}
  jobId=receipts.findLast(r=>r.result?.jobId)?.result.jobId||jobId;let call;
  const n=requests.length;
  if(n===1)call={name:'start_command_job',args:{command:executable,args:['server.mjs',String(port)],timeoutSeconds:90}};
  if(n===2)call={name:'open_browser',args:{url:'http://127.0.0.1:'+port+'/'}};
  if(n===3)call={name:'browser_action',args:{sessionId:session,pageVersion:version,elementId:controlRows.find(c=>c.tag==='input').elementId,action:'fill',text:'Ada'}};
  if(n===4)call={name:'browser_action',args:{sessionId:session,pageVersion:version,elementId:controlRows.find(c=>c.label==='Greet').elementId,action:'click',waitForText:'Hello Ada'}};
  if(n===5)call={name:'read_browser',args:{sessionId:session,waitForText:'Hello Ada',timeoutSeconds:3}};
  if(n===6)call={name:'cancel_command_job',args:{jobId}};
  if(n===7)call={name:'wait_command_job',args:{jobId,waitSeconds:5}};
  const delta=call?{role:'assistant',tool_calls:[{index:0,id:'call-'+randomUUID(),type:'function',function:{name:call.name,arguments:JSON.stringify(call.args)}}]}:{role:'assistant',content:'已启动并验证本地网页：输入Ada，点击Greet后实际显示Hello Ada；随后停止了本任务启动的服务。'};
  res.writeHead(200,{'content-type':'text/event-stream'});res.end('data: '+JSON.stringify({id:randomUUID(),object:'chat.completion.chunk',model:'browser-fixture',choices:[{index:0,delta,finish_reason:call?'tool_calls':'stop'}],usage:{prompt_tokens:100,completion_tokens:30}})+'\n\ndata: [DONE]\n\n');
 });});await new Promise(r=>model.listen(0,'127.0.0.1',r));
 const output=path.resolve('test-output/agent-browser');fs.mkdirSync(output,{recursive:true});
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(30000);page.on('pageerror',e=>errors.push(e.message));await page.waitForFunction(()=>!$('#startup'));
  await page.evaluate(async({endpoint,workspace})=>{await native('saveAiConfig',{endpoint,model:'browser-fixture',clearKey:true,automatic:false});await native('agentWorkspaceSave',{enabled:true,root:workspace,sandbox:false});await refresh();navigate('agent');await AgentUi.newSession();Object.assign(AgentUi.state,{skill:'',intent:'assist',protocol:'auto',scope:'all',requestLimit:12});AgentUi.render();},{workspace,endpoint:'http://127.0.0.1:'+model.address().port+'/v1'});
  await page.locator('#agent-input').fill('运行工作区的本地网页服务，用浏览器输入Ada并点击Greet，核对Hello Ada，最后停止自己启动的服务。');await page.locator('.agent-send').click();
  const approved=new Set(),deadline=Date.now()+120000;let started=false;
  while(Date.now()<deadline){const current=await page.evaluate(()=>({running:!!AgentUi.state.job,watch:AgentUi.state.watch,pending:AgentUi.state.messages.filter(m=>m.status==='pending').map(m=>({id:m.id,tool:m.tool}))}));if(current.watch)started=true;if(started&&!current.running)break;
   for(const pending of current.pending){if(approved.has(pending.id))continue;assert(['start_command_job','open_browser','browser_action','cancel_command_job'].includes(pending.tool));approved.add(pending.id);await page.locator('[data-agent-run="'+pending.id+'"]').click();}
   await page.waitForTimeout(100);
  }
  const done=await page.evaluate(async()=>({task:await native('agentTaskRead',{taskId:AgentUi.state.watch}),jobs:await native('agentCommandJobList')}));assert.equal(done.task.status,'completed',done.task.error);assert.equal(requests.length,8);
  const read=done.task.messages.findLast(m=>m.tool==='read_browser').result;assert(read.conditionVerified);assert.match(read.text,/Hello Ada/);assert.equal(blockedHits,0,'browser cannot request another loopback origin');assert(!JSON.stringify(requests).includes('PASSWORD_VALUE_CANARY'),'passwords never enter model snapshots');
  assert.equal(done.jobs[0].state,'cancelled');const reachable=await new Promise(resolve=>{const socket=net.connect(port,'127.0.0.1');socket.once('connect',()=>{socket.destroy();resolve(true);});socket.once('error',()=>resolve(false));});assert(!reachable,'task-owned service actually stopped');assert.deepEqual(errors,[]);
  await page.screenshot({path:path.join(output,'browser-result.png'),fullPage:true});fs.writeFileSync(path.join(output,'result.json'),JSON.stringify({passed:true,requests:requests.length,approvals:approved.size,pageResult:read.text,blockedOriginRequests:blockedHits,serviceStopped:true,passwordExcluded:true},null,2));
  console.log('Browser desktop: owned service, isolated WebView2, actual fill/click/postcondition, blocked cross-origin request, password exclusion and process cleanup passed.');
 }catch(error){if(page)await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});throw error;}finally{if(browser)await browser.close();model.closeAllConnections();outside.closeAllConnections();await Promise.all([new Promise(r=>model.close(r)),new Promise(r=>outside.close(r))]);}
})().catch(e=>{console.error(e);process.exitCode=1;});

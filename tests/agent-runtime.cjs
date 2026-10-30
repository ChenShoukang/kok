'use strict';
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),assert=require('node:assert/strict'),{execFileSync}=require('node:child_process'),{chromium}=require('./browser.cjs'),F=require('./agent-fixture.cjs');
(async()=>{
 const root=process.env.KOK_TEST_PROFILE,exe=process.env.KOK_TEST_EXECUTABLE;assert(root&&exe);let browser,page,newPid=0,mode='slow',id='',reads=0;const requests=[],errors=[];
 const server=http.createServer((req,res)=>{let raw='';req.on('data',p=>raw+=p);req.on('end',()=>{const body=JSON.parse(raw);requests.push(body);res.setHeader('content-type','application/json');let turn;
  if(mode==='slow'){setTimeout(()=>res.end(F.payload(body,{reply:'SURVIVED_RENDERER_RELOAD',actions:[],done:true})),1500);return;}
  if(mode==='read')turn=reads++<2?{reply:'Read evidence',actions:[{tool:'read_items',args:{ids:[id]}}],done:false}:{reply:'Evidence ready',actions:[],done:true};
  if(mode==='handoff')turn={reply:'Handoff inspected',actions:[],done:true};
  if(mode==='approve')turn={reply:'Review tag',actions:[{tool:'add_tags',args:{ids:[id],tags:['runtime-test']}}],done:true};
  if(mode==='invalid'){res.end(F.invalid(body));return;}
  res.end(F.payload(body,turn));});});await new Promise(r=>server.listen(0,'127.0.0.1',r));
 async function connect(){browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().includes('app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(20000);page.on('pageerror',e=>errors.push(e.message));await page.waitForFunction(()=>!$('#startup'));}
 const idle=()=>page.waitForFunction(()=>!AgentUi.state.job);
 // Terminate only the isolated, executable-verified fixture to simulate an unexpected process exit.
 function closeOwned(pid){const safe=exe.replaceAll("'","''");execFileSync('powershell',['-NoProfile','-Command',`$p=Get-Process -Id ${Number(pid)} -ErrorAction SilentlyContinue;if($p -and $p.Path -eq '${safe}'){Stop-Process -Id $p.Id}`],{windowsHide:true});}
 async function fresh(){await page.evaluate(async id=>{if(AgentUi.state.job){await AgentUi.stop();}await AgentUi.newSession();AgentUi.state.scope='selected';AgentUi.state.context=[id];AgentUi.state.protocol='tools';AgentUi.render();},id);}
 async function send(text){await page.locator('#agent-input').fill(text);await page.locator('.agent-send[type=submit]').click();}
 try{
  await connect();id=await page.evaluate(async endpoint=>{await native('saveAiConfig',{endpoint,model:'runtime-fixture',clearKey:true});const e=await native('createDocument',{name:'Runtime source',format:'md',collection:'',text:'# SOURCE_SENTINEL\n\n'+Array.from({length:260},(_,i)=>'Evidence paragraph '+i+' contains distinct source detail.').join('\n')});await refresh();navigate('agent');return e.id;},`http://127.0.0.1:${server.address().port}/v1`);
  assert.deepEqual((await page.evaluate(()=>Kok.list())).sort(),(await page.evaluate(()=>Kok.catalog().modules.filter(m=>m.ui).map(m=>m.id))).sort());
  await fresh();await send('Continue through renderer reload');await page.waitForFunction(()=>!!AgentUi.state.job?.id);await page.locator('#agent-input').fill('DRAFT_SURVIVES');await page.evaluate(()=>AgentUi.save());const start=requests.length;
  await page.reload();await page.waitForFunction(()=>!$('#startup'));await page.evaluate(()=>navigate('agent'));await idle();assert.equal(requests.length,start);assert((await page.locator('.agent-stream').innerText()).includes('SURVIVED_RENDERER_RELOAD'));assert.equal(await page.locator('#agent-input').inputValue(),'DRAFT_SURVIVES');
  console.log('PASS: renderer reload keeps the native task running with no duplicate API call and preserves the draft.');
  mode='read';reads=0;await fresh();await send('Read twice to test result references');await idle();const sourceTask=await page.evaluate(()=>AgentUi.state.watch),events=await page.evaluate(taskId=>native('agentTaskEvents',{taskId}),sourceTask);
  const domainCompletions=await page.evaluate(async({taskId,events})=>{const all=await Promise.all(events.filter(e=>e.kind==='tool.completed').map(e=>native('agentTaskEvents',{taskId,eventId:e.id})));const t=await native('agentTaskRead',{taskId});return all.filter(e=>t.calls.find(c=>c.id===e.data.callId)?.tool==='read_items').length;},{taskId:sourceTask,events});assert.equal(domainCompletions,1);assert.equal(events.filter(e=>e.kind==='tool.reused').length,1);
  await page.evaluate(()=>{const details=$('.agent-tool-group');details.open=true;const read=$('.agent-read-preview');read.open=true;});await page.locator('[data-agent-full-result]').first().click();assert((await page.locator('.agent-full-result').innerText()).length>1600);await page.evaluate(()=>closeSmall());
  const beforeHandoff=requests.length;await page.locator('[data-agent-prepare-handoff]').click();await page.locator('#agent-handoff-note').fill('Use only verified source evidence');await page.locator('#agent-handoff-confirm').click();assert.equal(requests.length,beforeHandoff);
  await page.waitForFunction(()=>!!AgentUi.state.handoff?.eventId);const handoff=await page.evaluate(()=>AgentUi.state.handoff);assert(handoff?.eventId);const prepared=await page.evaluate(h=>native('agentHandoffRead',{handoff:h}),handoff);assert(prepared.text.includes('SOURCE_SENTINEL'));
  await page.evaluate(async id=>{await native('update',{id,patch:{notes:'Changed source note'}});await refresh();},id);
  // Update is a normal source edit; the handoff must not quietly reuse stale evidence.
  const stale=await page.evaluate(h=>native('agentHandoffRead',{handoff:h}),handoff);assert(stale.text.includes('Source changed')&&!stale.text.includes('SOURCE_SENTINEL'));
  mode='handoff';await send('Continue this handoff');await idle();assert(F.text(requests.at(-1)).includes('Source changed'));
  console.log('PASS: full tool results are retained once, handoff is explicit and changed sources invalidate cached evidence.');
  mode='approve';await fresh();await send('Prepare a reviewed tag');await page.waitForSelector('[data-agent-run]');const task=await page.evaluate(()=>AgentUi.state.watch);const count=requests.length;await page.evaluate(()=>AgentUi.save());closeOwned(process.env.KOK_TEST_NATIVE_PID);await browser.close();browser=null;
  const psQuote=s=>"'"+s.replaceAll("'","''")+"'";const command=`$p=Start-Process -FilePath ${psQuote(exe)} -ArgumentList @('--debug','--test-silent','--data-dir',('"'+${psQuote(root)}+'"')) -PassThru -WindowStyle Hidden; $p.Id`;
  await new Promise(r=>setTimeout(r,600));newPid=Number(execFileSync('powershell',['-NoProfile','-Command',command],{encoding:'utf8',windowsHide:true}).trim());
  for(let n=0;n<80;n++){try{await fetch('http://127.0.0.1:9225/json/version').then(r=>r.json());break;}catch{await new Promise(r=>setTimeout(r,150));}}
  await connect();await page.evaluate(()=>navigate('agent'));assert.equal(requests.length,count);assert.equal(await page.locator('[data-agent-run]').count(),0);assert(await page.locator('[data-agent-continue-task]').count());
  assert.deepEqual(await page.evaluate(id=>byId(id).tags||[],id),[]);await page.locator('[data-agent-continue-task]').click();await page.waitForSelector('[data-agent-run]');assert.equal(requests.length,count);await page.locator('[data-agent-run]').click();await idle();assert.deepEqual(await page.evaluate(id=>byId(id).tags,id),['runtime-test']);
  const duplicate=await page.evaluate(async taskId=>{const t=await native('agentTaskRead',{taskId});try{await native('agentTaskDecide',{taskId,callId:t.calls[0].id,approve:true});return false;}catch{return true;}},task);assert(duplicate);
  console.log('PASS: process restart pauses work, explicit resume restores review without replanning or replaying writes.');
  mode='invalid';await fresh();await page.evaluate(()=>{AgentUi.state.requestLimit=1;AgentUi.render();});const cap=requests.length;await send('One request including repairs');await idle();assert.equal(requests.length-cap,1);
  await page.evaluate(async()=>{await setting({uiLanguage:'en'});I18n.applyLanguage('en');AgentUi.render();});assert.equal(await page.locator('[data-agent-task-records]').innerText(),'Task history');const recordedTasks=await page.evaluate(()=>native('agentTaskList').then(rows=>rows.length));assert(recordedTasks>=4);await page.locator('[data-agent-task-records]').click();await page.waitForFunction(count=>document.querySelectorAll('.agent-task-record').length===count,recordedTasks);
  assert.deepEqual(errors,[]);console.log('PASS: repair requests respect the same cap, native modules remain registered and task history supports English.');
 }finally{server.close();if(newPid){try{closeOwned(newPid);}catch{}}if(browser)await browser.close().catch(()=>{});if(newPid){try{execFileSync('powershell',['-NoProfile','-Command',`$p=Get-Process -Id ${newPid} -ErrorAction SilentlyContinue;if($p -and $p.Path -eq '${exe.replaceAll("'","''")}'){Stop-Process -Id $p.Id}`],{windowsHide:true});}catch{}}}
})().catch(e=>{console.error(e);process.exitCode=1;});

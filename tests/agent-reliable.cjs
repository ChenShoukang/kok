'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),http=require('node:http'),{chromium}=require('./browser.cjs'),Fixture=require('./agent-fixture.cjs');
(async()=>{
 const root=process.env.KOK_TEST_PROFILE;assert(root);const requests=[],errors=[],remote=[],held=[];let browser,page,mode='note',ids,slow=0;
 const server=http.createServer((req,res)=>{let body='';req.on('data',c=>body+=c);req.on('end',()=>{
  const request=JSON.parse(body);requests.push(request);const content=request.messages.map(m=>m.content).join('\n'),question=request.messages.at(-1).content;
  let reply={reply:'COMPLETE',actions:[],done:true};
  if(mode==='create')reply={reply:'Create a note.',actions:[{tool:'create_document',args:{name:'Safe created document',format:'md',text:'# Agent output'}}],done:true};
  if(mode==='note')reply={reply:'Append a reviewed note.',actions:[{tool:'write_note',args:{id:ids.a,text:'AGENT_NOTE',mode:'append'},why:'Test note'}],done:true};
  if(mode==='scope')reply=!Fixture.called(request,'search_library')?{reply:'Search scope.',actions:[{tool:'search_library',args:{},why:'search'}],done:false}:!Fixture.called(request,'read_items')?{reply:'Read scope.',actions:[{tool:'read_items',args:{ids:[ids.a]},why:'read'}],done:false}:{reply:'SCOPE_COMPLETE',actions:[],done:true};
  if(mode==='malicious')reply={reply:'Wrong source.',actions:[{tool:'read_items',args:{ids:[ids.b]}}],done:false};
  if(mode==='read-limit')reply={reply:'Read once.',actions:[{tool:'read_items',args:{ids:[ids.a]}}],done:false};
  if(mode==='steer'){const n=slow++;reply=n?{reply:'ADJUSTED_RESULT',actions:[],done:true}:{reply:'STALE_PLAN',actions:[{tool:'write_note',args:{id:ids.a,text:'STALE_WRITE'}}],done:true};}
  if(mode==='late')reply={reply:'LATE_AGENT_REPLY',actions:[],done:true};
  if(mode==='stream'){
   // Native mode streams the plain reply; JSON mode streams the protocol object that the page parses as it arrives.
   res.writeHead(200,{'content-type':'text/event-stream'});const completed=Fixture.message(request,{reply:'STREAMED_REPLY live progress',actions:[],done:true}),raw=request.tools?completed.content:completed.content,cut=request.tools?16:28;
   res.write('data: '+JSON.stringify({choices:[{delta:{content:raw.slice(0,cut)}}]})+'\n\n');
   setTimeout(()=>{res.write('data: '+JSON.stringify({choices:[{delta:{content:raw.slice(cut),...(completed.tool_calls?{tool_calls:completed.tool_calls.map((c,index)=>({...c,index}))}:{})},finish_reason:completed.tool_calls?'tool_calls':'stop'}]})+'\n\n');res.write('data: '+JSON.stringify({choices:[],usage:{prompt_tokens:125,completion_tokens:35}})+'\n\n');res.end('data: [DONE]\n\n');},700);return;
  }
  const payload=Fixture.payload(request,reply,{prompt_tokens:120,completion_tokens:40});
  const send=()=>{res.setHeader('content-type','application/json');res.end(payload);};
  // A held response may be released after a stop aborts the request; ignore the closed socket.
  if(mode==='queue'){res.on('error',()=>{});held.push(()=>{try{send();}catch{}});return;}if(mode==='late'||mode==='steer')setTimeout(send,650);else send();
 });});
 await new Promise(r=>server.listen(0,'127.0.0.1',r));
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');const context=browser.contexts()[0];page=context.pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(20000);page.on('pageerror',e=>errors.push(e.stack||e.message));
  await context.route(/^https?:\/\//,route=>{if(['app.kok.local','media.kok.local'].includes(new URL(route.request().url()).hostname))return route.continue();remote.push(route.request().url());return route.abort();});
  await page.waitForFunction(()=>!$('#startup'));assert(await page.evaluate(()=>typeof agent==='undefined'&&typeof agentPaint==='undefined'&&typeof runAgent==='undefined'&&Object.isFrozen(AgentUi)),'Agent implementation helpers must stay module-private');await page.setViewportSize({width:1280,height:860});
  ids=await page.evaluate(async endpoint=>{await native('saveAiConfig',{endpoint,model:'local-agent-fixture',apiKey:'',clearKey:true,automatic:false});const a=await native('createDocument',{name:'ALLOWED_SOURCE',collection:'Allowed folder',format:'md',text:'# Allowed\nSCOPE_EVIDENCE'}),b=await native('createDocument',{name:'OUTSIDE_NAME_SECRET',collection:'Other folder',format:'md',text:'OUTSIDE_BODY_SECRET'});await native('update',{id:a.id,patch:{notes:'INITIAL_NOTE'}});await refresh();navigate('agent');return{a:a.id,b:b.id};},`http://127.0.0.1:${server.address().port}/v1`);
  const fresh=async()=>{await page.evaluate(async id=>{if(AgentUi.state.job)await AgentUi.stop();await AgentUi.newSession();AgentUi.state.context=[id];AgentUi.state.scope='selected';AgentUi.state.skill='';AgentUi.state.requestLimit=6;AgentUi.state.costLimit=0;AgentUi.render();},ids.a);};
  const send=async text=>{await page.locator('#agent-input').fill(text);await page.locator('button[type=submit]').click();};
  const idle=()=>page.waitForFunction(()=>!AgentUi.state.job);
  const waitFixture=async(predicate,label)=>{for(let n=0;n<400;n++){if(predicate())return;await new Promise(resolve=>setTimeout(resolve,25));}throw Error('fixture timeout: '+(label||'event'));};
  const releaseHeld=()=>{const release=held.shift();assert(release,'expected a held model response');release();};
  const drawerCount=count=>page.waitForFunction(count=>{const drawer=document.querySelector('.agent-queued');return drawer&&!drawer.hidden&&drawer.innerText.includes('待处理 · '+count);},count);
  await fresh();await page.locator('#agent-input').fill('UNSENT_DRAFT');await page.evaluate(()=>navigate('overview'));await page.evaluate(()=>navigate('agent'));assert.equal(await page.locator('#agent-input').inputValue(),'UNSENT_DRAFT');
  mode='scope';const scopeStart=requests.length;await send('Summarize the selected source');await idle();assert(requests.slice(scopeStart).every(r=>!JSON.stringify(r).includes('OUTSIDE_NAME_SECRET')&&!JSON.stringify(r).includes('OUTSIDE_BODY_SECRET')));assert.equal(requests.length-scopeStart,3);
  assert.deepEqual(await page.evaluate(async()=>(await native('agentTaskRead',{taskId:AgentUi.state.watch})).options.allowed),[ids.a],'the native task records only the selected scope');
  console.log('PASS: route draft, strict native scope, scoped search/read and three-round task.');
  await fresh();await page.locator('#agent-advanced>summary').click();await page.locator('#agent-scope').selectOption('collection');await page.locator('#agent-collection').selectOption('Allowed folder');const folderStart=requests.length;await send('Summarize this folder');await idle();assert(requests.slice(folderStart).every(r=>!JSON.stringify(r).includes('OUTSIDE_NAME_SECRET')));
  await fresh();await page.locator('[data-agent-pick]').click();await page.locator('#agent-pick-search').fill('OUTSIDE');await page.locator('#agent-pick-list input').check();await page.locator('#agent-pick-search').fill('ALLOWED');assert(await page.locator('#agent-pick-list input').isChecked());await page.locator('#agent-pick-done').click();assert.equal(await page.evaluate(()=>AgentUi.state.context.length),2);await page.waitForFunction(()=>!$('#small-dialog').open);
  await fresh();mode='create';await send('Create a small note');await page.waitForSelector('[data-agent-run]');await page.locator('[data-agent-run]').click();await idle();const created=await page.evaluate(()=>{const m=AgentUi.state.messages.find(m=>m.tool==='create_document');return{id:m.result.id,path:byId(m.result.id).path};});await page.locator('[data-agent-undo]').click();await page.waitForFunction(id=>!byId(id),created.id);assert(fs.existsSync(created.path));
  console.log('PASS: collection boundaries, filtered picker preserves selections and created-file undo keeps the disk copy.');
  await fresh();mode='note';await send('Append this note');await page.waitForSelector('[data-agent-run]');assert.equal(await page.evaluate(id=>byId(id).notes,ids.a),'INITIAL_NOTE');
  await page.locator('details.agent-diff summary').click();assert(await page.locator('.agent-diff').innerText().then(t=>t.includes('INITIAL_NOTE')&&t.includes('AGENT_NOTE')));
  await page.locator('[data-agent-run]').click();await idle();await page.evaluate(async id=>{await native('update',{id,patch:{notes:'INITIAL_NOTE\n\nAGENT_NOTE\nMANUAL_NOTE'}});await refresh();},ids.a);
  await page.locator('[data-agent-undo]').click();await page.waitForFunction(id=>byId(id).notes.includes('MANUAL_NOTE'),ids.a);assert.match(await page.locator('.toast.error').last().innerText(),/内容已被修改/);
  console.log('PASS: review before write and conflict-safe undo preserve manual notes.');
  await fresh();mode='malicious';await send('Stay in this scope');await idle();assert.equal(await page.locator('[data-agent-run]').count(),0);assert(await page.locator('.agent-error').count());
  await fresh();mode='stream';await send('Stream the answer');await page.waitForFunction(()=>$('#agent-live-reply')?.textContent.includes('STREAMED_REPLY'));
  await page.locator('#agent-input').fill('DRAFT_DURING_STREAM');await page.evaluate(()=>{window.agentInputBefore=$('#agent-input');$('#agent-input').setSelectionRange(2,7);});await idle();
  assert.equal(await page.evaluate(()=>window.agentInputBefore===$('#agent-input')&&$('#agent-input').selectionStart===2&&$('#agent-input').selectionEnd===7),true);assert.equal(await page.locator('#agent-input').inputValue(),'DRAFT_DURING_STREAM');assert.equal(await page.evaluate(()=>AgentUi.state.usage.inputTokens),125);assert.equal(await page.evaluate(()=>AgentUi.state.usage.estimated),false);
  console.log('PASS: genuine stream progress, input DOM/selection retention and provider token usage.');
  // Steering no longer cancels an in-flight proposal: it survives the approval boundary and is injected next round.
  // v0.12 P1 profiles hide write tools for pure read/summarize goals, so use an organize+read mixed goal that
  // keeps write_note exposed while preserving the "steering across an approval boundary" scenario.
  await fresh();mode='steer';slow=0;const steeringStart=requests.length;await send('Organize and summarize this note');await page.waitForSelector('[data-agent-run]');
  const steerTask=await page.evaluate(()=>AgentUi.state.watch);
  await page.locator('#agent-input').fill('Only summarize, no edits');await page.locator('button[type=submit]').click();
  await page.waitForFunction(()=>$('#agent-input').value==='');await drawerCount(1);
  assert.equal(await page.evaluate(()=>AgentUi.state.queue.length),0,'steering never touches the local queue');
  assert.equal(await page.locator('[data-agent-edit-queued]').count(),0,'native queue rows are read-only');
  assert.equal(requests.length-steeringStart,1,'steering waits for the approval boundary instead of starting a request');
  await page.locator('[data-agent-run]').click();await idle();
  assert.equal(requests.length-steeringStart,2);
  assert((await page.evaluate(id=>byId(id).notes||'',ids.a)).includes('STALE_WRITE'),'steering does not cancel the tool the model already proposed');
  assert(requests.at(-1).messages.some(m=>String(m.content||'').includes('Only summarize, no edits')),'the steering input reaches the next model round of the same task');
  assert.equal(await page.evaluate(()=>AgentUi.state.watch),steerTask);
  const steeringEvents=await page.evaluate(async id=>{const rows=await native('agentTaskEvents',{taskId:id});const out=[];for(const row of rows.filter(row=>row.kind==='input.consumed'))out.push(await native('agentTaskEvents',{taskId:id,eventId:row.id}));return out;},steerTask);
  assert.equal(steeringEvents.at(-1).data.mode,'steer');
  assert.equal(await page.locator('[data-agent-run]').count(),0);assert((await page.locator('.agent-stream').innerText()).includes('ADJUSTED_RESULT'));
  await fresh();mode='queue';const followStart=requests.length;await send('First task');
  await page.waitForFunction(()=>!!AgentUi.state.job?.id);const followTask=await page.evaluate(()=>AgentUi.state.job.id);
  await waitFixture(()=>held.length===1,'first held request');
  await page.locator('#agent-input').fill('Queued follow-up');await page.locator('[data-agent-queue]').click();
  await page.waitForFunction(()=>$('#agent-input').value==='');await drawerCount(1);
  assert.match(await page.locator('.agent-queued').innerText(),/Queued follow-up/);
  assert.equal(await page.evaluate(()=>AgentUi.state.queue.length),0,'the running composer never fills the local queue');
  releaseHeld();await waitFixture(()=>requests.length===followStart+2,'continued model round');
  await page.waitForFunction(()=>{const drawer=document.querySelector('.agent-queued');return !drawer||drawer.hidden;});
  assert(requests.at(-1).messages.some(m=>String(m.content||'').includes('Queued follow-up')),'the follow-up is injected when the run would finish');
  assert.equal(await page.evaluate(()=>AgentUi.state.watch),followTask,'the follow-up keeps the same task');
  releaseHeld();await idle();
  assert.equal(requests.length-followStart,2);
  assert.equal(await page.evaluate(()=>AgentUi.state.messages.filter(m=>m.role==='user').length),2);
  assert.equal(await page.evaluate(async()=>(await native('agentTaskList')).filter(task=>task.sessionId===AgentUi.state.sessionId).length),1,'one task, not a queued successor');
  console.log('PASS: steering survives an approval boundary without cancelling the approved write, and follow-up input continues the same task.');
  await fresh();mode='late';await send('Cancel when switching modes');await page.waitForFunction(()=>!!AgentUi.state.job);await page.evaluate(async()=>{await native('setAppMode',{mode:'pure'});state.settings.appMode='pure';applyAppMode();render();});await idle();await new Promise(r=>setTimeout(r,850));assert.equal(await page.evaluate(()=>AgentUi.state.messages.some(m=>m.text==='LATE_AGENT_REPLY')),false);assert.equal(await page.locator('#agent-input').count(),0);
  await page.evaluate(async()=>{await native('agentTaskStop',{});await native('setAppMode',{mode:'smart',acknowledged:true});await refresh();navigate('agent');});
  console.log('PASS: switching to Pure cancels in-flight work and ignores late results; cancel also works in Pure.');
  await fresh();const budgetStart=requests.length;await page.evaluate(()=>{AgentUi.state.costLimit=.000001;AgentUi.state.inputRate=1;AgentUi.state.outputRate=1;AgentUi.render();});mode='stream';await send('Must not reach the API');await idle();assert.equal(requests.length,budgetStart);assert((await page.locator('.agent-error').innerText()).includes('费用'));
  await fresh();mode='read-limit';await page.evaluate(()=>{AgentUi.state.requestLimit=1;AgentUi.render();});const limitStart=requests.length;await send('Only one request');await idle();assert.equal(requests.length-limitStart,1);
  console.log('PASS: cost preflight blocks without network, request limit bounds model calls.');
  await fresh();mode='note';await send('Remember this proposal');await page.waitForSelector('[data-agent-run]');await page.locator('#agent-input').fill('PERSISTED_DRAFT');const pendingId=await page.evaluate(()=>AgentUi.state.sessionId);await page.evaluate(()=>AgentUi.save());
  await page.reload();await page.waitForFunction(()=>!$('#startup'));await page.evaluate(()=>navigate('agent'));assert.equal(await page.locator('#agent-input').inputValue(),'PERSISTED_DRAFT');assert.equal(await page.locator('[data-agent-run]').count(),1);assert.equal(await page.evaluate(()=>AgentUi.state.sessionId),pendingId);assert(await page.evaluate(()=>!!AgentUi.state.job));await page.locator('[data-agent-stop]').click();await idle();assert(await page.locator('.agent-paused').count());
  await page.evaluate(()=>AgentUi.save());assert(!fs.readFileSync(path.join(root,'agent-sessions.sealed')).includes(Buffer.from('PERSISTED_DRAFT')));
  await page.evaluate(async()=>{await AgentUi.newSession();});await page.locator('[data-agent-sessions]').click();await page.locator(`[data-agent-resume="${pendingId}"]`).click();await page.waitForFunction(id=>AgentUi.state.sessionId===id&&$('#agent-input')?.value==='PERSISTED_DRAFT',pendingId);assert.equal(await page.locator('#agent-input').inputValue(),'PERSISTED_DRAFT');
  console.log('PASS: encrypted renderer reload recovery, explicitly paused proposals, conversation switch and draft restore.');
  await page.evaluate(async()=>{await setting({skin:'classic',fontFamily:'smiley'});applyTheme();await applyFont();await settings();});assert.equal(await page.locator('#settings-panel-appearance #skin-select').count(),0);assert.equal(await page.locator('body').getAttribute('data-skin'),'flat');assert.match(await page.locator('#search').evaluate(el=>getComputedStyle(el).fontFamily),/Smiley/);await page.evaluate(()=>closeSmall());
  for(const width of [660,1280]){await page.setViewportSize({width,height:760});await page.evaluate(()=>{AgentUi.state.options=true;AgentUi.render();});await page.locator('#agent-input').scrollIntoViewIfNeeded();assert(await page.locator('#agent-input').isVisible());assert(await page.locator('.agent-send').isVisible());assert(await page.evaluate(()=>$('#main').scrollWidth<=$('#main').clientWidth+1));await page.screenshot({path:path.join(root,`agent-${width}.png`)});}
  await page.evaluate(async()=>{await setting({uiLanguage:'en'});I18n.applyLanguage('en');AgentUi.render();});assert.equal(await page.locator('[data-agent-sessions]').getAttribute('aria-label'),'Sessions');
  assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);assert(!requests.some(r=>JSON.stringify(r).includes(root.replace(/\\/g,'\\\\'))));
  console.log('PASS: Classic font/settings, small/large window controls, English, no JS errors and no remote browser requests.');
  // Stop returns unconsumed native input to the composer; it survives restart without auto-sending.
  await page.evaluate(async()=>{await setting({uiLanguage:'zh'});I18n.applyLanguage('zh');});await fresh();mode='queue';await send('Stop this task');
  await page.waitForFunction(()=>!!AgentUi.state.job?.id);const stopTask=await page.evaluate(()=>AgentUi.state.job.id);
  await waitFixture(()=>held.length===1,'held request before stop');
  await page.locator('#agent-input').fill('PAUSED_FOLLOWUP');await page.locator('[data-agent-queue]').click();
  await page.waitForFunction(()=>$('#agent-input').value==='');await drawerCount(1);
  await page.locator('[data-agent-stop]').click();await idle();await page.waitForFunction(async id=>!(await native('agentTaskRead',{taskId:id})).running,stopTask);held.splice(0).forEach(release=>release());
  const pausedRequests=requests.length;
  await page.waitForFunction(()=>$('#agent-input').value==='PAUSED_FOLLOWUP');await page.waitForFunction(()=>$('#toasts').innerText.includes('因停止未执行'));
  assert.equal(await page.evaluate(()=>AgentUi.state.draft),'PAUSED_FOLLOWUP','stop moves unconsumed native input back into the composer');
  assert.deepEqual(await page.evaluate(()=>AgentUi.state.queue),[]);
  assert.equal(await page.locator('.agent-queued').isVisible(),false);
  const stoppedSnapshot=await page.evaluate(async id=>native('agentTaskRead',{taskId:id}),stopTask);
  assert.equal(stoppedSnapshot.running,false);assert.deepEqual(stoppedSnapshot.queue,[]);assert.equal(stoppedSnapshot.restoredInputs.length,1);assert.equal(stoppedSnapshot.restoredInputs[0].text,'PAUSED_FOLLOWUP');
  await page.evaluate(()=>AgentUi.save());await page.reload();await page.waitForFunction(()=>!$('#startup'));await page.evaluate(()=>navigate('agent'));
  await page.waitForFunction(()=>AgentUi.state.loaded&&$('#agent-input')?.value==='PAUSED_FOLLOWUP');
  assert.equal(requests.length,pausedRequests,'restored input is never auto-sent');
  mode='late';await page.locator('#agent-input').press('Enter');await idle();assert.equal(requests.length,pausedRequests+1,'the restored text can be sent as a normal new task');
  await fresh();mode='late';await send('Cancel before entering the private folder');await page.evaluate(async()=>{await native('createPrivate',{password:'OnlyForGeneratedFixture927!'});});await page.waitForFunction(()=>state.isPrivate);assert.equal(await page.evaluate(async()=>{try{await native('agentSessions');return false;}catch{return true;}}),true);await page.evaluate(()=>native('lockPrivate'));await page.waitForFunction(()=>!state.isPrivate);await idle();assert.equal(await page.evaluate(()=>AgentUi.state.messages.some(m=>m.text==='LATE_AGENT_REPLY')),false);
  assert.deepEqual(errors,[]);console.log('PASS: restored queue input survives restart without auto-send, and private entry cancels work and blocks normal-session access.');
 }catch(error){if(page){await page.screenshot({path:path.join(root,'failure.png')}).catch(()=>{});console.error('Failure state:',await page.evaluate(()=>({phase:AgentUi.state.phase,job:!!AgentUi.state.job,cost:AgentUi.state.costLimit,rates:[AgentUi.state.inputRate,AgentUi.state.outputRate],scope:AgentUi.scope(),draft:AgentUi.state.draft,messages:AgentUi.state.messages.map(m=>({role:m.role,text:m.text,status:m.status})),toasts:$('#toasts').innerText})).catch(()=>null));}throw error;}finally{held.splice(0).forEach(release=>release());server.close();if(browser)await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

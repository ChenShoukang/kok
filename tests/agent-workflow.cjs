'use strict';
// Real WebView2 and native tasks; the model endpoint is an isolated loopback fixture.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http');
const {chromium}=require('./browser.cjs'),Fixture=require('./agent-fixture.cjs');
(async()=>{
 const profile=process.env.KOK_TEST_PROFILE;assert(profile);
 const output=path.resolve('test-output/agent-workflow');fs.mkdirSync(output,{recursive:true});
 let browser,page,itemId,mode='write';const requests=[],held=[],errors=[],remote=[];
 const server=http.createServer((req,res)=>{
  let raw='';req.on('data',chunk=>raw+=chunk);req.on('end',()=>{
   const request=JSON.parse(raw);requests.push(request);res.setHeader('content-type','application/json');
   let turn=mode==='write'?{reply:'这段笔记会补充到所选资料。',actions:[{tool:'write_note',args:{id:itemId,text:'APPROVED_WORKFLOW_NOTE',mode:'append'}}],done:false}:{reply:'已按要求完成。',actions:[],done:true};
   if(mode==='outline')turn=Fixture.called(request,'create_document')?{reply:'章纲草稿已保存，可以继续展开任一章节。',actions:[],done:true}:{reply:'## 三章，讲完一次重逢\n- **雨夜来信**：失联多年的朋友寄来一张旧车票。\n- **最后一班车**：两人在旅途中说出当年的误会。\n- **天亮以后**：他们各自作出新的选择。',actions:[{tool:'create_document',args:{name:'重逢 · 章纲草稿',format:'md',text:'# 重逢\n\n## 雨夜来信\n一张旧车票让两人再次相遇。\n\n## 最后一班车\n在旅途中解开误会。\n\n## 天亮以后\n各自作出新的选择。'}}],done:false};
   const payload=Fixture.payload(request,turn,{prompt_tokens:60,completion_tokens:30});
   // Held responses may be released after a stop aborts the request; ignore the closed socket.
   if(mode==='hold'){res.on('error',()=>{});held.push(()=>{try{res.end(payload);}catch{}});}else res.end(payload);
  });
 });
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');const context=browser.contexts()[0];
  page=context.pages().find(page=>page.url().startsWith('https://app.kok.local')&&!page.url().includes('preview='));
  page.setDefaultTimeout(18000);page.on('pageerror',error=>errors.push(error.message));
  await context.route(/^https?:\/\//,route=>['app.kok.local','media.kok.local'].includes(new URL(route.request().url()).hostname)?route.continue():(remote.push(route.request().url()),route.abort()));
  await page.waitForFunction(()=>!$('#startup'));await page.setViewportSize({width:1440,height:960});
  itemId=await page.evaluate(async endpoint=>{
   await native('saveAiConfig',{endpoint,model:'workflow-fixture',clearKey:true,automatic:false});
   const item=await native('createDocument',{name:'交互设计笔记',format:'md',collection:'设计',text:'# 设计笔记\n工作流测试资料。'});
   await refresh();await setting({theme:'dark',fontFamily:'system'});applyTheme();await applyFont();navigate('agent');await AgentUi.newSession();
   Object.assign(AgentUi.state,{context:[item.id],scope:'selected',intent:'assist',skill:'',requestLimit:6});AgentUi.render();
   return item.id;
  },`http://127.0.0.1:${server.address().port}/v1`);
  const send=async text=>{await page.locator('#agent-input').fill(text);await page.locator('#agent-form [type=submit]').click();};
  const idle=()=>page.waitForFunction(()=>!AgentUi.state.job);
  const fresh=()=>page.evaluate(async itemId=>{await AgentUi.newSession();Object.assign(AgentUi.state,{context:[itemId],scope:'selected',skill:'',intent:'assist'});AgentUi.render();},itemId);
  const waitFixture=async(predicate,label)=>{for(let n=0;n<400;n++){if(predicate())return;await new Promise(resolve=>setTimeout(resolve,25));}throw Error('fixture timeout: '+(label||'event'));};
  const releaseHeld=()=>{const release=held.shift();assert(release,'expected a held model response');release();};
  const taskEvents=async(taskId,kind)=>page.evaluate(async({taskId,kind})=>{const rows=await native('agentTaskEvents',{taskId});const found=[];for(const row of rows.filter(row=>row.kind===kind))found.push(await native('agentTaskEvents',{taskId,eventId:row.id}));return found;},{taskId,kind});
  const openQueue=count=>page.waitForFunction(count=>{const drawer=document.querySelector('.agent-queued');return drawer&&!drawer.hidden&&drawer.innerText.includes('待处理 · '+count);},count);

  assert(await page.locator('.agent-send').isDisabled(),'an empty composer cannot be sent');
  const initialRequests=requests.length;
  await page.locator('#agent-input').fill('中文输入法');
  await page.locator('#agent-input').dispatchEvent('keydown',{key:'Enter',code:'Enter',isComposing:true,bubbles:true});
  await page.locator('#agent-input').dispatchEvent('keydown',{key:'Enter',code:'Enter',keyCode:229,bubbles:true});
  assert.equal(requests.length,initialRequests);assert.equal(await page.locator('#agent-input').inputValue(),'中文输入法');
  await page.locator('#agent-input').press('Shift+Enter');assert.match(await page.locator('#agent-input').inputValue(),/\n/);
  await page.locator('#agent-input').fill('');assert(await page.locator('.agent-send').isDisabled());

  await send('把结论补充到这份笔记');await page.waitForSelector('[data-agent-run]');
  assert.equal(await page.evaluate(itemId=>byId(itemId).notes||'',itemId),'','review never writes before approval');
  assert(await page.locator('[data-agent-review-target]').isVisible(),'even one pending change has a fixed review entry');
  assert.equal(await page.locator('[data-agent-disclosure=activity]').evaluate(node=>node.open),false,'technical activity starts collapsed');
  assert(await page.locator('[data-agent-stop]').isVisible());assert.equal(await page.locator('.agent-send').isVisible(),false,'empty busy composer shows stop');
  await page.locator('[data-agent-review-target]').click();
  assert(await page.locator('[data-agent-run]').evaluate(node=>node===document.activeElement));
  await page.screenshot({path:path.join(output,'review-dark.png'),animations:'disabled'});
  await page.locator('[data-agent-run]').click();await idle();
  assert.match(await page.evaluate(itemId=>byId(itemId).notes||'',itemId),/APPROVED_WORKFLOW_NOTE/);
  assert.equal(await page.locator('[data-agent-result]').count(),1);
  await page.locator('[data-agent-disclosure=activity]>summary').click();await page.evaluate(()=>AgentUi.render({scroll:false}));
  assert(await page.locator('[data-agent-disclosure=activity]').evaluate(node=>node.open),'explicit activity disclosure survives a paint');
  await page.locator('[data-agent-disclosure=activity]>summary').click();
  console.log('PASS: IME-safe sending, empty control state, one-change review, native approval and stable activity disclosure.');

  // M0-2: fork from a transcript message. The source session keeps its completed fixture task; the
  // forked session gets the branch up to the clicked message, and its follow-up model request is built
  // from the native projection instead of the later transcript rows.
  const forkEligible=await page.evaluate(()=>AgentUi.state.messages.filter(m=>['user','agent'].includes(m.role)&&!m.meta&&!m.interrupted&&String(m.text||'').trim()&&/^[a-f0-9]{32}$/.test(String(m.id||''))).length);
  assert(forkEligible>=2,'the completed fixture task has forkable user and assistant rows');
  assert.equal(await page.locator('[data-agent-fork]').count(),forkEligible,'fork appears only on eligible user/assistant rows');
  assert.equal(await page.locator('[data-agent-result] [data-agent-fork]').count(),0,'result cards never offer a fork');
  assert.equal(await page.locator('.agent-card [data-agent-fork]').count(),0,'tool cards never offer a fork');
  const forked=await page.evaluate(()=>({sessionId:AgentUi.state.sessionId,entryId:AgentUi.state.messages.find(m=>m.role==='user').id,goal:AgentUi.state.messages.find(m=>m.role==='user').text}));
  await page.locator('[data-agent-sessions]').click();await page.locator('#agent-history').waitFor({state:'visible'});
  await page.locator(`[data-agent-fork="${forked.entryId}"]`).click();
  await page.waitForFunction(sessionId=>AgentUi.state.sessionId!==sessionId,forked.sessionId);
  await page.waitForFunction(()=>$('#toasts').innerText.includes('已从这条消息分叉'));
  const forkedState=await page.evaluate(()=>({sessionId:AgentUi.state.sessionId,messages:AgentUi.state.messages.map(m=>({id:m.id,role:m.role,text:m.text})),sessions:AgentUi.state.sessions.map(s=>({id:s.id,title:s.title,messages:(s.messages||[]).length,history:(s.history||[]).length})),history:AgentUi.state.history.map(t=>({role:t.role,content:t.content})),draft:AgentUi.state.draft,watch:AgentUi.state.watch,recovery:AgentUi.state.recovery,usage:AgentUi.state.usage,queue:AgentUi.state.queue,historyOpen:AgentUi.state.historyOpen}));
  assert.notEqual(forkedState.sessionId,forked.sessionId,'fork switches to a new session');
  assert.equal(forkedState.messages.length,1,'the branch is truncated at the clicked user message');
  assert.deepEqual(forkedState.messages.map(m=>({role:m.role,text:m.text})),[{role:'user',text:forked.goal}]);
  assert.equal(forkedState.messages[0].id,forked.entryId,'the truncated row keeps the rendered message id');
  assert(forkedState.sessions.some(row=>row.id===forked.sessionId),'the source session is kept in the session list');
  assert(forkedState.sessions.some(row=>row.id===forkedState.sessionId),'the forked session is added to the session list');
  assert.deepEqual(forkedState.history,[{role:'user',content:forked.goal}],'model history is the native projection of the branch');
  assert.equal(forkedState.watch,'');assert.equal(forkedState.recovery,null);assert.equal(forkedState.draft,'');assert.deepEqual(forkedState.queue,[]);assert.equal(forkedState.usage,null);
  assert.equal(forkedState.historyOpen,false,'fork closes the session drawer');
  const forkNative=await page.evaluate(async({sourceId,newId})=>({tree:await native('agentSessionTree',{sessionId:newId}),project:await native('agentSessionProject',{sessionId:newId}),sourceTree:await native('agentSessionTree',{sessionId:sourceId}),sourceProject:await native('agentSessionProject',{sessionId:sourceId})}),{sourceId:forked.sessionId,newId:forkedState.sessionId});
  assert.equal(forkNative.tree.sessionId,forkedState.sessionId);
  assert(forkNative.tree.entries.length>=1&&forkNative.tree.entries.length<=forkNative.sourceTree.entries.length,'the forked tree is a branch of the source tree');
  assert.deepEqual(forkNative.project.messages.map(m=>({role:m.role,content:m.content})),[{role:'user',content:forked.goal}],'the native projection is exactly the branch messages');
  assert(forkNative.sourceProject.messages.some(m=>m.role==='assistant'&&String(m.content||'').includes('这段笔记会补充到所选资料。')),'the source session keeps the later assistant content');
  mode='hold';const forkRequests=requests.length,forkTasksBefore=(await page.evaluate(async()=>native('agentTaskList'))).length;
  await send('续写接下来的情节');await page.waitForFunction(()=>!!AgentUi.state.job?.id);
  await waitFixture(()=>requests.length===forkRequests+1,'forked follow-up request');
  const followupText=requests[forkRequests].messages.map(message=>String(message.content||'')).join('\n');
  assert(followupText.includes(forked.goal),'the follow-up request history carries the forked branch text');
  assert(!followupText.includes('这段笔记会补充到所选资料。'),'the follow-up request history excludes the later assistant reply');
  assert(!followupText.includes('APPROVED_WORKFLOW_NOTE'),'the follow-up request history excludes the later tool result');
  releaseHeld();await idle();
  const forkTasks=await page.evaluate(async()=>native('agentTaskList'));
  assert.equal(forkTasks.length,forkTasksBefore+1,'the follow-up starts exactly one new task');
  assert.equal(forkTasks.filter(task=>task.sessionId===forkedState.sessionId).length,1,'the forked session owns one task');
  assert.equal(forkTasks.filter(task=>task.sessionId===forked.sessionId).length,1,'the source session keeps its original task');
  console.log('PASS: fork from the first user message truncates the branch, keeps both sessions, matches the native tree/projection and sends only the forked history in one new task.');

  // M1-1: discovered SKILL.md directories appear in the skill picker after the native reload action,
  // and `/skill:name rest` validates the skill, keeps only the rest as the goal and sends the
  // progressive-disclosure instruction instead of the raw slash text.
  const skillDir=path.join(profile,'.agents','skills','workflow-native-skill');
  fs.mkdirSync(skillDir,{recursive:true});fs.writeFileSync(path.join(skillDir,'SKILL.md'),'---\nname: workflow-native-skill\ndescription: Workflow fixture for a discovered skill.\n---\n1. Follow the workflow fixture instructions.\n');
  await page.locator('#agent-advanced>summary').click();await page.waitForFunction(()=>$('#agent-advanced')?.open===true);
  await page.locator('[data-agent-skills-refresh]').click();
  await page.waitForFunction(()=>[...document.querySelectorAll('#agent-skill option')].some(option=>option.value==='workflow-native-skill'));
  assert.equal(await page.evaluate(()=>document.querySelector('#agent-skill optgroup')?.label),'已发现的技能','discovered skills are grouped in the picker');
  assert.match(await page.evaluate(()=>document.querySelector('#agent-skill option[value="workflow-native-skill"]').title),/Workflow fixture for a discovered skill/,'the discovered skill description is the option title');
  mode='hold';const skillStart=requests.length;const skillTasksBefore=(await page.evaluate(async()=>native('agentTaskList'))).length;
  await page.locator('#agent-input').fill('/skill:workflow-native-skill 执行技能里的步骤');await page.locator('.agent-send').click();
  await page.waitForFunction(()=>!!AgentUi.state.job?.id);
  await waitFixture(()=>requests.length===skillStart+1,'skill command model request');
  const skillText=requests[skillStart].messages.map(message=>String(message.content||'')).join('\n');
  assert(skillText.includes('[Skill: workflow-native-skill] Use load_skill to load the full instructions before acting.'),'the skill command sends the progressive-disclosure instruction');
  assert(skillText.includes('执行技能里的步骤'),'the skill command keeps the rest of the line as the goal');
  assert(!skillText.includes('/skill:'),'the raw slash command is never sent to the model');
  assert.equal(await page.evaluate(()=>AgentUi.state.messages.some(message=>String(message.text||'').includes('/skill:'))),false,'the raw slash command never becomes a transcript row');
  releaseHeld();await idle();
  assert.equal((await page.evaluate(async()=>native('agentTaskList'))).length,skillTasksBefore+1,'the skill command starts exactly one task');
  fs.rmSync(path.join(profile,'.agents'),{recursive:true,force:true});
  await page.evaluate(()=>document.querySelector('[data-agent-skills-refresh]')?.click());
  await page.waitForFunction(()=>![...document.querySelectorAll('#agent-skill option')].some(option=>option.value==='workflow-native-skill'));
  console.log('PASS: discovered skills load through the picker refresh action and /skill:name validates then starts one task with the load_skill instruction.');

  // M1-3: in-place branch tree. The dialog lists native session entries in pre-order with the active
  // leaf; switching to the first user message rebuilds the visible transcript and model history from
  // the native projection, and the next task continues on that branch. Renaming persists in the
  // native session header and the UI session row.
  await page.locator('[data-agent-tree]').click();
  await page.waitForFunction(()=>$('#small-dialog').open&&$('#small-dialog').innerText.includes('会话分支'));
  assert(await page.locator('[data-branch-type="message"]').count()>=3,'the branch tree lists message entries');
  assert.match(await page.locator('#small-dialog').innerText(),/当前分支/,'the dialog header shows the current branch and entry count');
  const branchPreviews=await page.locator('[data-branch-type="message"] .agent-branch-preview').allInnerTexts();
  assert(branchPreviews.some(text=>text.includes(forked.goal)),'the tree preview shows the forked branch message');
  assert(branchPreviews.some(text=>text.includes('续写接下来的情节')),'the tree preview shows the later follow-up message');
  assert(branchPreviews.some(text=>text.includes('执行技能里的步骤')),'the tree preview shows the skill-command message');
  assert.equal(await page.locator('[data-branch-current="true"]').count(),1,'exactly one entry is the active leaf');
  assert.equal(await page.locator('[data-branch-type="message"][data-branch-current="true"]').count(),1,'the active leaf marker sits on a message row');
  const branchFirst=page.locator('[data-branch-type="message"]').first();
  const switchedLeaf=await branchFirst.getAttribute('data-branch-id');
  assert((await branchFirst.locator('.agent-branch-preview').innerText()).includes(forked.goal),'the first tree message is the forked branch root');
  await branchFirst.locator('[data-agent-branch-switch]').click();
  await page.waitForFunction(()=>!$('#small-dialog').open);
  await page.waitForFunction(leafId=>AgentUi.state.messages.length===1&&AgentUi.state.messages[0].id===leafId,switchedLeaf);
  const switchedState=await page.evaluate(()=>({messages:AgentUi.state.messages.map(m=>({id:m.id,role:m.role,text:m.text})),history:AgentUi.state.history.map(t=>({role:t.role,content:t.content})),draft:AgentUi.state.draft,watch:AgentUi.state.watch,recovery:AgentUi.state.recovery,queue:AgentUi.state.queue,usage:AgentUi.state.usage}));
  assert.deepEqual(switchedState.messages.map(m=>({role:m.role,text:m.text})),[{role:'user',text:forked.goal}],'switching rebuilds the transcript from the switched branch');
  assert.deepEqual(switchedState.history,[{role:'user',content:forked.goal}],'the model history follows the switched branch');
  assert.equal(switchedState.draft,'');assert.equal(switchedState.watch,'');assert.equal(switchedState.recovery,null);assert.deepEqual(switchedState.queue,[]);assert.equal(switchedState.usage,null);
  const switchedTree=await page.evaluate(async()=>native('agentSessionTree',{sessionId:AgentUi.state.sessionId}));
  assert.equal(switchedTree.leaf,switchedLeaf,'the native session leaf matches the switched message');
  mode='hold';const branchRequests=requests.length,branchTasksBefore=(await page.evaluate(async()=>native('agentTaskList'))).length;
  await send('继续这个分支');await page.waitForFunction(()=>!!AgentUi.state.job?.id);
  await page.waitForFunction(()=>document.querySelector('[data-agent-tree]')?.disabled===true);
  assert(await page.locator('[data-agent-tree]').isDisabled(),'the branch-tree entry point is disabled while a task runs');
  await waitFixture(()=>requests.length===branchRequests+1,'switched branch follow-up request');
  const branchRequestText=requests[branchRequests].messages.map(message=>String(message.content||'')).join('\n');
  assert(branchRequestText.includes(forked.goal),'the follow-up request history carries the switched branch text');
  assert(!branchRequestText.includes('续写接下来的情节'),'the follow-up request history excludes the removed follow-up message');
  assert(!branchRequestText.includes('执行技能里的步骤'),'the follow-up request history excludes the removed skill-command message');
  releaseHeld();await idle();
  assert.equal((await page.evaluate(async()=>native('agentTaskList'))).length,branchTasksBefore+1,'the switched branch starts exactly one new task');
  await page.locator('[data-agent-tree]').click();
  await page.waitForFunction(()=>$('#small-dialog').open&&$('#small-dialog').innerText.includes('会话分支'));
  await page.locator('[data-agent-tree-rename]').click();
  await page.locator('#agent-tree-title-input').waitFor({state:'visible'});
  await page.locator('#agent-tree-title-input').fill('分支测试会话');
  await page.locator('#agent-tree-rename-form [type=submit]').click();
  await page.waitForFunction(()=>AgentUi.state.titleOverride==='分支测试会话');
  const renamedTree=await page.evaluate(async()=>native('agentSessionTree',{sessionId:AgentUi.state.sessionId}));
  assert.equal(renamedTree.title,'分支测试会话','the native session header title is renamed');
  await page.evaluate(()=>closeSmall());await page.waitForFunction(()=>!$('#small-dialog').open);
  await page.locator('[data-agent-sessions]').click();await page.waitForFunction(()=>AgentUi.state.historyOpen);
  assert.match(await page.locator('.agent-history-row.is-current').innerText(),/分支测试会话/,'the session list shows the renamed title');
  await page.keyboard.press('Escape');await page.waitForFunction(()=>!AgentUi.state.historyOpen);
  console.log('PASS: branch tree previews the active leaf, switching rebuilds transcript/history, the next task continues on that branch, and rename persists.');

  // M3-1: declarative plugins. A plugin under the test library root contributes one skill and one
  // prompt template; reloading picks both up, /prompt expands the template into the composer without
  // sending, and removing the plugin is picked up on the next reload.
  const pluginRoot=path.join(profile,'.kok','plugins','demo-pack');
  fs.mkdirSync(path.join(pluginRoot,'skills','demo-plugin-skill'),{recursive:true});
  fs.mkdirSync(path.join(pluginRoot,'prompts'),{recursive:true});
  fs.writeFileSync(path.join(pluginRoot,'plugin.json'),JSON.stringify({name:'demo-pack',version:'1.0.0',description:'Demo declarative plugin',skills:['skills/'],prompts:['prompts/']}));
  fs.writeFileSync(path.join(pluginRoot,'skills','demo-plugin-skill','SKILL.md'),'---\nname: demo-plugin-skill\ndescription: Plugin skill fixture.\n---\n1. Follow the plugin skill fixture instructions.\n');
  fs.writeFileSync(path.join(pluginRoot,'prompts','write-notes.md'),'---\nname: write-notes\ndescription: Write notes from the argument\n---\nWrite concise notes about: $ARGUMENTS\n\nKeep the facts.\n');
  if(!await page.evaluate(()=>$('#agent-advanced')?.open===true)){await page.locator('#agent-advanced>summary').click();await page.waitForFunction(()=>$('#agent-advanced')?.open===true);}
  await page.locator('[data-agent-plugins]').click();
  await page.waitForFunction(()=>$('#small-dialog').open&&$('#small-dialog').innerText.includes('扩展包'));
  await page.locator('#agent-plugins-reload').click();
  await page.waitForFunction(()=>$('#small-dialog').innerText.includes('demo-pack')&&$('#small-dialog').innerText.includes('write-notes'));
  const pluginDialog=await page.locator('#small-dialog').innerText();
  assert.match(pluginDialog,/demo-pack · 1\.0\.0/,'the plugin name and version are shown');
  assert.match(pluginDialog,/Demo declarative plugin/,'the plugin description is shown');
  assert.match(pluginDialog,/技能 1 · 提示模板 1/,'the plugin lists its skill and prompt counts');
  assert.match(pluginDialog,/Write notes from the argument/,'the prompt template description is shown');
  assert.match(pluginDialog,/已安装扩展包 1/,'the dialog summarizes the installed plugin');
  assert.match(await page.locator('#toasts').innerText(),/扩展包已重新加载/,'reloading plugins is confirmed');
  await page.evaluate(()=>closeSmall());await page.waitForFunction(()=>!$('#small-dialog').open);
  await page.waitForFunction(()=>[...document.querySelectorAll('#agent-skill option')].some(option=>option.value==='demo-plugin-skill'),'plugin skills appear in the native skills picker');
  const pluginTasksBefore=(await page.evaluate(async()=>native('agentTaskList'))).length;
  await page.locator('#agent-input').fill('/prompt:write-notes Hello notes');await page.locator('.agent-send[type=submit]').click();
  await page.waitForFunction(()=>$('#agent-input').value.includes('Write concise notes about: Hello notes'));
  const expandedPrompt=await page.locator('#agent-input').inputValue();
  assert.match(expandedPrompt,/^Write concise notes about: Hello notes/,'$ARGUMENTS is replaced by the command arguments');
  assert.match(expandedPrompt,/Keep the facts\./,'the template body is preserved');
  assert(!expandedPrompt.includes('/prompt:'),'the raw /prompt command is never stored');
  assert.equal(await page.evaluate(()=>AgentUi.state.draft),expandedPrompt,'the composer draft is the expanded template');
  assert.equal(await page.evaluate(()=>AgentUi.state.job),null,'inserting a prompt template never starts a task');
  assert.equal((await page.evaluate(async()=>native('agentTaskList'))).length,pluginTasksBefore,'inserting a prompt template starts no task');
  fs.rmSync(path.join(profile,'.kok','plugins','demo-pack'),{recursive:true,force:true});
  await page.evaluate(()=>document.querySelector('[data-agent-plugins]')?.click());
  await page.waitForFunction(()=>$('#small-dialog').open);
  await page.locator('#agent-plugins-reload').click();
  await page.waitForFunction(()=>!$('#small-dialog').innerText.includes('demo-pack'));
  assert.doesNotMatch(await page.locator('#small-dialog').innerText(),/write-notes/,'removed plugins disappear after reload');
  await page.evaluate(()=>closeSmall());await page.waitForFunction(()=>!$('#small-dialog').open);
  await page.waitForFunction(()=>![...document.querySelectorAll('#agent-skill option')].some(option=>option.value==='demo-plugin-skill'),'removed plugin skills disappear from the picker');
  console.log('PASS: plugins reload, contribute skills and prompt templates, /prompt expands into the composer without sending, and removal is picked up.');

  await fresh();mode='hold';const heldStart=requests.length;await send('帮我规划一个创作项目');
  await page.waitForFunction(()=>!!AgentUi.state.job?.id);await page.waitForFunction(()=>$('#agent-input').placeholder==='输入会插入当前任务…');
  const taskId=await page.evaluate(()=>AgentUi.state.job.id);
  await waitFixture(()=>requests.length===heldStart+1,'first model request');assert.equal(held.length,1);
  // Enter while busy steers the running round through the native queue; nothing is written locally.
  await page.locator('#agent-input').fill('下一步整理章节');assert(await page.locator('.agent-send').isVisible());assert(await page.locator('[data-agent-queue]').isVisible());
  await page.locator('.agent-send').click();
  await page.waitForFunction(()=>$('#agent-input').value==='');await openQueue(1);
  await page.locator('.agent-queued>summary').click();await page.waitForFunction(()=>document.querySelector('.agent-queued')?.open===true);
  assert.match(await page.locator('.agent-queued').innerText(),/下一步整理章节/);assert.match(await page.locator('.agent-queued').innerText(),/下回合插入/);
  assert.equal(await page.locator('[data-agent-edit-queued]').count(),0,'native queue rows are read-only');assert.equal(await page.locator('[data-agent-remove-queued]').count(),0,'native queue rows cannot be removed');
  assert.deepEqual(await page.evaluate(()=>AgentUi.state.queue),[],'the running composer never fills the local queue');
  const queuedSteer=await taskEvents(taskId,'input.queued');assert.equal(queuedSteer.length,1);assert.equal(queuedSteer[0].data.mode,'steer');
  assert.equal(requests.length,heldStart+1,'steering an in-flight round starts no new model request');
  // The clock button queues a Pi follow-up, which only continues the task when the run would finish.
  await page.locator('#agent-input').fill('稍后制作封面');await page.locator('[data-agent-queue]').click();
  await page.waitForFunction(()=>$('#agent-input').value==='');await openQueue(2);
  assert.match(await page.locator('.agent-queued').innerText(),/稍后制作封面/);assert.match(await page.locator('.agent-queued').innerText(),/完成后跟进/);
  assert.equal(requests.length,heldStart+1,'queued follow-up input never starts a second task');
  // Release round 1: steering is consumed at the next boundary inside the same task.
  releaseHeld();await waitFixture(()=>requests.length===heldStart+2,'second model request');await openQueue(1);
  assert.equal(await page.evaluate(()=>AgentUi.state.watch),taskId);assert.equal(await page.evaluate(()=>AgentUi.state.job.id),taskId);
  assert(requests[heldStart+1].messages.some(message=>String(message.content||'').includes('下一步整理章节')),'steering reaches the next model round of the same task');
  assert(!(await page.locator('.agent-queued').innerText()).includes('下一步整理章节'));
  // Release round 2: the follow-up is consumed at the natural end and the run continues instead of finishing.
  releaseHeld();await waitFixture(()=>requests.length===heldStart+3,'third model request');
  await page.waitForFunction(()=>{const drawer=document.querySelector('.agent-queued');return !drawer||drawer.hidden;});
  assert(requests[heldStart+2].messages.some(message=>String(message.content||'').includes('稍后制作封面')),'the follow-up continues the same task');
  assert.equal(await page.evaluate(()=>AgentUi.state.job.id),taskId);
  const consumedModes=(await taskEvents(taskId,'input.consumed')).map(event=>event.data.mode);assert.deepEqual(consumedModes,['steer','follow']);
  assert.equal(await page.evaluate(async()=>(await native('agentTaskList')).filter(task=>task.sessionId===AgentUi.state.sessionId).length),1,'one task, not a queued successor');

  // A rejected mid-run submit must not replace newer composer input. Steering now hits the native
  // queue first; its older-binary fallback (agentTaskAdjust) is rejected too so the submit fully fails.
  await page.evaluate(()=>{
   Kok.extend('core','test-workflow:input-failure','native',previous=>async function(action,...args){
    if(action==='agentTaskSteer'&&window.rejectNextSteer){window.rejectNextSteer=false;return new Promise((resolve,reject)=>{window.rejectSteer=()=>reject(Error('TEST_STEER_REJECTED'));});}
    if(action==='agentTaskAdjust'&&window.rejectSteerFallback){window.rejectSteerFallback=false;throw Error('TEST_STEER_FALLBACK_REJECTED');}
    return previous.call(this,action,...args);
   });window.rejectNextSteer=true;window.rejectSteerFallback=true;
  });
  await page.locator('#agent-input').fill('失败时要保留的补充要求');await page.locator('.agent-send').click();
  await page.waitForFunction(()=>AgentUi.state.inputFlight);assert(await page.locator('.agent-send').isDisabled());
  await page.locator('#agent-input').fill('发送过程中继续输入的新要求');
  await page.evaluate(()=>window.rejectSteer());await page.waitForFunction(()=>!AgentUi.state.inputFlight);
  assert.equal(await page.locator('#agent-input').inputValue(),'发送过程中继续输入的新要求','a failed request cannot replace newer input');
  assert.equal(await page.evaluate(()=>AgentUi.state.draft),'发送过程中继续输入的新要求');
  await page.evaluate(()=>{window.rejectNextSteer=true;window.rejectSteerFallback=true;});await page.locator('.agent-send').click();await page.waitForFunction(()=>AgentUi.state.inputFlight);
  await page.evaluate(()=>window.rejectSteer());await page.waitForFunction(()=>!AgentUi.state.inputFlight);
  assert.equal(await page.locator('#agent-input').inputValue(),'发送过程中继续输入的新要求','failure preserves an unchanged draft too');
  await page.locator('[data-agent-sessions]').click();await page.locator('#agent-history').waitFor({state:'visible'});assert(await page.locator('#agent-history').isVisible());
  assert(await page.locator('.agent-history-busy').isVisible());assert(await page.locator('[data-agent-history-new]').isDisabled());
  assert.equal(await page.locator('[data-agent-sessions]').isDisabled(),false,'history remains inspectable during work');
  await page.keyboard.press('Escape');assert.equal(await page.locator('#agent-history').isVisible(),false);
  // Stop hands unconsumed native queue items back to the composer, exactly once across pulls.
  await page.locator('#agent-input').fill('停止后放回的文字');await page.locator('[data-agent-queue]').click();
  await page.waitForFunction(()=>$('#agent-input').value==='');await openQueue(1);
  await page.screenshot({path:path.join(output,'running-queue.png'),animations:'disabled'});
  await page.locator('#agent-input').press('Escape');await idle();await page.waitForFunction(async id=>!(await native('agentTaskRead',{taskId:id})).running,taskId);held.splice(0).forEach(release=>release());
  await page.waitForFunction(()=>$('#agent-input').value==='停止后放回的文字');
  assert.equal(await page.evaluate(()=>AgentUi.state.draft),'停止后放回的文字','stop restores unconsumed native input to the composer');
  assert.equal(await page.evaluate(()=>AgentUi.state.queue.length),0);
  assert.equal(await page.locator('.agent-queued').isVisible(),false);
  await page.waitForFunction(()=>$('#toasts').innerText.includes('因停止未执行'));
  assert((await page.locator('#toasts').innerText()).includes('1 条输入已放回输入框'));
  const stoppedSnapshot=await page.evaluate(async id=>native('agentTaskRead',{taskId:id}),taskId);
  assert.equal(stoppedSnapshot.running,false);assert.deepEqual(stoppedSnapshot.queue,[]);assert.equal(stoppedSnapshot.restoredInputs.length,1);assert.equal(stoppedSnapshot.restoredInputs[0].text,'停止后放回的文字');
  const restoredEvent=(await taskEvents(taskId,'input.restored')).at(-1);assert.equal(restoredEvent.data.reason,'aborted');
  await page.evaluate(()=>Kok.emit('libraryChanged'));await page.waitForTimeout(200);
  assert.equal(await page.locator('#agent-input').inputValue(),'停止后放回的文字','repeated pulls never duplicate restored input');
  const stoppedRequests=requests.length;await page.waitForTimeout(150);assert.equal(requests.length,stoppedRequests);
  console.log('PASS: native steering/follow-up queues are read-only, consumed inside the same task, failed submits keep newer input and stop returns queued text to the composer once.');

  await fresh();await page.locator('#agent-input').fill('需要保留的小说草稿');await page.evaluate(()=>AgentUi.save());
  const draftSession=await page.evaluate(()=>AgentUi.state.sessionId);
  await fresh();await page.locator('#agent-input').fill('另一个未发送的想法');
  const beforeHistory=requests.length;await page.locator('[data-agent-sessions]').click();
  await page.locator('#agent-history-search').fill('小说草稿');assert.equal(await page.locator('.agent-history-row').count(),1);
  await page.evaluate(()=>{window.historyInput=$('#agent-history-search');$('#agent-history-search').setSelectionRange(1,3);AgentUi.render({scroll:false});});
  assert(await page.evaluate(()=>$('#agent-history-search')===window.historyInput&&document.activeElement===window.historyInput&&window.historyInput.selectionStart===1));
  await page.screenshot({path:path.join(output,'history-search.png'),animations:'disabled'});
  await page.locator(`[data-agent-resume="${draftSession}"]`).click();
  await page.waitForFunction(id=>AgentUi.state.sessionId===id,draftSession);
  assert.equal(await page.locator('#agent-input').inputValue(),'需要保留的小说草稿');assert.equal(requests.length,beforeHistory);
  assert.equal(await page.locator('#agent-history').isVisible(),false);
  await page.locator('[data-agent-sessions]').click();await page.locator(`[data-agent-delete-session="${draftSession}"]`).click();
  await page.locator('#agent-delete-session-confirm').click();await page.waitForFunction(()=>!$('#small-dialog').open);
  assert.equal(await page.evaluate(()=>AgentUi.state.watch),'');assert.equal(await page.evaluate(()=>AgentUi.state.recovery),null);
  assert.equal(await page.evaluate(()=>AgentUi.state.handoff),null);assert.equal(await page.evaluate(itemId=>!!byId(itemId),itemId),true);
  await page.keyboard.press('Escape');
  console.log('PASS: searchable session drawer, stable focus, draft restore without requests and deletion with library preservation.');

  // P1 creative projects: 我的作品 detail -> 继续创作 opens a fresh Agent session pinned to the
  // project; agentTaskStart carries projectId plus the project materials as the default scope, the
  // task is a single task in that session, the project artifact appears in the artifact panel, and
  // the project chip survives 新建会话 until it is cleared with ×.
  const projectSeed=await page.evaluate(async({memberId})=>{
   const artifact=await native('createDocument',{name:'项目成果草稿',format:'md',collection:'',text:'# 项目成果\n\nPROJECT_ARTIFACT_TEXT'});
   const id=crypto.randomUUID().replace(/-/g,''),now=new Date().toISOString();
   const value={id,kind:'project',name:'重逢创作项目',goal:'完成重逢故事的终章',requirements:'保持第一人称；结尾留白。',status:'active',created:now,updated:now,view:'grid',positions:{},members:[memberId],artifacts:[artifact.id],sessions:[]};
   window.__projectSave=native('workspaceSave',{kind:'space',id,value});
   return {id,artifactId:artifact.id,now};
  },{memberId:itemId});
  // workspaceSave returns a proposed operation; the UI asks for confirmation before it persists.
  await page.waitForSelector('#operation-apply');
  await page.locator('#operation-apply').click();
  await page.waitForFunction(()=>!$('#small-dialog').open);
  await page.evaluate(()=>window.__projectSave);
  await page.evaluate(async()=>{await globalThis.Warehouse.load();await refresh();});
  await page.evaluate(()=>navigate('works'));await page.waitForFunction(()=>state.route==='works');
  await page.waitForSelector(`[data-project-card="${projectSeed.id}"]`);
  await page.locator(`[data-project-open="${projectSeed.id}"]`).click();
  await page.waitForSelector('#project-detail');
  const seededDetail=await page.locator('#project-detail').innerText();
  assert.match(seededDetail,/完成重逢故事的终章/,'the project detail shows the project goal');
  assert.match(seededDetail,/保持第一人称/,'the project detail shows the project requirements');
  assert.match(seededDetail,/项目成果草稿/,'the project detail lists the project artifact');
  const sessionBeforeProject=await page.evaluate(()=>AgentUi.state.sessionId);
  await page.locator(`#project-detail [data-project-continue="${projectSeed.id}"]`).click();
  await page.waitForFunction(()=>state.route==='agent');
  await page.waitForFunction(()=>{const chip=document.querySelector('[data-agent-project]');return chip&&!chip.hidden&&chip.innerText.includes('重逢创作项目');});
  assert.notEqual(await page.evaluate(()=>AgentUi.state.sessionId),sessionBeforeProject,'继续创作 opens a fresh session for the project');
  assert.equal(await page.evaluate(()=>AgentUi.state.scope),'all');
  assert.equal(await page.evaluate(()=>AgentUi.state.context.length),0);
  await page.evaluate(()=>{window.__projectStarts=[];Kok.extend('core','test-workflow:project-probe','native',previous=>async function(action,...args){if(action==='agentTaskStart')window.__projectStarts.push(JSON.parse(JSON.stringify(args&&args[0]||{})));return previous.call(this,action,...args);});});
  mode='hold';const projectRequests=requests.length,projectTasksBefore=(await page.evaluate(async()=>native('agentTaskList'))).length;
  await send('继续完成终章');await page.waitForFunction(()=>!!AgentUi.state.job?.id);
  await waitFixture(()=>requests.length===projectRequests+1,'project model request');
  const projectStarts=await page.evaluate(()=>window.__projectStarts);
  assert.equal(projectStarts.length,1,'one project task start is issued');
  assert.equal(projectStarts[0].projectId,projectSeed.id,'agentTaskStart carries the creative project id');
  assert.deepEqual(projectStarts[0].scope,{mode:'selected',ids:[projectSeed.artifactId,itemId],collection:''},'existing project outputs and materials become the default task scope');
  const projectGoalText=requests[projectRequests].messages.map(message=>String(message.content||'')).join('\n');
  assert(projectGoalText.includes('完成重逢故事的终章'),'the host project brief reaches the model request');
  const projectSession=await page.evaluate(()=>AgentUi.state.sessionId);
  const linkedProject=await page.evaluate(async id=>{const data=await native('workspaceState');const project=(data.spaces||[]).find(space=>space.id===id);return project?{sessions:project.sessions,artifacts:project.artifacts}:null;},projectSeed.id);
  assert(linkedProject&&linkedProject.sessions.includes(projectSession),'starting the task links the session into the project');
  releaseHeld();await idle();
  assert.equal((await page.evaluate(async()=>native('agentTaskList'))).length,projectTasksBefore+1,'the project task starts exactly one task');
  const projectArtifacts=await page.evaluate(async id=>{const data=await native('workspaceState');const project=(data.spaces||[]).find(space=>space.id===id);return project.artifacts;},projectSeed.id);
  assert(projectArtifacts.includes(projectSeed.artifactId),'the project keeps its artifact list');
  assert.equal(await page.locator('[data-agent-artifacts]').isVisible(),true,'project artifacts make the artifact panel available');
  await page.locator('[data-agent-artifacts]').click();
  await page.waitForSelector('.pi-artifacts:not([hidden])');
  assert.equal(await page.locator(`[data-pi-artifact="${projectSeed.artifactId}"]`).count(),1,'the artifact panel lists the project artifact');
  assert.match(await page.locator('.pi-artifacts').innerText(),/项目成果草稿/);
  await page.locator('[data-pi-artifact-close]').click();
  await page.evaluate(()=>document.querySelector('[data-agent-new]')?.click());
  await page.waitForFunction(()=>!AgentUi.state.job);
  await page.waitForFunction(()=>{const chip=document.querySelector('[data-agent-project]');return chip&&!chip.hidden&&chip.innerText.includes('重逢创作项目');});
  assert.equal(await page.evaluate(()=>AgentUi.state.project?.id),projectSeed.id,'新建会话 keeps the project chip');
  await page.evaluate(()=>document.querySelector('[data-agent-project-clear]')?.click());
  await page.waitForFunction(()=>{const chip=document.querySelector('[data-agent-project]');return !chip||chip.hidden;});
  assert.equal(await page.evaluate(()=>AgentUi.state.project),null,'× clears the project chip');
  console.log('PASS: creative project detail continues into one Agent session with projectId and auto project scope, project artifacts are listed, and the chip survives 新建会话 until cleared.');

  await page.evaluate(()=>{
   for(let n=0;n<12;n++)AgentUi.state.messages.push({id:uid(),role:'agent',text:'阅读位置测试 '+n+'\n'+('内容段落。\n'.repeat(12))});
   AgentUi.render();$('#agent-stream').scrollTop=0;
  });
  await page.locator('#agent-input').fill('继续书写时保留光标');
  await page.evaluate(()=>{window.stableAgentInput=$('#agent-input');$('#agent-input').setSelectionRange(2,5);AgentUi.render({scroll:false});});
  assert(await page.evaluate(()=>$('#agent-input')===window.stableAgentInput&&$('#agent-input').selectionStart===2&&$('#agent-input').selectionEnd===5&&$('#agent-stream').scrollTop<4));
  await fresh();mode='outline';await send('把重逢的故事整理成三章大纲，保存为草稿');
  await page.waitForSelector('[data-agent-run]');await page.locator('[data-agent-run]').click();await idle();
  assert(await page.locator('.agent-result-artifact [data-agent-open-item]').isVisible());
  assert.match(await page.locator('.agent-result-artifact h3').innerText(),/重逢/);
  assert.equal(await page.locator('.agent-completed-preview').evaluate(node=>node.open),false,'completed creation previews collapse so the result remains primary');
  await page.locator('.agent-completed-preview>summary').click();assert(await page.locator('.agent-completed-preview .agent-rich-text h3').first().isVisible());
  await page.locator('.agent-completed-preview>summary').click();
  await page.evaluate(()=>{document.querySelectorAll('#toasts .toast').forEach(toast=>toast.remove());});
  for(const [theme,width,height] of [['dark',1440,960],['light',1440,960],['dark',650,620],['light',500,600]]){
   await page.setViewportSize({width,height});await page.evaluate(async theme=>{await setting({theme,fontFamily:'system'});applyTheme();await applyFont();},theme);
   await page.locator('[data-agent-sessions]').click();
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
   const historyBox=await page.locator('#agent-history').boundingBox();assert(historyBox.x>=0&&historyBox.x+historyBox.width<=width);
   await page.screenshot({path:path.join(output,`workspace-${theme}-${width}.png`),animations:'disabled'});
   await page.keyboard.press('Escape');
  }
  await page.emulateMedia({reducedMotion:'reduce'});await page.locator('[data-agent-sessions]').click();
  assert.equal(await page.locator('#agent-history').evaluate(node=>node.getAnimations().filter(animation=>animation.playState==='running').length),0);
  await page.evaluate(async()=>{await setting({uiLanguage:'en'});I18n.applyLanguage('en');AgentUi.render({scroll:false});});
  assert.equal(await page.locator('#agent-history-search').getAttribute('placeholder'),'Search sessions');
  assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);
  console.log('PASS: history reading position, composer caret, 500–1440 px light/dark, reduced motion and English; no remote browser traffic.');
 }catch(error){if(page)await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});throw error;}
 finally{held.splice(0).forEach(release=>release());server.close();if(browser)await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

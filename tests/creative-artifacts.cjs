'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http');
const {chromium}=require('./browser.cjs'),F=require('./agent-fixture.cjs');
(async()=>{
 let browser,page;const requests=[],errors=[],remote=[];const output=path.resolve('test-output/creative-artifacts');fs.mkdirSync(output,{recursive:true});
 const model=http.createServer((req,res)=>{let raw='';req.on('data',chunk=>raw+=chunk);req.on('end',()=>{const request=JSON.parse(raw);requests.push(request);res.setHeader('content-type','application/json');res.end(F.payload(request,F.called(request,'create_document')?{reply:'阶段稿已保存。',actions:[],done:true}:{reply:'准备项目的第一份阶段稿。',actions:[{tool:'create_document',args:{name:'项目阶段稿',format:'md',text:'# 阶段稿\n\n保留项目要求，继续完善同一份作品。'}}],done:false},{prompt_tokens:90,completion_tokens:60}));});});await new Promise(resolve=>model.listen(0,'127.0.0.1',resolve));
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');const context=browser.contexts()[0];page=context.pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(20000);page.on('pageerror',error=>errors.push(error.message));
  await context.route(/^https?:\/\//,route=>['app.kok.local','media.kok.local'].includes(new URL(route.request().url()).hostname)?route.continue():(remote.push(route.request().url()),route.abort()));
  await page.waitForFunction(()=>!$('#startup'));await page.setViewportSize({width:1440,height:960});
  const openArtifacts=async()=>{if(!await page.locator('.pi-artifacts').isVisible()){await page.locator('[data-agent-artifacts]').click();}await page.waitForSelector('.pi-artifact-document');};
  await page.evaluate(async endpoint=>{await native('saveAiConfig',{endpoint,model:'creative-artifact-fixture',clearKey:true,automatic:false});await refresh();navigate('agent');await AgentUi.newSession();Object.assign(AgentUi.state,{skill:'',intent:'assist',scope:'all'});AgentUi.render();},`http://127.0.0.1:${model.address().port}/v1`);
  const project=await page.evaluate(()=>{const id=crypto.randomUUID().replace(/-/g,''),now=new Date().toISOString();window.projectSave=native('workspaceSave',{kind:'space',id,value:{id,kind:'project',name:'持续创作项目',goal:'从第一稿持续修改到终稿',requirements:'保留手动内容与既有成果',status:'active',created:now,updated:now,view:'grid',positions:{},members:[],artifacts:[],sessions:[]}});return id;});
  await page.waitForSelector('#operation-apply');await page.locator('#operation-apply').click();await page.evaluate(()=>window.projectSave);await page.evaluate(id=>AgentUi.openProject(id),project);
  await page.locator('#agent-input').fill('为这个项目创建一份阶段稿');await page.locator('.agent-send').click();await page.waitForSelector('[data-agent-run]');await page.locator('[data-agent-run]').click();await page.waitForFunction(()=>!AgentUi.state.job);
  const created=await page.evaluate(()=>{const m=AgentUi.state.messages.find(row=>row.tool==='create_document'&&row.status==='done');return {id:m.result.id,session:AgentUi.state.sessionId,file:byId(m.result.id).path};});
  assert(path.resolve(created.file).startsWith(path.resolve(process.env.KOK_TEST_PROFILE)+path.sep));const bytes=fs.readFileSync(created.file);
  await page.waitForFunction(id=>AgentUi.state.project?.artifacts.includes(id)&&AgentUi.state.sessionArtifacts.some(row=>row.itemId===id),created.id);
  const tree=await page.evaluate(sessionId=>native('agentSessionTree',{sessionId}),created.session),earlier=tree.entries.find(row=>row.type==='message'&&row.role==='user'),artifact=tree.entries.find(row=>row.type==='artifact');assert(earlier&&artifact);
  const fixedRequests=requests.length,fixedTasks=(await page.evaluate(()=>native('agentTaskList'))).length;
  await page.locator('[data-agent-tree]').click();await page.locator(`[data-agent-branch-switch="${earlier.id}"]`).click();await page.waitForFunction(()=>!$('#small-dialog').open);
  assert.equal(await page.evaluate(()=>AgentUi.state.sessionArtifacts.length),0,'earlier ordinary branch excludes future artifact metadata');
  assert(await page.locator('[data-agent-artifacts]').isVisible(),'project artifacts remain available on an earlier branch');
  await openArtifacts();assert.match(await page.locator('.pi-artifact-document').innerText(),/保留项目要求/);assert.equal(await page.locator('.pi-artifact-tabs button').count(),1,'project and session results dedupe the same file');
  await page.evaluate(()=>AgentUi.save());await page.reload();await page.waitForFunction(()=>!$('#startup'));await page.evaluate(()=>navigate('agent'));
  await page.waitForFunction(id=>AgentUi.state.loaded&&AgentUi.state.project?.artifacts.includes(id),created.id);
  assert.equal(await page.evaluate(()=>AgentUi.state.watch),'','restoring the selected branch does not reattach the last finished task');assert.equal(await page.evaluate(()=>AgentUi.state.messages.filter(row=>row.role==='tool').length),0,'old tool cards never replay into a selected branch');
  await openArtifacts();
  await page.locator('[data-agent-tree]').click();await page.waitForSelector(`[data-branch-type=artifact][data-branch-id="${artifact.id}"]`);await page.locator(`[data-agent-branch-switch="${artifact.id}"]`).click();await page.waitForFunction(()=>!$('#small-dialog').open);
  assert(await page.evaluate(id=>AgentUi.state.sessionArtifacts.some(row=>row.itemId===id),created.id),'selecting the saved artifact node restores its durable index');
  await page.locator('[data-agent-tree]').click();await page.locator(`[data-agent-branch-fork="${artifact.id}"]`).click();await page.waitForFunction(session=>AgentUi.state.sessionId!==session,created.session);
  assert(await page.evaluate(id=>AgentUi.state.sessionArtifacts.some(row=>row.itemId===id),created.id),'forking from an artifact keeps its index without tool replay');
  await page.locator('[data-agent-new]').click();await page.waitForFunction(()=>AgentUi.state.messages.length===0);const nextSession=await page.evaluate(()=>AgentUi.state.sessionId);
  assert.equal(await page.evaluate(()=>AgentUi.state.sessionArtifacts.length),0);assert(await page.locator('[data-agent-artifacts]').isVisible(),'a new project session has artifacts before any task exists');
  await page.evaluate(()=>AgentUi.save());await page.reload();await page.waitForFunction(()=>!$('#startup'));await page.evaluate(()=>navigate('agent'));await page.waitForFunction(id=>AgentUi.state.project?.artifacts.includes(id),created.id);assert.equal(await page.evaluate(()=>AgentUi.state.sessionId),nextSession);
  await openArtifacts();await page.screenshot({path:path.join(output,'project-across-sessions.png'),animations:'disabled'});
  assert.equal(requests.length,fixedRequests);assert.equal((await page.evaluate(()=>native('agentTaskList'))).length,fixedTasks);assert.deepEqual(fs.readFileSync(created.file),bytes);
  console.log('PASS: project artifacts survive early branch selection, reload, artifact-node switch/fork and a new session without tasks, replay or extra model requests.');

  await page.evaluate(()=>{
   Kok.extend('core','test-creative:delay-project','native',previous=>async function(action,...args){const result=await previous.call(this,action,...args);if(action==='workspaceState'&&window.delayProjectRecord){window.delayProjectRecord=false;const old=structuredClone(result);old.spaces.find(row=>row.id===AgentUi.state.project.id).name='迟到的旧项目名称';return new Promise(resolve=>{window.releaseProjectRecord=()=>resolve(old);});}if(action==='workspaceState'&&window.countProjectReply)window.projectReplyCount=(window.projectReplyCount||0)+1;return result;});window.delayProjectRecord=true;Kok.emit('libraryChanged');
  });
  await page.waitForFunction(()=>typeof window.releaseProjectRecord==='function');await page.evaluate(()=>{window.countProjectReply=true;Kok.emit('libraryChanged');});await page.waitForFunction(()=>window.projectReplyCount>=1);
  await page.evaluate(()=>window.releaseProjectRecord());await page.waitForFunction(()=>AgentUi.state.project?.name==='持续创作项目');
  assert.notEqual(await page.evaluate(()=>globalThis.Warehouse?.data?.spaces?.find(row=>row.id===AgentUi.state.project.id)?.name),'迟到的旧项目名称','a stale Agent read does not overwrite the workspace cache');

  await page.evaluate(()=>{
   Kok.extend('core','test-creative:delay-index','native',previous=>async function(action,...args){const result=await previous.call(this,action,...args);if(action==='agentSessionProject'&&window.delayArtifactIndex){window.delayArtifactIndex=false;return new Promise(resolve=>{window.releaseArtifactIndex=()=>resolve(result);});}return result;});window.delayArtifactIndex=true;
  });
  await page.evaluate(id=>{window.delayedRestore=AgentUi.newSession(id);},created.session);await page.waitForFunction(()=>typeof window.releaseArtifactIndex==='function');
  await page.locator('[data-agent-new]').click();await page.locator('[data-agent-project-clear]').click();await page.evaluate(()=>window.releaseArtifactIndex());await page.evaluate(()=>window.delayedRestore);
  assert.equal(await page.evaluate(()=>AgentUi.state.sessionArtifacts.length),0,'late native index cannot enter a different session');assert.equal(await page.locator('[data-agent-artifacts]').isVisible(),false,'clearing the project does not retain another session artifacts');
  assert.equal(requests.length,fixedRequests);assert.deepEqual(fs.readFileSync(created.file),bytes);assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);
  console.log('PASS: late artifact reads stay session-bound; clearing project context removes its artifacts and all navigation preserves files and budgets.');
 }catch(error){if(page)await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});throw error;}finally{model.close();if(browser)await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

'use strict';
// Agent end to end against a local mock model: native function calling, reads run alone, writes wait for the user,
// undo restores, invalid calls get bounded repair rounds, a tools refusal falls back to JSON, nothing leaves the machine.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict'),http=require('node:http'),{chromium}=require('./browser.cjs'),Fixture=require('./agent-fixture.cjs');
(async()=>{
 const root=process.env.KOK_TEST_PROFILE;assert(root);const errors=[],remote=[],requests=[];let browser,page,mode='plan',ids={};
 const server=http.createServer((req,res)=>{let body='';req.on('data',part=>body+=part);req.on('end',()=>{const request=JSON.parse(body);requests.push(body);
  res.setHeader('content-type','application/json');
  if(mode==='no-tools'&&request.tools){res.statusCode=400;res.end(JSON.stringify({error:{message:'tools are not supported'}}));return;}
  if(mode==='invalid'){res.end(Fixture.invalid(request));return;}
  let turn;
  if(mode==='no-tools')turn={reply:'FALLBACK_JSON_REPLY',actions:[],done:true};
  else if(!Fixture.called(request,'read_items'))turn={reply:'先读一下这份笔记。',actions:[{tool:'read_items',args:{ids:[ids.a]},why:'了解内容'}],done:false};
  else turn={reply:'建议加标签，并写一份摘要文档。',actions:[{tool:'add_tags',args:{ids:[ids.a],tags:['秩序']},why:'主题'},{tool:'create_document',args:{name:'Agent 摘要',format:'md',text:'# 摘要\n\n秩序比空间重要。'},why:'保存结论'}],done:true};
  res.end(Fixture.payload(request,turn));});});
 await new Promise(r=>server.listen(0,'127.0.0.1',r));
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');const context=browser.contexts()[0];page=context.pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(20000);page.on('pageerror',e=>errors.push(e.stack||e.message));
  await context.route(/^https?:\/\//,route=>{if(['app.kok.local','media.kok.local'].includes(new URL(route.request().url()).hostname))return route.continue();remote.push(route.request().url());return route.abort();});
  await page.waitForFunction(()=>!$('#startup'));await page.setViewportSize({width:1280,height:860});
  ids=await page.evaluate(async endpoint=>{await native('saveAiConfig',{endpoint,model:'agent-test',apiKey:'',clearKey:true,automatic:false});const a=await native('createDocument',{name:'秩序笔记',collection:'',format:'md',text:'# 秩序\n\nORDER_MATTERS 整理的意义在于重逢。'});await refresh();return{a:a.id};},`http://127.0.0.1:${server.address().port}/v1`);
  const original=await page.evaluate(id=>byId(id).path,ids.a),hash=()=>crypto.createHash('sha256').update(fs.readFileSync(original)).digest('hex'),before=hash();
  const idle=()=>page.waitForFunction(()=>!AgentUi.state.job);
  assert(await page.locator('#main-nav [data-route="agent"]').isVisible(),'Agent is the first navigation entry in Smart mode');
  await page.locator('#main-nav [data-route="agent"]').click();await page.waitForSelector('.agent-empty .agent-starters');
  await page.locator('[data-agent-starter]').first().click();assert.match(await page.locator('#agent-input').inputValue(),/整理/);assert.equal(requests.length,0,'starters only fill the composer');
  await page.locator('#agent-input').fill('给这份笔记打标签并写一份摘要');await page.locator('.agent-send').click();
  await page.waitForSelector('[data-agent-run]');assert.equal(requests.length,2,'one read round then one proposal round');
  const first=JSON.parse(requests[0]),second=JSON.parse(requests[1]);
  assert(first.tools.some(t=>t.function.name==='read_items')&&first.tool_choice==='auto','Agent requests use native function calling by default');
  assert(Fixture.toolResults(second).some(m=>m.tool_call_id.startsWith('call_fixture_')&&m.content.includes('ORDER_MATTERS')),'read results return as tool messages paired with the provider call ID');
  assert.equal(JSON.stringify(second.messages[0]),JSON.stringify(first.messages[0]),'the system prefix stays identical between rounds for provider prompt caching');
  assert(!requests.some(b=>b.includes(root.replace(/\\/g,'\\\\'))||b.includes(original.replace(/\\/g,'\\\\'))),'no local paths are sent to the model');
  assert(await page.locator('.agent-card-read.agent-done').count()===1,'read actions run automatically and are shown');
  assert.deepEqual(await page.evaluate(id=>byId(id).tags||[],ids.a),[],'proposals change nothing before confirmation');
  await page.locator('.agent-card:has-text("添加标签") [data-agent-run]').click();await page.waitForFunction(id=>(byId(id).tags||[]).includes('秩序'),ids.a);
  assert(await page.evaluate(()=>!!AgentUi.state.job),'the task still waits for the remaining proposal');
  await page.locator('.agent-card:has-text("添加标签") [data-agent-undo]').click();await page.waitForFunction(id=>!(byId(id).tags||[]).includes('秩序'),ids.a);
  const count=await page.evaluate(()=>state.items.length);await page.locator('.agent-card:has-text("新建文档") [data-agent-skip]').click();await page.waitForSelector('.agent-skipped');await idle();
  assert.equal(await page.evaluate(()=>state.items.length),count,'skipped writes never run');assert.equal(hash(),before,'original files are unchanged');
  await page.screenshot({path:'test-output/agent-072.png'});
  mode='invalid';const invalidStart=requests.length;await page.locator('[data-agent-new]').click();await page.locator('#agent-input').fill('再试一次');await page.locator('.agent-send').click();await page.waitForSelector('.agent-error');await idle();
  assert.equal(await page.locator('[data-agent-run]').count(),0,'invalid model output shows an error without actions');
  assert.equal(requests.length-invalidStart,3,'an invalid call gets two repair rounds, then the task stops');
  assert(Fixture.toolResults(JSON.parse(requests.at(-1))).some(m=>m.content.startsWith('Error: unknown tool')),'repair rounds return the validation error as a tool result');
  mode='no-tools';const fallbackStart=requests.length;await page.locator('[data-agent-new]').click();await page.locator('#agent-input').fill('接口不支持工具');await page.locator('.agent-send').click();await idle();
  const fallback=requests.slice(fallbackStart).map(b=>JSON.parse(b));
  assert.equal(fallback.length,2,'a tools refusal is retried once with the JSON protocol');assert(fallback[0].tools&&!fallback[1].tools,'the retry no longer sends tools');
  assert((await page.locator('.agent-stream').innerText()).includes('FALLBACK_JSON_REPLY'),'the JSON fallback reply is shown');
  await page.evaluate(async()=>{await native('setAppMode',{mode:'pure'});state.settings.appMode='pure';navigate('agent');});await page.waitForSelector('.agent-off');
  assert.equal(await page.locator('#agent-input').count(),0,'Pure mode shows no composer');
  await page.evaluate(async()=>{await native('setAppMode',{mode:'smart',acknowledged:true});state.settings.appMode='smart';navigate('overview');});
  assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);console.log('PASS: Agent home, native tool calls, stable prefix, automatic reads, confirmed writes, undo while waiting, skip, repair rounds, JSON fallback, Pure mode and no remote or path leakage.');
 }catch(error){console.error('JS errors:',errors);throw error;}finally{server.close();if(browser)await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

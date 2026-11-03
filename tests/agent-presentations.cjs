'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http'),crypto=require('node:crypto');
const {chromium}=require('./browser.cjs'),F=require('./agent-fixture.cjs');
(async()=>{
 const profile=process.env.KOK_TEST_PROFILE;assert(profile);let browser,page,id='',shapeId='',mode='create',oldText='本地资料与作品共用一个工作区',newText='围绕同一份作品继续创作';const errors=[],requests=[],remote=[];
 const slides=[{title:'把想法做成作品',layout:'cover',bullets:['kok · 创作与资料'],notes:'介绍这次演示的主题。'},{title:'从一个清楚的目标开始',layout:'content',bullets:['本地资料与作品共用一个工作区','Agent 在预览中完成后续修改'],notes:'让每页围绕一个主题。'},{title:'创作与阅读',layout:'two_columns',bullets:['创作：生成和修改文档','创作：整理演讲备注','阅读：打开原文与图片','阅读：查看来源和上下文'],notes:'分别解释两个场景。'},{title:'下一步',layout:'section',bullets:['把反馈落在具体页面上']},{title:'作品可以持续完善',layout:'quote',bullets:['一次局部修改，让下一版更接近你的想法。']}];
 const model=http.createServer((req,res)=>{let raw='';req.on('data',chunk=>raw+=chunk);req.on('end',()=>{
  const request=JSON.parse(raw);requests.push(request);let turn;
  if(mode==='create')turn=F.called(request,'create_presentation')?{reply:'五页演示文稿已保存，可以选择具体页面继续修改。',actions:[],done:true}:{reply:'准备五页演示，详细说明放在备注里。',actions:[{tool:'create_presentation',args:{title:'把想法做成作品',theme:'midnight',slides}}],done:false};
  else if(!F.called(request,'read_presentation'))turn={reply:'先读取第二页的内容和元素位置。',actions:[{tool:'read_presentation',args:{id,start_page:2,max_pages:1}}],done:false};
  else if(!F.called(request,'edit_presentation'))turn={reply:'只修改第二页的这一行。',actions:[{tool:'edit_presentation',args:{id,edits:[{action:'text',page:2,shape_id:shapeId,old_text:oldText,new_text:newText},{action:'notes',page:2,new_text:'修改后的备注：说明如何围绕同一份作品迭代。'}]}}],done:false};
  else turn={reply:'第二页已修改，其他页面和手动备注已保留。',actions:[],done:true};
  res.setHeader('content-type','application/json');res.end(F.payload(request,turn,{prompt_tokens:100,completion_tokens:80}));
 });});await new Promise(resolve=>model.listen(0,'127.0.0.1',resolve));
 const output=path.resolve('test-output/agent-presentations');fs.mkdirSync(output,{recursive:true});
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');const context=browser.contexts()[0];page=context.pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(25000);page.on('pageerror',error=>errors.push(error.message));
  await context.route(/^https?:\/\//,route=>['app.kok.local','media.kok.local'].includes(new URL(route.request().url()).hostname)?route.continue():(remote.push(route.request().url()),route.abort()));
  await page.waitForFunction(()=>!$('#startup'));await page.setViewportSize({width:1440,height:960});
  await page.evaluate(async endpoint=>{await native('saveAiConfig',{endpoint,model:'ppt-edit-fixture',clearKey:true,automatic:false});await setting({theme:'dark'});applyTheme();await refresh();navigate('works');await AgentUi.newSession();Object.assign(AgentUi.state,{skill:'',intent:'assist',scope:'all'});await DocumentUi.open({format:'pptx'});},`http://127.0.0.1:${model.address().port}/v1`);
  await page.locator('#new-presentation-topic').fill('kok 的创作与阅读');await page.locator('#new-presentation-pages').selectOption('5');await page.locator('#new-presentation-theme').selectOption('midnight');await page.locator('#new-presentation-agent').click();
  await page.waitForSelector('[data-agent-preview-slides]');assert.equal(await page.evaluate(()=>state.items.filter(i=>i.ext==='PPTX').length),0);
  await page.locator('[data-agent-preview-slides]').click();await page.waitForSelector('.agent-deck-slide');assert.equal(await page.locator('.agent-deck-pages button').count(),5);assert.match(await page.locator('.agent-deck-slide').innerText(),/把想法做成作品/);
  await page.locator('.agent-deck-pages [data-pi-deck-page="2"]').click();assert.match(await page.locator('.agent-deck-slide').innerText(),/阅读：查看来源/);
  await page.screenshot({path:path.join(output,'ppt-proposal-dark.png'),animations:'disabled'});
  await page.locator('[data-agent-run]').click();await page.waitForFunction(()=>!AgentUi.state.job);await page.waitForSelector('.pi-artifact-tabs [data-pi-artifact]');
  const item=await page.evaluate(()=>byId(AgentUi.state.messages.find(m=>m.tool==='create_presentation'&&m.status==='done').result.id));id=item.id;assert(path.resolve(item.path).startsWith(path.resolve(profile)+path.sep));assert(fs.existsSync(item.path));
  const read=()=>page.evaluate(id=>native('readPresentation',{id}),id),hash=()=>crypto.createHash('sha256').update(fs.readFileSync(item.path)).digest('hex');let deck=await read();assert.equal(deck.slides.length,5);assert.equal(deck.slides[0].background,'111218');assert.equal(deck.slides[0].notes,'介绍这次演示的主题。');shapeId=deck.slides[1].shapes.find(s=>s.text.includes(oldText)).id;
  const initial=hash(),other=JSON.stringify(deck.slides[4]);mode='edit';await page.locator('.agent-deck-pages [data-pi-deck-page="1"]').click();await page.locator('[data-pi-artifact-agent]').click();assert.match(await page.locator('#agent-input').inputValue(),/2/);
  await page.locator('#agent-input').fill('修改第二页第一行，让它更简洁，其他页面保留');await page.locator('.agent-send').click();await page.waitForSelector('.agent-card.agent-pending');
  const call=await page.evaluate(()=>AgentUi.state.messages.findLast(m=>m.tool==='edit_presentation'&&m.status==='pending'));assert(call);assert.equal(hash(),initial);
  await page.locator(`[data-agent-preview-slides="${call.id}"]`).click();await page.waitForSelector('.agent-deck-slide');await page.locator('.agent-deck-pages [data-pi-deck-page="1"]').click();assert.match(await page.locator('.agent-deck-slide').innerText(),new RegExp(newText));assert.equal(hash(),initial,'PPT candidate preview does not modify the real file');
  await page.locator(`[data-agent-run="${call.id}"]`).click();await page.waitForFunction(()=>!AgentUi.state.job);deck=await read();assert(deck.slides[1].shapes.some(s=>s.text.includes(newText)));assert.equal(JSON.stringify(deck.slides[4]),other);assert.notEqual(hash(),initial);assert.equal(await page.evaluate(()=>state.items.filter(i=>i.ext==='PPTX').length),1);
  await page.locator('.agent-deck-pages [data-pi-deck-page="1"]').click();await page.screenshot({path:path.join(output,'ppt-edited-dark.png'),animations:'disabled'});
  for(const theme of ['light','dark']){await page.evaluate(async theme=>{await setting({theme});applyTheme();},theme);await page.screenshot({path:path.join(output,`ppt-workspace-${theme}.png`),animations:'disabled'});}
  await page.locator(`[data-agent-undo="${call.id}"]`).click();await page.waitForFunction(id=>AgentUi.state.messages.some(m=>m.id===id&&m.status==='undone'),call.id);assert.equal(hash(),initial,'PPT undo restores the exact original package and retains the same item');

  await page.locator('[data-pi-artifact-edit]').click();await page.waitForSelector('#ppt-slide');await page.evaluate(()=>PresentationUi.go(2));await page.locator('[data-ppt-action=notes]').click();await page.locator('#ppt-notes').fill('手动编辑的备注必须保留。');await page.evaluate(()=>PresentationUi.save());
  deck=await read();assert.equal(deck.slides[2].notes,'手动编辑的备注必须保留。','managed manual edits save to the actual PPTX');
  shapeId=deck.slides[1].shapes.find(s=>s.text.includes(oldText)).id;newText='Agent 与用户共同完善同一份作品';
  await page.locator('[data-ppt-action=agent]').click();await page.waitForFunction(()=>!viewer.open&&$('#agent-input')?.value.includes('3'));assert.match(await page.locator('#agent-input').inputValue(),/3/);
  await page.locator('#agent-input').fill('把第二页第一行换一种更清晰的说法，保留第三页手动备注');await page.locator('.agent-send').click();await page.waitForSelector('.agent-card.agent-pending');const next=await page.evaluate(()=>AgentUi.state.messages.findLast(m=>m.tool==='edit_presentation'&&m.status==='pending'));
  await page.locator(`[data-agent-run="${next.id}"]`).click();await page.waitForFunction(()=>!AgentUi.state.job);deck=await read();assert(deck.slides[1].shapes.some(s=>s.text.includes(newText)));assert.equal(deck.slides[2].notes,'手动编辑的备注必须保留。');
  const count=requests.length;await page.evaluate(()=>AgentUi.save());await page.reload();await page.waitForFunction(()=>!$('#startup'));await page.evaluate(()=>navigate('agent'));await page.waitForFunction(()=>AgentUi.state.loaded&&!AgentUi.state.job);assert.equal(requests.length,count);

  // P2-C: a presentation page can be handed to Agent as a local-revision draft. The prompt carries
  // the page number/content and constraints, the deck stays attached as context, and nothing is sent.
  await page.evaluate(id=>openItem(id,{play:false}),id);await page.waitForSelector('#ppt-slide');
  await page.evaluate(()=>PresentationUi.go(1));await page.waitForFunction(()=>$('#ppt-position')?.textContent.startsWith('2 /'));
  const pageAgentRequests=requests.length;
  await page.locator('[data-ppt-agent]').click();
  await page.waitForFunction(()=>!viewer.open&&$('#agent-input')?.value.includes('第 2 页'));
  const pageAgentPrompt=await page.locator('#agent-input').inputValue();
  assert.match(pageAgentPrompt,/第 2 页（共 5 页）/,'the page prompt names the current page');
  assert.match(pageAgentPrompt,/从一个清楚的目标开始/,'the page prompt carries the slide title');
  assert.match(pageAgentPrompt,/Agent 与用户共同完善同一份作品/,'the page prompt carries the slide text');
  assert.match(pageAgentPrompt,/只修改第 2 页/,'the page prompt limits the change to the current page');
  assert.match(pageAgentPrompt,/edit_presentation/,'the page prompt suggests the local revision tool');
  assert.equal(await page.evaluate(()=>state.route),'agent','the Agent route is opened');
  assert.equal(await page.evaluate(id=>AgentUi.state.context.includes(id),id),true,'the presentation stays attached as Agent context');
  assert.equal(await page.evaluate(()=>AgentUi.state.job),null,'opening the page agent starts no task');
  assert.equal(requests.length,pageAgentRequests,'opening the page agent sends no model request');
  await page.evaluate(id=>openItem(id,{play:false}),id);await page.waitForSelector('#ppt-slide');
  await page.evaluate(()=>PresentationUi.go(2));await page.waitForFunction(()=>PresentationUi.state.session.page===2);
  assert.match(await page.locator('#ppt-slide').innerText(),/创作与阅读/,'the presentation stays usable after handing a page to Agent');
  await page.evaluate(()=>closeViewer());await page.waitForFunction(()=>!viewer.open);
  assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);console.log('PASS: built-in Agent PPT brief → native candidate preview → approved editable PPTX → page/shape revision → exact undo → manual edit → Agent continuation, themes and no-replay restore.');
 }catch(error){if(page)await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});throw error;}finally{model.close();if(browser)await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

'use strict';
// Pi-derived presentation with real native files and a loopback model fixture.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http');
const {chromium}=require('./browser.cjs'),F=require('./agent-fixture.cjs');
(async()=>{
 const profile=process.env.KOK_TEST_PROFILE;assert(profile);
 const output=path.resolve('test-output/pi-agent-ui');fs.mkdirSync(output,{recursive:true});
 let browser,page;const requests=[],errors=[],remote=[];
 const first='# 重逢\n\n## 雨夜来信\n失联多年的朋友寄来一张旧车票。\n\n## 最后一班车\n两人在旅途中说出当年的误会。\n\n## 天亮以后\n他们各自作出新的选择。';
 const second='# 人物设定\n\n- **林夏**：在老车站工作，保存着一封没有寄出的信。\n- **周远**：回乡寻找旧友，希望重新开始。\n\n<img src=x onerror="window.badPiArtifact=true">';
 const model=http.createServer((req,res)=>{let raw='';req.on('data',chunk=>raw+=chunk);req.on('end',()=>{
  const request=JSON.parse(raw);requests.push(request);
  const turn=F.called(request,'create_document')?{reply:'章纲和人物设定已经分别保存，可以边看草稿边继续修改。',actions:[],done:true}:{reply:'## 把这个故事分成三章\n- **雨夜来信**：一张旧车票，让两人再次相遇。\n- **最后一班车**：在旅途中解开误会。\n- **天亮以后**：各自作出新的选择。',actions:[{tool:'create_document',args:{name:'重逢 · 章纲',format:'md',text:first}},{tool:'create_document',args:{name:'人物设定',format:'md',text:second}}],done:false};
  res.setHeader('content-type','application/json');res.end(F.payload(request,turn,{prompt_tokens:80,completion_tokens:50}));
 });});await new Promise(resolve=>model.listen(0,'127.0.0.1',resolve));
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');const context=browser.contexts()[0];
  page=context.pages().find(page=>page.url().startsWith('https://app.kok.local')&&!page.url().includes('preview='));
  page.setDefaultTimeout(18000);page.on('pageerror',error=>errors.push(error.message));
  await context.route(/^https?:\/\//,route=>['app.kok.local','media.kok.local'].includes(new URL(route.request().url()).hostname)?route.continue():(remote.push(route.request().url()),route.abort()));
  await page.waitForFunction(()=>!$('#startup'));await page.setViewportSize({width:1440,height:960});
  await page.evaluate(async endpoint=>{
   await native('saveAiConfig',{endpoint,model:'pi-ui-fixture',clearKey:true,automatic:false});await setting({theme:'dark',fontFamily:'system'});applyTheme();await applyFont();
   await refresh();navigate('agent');await AgentUi.newSession();Object.assign(AgentUi.state,{skill:'',intent:'assist',scope:'all'});AgentUi.render();
  },`http://127.0.0.1:${model.address().port}/v1`);
  assert(await page.locator('#agent-input').evaluate(input=>input.getBoundingClientRect().top>innerHeight/2),'Pi input stays at the bottom on an empty task');
  assert.equal(await page.locator('[data-agent-pick] [data-lucide=paperclip]').count(),1);
  assert.equal(await page.locator('[data-agent-artifacts]').isVisible(),false,'empty sessions have no artifact control');
  await page.screenshot({path:path.join(output,'pi-empty-dark.png'),animations:'disabled'});
  await page.locator('#agent-input').fill('把重逢的故事整理成三章大纲，再写一份人物设定');await page.locator('.agent-send').click();
  await page.waitForSelector('[data-agent-run-selected]');assert.equal(await page.locator('.pi-artifacts').isVisible(),false,'proposals are not saved artifacts');
  await page.locator('[data-agent-run-selected]').click();await page.waitForFunction(()=>!AgentUi.state.job);
  await page.waitForSelector('.pi-artifact-document');await page.waitForFunction(()=>document.querySelectorAll('.pi-artifact-tabs button').length===2);
  const ids=await page.locator('.pi-artifact-tabs button').evaluateAll(buttons=>buttons.map(button=>button.dataset.piArtifact));
  assert.equal(ids.length,2);assert.equal(await page.locator('.pi-artifacts img').count(),0);assert.equal(await page.evaluate(()=>!!window.badPiArtifact),false);
  assert(await page.evaluate(()=>{const chat=$('.agent-main').getBoundingClientRect(),artifact=$('.pi-artifacts').getBoundingClientRect();return chat.right<=artifact.left+1&&chat.width>400&&artifact.width>400;}));
  assert.equal(await page.locator('.agent-main').evaluate(node=>node.inert),false,'wide view keeps the conversation usable');
  const afterTask=requests.length;await page.locator('#agent-input').fill('把第二章写得更有悬念');
  await page.locator(`[data-pi-artifact="${ids[0]}"]`).last().click();await page.waitForFunction(()=>document.querySelector('.pi-artifact-document')?.textContent.includes('雨夜来信'));
  await page.locator('[data-pi-artifact-mode=source]').click();assert.match(await page.locator('.pi-artifact-source').innerText(),/^# 重逢/);
  await page.evaluate(()=>{window.piInput=$('#agent-input');$('#agent-input').focus();$('#agent-input').setSelectionRange(2,5);AgentUi.render({scroll:false});});
  assert(await page.evaluate(()=>$('#agent-input')===window.piInput&&$('#agent-input').selectionStart===2&&$('#agent-input').selectionEnd===5));
  await page.locator('[data-pi-artifact-mode=preview]').click();assert.equal(await page.locator('#agent-input').inputValue(),'把第二章写得更有悬念');assert.equal(requests.length,afterTask);
  console.log('PASS: Pi editor layout, native approvals, multiple real artifacts, split view, safe Markdown and stable input without new model requests.');

  const file=await page.evaluate(id=>byId(id).path,ids[0]);assert(path.resolve(file).startsWith(path.resolve(profile)+path.sep),'only mutate an isolated test document');
  fs.writeFileSync(file,first+'\n\n## 新增场景\n清晨，旧车站重新开门，林夏作出了自己的决定。','utf8');
  await page.locator('[data-pi-artifact-refresh]').click();await page.waitForFunction(()=>document.querySelector('.pi-artifact-document')?.textContent.includes('清晨，旧车站重新开门'));
  assert.equal(requests.length,afterTask,'preview refresh reads the actual local file, not the model');
  await page.locator('[data-pi-artifact-edit]').click();await page.waitForSelector('#creative-text');
  assert.match(await page.locator('#creative-text').inputValue(),/清晨，旧车站重新开门/);await page.evaluate(()=>closeCreative());
  await page.waitForFunction(()=>!$('#creative-dialog').open);
  console.log('PASS: preview re-reads disk changes and the edit action opens the existing managed document editor.');
  await page.evaluate(()=>toast('工作已保存'));assert(await page.evaluate(()=>{const buttons=[...document.querySelectorAll('.pi-artifacts header button,.pi-artifact-toolbar button')].map(button=>button.getBoundingClientRect());return [...document.querySelectorAll('#toasts .toast')].every(toast=>{const box=toast.getBoundingClientRect();return buttons.every(button=>box.right<=button.left||box.left>=button.right||box.bottom<=button.top||box.top>=button.bottom);});}),'notifications do not cover artifact actions');
  await page.evaluate(()=>document.querySelectorAll('#toasts .toast').forEach(toast=>toast.remove()));

  for(const theme of ['dark','light']){
   await page.evaluate(async theme=>{await setting({theme});applyTheme();},theme);
   await page.screenshot({path:path.join(output,`pi-workspace-${theme}-1440.png`),animations:'disabled'});
  }
  for(const width of [650,500]){
   await page.setViewportSize({width,height:680});await page.waitForFunction(()=>$('.agent-main').inert);
   assert(await page.evaluate(()=>{const box=$('.pi-artifacts').getBoundingClientRect();return box.x>=0&&box.right<=innerWidth+1&&document.documentElement.scrollWidth<=innerWidth;}));
   await page.screenshot({path:path.join(output,`pi-artifact-${width}.png`),animations:'disabled'});
   await page.keyboard.press('Escape');await page.waitForFunction(()=>!$('.agent-main').inert);
   assert.equal(await page.locator('#agent-input').inputValue(),'把第二章写得更有悬念');
   await page.locator('[data-agent-artifacts]').click();await page.waitForSelector('.pi-artifacts:not([hidden])');
  }
  await page.setViewportSize({width:1440,height:960});await page.locator('[data-pi-artifact-close]').click();
  await page.evaluate(()=>AgentUi.save());await page.reload();await page.waitForFunction(()=>!$('#startup'));
  await page.evaluate(()=>navigate('agent'));await page.waitForFunction(()=>AgentUi.state.loaded&&!AgentUi.state.job);
  assert.equal(await page.locator('.pi-artifacts').isVisible(),false,'restoring a session does not auto-open its artifacts');
  await page.locator('[data-agent-artifacts]').click();await page.waitForSelector('.pi-artifact-document');assert.equal(await page.locator('.pi-artifact-tabs button').count(),2);
  assert.equal(requests.length,afterTask);console.log('PASS: light/dark split workspace, 500–650 px overlay, Escape return and no-replay session restore.');

  await page.locator('[data-pi-artifact-close]').click();
  await page.evaluate(()=>{
   Kok.extend('core','test-pi:delayed-read','native',previous=>async function(action,...args){
    const result=await previous.call(this,action,...args);
    if(action==='documentSource'&&window.delayPiRead){window.delayPiRead=false;return new Promise(resolve=>{window.finishPiRead=()=>resolve(result);});}
    return result;
   });window.delayPiRead=true;
  });
  await page.locator('[data-agent-artifacts]').click();await page.waitForFunction(()=>typeof window.finishPiRead==='function');
  await page.locator('[data-agent-new]').click();await page.waitForSelector('.agent-page.is-empty');
  await page.evaluate(()=>window.finishPiRead());await page.waitForTimeout(100);
  assert.equal(await page.locator('.pi-artifacts').isVisible(),false,'a late document read cannot enter a new session');
  assert.equal(await page.locator('.pi-artifact-document').count(),0);
  await page.emulateMedia({reducedMotion:'reduce'});await page.locator('[data-agent-sessions]').click();
  assert.equal(await page.locator('#agent-history').evaluate(node=>node.getAnimations().filter(animation=>animation.playState==='running').length),0);
  assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);console.log('PASS: late file responses stay session-bound, reduced motion and no external browser traffic.');
 }catch(error){if(page)await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});throw error;}
 finally{model.close();if(browser)await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

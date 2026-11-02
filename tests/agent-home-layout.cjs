'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),{chromium}=require('./browser.cjs');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 try{
  const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local')),errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  const output=path.resolve('test-output/agent-home-refresh');fs.mkdirSync(output,{recursive:true});
  await page.waitForFunction(()=>globalThis.AgentUi&&state.route==='agent');
  assert.equal(await page.locator('.nav-primary [data-route]').first().getAttribute('data-route'),'agent','Agent is the first destination');
  assert.deepEqual(await page.locator('.nav-primary [data-route]').evaluateAll(buttons=>buttons.map(button=>button.dataset.route)),['agent','works','overview'],'Smart mode exposes exactly the three scenario destinations');
  await page.setViewportSize({width:1440,height:1000});await page.emulateMedia({reducedMotion:'no-preference'});
  await page.evaluate(async()=>{await native('importSamples');await refresh();await setting({fontFamily:'system'});await applyFont();await AgentUi.newSession();});
  assert.equal(await page.locator('.topbar').isVisible(),false,'Library actions do not compete with the Agent composer');
  for(const theme of ['light','dark']){
   await page.evaluate(async theme=>{await setting({theme});applyTheme();},theme);
   await page.screenshot({path:path.join(output,'agent-'+theme+'-1440.png'),animations:'disabled'});
  }
  const background=await page.evaluate(()=>getComputedStyle(document.body).backgroundColor.match(/\d+/g).slice(0,3).map(Number));
  assert(Math.max(...background)<18&&Math.max(...background)-Math.min(...background)<6,'Default dark background is near black');
  await page.locator('[data-agent-more]').click();await page.evaluate(()=>AgentUi.render({scroll:false}));
  assert(await page.locator('.glass-menu:popover-open').isVisible(),'A background paint retains the menu and its trigger');
  await page.locator('[data-context-action=memory]').click();
  await page.waitForSelector('[data-memory-new]');await page.keyboard.press('Escape');await page.waitForFunction(()=>!$('#small-dialog').open);
  await page.locator('#agent-advanced>summary').click();assert(await page.locator('#agent-skill').isVisible());
  await page.locator('#agent-advanced>summary').click();assert.equal(await page.locator('#agent-skill').isVisible(),false);
  const input=await page.locator('#agent-input').elementHandle();
  await page.locator('#agent-input').fill('统计资料库构成');await page.locator('.agent-send').click();
  await page.waitForSelector('[data-agent-local]');await page.waitForFunction(()=>!AgentUi.state.job);
  assert(await input.evaluate(el=>el===document.querySelector('#agent-input')),'Starting a task preserves the input node');
  assert.equal(await page.locator('.agent-page.is-empty').count(),0,'A result switches to the conversation layout');
  await page.waitForFunction(()=>!document.querySelector('.agent-composer').getAnimations().some(a=>a.playState==='running'));
  assert(await page.evaluate(()=>{const composer=$('.agent-composer').getBoundingClientRect();return composer.bottom<=innerHeight&&composer.top>innerHeight/2;}),'Conversation composer stays within the bottom of the viewport');
  await page.screenshot({path:path.join(output,'agent-result-dark.png'),animations:'disabled'});
  await page.evaluate(()=>openItem(state.items.find(item=>item.kind==='music').id));await page.waitForSelector('body.has-player');
  await page.locator('#close-viewer').click();await page.waitForFunction(()=>!document.querySelector('#viewer').open);
  await page.locator('#music-bar').waitFor({state:'visible'});
  for(const width of [1440,650]){
   await page.setViewportSize({width,height:width===650?620:1000});
   await page.waitForFunction(()=>{const composer=document.querySelector('.agent-composer').getBoundingClientRect(),player=document.querySelector('#music-bar').getBoundingClientRect();return composer.bottom<=player.top&&document.documentElement.scrollWidth<=innerWidth;});
   assert(await page.locator('.agent-send').isVisible(),'Listening and Agent work can share the window');
   assert(await page.evaluate(()=>{const send=document.querySelector('.agent-send').getBoundingClientRect();return [...document.querySelectorAll('#toasts .toast')].every(toast=>{const r=toast.getBoundingClientRect();return !r.width||r.right<=send.left||r.left>=send.right||r.bottom<=send.top||r.top>=send.bottom;});}),'Background notices do not cover the send control');
   await page.screenshot({path:path.join(output,`agent-with-player-${width}.png`),animations:'disabled'});
  }
  await page.setViewportSize({width:1440,height:1000});
  await page.locator('#close-player').click();await page.waitForFunction(()=>!document.body.classList.contains('has-player'));
  await page.locator('[data-agent-new]').click();await page.waitForSelector('.agent-page.is-empty');
  for(const theme of ['light','dark'])for(const width of [960,650]){
   await page.setViewportSize({width,height:width===650?620:800});await page.evaluate(async theme=>{await setting({theme});applyTheme();},theme);
   assert(await page.locator('.agent-send').isVisible());
   assert(await page.evaluate(()=>{const input=$('#agent-input').getBoundingClientRect();return input.width>150&&input.bottom<innerHeight&&document.documentElement.scrollWidth<=innerWidth;}),'The main action fits compact windows');
   await page.screenshot({path:path.join(output,`agent-${theme}-${width}.png`),animations:'disabled'});
  }
  await page.setViewportSize({width:1440,height:1000});await page.evaluate(()=>navigate('overview'));
  assert(await page.locator('.topbar').isVisible());await page.screenshot({path:path.join(output,'library-dark.png'),animations:'disabled'});
  await page.locator('.sidebar .brand').click();await page.waitForSelector('.agent-page');
  await page.emulateMedia({reducedMotion:'reduce'});await page.locator('#agent-input').fill('统计资料库构成');await page.locator('.agent-send').click();await page.waitForSelector('[data-agent-local]');
  await page.waitForFunction(()=>!AgentUi.state.job);assert.equal(await page.locator('.agent-composer').evaluate(el=>el.getAnimations().length),0);
  await page.evaluate(()=>{Kok.extend('core','test-home:import','native',previous=>async function(action,...args){if(action==='importFiles'){await previous.call(this,'createDocument',{name:'Attached task brief',format:'md',collection:'',text:'# Task brief\nA local attachment fixture.'});return{added:1,skipped:0};}return previous.call(this,action,...args);});return AgentUi.newSession();});
  await page.locator('#agent-input').fill('保留我的任务要求');await page.locator('[data-agent-pick]').click();await page.locator('#agent-pick-import').click();
  await page.waitForFunction(()=>[...document.querySelectorAll('#agent-pick-list input:checked')].some(input=>byId(input.value)?.name==='Attached task brief'));
  await page.locator('#agent-pick-done').click();await page.waitForFunction(()=>!$('#small-dialog').open);
  assert.equal(await page.locator('#agent-input').inputValue(),'保留我的任务要求','Importing an attachment preserves the goal');
  assert.equal(await page.evaluate(()=>AgentUi.state.context.length),1,'Imported files become task attachments without a page detour');
  await page.screenshot({path:path.join(output,'agent-attachment-dark.png'),animations:'disabled'});
  await page.evaluate(()=>showModeChooser(true));await page.screenshot({path:path.join(output,'welcome-dark.png'),animations:'disabled'});await page.evaluate(()=>modeDialog().close());
  assert.deepEqual(errors,[]);console.log('PASS: Agent default home, secondary library, tools menu, local task/result, stable input, compact themes, brand navigation and reduced motion.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});

'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium}=require('./browser.cjs');
(async()=>{
 const profile=process.env.KOK_TEST_PROFILE;if(!profile)throw Error('Use the isolated desktop runner');
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225'),page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));
 const errors=[],remote=[];page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(/font|OTS/i.test(m.text()))console.log(m.type()+': '+m.text());});page.setDefaultTimeout(20000);
 const shot=async name=>{assert(page.url().startsWith('https://app.kok.local'));await page.screenshot({path:path.join(profile,name+'.png'),animations:'disabled'});};
 try{
  assert.equal(path.resolve((await page.evaluate(()=>native('diagnostics'))).dataDir),path.resolve(profile));
  await page.context().route(/^https?:\/\//,route=>{const host=new URL(route.request().url()).hostname;if(['app.kok.local','media.kok.local'].includes(host))return route.continue();remote.push(host);return route.abort();});
  await page.reload();await page.waitForFunction(()=>!document.querySelector('#startup'));
  assert(await page.evaluate(()=>getComputedStyle(document.body).fontFamily.includes('Smiley Sans')));
  assert(await page.evaluate(()=>[...document.fonts].filter(f=>/LXGW WenKai Lite/.test(f.family)).every(f=>f.status==='unloaded')),'Unselected fonts must not load during startup');
  await page.evaluate(async()=>{await setting({fontFamily:'chill'});await applyFont();});assert(await page.evaluate(()=>state.settings.fontFamily==='smiley'&&getComputedStyle(document.body).fontFamily.includes('Smiley Sans')),'Retired font preference must migrate to Smiley Sans');
  await page.route('**/fonts/LXGWWenKaiLite-Regular.woff2',async route=>{await new Promise(resolve=>setTimeout(resolve,250));await route.continue();});
  await page.evaluate(async()=>{await setting({fontFamily:'wenkai'});const earlier=applyFont();await setting({fontFamily:'smiley'});const latest=applyFont();await Promise.all([earlier,latest]);});
  const switched=await page.evaluate(()=>({id:state.settings.fontFamily,family:getComputedStyle(document.body).fontFamily,faces:[...document.fonts].map(f=>({family:f.family,status:f.status}))}));
  assert(switched.id==='smiley'&&switched.family.includes('Smiley Sans'),'Rapid switching must finish on the latest selection: '+JSON.stringify(switched));
  await page.unroute('**/fonts/LXGWWenKaiLite-Regular.woff2');
  await page.evaluate(async()=>{await native('importSamples');await refresh();await setting({theme:'light'});applyTheme();await settings();});
  for(const [id,family]of [['smiley','Smiley Sans'],['wenkai','LXGW WenKai Lite']]){
   assert.equal(await page.locator('#font-select option[value="'+id+'"]').count(),1);
   await page.locator('#font-select').selectOption(id);await page.waitForFunction(({id,family})=>state.settings.fontFamily===id&&getComputedStyle(document.body).fontFamily.includes(family),{id,family});
   assert(await page.evaluate(family=>[...document.fonts].some(f=>f.family.replaceAll('"','')===family&&f.status==='loaded'),family));
   await shot(id+'-settings-light');
  }
  await page.evaluate(async()=>{await closeSmall();const item=state.items.find(i=>i.ext==='EPUB');await openItem(item.id,{play:false});});await page.waitForSelector('#reader-text');
  for(const [id,family]of [['wenkai','LXGW WenKai Lite']]){
   await page.evaluate(async id=>{await setting({fontFamily:id,theme:'dark'});applyTheme();await applyFont();},id);
   assert(await page.locator('#reader-text').evaluate((el,family)=>getComputedStyle(el).fontFamily.includes(family),family));await shot(id+'-reading-dark');
  }
  await page.evaluate(async()=>{await closeViewer();await settings();I18n.applyLanguage('en');});assert.equal(await page.locator('#font-select option[value=chill]').count(),0);assert.equal(await page.locator('#font-select option[value=wenkai]').innerText(),'LXGW WenKai Lite (built-in)');
  await page.reload();await page.waitForFunction(()=>!document.querySelector('#startup'));assert(await page.evaluate(()=>state.settings.fontFamily==='wenkai'&&getComputedStyle(document.body).fontFamily.includes('LXGW WenKai Lite')));
  await page.evaluate(async()=>{const item=state.items.find(i=>i.ext==='PDF');await openItem(item.id,{play:false});});await page.waitForSelector('.pdf-page canvas');await page.waitForSelector('.textLayer span');assert((await page.locator('.textLayer').first().innerText()).length>20);await shot('pdf-official-minified');
  assert.deepEqual(remote,[]);assert.deepEqual(errors,[]);assert(await page.evaluate(()=>audio.paused));
  console.log('PASS: two local fonts, on-demand loading with remote requests blocked, latest-choice race handling, settings selection, reader type, English labels and persisted preference.');console.log('Captures: '+profile);
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});

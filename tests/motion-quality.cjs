const {chromium}=require('./browser.cjs'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 try{
  const page=browser.contexts()[0].pages().find(page=>page.url().startsWith('https://app.kok.local')),errors=[];
  page.on('pageerror',error=>errors.push(error.message));
  const output=path.resolve('test-output/oil-ui-refinement');fs.mkdirSync(output,{recursive:true});
  await page.setViewportSize({width:1440,height:1000});await page.emulateMedia({reducedMotion:'no-preference'});
  await page.evaluate(async()=>{await native('importSamples');await refresh();await setting({view:'grid',theme:'light',fontFamily:'system'});applyTheme();await applyFont();navigate('overview');});
  const filters=await page.locator('[data-library-filters]').elementHandle(),photo=await page.locator('[data-filter=photo]').elementHandle();
  await page.locator('[data-filter=photo]').click();await page.waitForFunction(()=>state.route==='photo');
  assert(await filters.evaluate(element=>element===document.querySelector('[data-library-filters]')),'Filtering keeps one shared indicator surface');
  assert(await photo.evaluate(element=>element===document.activeElement),'The filter retains keyboard focus');
  await page.locator('[data-filter=all]').click();await page.waitForFunction(()=>state.route==='overview');
  const continuity=await page.evaluate(async()=>{
   const element=document.createElement('div');element.style.cssText='position:fixed;left:300px;top:200px;width:20px;height:20px;opacity:1;pointer-events:none';document.body.append(element);
   const first=CalmMotion.play(element,[{opacity:0,transform:'translateX(20px)'},{opacity:1,transform:'none'}],{duration:400,channel:'check'});first.pause();first.currentTime=90;
   const before={opacity:Number(getComputedStyle(element).opacity),transform:getComputedStyle(element).transform};
   const second=CalmMotion.play(element,[{opacity:0,transform:'translateX(20px)'},{opacity:1,transform:'none'}],{duration:200,channel:'check'});second.pause();second.currentTime=0;
   const after={opacity:Number(getComputedStyle(element).opacity),transform:getComputedStyle(element).transform};second.cancel();element.remove();return{before,after};
  });
  assert(Math.abs(continuity.before.opacity-continuity.after.opacity)<.015,'Interruptions keep the displayed opacity');
  assert.equal(continuity.before.transform,continuity.after.transform,'Interruptions keep the displayed transform');
  await page.locator('[data-library-tools]').click();await page.locator('.glass-menu:popover-open').waitFor();
  await page.evaluate(()=>{for(const animation of document.querySelector('.glass-menu').getAnimations())animation.finish();});
  const menuGeometry=await page.locator('.glass-menu').evaluate(element=>{const root=element.getBoundingClientRect(),style=getComputedStyle(element);return{padding:style.padding,radius:style.borderRadius,background:style.backgroundColor,items:[...element.querySelectorAll('button')].map(button=>{const box=button.getBoundingClientRect();return[box.x-root.x,box.y-root.y,box.width,box.height];})};});
  await page.keyboard.press('Escape');
  assert.equal(await page.evaluate(()=>GlassMenu.isOpen),false,'Dismissal releases logical state immediately');
  const exit=await page.locator('.glass-menu-exit').evaluate(element=>{const root=element.getBoundingClientRect(),style=getComputedStyle(element);return{inert:element.inert,hidden:element.getAttribute('aria-hidden'),pointer:style.pointerEvents,geometry:{padding:style.padding,radius:style.borderRadius,background:style.backgroundColor,items:[...element.querySelectorAll('button')].map(button=>{const box=button.getBoundingClientRect();return[box.x-root.x,box.y-root.y,box.width,box.height];})}};});
  assert(exit.inert&&exit.hidden==='true'&&exit.pointer==='none','An exiting surface cannot intercept input or remain accessible');
  const exitGeometry=exit.geometry;
  assert.deepEqual([exitGeometry.padding,exitGeometry.radius,exitGeometry.background],[menuGeometry.padding,menuGeometry.radius,menuGeometry.background],'The exit retains the same surface geometry and color');
  assert(exitGeometry.items.every((box,index)=>box.every((value,axis)=>Math.abs(value-menuGeometry.items[index][axis])<.1)),'The exit does not reflow menu contents');
  await page.locator('[data-library-tools]').click();assert.equal(await page.locator('.glass-menu:popover-open').count(),1,'Reopening replaces a pending exit');
  await page.keyboard.press('Escape');await page.waitForFunction(()=>!document.querySelector('.glass-menu-exit'));
  await page.locator('#sort').selectOption('name');await page.waitForFunction(()=>state.settings.sort==='name'&&[...document.querySelectorAll('.file-card')].map(card=>card.dataset.id).join()===filtered().map(item=>item.id).join());
  assert.deepEqual(await page.locator('.file-card').evaluateAll(cards=>cards.map(card=>card.dataset.id)),await page.evaluate(()=>filtered().map(item=>item.id)),'Layout movement does not change sorting or hit targets');
  await page.evaluate(async()=>{await Promise.allSettled(document.getAnimations().map(animation=>animation.finished));});
  await page.screenshot({path:path.join(output,'library-light.png'),animations:'disabled'});
  for(const theme of ['light','dark']){
   await page.evaluate(async theme=>{await setting({theme});applyTheme();navigate('overview');},theme);
   await page.setViewportSize({width:650,height:520});await page.locator('[data-library-tools]').click();await page.locator('.glass-menu:popover-open').waitFor();
   await page.screenshot({path:path.join(output,`launcher-${theme}-650.png`),animations:'disabled'});await page.keyboard.press('Escape');
  }
  await page.setViewportSize({width:1440,height:1000});await page.evaluate(()=>navigate('overview'));
  const intervals=await page.evaluate(async()=>{
   const times=[];let last;for(let n=0;n<75;n++){const now=await new Promise(requestAnimationFrame);if(last!==undefined)times.push(now-last);last=now;if(n%10===0)document.querySelector(n%20===0?'[data-filter=photo]':'[data-filter=all]')?.click();}return times;
  });
  const sorted=[...intervals].sort((a,b)=>a-b),report={continuity,frameSample:{count:intervals.length,medianMs:sorted[Math.floor(sorted.length*.5)],p95Ms:sorted[Math.floor(sorted.length*.95)],maxMs:sorted.at(-1)},note:'Observed isolated WebView2 session; not a hardware-independent frame-rate guarantee.'};
  fs.writeFileSync(path.join(output,'motion-check.json'),JSON.stringify(report,null,2)+'\n');
  await page.emulateMedia({reducedMotion:'reduce'});await page.evaluate(()=>navigate('overview'));await page.locator('[data-library-tools]').click();
  assert.equal(await page.locator('.glass-menu').evaluate(element=>element.getAnimations().length),0,'Reduced motion bypasses the entrance');
  await page.keyboard.press('Escape');assert.equal(await page.locator('.glass-menu-exit').count(),0,'Reduced motion dismisses without a retained frame');
  await page.waitForFunction(()=>CalmMotion.activeCount===0);assert.deepEqual(errors,[]);
  console.log('PASS: persistent filter/focus, interruption continuity, non-interactive exits, rapid reopen, sorting, both compact themes and reduced motion.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exit(1);});

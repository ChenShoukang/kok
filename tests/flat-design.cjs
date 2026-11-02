const {chromium}=require('./browser.cjs');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 try{
  const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  const output=path.resolve('test-output/flat-design');fs.mkdirSync(output,{recursive:true});
  await page.evaluate(async()=>{await native('importSamples');await refresh();await setting({fontFamily:'system',theme:'light',skin:'classic',view:'grid'});applyTheme();await applyFont();navigate('overview');});
  await page.setViewportSize({width:1440,height:1000});
  assert.equal(await page.locator('body').getAttribute('data-skin'),'flat','Saved classic preferences must use the new presentation');
  assert.equal(await page.locator('.nav-primary .nav-item').count(),3,'Smart mode has exactly three scenario destinations');
  assert.equal(await page.locator('.nav-item[aria-current="page"]').count(),1);
  assert.equal(await page.locator('.flat-library-grid .file-card').count(),await page.evaluate(()=>state.items.length));
  assert.equal(await page.locator('img[src*="icons/fluent/"],.graphite-disc,.private-vault,.graphite-book').count(),0,'Application placeholders must use outline icons');
  assert.equal(await page.locator('#private-space-toggle svg').getAttribute('data-lucide'),'lock');
  const material=await page.evaluate(()=>['.sidebar','.topbar','.button.primary','.file-cover'].map(s=>{const c=getComputedStyle(document.querySelector(s));return [s,c.backgroundImage,c.boxShadow];}));
  for(const [selector,image,shadow]of material){assert.equal(image,'none',selector);assert.equal(shadow,'none',selector);}
  const grid=await page.locator('.flat-library-grid .file-card').evaluateAll(cards=>cards.map(c=>{const r=c.getBoundingClientRect();return{w:r.width,h:r.height,x:r.x};}));
  assert(new Set(grid.map(r=>Math.round(r.w))).size<=2,'Mixed types use equal-width cards');
  assert(new Set(grid.map(r=>Math.round(r.h))).size<=2,'Mixed types use equal-height cards');
  await page.screenshot({path:path.join(output,'library-light.png'),animations:'disabled'});
  await page.locator('[data-view="list"]').click();await page.locator('.file-list').waitFor();
  await page.locator('[data-view="grid"]').click();await page.locator('.flat-library-grid').waitFor();
  await page.locator('#sort').selectOption('name');
  await page.waitForFunction(()=>[...document.querySelectorAll('.file-card')].map(c=>c.dataset.id).join()===filtered().map(i=>i.id).join());
  assert.deepEqual(await page.locator('.file-card').evaluateAll(cards=>cards.map(c=>c.dataset.id)),await page.evaluate(()=>filtered().map(i=>i.id)));
  const menu=page.locator('.card-menu').first();await menu.focus();await page.keyboard.press('Tab');await page.keyboard.press('Shift+Tab');
  assert.equal(await menu.evaluate(e=>getComputedStyle(e).outlineStyle),'solid');
  await page.keyboard.press('Enter');await page.locator('.file-action-menu:popover-open').waitFor();await page.keyboard.press('Escape');await page.waitForFunction(()=>!GlassMenu.isOpen);
  const id=await page.locator('.file-card').first().getAttribute('data-id');
  await page.locator(`.file-card [data-favorite="${id}"]`).click();await page.waitForFunction(id=>byId(id).favorite,id);
  await page.locator('[data-library-quick="favorites"]').click();assert.equal(await page.locator('.file-card').count(),1);
  await page.locator('#main-nav [data-route="overview"]').click();
  const doc=await page.evaluate(()=>state.items.find(i=>i.kind==='document'&&i.ext==='md')?.id);
  if(doc){await page.locator(`[data-open="${doc}"]`).click();await page.waitForFunction(()=>document.querySelector('#viewer').open);await page.locator('#close-viewer').click();}
  for(const theme of ['light','dark']){
   await page.evaluate(async theme=>{await setting({theme});applyTheme();navigate('overview');},theme);
   for(const [width,height]of [[1440,1000],[960,720],[650,520]]){
    await page.setViewportSize({width,height});await page.waitForTimeout(350);
    assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'No page overflow');
    assert(await page.evaluate(()=>{const r=document.querySelector('#settings-button').getBoundingClientRect();return r.bottom<=innerHeight+1;}),'Settings stays reachable');
    assert.equal(await page.locator('.nav-primary .nav-item').evaluateAll(items=>items.filter(item=>item.getClientRects().length).length),3,'The same short rail is used at every width');
    const surfaces=await page.evaluate(()=>['body','.sidebar','.topbar'].map(s=>getComputedStyle(document.querySelector(s)).backgroundColor));
    assert.equal(new Set(surfaces).size,1,'Workspace chrome shares one background');
    await page.screenshot({path:path.join(output,`library-${theme}-${width}.png`),animations:'disabled'});
    if(width===1440){
     await page.evaluate(()=>showModeChooser(true));
     await page.screenshot({path:path.join(output,`welcome-${theme}.png`),animations:'disabled'});
     await page.evaluate(()=>modeDialog().close());
    }
   }
  }
  // The compact rail keeps exactly the three scenario destinations; quick links live in the library home.
  await page.setViewportSize({width:650,height:520});await page.waitForTimeout(300);
  assert.deepEqual(await page.evaluate(()=>[...document.querySelectorAll('.nav-primary [data-route]')].map(button=>button.dataset.route)),['agent','works','overview'],'The compact rail keeps three destinations');
  assert.equal(await page.locator('[data-nav-more]').count(),0,'The compact rail has no overflow trigger');
  await page.evaluate(()=>navigate('overview'));
  await page.locator('[data-library-quick="favorites"]').click();
  await page.waitForFunction(()=>state.route==='favorites');
  assert.equal(await page.locator('.file-card').count(),1);
  await page.evaluate(()=>navigate('overview'));
  await page.setViewportSize({width:1440,height:1000});
  await page.evaluate(async()=>{await setting({uiLanguage:'en',theme:'light'});I18n.applyLanguage('en');applyTheme();navigate('overview');});
  assert.equal(await page.locator('.nav-primary [data-route="overview"]').getAttribute('aria-label'),'Library');
  await page.evaluate(()=>navigate('overview'));
  await page.evaluate(async()=>{await setting({appMode:'pure'});applyAppMode();render();});
  assert.deepEqual(await page.evaluate(()=>[...document.querySelectorAll('.nav-primary [data-route]')].map(button=>button.dataset.route)),['works','overview'],'Pure mode keeps 我的作品 and 资料库');
  assert.equal(await page.locator('[data-route="agent"]').count(),0,'Pure mode hides the Agent destination');
  await page.evaluate(async()=>{await setting({appMode:'smart'});applyAppMode();render();});
  await page.screenshot({path:path.join(output,'library-en.png'),animations:'disabled'});
  await page.locator('#main-nav [data-route="agent"]').click();await page.screenshot({path:path.join(output,'agent-light.png'),animations:'disabled'});
  await page.emulateMedia({reducedMotion:'reduce'});await page.evaluate(()=>navigate('overview'));
  assert.deepEqual(errors,[]);
  console.log('PASS: legacy skin migration, flat chrome, uniform mixed grid, sorting, view switch, keyboard menu/focus, favorite navigation, document open/close, light/dark compact layout, English, Agent and reduced motion.');
  console.log('Screenshots: '+output);
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});

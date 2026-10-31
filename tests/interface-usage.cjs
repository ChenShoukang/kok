const {chromium}=require('./browser.cjs'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 try{
  const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local')),errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  const output=path.resolve('test-output/interface-usage');fs.mkdirSync(output,{recursive:true});
  await page.setViewportSize({width:1440,height:1000});
  await page.evaluate(async()=>{await native('importSamples');await refresh();await setting({theme:'light',uiLanguage:'zh',view:'grid',fontFamily:'system'});applyTheme();await applyFont();navigate('overview');});
  assert.equal(await page.locator('.study-launch,.collection-heading .agent-launch').count(),0);
  assert.equal(await page.locator('[data-library-tools]').count(),1);
  assert.equal(await page.locator('[data-filter]').count(),6);
  assert.equal(await page.locator('svg[data-lucide="more"]').first().locator('circle').count(),3,'Real Lucide ellipsis keeps visible circle geometry');
  // Right-click is the very first file-menu action in a fresh window.
  await page.locator('.file-card').first().click({button:'right'});
  await page.locator('.file-action-menu:popover-open').waitFor();
  assert(!await page.locator('#small-dialog').evaluate(e=>e.open));
  await page.keyboard.press('Escape');assert(await page.locator('.card-menu').first().evaluate(e=>e===document.activeElement));
  await page.keyboard.press('Shift+Tab');
  const first=page.locator('.file-open').first();await first.focus();await page.keyboard.press('Escape');
  await page.keyboard.press('ArrowRight');assert.equal(await page.evaluate(()=>document.activeElement.dataset.open),await page.locator('.file-open').nth(1).getAttribute('data-open'));
  await page.keyboard.press(' ');assert.equal(await page.locator('.library-selected').count(),1);
  await page.keyboard.press('Shift+ArrowRight');assert.equal(await page.locator('.library-selected').count(),2);
  await page.keyboard.press('Escape');assert.equal(await page.locator('.library-selected').count(),0);
  await page.keyboard.press('End');assert.equal(await page.evaluate(()=>document.activeElement.dataset.open),await page.locator('.file-open').last().getAttribute('data-open'));
  await page.keyboard.press('Home');await page.keyboard.press('Shift+F10');await page.locator('.file-action-menu:popover-open').waitFor();
  await page.keyboard.press('End');assert(await page.locator('.file-action-menu>button').last().evaluate(e=>e===document.activeElement));
  await page.keyboard.press('Home');assert(await page.locator('.file-action-menu>button').first().evaluate(e=>e===document.activeElement));
  await page.keyboard.press('Escape');
  await page.locator('.card-menu').first().click();await page.locator('.file-action-menu:popover-open').waitFor();
  await page.waitForTimeout(170);
  const bounds=await page.evaluate(()=>{const r=$('.file-action-menu').getBoundingClientRect(),a=$('.card-menu').getBoundingClientRect();return{inside:r.left>=0&&r.right<=innerWidth&&r.top>=0&&r.bottom<=innerHeight,near:Math.abs(r.top-a.bottom)<12};});assert(bounds.inside&&bounds.near);
  await page.screenshot({path:path.join(output,'file-menu.png'),animations:'disabled'});
  await page.locator('.file-action-menu>button').nth(1).click();await page.waitForFunction(()=>state.items.some(i=>i.favorite));
  assert(await page.locator('.card-menu').first().evaluate(e=>e===document.activeElement),'Favorite rerender keeps the command trigger focused');
  await page.locator('[data-filter="photo"]').click();await page.waitForFunction(()=>document.querySelectorAll('.file-card').length===state.items.filter(i=>i.kind==='photo').length);
  assert.equal(await page.locator('.file-card:not(.photo)').count(),0);
  await page.locator('[data-filter="all"]').click();await page.waitForFunction(()=>document.querySelectorAll('.file-card').length===state.items.length);
  await page.locator('[data-view="list"]').click();await page.locator('.file-list').waitFor();
  await page.screenshot({path:path.join(output,'list.png'),animations:'disabled'});
  await page.keyboard.press('Control+f');assert(await page.locator('#search').evaluate(e=>e===document.activeElement));
  await page.locator('#search').fill('no-such-file-918273');await page.locator('.empty-state').waitFor();assert.equal(await page.locator('.file-card').count(),0);
  await page.evaluate(()=>navigate('overview'));await page.locator('[data-view="grid"]').click();await page.locator('.flat-library-grid').waitFor();
  const tools=page.locator('details[data-nav-group]').first();assert(!await tools.evaluate(e=>e.open));await tools.locator('summary').click();
  await page.locator('#main-nav [data-route="photo"]').click();assert(await page.locator('details[data-nav-group]').first().evaluate(e=>e.open));
  await page.evaluate(()=>navigate('overview'));await page.locator('[data-library-tools]').click();await page.locator('.glass-menu:popover-open').waitFor();
  await page.screenshot({path:path.join(output,'library-tools.png'),animations:'disabled'});
  await page.getByRole('menuitem',{name:'多文件对比与笔记回顾'}).click();await page.locator('#study-dialog[open]').waitFor();await page.evaluate(()=>closeStudy());
  await page.locator('#settings-button').click();await page.locator('.settings-dialog[open]').waitFor();
  await page.screenshot({path:path.join(output,'settings.png'),animations:'disabled'});await page.keyboard.press('Escape');
  for(const theme of ['light','dark']){
   await page.evaluate(async theme=>{await setting({theme});applyTheme();navigate('overview');},theme);
   await page.setViewportSize({width:650,height:520});await page.waitForTimeout(180);
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
   await page.locator('.card-menu').last().scrollIntoViewIfNeeded();await page.locator('.card-menu').last().click();await page.waitForTimeout(160);
   assert(await page.locator('.file-action-menu').evaluate(e=>{const r=e.getBoundingClientRect();return r.left>=0&&r.right<=innerWidth&&r.top>=0&&r.bottom<=innerHeight;}));
   await page.screenshot({path:path.join(output,`compact-menu-${theme}.png`),animations:'disabled'});await page.keyboard.press('Escape');
  }
  await page.setViewportSize({width:1440,height:1000});await page.evaluate(()=>navigate('overview'));
  for(let n=0;n<8;n++){await page.locator('.card-menu').first().click();await page.keyboard.press('Escape');}
  await page.waitForTimeout(220);assert.equal(await page.locator('.file-action-menu').count(),0);assert.equal(await page.evaluate(()=>CalmMotion.activeCount),0);
  await page.emulateMedia({reducedMotion:'reduce'});await page.locator('.card-menu').first().click();
  assert.equal(await page.locator('.file-action-menu').evaluate(e=>e.getAnimations().length),0);await page.keyboard.press('Escape');
  assert.deepEqual(errors,[]);
  console.log('PASS: fresh right-click, arrow/range/space selection, keyboard and anchored menus, focus recovery, filters, list, Ctrl+F, search-empty, persistent tool disclosure, compare/settings, viewport boundaries, rapid menu cleanup and reduced motion.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});

'use strict';
const assert=require('node:assert/strict'),path=require('node:path'),fs=require('node:fs'),{execFileSync}=require('node:child_process');
const {chromium}=require('./browser.cjs');
(async()=>{
 const profile=process.env.KOK_TEST_PROFILE,pid=process.env.KOK_TEST_NATIVE_PID;if(!profile||!pid)throw Error('Use the isolated desktop runner');
 const helper=path.resolve('test-output/PreviewWindowHelper.exe'),executable=process.env.KOK_TEST_EXECUTABLE||path.resolve('dist/kok/kok.exe');
 execFileSync(path.join(process.env.WINDIR,'Microsoft.NET/Framework64/v4.0.30319/csc.exe'),['/nologo','/target:exe','/out:'+helper,path.resolve('tests/WindowChromeHelper.cs')],{windowsHide:true});
 const run=(handle,action,...args)=>execFileSync(helper,[pid,String(handle),executable,action,...args.map(String)],{encoding:'utf8',windowsHide:true}).trim();
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225'),context=browser.contexts()[0],main=context.pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));
 const errors=[],remote=[];const observe=page=>{page.setDefaultTimeout(20000);page.on('pageerror',e=>errors.push(e.message));};context.pages().forEach(observe);context.on('page',observe);
 await context.route(/^https?:\/\//,route=>{if(['app.kok.local','media.kok.local'].includes(new URL(route.request().url()).hostname))return route.continue();remote.push(route.request().url());return route.abort();});
 const windows=()=>main.evaluate(()=>native('previewWindows'));
 const waitNative=async(predicate,arg)=>{const deadline=Date.now()+20000;while(Date.now()<deadline){if(await main.evaluate(predicate,arg))return;await main.waitForTimeout(100);}console.log('Windows:',await windows());for(const page of context.pages())if(page.url().startsWith('https://app.kok.local'))console.log(await page.evaluate(()=>({url:location.href,classes:document.body.className,private:state.isPrivate,viewer:state.viewer?.id,toasts:$('#toasts').textContent})));assert.fail('Native state did not settle: '+await main.evaluate(predicate,arg));};
 const preview=async id=>{await waitNative(async id=>(await native('previewWindows')).some(w=>w.id===id&&w.ready),id);const page=browser.contexts().flatMap(c=>c.pages()).find(p=>new URL(p.url()).searchParams.get('preview')===id);if(!page){const cdp=await browser.newBrowserCDPSession();console.log('Native windows:',await windows(),'CDP pages:',browser.contexts().flatMap(c=>c.pages()).map(p=>p.url()),'targets:',await cdp.send('Target.getTargets'));await cdp.detach();}assert(page,'Missing native preview page');assert(await page.evaluate(()=>document.body.classList.contains('preview-window')&&viewer.open));return page;};
 const shot=(page,name)=>{assert(page.url().startsWith('https://app.kok.local'));return page.screenshot({path:path.join(profile,name+'.png'),animations:'disabled'});};
 try{
  assert.equal(path.resolve((await main.evaluate(()=>native('diagnostics'))).dataDir),path.resolve(profile));
  await main.waitForFunction(()=>!document.querySelector('#startup'));await main.evaluate(async()=>{await native('importSamples');await refresh();});
  const ids=await main.evaluate(()=>({photo:state.items.find(i=>i.kind==='photo').id,book:state.items.find(i=>i.ext==='EPUB').id,document:state.items.find(i=>i.ext==='MD').id,video:state.items.find(i=>i.kind==='video').id}));
  await main.locator('[data-open="'+ids.photo+'"]').click();const photo=await preview(ids.photo);await photo.waitForFunction(()=>$('#photo-image')?.naturalWidth>0);
  assert.equal(await main.evaluate(()=>viewer.open),false,'Main library must remain usable');
  for(const id of [ids.book,ids.document,ids.video])await main.evaluate(id=>openPreviewWindow(id,{play:false}),id);
  const book=await preview(ids.book),doc=await preview(ids.document),video=await preview(ids.video);await book.waitForSelector('#reader-text');await doc.waitForSelector('#reader-text');await video.waitForFunction(()=>video?.readyState>=2);
  assert.equal((await windows()).length,4);assert.equal(await main.locator('.preview-window-tab').count(),4);
  assert(await photo.evaluate(()=>window.kokNativeDragSupported===true&&getComputedStyle($('.viewer-header')).getPropertyValue('app-region')==='drag'));
  const photoInfo=(await windows()).find(w=>w.id===ids.photo),mainHandle=(await main.evaluate(()=>native('diagnostics'))).window.handle;
  run(photoInfo.handle,'move',photoInfo.bounds.x+50,photoInfo.bounds.y+35);run(photoInfo.handle,'resize',640,500);
  await waitNative(async id=>{const w=(await native('previewWindows')).find(w=>w.id===id);return w.bounds.width===640&&w.bounds.height===500;},ids.photo);
  assert.equal(run(photoInfo.handle,'hit',1,200),'10');await photo.waitForFunction(()=>viewer.getBoundingClientRect().width>=innerWidth-1);
  await shot(photo,'photo-native-compact');
  // A real double click on unused image margin must change OS stacking, not close it.
  await photo.locator('.photo-image-wrap').dblclick({position:{x:6,y:8}});
  await waitNative(async id=>(await native('previewWindows')).find(w=>w.id===id)?.behind,ids.photo);
  assert.equal(run(mainHandle,'above',photoInfo.handle),'True');assert(await photo.locator('#photo-image').isVisible());
  await main.locator('[data-preview-focus="'+ids.photo+'"]').click();await waitNative(async id=>!(await native('previewWindows')).find(w=>w.id===id)?.behind,ids.photo);
  assert.equal((await windows()).length,4);assert.equal((await windows()).find(w=>w.id===ids.photo).handle,photoInfo.handle);
  // Text selection and editing controls must not send a reader behind the library.
  await doc.locator('#reader-text').dblclick();assert(!(await windows()).find(w=>w.id===ids.document).behind);
  await doc.locator('#notes-button').click();await doc.locator('#notes').fill('WINDOW_NOTE_SYNTHETIC');
  await doc.locator('.reader-scroll').dblclick({position:{x:5,y:8}});await waitNative(async id=>(await native('previewWindows')).find(w=>w.id===id)?.behind,ids.document);
  await main.evaluate(id=>native('focusPreview',{id}),ids.document);await doc.locator('#close-viewer').click();await waitNative(async id=>!(await native('previewWindows')).some(w=>w.id===id),ids.document);
  assert.equal(await main.evaluate(async id=>(await native('library')).items.find(i=>i.id===id).notes,ids.document),'WINDOW_NOTE_SYNTHETIC');
  await main.evaluate(async()=>{await setting({theme:'dark',fontFamily:'wenkai',uiLanguage:'en'});applyTheme();I18n.applyLanguage('en');await applyFont();});
  await book.waitForFunction(()=>document.body.dataset.theme==='dark'&&document.documentElement.lang.startsWith('en')&&getComputedStyle($('#reader-text')).fontFamily.includes('LXGW WenKai Lite'));
  assert.equal(await book.locator('[data-preview-action=sendPreviewBehind]').getAttribute('aria-label'),'Send behind main window');assert.deepEqual(await book.locator('.reading-side-tabs button').allTextContents(),['Contents','Bookmarks','Full-text search']);assert.match(await book.locator('.toc-chapters').innerText(),/第一章/);await shot(book,'book-native-dark-english');
  await book.locator('[data-preview-action=maximizePreview]').click();await waitNative(async id=>(await native('previewWindows')).find(w=>w.id===id)?.state==='Maximized',ids.book);await book.locator('[data-preview-action=maximizePreview]').click();
  await video.locator('#video-fullscreen').click();await waitNative(async id=>(await native('previewWindows')).find(w=>w.id===id)?.fullScreen,ids.video);assert.equal((await windows()).find(w=>w.id===ids.video).border,'None');await video.keyboard.press('Escape');await waitNative(async id=>!(await native('previewWindows')).find(w=>w.id===id)?.fullScreen,ids.video);assert.equal((await windows()).find(w=>w.id===ids.video).border,'Sizable');
  assert(await video.evaluate(()=>video.paused));
  // Vault transitions must save then destroy every window from the previous library.
  run(mainHandle,'activate');await main.waitForTimeout(250);assert.equal(await main.evaluate(()=>native('createPrivate',{password:'isolated-preview-test-password'})),true);await main.waitForFunction(()=>state.isPrivate);assert.equal((await windows()).length,0);
  await main.evaluate(async()=>{await native('importSamples');await refresh();});const privateBook=await main.evaluate(()=>state.items.find(i=>i.ext==='EPUB').id);
  await main.evaluate(id=>openPreviewWindow(id,{play:false}),privateBook);const privatePage=await preview(privateBook);await privatePage.waitForSelector('#reader-text');
  await privatePage.locator('#notes-button').click();await privatePage.locator('#notes').fill('PRIVATE_WINDOW_TEST');
  await main.evaluate(()=>native('lockPrivate'));await main.waitForFunction(()=>!state.isPrivate);assert.equal((await windows()).length,0);assert.equal(await main.locator('.preview-window-tab').count(),0);
  await main.evaluate(()=>native('unlockPrivate',{password:'isolated-preview-test-password'}));await main.waitForFunction(()=>state.isPrivate);
  assert(await main.evaluate(()=>(state.items.some(i=>i.notes==='PRIVATE_WINDOW_TEST'))));await main.evaluate(()=>native('lockPrivate'));await main.waitForFunction(()=>!state.isPrivate);
  assert(!fs.readFileSync(path.join(profile,'private-vault/library.vault')).includes(Buffer.from('PRIVATE_WINDOW_TEST')));
  assert.deepEqual(remote,[]);assert.deepEqual(errors,[]);assert(await main.evaluate(()=>audio.paused));
  console.log('PASS: four independent native viewers, library interaction, drag regions, native move/resize, real stacking on blank double click, restore, notes on close, shared appearance/language, fullscreen and private-window lifecycle.');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

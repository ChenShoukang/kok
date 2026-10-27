'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {execFileSync}=require('node:child_process'),{chromium}=require('./browser.cjs');
(async()=>{
 const root=process.env.KOK_TEST_PROFILE,executable=process.env.KOK_TEST_EXECUTABLE,pid=Number(process.env.KOK_TEST_NATIVE_PID);
 assert(root&&executable&&pid,'Use the isolated desktop runner');
 const errors=[],remote=[],report={};let browser,page,closed=false;
 try{
  browser=await chromium.connectOverCDP('http://127.0.0.1:9225');const context=browser.contexts()[0];
  page=context.pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));page.setDefaultTimeout(15000);
  page.on('pageerror',error=>errors.push(error.stack||error.message));
  await context.route(/^https?:\/\//,route=>{if(['app.kok.local','media.kok.local'].includes(new URL(route.request().url()).hostname))return route.continue();remote.push(route.request().url());return route.abort();});
  await page.waitForFunction(()=>!$('#startup'));
  // Switch from a custom palette back to the active standard palette, without
  // inheriting inline colors left over from the previously selected theme.
  await page.evaluate(async()=>{await setting({theme:'dark',customColorsEnabled:true,customDarkBg:'#211c2b',customDarkGlass:'#514163'});applyTheme();});
  await page.waitForFunction(()=>getComputedStyle(document.body).backgroundColor==='rgb(33, 28, 43)');
  await page.evaluate(async()=>{await setting({theme:'light',customColorsEnabled:false});applyTheme();});
  assert.deepEqual(await page.evaluate(()=>({color:getComputedStyle(document.body).backgroundColor,inline:document.body.style.getPropertyValue('--bg'),custom:!!document.body.dataset.customColors})),{color:'rgb(233, 237, 242)',inline:'',custom:false});
  await page.evaluate(async()=>{await setting({theme:'dark'});applyTheme();});
  assert.equal(await page.evaluate(()=>getComputedStyle(document.body).backgroundColor),'rgb(22, 26, 32)');
  // Change the accessibility preference during an entrance, then simulate a
  // native resize during the next one. Neither operation may replay an entrance.
  await page.emulateMedia({reducedMotion:'no-preference'});
  await page.evaluate(()=>{settings();animateIn($('#small-dialog'))?.pause();});
  assert(await page.evaluate(()=>surfaceMotions.size>0));
  await page.emulateMedia({reducedMotion:'reduce'});
  await page.waitForFunction(()=>surfaceMotions.size===0);
  assert(await page.locator('#font-select').isVisible());
  assert.equal(await page.locator('#small-dialog').evaluate(el=>getComputedStyle(el).opacity),'1');
  await page.evaluate(()=>closeSmall());await page.emulateMedia({reducedMotion:'no-preference'});
  await page.evaluate(()=>{settings();animateIn($('#small-dialog'))?.pause();beginWindowInteraction(true,true);});
  await page.waitForFunction(()=>surfaceMotions.size===0);
  await page.evaluate(()=>{readingFlow.nativeGesture=false;finishWindowInteraction();});
  await page.evaluate(()=>closeSmall());
  report.interruptedAnimations=true;
  // Bundled fonts remain available inside the private folder. Its scope is
  // stated in both languages, without making an encryption claim about originals.
  await page.evaluate(async()=>{await setting({fontFamily:'wenkai'});await applyFont();});
  assert.match(await page.locator('.private-folder-card').innerText(),/原文件未加密/);
  await page.evaluate(()=>native('createPrivate',{password:'isolated-foundation-vault-password'}));
  await page.waitForFunction(()=>state.isPrivate&&state.route==='private'&&!document.body.classList.contains('private-locking'));
  await page.waitForFunction(()=>getComputedStyle(document.body).fontFamily.includes('LXGW WenKai Lite'));
  assert.equal(await page.evaluate(()=>state.settings.fontFamily),'wenkai');
  assert.match(await page.locator('.private-scope').innerText(),/原文件未加密/);
  await page.evaluate(()=>I18n.applyLanguage('en'));
  assert.match(await page.locator('.private-scope').innerText(),/original files.*not encrypted/i);
  await page.evaluate(()=>native('lockPrivate'));
  await page.waitForFunction(()=>!state.isPrivate&&!document.body.classList.contains('private-locking'));
  report.privateFontAndScope=true;
  // Exercise the actual native close path with a renderer that stops answering.
  // Only the process and files belonging to this isolated test are touched.
  await page.evaluate(()=>I18n.applyLanguage('zh'));
  const item=await page.evaluate(()=>native('createDocument',{name:'Close recovery',format:'md',collection:''}));
  await page.evaluate(async id=>{await refresh();await openCreativeEditor(id);},item.id);
  await page.waitForSelector('.managed-document-editor #creative-text',{state:'visible'});
  const text='Unsaved text survives an unresponsive renderer and a declined close.';
  await page.locator('#creative-text').fill(text);
  const handle=String((await page.evaluate(()=>native('diagnostics'))).window.handle);
  const helper=path.join(root,'SessionWindowHelper.exe');
  execFileSync(path.join(process.env.WINDIR,'Microsoft.NET','Framework64','v4.0.30319','csc.exe'),['/nologo','/out:'+helper,path.resolve('tests/SessionWindowHelper.cs')],{windowsHide:true,encoding:'utf8'});
  const control=action=>execFileSync(helper,[String(pid),handle,executable,action],{windowsHide:true,encoding:'utf8',timeout:12000});
  await page.evaluate(()=>{window.foundationSnapshot=window.kokSession;window.kokSession=()=>{const until=performance.now()+8000;while(performance.now()<until){}return window.foundationSnapshot();};});
  control('request-close');assert.match(control('decline-failed-close'),/Default No verified/);
  await page.evaluate(()=>{window.kokSession=window.foundationSnapshot;delete window.foundationSnapshot;});
  assert.equal(await page.locator('#creative-text').inputValue(),text);
  await page.waitForFunction(()=>managedTextSession(creative.session)&&!creative.session.documentDirty&&!creative.session.documentSaving);
  assert.equal(fs.readFileSync(item.path,'utf8'),text);
  assert.match(control('close-and-wait'),/Healthy close completed/);closed=true;
  report.failedCloseKeepsWindowAndText=true;report.retryCloses=true;
  assert.deepEqual(errors,[]);assert.deepEqual(remote,[]);
  fs.writeFileSync(path.join(root,'ui-foundation-report.json'),JSON.stringify(report,null,2));
  console.log('PASS: custom/theme reset, interrupted and reduced-motion entrances, private bundled font and bilingual encryption scope, real native save timeout/default No/text preservation/healthy retry. No external requests.');
 }catch(error){if(page&&!closed)try{await page.screenshot({path:path.join(root,'failure.png'),animations:'disabled',timeout:5000});}catch{}console.error('JS errors:',errors);throw error;}
 finally{if(browser)try{await browser.close();}catch{}}
})().catch(error=>{console.error(error);process.exitCode=1;});

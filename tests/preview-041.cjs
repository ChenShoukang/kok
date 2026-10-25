'use strict';
const fs=require('node:fs');
const path=require('node:path');
const http=require('node:http');
const crypto=require('node:crypto');
const assert=require('node:assert/strict');
const {chromium}=require('./browser.cjs');

function pdfFixture(){
 const objects=['<< /Type /Catalog /Pages 2 0 R >>','<< /Type /Pages /Kids [3 0 R 5 0 R 7 0 R] /Count 3 >>'];
 for(let n=1;n<=3;n++){const text=`BT /F1 24 Tf 60 700 Td (Workspace page ${n}) Tj ET`;objects.push(`<< /Type /Page /Parent 2 0 R /MediaBox [0 0 600 800] /Resources << /Font << /F1 9 0 R >> >> /Contents ${objects.length+2} 0 R >>`,`<< /Length ${text.length} >>\nstream\n${text}\nendstream`);}
 objects.push('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>');let content='%PDF-1.4\n',offsets=[0];objects.forEach((value,index)=>{offsets.push(Buffer.byteLength(content));content+=`${index+1} 0 obj\n${value}\nendobj\n`;});const xref=Buffer.byteLength(content);content+=`xref\n0 ${objects.length+1}\n0000000000 65535 f \n`+offsets.slice(1).map(offset=>String(offset).padStart(10,'0')+' 00000 n \n').join('')+`trailer\n<< /Size ${objects.length+1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF`;return Buffer.from(content);
}
function prepare(profile){
 const fixtures=path.join(profile,'fixtures');fs.mkdirSync(fixtures,{recursive:true});
 const reading=path.join(fixtures,'local-reading.txt'),pdf=path.join(fixtures,'local-search.pdf');
 const paragraph='A quiet page leaves room for a thought. Original words stay untouched, and a bookmark brings us back.\n\n';
 fs.writeFileSync(reading,'Chapter 1 First light\n'+paragraph.repeat(110)+'\nChapter 2 A place to return\nBEACON_041 appears only in this chapter.\n\n'+paragraph.repeat(100)+'\nChapter 3 Keeping a note\n'+paragraph.repeat(80));fs.writeFileSync(pdf,pdfFixture());
 const db=JSON.parse(fs.readFileSync(path.join(profile,'library.json'),'utf8'));
 db.items=[{id:'041'.padEnd(32,'0'),path:reading,name:'Local reading fixture',kind:'book',ext:'TXT',size:fs.statSync(reading).size,annotations:[{id:'preserve-note',style:'underline',text:'A quiet',note:'A local thought',chapter:0,start:0,end:7,page:0,rects:[]}]},{id:'041'.padEnd(31,'0')+'1',path:pdf,name:'Searchable PDF',kind:'document',ext:'PDF',size:fs.statSync(pdf).size}];
 fs.writeFileSync(path.join(profile,'library.json'),JSON.stringify(db));fs.writeFileSync(path.join(profile,'downloads.json'),JSON.stringify({folder:path.join(profile,'downloaded'),ffmpegFolder:'',jobs:[]}));

}
if(process.argv[2]==='--prepare'){prepare(process.argv[3]);console.log('Prepared isolated v0.4.1 fixtures.');}
else (async()=>{
 const profile=process.env.KOK_TEST_PROFILE;if(!profile)throw Error('Run through tools/test-desktop.ps1 -Suites preview-041.');
 const output=path.join(profile,'captures');fs.mkdirSync(output,{recursive:true});
 const original=fs.readFileSync(path.resolve('samples/一段安静的时光.webm'));let ranges=0;const intervals=new Set();
 const server=http.createServer((req,res)=>{
   const range=/bytes=(\d+)-/.exec(req.headers.range||'');let at=range?Number(range[1]):0;if(range)ranges++;
   if(at>=original.length){res.writeHead(416,{'Content-Range':`bytes */${original.length}`});res.end();return;}
   res.writeHead(range?206:200,{'Content-Type':'video/webm','Content-Length':original.length-at,'Accept-Ranges':'bytes','ETag':'"preview-local"',...(range?{'Content-Range':`bytes ${at}-${original.length-1}/${original.length}`}:{})});
   if(req.method==='HEAD'){res.end();return;}
   const timer=setInterval(()=>{if(at>=original.length){clearInterval(timer);intervals.delete(timer);res.end();return;}const next=Math.min(original.length,at+1024);res.write(original.subarray(at,next));at=next;},80);intervals.add(timer);res.on('close',()=>{clearInterval(timer);intervals.delete(timer);});
 });await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 let browser,page;const errors=[];
 try{
   browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));page.on('pageerror',e=>errors.push(e.message));page.setDefaultTimeout(30000);await page.setViewportSize({width:1380,height:900});
   await page.waitForFunction(()=>!document.querySelector('#startup'));assert.equal((await page.evaluate(()=>native('diagnostics'))).version,'0.4.1');
   const change=async(selector,value)=>page.locator(selector).evaluate((input,value)=>{input.value=String(value);input.dispatchEvent(new Event('input',{bubbles:true}));input.dispatchEvent(new Event('change',{bubbles:true}));},value);
   const english=async selector=>{const missing=await page.locator(selector).evaluate(root=>{const found=[];const walker=document.createTreeWalker(root,NodeFilter.SHOW_TEXT);let node;while(node=walker.nextNode()){if(node.parentElement.getClientRects().length&&!I18n.isProtected(node.parentElement)&&!node.parentElement.closest('script,style,svg,textarea')&&/[\u3400-\u9fff]/.test(node.nodeValue))found.push(node.nodeValue.trim());}return found;});assert.deepEqual(missing,[],'Visible interface text must have an English translation');};
   if(!['pdf','downloads'].includes(process.env.KOK_PREVIEW_FROM)){
   await page.evaluate(()=>settings());await page.locator('[data-open-appearance]').click();await change('[data-color-setting=customLightBg]','#e8eef8');await change('[data-color-setting=customLightGlass]','#d9d2f4');await change('[data-color-setting=glassOpacity]',55);await change('[data-color-setting=glassBlur]',28);
   await page.waitForFunction(()=>getComputedStyle(document.body).backgroundColor==='rgb(232, 238, 248)');assert.match(await page.locator('.sidebar').evaluate(el=>getComputedStyle(el).backdropFilter),/28px/);
   await page.screenshot({path:path.join(output,'appearance-light.png'),scale:'css'});
   await page.evaluate(async()=>{await closeSmall();await setting({theme:'dark',customDarkBg:'#20212a',customDarkGlass:'#3c354c'});applyTheme();I18n.applyLanguage('en');await settings();});await page.locator('[data-open-appearance]').click();assert.equal(await page.locator('[data-color-setting=customDarkBg]').inputValue(),'#20212a');await english('#small-dialog');await page.screenshot({path:path.join(output,'appearance-dark-en.png'),scale:'css'});
   const saved=await page.evaluate(()=>native('library'));assert.equal(saved.settings.customLightBg,'#e8eef8');assert.equal(saved.settings.customDarkBg,'#20212a');
   await page.evaluate(async()=>{await closeSmall();await setting({theme:'light',uiLanguage:'zh',readerLayout:'scroll'});applyTheme();I18n.applyLanguage('zh');const item=state.items.find(i=>i.ext==='TXT');await native('update',{id:item.id,patch:{bookmarks:[]}});item.bookmarks=[];await openItem(item.id);});await page.waitForSelector('#reader-text');
   assert(await page.locator('#reader-text .text-mark').count());assert.equal(await page.locator('#reader-text').textContent(),await page.evaluate(()=>state.book.chapters[state.chapter].text));
   const searchChapter=await page.evaluate(()=>state.book.chapters.findIndex(c=>c.text.includes('BEACON_041')));await page.evaluate(target=>changeChapter(target===0?1:0),searchChapter);
   await page.locator('[data-reading-tab=search]').click();await page.locator('#reading-full-query').fill('BEACON_041');await page.waitForSelector('[data-reading-result="0"]');await page.locator('[data-reading-result="0"]').click();await page.waitForFunction(target=>state.chapter===target&&CSS.highlights.get('kok-global-find')?.size>0,searchChapter);
   await page.locator('.reader-tools [data-add-bookmark]').click();await page.locator('[data-reading-tab=bookmarks]').click();assert.equal(await page.locator('[data-open-bookmark]').count(),1);await page.screenshot({path:path.join(output,'reading-search-bookmarks.png'),scale:'css'});
   await page.locator('[data-reading-settings]').click();await page.locator('[data-reading-layout=paged]').click();await change('[data-reader-setting=readerLineHeight]',1.65);await page.locator('[data-close-dialog]').click();await page.waitForFunction(()=>!$('#small-dialog').open&&readerPaged()&&$('#reader-scroll').scrollWidth>$('#reader-scroll').clientWidth);
   await page.evaluate(()=>restoreReaderPosition(0));await page.keyboard.press('ArrowRight');await page.waitForFunction(()=>$('#reader-scroll').scrollLeft>100);assert.equal(await page.evaluate(()=>state.chapter),searchChapter);assert(await page.locator('#reader-scroll').evaluate(el=>el.scrollHeight<=el.clientHeight+2));
   await page.evaluate(()=>I18n.applyLanguage('en'));await english('.reader-tools');await page.screenshot({path:path.join(output,'reading-paged-en.png'),scale:'css'});
   const oldPosition=await page.evaluate(async()=>{await saveReader();return readerPosition();});const bookId=await page.evaluate(()=>state.viewer.id);await page.evaluate(async id=>{await closeViewer();await openItem(id);},bookId);await page.waitForFunction(()=>readerPaged()&&$('#reader-scroll')?.scrollWidth>$('#reader-scroll').clientWidth);await page.waitForTimeout(250);assert(Math.abs(await page.evaluate(()=>readerPosition())-oldPosition)<.12);
   }
   if(process.env.KOK_PREVIEW_FROM!=='downloads'){
   await page.evaluate(async()=>{if(viewer.open)await closeViewer();await setting({customColorsEnabled:false,theme:'light',uiLanguage:'en'});applyTheme();I18n.applyLanguage('en');await openItem(state.items.find(i=>i.ext==='PDF').id);});await page.waitForSelector('[data-reading-sidebar]');await page.locator('[data-reading-sidebar]').click();await page.locator('[data-reading-tab=search]').click();await page.locator('#reading-full-query').fill('Workspace page 3');await page.waitForSelector('[data-reading-result="0"]');await page.locator('[data-reading-result="0"]').click();await page.waitForFunction(()=>$('#pdf-page-input').value==='3');await page.locator('.pdf-extra-tools [data-add-bookmark], .pdf-tools [data-add-bookmark]').first().click();assert.equal((await page.evaluate(()=>state.viewer.bookmarks)).length,1);await english('.kok-pdf-sidebar');await page.screenshot({path:path.join(output,'pdf-search-en.png'),scale:'css'});
   console.log('PASS: live glass colors, separate palettes, full-text book/PDF search, persistent bookmarks, pagination, keyboard controls and English UI.');
   }
   await page.evaluate(async()=>{if(viewer.open)await closeViewer();await setting({customColorsEnabled:false,theme:'light',uiLanguage:'en'});applyTheme();I18n.applyLanguage('en');navigate('downloads');});await page.waitForFunction(()=>downloadSnapshot?.downloads);await page.locator('#download-urls').fill(`http://127.0.0.1:${server.address().port}/original-clip.webm`);await page.locator('[data-download-probe]').click();await page.waitForSelector('.download-preview-card',{timeout:60000});await page.locator('[data-start-download]').click();
   await page.waitForFunction(()=>downloadSnapshot.downloads.jobs.some(j=>j.status==='downloading'&&j.received>12288),{},{timeout:60000});await page.locator('[data-download-command=pauseDownload]').first().click();await page.waitForFunction(()=>downloadSnapshot.downloads.jobs[0].status==='paused');await page.locator('[data-download-command=resumeDownload]').first().click();await page.waitForFunction(()=>downloadSnapshot.downloads.jobs[0].status==='completed'&&downloadSnapshot.downloads.jobs[0].imported,{},{timeout:90000});
   assert(ranges>0,'Resuming must issue a byte-range request');const history=JSON.parse(fs.readFileSync(path.join(profile,'downloads.json'),'utf8'));const file=history.jobs[0].files[0];assert.equal(crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex'),crypto.createHash('sha256').update(original).digest('hex'));await english('.download-workspace');await page.screenshot({path:path.join(output,'downloads-en.png'),scale:'css'});
   await page.setViewportSize({width:840,height:620});assert(await page.locator('#main').evaluate(el=>el.scrollWidth<=el.clientWidth+2));await page.screenshot({path:path.join(output,'downloads-compact.png'),scale:'css'});
   await page.evaluate(async()=>{await native('setAppMode',{mode:'pure'});await refresh();});assert.equal(await page.locator('[data-route=downloads]').count(),0);const blocked=await page.evaluate(()=>native('downloadState').then(()=>false).catch(()=>true));assert(blocked);assert.equal(await page.evaluate(()=>audio.paused),true);assert.deepEqual(errors,[]);
   console.log('PASS: native downloads against an original local video, metadata, pause/range-resume, byte-perfect file, library import, compact layout and Pure-mode blocking.');console.log('Captures: '+output);
 }finally{if(browser)await browser.close();for(const timer of intervals)clearInterval(timer);server.closeAllConnections();await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});

'use strict';
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const {chromium}=require('./browser.cjs');
const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const hash=buffer=>crypto.createHash('sha256').update(buffer).digest('hex');
if(process.argv[2]==='--prepare'){
 const profile=process.argv[3];fs.writeFileSync(path.join(profile,'downloads.json'),JSON.stringify({folder:path.join(profile,'downloaded'),jobs:[]}));
 console.log('Prepared native downloads with no optional components.');
}else (async()=>{
 const profile=process.env.KOK_TEST_PROFILE;if(!profile)throw Error('Run tools/test-desktop.ps1 -Suites native-downloads.');
 const video=fs.readFileSync(path.resolve('samples/一段安静的时光.webm')),audio=fs.readFileSync(path.resolve('samples/午后留白.wav')),changed=Buffer.from(audio);changed[changed.length-100]^=17;
 const requests=[],timers=new Set();let version=1;
 const server=http.createServer((req,res)=>{
   const url=new URL(req.url,'http://localhost'),p=url.pathname;
   requests.push({path:p,range:req.headers.range,ifRange:req.headers['if-range'],auth:req.headers.authorization,cookie:req.headers.cookie});
   if(p==='/redirect'){res.writeHead(302,{Location:'/opaque?token=fixture-private-query'});res.end();return;}
   if(p==='/html.mp4'){res.writeHead(200,{'Content-Type':'text/html'});res.end('<html>fixture-private-query</html>');return;}
   const body=p.endsWith('.wav')?(version===1?audio:changed):video;
   const range=/bytes=(\d+)-(\d*)/.exec(req.headers.range||''),requested=range?Number(range[1]):0;
   if(p==='/range416.webm'&&requested>0){res.writeHead(416,{'Content-Range':`bytes */${body.length}`});res.end();return;}
   const etag=p==='/changing.wav'?`"audio-${version}"`:'"local-media-1"';
   const partial=range&&p!=='/no-range.webm'&&(!req.headers['if-range']||req.headers['if-range']===etag);
   let from=partial?requested:0,end=partial&&range[2]?Math.min(Number(range[2]),body.length-1):body.length-1;
   const headers={'Content-Type':p.endsWith('.wav')?'audio/wav':'video/webm','ETag':etag,'Accept-Ranges':'bytes'};
   if(p==='/opaque')headers['Content-Disposition']="attachment; filename*=UTF-8''%E4%B8%AD%E6%96%87%E5%BD%B1%E7%89%87.webm";
   if(p==='/no-validator.webm'){delete headers.ETag;delete headers['Accept-Ranges'];}
   if(p!=='/chunked.webm')headers['Content-Length']=end-from+1;
   if(partial)headers['Content-Range']=`bytes ${p==='/bad-range.webm'&&from>0?from+1:from}-${end}/${p==='/huge.webm'?8589934592:body.length}`;
   res.writeHead(partial?206:200,headers);
   const slow=/slow|no-range|changing|bad-range|no-validator|range416|queue-first/.test(p)&&!range?.[2];
   if(!slow){res.end(body.subarray(from,end+1));return;}
   const timer=setInterval(()=>{if(from>end){res.end();clearInterval(timer);timers.delete(timer);return;}const next=Math.min(end+1,from+4096);res.write(body.subarray(from,next));from=next;},30);timers.add(timer);
   res.on('close',()=>{clearInterval(timer);timers.delete(timer);});
 });await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 let browser,page;const errors=[];const base=`http://127.0.0.1:${server.address().port}`;
 try{
   browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));page.on('pageerror',e=>errors.push(e.message));page.setDefaultTimeout(20000);await page.setViewportSize({width:1280,height:900});
   await page.waitForFunction(()=>!document.querySelector('#startup'));await page.evaluate(()=>navigate('downloads'));
   await page.waitForFunction(()=>downloadSnapshot?.downloads);assert.equal(await page.locator('[data-download-backend]').count(),0);assert(!('engineInstalled' in await page.evaluate(()=>downloadSnapshot.downloads.tools)));
   const call=(action,args={})=>page.evaluate(({action,args})=>native(action,args),{action,args});
   const state=async()=> (await call('downloadState')).downloads;
   const wait=async(predicate,label)=>{const deadline=Date.now()+25000;while(Date.now()<deadline){const d=await state(),result=predicate(d);if(result)return result;await delay(90);}throw Error('Timed out: '+label);};
   const job=async(id,predicate)=>wait(d=>{const j=d.jobs.find(j=>j.id===id);return j&&predicate(j)?j:false;},id);
   const start=async urls=>{const before=new Set((await state()).jobs.map(j=>j.id));await call('startDownloads',{urls:urls.map(p=>base+p),backend:'native',quality:'original',subtitles:false});return (await state()).jobs.filter(j=>!before.has(j.id));};
   const paused=async id=>{await job(id,j=>j.status==='downloading'&&j.received>8192);await call('pauseDownload',{id});await job(id,j=>j.status==='paused');await delay(80);};
   const finished=async(id,expected)=>{await job(id,j=>j.status==='completed'&&j.imported);const store=JSON.parse(fs.readFileSync(path.join(profile,'downloads.json'),'utf8')),record=store.jobs.find(j=>j.id===id);assert.equal(hash(fs.readFileSync(record.files[0])),hash(expected));return record;};
   await page.locator('#download-urls').fill(base+'/slow.webm');assert.equal(await page.locator('[data-start-download]').isEnabled(),true);await page.locator('[data-download-probe]').click();await page.waitForSelector('.download-preview-card');assert.match(await page.locator('.download-preview-card').innerText(),/支持断点续传/);
   await page.locator('[data-start-download]').click();const first=await wait(d=>d.jobs[0],'first job');await paused(first.id);
   const partial=JSON.parse(fs.readFileSync(path.join(profile,'downloads.json'),'utf8')).jobs[0];const meta=fs.readFileSync(path.join(partial.folder,'native-resume.json'),'utf8');assert(!meta.includes('http://'));assert(fs.statSync(path.join(partial.folder,'native-media.part')).size>0);
   await call('resumeDownload',{id:first.id});await finished(first.id,video);assert(requests.some(r=>r.path==='/slow.webm'&&/^bytes=[1-9]/.test(r.range||'')&&r.ifRange==='"local-media-1"'));
   console.log('PASS: native UI without components, probe, pause, validated byte-range resume and byte-perfect library import.');
   for(const route of ['/no-range.webm','/bad-range.webm','/no-validator.webm','/range416.webm']){
     const [j]=await start([route]);await paused(j.id);await call('resumeDownload',{id:j.id});const record=await finished(j.id,video);assert(record.notice,'Safe restart should be explained');
   }
   const [changing]=await start(['/changing.wav']);await paused(changing.id);version=2;await call('resumeDownload',{id:changing.id});const changedRecord=await finished(changing.id,changed);assert(changedRecord.sourceIsAudio);const db=await call('library');assert.equal(db.items.find(i=>i.path===changedRecord.files[0]).kind,'music');
   const [redirect]=await start(['/redirect']);const redirected=await finished(redirect.id,video);assert.equal(path.basename(redirected.files[0]),'中文影片.webm');
   const [chunked]=await start(['/chunked.webm']);await finished(chunked.id,video);
   const huge=await call('probeDownload',{url:base+'/huge.webm',backend:'native'});assert.equal(huge.bytes,8589934592);
   const rejected=await call('probeDownload',{url:base+'/html.mp4?token=fixture-private-query',backend:'native'}).then(()=>null,e=>String(e));assert(rejected&&!rejected.includes('fixture-private-query'));
   console.log('PASS: ignored/invalid ranges, missing validators, 416 restart, changed files, audio classification, UTF-8 names, redirects, chunked transfer, 8 GiB metadata and HTML rejection.');
   const queued=await start(['/queue-first.webm','/queue-next.webm','/queue-cancel.webm']);const blocked=queued[0],next=queued[1],leading=queued[2];await job(leading.id,j=>j.status==='downloading'&&j.received>0);await call('cancelDownload',{id:blocked.id});await job(blocked.id,j=>j.status==='cancelled');assert(!requests.some(r=>r.path==='/queue-cancel.webm'));await call('pauseDownload',{id:leading.id});await job(leading.id,j=>j.status==='paused');await finished(next.id,video);
   await page.evaluate(async()=>{await setting({uiLanguage:'en'});I18n.applyLanguage('en');await loadDownloadState();});
   await page.waitForFunction(()=>![...document.querySelectorAll('.download-job-meta')].some(el=>/[\u3400-\u9fff]/.test(el.textContent)));
   const missing=await page.locator('.download-workspace').evaluate(root=>{const values=[],walker=document.createTreeWalker(root,NodeFilter.SHOW_TEXT);let n;while(n=walker.nextNode())if(n.parentElement.getClientRects().length&&!I18n.isProtected(n.parentElement)&&!n.parentElement.closest('svg,textarea,script,style')&&/[\u3400-\u9fff]/.test(n.nodeValue))values.push(n.nodeValue.trim());return values;});assert.deepEqual(missing,[]);
   const captures=path.join(profile,'captures');fs.mkdirSync(captures,{recursive:true});await page.screenshot({path:path.join(captures,'native-downloads-en.png'),scale:'css'});
   await page.setViewportSize({width:840,height:620});assert(await page.locator('#main').evaluate(el=>el.scrollWidth<=el.clientWidth+2));await page.screenshot({path:path.join(captures,'native-downloads-compact.png'),scale:'css'});
   await page.evaluate(async()=>{await native('setAppMode',{mode:'pure'});await refresh();});assert(await call('startDownloads',{urls:[base+'/pure-blocked.webm'],backend:'native',quality:'original'}).then(()=>false,()=>true));assert(!requests.some(r=>r.path==='/pure-blocked.webm'));
   assert(requests.every(r=>!r.auth&&!r.cookie));assert(!fs.existsSync(path.join(profile,'download-components','yt-dlp.exe')));assert.equal(await page.evaluate(()=>audio.paused),true);assert.deepEqual(errors,[]);
   console.log('PASS: ordered queue, queued cancellation, English UI, compact layout, Pure mode network blocking, no cookies/credentials/components and silent output.');
 }finally{if(browser)await browser.close();for(const timer of timers)clearInterval(timer);server.closeAllConnections();await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});

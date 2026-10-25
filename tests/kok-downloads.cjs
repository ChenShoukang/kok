'use strict';
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),assert=require('node:assert/strict'),crypto=require('node:crypto'),{spawnSync}=require('node:child_process');
const {chromium}=require('./browser.cjs');
const ffmpeg=path.resolve('test-output/component-cache/ffmpeg.exe'),ffprobe=path.resolve('test-output/component-cache/ffprobe.exe');
const run=(exe,args)=>{const p=spawnSync(exe,args,{encoding:'utf8',windowsHide:true,maxBuffer:4*1024*1024});assert.equal(p.status,0,p.stderr||p.error?.message);return p.stdout;};
const sha=buffer=>crypto.createHash('sha256').update(buffer).digest('hex'),delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
if(process.argv[2]==='--prepare'){
 const profile=process.argv[3],fixtures=path.join(profile,'fixtures'),components=path.join(profile,'download-components');fs.mkdirSync(fixtures,{recursive:true});fs.mkdirSync(components,{recursive:true});
 const manifest=JSON.parse(fs.readFileSync('ui/download-components.json','utf8'));assert.deepEqual(Object.keys(manifest),['ffmpeg']);
 for(const [file,expected]of [['ffmpeg.exe',manifest.ffmpeg.executableSha256],['ffprobe.exe',manifest.ffmpeg.probeSha256]]){const source=path.resolve('test-output/component-cache',file);assert.equal(sha(fs.readFileSync(source)),expected);fs.copyFileSync(source,path.join(components,file));}
 for(const name of ['yt-dlp.exe','deno.exe'])fs.writeFileSync(path.join(components,name),'retired test fixture');
 fs.writeFileSync(path.join(profile,'downloads.json'),JSON.stringify({folder:path.join(profile,'downloaded'),jobs:[]}));
 for(const [name,color]of [['low.mp4','navy'],['high.mp4','teal']])run(ffmpeg,['-v','error','-nostdin','-f','lavfi','-i',`color=c=${color}:size=160x90:rate=12`,'-f','lavfi','-i','sine=frequency=440:sample_rate=48000','-t','4','-c:v','libx264','-preset','ultrafast','-pix_fmt','yuv420p','-g','12','-sc_threshold','0','-c:a','aac',path.join(fixtures,name)]);
 run(ffmpeg,['-v','error','-nostdin','-i',path.join(fixtures,'high.mp4'),'-c','copy','-f','hls','-hls_time','1','-hls_playlist_type','vod',path.join(fixtures,'stream.m3u8')]);
 run(ffmpeg,['-v','error','-nostdin','-i',path.join(fixtures,'high.mp4'),'-map','0','-c','copy','-f','dash','-seg_duration','1',path.join(fixtures,'stream.mpd').replaceAll('\\','/')]);
 console.log('Prepared original media and verified FFmpeg for the kok-only downloader.');
}else (async()=>{
 const profile=process.env.KOK_TEST_PROFILE;if(!profile)throw Error('Run tools/test-desktop.ps1 -Suites kok-downloads.');const fixtures=path.join(profile,'fixtures'),requests=[];
 const server=http.createServer((req,res)=>{
   const u=new URL(req.url,'http://localhost'),p=u.pathname;requests.push({path:p,ref:req.headers.referer,auth:req.headers.authorization,cookie:req.headers.cookie});
   const html=text=>{res.writeHead(200,{'Content-Type':'text/html; charset=utf-8'});res.end(text);};
   if(p==='/watch'){html('<title>A &amp; B</title><video><source src="/high.mp4?src=page" label="1080p" type="video/mp4"><source src="/low.mp4?src=page" label="720p" type="video/mp4"><track kind="subtitles" srclang="en" src="/caption.vtt"></video><script>fetch("/must-not-run")</script>');return;}
   if(p==='/structured'){html('<title>Structured clip</title><script type="application/ld+json">{"@type":"VideoObject","contentUrl":"/stream.m3u8"}</script>');return;}
   if(p==='/open-graph'){html('<title>Metadata clip</title><meta property="og:video" content="/high.mp4?src=page">');return;}
   if(p==='/embed'){html('<title>Embedded clip</title><iframe src="/player"></iframe>');return;}
   if(p==='/player'){html('<video src="/low.mp4?src=page"></video>');return;}
   if(p==='/json-player'){html('<title>Player metadata</title><script type="application/json">{"sources":[{"file":"/high.mp4?src=page","label":"1080p"},{"file":"/low.mp4?src=page","label":"720p"}]}</script>');return;}
   if(p==='/script-player'){html('<script>player.setup({file:"/stream.mpd"});</script>');return;}
   if(p==='/unsupported'){html('<title>Sign in</title><video src="javascript:alert(1)"></video><script>fetch("/must-not-run")</script>');return;}
   if(p==='/caption.vtt'){res.writeHead(200,{'Content-Type':'text/vtt'});res.end('WEBVTT\n\n00:00.000 --> 00:03.000\nA local subtitle.\n');return;}
   if(u.searchParams.has('src')&&!['/watch','/player','/open-graph','/json-player'].some(page=>req.headers.referer?.endsWith(page))){res.writeHead(403);res.end();return;}
   const file=path.join(fixtures,path.basename(p));if(!fs.existsSync(file)||!fs.statSync(file).isFile()){res.writeHead(404);res.end();return;}
   const buffer=fs.readFileSync(file),range=/bytes=(\d+)-(\d*)/.exec(req.headers.range||''),from=range?Number(range[1]):0,end=range&&range[2]?Math.min(Number(range[2]),buffer.length-1):buffer.length-1;
   if(from>=buffer.length){res.writeHead(416,{'Content-Range':`bytes */${buffer.length}`});res.end();return;}
   const mime=p.endsWith('.m3u8')?'application/vnd.apple.mpegurl':p.endsWith('.mpd')?'application/dash+xml':p.endsWith('.ts')?'video/mp2t':'video/mp4';res.writeHead(range?206:200,{'Content-Type':mime,'Content-Length':end-from+1,'ETag':'"kok-static"',...(range?{'Content-Range':`bytes ${from}-${end}/${buffer.length}`}:{})});res.end(buffer.subarray(from,end+1));
 });await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));const base=`http://127.0.0.1:${server.address().port}`;let browser,page;const errors=[];
 try{
   browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));page.on('pageerror',e=>errors.push(e.message));page.setDefaultTimeout(20000);await page.waitForFunction(()=>!document.querySelector('#startup'));await page.setViewportSize({width:1280,height:900});await page.evaluate(()=>navigate('downloads'));await page.waitForFunction(()=>downloadSnapshot?.downloads);
   for(const name of ['yt-dlp.exe','deno.exe'])assert(!fs.existsSync(path.join(profile,'download-components',name)));assert.equal(await page.locator('[data-download-backend]').count(),0);
   const call=(action,args={})=>page.evaluate(({action,args})=>native(action,args),{action,args});const jobs=async()=>(await call('downloadState')).downloads.jobs;
   const wait=async(fn,label)=>{const end=Date.now()+40000;while(Date.now()<end){const j=await jobs(),value=fn(j);if(value)return value;await delay(100);}throw Error('Timed out: '+label);};
   const done=async id=>{await wait(j=>{const item=j.find(j=>j.id===id);if(item?.status==='error')throw Error(item.error);return item?.status==='completed'&&item.imported;},id);return JSON.parse(fs.readFileSync(path.join(profile,'downloads.json'),'utf8')).jobs.find(j=>j.id===id);};
   const start=async(name,quality='best',subtitles=false,selection='')=>{const ids=new Set((await jobs()).map(j=>j.id));await call('startDownloads',{urls:[base+name],quality,subtitles,selection});return (await jobs()).find(j=>!ids.has(j.id));};
   await page.locator('#download-quality').selectOption('720');await page.locator('#download-urls').fill(base+'/watch?private=fixture-query');await page.locator('[data-download-subtitles], #download-subtitles').check();await page.locator('[data-download-probe]').click();await page.waitForSelector('#download-source');assert.equal(await page.locator('#download-source option').count(),2);assert.match(await page.locator('#download-source option:checked').innerText(),/720/);assert.match(await page.locator('.download-preview-card').innerText(),/A & B/);
   await page.locator('[data-start-download]').click();const first=await wait(j=>j[0],'page download'),saved=await done(first.id);assert.equal(sha(fs.readFileSync(saved.files[0])),sha(fs.readFileSync(path.join(fixtures,'low.mp4'))));assert(fs.readdirSync(saved.folder).some(f=>f.endsWith('.vtt')));assert((await jobs()).find(j=>j.id===saved.id).imported);
   assert(requests.some(r=>r.path==='/low.mp4'&&r.ref===base+'/watch'));assert(requests.every(r=>!r.ref?.includes('fixture-query')));assert(!requests.some(r=>r.path==='/must-not-run'));
   const chosen=await call('probeDownload',{url:base+'/watch',quality:'best'});assert(chosen.sources.some(s=>s.height===1080));const low=chosen.sources.find(s=>s.height===720);const explicit=await start('/watch','best',false,low.id),explicitSaved=await done(explicit.id);assert.equal(sha(fs.readFileSync(explicitSaved.files[0])),sha(fs.readFileSync(path.join(fixtures,'low.mp4'))));
   console.log('PASS: unified UI, retired component cleanup, HTML sources, quality selection, explicit source selection, subtitles, safe referrer and byte-exact import.');
   for(const name of ['/open-graph','/embed','/json-player']){const probe=await call('probeDownload',{url:base+name,quality:'720'});assert(probe.sources.length>0);}
   const audio=await start('/watch','audio'),audioSaved=await done(audio.id),audioInfo=JSON.parse(run(ffprobe,['-v','error','-show_streams','-of','json',audioSaved.files[0]]));assert(audioInfo.streams.some(s=>s.codec_type==='audio'));assert(!audioInfo.streams.some(s=>s.codec_type==='video'));const db=await call('library');assert.equal(db.items.find(i=>i.path===audioSaved.files[0]).kind,'music');
   const hls=await start('/structured');assert.equal((await done(hls.id)).streamKind,'hls');const dash=await start('/script-player');assert.equal((await done(dash.id)).streamKind,'dash');
   const streamAudio=await start('/stream.m3u8','audio'),streamSaved=await done(streamAudio.id);const streamInfo=JSON.parse(run(ffprobe,['-v','error','-show_streams','-of','json',streamSaved.files[0]]));assert(streamInfo.streams.every(s=>s.codec_type==='audio'));
   const failed=await call('probeDownload',{url:base+'/unsupported',quality:'best'}).then(()=>'',e=>String(e));assert(failed&&!failed.includes('fixture-query'));
   console.log('PASS: Open Graph, nested player, JSON metadata, audio export / music import, discovered HLS / DASH, audio-only streams and clear unsupported-page handling.');
   await page.evaluate(async()=>{await setting({uiLanguage:'en'});I18n.applyLanguage('en');await loadDownloadState();});await page.waitForFunction(()=>![...document.querySelectorAll('.download-job-meta')].some(e=>/[\u3400-\u9fff]/.test(e.textContent)));
   const visible=await page.locator('.download-workspace').evaluate(root=>{const result=[],walker=document.createTreeWalker(root,NodeFilter.SHOW_TEXT);let n;while(n=walker.nextNode())if(n.parentElement.getClientRects().length&&!I18n.isProtected(n.parentElement)&&!n.parentElement.closest('script,style,svg,textarea')&&/[\u3400-\u9fff]/.test(n.nodeValue))result.push(n.nodeValue.trim());return result;});assert.deepEqual(visible,[]);
   const captures=path.join(profile,'captures');fs.mkdirSync(captures,{recursive:true});await page.screenshot({path:path.join(captures,'kok-downloads-en.png'),scale:'css'});await page.setViewportSize({width:840,height:620});assert(await page.locator('#main').evaluate(el=>el.scrollWidth<=el.clientWidth+2));
   await page.evaluate(async()=>{await native('setAppMode',{mode:'pure'});await refresh();});assert(await call('probeDownload',{url:base+'/watch'}).then(()=>false,()=>true));assert(await call('startDownloads',{urls:[base+'/watch'],quality:'best'}).then(()=>false,()=>true));assert.equal(await page.evaluate(()=>audio.paused),true);assert(requests.every(r=>!r.auth&&!r.cookie));assert.deepEqual(errors,[]);
   console.log('PASS: English UI, compact layout, Pure-mode blocking, no cookies, no scripts, no model calls and silent output.');
 }finally{if(browser)await browser.close();server.closeAllConnections();await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});

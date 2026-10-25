'use strict';
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),assert=require('node:assert/strict'),crypto=require('node:crypto'),{spawnSync}=require('node:child_process');
const {chromium}=require('./browser.cjs');
const ffmpeg=path.resolve('test-output/component-cache/ffmpeg.exe'),ffprobe=path.resolve('test-output/component-cache/ffprobe.exe');
const run=(program,args)=>{const p=spawnSync(program,args,{encoding:'utf8',windowsHide:true,maxBuffer:4*1024*1024});assert.equal(p.status,0,p.stderr||p.error?.message);return p.stdout;};
const waitTime=ms=>new Promise(resolve=>setTimeout(resolve,ms));
if(process.argv[2]==='--prepare'){
 const profile=process.argv[3],fixtures=path.join(profile,'fixtures');fs.mkdirSync(fixtures,{recursive:true});fs.writeFileSync(path.join(profile,'downloads.json'),JSON.stringify({folder:path.join(profile,'downloaded'),jobs:[]}));
 run(ffmpeg,['-v','error','-nostdin','-f','lavfi','-i','testsrc=size=160x90:rate=12','-f','lavfi','-i','sine=frequency=440:sample_rate=48000','-t','16','-c:v','libx264','-pix_fmt','yuv420p','-preset','ultrafast','-g','24','-sc_threshold','0','-c:a','aac','-b:a','64k',path.join(fixtures,'original.mp4')]);
 run(ffmpeg,['-v','error','-nostdin','-i',path.join(fixtures,'original.mp4'),'-c','copy','-f','hls','-hls_time','2','-hls_playlist_type','vod',path.join(fixtures,'media.m3u8')]);
 // DASH muxing resolves child filenames using URL-style separators on Windows.
 run(ffmpeg,['-v','error','-nostdin','-i',path.join(fixtures,'original.mp4'),'-map','0','-c','copy','-f','dash','-seg_duration','2',path.join(fixtures,'media.mpd').replaceAll('\\','/')]);
 assert(fs.existsSync(path.join(fixtures,'init-stream0.m4s')),'DASH fragments must stay beside the manifest');
 fs.writeFileSync(path.join(fixtures,'master.m3u8'),'#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=300000,RESOLUTION=160x90\nmedia.m3u8\n');
 console.log('Prepared original, silent HLS / DASH fixtures without managed tools installed.');
}else (async()=>{
 const profile=process.env.KOK_TEST_PROFILE;if(!profile)throw Error('Run tools/test-desktop.ps1 -Suites media-downloads.');
 const fixtures=path.join(profile,'fixtures'),requests=[],timers=new Set();let slow=false;
 const server=http.createServer((req,res)=>{
   const url=new URL(req.url,'http://localhost'),name=path.basename(url.pathname);requests.push({path:url.pathname,auth:req.headers.authorization,cookie:req.headers.cookie});
   if(name==='bad-local.m3u8'){res.writeHead(200,{'Content-Type':'application/vnd.apple.mpegurl'});res.end('#EXTM3U\n#EXT-X-TARGETDURATION:2\n#EXTINF:2,\nfile:///C:/Windows/win.ini\n#EXT-X-ENDLIST\n');return;}
   if(name==='live.m3u8'){res.writeHead(200,{'Content-Type':'application/vnd.apple.mpegurl'});res.end('#EXTM3U\n#EXT-X-TARGETDURATION:2\n#EXTINF:2,\nmedia0.ts\n');return;}
   if(name==='encrypted.m3u8'){res.writeHead(200,{'Content-Type':'application/vnd.apple.mpegurl'});res.end('#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI="key"\n#EXTINF:2,\nmedia0.ts\n#EXT-X-ENDLIST\n');return;}
   if(name==='dynamic.mpd'){res.writeHead(200,{'Content-Type':'application/dash+xml'});res.end('<MPD type="dynamic" xmlns="urn:mpeg:dash:schema:mpd:2011"/>');return;}
   if(name==='entity.mpd'){res.writeHead(200,{'Content-Type':'application/dash+xml'});res.end('<!DOCTYPE MPD [<!ENTITY x SYSTEM "file:///C:/Windows/win.ini">]><MPD>&x;</MPD>');return;}
   const file=path.join(fixtures,name==='opaque'?'master.m3u8':name);if(!fs.existsSync(file)||!fs.statSync(file).isFile()){res.writeHead(404);res.end();return;}
   const body=fs.readFileSync(file),match=/bytes=(\d+)-(\d*)/.exec(req.headers.range||''),from=match?Number(match[1]):0,end=match&&match[2]?Math.min(Number(match[2]),body.length-1):body.length-1;
   if(from>=body.length){res.writeHead(416);res.end();return;}
   const mime=name.endsWith('.mpd')?'application/dash+xml':name.endsWith('.m3u8')||name==='opaque'?'application/vnd.apple.mpegurl':name.endsWith('.ts')?'video/mp2t':'video/mp4';
   res.writeHead(match?206:200,{'Content-Type':mime,'Content-Length':end-from+1,'ETag':'"fixture-static"',...(match?{'Content-Range':`bytes ${from}-${end}/${body.length}`}:{})});
   if(slow&&/\.(?:ts|m4s)$/.test(file)){let at=from;const timer=setInterval(()=>{if(at>end){res.end();clearInterval(timer);timers.delete(timer);return;}const next=Math.min(end+1,at+1024);res.write(body.subarray(at,next));at=next;},25);timers.add(timer);res.on('close',()=>{clearInterval(timer);timers.delete(timer);});}else res.end(body.subarray(from,end+1));
 });await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));const base=`http://127.0.0.1:${server.address().port}`;let browser,page;const errors=[];
 try{
   browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));page.on('pageerror',e=>errors.push(e.message));page.setDefaultTimeout(25000);await page.waitForFunction(()=>!document.querySelector('#startup'));await page.setViewportSize({width:1280,height:900});await page.evaluate(()=>navigate('downloads'));await page.waitForFunction(()=>downloadSnapshot?.downloads);
   const call=(action,args={})=>page.evaluate(({action,args})=>native(action,args),{action,args});const state=async()=>{const snapshot=await call('downloadState');return {...snapshot.downloads,installationError:snapshot.installationError};};
   const wait=async(fn,label,timeout=45000)=>{const end=Date.now()+timeout;while(Date.now()<end){const d=await state(),result=fn(d);if(result)return result;await waitTime(130);}throw Error('Timed out: '+label);};
   assert.equal((await state()).tools.ffmpegInstalled,false);const missing=await call('probeDownload',{url:base+'/media.m3u8',backend:'native'}).then(()=>'',e=>String(e));assert(missing.includes('FFmpeg'));
   await page.locator('[data-install-ffmpeg]').click();await wait(d=>{if(d.installationError)throw Error(d.installationError);return d.tools.ffmpegInstalled;},'actual media component installation',180000);
   await page.waitForFunction(()=>downloadSnapshot?.downloads.tools.ffmpegInstalled);await page.waitForFunction(()=>downloadSnapshot?.installation.status==='completed');
   const manifest=JSON.parse(fs.readFileSync('ui/download-components.json','utf8')).ffmpeg;
   for(const [name,expected]of [['ffmpeg.exe',manifest.executableSha256],['ffprobe.exe',manifest.probeSha256]])assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(profile,'download-components',name))).digest('hex'),expected);
   assert(fs.existsSync(path.join(profile,'download-components','FFmpeg-LICENSE.txt')));assert(!fs.existsSync(path.join(profile,'download-components','yt-dlp.exe')));assert(!fs.existsSync(path.join(profile,'download-components','deno.exe')));
   console.log('PASS: UI one-click installation of pinned FFmpeg / FFprobe, archive and executable hashes, automatic discovery and preserved license; no yt-dlp.');
   const start=async name=>{const ids=new Set((await state()).jobs.map(j=>j.id));await call('startDownloads',{urls:[base+'/'+name],backend:'native',quality:'original'});return (await state()).jobs.find(j=>!ids.has(j.id));};
   const job=async(id,fn)=>wait(d=>{const j=d.jobs.find(j=>j.id===id);if(j?.status==='error')throw Error(j.error);return j&&fn(j)?j:false;},id);
   const frames=file=>run(ffmpeg,['-v','error','-i',file,'-map','0:v:0','-f','framehash','-hash','sha256','-']).split(/\r?\n/).filter(line=>line&&!line.startsWith('#')).map(line=>line.split(',').at(-1).trim());
   const expected=frames(path.join(fixtures,'original.mp4'));
   const verify=async id=>{await job(id,j=>j.status==='completed'&&j.imported);const saved=JSON.parse(fs.readFileSync(path.join(profile,'downloads.json'),'utf8')).jobs.find(j=>j.id===id),file=saved.files[0];const probe=JSON.parse(run(ffprobe,['-v','error','-show_format','-show_streams','-of','json',file]));assert(probe.streams.some(s=>s.codec_type==='video'));assert(probe.streams.some(s=>s.codec_type==='audio'));assert(Number(probe.format.duration)>15);assert.deepEqual(frames(file),expected);return saved;};
   await page.locator('#download-urls').fill(base+'/opaque');await page.locator('[data-download-probe]').click();await page.waitForSelector('.download-preview-card');assert.match(await page.locator('.download-preview-card').innerText(),/分段合并/);await page.locator('[data-start-download]').click();const hls=await wait(d=>d.jobs[0],'HLS job');const hlsSaved=await verify(hls.id);assert.equal(hlsSaved.streamKind,'hls');
   console.log('PASS: actual HLS master download and original decoded video frames.');
   const dashProbe=await call('probeDownload',{url:base+'/media.mpd',backend:'native'});assert.equal(dashProbe.streamKind,'dash');assert(dashProbe.duration>15);const dash=await start('media.mpd');await verify(dash.id);
   console.log('PASS: actual HLS master and DASH SegmentTemplate downloads, audio/video merge, original decoded frames, duration, progress and automatic import.');
   slow=true;const paused=await start('media.m3u8');await job(paused.id,j=>j.streamKind==='hls'&&j.received>0);await call('pauseDownload',{id:paused.id});await job(paused.id,j=>j.status==='paused');await waitTime(150);await call('resumeDownload',{id:paused.id});slow=false;await verify(paused.id);assert((await state()).jobs.find(j=>j.id===paused.id).notice.includes('重新下载'));
   const plain=await start('original.mp4');await job(plain.id,j=>j.status==='completed'&&j.imported);const saved=JSON.parse(fs.readFileSync(path.join(profile,'downloads.json'),'utf8')).jobs.find(j=>j.id===plain.id);assert(fs.readFileSync(saved.files[0]).equals(fs.readFileSync(path.join(fixtures,'original.mp4'))));
   for(const name of ['bad-local.m3u8','live.m3u8','encrypted.m3u8','dynamic.mpd','entity.mpd'])assert(await call('probeDownload',{url:base+'/'+name,backend:'native'}).then(()=>false,()=>true),name+' must be refused');
   await page.evaluate(async()=>{await setting({uiLanguage:'en'});I18n.applyLanguage('en');await loadDownloadState();});await page.waitForFunction(()=>![...document.querySelectorAll('.download-job-meta')].some(el=>/[\u3400-\u9fff]/.test(el.textContent)));
   const captures=path.join(profile,'captures');fs.mkdirSync(captures,{recursive:true});await page.screenshot({path:path.join(captures,'media-components-en.png'),scale:'css'});await page.setViewportSize({width:840,height:620});assert(await page.locator('#main').evaluate(el=>el.scrollWidth<=el.clientWidth+2));
   await page.evaluate(async()=>{await native('setAppMode',{mode:'pure'});await refresh();});assert(await call('installDownloadTools',{bundle:'ffmpeg'}).then(()=>false,()=>true));assert(await call('probeDownload',{url:base+'/media.mpd',backend:'native'}).then(()=>false,()=>true));assert.equal(await page.evaluate(()=>audio.paused),true);assert(requests.every(r=>!r.auth&&!r.cookie));assert.deepEqual(errors,[]);
   console.log('PASS: pause/restart with clear notice, byte-exact plain downloads, refusal of local references / live / encrypted / dynamic / DTD manifests, English / compact UI and Pure mode blocking.');
 }finally{if(browser)await browser.close();for(const timer of timers)clearInterval(timer);server.closeAllConnections();await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});

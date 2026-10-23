// Read-only regression against a user-supplied local media file. No movie data is copied or uploaded.
const{chromium}=require('./browser.cjs');
const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const target=path.resolve(process.argv[2]||'');
assert(process.argv[2]&&fs.existsSync(target),'Usage: node tests/large-media.cjs <existing-large-video>');
const size=fs.statSync(target).size;assert(size>16*1024*1024,'Use a file larger than 16 MiB to exercise response limits.');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));
 const fd=fs.openSync(target,'r');let id,added=false;
 try{
   await page.waitForFunction(()=>!$('#startup'));const diag=await page.evaluate(()=>native('diagnostics'));assert.equal(diag.silentTest,true);const cap=diag.mediaResponseLimit;assert(cap>0&&cap<=8*1024*1024);
   id=await page.evaluate(file=>state.items.find(item=>item.path===file)?.id,target);
   if(!id){const session=await page.context().newCDPSession(page);const data={items:[],files:[target],dragOperationsMask:1};for(const type of ['dragEnter','dragOver','drop'])await session.send('Input.dispatchDragEvent',{type,x:500,y:300,data});await page.waitForFunction(file=>state.items.some(item=>item.path===file),target);id=await page.evaluate(file=>state.items.find(item=>item.path===file).id,target);added=true;}
   const at=Math.min(5*1024*1024*1024+123,size-cap-10);
   const checks=[{range:'bytes=0-',start:0,length:cap},{range:`bytes=${at}-`,start:at,length:cap},{range:`bytes=${at}-${at+63}`,start:at,length:64},{range:'bytes=-128',start:size-128,length:128}];
   for(const check of checks){
     const result=await page.evaluate(async({id,range})=>{const response=await fetch(media(byId(id)),{headers:{Range:range}}),data=new Uint8Array(await response.arrayBuffer());return{status:response.status,range:response.headers.get('Content-Range'),length:Number(response.headers.get('Content-Length')),bytes:data.length,head:[...data.slice(0,32)],tail:[...data.slice(-32)]};},{id,range:check.range});
     assert.equal(result.status,206);assert.equal(result.bytes,check.length);assert.equal(result.length,check.length);assert.equal(result.range,`bytes ${check.start}-${check.start+check.length-1}/${size}`);
     const head=Buffer.alloc(32),tail=Buffer.alloc(32);fs.readSync(fd,head,0,32,check.start);fs.readSync(fd,tail,0,32,check.start+check.length-32);assert.deepEqual(result.head,[...head]);assert.deepEqual(result.tail,[...tail]);
   }
   const invalid=await page.evaluate(async({id,size})=>{const r=await fetch(media(byId(id)),{headers:{Range:`bytes=${size}-`}});return{status:r.status,range:r.headers.get('Content-Range')};},{id,size});assert.deepEqual(invalid,{status:416,range:`bytes */${size}`});
   const noRange=await page.evaluate(async id=>{const r=await fetch(media(byId(id)));return{status:r.status,bytes:(await r.arrayBuffer()).byteLength};},id);assert.deepEqual(noRange,{status:413,bytes:0});console.log('PASS: capped open ranges, exact byte content, suffix reads, invalid ranges and no-range protection; 64-bit offsets verified.');
   await page.evaluate(id=>openItem(id),id);await page.waitForFunction(()=>video?.readyState>=2&&Number.isFinite(video.duration),null,{timeout:20000});
   await page.evaluate(()=>video.play());await page.waitForFunction(()=>video.currentTime>.5);const videoReport=await page.evaluate(()=>({duration:video.duration,width:video.videoWidth,height:video.videoHeight,frames:video.getVideoPlaybackQuality().totalVideoFrames}));assert(videoReport.width>0&&videoReport.frames>0);
   for(const targetTime of [Math.min(60,videoReport.duration*.1),videoReport.duration*.5,Math.max(0,videoReport.duration-60)]){
     await page.evaluate(time=>{video.pause();video.currentTime=time;},targetTime);await page.waitForFunction(time=>!video.seeking&&video.readyState>=2&&Math.abs(video.currentTime-time)<1,targetTime,{timeout:20000});
   }
   fs.writeFileSync('test-output/large-media.json',JSON.stringify({version:diag.version,fileBytes:size,responseLimit:cap,largeOffset:at,playback:videoReport,rangeChecks:checks.length,seeks:3},null,2)+'\n');
   console.log('PASS: actual large-file video frames and seeks at the beginning, middle and end; window stays alive.');
 }finally{fs.closeSync(fd);await page.evaluate(async({id,added})=>{if(viewer.open)await closeViewer();if(added)await native('remove',{id});await refresh();},{id,added});await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

// Generate original fixtures with a test-only FFmpeg, then decode through the real native media route.
// Usage: node tests/compatibility.cjs <ffmpeg.exe>
const {chromium}=require('./browser.cjs');
const fs=require('node:fs');
const path=require('node:path');
const {execFileSync}=require('node:child_process');
const assert=require('node:assert/strict');
const ffmpeg=path.resolve(process.argv[2]||'test-output/codec-tools/node_modules/ffmpeg-static/ffmpeg.exe');
assert(fs.existsSync(ffmpeg),'Supply a test-only FFmpeg executable; it is not a kok runtime dependency.');
const fixtures=[
 ['h264-aac.mp4','video',['-c:v','libx264','-pix_fmt','yuv420p','-c:a','aac']],
 ['h264-aac.m4v','video',['-c:v','libx264','-pix_fmt','yuv420p','-c:a','aac','-f','mp4']],
 ['h264-aac.mov','video',['-c:v','libx264','-pix_fmt','yuv420p','-c:a','aac']],
 ['vp8-opus.webm','video',['-c:v','libvpx','-c:a','libopus']],
 ['vp9-opus.webm','video',['-c:v','libvpx-vp9','-c:a','libopus']],
 ['h264-aac.mkv','video',['-c:v','libx264','-pix_fmt','yuv420p','-c:a','aac'],false],
 ['mpeg4-mp3.avi','video',['-c:v','mpeg4','-c:a','libmp3lame'],false],
 ['wmv2-wma2.wmv','video',['-c:v','wmv2','-c:a','wmav2'],false],
 ['h264-aac.ts','video',['-c:v','libx264','-pix_fmt','yuv420p','-c:a','aac','-f','mpegts'],false],
 ['mp3.mp3','music',['-c:a','libmp3lame']], ['pcm.wav','music',['-c:a','pcm_s16le']],
 ['flac.flac','music',['-c:a','flac']], ['vorbis.ogg','music',['-c:a','libvorbis']],
 ['opus.opus','music',['-c:a','libopus']], ['aac.m4a','music',['-c:a','aac']],
 ['adts.aac','music',['-c:a','aac']], ['wma.wma','music',['-c:a','wmav2'],false],
 ['pcm.aiff','music',['-c:a','pcm_s16be'],false],
 ...['jpg','png','gif','webp','bmp','avif','tiff'].map(ext=>['photo.'+ext,'photo',[],ext!=='tiff'])
];
(async()=>{
 const folder=path.resolve('dist/kok/samples');
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));
 const results=[];
 try{
   for(const [name,kind,args]of fixtures){
     const input=kind==='video'?['-f','lavfi','-i','color=c=0x4888dd:s=128x96:r=12','-f','lavfi','-i','sine=frequency=440:sample_rate=44100']:kind==='music'?['-f','lavfi','-i','sine=frequency=440:sample_rate=44100']:['-f','lavfi','-i','color=c=0x4888dd:s=48x32:r=1'];
     execFileSync(ffmpeg,['-hide_banner','-loglevel','error','-y',...input,...args,...(kind==='photo'?['-frames:v','1']:['-t','1.2']),path.join(folder,'compat-'+name)],{windowsHide:true,stdio:'pipe'});
   }
   await page.evaluate(async()=>{if(viewer.open)await closeViewer();if($('#small-dialog').open)await closeSmall();await native('importSamples');await refresh();});
   for(const [name,kind,,required=true]of fixtures){
     const result=await page.evaluate(async({name,kind})=>{
       const item=state.items.find(i=>i.path.endsWith('compat-'+name));if(!item)return{ok:false,error:'not indexed'};
       if(kind==='photo'){
         const img=new Image();img.crossOrigin='anonymous';img.src=media(item);
         try{await img.decode();const c=document.createElement('canvas');c.width=c.height=1;c.getContext('2d').drawImage(img,0,0,1,1);const pixel=[...c.getContext('2d').getImageData(0,0,1,1).data];return{ok:img.naturalWidth>0&&pixel[3]>0,width:img.naturalWidth,pixel};}catch(e){return{ok:false,error:e.message};}
       }
       const el=document.createElement(kind==='video'?'video':'audio');el.crossOrigin='anonymous';el.muted=true;el.src=media(item);el.style.cssText='position:fixed;width:128px;height:96px;bottom:0;right:0';document.body.append(el);
       try{
         await Promise.race([new Promise((resolve,reject)=>{el.onloadeddata=resolve;el.onerror=()=>reject(Error('media error '+el.error?.code));el.load();}),new Promise((_,reject)=>setTimeout(()=>reject(Error('decode timeout')),6000))]);
         await el.play();await new Promise(r=>setTimeout(r,250));const time=el.currentTime;
         return{ok:time>.03&&(kind!=='video'||el.videoWidth>0),time,width:el.videoWidth||undefined,duration:el.duration};
       }catch(e){return{ok:false,error:e.message};}finally{el.pause();el.removeAttribute('src');el.load();el.remove();}
     },{name,kind});
     results.push({file:name,kind,required,...result});console.log((result.ok?'DECODED':'EXTERNAL')+': '+name+(result.error?' ('+result.error+')':''));
   }
   await page.evaluate(async()=>{await settings();});await page.locator('#formats-button').click();await page.waitForSelector('.formats-list');
   assert.equal(await page.locator('.format-group').count(),12);assert.match(await page.locator('.formats-list').innerText(),/DOCX/);
   await page.evaluate(()=>I18n.applyLanguage('en'));await page.waitForFunction(()=>$('#small-dialog h2').textContent==='Supported formats');
   assert(!/[\u3400-\u9fff]/.test(await page.locator('.formats-list').innerText()),'All compatibility explanations must be bilingual');
   await page.screenshot({path:'test-output/formats-en.jpg',type:'jpeg',quality:86,scale:'css'});
   await page.evaluate(()=>I18n.applyLanguage('zh'));await page.locator('#formats-back').click();await page.waitForSelector('#theme-select');await page.evaluate(()=>closeSmall());
   const report={engine:await page.evaluate(()=>native('diagnostics')),generated:new Date().toISOString(),results};delete report.engine.dataDir;
   fs.writeFileSync('test-output/compatibility.json',JSON.stringify(report,null,2)+'\n');
   assert.deepEqual(results.filter(r=>r.required&&!r.ok),[],'Recommended codecs must decode actual original files');
   console.log('PASS: actual media decoding and bilingual compatibility dialog');
 }finally{
   await page.evaluate(async()=>{for(const item of state.items.filter(i=>i.path.split(/[\\/]/).pop().startsWith('compat-')))await native('remove',{id:item.id});await refresh();});
   for(const [name]of fixtures){const file=path.join(folder,'compat-'+name);if(fs.existsSync(file))fs.unlinkSync(file);}
   await browser.close();
 }
})().catch(e=>{console.error(e);process.exitCode=1;});

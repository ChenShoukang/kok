// Read-only regression against a user-supplied folder of real files. Files are imported by reference into the isolated
// test profile, opened once each, then removed again; nothing is copied, uploaded or written beside the originals.
'use strict';
const{chromium}=require('./browser.cjs');
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const folder=path.resolve(process.argv[2]||'');
assert(process.argv[2]&&fs.existsSync(folder)&&fs.statSync(folder).isDirectory(),'Usage: node tests/real-files.cjs <folder-with-real-files>');
const out=path.resolve('test-output','real-files');fs.mkdirSync(out,{recursive:true});

function fingerprint(file){
 const stat=fs.statSync(file),hash=crypto.createHash('sha256'),fd=fs.openSync(file,'r'),buffer=Buffer.alloc(8*1024*1024);
 try{let read;while((read=fs.readSync(fd,buffer,0,buffer.length,null))>0)hash.update(buffer.subarray(0,read));}finally{fs.closeSync(fd);}
 return{size:stat.size,modified:stat.mtimeMs,sha256:hash.digest('hex')};
}
const elapsed=start=>Math.round(performance.now()-start);
const failureText=page=>page.evaluate(()=>{const box=document.querySelector('#viewer-content .empty-state, #viewer-content .photo-error');return box?box.textContent.replace(/\s+/g,' ').trim():'';});
async function opened(page,ready,timeout){
 // Each predicate also resolves when the viewer shows its own error state; the app CSP forbids string expressions.
 await page.waitForFunction(ready,null,{timeout});
}

async function checkVideo(page,report){
 await opened(page,()=>!!document.querySelector('#viewer-content .empty-state')||!!(video&&video.readyState>=2&&Number.isFinite(video.duration)),60000);
 const error=await failureText(page);if(error)throw Error(error);
 await page.evaluate(()=>{video.muted=true;return video.play();});await page.waitForFunction(()=>video.currentTime>.3,null,{timeout:20000});
 Object.assign(report.details,await page.evaluate(()=>({duration:video.duration,width:video.videoWidth,height:video.videoHeight})));
 assert(report.details.width>0,'Video has audio only or no decodable picture');
 report.details.seeks=[];
 for(const ratio of [.1,.5,.95]){
   const start=performance.now(),target=report.details.duration*ratio;
   await page.evaluate(time=>{video.pause();video.currentTime=time;},target);
   await page.waitForFunction(time=>!video.seeking&&video.readyState>=2&&Math.abs(video.currentTime-time)<1.5,target,{timeout:30000});
   report.details.seeks.push({at:Math.round(target),ms:elapsed(start)});
 }
}
async function checkPhoto(page,report){
 await opened(page,()=>!!document.querySelector('#viewer-content .photo-error')||$('#photo-image')?.naturalWidth>0,30000);
 const error=await failureText(page);if(error)throw Error(error);
 Object.assign(report.details,await page.evaluate(()=>({width:$('#photo-image').naturalWidth,height:$('#photo-image').naturalHeight})));
 await page.evaluate(()=>zoomPhoto(2));await page.waitForFunction(()=>Number(state.zoom)===2,null,{timeout:5000});
}
async function checkPdf(page,report){
 await opened(page,()=>!!document.querySelector('#viewer-content .empty-state')||/^1 \/ \d+$/.test($('#pdf-page-status')?.textContent||''),120000);
 const error=await failureText(page);if(error)throw Error(error);
 report.details.pages=await page.evaluate(()=>pdfState.doc.numPages);
 await page.waitForTimeout(1500);
 report.details.textLayerSpans=await page.evaluate(()=>document.querySelectorAll('#viewer-content .textLayer span').length);
 if(!report.details.textLayerSpans)report.notes.push('No text layer on the first page (likely scanned; search and translation will not work without OCR).');
 if(report.details.pages>1){const start=performance.now();await page.evaluate(()=>jumpPdfPage(2));await page.waitForFunction(()=>/^2 \//.test($('#pdf-page-status').textContent),null,{timeout:60000});report.details.secondPageMs=elapsed(start);}
}
async function checkReader(page,report){
 await opened(page,()=>!!document.querySelector('#viewer-content .empty-state')||!!(state.book&&$('#reader-text')?.textContent.trim().length>0),60000);
 const error=await failureText(page);if(error)throw Error(error);
 Object.assign(report.details,await page.evaluate(()=>({chapters:state.book.chapters.length,toc:(state.book.toc||[]).length,characters:$('#reader-text').textContent.length})));
 if(report.details.chapters>1){await page.evaluate(()=>changeChapter(1));await page.waitForFunction(()=>state.chapter===1&&$('#reader-text').textContent.trim().length>0,null,{timeout:30000});}
}

(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local')&&!p.url().includes('preview='));
 await page.waitForFunction(()=>!$('#startup'));
 const diag=await page.evaluate(()=>native('diagnostics'));assert.equal(diag.silentTest,true);
 assert.equal(await page.evaluate(async()=>{const r=await fetch('https://media.kok.local/thumb/00000000000000000000000000000000');return r.status;}),204,'a missing optional cover has a silent, empty fallback');
 const remote=[];await page.context().route(/^https?:\/\//,route=>{if(['app.kok.local','media.kok.local'].includes(new URL(route.request().url()).hostname))return route.continue();remote.push(new URL(route.request().url()).hostname);return route.abort();});
 let current=null;
 page.on('pageerror',error=>current?.errors.push('pageerror: '+(error.stack||error.message).split('\n')[0]));
 page.on('console',message=>{if(message.type()==='error')current?.errors.push('console: '+message.text().slice(0,300));});
 page.on('response',response=>{if(response.status()>=400&&current){current.resourceErrors||=[];const u=new URL(response.url());current.resourceErrors.push({status:response.status(),host:u.hostname,path:u.pathname});}});
 const files=fs.readdirSync(folder).map(name=>path.join(folder,name)).filter(file=>fs.statSync(file).isFile()).sort();
 const reports=[];
 for(const [index,file] of files.entries()){
   const report=current={file:path.basename(file),bytes:fs.statSync(file).size,kind:null,ext:null,passed:false,importMs:null,openMs:null,details:{},notes:[],errors:[]};
   reports.push(report);let id=null,before=null;
   try{
     before=fingerprint(file);
     let start=performance.now();
     const session=await page.context().newCDPSession(page),data={items:[],files:[file],dragOperationsMask:1};
     for(const type of ['dragEnter','dragOver','drop'])await session.send('Input.dispatchDragEvent',{type,x:500,y:300,data});
     await session.detach();
     await page.waitForFunction(file=>state.items.some(item=>item.path.toLowerCase()===file.toLowerCase()),file,{timeout:60000}).catch(()=>{throw Error('Not imported within 60 s (unsupported type or import error)');});
     report.importMs=elapsed(start);
     const item=await page.evaluate(file=>{const i=state.items.find(item=>item.path.toLowerCase()===file.toLowerCase());return{id:i.id,kind:i.kind,ext:i.ext};},file);
     id=item.id;report.kind=item.kind;report.ext=item.ext;
     start=performance.now();
     await page.evaluate(id=>{openItem(id,{play:false});},id);
     if(item.kind==='video')await checkVideo(page,report);
     else if(item.kind==='photo')await checkPhoto(page,report);
     else if(item.ext==='PDF')await checkPdf(page,report);
     else if(item.kind==='music')await opened(page,()=>!!document.querySelector('#viewer-content .empty-state')||!!(audio&&audio.readyState>=1),30000);
     else await checkReader(page,report);
     report.openMs=elapsed(start);
     await page.screenshot({path:path.join(out,String(index+1).padStart(2,'0')+'-'+(item.kind||'file')+'.png')});
     report.passed=true;
   }catch(error){
     report.failure=(error.message||String(error)).split('\n')[0];
     await page.screenshot({path:path.join(out,String(index+1).padStart(2,'0')+'-failed.png')}).catch(()=>{});
   }finally{
     if(before){try{assert.deepEqual(fingerprint(file),before,'Original file changed during the test');report.originalUnchanged=true;}catch(error){report.passed=false;report.originalUnchanged=false;report.failure=error.message;}}
     await page.evaluate(async id=>{if(viewer.open)await closeViewer();if(id&&byId(id))await native('remove',{id});await refresh();},id).catch(error=>report.errors.push('cleanup: '+error.message));
     if(report.errors.length&&report.passed)report.notes.push('Page reported errors while this file was open.');
     console.log(`${report.passed?'PASS':'FAIL'}: [${report.kind||'?'}] ${report.file} import=${report.importMs}ms open=${report.openMs}ms${report.failure?' — '+report.failure:''}`);
   }
 }
 current=null;
 fs.writeFileSync(path.join(out,'report.json'),JSON.stringify({version:diag.version,engine:diag.engine,folder,remoteHosts:remote,files:reports},null,2)+'\n');
 await browser.close();
 const failed=reports.filter(r=>!r.passed).length;
 console.log(`${reports.length-failed} / ${reports.length} real files passed. Report: ${path.join(out,'report.json')}`);
 if(failed||remote.length)process.exitCode=1;
})().catch(error=>{console.error(error);process.exitCode=1;});

const{chromium}=require('./browser.cjs');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
(async()=>{
 let browser=await chromium.connectOverCDP('http://127.0.0.1:9225');let page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));
 await page.waitForFunction(()=>!$('#startup'));await page.evaluate(()=>openItem(state.items.find(i=>i.kind==='video').id));await page.waitForFunction(()=>video.readyState>=2);await page.locator('#video-fullscreen').click();await page.waitForFunction(async()=>(await native('diagnostics')).window.fullScreen);const before=await page.evaluate(()=>({count:state.items.length,prefix:requestPrefix}));
 const session=await page.context().newCDPSession(page);
 // Crash only this isolated WebView renderer. The native host must survive and restore the library.
 await Promise.race([session.send('Page.crash').catch(()=>{}),sleep(1200)]);await browser.close();
 let recovered=false,lastError;
 for(let attempt=0;attempt<15&&!recovered;attempt++){
   await sleep(400);
   try{
     browser=await chromium.connectOverCDP('http://127.0.0.1:9225');page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));
     await page.waitForFunction(()=>!$('#startup')&&state.items.length>0,null,{timeout:3000});
     const after=await page.evaluate(()=>({count:state.items.length,prefix:requestPrefix,viewer:state.viewer}));
     assert.equal(after.count,before.count);assert.notEqual(after.prefix,before.prefix);assert.equal(after.viewer,null);assert.equal(await page.locator('#video').count(),0);recovered=true;
   }catch(error){lastError=error;await browser?.close();}
 }
 assert(recovered,lastError?.message||'Renderer recovery timed out');
 const diagnostics=await page.evaluate(()=>native('diagnostics'));assert.equal(diagnostics.window.fullScreen,false);assert.equal(diagnostics.window.border,'Sizable');const log=fs.readFileSync(diagnostics.logFile,'utf8');assert(log.includes('kind=RenderProcessExited'));
 await browser.close();console.log('PASS: native host survives a fullscreen renderer crash, restores window borders and the library, uses fresh request IDs and does not reopen the video.');
})().catch(error=>{console.error(error);process.exitCode=1;});

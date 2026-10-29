const {chromium}=require('./browser.cjs');
(async()=>{
 const b=await chromium.connectOverCDP('http://127.0.0.1:9225'),p=b.contexts()[0].pages().find(p=>p.url().includes('app.kok.local'));
 p.on('pageerror',e=>console.log('ERROR',e.message));await p.waitForFunction(()=>!$('#startup'));
 await p.evaluate(()=>{window.formTrace=[];const descriptor=Object.getOwnPropertyDescriptor(HTMLElement.prototype,'onsubmit');Object.defineProperty(HTMLElement.prototype,'onsubmit',{...descriptor,set(value){window.formTrace.push({setter:this.id,value:String(value).slice(0,80)});return descriptor.set.call(this,value);}});new MutationObserver(rows=>{for(const r of rows)if(r.target.id==='small-dialog')window.formTrace.push({mutation:r.removedNodes.length,added:r.addedNodes.length});}).observe($('#small-dialog'),{childList:true});navigate('spaces');});
 await p.locator('[data-warehouse="new-space"]').click();await p.waitForTimeout(200);
 console.log(await p.evaluate(()=>({trace:window.formTrace,forms:[...document.querySelectorAll('#workspace-form')].map(f=>({handler:String(f.onsubmit),parent:f.parentElement.className})),method:Warehouse.nameForm.toString().slice(-250)})));
 await b.close();
})().catch(e=>{console.error(e);process.exitCode=1;});

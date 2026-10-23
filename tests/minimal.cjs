const{chromium}=require('playwright-core');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.setViewportSize({width:1440,height:1000});
 await page.evaluate(async()=>{await setting({uiLanguage:'zh'});I18n.applyLanguage('zh');});
 await page.reload({waitUntil:'commit'});await page.waitForSelector('#startup');
 assert.equal(await page.locator('#startup img').getAttribute('src'),'logo.svg');
 assert.equal((await page.locator('#startup').innerText()).trim(),'');
 await page.screenshot({path:'test-output/minimal-startup.png',scale:'css',animations:'disabled'});
 await page.waitForFunction(()=>!document.querySelector('#startup'));
 assert.equal(await page.locator('body').getAttribute('aria-busy'),'false');
 assert.equal(await page.locator('.hero,.quote-card,.category-grid,.workspace,.avatar,.bottom-line').count(),0);
 assert.equal(await page.locator('#main h1').innerText(),'资料库');
 assert.equal(await page.locator('.file-card').count(),await page.evaluate(()=>state.items.length));
 const blur=await page.locator('.sidebar').evaluate(el=>getComputedStyle(el).backdropFilter);assert(blur.includes('blur('));
 await page.locator('#import-more').click();await page.waitForSelector('#import-menu:not(.hidden)');assert.equal(await page.locator('#import-menu [data-action="importFolder"]').count(),1);await page.keyboard.press('Escape');assert(await page.locator('#import-menu').evaluate(el=>el.classList.contains('hidden')));
 console.log('PASS: minimal startup, content-first home, glass sidebar and import menu');
 // Check the actual overlapping page snapshots and rapid navigation cancellation.
 const motion=await page.evaluate(async()=>{
   const properties=new Set();const samples=[];let last=performance.now(),running=true;
   function frame(now){samples.push(now-last);last=now;if(running)requestAnimationFrame(frame);}requestAnimationFrame(frame);
   const longTasks=[];let observer;try{observer=new PerformanceObserver(list=>longTasks.push(...list.getEntries().map(e=>e.duration)));observer.observe({type:'longtask',buffered:false});}catch{}
   let transitions=0;
   for(const route of ['photo','document','music','book','overview']){navigate(route);const transition=pageTransition;if(transition){await transition.ready;transitions++;}for(const animation of document.getAnimations())for(const frame of animation.effect.getKeyframes())for(const key of Object.keys(frame))if(!['offset','computedOffset','easing','composite'].includes(key))properties.add(key);if(transition)await transition.finished;else await new Promise(resolve=>setTimeout(resolve,500));}
   running=false;observer?.disconnect();samples.sort((a,b)=>a-b);
   return{frames:samples.length,p95FrameMs:samples[Math.floor(samples.length*.95)]||0,longTasks,properties:[...properties],transitions};
 });
 assert(motion.frames>10);assert(motion.transitions>=4);assert(!motion.properties.includes('filter'));assert(!motion.properties.includes('backdropFilter'));console.log('Navigation timing:',JSON.stringify(motion));
 await page.evaluate(async()=>{for(const route of ['photo','music','book','document','overview']){navigate(route);await new Promise(r=>setTimeout(r,35));}if(pageTransition)await pageTransition.finished;});
 assert.equal(await page.locator('#main h1').innerText(),'资料库');assert.equal(await page.locator('.file-card').count(),await page.evaluate(()=>state.items.length));
 await page.evaluate(async()=>{showSmall('<h2>First</h2>');const closing=closeSmall();showSmall('<h2>Replacement</h2>');await closing;});
 assert(await page.locator('#small-dialog').evaluate(el=>el.open));assert.equal(await page.locator('#small-dialog h2').innerText(),'Replacement');await page.evaluate(()=>closeSmall());
 console.log('PASS: rapid navigation and reopening a closing dialog preserve the newest view');
 await page.screenshot({path:'docs/overview.jpg',fullPage:true,scale:'css',type:'jpeg',quality:90});
 for(const size of [{width:1440,height:1000},{width:1000,height:720},{width:960,height:512}]){await page.setViewportSize(size);const gear=await page.locator('#settings-button').boundingBox();assert(gear.y+gear.height<=size.height);const sidebar=await page.locator('.sidebar').boundingBox();assert(sidebar.width>=192);assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));}await page.screenshot({path:'test-output/minimal-compact.png',scale:'css'});
 await page.emulateMedia({reducedMotion:'reduce'});await page.reload();await page.waitForFunction(()=>!document.querySelector('#startup'));assert.equal(await page.evaluate(()=>animateIn(document.querySelector('#main'))),undefined);console.log('PASS: reduced-motion preference and compact layout');
 await page.emulateMedia({reducedMotion:'no-preference'});await page.setViewportSize({width:1440,height:1000});await page.evaluate(()=>navigate('overview'));assert.deepEqual(errors,[]);
 fs.writeFileSync(path.resolve('test-output/minimal-validation.json'),JSON.stringify({motion,checked:true},null,2));await browser.close();console.log('Minimal interface checks passed.');
})().catch(error=>{console.error(error);process.exit(1);});

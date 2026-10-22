const{chromium}=require('playwright-core');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.setViewportSize({width:1440,height:1000});
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
 // Navigation uses short transform/opacity animations instead of animating filters.
 const motion=await page.evaluate(async()=>{
   const properties=new Set();const samples=[];let last=performance.now(),running=true;
   function frame(now){samples.push(now-last);last=now;if(running)requestAnimationFrame(frame);}requestAnimationFrame(frame);
   const longTasks=[];let observer;try{observer=new PerformanceObserver(list=>longTasks.push(...list.getEntries().map(e=>e.duration)));observer.observe({type:'longtask',buffered:false});}catch{}
   for(const route of ['photo','document','music','book','overview']){navigate(route);for(const animation of $('#main').getAnimations())for(const frame of animation.effect.getKeyframes())for(const key of Object.keys(frame))if(!['offset','computedOffset','easing','composite'].includes(key))properties.add(key);await new Promise(resolve=>setTimeout(resolve,220));}
   running=false;observer?.disconnect();samples.sort((a,b)=>a-b);
   return{frames:samples.length,p95FrameMs:samples[Math.floor(samples.length*.95)]||0,longTasks,properties:[...properties]};
 });
 assert(motion.frames>10);assert(!motion.properties.includes('filter'));console.log('Navigation timing:',JSON.stringify(motion));
 await page.screenshot({path:'docs/overview.jpg',fullPage:true,scale:'css',type:'jpeg',quality:90});
 await page.setViewportSize({width:1000,height:720});const gear=await page.locator('#settings-button').boundingBox();assert(gear.y+gear.height<=720);await page.screenshot({path:'test-output/minimal-compact.png',scale:'css'});
 await page.emulateMedia({reducedMotion:'reduce'});await page.reload();await page.waitForFunction(()=>!document.querySelector('#startup'));assert.equal(await page.evaluate(()=>animateIn(document.querySelector('#main'))),undefined);console.log('PASS: reduced-motion preference and compact layout');
 await page.emulateMedia({reducedMotion:'no-preference'});await page.setViewportSize({width:1440,height:1000});await page.evaluate(()=>navigate('overview'));assert.deepEqual(errors,[]);
 fs.writeFileSync(path.resolve('test-output/minimal-validation.json'),JSON.stringify({motion,checked:true},null,2));await browser.close();console.log('Minimal interface checks passed.');
})().catch(error=>{console.error(error);process.exit(1);});

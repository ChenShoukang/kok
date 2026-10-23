const {chromium}=require('./browser.cjs');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));
 const session=await page.context().newCDPSession(page);await session.send('Network.setCacheDisabled',{cacheDisabled:true});await page.reload();
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 const win=()=>page.evaluate(async()=>(await native('diagnostics')).window);
 const waitWindow=async full=>{await page.waitForFunction(async full=>(await native('diagnostics')).window.fullScreen===full,full);};
 try{
  await page.waitForFunction(()=>!$('#startup'));
  await page.evaluate(async()=>{if(viewer.open)await closeViewer();await native('importSamples');await refresh();await openItem(state.items.find(i=>i.kind==='video').id);});
  await page.waitForFunction(()=>video.readyState>=2);
  const original=await win();assert.equal(original.fullScreen,false);
  async function oneRow(){
   const layout=await page.evaluate(()=>{const row=$('.video-control-row').getBoundingClientRect();return{height:row.height,width:$('.video-control-row').clientWidth,scroll:$('.video-control-row').scrollWidth,centers:['#video-toggle','#video-seek','#video-volume','#video-options-toggle','#video-fullscreen'].map(s=>{const r=$(s).getBoundingClientRect();return r.y+r.height/2;})};});
   assert(Math.max(...layout.centers)-Math.min(...layout.centers)<2,'All transport controls share one row');assert(layout.scroll<=layout.width+1,'Toolbar has no horizontal overflow');assert(layout.height<45);
  }
  await oneRow();assert(await page.locator('.video-options').isHidden());
  await page.locator('#video-options-toggle').click();assert(await page.locator('.video-options').isVisible());
  await page.locator('#video-rate').selectOption('1.5');await page.waitForFunction(()=>video.playbackRate===1.5);
  await page.keyboard.press('Escape');assert(await page.locator('.video-options').isHidden());assert(await page.evaluate(()=>viewer.open));
  await page.locator('#video-fullscreen').click();await waitWindow(true);
  const full=await win();assert.equal(full.border,'None');assert.deepEqual(full.bounds,{x:full.screen.X,y:full.screen.Y,width:full.screen.Width,height:full.screen.Height});
  const surface=await page.evaluate(()=>{const r=$('.video-workspace').getBoundingClientRect();return{x:r.x,y:r.y,w:r.width,h:r.height,vw:innerWidth,vh:innerHeight};});assert.equal(surface.x,0);assert.equal(surface.y,0);assert(Math.abs(surface.w-surface.vw)<1);assert(Math.abs(surface.h-surface.vh)<1);await oneRow();
  await page.evaluate(async()=>{video.loop=true;await video.play();});await page.mouse.move(400,200);await page.waitForFunction(()=>$('.video-workspace').classList.contains('controls-idle'),null,{timeout:7000});
  await page.waitForTimeout(450);assert.equal(await page.locator('.video-controls').evaluate(el=>getComputedStyle(el).opacity),'0');
  await page.mouse.move(500,250);await page.waitForFunction(()=>!$('.video-workspace').classList.contains('controls-idle'));
  await page.evaluate(()=>video.pause());await page.screenshot({path:'test-output/video-fullscreen.png',scale:'css'});
  await page.keyboard.press('Escape');await waitWindow(false);assert(await page.evaluate(()=>viewer.open));const restored=await win();assert.deepEqual(restored.bounds,original.bounds);assert.equal(restored.border,original.border);assert.equal(restored.state,original.state);
  await page.locator('#video').focus();await page.keyboard.press('f');await waitWindow(true);await page.keyboard.press('f');await waitWindow(false);
  await page.locator('.video-canvas').dblclick();await waitWindow(true);await page.evaluate(()=>closeViewer());await waitWindow(false);assert(!await page.evaluate(()=>viewer.open));
  console.log('PASS: native borderless screen bounds, restore bounds, Esc/F/double-click, close while fullscreen, silent idle controls');
  for(const language of ['zh','en'])for(const size of [{width:1440,height:1000},{width:960,height:640}]){
   await page.setViewportSize(size);await page.evaluate(async language=>{I18n.applyLanguage(language);await openItem(state.items.find(i=>i.kind==='video').id);},language);await page.waitForFunction(()=>video.readyState>=2);await oneRow();
   await page.locator('#video-options-toggle').click();assert(await page.locator('#subtitle-select').isVisible());const box=await page.locator('.video-options').boundingBox();assert(box.x>=0&&box.y>=0&&box.x+box.width<=size.width&&box.y+box.height<=size.height,'Settings panel stays within viewport');
   await page.locator('.video-canvas').click();assert(await page.locator('.video-options').isHidden());await page.evaluate(()=>video.pause());if(language==='zh'&&size.width===1440)await page.screenshot({path:'docs/screenshots/video-player.jpg',type:'jpeg',quality:88,scale:'css'});await page.evaluate(()=>closeViewer());
  }
  await page.emulateMedia({reducedMotion:'reduce'});await page.evaluate(()=>openItem(state.items.find(i=>i.kind==='video').id));assert.equal(await page.locator('.video-controls').evaluate(el=>getComputedStyle(el).transitionDuration),'0s');
  assert.deepEqual(errors,[]);console.log('PASS: single transport row, floating settings, compact Chinese/English layouts, reduced motion');
 }finally{
  await page.evaluate(async()=>{if(document.fullscreenElement)await document.exitFullscreen();if(viewer.open)await closeViewer();audio.pause();I18n.applyLanguage('zh');});
  await browser.close();
 }
})().catch(error=>{console.error(error);process.exitCode=1;});

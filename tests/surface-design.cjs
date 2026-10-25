'use strict';
const assert=require('node:assert/strict'),path=require('node:path');
const {chromium}=require('./browser.cjs');
(async()=>{
 const profile=process.env.KOK_TEST_PROFILE;if(!profile)throw Error('Use the isolated desktop runner');
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));
 const errors=[];page.on('pageerror',error=>errors.push(error.message));page.setDefaultTimeout(20000);
 const shot=async name=>{assert(page.url().startsWith('https://app.kok.local'));await page.screenshot({path:path.join(profile,name+'.png'),animations:'disabled'});};
 const fits=async selector=>{const r=await page.locator(selector).boundingBox();const s=await page.evaluate(()=>({w:innerWidth,h:innerHeight}));assert(r&&r.x>=-1&&r.y>=-1&&r.x+r.width<=s.w+1&&r.y+r.height<=s.h+1,selector+' fits the viewport');};
 try{
  assert.equal(path.resolve((await page.evaluate(()=>native('diagnostics'))).dataDir),path.resolve(profile));
  await page.waitForFunction(()=>!document.querySelector('#startup'));
  await page.evaluate(async()=>{await native('importSamples');await refresh();await setting({theme:'dark',view:'grid'});applyTheme();navigate('overview');});
  await page.evaluate(()=>document.fonts.ready);assert(await page.evaluate(()=>[...document.fonts].some(f=>f.family==='Smiley Sans'&&f.status==='loaded')));assert.match(await page.locator('body').evaluate(el=>getComputedStyle(el).fontFamily),/Smiley Sans/);
  await page.waitForSelector('.file-card.photo img');await page.waitForFunction(()=>[...document.querySelectorAll('.file-card.photo img')].every(i=>i.complete&&i.naturalWidth>0));
  await page.waitForFunction(()=>[...document.querySelectorAll('.nav-art img,.object-cover>.object-icon')].every(i=>i.complete&&i.naturalWidth>0));
  if(process.env.KOK_DESIGN_CAPTURE_ONLY){
   await shot('library-dark');
   if(process.env.KOK_DESIGN_CAPTURE_ONLY==='icons'){
    assert.equal(await page.locator('.nav-art').first().evaluate(el=>getComputedStyle(el).filter),'none');
    assert(await page.locator('.nav-art img').evaluateAll(images=>images.length===6&&images.every(image=>image.src.endsWith('_flat.svg'))));
    const importColor=await page.locator('.import-control').evaluate(el=>getComputedStyle(el).backgroundColor);const rgb=importColor.match(/[\d.]+/g).slice(0,3).map(Number);assert(Math.max(...rgb)<100,'Dark import control must use a dark surface');
    const aligned=await page.evaluate(async()=>{
     const records=[];
     for(const element of document.querySelectorAll('.sidebar .nav-art img,.sidebar .nav-utility-icon,.file-grid .object-cover>.object-icon,.file-grid .private-folder-cover>.object-icon')){
      if(!element.getClientRects().length)continue;
      let image=element;
      if(element instanceof SVGElement){const copy=element.cloneNode(true),view=element.viewBox.baseVal;copy.removeAttribute('style');copy.setAttribute('width',view.width);copy.setAttribute('height',view.height);copy.setAttribute('stroke-width',getComputedStyle(element).strokeWidth);copy.setAttribute('stroke','#000');image=new Image();image.src='data:image/svg+xml;charset=utf-8,'+encodeURIComponent(new XMLSerializer().serializeToString(copy));await image.decode();}
      const canvas=document.createElement('canvas');canvas.width=canvas.height=512;const ctx=canvas.getContext('2d');ctx.drawImage(image,0,0,512,512);const pixels=ctx.getImageData(0,0,512,512).data;let left=512,top=512,right=-1,bottom=-1;
      for(let y=0;y<512;y++)for(let x=0;x<512;x++)if(pixels[(y*512+x)*4+3]>8){left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);}
      const rect=element.getBoundingClientRect(),cell=element.closest('.nav-item,#add-collection,.file-cover,.private-folder-cover').getBoundingClientRect();const centerX=rect.x+(left+right+1)/1024*rect.width,centerY=rect.y+(top+bottom+1)/1024*rect.height;const side=!!element.closest('.sidebar');
      records.push({kind:element.dataset.object||element.closest('[aria-label]')?.getAttribute('aria-label'),dx:centerX-(cell.x+cell.width/2),dy:centerY-(cell.y+cell.height/2),longest:Math.max((right-left+1)/512*rect.width,(bottom-top+1)/512*rect.height),expected:side?20:80});
     }return records;
    });
    for(const item of aligned){assert(Math.abs(item.dx)<1&&Math.abs(item.dy)<1,JSON.stringify(item));assert(Math.abs(item.longest-item.expected)<1,JSON.stringify(item));}
    await page.locator('#main-nav [data-route="video"]').hover();await page.waitForSelector('#navigation-hint.visible');await page.mouse.move(400,110);
    await page.evaluate(async()=>{await setting({theme:'light',uiLanguage:'en'});applyTheme();I18n.applyLanguage('en');navigate('overview');});await page.waitForTimeout(350);await shot('library-light');assert.deepEqual(errors,[]);console.log('Verified Flat icon loading, theme contrast, hover and new kok brand; captured only kok content.');return;
   }
   await page.evaluate(async()=>{await openItem(state.items.find(i=>i.path.toLowerCase().endsWith('.docx')).id);});await page.waitForSelector('#reader-text');await shot('document-dark');await page.evaluate(()=>closeViewer());
   await page.evaluate(async()=>{await openItem(state.items.find(i=>i.kind==='music').id);audio.pause();});await page.waitForSelector('.music-workspace');const playColors=await page.locator('#listening-play').evaluate(el=>({ink:getComputedStyle(el).color,bg:getComputedStyle(el).backgroundColor}));assert.notEqual(playColors.ink,playColors.bg,'Play glyph remains visible against its button');await shot('music-dark');await page.evaluate(()=>closeViewer());
   await page.evaluate(async()=>{audio.pause();await setting({theme:'light',uiLanguage:'en'});applyTheme();I18n.applyLanguage('en');navigate('overview');});await page.waitForTimeout(350);await shot('library-light');assert.deepEqual(errors,[]);console.log('Verified local icon / font loading and play-glyph contrast, captured finished kok views only.');return;
  }
  const nav=page.locator('#main-nav [data-route="video"]');assert.equal(await nav.getAttribute('aria-label'),'视频');
  const n=await nav.boundingBox();assert(Math.abs(n.width-44)<.1);assert(Math.abs(n.height-44)<.1);
  await nav.hover();await page.waitForSelector('#navigation-hint.visible');assert.match(await page.locator('#navigation-hint').innerText(),/视频/);await nav.click();await page.waitForFunction(()=>state.route==='video');
  await page.evaluate(()=>navigate('overview'));await page.locator('#main-nav [data-route="book"]').focus();await page.waitForSelector('#navigation-hint.visible');assert.match(await page.locator('#navigation-hint').innerText(),/读书/);await page.keyboard.press('Escape');
  const squares=await page.locator('.file-grid .file-cover').evaluateAll(list=>list.map(el=>{const r=el.getBoundingClientRect();return{w:r.width,h:r.height};}));assert(squares.length>5);for(const r of squares){assert(Math.abs(r.w-r.h)<1);assert(r.w<=184.1);}
  const first=page.locator('.file-card.video').first();await first.hover();await page.waitForFunction(()=>getComputedStyle(document.querySelector('.file-card.video .card-open-hint')).opacity==='1');assert.equal(await first.locator('.card-open-hint').innerText(),'播放');
  await first.locator('.card-favorite').click();await page.waitForFunction(()=>document.querySelector('.file-card.video .card-favorite.selected'));
  await page.mouse.move(400,110);await shot('library-dark');
  await page.evaluate(async()=>{await openItem(state.items.find(i=>i.kind==='document'&&i.path.toLowerCase().endsWith('.docx')).id);});await page.waitForSelector('#reader-text');await fits('#viewer');await shot('document-dark');await page.evaluate(()=>closeViewer());
  await page.evaluate(async()=>{await openItem(state.items.find(i=>i.kind==='book').id);});await page.waitForSelector('.reader-tools');await fits('#viewer');await page.evaluate(()=>closeViewer());
  await page.evaluate(async()=>{await openItem(state.items.find(i=>i.kind==='photo').id);});await page.waitForFunction(()=>document.querySelector('#photo-image')?.naturalWidth>0);const zoom=await page.evaluate(()=>state.zoom);await page.locator('.photo-image-wrap').hover();await page.mouse.wheel(0,-120);assert(await page.evaluate(previous=>state.zoom>previous,zoom));await fits('#viewer');await page.evaluate(()=>closeViewer());
  await page.evaluate(async()=>{await openItem(state.items.find(i=>i.kind==='music').id);audio.pause();});await page.waitForSelector('.music-workspace');assert.equal(await page.locator('.music-workspace').evaluate(el=>getComputedStyle(el).backgroundColor),await page.locator('body').evaluate(el=>getComputedStyle(el).backgroundColor));await shot('music-dark');await page.evaluate(()=>closeViewer());
  await page.evaluate(async()=>{await openItem(state.items.find(i=>i.kind==='video').id);});await page.waitForFunction(()=>video.readyState>=2);await page.locator('#video-fullscreen').click();await page.waitForFunction(async()=>(await native('diagnostics')).window.fullScreen);await page.keyboard.press('Escape');await page.waitForFunction(async()=>!(await native('diagnostics')).window.fullScreen);await page.evaluate(()=>closeViewer());
  await page.evaluate(async()=>{await setting({theme:'light',uiLanguage:'en'});applyTheme();I18n.applyLanguage('en');navigate('overview');});await page.waitForTimeout(300);
  await page.locator('#main-nav [data-route="photo"]').hover();await page.waitForSelector('#navigation-hint.visible');assert.match(await page.locator('#navigation-hint').innerText(),/Photos/);await page.mouse.move(400,110);await shot('library-light');
  await page.locator('#settings-button').click();await page.waitForSelector('#theme-select');await fits('#small-dialog');assert.equal(await page.locator('#font-select').inputValue(),'smiley');await page.locator('#font-select').selectOption('system');await page.waitForFunction(()=>!getComputedStyle(document.body).fontFamily.includes('Smiley Sans'));await page.locator('#font-select').selectOption('smiley');await page.waitForFunction(()=>getComputedStyle(document.body).fontFamily.includes('Smiley Sans'));await page.locator('#theme-select').click();await page.waitForSelector('.glass-menu');await page.keyboard.press('Escape');await page.evaluate(()=>closeSmall());
  await page.setViewportSize({width:820,height:610});assert(await page.locator('#main').evaluate(el=>el.scrollWidth<=el.clientWidth+1));
  const small=await page.locator('#main-nav [data-route="book"]').boundingBox();assert(Math.abs(small.width-small.height)<.1);
  await page.evaluate(async()=>{await openItem(state.items.find(i=>i.kind==='photo').id);});await page.waitForFunction(()=>document.querySelector('#photo-image')?.naturalWidth>0);await fits('#viewer');await page.evaluate(()=>closeViewer());
  await page.emulateMedia({reducedMotion:'reduce'});assert.equal(await nav.locator('.nav-art').evaluate(el=>getComputedStyle(el).transitionDuration),'0s');assert.deepEqual(errors,[]);assert.equal(await page.evaluate(()=>audio.paused),true);
  console.log('PASS: compact object icons, bilingual pointer/keyboard hints, square covers and hover actions, document/book/photo/music/video opening, fullscreen, settings, compact reflow and reduced motion. Screenshots capture only kok content.');
 }finally{await page.evaluate(()=>{audio.pause();if(viewer.open)closeViewer();}).catch(()=>{});await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});

// Review every navigable page and preview in isolated local state. Never calls a model.
const {chromium}=require('./browser.cjs'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 const output=path.resolve('test-output/interface-audit');fs.mkdirSync(output,{recursive:true});
 const report={screens:[],issues:[],errors:[],started:new Date().toISOString()};let page;
 try{
  page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));page.setDefaultTimeout(12000);
  page.on('pageerror',e=>report.errors.push(e.message));
  await page.evaluate(async()=>{await native('importSamples');await refresh();await setting({fontFamily:'system',theme:'light',uiLanguage:'zh',view:'grid'});applyTheme();await applyFont();navigate('overview');});
  // Include realistic long mixed-script names, without touching original files.
  await page.evaluate(async()=>{const d=await native('createDocument',{name:'排版边界：这是一个很长的中文文件名 — Research notes and observations 2026',format:'md',text:'# 排版检查\n\n用于检查长标题、按钮和阅读区域。\n\nSecond paragraph for layout inspection.',collection:''});await refresh();await patch(byId(d.id),{favorite:true,opened:new Date().toISOString()});});
  async function capture(name,width,height,theme,scope='#main'){
   await page.setViewportSize({width,height});await page.evaluate(async theme=>{await setting({theme});applyTheme();},theme);await page.waitForTimeout(220);if(name==='space-graph')await page.waitForFunction(()=>Warehouse.graph?.ready&&Warehouse.graph.nodes.length>0&&Warehouse.graph.nodes.every(n=>n.opacity>.95));
   const checks=await page.evaluate(scope=>{
    const root=document.querySelector(scope);if(!root)return{missing:scope};
    const r=root.getBoundingClientRect(),controls=[...root.querySelectorAll('button,input,select')].filter(e=>{const s=getComputedStyle(e),b=e.getBoundingClientRect();return s.display!=='none'&&s.visibility!=='hidden'&&b.width&&b.height;});
    return{pageOverflow:document.documentElement.scrollWidth>innerWidth+1,contentOverflow:root.scrollWidth>root.clientWidth+2,
     scrollableCanvases:[...root.querySelectorAll('.space-canvas')].map(e=>({width:e.clientWidth,content:e.scrollWidth})),offscreen:controls.filter(e=>{if(e.closest('.space-canvas'))return false;const b=e.getBoundingClientRect();return b.bottom>0&&b.top<innerHeight&&(b.left<Math.max(0,r.left)-2||b.right>Math.min(innerWidth,r.right)+2);}).map(e=>({text:(e.getAttribute('aria-label')||e.textContent).trim().slice(0,70),class:e.className})).slice(0,12),
     settingsReachable:document.querySelector('#settings-button').getBoundingClientRect().bottom<=innerHeight+1};
   },scope);
   const file=`${String(report.screens.length+1).padStart(3,'0')}-${name}-${theme}-${width}.png`;
   await page.screenshot({path:path.join(output,file),animations:'disabled'});
   const entry={name,width,height,theme,scope,file,checks};report.screens.push(entry);
   if(checks.missing||checks.pageOverflow||checks.offscreen?.length||!checks.settingsReachable)report.issues.push(entry);
   console.log(`SCREEN ${report.screens.length}: ${name} ${theme} ${width} ${JSON.stringify(checks)}`);
  }
  const routes=await page.evaluate(()=>[...new Set([...document.querySelectorAll('#main-nav [data-route],#extra-nav [data-route]')].map(e=>e.dataset.route))]);
  for(const route of routes){
   await page.evaluate(route=>navigate(route),route);await capture(route,1440,1000,'light');
   await page.evaluate(route=>navigate(route),route);await capture(route,960,720,'dark');
   await page.evaluate(route=>navigate(route),route);await capture(route,650,520,'light');
  }
  // The same pages with real saved objects, not just empty-state screenshots.
  const docId=await page.evaluate(()=>state.items.find(i=>i.name.startsWith('排版边界')).id);
  async function approve(){await page.locator('#operation-apply').click();await page.waitForFunction(()=>!$('#small-dialog').open&&!OperationUI.reviewing);}
  await page.evaluate(()=>navigate('spaces'));await page.locator('[data-warehouse="new-space"]').first().click();
  await page.locator('#workspace-form [name="name"]').fill('阅读与研究 · Research materials');
  await capture('space-create',650,520,'light','#small-dialog');await page.locator('#workspace-form [type="submit"]').click();await approve();
  await page.waitForFunction(()=>Warehouse.data.spaces.some(space=>space.name.startsWith('阅读与研究')),{timeout:15000});
  const spaceId=await page.evaluate(()=>Warehouse.data.spaces.find(space=>space.name.startsWith('阅读与研究')).id);
  await page.evaluate(id=>{Warehouse.activeSpace=id;navigate('spaces');},spaceId);
  await page.locator('[data-warehouse="space-pick"]').click();await page.locator('#space-pick-form input[name="item"]').nth(0).check();await page.locator('#space-pick-form input[name="item"]').nth(1).check();await page.locator('#space-pick-form input[name="item"]').nth(2).check();await page.locator('#space-pick-form button').click();await approve();
  for(const mode of ['grid','canvas','timeline','graph']){
   if(mode!=='grid'){await page.locator('#space-view').selectOption(mode);await approve();}
   await capture('space-'+mode,1440,1000,'light');await capture('space-'+mode,650,520,'dark');
  }
  await page.evaluate(id=>{Warehouse.select([id]);return Warehouse.clipForm();},docId);await page.locator('#clip-form [name="text"]').fill('这是一条用于检查摘录卡片、来源和长标题的本地内容。');await page.locator('#clip-form button[type="submit"]').click();await approve();
  await page.evaluate(()=>navigate('clips'));await capture('clips-populated',1440,1000,'light');await capture('clips-populated',650,520,'dark');
  await page.evaluate(()=>navigate('queries'));await page.locator('[data-warehouse="new-query"]').first().click();await page.locator('#workspace-form [name="name"]').fill('全部研究资料');await page.locator('#workspace-form [type="submit"]').click();await approve();
  await page.locator('[data-query]').first().click();await capture('query-populated',1440,1000,'light');await capture('query-populated',650,520,'dark');
  await page.evaluate(id=>{Warehouse.select([id]);navigate('recipes');},docId);await page.locator('[data-warehouse="new-recipe"]').first().click();await page.locator('#recipe-template').selectOption('notes');await page.locator('#recipe-form [name="name"]').fill('阅读后整理笔记');
  await capture('recipe-editor',1440,1000,'light','#small-dialog');await capture('recipe-editor',650,520,'dark','#small-dialog');await page.locator('#recipe-form [type="submit"]').click();await approve();
  await capture('recipes-populated',1440,1000,'light');await capture('recipes-populated',650,520,'dark');
  await page.evaluate(id=>Inspector.show(id),docId);
  for(const tab of ['details','notes','links','ai']){await page.locator('[data-inspector-tab="'+tab+'"]').click();await capture('inspector-'+tab,650,520,'light','#workspace-inspector');}
  await page.evaluate(()=>Inspector.hide());
  // Inspect each supported example through the actual viewer adapter.
  const items=await page.evaluate(()=>state.items.filter(i=>!i.name.startsWith('排版边界')).map(i=>({id:i.id,ext:i.ext,kind:i.kind})));
  for(const item of items){
   try{
    await page.evaluate(async id=>{if(viewer.open)await closeViewer();navigate('overview');await openItem(id,{play:false});},item.id);
    await page.waitForFunction(()=>viewer.open);await page.waitForTimeout(item.ext==='PDF'?800:200);
    await capture('preview-'+item.ext.toLowerCase(),1440,1000,'light','#viewer');
    await capture('preview-'+item.ext.toLowerCase(),960,720,'dark','#viewer');
    await capture('preview-'+item.ext.toLowerCase(),650,520,'light','#viewer');
   }catch(e){report.errors.push('Preview '+item.ext+': '+e.message);}
   await page.evaluate(async()=>{if(viewer.open)await closeViewer();if($('#small-dialog').open)await closeSmall();});
  }
  await page.evaluate(()=>navigate('overview'));
  await page.locator('#settings-button').click();await page.locator('.settings-dialog[open]').waitFor();
  for(const tab of ['appearance','tools','library']){
   await page.locator(`[data-settings-section="${tab}"]`).click();
   await capture('settings-'+tab,1440,1000,'light','#small-dialog');
   await capture('settings-'+tab,650,520,'dark','#small-dialog');
  }
  await page.evaluate(()=>closeSmall());
  await page.evaluate(()=>ModuleUI.open());await capture('modules',1440,1000,'light','#small-dialog');await capture('modules',650,520,'dark','#small-dialog');await page.evaluate(()=>closeSmall());
  await page.locator('#private-space-toggle').click();await capture('private-unlock',1440,1000,'light','#small-dialog');await capture('private-unlock',650,520,'dark','#small-dialog');await page.evaluate(()=>closeSmall());
  await page.keyboard.press('Control+k');await capture('commands',1440,1000,'light','#small-dialog');await capture('commands',650,520,'dark','#small-dialog');await page.evaluate(()=>closeSmall());
  await page.evaluate(async()=>{await setting({uiLanguage:'en'});I18n.applyLanguage('en');navigate('overview');});
  for(const route of ['overview','today','agent','spaces','recipes','downloads']){await page.evaluate(route=>navigate(route),route);await capture(route+'-en',650,520,'light');}
  await page.evaluate(()=>navigate('overview'));await page.setViewportSize({width:1440,height:1000});await page.locator('#settings-button').click();await page.locator('[data-settings-section="tools"]').click();await page.locator('[data-change-app-mode]').click();
  await capture('mode-choice',1440,1000,'light','#app-mode-dialog');await capture('mode-choice',650,520,'dark','#app-mode-dialog');
  await page.evaluate(()=>renderSmartNotice());await capture('mode-notice',650,520,'light','#app-mode-dialog');
 }finally{
  report.finished=new Date().toISOString();fs.writeFileSync(path.join(output,'report.json'),JSON.stringify(report,null,2));
  fs.writeFileSync(path.join(output,'index.html'),'<!doctype html><meta charset="utf-8"><title>kok interface audit</title><style>body{font:14px system-ui;margin:24px;background:#eceef2}main{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:20px}figure{margin:0;background:white;padding:10px}img{width:100%}figcaption{padding:10px 0}</style><h1>kok interface audit</h1><p>'+report.screens.length+' screens; '+report.issues.length+' geometry findings; '+report.errors.length+' runtime findings.</p><main>'+report.screens.map(r=>'<figure><a href="'+r.file+'"><img loading="lazy" src="'+r.file+'"></a><figcaption>'+r.name+' · '+r.theme+' · '+r.width+'×'+r.height+'</figcaption></figure>').join('')+'</main>');
  if(browser)await browser.close();
 }
 console.log('Audit: '+report.screens.length+' screenshots; '+report.issues.length+' geometry findings; '+report.errors.length+' runtime findings.');
 assert.deepEqual(report.errors,[]);assert.deepEqual(report.issues.map(i=>i.name),[]);
})().catch(e=>{console.error(e);process.exit(1);});

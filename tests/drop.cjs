// CDP supplies native-backed local File objects, exercising WebView2 AdditionalObjects and the C# importer.
const{chromium}=require('./browser.cjs');
const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const crypto=require('node:crypto');
const root=path.resolve('test-output/drop-fixtures');fs.mkdirSync(root,{recursive:true});
const originals=[['一段安静的时光.webm','拖入的视频.webm','video'],['林间小调.wav','拖入的音乐.wav','music'],['落日与远山.png','拖入的照片.png','photo'],['整理生活的小清单.docx','拖入的文档.docx','document'],['慢慢收藏生活.epub','拖入的书.epub','book']];
for(const[source,name]of originals)fs.copyFileSync(path.resolve('samples',source),path.join(root,name));
const unknown=path.join(root,'不支持.bin');fs.writeFileSync(unknown,'unsupported');
const nested=path.join(root,'拖入文件夹','子目录');fs.mkdirSync(nested,{recursive:true});
fs.writeFileSync(path.join(nested,'阅读.TXT'),'拖入的中文内容','utf8');
fs.writeFileSync(path.join(nested,'说明.html'),'<h1>Safe source</h1><script>window.DROP_SCRIPT_EXECUTED=true</script>','utf8');
fs.writeFileSync(path.join(nested,'unknown.bin'),'not indexed');
const digest=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const before=Object.fromEntries(originals.map(([,name])=>[name,digest(path.join(root,name))]));
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));
 const session=await page.context().newCDPSession(page);const errors=[];page.on('pageerror',e=>errors.push(e.message));
 const old=await page.evaluate(()=>({settings:{uiLanguage:state.settings.uiLanguage||'zh',theme:state.settings.theme||'light'},collections:[...state.collections]}));
 const data=files=>({items:[],files,dragOperationsMask:1});
 const drag=async(type,files)=>session.send('Input.dispatchDragEvent',{type,x:500,y:300,data:data(files)});
 async function drop(files){
   await page.evaluate(()=>$('#toasts').replaceChildren());await drag('dragEnter',files);await drag('dragOver',files);await drag('drop',files);
   await page.waitForFunction(()=>!$('#import-status').classList.contains('hidden')||$('#toasts').children.length>0);
   await page.waitForFunction(()=>$('#import-status').classList.contains('hidden')&&$('#toasts').children.length>0);
   return page.locator('#toasts').innerText();
 }
 try{
   await page.evaluate(async()=>{if(viewer.open)await closeViewer();if($('#small-dialog').open)await closeSmall();await setting({uiLanguage:'zh',theme:'light'});I18n.applyLanguage('zh');applyTheme();navigate('overview');});
   const files=originals.map(([,name])=>path.join(root,name));
   await drag('dragEnter',files);await drag('dragOver',files);await page.waitForSelector('#file-drop-overlay:popover-open');
   await page.evaluate(()=>I18n.applyLanguage('en'));assert.equal(await page.locator('#file-drop-overlay h2').innerText(),'Drop to import');
   await page.waitForTimeout(450);await page.screenshot({path:'test-output/drop-overlay-en.jpg',type:'jpeg',quality:86,scale:'css'});
   await page.keyboard.press('Escape');await page.waitForFunction(()=>!$('#file-drop-overlay').matches(':popover-open'));
   const interrupted=await page.evaluate(async()=>{
     const data=new DataTransfer();data.items.add(new File(['test'],'motion.txt'));
     const event=type=>document.dispatchEvent(new DragEvent(type,{bubbles:true,cancelable:true,dataTransfer:data,clientX:500,clientY:300}));
     event('dragenter');event('dragleave');await new Promise(r=>setTimeout(r,35));event('dragenter');await new Promise(r=>setTimeout(r,450));
     return{visible:$('#file-drop-overlay').matches(':popover-open'),opacity:Number(getComputedStyle($('#file-drop-overlay')).opacity)};
   });assert(interrupted.visible&&interrupted.opacity>.99,'Reentering during fade-out must keep the latest drop target visible');
   await page.keyboard.press('Escape');await page.waitForFunction(()=>!$('#file-drop-overlay').matches(':popover-open'));
   await page.evaluate(()=>I18n.applyLanguage('zh'));
   assert.match(await drop([...files,unknown]),/已加入 5 个文件，跳过 1/);
   const imported=await page.evaluate(root=>state.items.filter(i=>i.path.startsWith(root)),root);
   assert.equal(imported.length,5);assert.deepEqual(new Set(imported.map(i=>i.kind)),new Set(['video','photo','music','document','book']));
   assert.equal(await page.locator('#main h1').innerText(),'资料库');assert(!await page.locator('#file-drop-overlay').evaluate(el=>el.matches(':popover-open')));
   assert.match(await drop(files),/已加入 0 个文件，跳过 5/);console.log('PASS: native-backed multi-file drops, five categories, Unicode paths, unsupported-file skip and duplicate detection');
   const target=await page.evaluate(()=>state.items.find(i=>i.ext==='PDF').id);await page.evaluate(id=>openItem(id),target);await page.waitForSelector('.pdf-page canvas');
   const folder=path.dirname(nested);await drag('dragEnter',[folder]);await page.waitForSelector('#file-drop-overlay:popover-open');
   assert(await page.locator('#viewer').evaluate(el=>el.open));assert(await page.locator('#file-drop-overlay').evaluate(el=>el.matches(':popover-open')));
   await drag('drop',[folder]);await page.waitForFunction(root=>state.items.filter(i=>i.path.startsWith(root)).length===7,root);
   assert(await page.locator('#viewer').evaluate(el=>el.open),'Importing preserves the active preview');
   await page.evaluate(()=>closeViewer());
   const html=await page.evaluate(root=>state.items.find(i=>i.path.startsWith(root)&&i.ext==='HTML'),root);await page.evaluate(id=>openItem(id),html.id);await page.waitForSelector('#reader-text');
   assert.match(await page.locator('#reader-text').innerText(),/<script>/);assert.equal(await page.evaluate(()=>window.DROP_SCRIPT_EXECUTED),undefined);await page.evaluate(()=>closeViewer());
   console.log('PASS: real directory drops recurse, overlay works over an open preview, and imported HTML remains inert text');
   const rejected=await page.evaluate(async file=>{try{await native('importDropped',{paths:[file]});return false;}catch(e){return e.message.includes('未收到本地文件');}},files[0]);assert(rejected,'JSON path arguments must not bypass native file objects');
   const plain=await page.evaluate(()=>{const data=new DataTransfer();data.setData('text/plain','ordinary selection');const event=new DragEvent('dragenter',{bubbles:true,cancelable:true,dataTransfer:data});document.dispatchEvent(event);return{prevented:event.defaultPrevented,visible:$('#file-drop-overlay').matches(':popover-open')};});assert.deepEqual(plain,{prevented:false,visible:false});
   assert.deepEqual(Object.fromEntries(originals.map(([,name])=>[name,digest(path.join(root,name))])),before);assert.deepEqual(errors,[]);
   console.log('PASS: forged path requests rejected, ordinary text dragging unaffected, original file hashes unchanged');
 }finally{
   await page.evaluate(async({root,old})=>{if(viewer.open)await closeViewer();if($('#small-dialog').open)await closeSmall();for(const item of state.items.filter(i=>i.path.startsWith(root)))await native('remove',{id:item.id});await refresh();for(const name of state.collections.filter(n=>!old.collections.includes(n)))await native('deleteCollection',{name});await setting(old.settings);I18n.applyLanguage(old.settings.uiLanguage);await refresh();}, {root,old});
   await browser.close();
 }
})().catch(e=>{console.error(e);process.exitCode=1;});

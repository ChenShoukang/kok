'use strict';
const paths={
  grid:'M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z',
  layers:'m12 3 9 5-9 5-9-5 9-5Z M3 12l9 5 9-5 M3 16l9 5 9-5',
  video:'M4 5h16v14H4z M10 9l5 3-5 3V9Z',photo:'M4 4h16v16H4z M4 16l5-5 4 4 3-3 4 4 M16 8h.01',
  music:'M9 18V5l12-2v13 M9 8l12-2 M9 18c0 1.7-1.6 3-3.5 3S2 20 2 18.5 3.6 16 5.5 16 9 16.3 9 18Z M21 16c0 1.7-1.6 3-3.5 3S14 18 14 16.5 15.6 14 17.5 14s3.5.3 3.5 2Z',
  document:'M14 3H5v18h14V8l-5-5Z M14 3v5h5 M8 12h8 M8 16h6',book:'M12 5C8 2 3 3 3 3v16s5-1 9 2c4-3 9-2 9-2V3s-5-1-9 2Z M12 5v16',
  heart:'M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8L12 21l8.8-8.6a5.5 5.5 0 0 0 0-7.8Z',
  clock:'M12 8v5l3 2 M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0Z',settings:'m9 3-1 3-3 1-2 4 2 2v3l3 2 1 3h5l1-3 3-2v-3l2-2-2-4-3-1-1-3H9Z M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z',
  shield:'M12 3 3 7v5c0 5 9 10 9 10s9-5 9-10V7l-9-4Z m-4 9 3 3 5-6',search:'M21 21l-5-5 M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0Z',plus:'M12 5v14 M5 12h14',arrow:'M5 12h14 m-5-5 5 5-5 5',chevron:'m9 5 7 7-7 7',
  calendar:'M4 5h16v16H4z M8 3v4 M16 3v4 M4 10h16',leaf:'M20 3S3 2 3 13c0 8 12 9 15 1 2-5 2-11 2-11Z M6 18 16 8',
  folder:'M3 7V4h7l2 3h9v14H3V7Z',list:'M8 6h13 M8 12h13 M8 18h13 M3 6h.01 M3 12h.01 M3 18h.01',more:'M5 12h.01 M12 12h.01 M19 12h.01',
  play:'m9 5 11 7-11 7V5Z',pause:'M7 5h3v14H7z M14 5h3v14h-3z',skipBack:'M5 5v14 M19 5 8 12l11 7V5Z',skipForward:'M19 5v14 M5 5l11 7-11 7V5Z',
  repeat:'m17 2 4 4-4 4 M3 11V9a3 3 0 0 1 3-3h15 M7 22l-4-4 4-4 M21 13v2a3 3 0 0 1-3 3H3',shuffle:'M3 6h3c4 0 8 12 12 12h3 M17 14l4 4-4 4 M3 18h3c2 0 4-3 5-5 M13 9c2-2 3-3 5-3h3 m-4-4 4 4-4 4',
  volume:'M11 4 6 8H3v8h3l5 4V4Z M15 8a6 6 0 0 1 0 8 M18 5a10 10 0 0 1 0 14',listMusic:'M3 5h12 M3 10h12 M3 15h6 M16 18V9l6-1v8 M16 18c0 2-5 3-5 0s5-3 5 0Z',
  close:'m6 6 12 12 M6 18 18 6',edit:'m15 4 5 5 M4 16 16 4a2 2 0 0 1 4 4L8 20l-5 1 1-5Z',external:'M14 3h7v7 M21 3l-11 11 M10 5H4v15h15v-6',
  trash:'M3 6h18 M9 6V3h6v3 M5 6l1 15h12l1-15 M10 10v7 M14 10v7',download:'M12 3v12 m-5-5 5 5 5-5 M4 15v6h16v-6',
  info:'M12 11v6 M12 7h.01 M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0Z',rotate:'M3 10a9 9 0 1 1 2 8 M3 3v7h7',zoomIn:'M10 7v6 M7 10h6 M21 21l-5-5 M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0Z',zoomOut:'M7 10h6 M21 21l-5-5 M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0Z',sun:'M12 3v2 M12 19v2 M3 12h2 M19 12h2 m-3-7-1.5 1.5 M6.5 17.5 5 19 M5 5l1.5 1.5 M17.5 17.5 19 19 M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z'
};
const $=s=>document.querySelector(s);
const svg=name=>`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${paths[name]||paths.document}"/></svg>`;
const esc=value=>String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function hydrate(root=document){root.querySelectorAll('[data-icon]').forEach(el=>{el.innerHTML=svg(el.dataset.icon);});}
const kinds=[['video','视频','光影与故事'],['photo','照片','定格的小美好'],['music','音乐','生活的背景音'],['document','文档','灵感与思考'],['book','读书','字里行间的世界']];
const labels=Object.fromEntries(kinds.map(k=>[k[0],k[1]]));
const pending=new Map();let requestSequence=0;const requestPrefix=crypto.randomUUID()+'-';
function native(action,args={},additionalObjects){
  return new Promise((resolve,reject)=>{
    if(!window.chrome?.webview){reject(new Error('请从 kok.exe 打开应用，以使用本地文件功能。'));return;}
    const requestId=requestPrefix+String(++requestSequence);
    pending.set(requestId,{resolve,reject});
    try{
      if(additionalObjects)window.chrome.webview.postMessageWithAdditionalObjects({requestId,action,args},additionalObjects);
      else window.chrome.webview.postMessage({requestId,action,args});
    }catch(error){pending.delete(requestId);reject(error);}
  });
}
window.chrome?.webview?.addEventListener('message',e=>{
  const m=e.data;
  if(m.eventName==='engineRecovered'){toast('播放界面发生异常，已恢复资料库。可使用系统播放器打开该文件。',true);return;}
  if(m.eventName==='importing'){$('#import-status').classList.remove('hidden');$('#import-status span:last-child').textContent=m.data.count?`已整理 ${m.data.count} 个文件…`:'正在整理你的资料…';return;}
  if(m.eventName==='imported'){$('#import-status').classList.add('hidden');return;}
  const p=pending.get(m.requestId);if(!p)return;pending.delete(m.requestId);m.error?p.reject(new Error(m.error)):p.resolve(m.result);
});
const state={items:[],settings:{},route:'overview',filter:'all',query:'',limit:60,viewer:null,book:null,chapter:0,photoIndex:0,photoList:[],rotation:0,zoom:1,slide:null,queue:[],track:null,shuffle:false,loop:false};
function toast(message,error=false){const el=document.createElement('div');el.className='toast'+(error?' error':'');el.textContent=message;const stack=$('#toasts');stack.append(el);if(typeof CalmMotion!=='undefined')CalmMotion.play(el,[{opacity:0,transform:'translateY(5px)'},{opacity:1,transform:'none'}],{duration:170});if(!stack.matches(':popover-open'))stack.showPopover();setTimeout(async()=>{await dismissElement(el);if(!stack.children.length&&stack.matches(':popover-open'))stack.hidePopover();},error?8000:4500);}
function fail(error){toast(error.message||String(error),true);}
const size=n=>n>=1073741824?(n/1073741824).toFixed(1)+' GB':n>=1048576?(n/1048576).toFixed(1)+' MB':Math.max(1,Math.round(n/1024))+' KB';
const time=s=>{s=Number.isFinite(s)?Math.floor(s):0;return (s>=3600?Math.floor(s/3600)+':':'')+String(Math.floor(s/60)%60).padStart(s>=3600?2:1,'0')+':'+String(s%60).padStart(2,'0');};
const media=(item,type='file')=>`https://media.kok.local/${type}/${item.id}`;
const byId=id=>state.items.find(i=>i.id===id);
async function refresh(){const db=await native('library');state.items=db.items;state.navigation=db.navigation||null;state.settings=db.settings;state.collections=db.collections||[];state.fonts=db.fonts||[];state.isPrivate=!!db.isPrivate;renderPrivateState();applyAppMode();applyTheme();I18n.applyLanguage(state.settings.uiLanguage||'zh');if(typeof applyFont==='function')await applyFont();renderNav();render();if(typeof scheduleVideoCovers==='function')scheduleVideoCovers();}
async function patch(item,values){Object.assign(item,values);await native('update',{id:item.id,patch:values});}
async function setting(values){Object.assign(state.settings,values);await native('settings',values);}
function applyTheme(){let theme=state.settings.theme||'dark';if(theme==='system')theme=matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light';document.body.dataset.theme=theme;}
function renderNav(){
  const navItems=state.navigation?.items||state.items,navCollections=state.navigation?.collections||state.collections||[];
  $('#main-nav').innerHTML=[['overview','全部文件','grid'],...kinds.map(k=>[k[0],k[1],k[0]])].map(([r,l,i])=>`<button class="nav-item ${state.route===r?'active':''}" data-route="${r}" aria-label="${l}">${navigationIcon(i)}<span class="nav-label">${l}</span>${r==='overview'?'':`<span class="count">${navItems.filter(x=>x.kind===r).length||''}</span>`}</button>`).join('');
  $('#extra-nav').innerHTML=[['favorites','收藏','heart'],['recent','最近打开','clock'],['organize','智能整理','layers']].map(([r,l,i])=>`<button class="nav-item ${state.route===r?'active':''}" data-route="${r}" aria-label="${l}">${svg(i)}<span class="nav-label">${l}</span></button>`).join('');
  $('#collections-nav').innerHTML=navCollections.map(name=>`<button class="nav-item ${state.route==='collection'&&state.collection===name?'active':''}" data-collection="${esc(name)}" aria-label="${esc(name)}" data-user-content>${svg('folder')}<span class="nav-label">${esc(name)}</span><span class="count">${navItems.filter(i=>i.collection===name).length}</span></button>`).join('');
  if(isSmartMode()&&!state.isPrivate)$('#extra-nav').insertAdjacentHTML('beforeend',`<button class="nav-item ${state.route==='downloads'?'active':''}" data-route="downloads" aria-label="视频下载">${svg('download')}<span class="nav-label">视频下载</span></button>`);
  updateNavIndicator();
}
function updateNavIndicator(){
 const indicator=$('.nav-indicator'),active=$('.sidebar .nav-item.active');if(!indicator)return;
 if(!active){indicator.style.opacity='0';return;}
 const box=active.getBoundingClientRect(),side=$('.sidebar').getBoundingClientRect();
 const clip=$('.sidebar-scroll').getBoundingClientRect();if(box.top<clip.top-1||box.bottom>clip.bottom+1){indicator.style.opacity='0';return;}
 indicator.style.width=box.width+'px';indicator.style.height=box.height+'px';indicator.style.transform=`translate(${box.left-side.left}px,${box.top-side.top}px)`;indicator.style.opacity='1';
 requestAnimationFrame(()=>indicator.classList.add('ready'));
}
window.addEventListener('resize',updateNavIndicator);
$('#collections-nav').addEventListener('scroll',updateNavIndicator);
function heading(title,subtitle=''){return `<div class="page-heading"><h1 ${state.route==='collection'&&!state.query?'data-user-content':''}>${esc(title)}</h1>${subtitle?`<span>${esc(subtitle)}</span>`:''}</div>`;}
function viewOptions(sort=state.settings.sort||'added'){
 return `<div class="view-options"><select class="sort-select" id="sort" aria-label="排序方式">${[['added','最近加入'],['opened','最近打开'],['name','名称'],['size','文件大小']].map(([key,label])=>`<option value="${key}" ${sort===key?'selected':''}>${label}</option>`).join('')}</select><div class="view-toggle"><button class="${state.settings.view!=='list'?'active':''}" data-view="grid" title="网格视图" aria-label="网格视图">${svg('grid')}</button><button class="${state.settings.view==='list'?'active':''}" data-view="list" title="列表视图" aria-label="列表视图">${svg('list')}</button></div></div>`;
}
function fileCollection(items,includePrivate=false){return items.length||includePrivate?`<div class="${state.settings.view==='list'?'file-list':'file-grid'}">${includePrivate?privateFolderCard():''}${items.slice(0,state.limit).map(card).join('')}</div>${items.length>state.limit?`<button class="button secondary load-more" data-action="loadMore">显示更多</button>`:''}`:'';}
function overview(){
 const items=filtered();
 return `<div class="collection-heading">${heading('资料库',`${items.length} 个文件`)}${viewOptions()}</div>`+(fileCollection(items,true)+(items.length?'':empty('导入你的第一份文件','拖入文件，或点击导入。',true)));
}
function cover(i){
 if(i.kind==='photo')return `<img src="${media(i,'thumb')}" alt="" loading="lazy" data-fallback="photo">${svg('photo')}`;
 if(i.kind==='book'||i.kind==='music')return `<div class="object-cover object-cover-${i.kind}">${libraryObject(i.kind)}</div>`;
 if(i.kind==='video')return `<div class="video-art ${i.hasCover?'has-cover':'object-cover'}">${i.hasCover?`<img src="${media(i,'thumb')}?v=${encodeURIComponent(i.coverVersion||'1')}" alt="" loading="lazy" data-fallback="video">`:libraryObject('video')}<span class="video-cover-play">${svg('play')}</span></div>`;
 return `<div class="object-cover object-cover-document">${libraryObject('document')}</div>`;
}
function card(i){const action=['video','music'].includes(i.kind)?'播放':i.kind==='photo'?'预览':i.kind==='book'?'阅读':'打开';return `<article class="file-card ${i.kind}" data-id="${i.id}"><button class="file-open" data-open="${i.id}" title="打开 ${esc(i.name)}"><div class="file-cover">${cover(i)}<span class="ext">${esc(i.ext)}</span><span class="card-open-hint" aria-hidden="true">${svg(['video','music'].includes(i.kind)?'play':i.kind==='book'?'book':'external')}<span>${action}</span></span></div><div class="file-info"><b>${esc(i.name)}</b><p>${labels[i.kind]}<i></i>${size(i.size)}${i.progress>0?`<i></i>${Math.round(i.progress*100)}%`:''}</p></div></button><button class="card-favorite ${i.favorite?'selected':''}" data-favorite="${i.id}" title="${i.favorite?'取消收藏':'收藏'}" aria-label="${i.favorite?'取消收藏':'收藏'} ${esc(i.name)}">${svg('heart')}</button><button class="card-menu" data-menu="${i.id}" aria-label="${esc(i.name)} 的更多操作" title="更多操作">${svg('more')}</button>${i.progress>0?`<div class="file-progress"><span style="width:${Math.round(i.progress*100)}%"></span></div>`:''}</article>`;}
function empty(title,description,samples=false){return `<div class="empty-state">${svg(state.query?'search':'folder')}<h3>${esc(title)}</h3><p>${esc(description)}</p><div class="empty-actions">${state.query?'':`<button class="button secondary" data-action="importFiles">${svg('plus')}导入文件</button>${samples?'<button class="sample-button" data-action="importSamples">体验示例资料</button>':''}`}</div></div>`;}
function filtered(){
 let items=state.items.filter(i=>{
   const r=state.route;
   if(kinds.some(k=>k[0]===r)&&i.kind!==r)return false;
   if(r==='favorites'&&!i.favorite)return false;
   if(r==='recent'&&!i.opened)return false;
   if(r==='collection'&&i.collection!==state.collection)return false;
   if(state.filter!=='all'&&i.kind!==state.filter)return false;
   const q=state.query.toLocaleLowerCase();
   return !q||[i.name,i.ext,i.notes,i.collection,i.aiSummary,...(i.tags||[])].some(s=>String(s||'').toLocaleLowerCase().includes(q));
 });
 const sort=state.settings.sort||(state.route==='recent'?'opened':'added');
 items.sort((a,b)=>sort==='name'?String(a.name||'').localeCompare(String(b.name||''),'zh-CN'):sort==='size'?(Number(b.size)||0)-(Number(a.size)||0):sort==='opened'?String(b.opened||'').localeCompare(String(a.opened||'')):String(b.added||'').localeCompare(String(a.added||'')));
 return items;
}
function collection(){
 const route=state.route,items=filtered();
 const title=state.query?`搜索结果`:labels[route]||({favorites:'收藏',recent:'最近打开',all:'全部文件',collection:state.collection})[route]||'资料库';
 const showTabs=!labels[route]&&route!=='recent';
 return `<div class="collection-heading">${heading(title,`${items.length} 个文件`)}${viewOptions(state.settings.sort||(route==='recent'?'opened':'added'))}</div>${showTabs?`<div class="library-toolbar"><div class="filter-tabs">${[['all','全部'],...kinds.map(k=>[k[0],k[1]])].map(([key,label])=>`<button class="filter-tab ${state.filter===key?'active':''}" data-filter="${key}">${label}</button>`).join('')}</div></div>`:''}`+(items.length?fileCollection(items):empty(state.query?'没有找到相关文件':route==='favorites'?'还没有收藏':route==='recent'?'还没有打开记录':'这个分类导入你的第一份文件',state.query?'试试文件名、格式或笔记里的关键词。':route==='favorites'?'点击文件上的爱心即可收藏。':'导入文件，或在文件菜单中更改分类。'));
}
let pageAnimation=null,pageTransition=null,renderSequence=0;
async function dismissElement(element){if(!element)return;element.style.pointerEvents='none';await animateOut(element);element.remove();}

function render(){
 renderNav();
 const html=state.isPrivate?privateFolderView():state.route==='downloads'&&isSmartMode()?downloadWorkspace():state.route==='organize'&&!state.query&&typeof renderOrganizer==='function'?renderOrganizer():state.route==='overview'&&!state.query?overview():state.route==='music'&&!state.query&&typeof renderMusic==='function'?renderMusic():collection();
 const key=state.route+'|'+(state.musicAlbum||'')+'|'+(state.musicMode||'');
 const changed=render.previousKey!==key&&!state.query;render.previousKey=key;
 const sequence=++renderSequence;
 if(pageAnimation)pageAnimation.cancel();
 if(pageTransition){pageTransition.skipTransition();pageTransition=null;}
 const update=()=>{if(sequence===renderSequence)$('#main').innerHTML=html;};
 if(changed&&!document.body.classList.contains('booting')&&!reducedMotion()&&document.startViewTransition&&!viewer?.open){
   const transition=document.startViewTransition(update);pageTransition=transition;transition.ready.catch(()=>{});
   transition.finished.catch(()=>{}).finally(()=>{if(pageTransition===transition)pageTransition=null;});
 }else {update();if(changed&&!document.body.classList.contains('booting')&&!reducedMotion())pageAnimation=animateIn($('#main'));}
}
function navigate(route){if(state.isPrivate)return lockPrivateSpace().then(()=>navigate(route));if(route==='organize'&&!isSmartMode())route='overview';state.route=route;state.filter='all';state.limit=60;state.query='';state.musicAlbum=null;$('#search').value='';render();if(route==='organize')loadOrganizer().catch(fail);window.scrollTo(0,0);}
async function importItems(action,additionalObjects){
 const result=await native(action,{},additionalObjects);if(!result||result.cancelled)return;
 await refresh();toast(`已加入 ${result.added} 个文件${result.skipped?`，跳过 ${result.skipped} 个重复或不支持的文件`:''}。`);
 if(result.errors?.length)toast('部分文件未能导入：\n'+result.errors.slice(0,4).join('\n'),true);
 return result;
}
async function favorite(id){const item=byId(id);if(!item)return;await patch(item,{favorite:!item.favorite});render();if(state.viewer?.id===id)$('#viewer-favorite').classList.toggle('selected',item.favorite);if(state.track?.id===id)$('#track-favorite').classList.toggle('selected',item.favorite);}
let smallClose=null;
function showSmall(content){const d=$('#small-dialog'),wasOpen=d.open,interrupted=elementMotions.get(d)?.playState==='running';smallClose=null;closeSmall.token=null;d.inert=false;d.classList.remove('is-closing','settings-dialog','appearance-dialog','reading-settings-dialog','music-info-dialog','music-ai-dialog');d.innerHTML=`<div class="dialog-top"><div></div><button class="icon-button dialog-close" data-close-dialog aria-label="关闭">${svg('close')}</button></div>${content}`;if(!wasOpen)d.showModal();d.scrollTop=0;if(!wasOpen||interrupted)animateIn(d);else if(typeof CalmMotion!=='undefined')CalmMotion.play(d,[{opacity:.9},{opacity:1}],{duration:120});}
function closeSmall(){
 const d=$('#small-dialog');if(!d.open)return Promise.resolve();if(smallClose)return smallClose;
 const token={};closeSmall.token=token;d.inert=true;d.classList.add('is-closing');
 smallClose=animateOut(d).then(()=>{if(closeSmall.token!==token)return;d.close();d.inert=false;d.classList.remove('is-closing');smallClose=null;});return smallClose;
}
$('#small-dialog').addEventListener('cancel',e=>{e.preventDefault();closeSmall();});
function fileMenu(id){const i=byId(id);if(!i)return;showSmall(`<h2 data-user-content>${esc(i.name)}</h2><p class="dialog-description">${labels[i.kind]} · ${esc(i.ext)} · ${size(i.size)}</p>${i.tags?.length||i.aiSummary?`<div class="organizer-metadata" data-user-content><div class="organizer-tags">${(i.tags||[]).map(t=>`<span>${esc(t)}</span>`).join('')}</div>${esc(i.aiSummary||'')}</div>`:''}<div class="menu-actions"><button data-edit-organization="${id}">${svg('layers')}编辑分类与标签</button><button data-open="${id}">${svg('external')}在 kok 中打开</button><button data-favorite="${id}">${svg('heart')}${i.favorite?'取消收藏':'加入收藏'}</button><button data-classify="${id}">${svg('layers')}<span>分类归纳</span>${i.collection?`<span data-user-content> · ${esc(i.collection)}</span>`:''}</button>${i.kind==='music'?`<button data-album-edit="${id}">${svg('edit')}编辑专辑和歌手</button>`:''}<button data-native="reveal" data-id="${id}">${svg('folder')}在文件夹中显示</button><button data-native="openExternal" data-id="${id}">${svg('external')}使用系统应用打开</button><button class="danger" data-remove="${id}">${svg('trash')}从资料库移除</button></div><div class="dialog-footnote">移除仅删除资料库记录，原文件会保留。<br>${esc(i.path)}</div>`);}
async function settings(){
 const diag=await native('diagnostics');
 showSmall(`<h2>设置</h2><div class="setting-row"><div>界面外观</div><select id="theme-select" aria-label="界面外观"><option value="light">浅色</option><option value="dark">深色</option><option value="system">跟随系统</option></select></div><div class="setting-row"><div>资料库备份<small>${state.isPrivate?'导出加密索引与笔记，不包含原文件':'导出索引、进度与笔记，不包含原文件'}</small></div><button class="button secondary" data-action="export">${svg('download')}导出</button></div><div class="setting-row"><div>体验示例<small>加入五类本地示例文件</small></div><button class="button secondary" data-action="importSamples">添加示例</button></div><details class="about-details"><summary>关于 kok</summary><div class="dialog-footnote"><b>kok</b><br>v${esc(diag.version)} · Windows x64 · WebView2 ${esc(diag.engine)}<br>本地保存 · AI 翻译需联网<br><br>资料库位置：<br><code>${esc(diag.dataDir)}</code><br><br>快捷键：Ctrl K 搜索 · Esc 关闭预览<br>照片：← → 切换 · 阅读：← → 切换章节<br>音乐：空格播放 / 暂停 · 视频：可在播放偏好中设置<br><br>兼容性：加密电子书、OCR、Office 原始排版和部分媒体编码需使用相应系统应用。</div></details>`);
 $('#theme-select').value=state.settings.theme||'dark';
 setupCompatibility();
 if(typeof setupSettings==='function')setupSettings();setupAppModeSettings();I18n.setupSettings();animateSettings();
}
const viewer=$('#viewer');let readerTimer=null,video=null,mediaSaveTime=0,closingViewer=null;
async function openItem(id,{play=true,keepQueue=false}={}){
 if(closingViewer)await closingViewer;
 const item=byId(id);if(!item)return;
 if($('#small-dialog').open)await closeSmall();
 await patch(item,{opened:new Date().toISOString()});
 if(viewer.open){await saveViewer();clearViewer();}else {viewer.showModal();$('#viewer-content').tabIndex=-1;$('#viewer-content').focus({preventScroll:true});animateIn(viewer);}
 viewer.dataset.kind=item.kind;state.viewer=item;beginMediaLayout(item);beginFileAssistant(item);if(typeof beginTranslationView==='function')beginTranslationView(item);viewer.classList.remove('notes-open');$('#notes-panel').classList.add('hidden');$('#notes').value=item.notes||'';
 $('#viewer-title').textContent=item.name;$('#viewer-subtitle').textContent=`${labels[item.kind]} · ${item.ext} · ${size(item.size)}`;$('#viewer-icon').innerHTML=svg(item.kind);$('#viewer-favorite').classList.toggle('selected',item.favorite);
 $('#viewer-content').innerHTML='<div class="loading-view"><span class="spinner"></span>正在打开…</div>';
 const token=item.id;
 if(item.kind==='music'){if(!keepQueue)state.queue=state.items.filter(i=>i.kind==='music'&&(!state.musicAlbum||albumName(i)===state.musicAlbum));await playTrack(item,{autoplay:play});renderMusicWorkspace();render();return;}
 if(item.kind==='video'){renderVideo(item);return;}
 if(item.kind==='photo'){state.photoList=filtered().filter(i=>i.kind==='photo');if(!state.photoList.some(i=>i.id===id))state.photoList=state.items.filter(i=>i.kind==='photo');state.photoIndex=state.photoList.findIndex(i=>i.id===id);state.rotation=0;state.zoom=1;renderPhoto();return;}
 if(item.ext==='PDF'){try{await renderPdf(item);}catch(e){if(state.viewer?.id===item.id){$('#viewer-content').innerHTML=empty('PDF 暂时无法预览',e.message);fail(e);}}return;}
 try{const book=await native('read',{id});if(state.viewer?.id!==token)return;state.book=book;state.chapter=Math.min(item.chapter||0,book.chapters.length-1);renderReader(item.position||0);}catch(e){if(state.viewer?.id!==token)return;$('#viewer-content').innerHTML=`<div class="empty-state" style="height:100%;border:0">${svg('document')}<h3>用熟悉的应用，继续打开</h3><p>${esc(e.message)}</p><button class="button primary" data-native="openExternal" data-id="${item.id}">${svg('external')}使用系统应用打开</button></div>`;}
}
function renderVideo(item){renderVideoWorkspace(item);}
async function saveVideo(){if(video&&state.viewer&&Number.isFinite(video.duration)&&video.duration>0)await patch(state.viewer,{position:video.currentTime,progress:video.currentTime/video.duration});}
function renderPhoto(){
 viewer.classList.remove('photo-editing');
 cleanupWorkspace();const i=state.photoList[state.photoIndex];if(!i)return;state.viewer=i;state.photoPanX=0;state.photoPanY=0;beginMediaLayout(i);
 $('#viewer-title').textContent=i.name;$('#viewer-subtitle').textContent=`照片 · ${i.ext} · ${size(i.size)}`;$('#viewer-favorite').classList.toggle('selected',i.favorite);$('#notes').value=i.notes||'';
 $('#viewer-content').innerHTML=`<div class="media-stage"><div class="photo-image-wrap"><img id="photo-image" src="${media(i)}" alt="${esc(i.name)}"></div><div class="photo-tools"><button class="icon-button" data-photo="prev" title="上一张" aria-label="上一张">${svg('skipBack')}</button><span>${state.photoIndex+1} / ${state.photoList.length}</span><button class="icon-button" data-photo="next" title="下一张" aria-label="下一张">${svg('skipForward')}</button><button class="icon-button" data-photo="out" title="缩小" aria-label="缩小">${svg('zoomOut')}</button><span id="zoom-label">100%</span><button class="icon-button" data-photo="in" title="放大" aria-label="放大">${svg('zoomIn')}</button><button class="icon-button" data-photo="rotate" title="旋转" aria-label="旋转">${svg('rotate')}</button><button class="icon-button" data-photo="slideshow" title="幻灯片播放或暂停" aria-label="幻灯片播放或暂停">${svg(state.slide?'pause':'play')}</button></div></div>`;
 const photoObserver=new ResizeObserver(()=>transformPhoto());photoObserver.observe($('.photo-image-wrap'));workspaceDisposers.push(()=>photoObserver.disconnect());$('#photo-image').addEventListener('load',transformPhoto);
 $('#photo-image').addEventListener('error',()=>{$('.photo-image-wrap').innerHTML=`<div class="photo-error">此照片格式暂不支持内置预览。<br>请点击右上角的「系统应用打开」。</div>`;});
 transformPhoto();setupPhotoInteraction();
 const tools=$('.photo-tools');if(tools)tools.insertAdjacentHTML('beforeend',`<button class="icon-button" data-photo-edit title="编辑照片" aria-label="编辑照片">${svg('edit')}<span>编辑</span></button>`);
}
function transformPhoto(){
 const img=$('#photo-image'),wrap=$('.photo-image-wrap');
 if(img&&wrap&&img.naturalWidth){const rotated=Math.abs(state.rotation%180)===90,w=rotated?img.naturalHeight:img.naturalWidth,h=rotated?img.naturalWidth:img.naturalHeight;const fit=Math.max(.001,Math.min((wrap.clientWidth-24)/w,(wrap.clientHeight-24)/h));img.style.width=(img.naturalWidth*fit)+'px';img.style.height=(img.naturalHeight*fit)+'px';const maxX=Math.max(0,(w*fit*state.zoom-wrap.clientWidth)/2),maxY=Math.max(0,(h*fit*state.zoom-wrap.clientHeight)/2);state.photoPanX=Math.max(-maxX,Math.min(maxX,state.photoPanX||0));state.photoPanY=Math.max(-maxY,Math.min(maxY,state.photoPanY||0));img.style.transform=`translate(${state.photoPanX}px,${state.photoPanY}px) rotate(${state.rotation}deg) scale(${state.zoom})`;wrap.classList.toggle('photo-zoomed',state.zoom>1.001);}
 const label=$('#zoom-label');if(label)label.textContent=Math.round(state.zoom*100)+'%';
}
async function photoAction(action){
 if(action==='next'||action==='prev'){
  await saveNotes();state.photoIndex=(state.photoIndex+(action==='next'?1:-1)+state.photoList.length)%state.photoList.length;state.zoom=1;state.rotation=0;renderPhoto();await patch(state.viewer,{opened:new Date().toISOString()});
 }else if(action==='rotate'){state.rotation+=90;state.photoPanX=state.photoPanY=0;transformPhoto();scheduleMediaFit();}
 else if(action==='in'||action==='out'){zoomPhoto(state.zoom+(action==='in'?.25:-.25));}
 else if(action==='slideshow'){if(state.slide){clearInterval(state.slide);state.slide=null;}else{state.slide=setInterval(()=>photoAction('next').catch(fail),4000);}const button=$('[data-photo="slideshow"]');button.innerHTML=svg(state.slide?'pause':'play');}
}
function renderReader(scroll=0){renderReadingWorkspace(scroll);}
function readerPosition(){const el=$('#reader-scroll');return el?Math.max(0,Math.min(1,el.scrollTop/Math.max(1,el.scrollHeight-el.clientHeight))):0;}
async function saveReader(){if(!state.book||!state.viewer||!$('#reader-scroll'))return;const el=$('#reader-scroll');const ratio=el.scrollHeight<=el.clientHeight?1:readerPosition();await patch(state.viewer,{chapter:state.chapter,position:readerPosition(),progress:(state.chapter+ratio)/state.book.chapters.length});}
async function changeChapter(n){if(!state.book||n<0||n>=state.book.chapters.length)return;await saveReader();state.chapter=n;renderReader(0);animateIn($('#reader-scroll'));await patch(state.viewer,{chapter:n,position:0,progress:n/state.book.chapters.length});}
async function saveNotes(){if(state.viewer&&$('#notes').value!==(state.viewer.notes||''))await patch(state.viewer,{notes:$('#notes').value});}
async function saveViewer(){clearTimeout(readerTimer);if(state.viewer?.kind==='music')await saveAudio();await saveVideo();await saveReader();await saveNotes();if(typeof savePdf==='function')await savePdf();}
function clearViewer(){resetFileAssistant();cleanupWorkspace();if(typeof cleanupFeatures==='function')cleanupFeatures();if(video){video.pause();video.removeAttribute('src');video.load();video=null;}if(state.slide)clearInterval(state.slide);state.slide=null;state.book=null;state.viewer=null;$('#viewer-content').innerHTML='';}
async function closeViewer(){if(closingViewer)return closingViewer;closingViewer=(async()=>{if(document.fullscreenElement)await document.exitFullscreen();await saveViewer();viewer.classList.add('is-closing');await animateOut(viewer);clearViewer();viewer.close();viewer.classList.remove('is-closing');render();})();try{await closingViewer;}finally{closingViewer=null;}}
// The audio element survives navigation, so a collection can be browsed while music plays.
const audio=$('#audio');let audioSaveAt=0;
async function playTrack(item,{autoplay=true}={}){
 if(video)video.pause();
 if(state.track?.id!==item.id){if(state.viewer?.kind==='music')await saveNotes();await saveAudio();audio.pause();state.track=item;audio.src=media(item);audio.load();}
 if(!state.queue.some(i=>i.id===item.id))state.queue=state.items.filter(i=>i.kind==='music'&&(!state.musicAlbum||(i.album||'未命名专辑')===state.musicAlbum));
 $('#music-bar').classList.remove('hidden');document.body.classList.add('has-player');
 $('#track-name').textContent=item.name;$('#track-subtitle').textContent=`${item.ext} · 本地音乐`;$('#track-favorite').classList.toggle('selected',item.favorite);
 if(state.viewer?.kind==='music')syncMusicWorkspace(true);
 try{if(autoplay)await audio.play();}catch(e){if(e.name!=='AbortError')toast('这首音乐暂时无法内置播放，可从更多操作中使用系统应用打开。',true);}
}
async function saveAudio(){if(state.track&&Number.isFinite(audio.duration)&&audio.duration>0)await patch(state.track,{position:audio.currentTime,progress:audio.currentTime/audio.duration});}
async function stepTrack(delta){
 if(!state.track||!state.queue.length)return;
 let index=state.queue.findIndex(i=>i.id===state.track.id);
 if(state.shuffle&&state.queue.length>1){let next=index;while(next===index)next=Math.floor(Math.random()*state.queue.length);index=next;}else index=(index+delta+state.queue.length)%state.queue.length;
 const item=state.queue[index];await patch(item,{opened:new Date().toISOString()});await playTrack(item);
}
function updateTransport(){const playing=!audio.paused;$('#play-pause').innerHTML=svg(playing?'pause':'play');$('#shuffle').classList.toggle('enabled',state.shuffle);$('#repeat').classList.toggle('enabled',state.loop);syncMusicWorkspace();}
audio.addEventListener('play',updateTransport);audio.addEventListener('pause',()=>{updateTransport();saveAudio().catch(fail);});
audio.addEventListener('loadedmetadata',()=>{if(state.track?.position>0&&state.track.position<audio.duration-.1)audio.currentTime=state.track.position;$('#duration').textContent=time(audio.duration);syncMusicWorkspace();});
audio.addEventListener('timeupdate',()=>{syncMusicWorkspace();$('#current-time').textContent=time(audio.currentTime);$('#duration').textContent=time(audio.duration);$('#audio-seek').value=Number.isFinite(audio.duration)&&audio.duration>0?Math.round(audio.currentTime/audio.duration*1000):0;if(Date.now()-audioSaveAt>5000){audioSaveAt=Date.now();saveAudio().catch(fail);}});
audio.addEventListener('ended',async()=>{try{await saveAudio();if(state.loop){audio.currentTime=0;await audio.play();}else if(state.queue.length>1)await stepTrack(1);}catch(e){fail(e);}});
audio.addEventListener('error',()=>{if(audio.getAttribute('src'))toast('音频解码失败：请使用系统音乐播放器打开此文件。',true);});
$('#play-pause').addEventListener('click',()=>{if(audio.paused)audio.play().catch(fail);else audio.pause();});
$('#prev-track').addEventListener('click',()=>stepTrack(-1).catch(fail));$('#next-track').addEventListener('click',()=>stepTrack(1).catch(fail));
$('#shuffle').addEventListener('click',()=>{state.shuffle=!state.shuffle;setting({shuffle:state.shuffle}).catch(fail);updateTransport();});
$('#repeat').addEventListener('click',()=>{state.loop=!state.loop;setting({loop:state.loop}).catch(fail);updateTransport();});
$('#audio-seek').addEventListener('input',e=>{if(Number.isFinite(audio.duration))audio.currentTime=Number(e.target.value)/1000*audio.duration;});
$('#volume').addEventListener('input',e=>{audio.volume=Number(e.target.value);});$('#volume').addEventListener('change',()=>setting({volume:audio.volume}).catch(fail));
$('#track-favorite').addEventListener('click',()=>{if(state.track)favorite(state.track.id).catch(fail);});
$('#close-player').addEventListener('click',async()=>{try{audio.pause();await saveAudio();audio.removeAttribute('src');audio.load();state.track=null;$('#music-bar').classList.add('hidden');document.body.classList.remove('has-player');render();}catch(e){fail(e);}});
$('#queue-button').addEventListener('click',()=>{showSmall(`<h2>这一刻的旋律</h2><p class="dialog-description">${state.queue.length} 首本地音乐</p>${state.queue.map((i,n)=>`<button class="queue-item ${state.track?.id===i.id?'active':''}" data-open="${i.id}"><small>${String(n+1).padStart(2,'0')}</small>${svg('music')}<span>${esc(i.name)}</span></button>`).join('')}`);});
$('#search').addEventListener('input',e=>{state.query=e.target.value.trim();state.limit=60;render();});
$('#import-top').addEventListener('click',()=>importItems('importFiles').catch(fail));$('#settings-button').addEventListener('click',()=>settings().catch(fail));
$('#close-viewer').addEventListener('click',()=>closeViewer().catch(fail));viewer.addEventListener('cancel',e=>{e.preventDefault();if(document.fullscreenElement){document.exitFullscreen().catch(fail);return;}if(Date.now()-Number(viewer.dataset.fullscreenExited||0)<300)return;closeViewer().catch(fail);});
$('#viewer-favorite').addEventListener('click',()=>{if(state.viewer)favorite(state.viewer.id).catch(fail);});
$('#external-button').addEventListener('click',()=>{if(state.viewer)native('openExternal',{id:state.viewer.id}).catch(fail);});
$('#notes-button').addEventListener('click',()=>{if(typeof CalmMotion!=='undefined'){CalmMotion.toggleNotes();return;}$('#notes-panel').classList.toggle('hidden');viewer.classList.toggle('notes-open',!$('#notes-panel').classList.contains('hidden'));if(typeof renderAnnotationIndex==='function')renderAnnotationIndex();});$('#save-notes').addEventListener('click',async()=>{try{await saveNotes();toast('笔记已保存。');}catch(e){fail(e);}});
document.addEventListener('click',async e=>{
 const el=e.target.closest('button,[data-route]');if(!el)return;
 try{
  if(el.dataset.route){navigate(el.dataset.route);return;}
  if(el.hasAttribute('data-close-dialog')){await closeSmall();return;}
  if(el.dataset.open){await openItem(el.dataset.open);return;}
  if(el.dataset.favorite){await favorite(el.dataset.favorite);if($('#small-dialog').open)await closeSmall();return;}
  if(el.dataset.menu){fileMenu(el.dataset.menu);return;}
  if(el.dataset.native){await native(el.dataset.native,{id:el.dataset.id});return;}
  if(el.dataset.remove){const item=byId(el.dataset.remove);await native('remove',{id:item.id});if(state.track?.id===item.id)$('#close-player').click();state.items=state.items.filter(i=>i.id!==item.id);state.queue=state.queue.filter(i=>i.id!==item.id);await closeSmall();render();toast('已从资料库移除，原文件已保留。');return;}
  if(el.dataset.filter){state.filter=el.dataset.filter;state.limit=60;render();return;}
  if(el.dataset.view){await setting({view:el.dataset.view});render();return;}
  if(el.dataset.photo){await photoAction(el.dataset.photo);return;}
  if(el.hasAttribute('data-chapter')){await changeChapter(Number(el.dataset.chapter));return;}
  if(el.dataset.reader){await changeChapter(state.chapter+(el.dataset.reader==='next'?1:-1));return;}
  if(el.dataset.font){const scroll=readerPosition();await setting({readerSize:Math.max(12,Math.min(32,(Number(state.settings.readerSize)||18)+Number(el.dataset.font)))});renderReader(scroll);return;}
  if(el.dataset.paper){const scroll=readerPosition();await setting({readerTheme:el.dataset.paper,readerFollowTheme:el.dataset.paper==='auto'});renderReader(scroll);return;}
  if(el.dataset.action){if(el.dataset.action.startsWith('import')){await importItems(el.dataset.action);if($('#small-dialog').open)await closeSmall();}else if(el.dataset.action==='export'){if(await native('export'))toast('资料库索引与笔记已导出。');}else if(el.dataset.action==='loadMore'){state.limit+=60;render();}return;}
 }catch(error){fail(error);}
});
document.addEventListener('change',async e=>{try{if(e.target.id==='sort'){await setting({sort:e.target.value});render();}if(e.target.id==='theme-select'){await setting({theme:e.target.value});applyTheme();}}catch(error){fail(error);}});
document.addEventListener('error',e=>{const img=e.target;if(img instanceof HTMLImageElement&&img.dataset.fallback)img.remove();},true);
document.addEventListener('load',e=>{const img=e.target;if(img instanceof HTMLImageElement&&img.dataset.fallback){const next=img.nextElementSibling;if(next?.tagName.toLowerCase()==='svg')next.style.display='none';}},true);
document.addEventListener('keydown',e=>{
 const editable=e.target.matches('input,textarea,select,[contenteditable]');
 if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='k'){e.preventDefault();if(!viewer.open&&!$('#small-dialog').open)$('#search').focus();return;}
 if(editable||$('#small-dialog').open)return;
 if(viewer.open&&state.viewer?.kind==='photo'&&['ArrowLeft','ArrowRight'].includes(e.key)){e.preventDefault();photoAction(e.key==='ArrowRight'?'next':'prev').catch(fail);}
 if(viewer.open&&state.book&&['ArrowLeft','ArrowRight'].includes(e.key)){e.preventDefault();changeChapter(state.chapter+(e.key==='ArrowRight'?1:-1)).catch(fail);}
 if(e.code==='Space'&&!video&&state.track&&(!viewer.open||state.viewer?.kind==='music')){e.preventDefault();const player=audio;if(player.paused)player.play().catch(fail);else player.pause();}
});
matchMedia('(prefers-color-scheme: dark)').addEventListener('change',()=>{if(state.settings.theme==='system')applyTheme();});
async function init(){await I18n.ready;hydrate();try{await refresh();audio.volume=Math.max(0,Math.min(1,Number(state.settings.volume??.7)));$('#volume').value=audio.volume;state.shuffle=!!state.settings.shuffle;state.loop=!!state.settings.loop;updateTransport();await initializeAppMode();}catch(e){$('#main').innerHTML=heading('欢迎来到 kok','给喜欢的一切，留一点空间。')+empty('无法打开资料库',e.message);}finally{finishStartup();}}
async function finishStartup(){
 const screen=$('#startup');if(!screen)return;
 try{await screen.querySelector('img').decode();}catch{}
 const reduced=reducedMotion();
 const delay=reduced?0:Math.max(0,780-performance.now());
 setTimeout(()=>{
   document.body.classList.remove('booting');document.body.setAttribute('aria-busy','false');
   if(reduced){screen.remove();return;}
   screen.classList.add('leaving');
   for(const [selector,x,y,delay] of [['.sidebar',-8,0,70],['.topbar',0,6,110],['#main',0,12,160]]){
     $(selector).animate([{opacity:0,transform:`translate(${x}px,${y}px)`},{opacity:1,transform:'translate(0,0)'}],{duration:650,delay,easing:'cubic-bezier(.16,1,.3,1)',fill:'backwards'});
   }
   screen.addEventListener('transitionend',e=>{if(e.target===screen&&e.propertyName==='opacity')screen.remove();},{once:true});
   setTimeout(()=>screen.remove(),750);
 },delay);
}
$('#import-more').addEventListener('click',()=>{const menu=$('#import-menu');const open=menu.classList.toggle('hidden')===false;$('#import-more').setAttribute('aria-expanded',String(open));if(open)animateIn(menu);});
document.addEventListener('click',e=>{if(!e.target.closest('.import-control')){$('#import-menu').classList.add('hidden');$('#import-more').setAttribute('aria-expanded','false');}else if(e.target.closest('[data-action]')){$('#import-menu').classList.add('hidden');$('#import-more').setAttribute('aria-expanded','false');}});
document.addEventListener('keydown',e=>{if(e.key==='Escape'){$('#import-menu').classList.add('hidden');$('#import-more').setAttribute('aria-expanded','false');}});
document.addEventListener('DOMContentLoaded',init,{once:true});

$('.sidebar-scroll')?.addEventListener('scroll',updateNavIndicator,{passive:true});

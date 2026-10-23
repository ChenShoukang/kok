'use strict';
// Desktop features share the same local bridge and persistent library as app.js.
let selectionAnchor=null,pdfState=null,photoEditor=null,subtitleUrl=null,speedTimer=null,videoAudioContext=null;
let subtitleTracks=[];
const fontStacks={system:"'Segoe UI','Microsoft YaHei UI',sans-serif",yahei:"'Microsoft YaHei','Segoe UI',sans-serif",song:"'SimSun','Songti SC',serif",kai:"'KaiTi','STKaiti','SimSun',serif"};
const uid=()=>crypto.randomUUID().replaceAll('-','');
function albumName(item){return item.album||'未命名专辑';}
function hashHue(value){let n=0;for(const c of value)n=(n*31+c.charCodeAt(0))%360;return n;}
async function applyFont(){
 const name=state.settings.fontFamily||'system';let family=fontStacks[name];
 if(!family){const asset=(state.fonts||[]).find(f=>f.id===name);if(asset){family=`'kokFont${asset.id}'`;if(![...document.fonts].some(f=>f.family==='kokFont'+asset.id)){try{const font=new FontFace('kokFont'+asset.id,`url(https://media.kok.local/font/${asset.id})`);await font.load();document.fonts.add(font);}catch(error){family=fontStacks.system;await setting({fontFamily:'system'});toast('字体无法加载，已恢复系统字体。',true);}}}}
 document.documentElement.style.setProperty('--user-font',family||fontStacks.system);
 document.documentElement.style.setProperty('--reading-font',name==='system'?"'Georgia','SimSun',serif":family||fontStacks.system);
}
function setupSettings(){
 const row=document.createElement('div');row.className='setting-row';row.innerHTML=`<div>常用字体<small>同时应用于界面与正文阅读</small></div><select id="font-select" aria-label="字体选择">${[['system','系统默认'],['yahei','微软雅黑'],['song','宋体'],['kai','楷体'],...(state.fonts||[]).map(f=>[f.id,f.name])].map(([v,l])=>`<option value="${esc(v)}">${esc(l)}</option>`).join('')}</select>`;
 $('#theme-select').closest('.setting-row').after(row);$('#font-select').value=state.settings.fontFamily||'system';
 row.insertAdjacentHTML('afterend',`<div class="setting-row"><div>导入字体<small>本地 TTF / OTF，最多 20 MB</small></div><button class="button secondary" data-import-font>${svg('plus')}导入</button></div>`);
 $('#small-dialog .about-details').insertAdjacentHTML('beforebegin','<div class="setting-row"><div>视频操作<small>快捷键、跳跃距离与双击全屏</small></div><button class="button secondary" data-video-preferences>播放偏好</button></div>');
 if(typeof setupTranslationSettings==='function')setupTranslationSettings();
}
function newCollection(itemId){showSmall(`<h2>${itemId?'整理到一个分类':'建立自己的分类'}</h2><p class="dialog-description">文件会按类型与来源文件夹自动归纳，也可以按项目、旅行或主题重新整理。</p>${itemId?`<label class="form-label">已有分类<select id="collection-existing"><option value="">不归类</option>${(state.collections||[]).map(n=>`<option data-user-content value="${esc(n)}">${esc(n)}</option>`).join('')}</select></label>`:''}<label class="form-label">${itemId?'或者新建分类':'分类名称'}<input id="collection-name" maxlength="100" placeholder="例如：今年的旅行"></label><button class="button primary dialog-submit" data-save-collection="${itemId||'new'}">保存分类</button>`);if(itemId)$('#collection-existing').value=byId(itemId).collection||'';$('#collection-name').focus();}
function albumArt(name,large=false){return `<div class="album-art ${large?'large':''}" style="--album-hue:${hashHue(name)}"><div class="album-art-orb"></div><div class="album-vinyl"></div><span>kok</span><small ${name==='未命名专辑'?'':'data-user-content'}>${esc(name)}</small></div>`;}
function renderMusic(){
 const songs=state.items.filter(i=>i.kind==='music');const albums=[...new Set(songs.map(albumName))];
 if(state.musicAlbum){const tracks=songs.filter(i=>albumName(i)===state.musicAlbum);return `<button class="text-button album-back" data-album-back>← 所有专辑</button><section class="album-hero">${albumArt(state.musicAlbum,true)}<div><span class="album-kind">专辑</span><h1 ${state.musicAlbum==='未命名专辑'?'':'data-user-content'}>${esc(state.musicAlbum)}</h1><p>${tracks.length} 首歌曲 · ${esc([...new Set(tracks.map(i=>i.artist||'本地音乐'))].join(' / '))}</p><div class="album-actions"><button class="button primary" data-play-album="${esc(state.musicAlbum)}">${svg('play')}播放专辑</button><button class="button secondary" data-action="importFiles">${svg('plus')}导入</button></div></div></section><div class="track-table"><div class="track-table-heading"><span>#</span><span>歌曲</span><span>格式 / 大小</span><span></span></div>${tracks.map((i,n)=>`<article class="track-row"><button class="track-number" data-open="${i.id}" aria-label="播放 ${esc(i.name)}">${state.track?.id===i.id?svg('music'):String(n+1).padStart(2,'0')}</button><button class="track-title" data-open="${i.id}"><b>${esc(i.name)}</b><small ${!i.artist||i.artist==='本地音乐'?'':'data-user-content'}>${esc(i.artist||'本地音乐')}</small></button><span>${esc(i.ext)} · ${size(i.size)}</span><div><button class="icon-button ${i.favorite?'selected':''}" data-favorite="${i.id}" aria-label="收藏 ${esc(i.name)}">${svg('heart')}</button><button class="icon-button" data-menu="${i.id}" aria-label="更多操作">${svg('more')}</button></div></article>`).join('')}</div>`;}
 return heading('音乐',songs.length+' 首歌曲')+`<div class="library-toolbar"><div class="filter-tabs"><button class="filter-tab ${state.musicMode!=='songs'?'active':''}" data-music-mode="albums">专辑</button><button class="filter-tab ${state.musicMode==='songs'?'active':''}" data-music-mode="songs">全部歌曲</button></div><span class="subtle-label">${albums.length} 张专辑 · ${songs.length} 首音乐</span></div>${!songs.length?empty('你的第一张专辑，从这里开始','导入音乐，会按照文件夹自动归入专辑。'):state.musicMode==='songs'?`<div class="file-grid">${songs.map(card).join('')}</div>`:`<div class="album-grid">${albums.map(name=>`<button class="album-card" data-album="${esc(name)}">${albumArt(name)}<b ${name==='未命名专辑'?'':'data-user-content'}>${esc(name)}</b><small>${songs.filter(i=>albumName(i)===name).length} 首歌曲</small></button>`).join('')}</div>`}`;
}
async function initVideoFeatures(item,player){
 subtitleTracks=[];$('#video-rate').value=String(state.settings.videoRate||1);player.playbackRate=Number(state.settings.videoRate)||1;
 $('#smart-speed').classList.toggle('enabled',!!state.settings.smartSpeed);
 let silenceAt=0,analyser=null,data=null;
 function analyse(){
   if(!state.settings.smartSpeed||player.paused){if(!player.paused)player.playbackRate=Number(state.settings.videoRate)||1;return;}
   try{if(!videoAudioContext){videoAudioContext=new AudioContext();const source=videoAudioContext.createMediaElementSource(player);analyser=videoAudioContext.createAnalyser();analyser.fftSize=512;source.connect(analyser);analyser.connect(videoAudioContext.destination);data=new Uint8Array(analyser.fftSize);}if(videoAudioContext.state==='suspended')videoAudioContext.resume();if(!analyser)return;analyser.getByteTimeDomainData(data);const rms=Math.sqrt(data.reduce((sum,n)=>sum+(n-128)**2,0)/data.length)/128;if(rms<.012){if(!silenceAt)silenceAt=Date.now();}else silenceAt=0;const base=Number(state.settings.videoRate)||1;const fast=silenceAt&&Date.now()-silenceAt>650;player.playbackRate=fast?Math.min(3,base*1.7):base;const label=$('#rate-status');if(label)label.textContent=fast?'静音片段 '+player.playbackRate.toFixed(1)+'×':'';}catch(e){setting({smartSpeed:false}).catch(fail);toast('当前视频不支持静音分析，仍可手动调整倍速。');}
 }
 speedTimer=setInterval(analyse,120);
 const tracks=await native('subtitles',{id:item.id});if(state.viewer?.id!==item.id)return;
 subtitleTracks=tracks||[];renderSubtitleOptions();if(subtitleTracks.length){$('#subtitle-select').value='0';setSubtitle(0);$('#video-hint').textContent=`自动找到 ${subtitleTracks.length} 份字幕 · ASS/SSA 使用文字兼容模式`;}else $('#video-hint').textContent='自动寻找同名字幕 · 内嵌 / 位图字幕可使用系统播放器';
 if(typeof scheduleAutoTranslation==='function'&&subtitleTracks.length)scheduleAutoTranslation();
}
function renderSubtitleOptions(){const select=$('#subtitle-select');if(!select)return;select.innerHTML='<option value="-1">关闭</option>'+subtitleTracks.map((t,n)=>`<option value="${n}" ${t.translated?'':'data-user-content'}>${esc(t.name)}</option>`).join('');}
function setSubtitle(index){
 const player=$('#video');if(!player)return;player.querySelectorAll('track').forEach(t=>t.remove());if(subtitleUrl){URL.revokeObjectURL(subtitleUrl);subtitleUrl=null;}
 const track=subtitleTracks[index];if(!track)return;
 const stamp=s=>{let ms=Math.round(s*1000);return String(Math.floor(ms/3600000)).padStart(2,'0')+':'+String(Math.floor(ms/60000)%60).padStart(2,'0')+':'+String(Math.floor(ms/1000)%60).padStart(2,'0')+'.'+String(ms%1000).padStart(3,'0');};
 const text='WEBVTT\n\n'+track.cues.map((c,n)=>`${n+1}\n${stamp(c.start)} --> ${stamp(c.end)}\n${c.text.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;')}\n`).join('\n');
 subtitleUrl=URL.createObjectURL(new Blob([text],{type:'text/vtt'}));const el=document.createElement('track');el.kind='subtitles';el.label=track.translated?I18n.text(track.name):track.name;el.srclang=track.language==='English'?'en':track.language==='简体中文'||track.language==='繁體中文'?'zh':(/[._-](en|zh|ja|ko|fr|de|es)(?=[._-])/i.exec(track.name)?.[1]||'und');el.src=subtitleUrl;el.default=true;el.addEventListener('load',()=>el.track.mode='showing');player.append(el);el.track.mode='showing';
}
function markedText(item,text,chapter){
 const marks=(item.annotations||[]).filter(a=>a.chapter===chapter&&!a.page&&a.style!=='insert'&&a.start>=0&&a.end<=text.length);
 const edges=[...new Set([0,text.length,...marks.flatMap(a=>[a.start,a.end])])].sort((a,b)=>a-b);
 let html='';for(let n=0;n<edges.length-1;n++){const start=edges[n],end=edges[n+1];const active=marks.filter(a=>a.start<end&&a.end>start);const piece=esc(text.slice(start,end));html+=active.length?`<span class="text-mark ${active.map(a=>'mark-'+a.style).join(' ')}" data-mark="${active[0].id}" title="${esc(active[0].note||'点击记录想法')}">${piece}</span>`:piece;}return html;
}
function initReadingFeatures(){
 if(typeof initDocumentTranslation==='function')initDocumentTranslation();
 const tools=$('.reader-tools');if(tools){tools.insertAdjacentHTML('beforeend',`<span>｜</span><button class="icon-button text-tool" data-show-marks>${svg('edit')}标记与想法</button>${state.viewer.kind==='document'?'<button class="icon-button text-tool" data-insert-text>插入文字</button>':''}<button class="icon-button text-tool" data-export-reading title="导出带标记的 HTML 副本">${svg('download')}</button>`);}
 const root=$('#reader-text');if(root){root.addEventListener('mouseup',()=>setTimeout(captureSelection,0));root.addEventListener('keyup',()=>setTimeout(captureSelection,0));}
 renderAnnotationIndex();
}
function hideSelectionTools(){viewer.querySelector('.selection-tools')?.remove();}
function captureSelection(){
 const selection=getSelection();if(!selection||selection.isCollapsed||!selection.rangeCount){hideSelectionTools();return;}
 const range=selection.getRangeAt(0);let root=range.commonAncestorContainer.nodeType===1?range.commonAncestorContainer:range.commonAncestorContainer.parentElement;
 const textRoot=root.closest('#reader-text');const layer=root.closest('.textLayer');
 if(!textRoot&&!layer)return;
 const source=textRoot||layer;if(!source.contains(range.startContainer)||!source.contains(range.endContainer))return;
 const before=range.cloneRange();before.selectNodeContents(source);before.setEnd(range.startContainer,range.startOffset);
 const rect=range.getBoundingClientRect();if(!rect.width&&!rect.height)return;
 let rects=[],page=0;
 if(layer){const wrapper=layer.closest('.pdf-page');const box=wrapper.getBoundingClientRect();page=Number(wrapper.dataset.page);rects=[...range.getClientRects()].filter(r=>r.width>1&&r.height>1).map(r=>({x:(r.left-box.left)/box.width,y:(r.top-box.top)/box.height,w:r.width/box.width,h:r.height/box.height}));}
 selectionAnchor={start:before.toString().length,end:before.toString().length+range.toString().length,text:range.toString(),chapter:state.chapter||0,page,rects,left:rect.left,top:rect.bottom};
 hideSelectionTools();const bar=document.createElement('div');bar.className='selection-tools';bar.style.left=Math.max(16,Math.min(innerWidth-330,rect.left))+'px';bar.style.top=Math.min(innerHeight-55,rect.bottom+9)+'px';bar.innerHTML=`<button data-add-mark="underline"><u>下划线</u></button><button data-add-mark="highlight"><mark>荧光</mark></button><button data-add-mark="note">${svg('edit')}想法</button>${state.viewer.kind==='document'&&!page?'<button data-insert-text>插入文字</button>':''}`;viewer.append(bar);
}
async function addMark(style){
 const anchor=selectionAnchor;if(!anchor||!state.viewer)return;
 const mark={id:uid(),style:style==='note'?'underline':style,text:anchor.text,note:'',chapter:anchor.chapter,start:anchor.start,end:anchor.end,page:anchor.page,rects:anchor.rects,x:0,y:0};
 state.viewer.annotations=state.viewer.annotations||[];state.viewer.annotations.push(mark);
 await patch(state.viewer,{annotations:state.viewer.annotations});hideSelectionTools();getSelection()?.removeAllRanges();
 if(pdfState)drawPdfMarks(mark.page);else{const pos=readerPosition();renderReader(pos);}
 renderAnnotationIndex();if(style==='note')showMarkPopover(mark.id,{left:anchor.left,top:anchor.top});
}
function showMarkPopover(id,point){
 const mark=state.viewer?.annotations?.find(a=>a.id===id);if(!mark)return;
 viewer.querySelector('.mark-popover')?.remove();hideSelectionTools();
 const pop=document.createElement('aside');pop.className='mark-popover';const found=viewer.querySelector(`[data-mark="${id}"]`)?.getBoundingClientRect();const location=point||{left:found?.right||innerWidth-350,top:found?.top||150};
 pop.style.left=Math.max(20,Math.min(innerWidth-330,location.left+15))+'px';pop.style.top=Math.max(85,Math.min(innerHeight-340,location.top+12))+'px';
 pop.innerHTML=`<div class="popover-heading"><b>${mark.style==='insert'?'插入的文字':'此刻的想法'}</b><button class="icon-button" data-close-popover aria-label="关闭">${svg('close')}</button></div><blockquote>${esc(mark.text.slice(0,220))}</blockquote><textarea id="mark-note" placeholder="写下你的想法…" aria-label="选中文字的想法">${esc(mark.note||'')}</textarea><div class="popover-actions"><button data-delete-mark="${id}" class="text-button">移除标记</button><button class="button primary" data-save-mark="${id}">保存想法</button></div>`;viewer.append(pop);$('#mark-note').focus();
}
function renderAnnotationIndex(){
 $('#annotation-index')?.remove();if(!state.viewer)return;
 const marks=state.viewer.annotations||[];const div=document.createElement('div');div.id='annotation-index';div.innerHTML=`<div class="annotation-index-title">划线与想法 <span>${marks.length}</span></div>${marks.length?marks.map(a=>`<button data-jump-mark="${a.id}"><b>${esc((a.page?'第 '+a.page+' 页 · ':'')+a.text.slice(0,48))}</b><small>${esc(a.note||'点击补充想法')}</small></button>`).join(''):'<p>选中正文中的文字，添加下划线或荧光标记。</p>'}`;$('#notes-panel').insertBefore(div,$('#notes'));
}
function insertTextDialog(){
 if(pdfState){pdfState.inserting=!pdfState.inserting;$('.pdf-scroll').classList.toggle('inserting',pdfState.inserting);toast(pdfState.inserting?'请点击 PDF 页面中要插入文字的位置。':'已退出插入模式。');return;}
 if(!state.book||state.viewer?.kind!=='document')return;
 const at=selectionAnchor&&!selectionAnchor.page?selectionAnchor.end:state.book.chapters[state.chapter].text.length;
 hideSelectionTools();showSmall(`<h2>插入一段文字</h2><p class="dialog-description">插入到当前选择的后面；未选中文字时插入到本节末尾。修改保存为资料库中的阅读副本，原文件保留。</p><textarea id="insert-value" class="dialog-textarea" placeholder="写下需要补充的文字…"></textarea><button class="button primary dialog-submit" data-confirm-insert="${at}">插入并保存</button>`);$('#insert-value').focus();
}
async function insertDocumentText(at,text){
 if(!text)return;const item=state.viewer,chapter=state.chapter,current=state.book.chapters[chapter].text;at=Math.max(0,Math.min(at,current.length));
 const edited=current.slice(0,at)+text+current.slice(at);const marks=(item.annotations||[]).map(a=>{if(a.chapter!==chapter||a.page)return a;return{...a,start:a.start>=at?a.start+text.length:a.start,end:a.end>=at?a.end+text.length:a.end};});
 const chapterTexts={...(item.chapterTexts||{}),[String(chapter)]:edited};state.book.chapters[chapter].text=edited;
 await patch(item,{chapterTexts,annotations:marks});const scroll=readerPosition();renderReader(scroll);toast('文字已插入并保存，可导出带标记的阅读副本。');
}
async function exportReading(){
 if(!state.viewer)return;const item=state.viewer;
 let body='';if(state.book){body=state.book.chapters.map((chapter,n)=>`<section><h2>${esc(chapter.title)}</h2><div class="content">${markedText(item,chapter.text,n)}</div></section>`).join('');}
 const marks=item.annotations||[];body+=`<h2>${I18n.text('标记与想法')}</h2>${marks.map(a=>`<aside><small>${I18n.text(a.page?'第 '+a.page+' 页':'第 '+(a.chapter+1)+' 节')} · ${I18n.text(a.style==='insert'?'插入文字':a.style==='underline'?'下划线':'荧光标记')}</small><blockquote>${esc(a.text)}</blockquote><p>${esc(a.note||'')}</p></aside>`).join('')}<h2>${I18n.text('随手记')}</h2><p class="content">${esc(item.notes||'')}</p>`;
 const html=`<!doctype html><meta charset="utf-8"><title>${esc(I18n.text(item.name+' · kok 阅读副本'))}</title><style>body{font:18px/2 'Microsoft YaHei',sans-serif;max-width:850px;margin:60px auto;padding:0 30px;color:#344152}h1,h2{font-weight:500}.content{white-space:pre-wrap}.mark-highlight{background:#ffea85}.mark-underline{text-decoration:underline;text-decoration-color:#7189c5;text-underline-offset:4px}aside{background:#f2f5fa;padding:20px;border-radius:14px;margin:20px 0}small{color:#8591a1}</style><h1>${esc(item.name)}</h1><p>${I18n.text('由 kok 导出 · 原文件保持不变')}${pdfState?' '+I18n.text('· PDF 批注摘要'):''}</p>${body}`;
 if(await native('exportReading',{name:item.name,html}))toast('阅读副本与批注已导出。');
}
async function renderPdf(item){
 const token=item.id;$('#viewer-content').innerHTML=pdfWorkspaceMarkup();
 const pdfjs=await import('./vendor/pdfjs/pdf.mjs');pdfjs.GlobalWorkerOptions.workerSrc=new URL('./vendor/pdfjs/pdf.worker.mjs',location.href).href;
 const task=pdfjs.getDocument({url:media(item),cMapUrl:new URL('./vendor/pdfjs/cmaps/',location.href).href,cMapPacked:true,standardFontDataUrl:new URL('./vendor/pdfjs/standard_fonts/',location.href).href,wasmUrl:new URL('./vendor/pdfjs/wasm/',location.href).href,isEvalSupported:false});
 const doc=await task.promise;if(state.viewer?.id!==token){await task.destroy();return;}
 const first=await doc.getPage(1),base=first.getViewport({scale:1});if(state.viewer?.id!==token){await task.destroy();return;}
 const scroll=$('.pdf-scroll');scroll.innerHTML='';
 const session=pdfState={doc,itemId:token,rendered:new Set(),inserting:false,observer:null,jobs:new Set(),epoch:0,width:0,aspects:new Map(),resizeTimer:null};
 for(let n=1;n<=doc.numPages;n++){const wrapper=document.createElement('div');wrapper.className='pdf-page';wrapper.dataset.page=n;scroll.append(wrapper);}
 async function draw(wrapper){
  const n=Number(wrapper.dataset.page),epoch=session.epoch;if(session.rendered.has(n)||pdfState!==session)return;session.rendered.add(n);const job={};session.jobs.add(job);
  try{
   const source=await doc.getPage(n);if(pdfState!==session||epoch!==session.epoch)return;
   const viewport=source.getViewport({scale:session.width/source.getViewport({scale:1}).width});session.aspects.set(n,viewport.height/viewport.width);wrapper.style.height=viewport.height+'px';wrapper.style.setProperty('--total-scale-factor',viewport.scale);wrapper.style.setProperty('--scale-factor',viewport.scale);
   const canvas=document.createElement('canvas'),density=Math.min(devicePixelRatio,2);canvas.width=Math.floor(viewport.width*density);canvas.height=Math.floor(viewport.height*density);canvas.style.width=viewport.width+'px';canvas.style.height=viewport.height+'px';wrapper.append(canvas);
   job.render=source.render({canvasContext:canvas.getContext('2d'),viewport,transform:density===1?null:[density,0,0,density,0,0]});await job.render.promise;if(pdfState!==session||epoch!==session.epoch)return;
   const content=await source.getTextContent();if(pdfState!==session||epoch!==session.epoch)return;const layer=document.createElement('div');layer.className='textLayer';wrapper.append(layer);
   job.text=new pdfjs.TextLayer({textContentSource:content,container:layer,viewport});await job.text.render();if(pdfState!==session||epoch!==session.epoch)return;
   const marks=document.createElement('div');marks.className='pdf-marks';wrapper.append(marks);layer.addEventListener('mouseup',()=>setTimeout(captureSelection,0));drawPdfMarks(n);
  }catch(error){if(pdfState===session&&epoch===session.epoch&&error.name!=='RenderingCancelledException')toast('第 '+n+' 页无法渲染：'+error.message,true);}
  finally{session.jobs.delete(job);}
 }
 const observer=new IntersectionObserver(entries=>{for(const entry of entries)if(entry.isIntersecting)draw(entry.target);},{root:scroll,rootMargin:'400px'});session.observer=observer;
 session.relayout=(force=true)=>{
   if(pdfState!==session||!scroll.isConnected)return;
   const css=getComputedStyle(scroll),available=Math.max(120,scroll.clientWidth-parseFloat(css.paddingLeft)-parseFloat(css.paddingRight));const width=Math.min(950,available)*(state.pdfZoom||1);
   if(!force&&Math.abs(width-session.width)<1)return;
   const pages=[...scroll.children],firstPage=pages[0];let anchor=firstPage,offset=0;
   if(session.width){for(const page of pages){if(page.offsetTop-firstPage.offsetTop<=scroll.scrollTop)anchor=page;else break;}offset=(scroll.scrollTop-(anchor.offsetTop-firstPage.offsetTop))/Math.max(1,anchor.offsetHeight);}
   session.epoch++;observer.disconnect();for(const job of session.jobs){job.render?.cancel();job.text?.cancel();}session.rendered.clear();session.width=width;
   for(const wrapper of pages){const n=Number(wrapper.dataset.page);wrapper.style.width=width+'px';wrapper.style.height=width*(session.aspects.get(n)||base.height/base.width)+'px';wrapper.innerHTML=`<span class="pdf-page-number">${n}</span>`;observer.observe(wrapper);}
   scroll.scrollTop=session.epoch===1?(item.position||0)*Math.max(0,scroll.scrollHeight-scroll.clientHeight):anchor.offsetTop-firstPage.offsetTop+offset*anchor.offsetHeight;
 };
 const resize=new ResizeObserver(()=>{clearTimeout(session.resizeTimer);session.resizeTimer=setTimeout(()=>session.relayout(false),80);});resize.observe(scroll);
 session.dispose=()=>{clearTimeout(session.resizeTimer);resize.disconnect();observer.disconnect();session.epoch++;for(const job of session.jobs){job.render?.cancel();job.text?.cancel();}};
 session.relayout();if(typeof initDocumentTranslation==='function')initDocumentTranslation();
 scroll.addEventListener('scroll',()=>{hideSelectionTools();clearTimeout(readerTimer);readerTimer=setTimeout(()=>savePdf().catch(fail),700);if(typeof scheduleAutoTranslation==='function')scheduleAutoTranslation();});
 scroll.addEventListener('click',e=>{if(!pdfState?.inserting||e.target.closest('[data-mark]'))return;const wrapper=e.target.closest('.pdf-page');if(!wrapper)return;const box=wrapper.getBoundingClientRect();pdfState.insertPoint={page:Number(wrapper.dataset.page),x:(e.clientX-box.left)/box.width,y:(e.clientY-box.top)/box.height};pdfState.inserting=false;scroll.classList.remove('inserting');showSmall(`<h2>在 PDF 上插入文字</h2><p class="dialog-description">文字保存为本地阅读批注，不改变原 PDF。第 ${wrapper.dataset.page} 页。</p><textarea id="pdf-insert-value" class="dialog-textarea" placeholder="输入补充的文字…"></textarea><button class="button primary dialog-submit" data-confirm-pdf-insert>插入并保存</button>`);$('#pdf-insert-value').focus();});
 initPdfWorkspace(doc,scroll);renderAnnotationIndex();
}
function drawPdfMarks(page){
 if(!pdfState||!state.viewer)return;const wrapper=$(`.pdf-page[data-page="${page}"]`),layer=wrapper?.querySelector('.pdf-marks');if(!layer)return;
 layer.innerHTML=(state.viewer.annotations||[]).filter(a=>a.page===page).map(a=>a.style==='insert'?`<button class="pdf-insert" data-mark="${a.id}" style="left:${a.x*100}%;top:${a.y*100}%">${esc(a.text)}</button>`:(a.rects||[]).map(r=>`<button class="pdf-mark mark-${a.style}" data-mark="${a.id}" aria-label="${esc(a.note||a.text)}" style="left:${r.x*100}%;top:${r.y*100}%;width:${r.w*100}%;height:${r.h*100}%"></button>`).join('')).join('');
}
async function savePdf(){const scroll=$('.pdf-scroll');if(pdfState&&state.viewer&&scroll)await patch(state.viewer,{position:scroll.scrollTop/Math.max(1,scroll.scrollHeight-scroll.clientHeight),progress:(scroll.scrollTop+scroll.clientHeight)/Math.max(1,scroll.scrollHeight)});}
function cleanupFeatures(){
 if(typeof resetTranslationView==='function')resetTranslationView();
 hideSelectionTools();viewer.querySelector('.mark-popover')?.remove();selectionAnchor=null;photoEditor=null;
 if(pdfState){const session=pdfState;pdfState=null;session.dispose?.();session.doc.loadingTask.destroy().catch(()=>{});}
 if(speedTimer)clearInterval(speedTimer);speedTimer=null;if(videoAudioContext){videoAudioContext.close().catch(()=>{});videoAudioContext=null;}
 if(subtitleUrl)URL.revokeObjectURL(subtitleUrl);subtitleUrl=null;
 viewer.classList.remove('notes-open');
}
$('#add-collection').addEventListener('click',()=>newCollection());
document.addEventListener('mousedown',e=>{if(e.target.closest('.selection-tools'))e.preventDefault();});
document.addEventListener('click',async e=>{
 const el=e.target.closest('button,[data-mark]');if(!el)return;
 try{
  if(el.dataset.collection){state.collection=el.dataset.collection;navigate('collection');return;}
  if(el.dataset.classify){newCollection(el.dataset.classify);return;}
  if(el.hasAttribute('data-save-collection')){const name=$('#collection-name').value.trim()||$('#collection-existing')?.value||'';const id=el.dataset.saveCollection;if(id==='new'){await native('addCollection',{name});}else await patch(byId(id),{collection:name});await closeSmall();await refresh();toast('分类已保存。');return;}
  if(el.dataset.musicMode){state.musicMode=el.dataset.musicMode;render();return;}
  if(el.hasAttribute('data-album')){state.musicAlbum=el.dataset.album;render();return;}
  if(el.hasAttribute('data-album-back')){state.musicAlbum=null;render();return;}
  if(el.hasAttribute('data-play-album')){state.queue=state.items.filter(i=>i.kind==='music'&&albumName(i)===el.dataset.playAlbum);if(state.queue.length)await openItem(state.queue[0].id);return;}
  if(el.dataset.albumEdit){const item=byId(el.dataset.albumEdit);showSmall(`<h2>这一首的归属</h2><p class="dialog-description">专辑名相同的歌曲会归在同一张专辑里。</p><label class="form-label">专辑<input id="album-name" maxlength="200" value="${esc(item.album||'')}"></label><label class="form-label">歌手 / 演奏者<input id="artist-name" maxlength="200" value="${esc(item.artist||'本地音乐')}"></label><button class="button primary dialog-submit" data-save-album="${item.id}">保存</button>`);return;}
  if(el.dataset.saveAlbum){await patch(byId(el.dataset.saveAlbum),{album:$('#album-name').value.trim(),artist:$('#artist-name').value.trim()});await closeSmall();state.musicAlbum=null;render();return;}
  if(el.hasAttribute('data-import-font')){const font=await native('importFont');if(font){await refresh();await setting({fontFamily:font.id});await applyFont();await closeSmall();toast('字体已导入并应用：'+font.name);}return;}
  if(el.id==='smart-speed'){await setting({smartSpeed:!state.settings.smartSpeed});el.classList.toggle('enabled',!!state.settings.smartSpeed);if(!state.settings.smartSpeed&&video){video.playbackRate=Number(state.settings.videoRate)||1;$('#rate-status').textContent='';}return;}
  if(el.id==='load-subtitle'){const track=await native('chooseSubtitle');if(track){subtitleTracks.push(track);renderSubtitleOptions();$('#subtitle-select').value=String(subtitleTracks.length-1);setSubtitle(subtitleTracks.length-1);if(typeof scheduleAutoTranslation==='function')scheduleAutoTranslation();}return;}
  if(el.dataset.addMark){await addMark(el.dataset.addMark);return;}
  if(el.dataset.mark){showMarkPopover(el.dataset.mark);return;}
  if(el.hasAttribute('data-close-popover')){await dismissElement(viewer.querySelector('.mark-popover'));return;}
  if(el.dataset.saveMark){const mark=state.viewer.annotations.find(a=>a.id===el.dataset.saveMark);mark.note=$('#mark-note').value;await patch(state.viewer,{annotations:state.viewer.annotations});await dismissElement(viewer.querySelector('.mark-popover'));renderAnnotationIndex();toast('想法已保存。');return;}
  if(el.dataset.deleteMark){state.viewer.annotations=state.viewer.annotations.filter(a=>a.id!==el.dataset.deleteMark);await patch(state.viewer,{annotations:state.viewer.annotations});await dismissElement(viewer.querySelector('.mark-popover'));if(pdfState)document.querySelectorAll('.pdf-page').forEach(p=>drawPdfMarks(Number(p.dataset.page)));else{const p=readerPosition();renderReader(p);}renderAnnotationIndex();return;}
  if(el.hasAttribute('data-show-marks')){$('#notes-panel').classList.remove('hidden');viewer.classList.add('notes-open');renderAnnotationIndex();return;}
  if(el.dataset.jumpMark){const mark=state.viewer.annotations.find(a=>a.id===el.dataset.jumpMark);if(!mark)return;if(pdfState){$(`.pdf-page[data-page="${mark.page}"]`)?.scrollIntoView({block:'center'});}else if(mark.chapter!==state.chapter)await changeChapter(mark.chapter);const span=viewer.querySelector(`[data-mark="${mark.id}"]`);span?.scrollIntoView({block:'center'});showMarkPopover(mark.id);return;}
  if(el.hasAttribute('data-insert-text')){insertTextDialog();return;}
  if(el.hasAttribute('data-confirm-insert')){const text=$('#insert-value').value;const at=Number(el.dataset.confirmInsert);await closeSmall();await insertDocumentText(at,text);return;}
  if(el.hasAttribute('data-confirm-pdf-insert')){const text=$('#pdf-insert-value').value;if(!text.trim())return;const point=pdfState.insertPoint;const mark={id:uid(),style:'insert',text,note:'',page:point.page,chapter:0,start:0,end:0,rects:[],x:point.x,y:point.y};state.viewer.annotations=state.viewer.annotations||[];state.viewer.annotations.push(mark);await patch(state.viewer,{annotations:state.viewer.annotations});await closeSmall();drawPdfMarks(point.page);renderAnnotationIndex();toast('文字已保存为 PDF 阅读批注。');return;}
  if(el.hasAttribute('data-export-reading')){await exportReading();return;}
  if(el.hasAttribute('data-photo-edit')){await startPhotoEditor();return;}
 }catch(error){fail(error);}
});
document.addEventListener('change',async e=>{
 try{if(e.target.id==='font-select'){await setting({fontFamily:e.target.value});await applyFont();}if(e.target.id==='video-rate'){await setting({videoRate:Number(e.target.value)});video.playbackRate=Number(e.target.value);}if(e.target.id==='subtitle-select'){setSubtitle(Number(e.target.value));if(typeof scheduleAutoTranslation==='function'&&!subtitleTracks[Number(e.target.value)]?.translated)scheduleAutoTranslation();}if(e.target.id==='pdf-zoom'&&pdfState){await savePdf();state.pdfZoom=Number(e.target.value);pdfState.relayout();}}catch(error){fail(error);}
});
// The native window asks for this synchronous snapshot before closing.
window.kokSession=()=>{
 const edits=[];
 if(state.viewer){const item=state.viewer;const patch={notes:$('#notes').value};const pop=viewer.querySelector('[data-save-mark]');if(pop&&$('#mark-note')){const mark=item.annotations?.find(a=>a.id===pop.dataset.saveMark);if(mark)mark.note=$('#mark-note').value;patch.annotations=item.annotations||[];}
 if(video&&Number.isFinite(video.duration)&&video.duration>0){patch.position=video.currentTime;patch.progress=video.currentTime/video.duration;}
 if(state.book&&$('#reader-scroll')){patch.chapter=state.chapter;patch.position=readerPosition();const reader=$('#reader-scroll');patch.progress=(state.chapter+(reader.scrollHeight<=reader.clientHeight?1:patch.position))/state.book.chapters.length;}
 const scroll=$('.pdf-scroll');if(pdfState&&scroll){patch.position=scroll.scrollTop/Math.max(1,scroll.scrollHeight-scroll.clientHeight);patch.progress=(scroll.scrollTop+scroll.clientHeight)/Math.max(1,scroll.scrollHeight);}
 edits.push({id:item.id,patch});}
 if(state.track&&Number.isFinite(audio.duration)&&audio.duration>0)edits.push({id:state.track.id,patch:{position:audio.currentTime,progress:audio.currentTime/audio.duration}});
 return edits;
};

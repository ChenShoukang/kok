'use strict';
paths.bookmark='M6 3h12v18l-6-4-6 4V3Z';
paths.maximize='M8 3H3v5 M16 3h5v5 M21 16v5h-5 M3 16v5h5';
paths.list='M8 6h13 M8 12h13 M8 18h13 M3 6h.01 M3 12h.01 M3 18h.01';
let readingSearchEpoch=0,readingToolState=null;
const readingDefaults={readerLineHeight:2.05,readerLetterSpacing:.3,readerParagraphSpace:.3,readerWidth:760,readerLayout:'scroll'};
function ensureReadingToolState(){if(readingToolState?.id!==state.viewer?.id)readingToolState={id:state.viewer?.id,tab:'toc',query:'',results:[],busy:false,origin:null};return readingToolState;}
function readerPaged(){return !!$('#reader-scroll')&&state.settings.readerLayout==='paged';}
readerPosition=function(){const el=$('#reader-scroll');if(!el)return 0;return Math.max(0,Math.min(1,readerPaged()?el.scrollLeft/Math.max(1,el.scrollWidth-el.clientWidth):el.scrollTop/Math.max(1,el.scrollHeight-el.clientHeight)));};
function readerFraction(){const el=$('#reader-scroll');if(!el)return 0;return (readerPaged()?el.scrollWidth<=el.clientWidth+2:el.scrollHeight<=el.clientHeight+2)?1:readerPosition();}
saveReader=async function(){if(!state.book||!state.viewer||!$('#reader-scroll'))return;await patch(state.viewer,{chapter:state.chapter,position:readerPosition(),progress:(state.chapter+readerFraction())/state.book.chapters.length});};
updateReadingPosition=function(){
 if(!state.book||!$('#reader-scroll'))return;const progress=(state.chapter+readerFraction())/state.book.chapters.length;
 $('#reading-percent').textContent=Math.round(progress*100)+'%';$('.reading-meter i').style.width=(progress*100)+'%';
 const page=$('#reading-page-indicator');if(page){const el=$('#reader-scroll');page.textContent=readerPaged()?`${Math.round(el.scrollLeft/Math.max(1,el.clientWidth))+1} / ${Math.max(1,Math.ceil((el.scrollWidth-2)/Math.max(1,el.clientWidth)))}`:'';}
 const slider=$('#reading-location');if(slider&&document.activeElement!==slider)slider.value=Math.round(progress*1000);
 if(readerPaged()){const el=$('#reader-scroll');$('[data-reader="prev"]').disabled=state.chapter===0&&el.scrollLeft<2;$('[data-reader="next"]').disabled=state.chapter===state.book.chapters.length-1&&el.scrollLeft>=el.scrollWidth-el.clientWidth-2;}
};
function applyReadingType(){
 const root=$('.reading-workspace');if(!root)return;
 for(const [key,css]of [['readerLineHeight','--reading-leading'],['readerLetterSpacing','--reading-tracking'],['readerParagraphSpace','--reading-paragraph'],['readerWidth','--reading-width']])root.style.setProperty(css,String(state.settings[key]??readingDefaults[key])+(['readerLetterSpacing','readerWidth'].includes(key)?'px':key==='readerParagraphSpace'?'em':''));
 root.classList.toggle('reader-paged',state.settings.readerLayout==='paged');
}
function paragraphSpacing(root){
 const walker=document.createTreeWalker(root,NodeFilter.SHOW_TEXT),nodes=[];let node;while(node=walker.nextNode())if(/\n{2,}/.test(node.nodeValue))nodes.push(node);
 for(const node of nodes){const fragment=document.createDocumentFragment();for(const part of node.nodeValue.split(/(\n{2,})/)){if(!part)continue;if(/^\n{2,}$/.test(part)){const span=document.createElement('span');span.className='reader-paragraph-gap';span.textContent=part;fragment.append(span);}else fragment.append(document.createTextNode(part));}node.replaceWith(fragment);}
}
function restoreReaderPosition(fraction){
 const el=$('#reader-scroll');if(!el)return;
 if(readerPaged()){
   const column=Math.max(200,Math.min(el.clientWidth-64,Number(state.settings.readerWidth)||760));el.style.setProperty('--reading-column',column+'px');el.style.setProperty('--reading-gutter',Math.max(32,(el.clientWidth-column)/2)+'px');
   let end=el.querySelector(':scope>.reader-page-end');if(!end){end=document.createElement('i');end.className='reader-page-end';end.setAttribute('aria-hidden','true');el.append(end);}end.hidden=true;
   const stride=Math.max(1,el.clientWidth),pages=Math.max(1,Math.ceil((el.scrollWidth-2)/stride));end.style.left=(pages*stride-1)+'px';end.hidden=false;
   const page=Math.max(0,Math.min(pages-1,Math.round((pages-1)*fraction)));el.scrollTo({left:page*stride,top:0,behavior:'instant'});
 }else el.scrollTo({top:Math.max(0,el.scrollHeight-el.clientHeight)*fraction,left:0,behavior:'instant'});updateReadingPosition();
}
function advanceReadingPage(direction){
 const el=$('#reader-scroll');if(!el)return;
 if(!readerPaged()){el.scrollBy({top:direction*el.clientHeight*.88,behavior:reducedMotion()?'instant':'smooth'});return;}
 const at=Math.round(el.scrollLeft/Math.max(1,el.clientWidth)),pages=Math.max(1,Math.ceil((el.scrollWidth-2)/Math.max(1,el.clientWidth)));
 if(direction<0&&at===0&&state.chapter>0){openReadingLocation({chapter:state.chapter-1,position:1}).catch(fail);return;}
 if(direction>0&&at>=pages-1&&state.chapter<state.book.chapters.length-1){changeChapter(state.chapter+1).catch(fail);return;}
 el.scrollTo({left:Math.max(0,Math.min(el.scrollWidth-el.clientWidth,(at+direction)*el.clientWidth)),behavior:reducedMotion()?'instant':'smooth'});
}
function readingToolbarMarkup(pdf=false){return `${pdf?workspaceButton('panel','目录与阅读工具','data-reading-sidebar'):''}${workspaceButton('bookmark','添加书签','data-add-bookmark')}${pdf?'':workspaceButton('settings','阅读排版','data-reading-settings')}${workspaceButton('maximize','专注阅读','data-reading-focus')}`;}
function bookmarkMarkup(){const list=state.viewer?.bookmarks||[];return `<div class="reading-pane-heading"><b>我的书签</b><button class="icon-button" data-add-bookmark title="添加书签" aria-label="添加书签">${svg('plus')}</button></div>${list.length?list.map(b=>`<div class="reading-bookmark"><button data-open-bookmark="${esc(b.id)}"><b data-user-content>${esc(b.name)}</b><small data-user-content>${b.page?`${b.page} / ${pdfState?.doc.numPages||'—'}`:`${b.chapter+1} · ${Math.round(b.position*100)}%`}</small></button><button class="icon-button" data-remove-bookmark="${esc(b.id)}" title="删除书签" aria-label="删除书签">${svg('close')}</button></div>`).join(''):'<p class="reading-pane-empty">把想回到的位置，留在这里。</p>'}<button class="button secondary reading-export" data-export-reading-notes>${svg('download')}导出笔记与书签</button>`;}
function searchResultMarkup(){const tool=ensureReadingToolState();return `<div class="reading-search-summary"><span>${tool.busy?'正在搜索…':tool.results.length?I18n.text('找到匹配')+' · '+tool.results.length:tool.query?'没有找到匹配':'搜索整本文档或书籍'}</span>${tool.busy?'<button class="text-button" data-stop-reading-search>停止</button>':''}</div>${tool.results.map((r,n)=>`<button class="reading-search-result" data-reading-result="${n}"><small data-user-content>${esc(r.title)}</small><span data-user-content>${esc(r.before)}<mark>${esc(r.match)}</mark>${esc(r.after)}</span></button>`).join('')}${tool.results.length>=200?'<p class="reading-pane-empty">最多显示 200 个匹配</p>':''}${tool.origin?'<button class="text-button reading-return" data-reading-origin>返回搜索前的位置</button>':''}`;}
function setupReadingSidebar(pdf=false){
 const tool=ensureReadingToolState(),root=pdf?$('.kok-pdf-sidebar'):$('.reader-toc');if(!root)return;
 let toc=pdf?'<div class="pdf-outline"><p class="reading-pane-empty">正在读取目录…</p></div>':root.querySelector('.toc-chapters').outerHTML;
 root.innerHTML=`<div class="reading-side-tabs" role="tablist" aria-label="阅读工具"><button role="tab" data-reading-tab="toc">目录</button><button role="tab" data-reading-tab="bookmarks">书签</button><button role="tab" data-reading-tab="search">全文搜索</button></div><section class="reading-side-pane" data-reading-pane="toc"><input type="search" id="reading-toc-filter" placeholder="查找章节" aria-label="查找章节">${toc}</section><section class="reading-side-pane" data-reading-pane="bookmarks">${bookmarkMarkup()}</section><section class="reading-side-pane" data-reading-pane="search"><label class="reading-full-search">${svg('search')}<input type="search" id="reading-full-query" value="${esc(tool.query)}" placeholder="搜索全文" aria-label="搜索全文" autocomplete="off"></label><div id="reading-full-results">${searchResultMarkup()}</div></section>`;
 setReadingTab(tool.tab,false);if(pdf)loadPdfOutline().catch(()=>{const el=$('.pdf-outline');if(el){el.textContent=I18n.text('这份 PDF 没有目录');}});I18n.localize(root);
}
function setReadingTab(tab,open=true){
 const root=$('.reader-toc')||$('.kok-pdf-sidebar');if(!root)return;ensureReadingToolState().tab=tab;
 root.querySelectorAll('[data-reading-tab]').forEach(b=>{const active=b.dataset.readingTab===tab;b.classList.toggle('active',active);b.setAttribute('aria-selected',String(active));});
 root.querySelectorAll('[data-reading-pane]').forEach(p=>p.hidden=p.dataset.readingPane!==tab);
 if(open){$('.reading-workspace')?.classList.remove('toc-collapsed');$('.pdf-workspace')?.classList.add('has-reading-sidebar');updateTocAccessibility();}
}
async function loadPdfOutline(){
 const session=pdfState;if(!session)return;const outline=await session.doc.getOutline();if(session!==pdfState)return;const target=$('.pdf-outline');if(!target)return;let count=0;
 function markup(items,depth=0){return (items||[]).slice(0,1000).map(item=>{const index=count++;return `<div class="pdf-outline-row" style="--outline-depth:${Math.min(depth,5)}"><button data-pdf-outline="${index}" data-user-content>${esc(item.title||'—')}</button>${markup(item.items,depth+1)}</div>`;}).join('');}
 target.innerHTML=outline?.length?markup(outline):`<p class="reading-pane-empty">这份 PDF 没有目录</p>`;const flat=[];function walk(items){for(const item of items||[]){flat.push(item);walk(item.items);}}walk(outline);
 target.querySelectorAll('[data-pdf-outline]').forEach(button=>button.onclick=async()=>{try{const item=flat[Number(button.dataset.pdfOutline)];const dest=typeof item.dest==='string'?await session.doc.getDestination(item.dest):item.dest;if(!Array.isArray(dest)||session!==pdfState)return;const page=typeof dest[0]==='number'?dest[0]:await session.doc.getPageIndex(dest[0]);if(session!==pdfState)return;jumpPdfPage(page+1);}catch(error){fail(error);}});I18n.localize(target);
}
function jumpPdfPage(page){const input=$('#pdf-page-input');if(!input)return;input.value=Math.max(1,Math.min(pdfState.doc.numPages,page));input.dispatchEvent(new Event('change',{bubbles:true}));}
function currentReadingLocation(){if(pdfState)return {chapter:0,page:Number($('#pdf-page-input')?.value)||1,position:0};return {chapter:state.chapter,page:0,position:readerPosition()};}
async function addReadingBookmark(){
 if(!state.viewer)return;const at=currentReadingLocation(),list=state.viewer.bookmarks||[];
 if(list.some(b=>b.chapter===at.chapter&&b.page===at.page&&Math.abs(b.position-at.position)<.01)){toast('这个位置已有书签。');return;}
 const name=at.page?I18n.text('第 '+at.page+' 页'):state.book.chapters[at.chapter].title+' · '+Math.round(at.position*100)+'%';
 await patch(state.viewer,{bookmarks:[...list,{id:uid(),name,...at}]});refreshBookmarks();toast('书签已保存。');
}
function refreshBookmarks(){const root=$('[data-reading-pane="bookmarks"]');if(root){root.innerHTML=bookmarkMarkup();I18n.localize(root);}}
async function openReadingLocation(at){if(at.page){jumpPdfPage(at.page);return;}if(!state.book)return;if(at.chapter!==state.chapter)await changeChapter(at.chapter);await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));restoreReaderPosition(at.position||0);}
function renderFullSearchResults(){const target=$('#reading-full-results');if(target){target.innerHTML=searchResultMarkup();I18n.localize(target);}}
async function searchReadingAll(query){
 const tool=ensureReadingToolState(),epoch=++readingSearchEpoch,session=pdfState,book=state.book,id=state.viewer?.id;
 tool.query=query.trim().slice(0,200);tool.results=[];tool.busy=!!tool.query;if(tool.query&&!tool.origin)tool.origin=currentReadingLocation();renderFullSearchResults();if(!tool.query)return;
 const needle=tool.query.replace(/[.*+?^${}()|[\]\\]/g,'\\$&'),matcher=new RegExp(needle,'giu'),count=session?session.doc.numPages:book?.chapters.length||0;
 for(let n=0;n<count;n++){
   if(epoch!==readingSearchEpoch||state.viewer?.id!==id)return;
   let text,title;if(session){try{const page=await session.doc.getPage(n+1);const content=await page.getTextContent();text=content.items.map(i=>i.str+(i.hasEOL?'\n':' ')).join('');title=I18n.text('第 '+(n+1)+' 页');}catch(error){if(epoch!==readingSearchEpoch||session!==pdfState)return;throw error;}}else{text=book.chapters[n].text;title=book.chapters[n].title;}
   if(epoch!==readingSearchEpoch||state.viewer?.id!==id)return;
   matcher.lastIndex=0;let match;while(tool.results.length<200&&(match=matcher.exec(text))){const at=match.index,length=match[0].length;tool.results.push({chapter:n,page:session?n+1:0,start:at,end:at+length,title,before:text.slice(Math.max(0,at-24),at),match:text.slice(at,at+length),after:text.slice(at+length,at+length+52)});}
   if(n%5===0)renderFullSearchResults();if(tool.results.length>=200)break;await new Promise(resolve=>setTimeout(resolve,0));
 }
 if(epoch===readingSearchEpoch){tool.busy=false;renderFullSearchResults();}
}
function textRange(root,start,end){const walker=document.createTreeWalker(root,NodeFilter.SHOW_TEXT);let node,offset=0,range=new Range(),found=false;while(node=walker.nextNode()){const next=offset+node.length;if(!found&&next>start){range.setStart(node,start-offset);found=true;}if(found&&next>=end){range.setEnd(node,end-offset);return range;}offset=next;}return null;}
function revealReadingRange(range){const el=$('#reader-scroll');if(!el||!range)return;const rect=range.getBoundingClientRect(),box=el.getBoundingClientRect();if(readerPaged())el.scrollTo({left:Math.floor((el.scrollLeft+rect.left-box.left)/Math.max(1,el.clientWidth))*el.clientWidth,behavior:reducedMotion()?'instant':'smooth'});else el.scrollBy({top:rect.top-box.top-el.clientHeight*.3,behavior:reducedMotion()?'instant':'smooth'});}
async function showReadingSearchResult(index){
 const result=ensureReadingToolState().results[index];if(!result)return;
 if(result.page){jumpPdfPage(result.page);const session=pdfState;for(let n=0;n<40&&session===pdfState;n++){const layer=$(`.pdf-page[data-page="${result.page}"] .textLayer`);if(layer){const query=ensureReadingToolState().query.toLocaleLowerCase(),at=layer.textContent.toLocaleLowerCase().indexOf(query);if(at>=0){const range=textRange(layer,at,at+query.length);if(range&&window.Highlight)CSS.highlights.set('kok-global-find',new Highlight(range));}break;}await new Promise(r=>setTimeout(r,80));}return;}
 if(result.chapter!==state.chapter)await changeChapter(result.chapter);
 await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
 const range=textRange($('#reader-text'),result.start,result.end);if(range&&window.Highlight)CSS.highlights.set('kok-global-find',new Highlight(range));revealReadingRange(range);
}
function showReadingSettings(){
 const rows=[['readerSize','字号',12,32,1,18,'px'],['readerLineHeight','行距',1.2,2.8,.05,2.05,''],['readerLetterSpacing','字距',0,3,.1,.3,'px'],['readerParagraphSpace','段落留白',0,2,.1,.3,'em'],['readerWidth','正文宽度',420,1100,20,760,'px']];
 showSmall(`<div class="dialog-topline"><div><small class="eyebrow">阅读空间</small><h2>舒服地读下去</h2></div></div><div class="reading-layout-choices"><button data-reading-layout="scroll" aria-pressed="${state.settings.readerLayout!=='paged'}">${svg('list')}连续滚动</button><button data-reading-layout="paged" aria-pressed="${state.settings.readerLayout==='paged'}">${svg('book')}左右分页</button></div>${rows.map(([key,name,min,max,step,fallback,unit])=>`<label class="appearance-range"><span>${name}<output data-reader-value="${key}" data-user-content>${state.settings[key]??fallback}${unit}</output></span><input type="range" data-reader-setting="${key}" data-unit="${unit}" min="${min}" max="${max}" step="${step}" value="${state.settings[key]??fallback}" aria-label="${name}"></label>`).join('')}<div class="appearance-footer"><small>设置保存在本机，保留原文件</small><button class="text-button" data-reset-reading>恢复默认排版</button></div>`);$('#small-dialog').classList.add('reading-settings-dialog');I18n.localize($('#small-dialog'));
}
async function exportReadingNotes(){const item=state.viewer;if(!item)return;let text='# '+item.name.replace(/[\r\n]/g,' ')+'\n\n';for(const mark of item.annotations||[]){text+='## '+(mark.page?I18n.text('第 '+mark.page+' 页'):state.book?.chapters[mark.chapter]?.title||String(mark.chapter+1))+'\n\n> '+String(mark.text||'').replace(/\n/g,'\n> ')+'\n\n'+(mark.note||'')+'\n\n';}text+='## '+I18n.text('我的书签')+'\n\n'+(item.bookmarks||[]).map(b=>'- '+b.name).join('\n')+'\n\n## '+I18n.text('笔记')+'\n\n'+(item.notes||'');if(await native('exportReadingNotes',{name:item.name,text}))toast('笔记与书签已导出。');}
const renderReadingBeforeTools=renderReadingWorkspace;
renderReadingWorkspace=function(position=0){
 renderReadingBeforeTools(position);ensureReadingToolState();applyReadingType();paragraphSpacing($('#reader-text'));setupReadingSidebar();
 $('.reader-tools .toolbar-spacer').insertAdjacentHTML('beforebegin',readingToolbarMarkup());
 $('.reading-position').insertAdjacentHTML('beforeend','<small id="reading-page-indicator" data-user-content></small>');
 $('.reader-bottom').insertAdjacentHTML('beforebegin','<label class="reading-progress-control"><input type="range" id="reading-location" min="0" max="1000" value="0" aria-label="阅读进度跳转"></label>');
 const scroller=$('#reader-scroll');let current=position,ready=false;const resize=new ResizeObserver(()=>{const fraction=ready?readerPosition():current;scroller.style.setProperty('--reading-column',Math.max(200,scroller.clientWidth-64)+'px');requestAnimationFrame(()=>{if(scroller.isConnected){restoreReaderPosition(fraction);ready=true;}});});resize.observe(scroller);workspaceDisposers.push(()=>resize.disconnect());
 requestAnimationFrame(()=>requestAnimationFrame(()=>{if(scroller.isConnected)restoreReaderPosition(position);}));
 scroller.addEventListener('scroll',()=>{current=readerPosition();},{passive:true});
 $('#reader-find').addEventListener('input',()=>requestAnimationFrame(()=>{if(readerPaged())revealReadingRange([...(CSS.highlights?.get('kok-find-current')||[])][0]);}));
 I18n.localize($('.reading-workspace'));
};
const renderPdfBeforeTools=renderPdf;
renderPdf=async function(item){await renderPdfBeforeTools(item);if(pdfState?.itemId!==item.id)return;ensureReadingToolState();const root=$('.pdf-workspace');root.insertAdjacentHTML('beforeend','<aside class="kok-pdf-sidebar"></aside>');root.querySelector('.pdf-tools')?.insertAdjacentHTML('beforeend',readingToolbarMarkup(true));if(!root.querySelector('[data-add-bookmark]'))root.querySelector('.pdf-page-controls').insertAdjacentHTML('afterend',`<div class="pdf-extra-tools">${readingToolbarMarkup(true)}</div>`);setupReadingSidebar(true);I18n.localize(root);};
document.addEventListener('input',event=>{
 const input=event.target;if(input.id==='reading-toc-filter'){const query=input.value.trim().toLocaleLowerCase();document.querySelectorAll('.toc-chapters [data-chapter],.pdf-outline [data-pdf-outline]').forEach(b=>b.hidden=!b.textContent.toLocaleLowerCase().includes(query));}
 if(input.id==='reading-full-query'){clearTimeout(input.searchDelay);input.searchDelay=setTimeout(()=>searchReadingAll(input.value).catch(fail),250);}
 if(input.dataset.readerSetting){const fraction=readerPosition(),key=input.dataset.readerSetting;state.settings[key]=Number(input.value);$(`[data-reader-value="${key}"]`).textContent=input.value+input.dataset.unit;if(key==='readerSize'){$('#reader-scroll').style.fontSize=input.value+'px';$('#font-size-label').textContent=input.value+' px';}applyReadingType();requestAnimationFrame(()=>restoreReaderPosition(fraction));}
});
document.addEventListener('change',async event=>{const input=event.target;try{if(input.dataset.readerSetting)await setting({[input.dataset.readerSetting]:Number(input.value)});if(input.id==='reading-location'&&state.book){const target=Math.min(.999999,Number(input.value)/1000)*state.book.chapters.length;await openReadingLocation({chapter:Math.floor(target),position:target%1,page:0});await saveReader();}}catch(error){fail(error);}});
document.addEventListener('click',event=>{const b=event.target.closest('[data-reader]');if(b&&readerPaged()) {event.preventDefault();event.stopImmediatePropagation();advanceReadingPage(b.dataset.reader==='next'?1:-1);}},true);
document.addEventListener('click',async event=>{const b=event.target.closest('button');if(!b)return;try{
 if(b.dataset.readingTab)setReadingTab(b.dataset.readingTab);
 if(b.hasAttribute('data-add-bookmark'))await addReadingBookmark();
 if(b.dataset.openBookmark){const mark=state.viewer.bookmarks.find(m=>m.id===b.dataset.openBookmark);if(mark)await openReadingLocation(mark);}
 if(b.dataset.removeBookmark){await patch(state.viewer,{bookmarks:state.viewer.bookmarks.filter(m=>m.id!==b.dataset.removeBookmark)});refreshBookmarks();}
 if(b.hasAttribute('data-reading-settings'))showReadingSettings();
 if(b.hasAttribute('data-close-small'))await closeSmall();
 if(b.hasAttribute('data-reading-focus')){viewer.classList.toggle('reading-focused');b.setAttribute('aria-pressed',String(viewer.classList.contains('reading-focused')));}
 if(b.hasAttribute('data-reading-sidebar'))$('.pdf-workspace')?.classList.toggle('has-reading-sidebar');
 if(b.hasAttribute('data-reading-result'))await showReadingSearchResult(Number(b.dataset.readingResult));
 if(b.hasAttribute('data-reading-origin')&&readingToolState.origin)await openReadingLocation(readingToolState.origin);
 if(b.hasAttribute('data-stop-reading-search')){readingSearchEpoch++;readingToolState.busy=false;renderFullSearchResults();}
 if(b.hasAttribute('data-export-reading-notes'))await exportReadingNotes();
 if(b.dataset.readingLayout){const position=readerPosition();await setting({readerLayout:b.dataset.readingLayout});renderReader(position);showReadingSettings();}
 if(b.hasAttribute('data-reset-reading')){const position=readerPosition();await setting({...readingDefaults,readerSize:18});renderReader(position);showReadingSettings();}
 if(b.hasAttribute('data-find-step'))requestAnimationFrame(()=>{if(readerPaged())revealReadingRange([...(CSS.highlights?.get('kok-find-current')||[])][0]);});
 }catch(error){fail(error);}});
document.addEventListener('keydown',event=>{
 if(!viewer.open||$('#small-dialog').open||event.target.closest('input,textarea,select,[contenteditable]'))return;
 if((state.book||pdfState)&&(event.ctrlKey||event.metaKey)&&event.shiftKey&&event.key.toLowerCase()==='f'){event.preventDefault();event.stopImmediatePropagation();setReadingTab('search');$('#reading-full-query')?.focus();return;}
 if(state.book&&readerPaged()&&['ArrowLeft','ArrowRight','PageUp','PageDown','Space'].includes(event.code)&&!event.ctrlKey&&!event.altKey&&!event.metaKey){event.preventDefault();event.stopImmediatePropagation();advanceReadingPage(['ArrowLeft','PageUp'].includes(event.code)||event.shiftKey?-1:1);}
},true);
const clearViewerBeforeReadingTools=clearViewer;
clearViewer=function(){readingSearchEpoch++;readingToolState=null;viewer.classList.remove('reading-focused');CSS.highlights?.delete('kok-global-find');clearViewerBeforeReadingTools();};

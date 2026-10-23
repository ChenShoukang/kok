'use strict';
Object.assign(paths,{
 expand:'M8 3H3v5 M16 3h5v5 M21 16v5h-5 M3 16v5h5',
 panel:'M3 4h18v16H3z M9 4v16',
 back10:'M4 9a9 9 0 1 1 0 7 M4 3v6h6 M10 12v5 M14 12h3v5h-3z',
 forward10:'M20 9a9 9 0 1 0 0 7 M20 3v6h-6 M7 12v5 M11 12h3v5h-3z',
 pip:'M3 4h18v16H3z M12 11h7v7h-7z',
 headphones:'M4 14v-3a8 8 0 0 1 16 0v3 M4 12H3v8h5v-8H4 M20 12h1v8h-5v-8h4',
 focus:'M8 3H3v5 M16 3h5v5 M21 16v5h-5 M3 16v5h5 M9 12h6'
});
const workspaceButton=(icon,label,attributes='',className='')=>`<button class="workspace-button ${className}" title="${esc(label)}" aria-label="${esc(label)}" ${attributes}>${svg(icon)}</button>`;
let workspaceDisposers=[];
function cleanupWorkspace(){
 for(const dispose of workspaceDisposers)dispose();workspaceDisposers=[];
 CSS.highlights?.delete('kok-find');CSS.highlights?.delete('kok-find-current');
}
function setRangeProgress(input,ratio){if(input)input.style.setProperty('--filled',(Math.max(0,Math.min(1,ratio||0))*100)+'%');}
function renderVideoWorkspace(item){
 $('#viewer-content').innerHTML=`<section class="video-workspace">
 <div class="video-canvas"><video id="video" crossorigin="anonymous" preload="metadata" src="${media(item)}"></video><button id="video-center-play" class="video-center-play" aria-label="播放或暂停">${svg('play')}</button><div class="video-error hidden"><span>${svg('video')}</span><h2>此视频暂时无法内置播放</h2><p>请使用系统播放器打开，原文件保持不变。</p><button class="button secondary" data-native="openExternal" data-id="${item.id}">系统播放器打开</button></div></div>
 <div class="video-controls"><div class="video-control-row">
 <div class="control-group video-transport">${workspaceButton('play','播放或暂停','id="video-toggle"','primary-control')}${workspaceButton('back10','后退 10 秒','data-video-skip="-10"')}${workspaceButton('forward10','前进 10 秒','data-video-skip="10"')}<span class="video-time"><span id="video-current">0:00</span><span class="time-divider">/</span><span id="video-duration">0:00</span></span></div>
 <div class="video-timeline"><input id="video-seek" class="workspace-range" type="range" min="0" max="1000" value="0" aria-label="视频播放进度" disabled></div>
 <div class="control-group video-right"><label class="workspace-volume">${svg('volume')}<input id="video-volume" class="workspace-range" type="range" min="0" max="1" step=".01" value=".8" aria-label="视频音量"></label>${workspaceButton('settings','播放设置','id="video-options-toggle" aria-expanded="false" aria-controls="video-options"')}${workspaceButton('pip','画中画','id="video-pip"')}${workspaceButton('expand','全屏','id="video-fullscreen" aria-pressed="false"')}</div>
 </div></div>
 <div id="video-options" class="video-feature-bar video-options hidden" role="region" aria-label="播放设置"><label>倍速 <select id="video-rate" aria-label="播放倍速">${[.5,.75,1,1.25,1.5,1.75,2,2.5,3].map(n=>`<option value="${n}">${n}×</option>`).join('')}</select></label><button id="smart-speed" class="video-pill">静音自动加速</button><label class="subtitle-choice">字幕 <select id="subtitle-select" aria-label="字幕轨道"><option value="-1">关闭</option></select></label><button id="load-subtitle" class="video-pill">载入字幕</button><span id="rate-status" role="status"></span><details class="video-shortcut-details"><summary>快捷键与跳跃设置</summary>${videoPreferencesMarkup()}</details><p id="video-hint">在设置中自定义快捷键与跳跃距离</p></div>
 </section>`;
 video=$('#video');const player=video,root=$('.video-workspace'),controls=$('.video-controls'),options=$('#video-options'),optionsButton=$('#video-options-toggle');audio.pause();
 player.volume=Number(state.settings.videoVolume??.8);$('#video-volume').value=player.volume;
 let idleTimer,disposed=false,keyboardFocus=false;
 const setOptions=open=>{optionsButton.setAttribute('aria-expanded',String(open));options.classList.toggle('hidden',!open);if(open)animateIn(options);};
 const reveal=()=>{
   clearTimeout(idleTimer);root.classList.remove('controls-idle');
   if(document.fullscreenElement===root&&!player.paused&&!player.ended&&!player.error&&options.classList.contains('hidden')){
     idleTimer=setTimeout(()=>{if(!controls.matches(':hover')&&(!keyboardFocus||!controls.matches(':focus-within'))&&options.classList.contains('hidden'))root.classList.add('controls-idle');},2400);
   }
 };
 const update=()=>{
   if(!root.isConnected)return;
   const playable=Number.isFinite(player.duration)&&player.duration>0;
   $('#video-toggle').innerHTML=svg(player.paused?'play':'pause');$('#video-center-play').innerHTML=svg(player.paused?'play':'pause');
   root.classList.toggle('is-playing',!player.paused);$('#video-seek').disabled=!playable;
   $('#video-current').textContent=time(player.currentTime);$('#video-duration').textContent=time(player.duration);
   const ratio=playable?player.currentTime/player.duration:0;$('#video-seek').value=Math.round(ratio*1000);setRangeProgress($('#video-seek'),ratio);
   setRangeProgress($('#video-volume'),player.volume);
 };
 const toggle=()=>{if(player.paused)player.play().catch(fail);else player.pause();};
 $('.video-canvas').addEventListener('contextmenu',event=>showVideoContextMenu(event,item,player));
 $('#video-toggle').onclick=toggle;$('#video-center-play').onclick=toggle;player.onclick=toggle;
 root.querySelectorAll('[data-video-skip]').forEach(button=>button.onclick=()=>{if(Number.isFinite(player.duration))player.currentTime=Math.max(0,Math.min(player.duration,player.currentTime+Number(button.dataset.videoSkip)));});
 $('#video-seek').oninput=event=>{if(Number.isFinite(player.duration))player.currentTime=Number(event.target.value)/1000*player.duration;update();};
 $('#video-volume').oninput=event=>{player.volume=Number(event.target.value);update();};
 $('#video-volume').onchange=()=>setting({videoVolume:player.volume}).catch(fail);
 optionsButton.onclick=()=>{setOptions(optionsButton.getAttribute('aria-expanded')!=='true');reveal();};
 const toggleFullscreen=async()=>{try{setOptions(false);if(document.fullscreenElement)await document.exitFullscreen();else await root.requestFullscreen();}catch(error){fail(error);}};
 $('#video-fullscreen').onclick=toggleFullscreen;$('.video-canvas').ondblclick=event=>{if(videoPreferences().doubleClick&&!event.target.closest('.video-error'))toggleFullscreen();};
 const fullscreenChanged=()=>{
   if(disposed)return;
   const full=document.fullscreenElement===root,button=$('#video-fullscreen');
   button.setAttribute('aria-pressed',String(full));button.setAttribute('aria-label',full?'退出全屏':'全屏');button.title=full?'退出全屏':'全屏';I18n.localize(button);
   if(!full)viewer.dataset.fullscreenExited=String(Date.now());
   reveal();
 };
 const dismiss=event=>{if(!options.classList.contains('hidden')&&!options.contains(event.target)&&!optionsButton.contains(event.target))setOptions(false);};
 const keys=event=>{
   if(event.key==='Tab')keyboardFocus=true;reveal();
   if(event.key==='Escape'&&!options.classList.contains('hidden')){event.preventDefault();event.stopPropagation();setOptions(false);optionsButton.focus();reveal();return;}
   if(event.target.matches('input,textarea,select,[contenteditable]'))return;
   if(event.key==='Escape'&&document.fullscreenElement===root){event.preventDefault();event.stopPropagation();document.exitFullscreen().catch(fail);}
   if(videoPreferences().enabled&&((event.key.toLowerCase()==='f'&&!event.ctrlKey&&!event.metaKey&&!event.altKey)||event.key==='F11')){event.preventDefault();event.stopPropagation();toggleFullscreen();}
 };
 document.addEventListener('fullscreenchange',fullscreenChanged);document.addEventListener('pointerdown',dismiss);viewer.addEventListener('keydown',keys,true);
 root.addEventListener('pointermove',reveal);root.addEventListener('focusin',reveal);root.addEventListener('pointerdown',()=>{keyboardFocus=false;reveal();});controls.addEventListener('pointerleave',reveal);
 player.addEventListener('play',reveal);player.addEventListener('pause',reveal);player.addEventListener('ended',reveal);
 workspaceDisposers.push(()=>{disposed=true;clearTimeout(idleTimer);document.removeEventListener('fullscreenchange',fullscreenChanged);document.removeEventListener('pointerdown',dismiss);viewer.removeEventListener('keydown',keys,true);});
 $('#video-pip').hidden=!document.pictureInPictureEnabled;
 $('#video-pip').onclick=async()=>{try{if(document.pictureInPictureElement)await document.exitPictureInPicture();else await player.requestPictureInPicture();}catch{toast('画中画暂不可用，请先播放视频。',true);}};
 player.addEventListener('loadedmetadata',()=>{if(item.position>0&&item.position<player.duration-.1)player.currentTime=item.position;update();});
 for(const event of ['play','pause','durationchange','timeupdate','volumechange','ended'])player.addEventListener(event,update);
 player.addEventListener('pause',()=>saveVideo().catch(fail));player.addEventListener('ended',()=>saveVideo().catch(fail));
 player.addEventListener('timeupdate',()=>{if(Date.now()-mediaSaveTime>5000){mediaSaveTime=Date.now();saveVideo().catch(fail);}});
 player.addEventListener('error',()=>{if(root.isConnected){$('.video-error').classList.remove('hidden');$('#video-center-play').classList.add('hidden');$('#video-hint').textContent='当前内核无法解码此视频，请使用系统播放器。';reveal();}});
 player.addEventListener('waiting',()=>root.classList.add('is-buffering'));player.addEventListener('playing',()=>root.classList.remove('is-buffering'));
 syncVideoPreferences(root);initVideoFeatures(item,player).catch(fail);update();animateIn(controls);
}
function renderMusicWorkspace(){
 const item=state.track;if(!item||state.viewer?.kind!=='music')return;
 state.viewer=item;viewer.dataset.kind='music';$('#viewer-title').textContent=item.name;$('#viewer-subtitle').textContent=`${labels.music} · ${item.ext} · ${size(item.size)}`;$('#notes').value=item.notes||'';$('#viewer-favorite').classList.toggle('selected',item.favorite);
 $('#viewer-content').innerHTML=`<section class="music-workspace" style="--album-hue:${hashHue(albumName(item))}" data-track="${item.id}"><div class="now-listening"><div class="listening-art">${albumArt(albumName(item),true)}</div><div class="listening-info"><span class="workspace-eyebrow" id="listening-status">正在播放</span><h1 data-user-content>${esc(item.name)}</h1><p><span ${item.artist&&item.artist!=='本地音乐'?'data-user-content':''}>${esc(item.artist||'本地音乐')}</span><span> · </span><span ${albumName(item)==='未命名专辑'?'':'data-user-content'}>${esc(albumName(item))}</span></p></div><div class="listening-player"><div class="music-playback-error hidden" id="music-playback-error"><span>此音频暂时无法内置播放</span><button data-native="openExternal" data-id="${item.id}">系统播放器打开</button></div><div class="listening-timeline"><input id="listening-seek" class="workspace-range" type="range" min="0" max="1000" value="0" aria-label="歌曲播放进度"><div><span id="listening-current">0:00</span><span id="listening-duration">0:00</span></div></div><div class="listening-transport">${workspaceButton('shuffle','随机播放','data-audio-action="shuffle"')}${workspaceButton('skipBack','上一首','data-audio-action="prev"')}${workspaceButton('play','播放或暂停','id="listening-play" data-audio-action="play"','listening-play')}${workspaceButton('skipForward','下一首','data-audio-action="next"')}${workspaceButton('repeat','循环播放','data-audio-action="repeat"')}</div><div class="listening-footer"><label class="workspace-volume">${svg('volume')}<input id="listening-volume" class="workspace-range" type="range" min="0" max="1" step=".01" aria-label="音乐音量"></label><button class="text-button" data-listening-collapse>${svg('chevron')}收起播放器</button></div></div></div><aside class="listening-queue"><div class="listening-queue-heading"><span>${svg('listMusic')}播放队列</span><small>${state.queue.length}</small></div><div class="listening-queue-list">${state.queue.map((track,n)=>`<button class="listening-track ${track.id===item.id?'active':''}" data-music-track="${track.id}"><span class="listening-track-index">${track.id===item.id?svg('volume'):String(n+1).padStart(2,'0')}</span><span><b data-user-content>${esc(track.name)}</b><small ${track.artist&&track.artist!=='本地音乐'?'data-user-content':''}>${esc(track.artist||'本地音乐')}</small></span><span class="listening-track-ext" data-user-content>${esc(track.ext)}</span></button>`).join('')}</div><div class="queue-footnote">关闭预览后，音乐继续播放</div></aside></section>`;
 $('#listening-seek').oninput=event=>{if(Number.isFinite(audio.duration))audio.currentTime=Number(event.target.value)/1000*audio.duration;syncMusicWorkspace();};
 $('#listening-volume').oninput=event=>{audio.volume=Number(event.target.value);$('#volume').value=audio.volume;syncMusicWorkspace();};
 $('#listening-volume').onchange=()=>setting({volume:audio.volume}).catch(fail);
 syncMusicWorkspace();animateIn($('.listening-art'));animateIn($('.listening-info'));
}
function syncMusicWorkspace(trackChanged=false){
 if(state.viewer?.kind!=='music'||!state.track)return;
 const workspace=$('.music-workspace');if(!workspace||workspace.dataset.track!==state.track.id){if(trackChanged)renderMusicWorkspace();return;}
 $('#music-playback-error').classList.toggle('hidden',!audio.error);$('#listening-status').textContent=audio.error?'无法播放':audio.paused?'已暂停':'正在播放';$('#listening-play').innerHTML=svg(audio.paused?'play':'pause');workspace.classList.toggle('is-playing',!audio.paused);
 workspace.querySelector('[data-audio-action="shuffle"]').setAttribute('aria-pressed',String(state.shuffle));workspace.querySelector('[data-audio-action="repeat"]').setAttribute('aria-pressed',String(state.loop));
 $('#listening-current').textContent=time(audio.currentTime);$('#listening-duration').textContent=time(audio.duration);
 const ratio=Number.isFinite(audio.duration)&&audio.duration>0?audio.currentTime/audio.duration:0;$('#listening-seek').value=Math.round(ratio*1000);$('#listening-seek').disabled=!Number.isFinite(audio.duration);
 setRangeProgress($('#listening-seek'),ratio);$('#listening-volume').value=audio.volume;setRangeProgress($('#listening-volume'),audio.volume);
}
function renderReadingWorkspace(scroll=0){
 cleanupWorkspace();
 CSS.highlights?.delete('kok-find');CSS.highlights?.delete('kok-find-current');
 const book=state.book,chapter=book.chapters[state.chapter],documentMode=state.viewer.kind==='document';
 $('#viewer-content').innerHTML=`<section class="reader reading-workspace reader-${esc(state.settings.readerFollowTheme!==false?'auto':state.settings.readerTheme||'cream')} ${documentMode?'document-mode':'book-mode'} ${state.settings.readerToc===false?'toc-collapsed':''}"><aside class="reader-toc"><div class="toc-heading"><span>目录</span><small>${book.chapters.length}</small></div><div class="toc-book-title" data-user-content>${esc(book.title||state.viewer.name)}</div><div class="toc-chapters">${book.chapters.map((c,n)=>`<button data-chapter="${n}" ${c.generatedTitle?'data-ui-content':''} class="${n===state.chapter?'active':''}" title="${esc(c.title)}"><small>${String(n+1).padStart(2,'0')}</small><span ${c.generatedTitle?'data-ui-content':'data-user-content'}>${esc(c.title)}</span></button>`).join('')}</div><div class="toc-footnote">选中文字，记录你的想法</div></aside><section class="reader-body"><div class="reader-tools">${workspaceButton('panel','显示或收起目录','data-toggle-toc aria-expanded="'+String(state.settings.readerToc!==false)+'"')}${workspaceButton('search','搜索当前章节','data-reader-search-toggle aria-expanded="false"')}<span class="tool-separator"></span><div class="font-control"><button class="icon-button" data-font="-1" title="减小字号" aria-label="减小字号">A−</button><span id="font-size-label">${Number(state.settings.readerSize)||18} px</span><button class="icon-button" data-font="1" title="增大字号" aria-label="增大字号">A+</button></div><div class="paper-options"><button class="text-button paper-auto" data-paper="auto" title="跟随界面" aria-label="跟随界面">${svg('sun')}</button><button class="paper-swatch cream" data-paper="cream" title="护眼纸色" aria-label="护眼纸色"></button><button class="paper-swatch white" data-paper="white" title="纯白纸色" aria-label="纯白纸色"></button><button class="paper-swatch dark" data-paper="dark" title="夜间阅读" aria-label="夜间阅读"></button></div><span class="toolbar-spacer"></span></div><div class="reader-search hidden"><label>${svg('search')}<input id="reader-find" type="search" placeholder="搜索当前章节" aria-label="搜索当前章节" autocomplete="off"></label><span id="reader-find-count">0 / 0</span>${workspaceButton('skipBack','上一个匹配','data-find-step="-1"')}${workspaceButton('skipForward','下一个匹配','data-find-step="1"')}${workspaceButton('close','关闭搜索','data-reader-search-close')}</div><article class="reader-scroll" id="reader-scroll" style="font-size:${Math.max(12,Math.min(32,Number(state.settings.readerSize)||18))}px"><div class="reading-sheet"><div class="reading-kicker">${documentMode?'文档正文':'正文阅读'}</div><h2 ${chapter.generatedTitle?'data-ui-content':''}>${esc(chapter.title)}</h2><div class="reader-text" id="reader-text" ${chapter.placeholder?'data-ui-content':''}>${markedText(state.viewer,chapter.text,state.chapter)}</div><div class="reading-end" aria-hidden="true">· · ·</div></div></article><footer class="reader-bottom"><button data-reader="prev" ${state.chapter===0?'disabled':''}>← 上一节</button><div class="reading-position"><span id="reading-progress">${state.chapter+1} / ${book.chapters.length}</span><span class="reading-meter"><i></i></span><small id="reading-percent">0%</small></div><button data-reader="next" ${state.chapter===book.chapters.length-1?'disabled':''}>下一节 →</button></footer></section></section>`;
 const container=$('#reader-scroll');requestAnimationFrame(()=>{if(container.isConnected){container.scrollTop=scroll*Math.max(0,container.scrollHeight-container.clientHeight);updateReadingPosition();}});
 initReadingFeatures();setupReaderSearch();updateTocAccessibility();
 let compact;const readingRoot=$('.reading-workspace');const readingResize=new ResizeObserver(()=>{readingRoot.style.setProperty('--reader-tools-height',readingRoot.querySelector('.reader-tools').offsetHeight+'px');const next=readingRoot.clientWidth<760;if(next!==compact){compact=next;readingRoot.classList.toggle('toc-collapsed',next||state.settings.readerToc===false);const button=readingRoot.querySelector('[data-toggle-toc]');button.setAttribute('aria-expanded',String(!readingRoot.classList.contains('toc-collapsed')));updateTocAccessibility();}});readingResize.observe(readingRoot);readingResize.observe(readingRoot.querySelector('.reader-tools'));workspaceDisposers.push(()=>readingResize.disconnect());
 container.addEventListener('scroll',()=>{hideSelectionTools();updateReadingPosition();clearTimeout(readerTimer);readerTimer=setTimeout(()=>saveReader().catch(fail),700);});
}
function updateReadingPosition(){
 const el=$('#reader-scroll');if(!el||!state.book)return;
 const fraction=el.scrollHeight<=el.clientHeight?1:readerPosition(),progress=(state.chapter+fraction)/state.book.chapters.length;
 $('#reading-percent').textContent=Math.round(progress*100)+'%';$('.reading-meter i').style.width=(progress*100)+'%';
}
function updateTocAccessibility(){const toc=$('.reader-toc');if(toc){toc.inert=$('.reading-workspace').classList.contains('toc-collapsed');toc.setAttribute('aria-hidden',String(toc.inert));}}
function setupReaderSearch(){
 const input=$('#reader-find');let ranges=[],current=0;
 const reveal=()=>{
   $('#reader-find-count').textContent=`${ranges.length?current+1:0} / ${ranges.length}`;
   CSS.highlights?.delete('kok-find-current');if(!ranges.length)return;
   if(window.Highlight)CSS.highlights.set('kok-find-current',new Highlight(ranges[current]));
   const box=ranges[current].getBoundingClientRect(),scroller=$('#reader-scroll');scroller.scrollBy({top:box.top-scroller.getBoundingClientRect().top-scroller.clientHeight*.35,behavior:reducedMotion()?'instant':'smooth'});
 };
 input.oninput=()=>{
   CSS.highlights?.delete('kok-find');CSS.highlights?.delete('kok-find-current');ranges=[];current=0;
   const root=$('#reader-text'),text=root.textContent,query=input.value;
   if(query){const walker=document.createTreeWalker(root,NodeFilter.SHOW_TEXT),nodes=[];let node,offset=0;while(node=walker.nextNode()){nodes.push({node,start:offset,end:offset+node.length});offset+=node.length;}
     const slash=String.fromCharCode(92),literal=Array.from(query).map(char=>('^$.*+?()[]{}|'+slash).includes(char)?slash+char:char).join('');
     for(const match of text.matchAll(new RegExp(literal,'giu'))){if(ranges.length>=1000)break;const at=match.index,end=at+match[0].length,a=nodes.find(n=>n.end>at),b=nodes.find(n=>n.end>=end);if(!a||!b)continue;const range=new Range();range.setStart(a.node,at-a.start);range.setEnd(b.node,end-b.start);ranges.push(range);}
   }
   if(window.Highlight&&ranges.length)CSS.highlights.set('kok-find',new Highlight(...ranges));reveal();
 };
 $('.reader-search').querySelectorAll('[data-find-step]').forEach(button=>button.onclick=()=>{if(ranges.length){current=(current+Number(button.dataset.findStep)+ranges.length)%ranges.length;reveal();}});
 input.onkeydown=event=>{if(event.key==='Enter'){event.preventDefault();if(ranges.length){current=(current+(event.shiftKey?-1:1)+ranges.length)%ranges.length;reveal();}}if(event.key==='Escape'){event.preventDefault();event.stopPropagation();closeReaderSearch();}};
}
function closeReaderSearch(){const bar=$('.reader-search');if(!bar)return;bar.classList.add('hidden');$('[data-reader-search-toggle]').setAttribute('aria-expanded','false');CSS.highlights?.delete('kok-find');CSS.highlights?.delete('kok-find-current');}
function pdfWorkspaceMarkup(){return `<section class="pdf-workspace document-workspace"><div class="pdf-toolbar"><span class="pdf-label">PDF</span><div class="pdf-page-controls">${workspaceButton('skipBack','上一页','data-pdf-step="-1"')}<input id="pdf-page-input" type="number" min="1" value="1" aria-label="跳转到页码"><span id="pdf-page-total">/ —</span>${workspaceButton('skipForward','下一页','data-pdf-step="1"')}</div><span class="toolbar-spacer"></span><select id="pdf-zoom" aria-label="PDF 缩放">${[[.7,'70%'],[.85,'85%'],[1,'适合宽度'],[1.15,'115%'],[1.3,'130%'],[1.5,'150%']].map(([v,l])=>`<option value="${v}" ${(state.pdfZoom||1)===v?'selected':''}>${l}</option>`).join('')}</select><button class="workspace-text-button" data-insert-text>${svg('plus')}<span>插入文字</span></button><button class="workspace-text-button" data-show-marks>${svg('edit')}<span>标记与想法</span></button>${workspaceButton('download','导出批注摘要','data-export-reading')}</div><div class="pdf-scroll"><div class="loading-view"><span class="spinner"></span>正在打开 PDF…</div></div><footer class="pdf-bottom"><span>选中文字即可划线 / 荧光标记</span><span id="pdf-page-status">1 / —</span></footer></section>`;}
function initPdfWorkspace(doc,scroll){
 const input=$('#pdf-page-input');input.max=doc.numPages;$('#pdf-page-total').textContent='/ '+doc.numPages;let frame=0;
 const go=n=>{n=Math.max(1,Math.min(doc.numPages,Math.round(Number(n))||1));const page=scroll.querySelector(`[data-page="${n}"]`),first=scroll.querySelector('.pdf-page');if(page&&first){input.value=n;scroll.scrollTo({top:page.offsetTop-first.offsetTop,behavior:reducedMotion()?'instant':'smooth'});}};
 input.onchange=()=>go(input.value);input.onkeydown=event=>{if(event.key==='Enter'){event.preventDefault();go(input.value);}};
 $('.pdf-page-controls').querySelectorAll('[data-pdf-step]').forEach(button=>button.onclick=()=>go(Number(input.value)+Number(button.dataset.pdfStep)));
 const update=()=>{frame=0;if(!scroll.isConnected)return;const pages=scroll.children,first=pages[0];if(!first)return;let low=0,high=pages.length-1,target=scroll.scrollTop+scroll.clientHeight*.2;while(low<high){const mid=Math.ceil((low+high)/2);if(pages[mid].offsetTop-first.offsetTop<=target)low=mid;else high=mid-1;}if(document.activeElement!==input)input.value=low+1;$('#pdf-page-status').textContent=`${low+1} / ${doc.numPages}`;};
 scroll.addEventListener('scroll',()=>{if(!frame)frame=requestAnimationFrame(update);});workspaceDisposers.push(()=>cancelAnimationFrame(frame));update();
}
document.addEventListener('click',async event=>{
 const button=event.target.closest('button');if(!button)return;
 try{
   if(button.hasAttribute('data-toggle-toc')){const root=$('.reading-workspace'),visible=root.classList.contains('toc-collapsed');root.classList.toggle('toc-collapsed',!visible);button.setAttribute('aria-expanded',String(visible));updateTocAccessibility();await setting({readerToc:visible});}
   if(button.hasAttribute('data-reader-search-toggle')){const bar=$('.reader-search'),open=bar.classList.contains('hidden');if(open){bar.classList.remove('hidden');button.setAttribute('aria-expanded','true');animateIn(bar);$('#reader-find').focus();if($('#reader-find').value)$('#reader-find').dispatchEvent(new Event('input'));}else closeReaderSearch();}
   if(button.hasAttribute('data-reader-search-close'))closeReaderSearch();
   if(button.dataset.audioAction){const targets={play:'play-pause',prev:'prev-track',next:'next-track',shuffle:'shuffle',repeat:'repeat'};$('#'+targets[button.dataset.audioAction])?.click();}
   if(button.dataset.musicTrack){await patch(byId(button.dataset.musicTrack),{opened:new Date().toISOString()});await playTrack(byId(button.dataset.musicTrack));}
   if(button.hasAttribute('data-listening-collapse'))await closeViewer();
 }catch(error){fail(error);}
});
document.addEventListener('keydown',event=>{
 if(event.target.matches('input,textarea,select,[contenteditable]')||$('#small-dialog').open)return;
 if(state.book&&(event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='f'){event.preventDefault();if($('.reader-search').classList.contains('hidden'))$('[data-reader-search-toggle]').click();else $('#reader-find').focus();}
});
$('#open-now-playing')?.addEventListener('click',async()=>{if(!state.track)return;await openItem(state.track.id,{play:!audio.paused,keepQueue:true});});

audio.addEventListener('error',()=>syncMusicWorkspace());

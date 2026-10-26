'use strict';
// Keep active native gestures cheap; settle typography once the viewport is stable.
const readingFlow={bookId:null,sidebarOpen:false,nativeGesture:false,resizeUntil:0,settle:0,layoutTimer:0,position:0,scroller:null,pageMotion:null,layoutPasses:0,wheel:{sum:0,last:0,locked:0},headerFrame:0,commands:[]};
function isWindowInteracting(){return readingFlow.nativeGesture||performance.now()<readingFlow.resizeUntil;}
function readingSpreadCount(){return readingLayout()==='spread'&&($('#reader-scroll')?.clientWidth||0)>=820?2:1;}
function readingPageOffset(element){return readingFlow.pageMotion?.element===element?readingFlow.pageMotion.to:element.scrollLeft;}
function cancelReadingPage(){const motion=readingFlow.pageMotion;if(!motion)return;cancelAnimationFrame(motion.frame);motion.element.style.scrollSnapType='';readingFlow.pageMotion=null;}
function animateReadingPage(element,to){
 cancelReadingPage();if(reducedMotion()||isWindowInteracting()){element.scrollLeft=to;return;}const motion={element,to,from:element.scrollLeft,started:performance.now(),frame:0};readingFlow.pageMotion=motion;element.style.scrollSnapType='none';
 const tick=now=>{if(readingFlow.pageMotion!==motion||!element.isConnected)return;const t=Math.min(1,(now-motion.started)/210),progress=1-Math.pow(1-t,3);element.scrollLeft=motion.from+(to-motion.from)*progress;if(t<1)motion.frame=requestAnimationFrame(tick);else{element.scrollLeft=to;element.style.scrollSnapType='';readingFlow.pageMotion=null;readingFlow.position=readerPosition();updateReadingPosition();}};motion.frame=requestAnimationFrame(tick);
}
function scheduleReadingFlowLayout(element,initial){
 if(!element?.isConnected)return;const fresh=readingFlow.scroller!==element;if(fresh){readingFlow.scroller=element;readingFlow.position=initial??0;}else if(initial!==undefined)readingFlow.position=initial;
 clearTimeout(readingFlow.layoutTimer);readingFlow.layoutTimer=setTimeout(()=>{if(!element.isConnected||element!==$('#reader-scroll'))return;if(isWindowInteracting())return scheduleReadingFlowLayout(element);cancelReadingPage();restoreReaderPosition(readingFlow.position);readingFlow.layoutPasses++;},isWindowInteracting()?140:fresh?0:80);
}
function finishWindowInteraction(){
 clearTimeout(readingFlow.settle);if(readingFlow.nativeGesture)return;const wait=Math.max(0,readingFlow.resizeUntil-performance.now());if(wait>0){readingFlow.settle=setTimeout(finishWindowInteraction,wait+8);return;}
 document.body.classList.remove('window-interacting');if(state.book){applyReadingType();scheduleReadingFlowLayout($('#reader-scroll'));}if(typeof syncReadingComparisonLayout==='function')syncReadingComparisonLayout();if(typeof scheduleMediaFit==='function')scheduleMediaFit();
}
function beginWindowInteraction(nativeGesture=false){
 const entering=!isWindowInteracting();if(entering&&$('#reader-scroll'))readingFlow.position=readerPosition();if(nativeGesture)readingFlow.nativeGesture=true;readingFlow.resizeUntil=performance.now()+160;document.body.classList.add('window-interacting');
 if(entering){cancelReadingPage();for(const element of [viewer,$('#viewer-content')]){elementMotions.get(element)?.cancel();elementMotions.delete(element);}closeViewerCommands();}
 clearTimeout(readingFlow.settle);readingFlow.settle=setTimeout(finishWindowInteraction,170);
}
window.addEventListener('resize',()=>beginWindowInteraction(),{passive:true});
window.chrome?.webview?.addEventListener('message',event=>{if(event.data.eventName!=='windowInteraction')return;if(event.data.data.active)beginWindowInteraction(true);else{readingFlow.nativeGesture=false;readingFlow.resizeUntil=performance.now()+40;finishWindowInteraction();}});
const renderBeforeReadingFlow=renderReadingWorkspace;
renderReadingWorkspace=function(position=0){
 const fresh=readingFlow.bookId!==state.viewer?.id;if(fresh){readingFlow.bookId=state.viewer?.id;readingFlow.sidebarOpen=false;}readingFlow.position=position;renderBeforeReadingFlow(position);viewer.classList.toggle('book-quiet',state.viewer.kind==='book');
 if(state.viewer.kind==='book'){$('.reading-workspace').classList.toggle('toc-collapsed',!readingFlow.sidebarOpen);$('[data-toggle-toc]').setAttribute('aria-expanded',String(readingFlow.sidebarOpen));updateTocAccessibility();}
 const bar=$('.reader-tools');bar.insertAdjacentHTML('afterbegin',`<div class="reading-flow-choices" role="group" aria-label="阅读方式">${[['paged','document','单页'],['spread','book','双页'],['scroll','list','上下滚动']].map(([mode,icon,label])=>`<button class="icon-button" data-reading-flow-layout="${mode}" aria-pressed="${readingLayout()===mode}" title="${label}" aria-label="${label}">${svg(icon)}</button>`).join('')}</div>`);
 const scroller=$('#reader-scroll');scroller.addEventListener('scroll',()=>{if(!isWindowInteracting())readingFlow.position=readerPosition();},{passive:true});I18n.localize(bar);scheduleReadingFlowLayout(scroller,position);scheduleViewerActions();syncFavoriteFeedback();
};
async function setReadingFlowLayout(mode){if(!['paged','spread','scroll'].includes(mode)||!state.book)return;const position=readerPosition();cancelReadingPage();const saved=setting({[readingPrefix()+'Layout']:mode});applyReadingType();readingFlow.position=position;scheduleReadingFlowLayout($('#reader-scroll'),position);for(const button of viewer.querySelectorAll('[data-reading-flow-layout]'))button.setAttribute('aria-pressed',String(button.dataset.readingFlowLayout===mode));dismissSelection();wakeViewerChrome();await saved;}
viewer.addEventListener('wheel',event=>{
 if(!event.target.closest('#reader-scroll')||!state.book||!readerPaged()||event.ctrlKey||event.metaKey||$('#small-dialog').open||getSelection()?.toString())return;event.preventDefault();
 const wheel=readingFlow.wheel,now=performance.now(),raw=Math.abs(event.deltaY)>=Math.abs(event.deltaX)?event.deltaY:event.deltaX,delta=raw*(event.deltaMode===1?16:event.deltaMode===2?300:1);if(!delta)return;
 if(now-wheel.last>160||Math.sign(delta)!==Math.sign(wheel.sum)){wheel.sum=0;wheel.locked=0;}wheel.last=now;wheel.sum+=delta;if(now<wheel.locked||Math.abs(wheel.sum)<36)return;wheel.locked=now+(Math.abs(delta)>=60?240:5000);advanceReadingPage(delta>0?1:-1);wakeViewerChrome();
},{passive:false});
function syncFavoriteFeedback(){
 for(const button of document.querySelectorAll('#viewer-favorite,#track-favorite,[data-favorite].card-favorite,[data-favorite].icon-button')){const item=button.id==='viewer-favorite'?state.viewer:button.id==='track-favorite'?state.track:byId(button.dataset.favorite);if(!item)continue;const selected=!!item.favorite;button.classList.toggle('selected',selected);button.setAttribute('aria-pressed',String(selected));button.title=I18n.text(selected?'取消收藏':'收藏');button.setAttribute('aria-label',button.title);const icon=button.querySelector('svg');icon?.setAttribute('viewBox','-1 -1 26 26');}
}
const favoriteBeforeFlow=favorite;favorite=async function(id){await favoriteBeforeFlow(id);if(state.viewer?.id===id)state.viewer.favorite=!!byId(id)?.favorite;syncFavoriteFeedback();};
const renderBeforeFavoriteFeedback=render;render=function(){renderBeforeFavoriteFeedback();syncFavoriteFeedback();};
function scheduleViewerActions(){if(readingFlow.headerFrame)return;readingFlow.headerFrame=requestAnimationFrame(()=>{readingFlow.headerFrame=0;organizeViewerActions();});}
function organizeViewerActions(){
 const actions=$('.viewer-actions');if(!actions)return;let extra=actions.querySelector('.viewer-extra-actions');if(!extra){actions.insertAdjacentHTML('beforeend',`<div class="viewer-extra-actions" role="group" aria-label="文件工具"></div><div class="viewer-pinned-actions"></div><button class="icon-button" id="viewer-more-actions" aria-label="更多文件工具" title="更多文件工具" aria-expanded="false">${svg('more')}</button><div class="viewer-system-actions"></div>`);extra=actions.querySelector('.viewer-extra-actions');}
 for(const button of [...actions.children].filter(el=>el.tagName==='BUTTON'&&el.id!=='viewer-more-actions')){const group=button.id==='close-viewer'||button.hasAttribute('data-preview-action')?'.viewer-system-actions':button.id==='viewer-favorite'?'.viewer-pinned-actions':'.viewer-extra-actions';actions.querySelector(group).append(button);}
 actions.setAttribute('aria-label',I18n.text('文件工具'));I18n.localize($('#viewer-more-actions'));syncFavoriteFeedback();
}
function closeViewerCommands(){viewer.querySelector('.viewer-overflow-menu')?.remove();$('#viewer-more-actions')?.setAttribute('aria-expanded','false');}
function showViewerCommands(){
 const opened=!!viewer.querySelector('.viewer-overflow-menu');closeViewerCommands();if(opened)return;readingFlow.commands=[...viewer.querySelectorAll('.viewer-extra-actions>button')].filter(button=>!button.classList.contains('hidden')&&!button.hidden&&getComputedStyle(button).display!=='none');
 const menu=document.createElement('div');menu.className='viewer-overflow-menu';menu.setAttribute('role','menu');menu.innerHTML=readingFlow.commands.map((button,n)=>`<button role="menuitem" data-viewer-command="${n}" ${button.disabled?'disabled':''}>${button.querySelector('svg')?.outerHTML||svg('document')}<span>${esc(button.title||button.getAttribute('aria-label')||button.textContent.trim())}</span></button>`).join('');viewer.append(menu);$('#viewer-more-actions').setAttribute('aria-expanded','true');I18n.localize(menu);animateIn(menu);menu.querySelector('button:not(:disabled)')?.focus();wakeViewerChrome();
}
new MutationObserver(scheduleViewerActions).observe($('.viewer-actions'),{childList:true});
document.addEventListener('click',async event=>{const button=event.target.closest('button');if(!button)return;try{
 if(button.dataset.readingFlowLayout)await setReadingFlowLayout(button.dataset.readingFlowLayout);
 if(button.hasAttribute('data-toggle-toc'))queueMicrotask(()=>{readingFlow.sidebarOpen=!$('.reading-workspace')?.classList.contains('toc-collapsed');});
 if(button.id==='viewer-more-actions')showViewerCommands();if(button.hasAttribute('data-viewer-command')){const command=readingFlow.commands[Number(button.dataset.viewerCommand)];closeViewerCommands();command?.click();}
}catch(error){fail(error);}});
document.addEventListener('pointerdown',event=>{if(!event.target.closest('.viewer-overflow-menu,#viewer-more-actions'))closeViewerCommands();},true);
document.addEventListener('keydown',event=>{const menu=viewer.querySelector('.viewer-overflow-menu');if(!menu)return;if(event.key==='Escape'){event.preventDefault();event.stopImmediatePropagation();closeViewerCommands();$('#viewer-more-actions').focus();return;}if(!['ArrowDown','ArrowUp','Home','End'].includes(event.key)||!menu.contains(event.target))return;event.preventDefault();const buttons=[...menu.querySelectorAll('button:not(:disabled)')],at=buttons.indexOf(document.activeElement),next=event.key==='Home'?0:event.key==='End'?buttons.length-1:(at+(event.key==='ArrowDown'?1:-1)+buttons.length)%buttons.length;buttons[next]?.focus();},true);
const cleanupBeforeReadingFlow=cleanupFeatures;cleanupFeatures=function(){cancelReadingPage();closeViewerCommands();clearTimeout(readingFlow.layoutTimer);readingFlow.bookId=null;readingFlow.scroller=null;viewer.classList.remove('book-quiet');cleanupBeforeReadingFlow();};
scheduleViewerActions();

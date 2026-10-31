'use strict';
// Scoped, interruptible WAAPI motion. No framework or global list observer is required.
const CalmMotion=(()=>{
 const active=new Map(),choices=new Map(),presence=new WeakMap(),contentGhosts=new WeakMap(),ease=kokMotion.ease;
 const groupSelector='.reading-flow-choices,.settings-tabs,.reading-layout-choices,.reading-side-tabs,.note-context-tabs';
 const allowed=()=>!reducedMotion()&&!document.hidden&&!document.body.classList.contains('window-interacting')&&!(typeof isWindowInteracting==='function'&&isWindowInteracting())&&!viewer.classList.contains('opening-preview');
 function play(element,keyframes,{duration=170,channel='surface',pseudoElement,persist=false}={}){
  if(!element?.isConnected)return;let channels=active.get(element);const previous=channels?.get(channel);previous?.cancel();if(!allowed())return;
  const animation=element.animate(keyframes,{duration,easing:ease,fill:'both',...(pseudoElement?{pseudoElement}:{})});if(!channels){channels=new Map();active.set(element,channels);}channels.set(channel,animation);
  animation.finished.catch(()=>{}).finally(()=>{if(channels.get(channel)===animation){channels.delete(channel);if(!channels.size)active.delete(element);}if(!persist)animation.cancel();});return animation;
 }
 function finishAll(){for(const channels of [...active.values()])for(const animation of [...channels.values()])try{animation.finish();}catch{animation.cancel();}}
 function enter(element,kind='menu'){
  if(!element)return;const side=kind==='drawer',running=active.get(element)?.has('surface'),style=running?getComputedStyle(element):null;return play(element,[{opacity:style?.opacity??0,transform:style?.transform??(side?'translateX(10px)':'translateY(-4px)')},{opacity:1,transform:'none'}],{duration:side?180:140});
 }
 function exit(element,remove=false){
  if(!element)return Promise.resolve();const style=getComputedStyle(element),animation=play(element,[{opacity:style.opacity,transform:'none'},{opacity:0,transform:'none'}],{duration:100,persist:true});
  return(animation?.finished.catch(()=>{})||Promise.resolve()).then(()=>{if(remove)element.remove();animation?.cancel();});
 }
 function visibility(element,visible){
  const running=active.get(element)?.get('surface');
  if(presence.get(element)?.visible===visible&&element.inert===!visible&&(visible?!element.classList.contains('hidden'):element.classList.contains('hidden')||running))return running?.finished.catch(()=>{})||Promise.resolve();
  const revision=(presence.get(element)?.revision||0)+1;presence.set(element,{revision,visible});element.inert=!visible;element.setAttribute('aria-hidden',String(!visible));
  if(visible){element.classList.remove('hidden');const animation=enter(element,'drawer');return animation?.finished.catch(()=>{})||Promise.resolve();}
  const style=getComputedStyle(element),animation=play(element,[{opacity:style.opacity,transform:'none'},{opacity:0,transform:'none'}],{duration:130,persist:true});return(animation?.finished.catch(()=>{})||Promise.resolve()).then(()=>{if(presence.get(element)?.revision===revision)element.classList.add('hidden');animation?.cancel();});
 }
 function snapshot(element){
  const host=element.parentElement;contentGhosts.get(host)?.remove();const ghost=element.cloneNode(true),style=getComputedStyle(element);ghost.removeAttribute('id');ghost.querySelectorAll('[id]').forEach(n=>n.removeAttribute('id'));ghost.classList.add('motion-content-ghost');ghost.inert=true;ghost.setAttribute('aria-hidden','true');
  Object.assign(ghost.style,{position:'absolute',left:element.offsetLeft+'px',top:element.offsetTop+'px',width:element.offsetWidth+'px',height:element.offsetHeight+'px',margin:'0',padding:style.padding,boxSizing:style.boxSizing,font:style.font,overflow:'hidden',opacity:style.opacity,transform:style.transform,pointerEvents:'none',zIndex:'3'});
  host.append(ghost);ghost.scrollTop=element.scrollTop;contentGhosts.set(host,ghost);return ghost;
 }
 function dismissSnapshot(ghost,direction){
  const leaving=play(ghost,[{opacity:getComputedStyle(ghost).opacity,transform:getComputedStyle(ghost).transform},{opacity:0,transform:`translateX(${-direction*5}px)`}],{duration:100,persist:true});
  const host=ghost.parentElement;(leaving?.finished.catch(()=>{})||Promise.resolve()).then(()=>{ghost.remove();leaving?.cancel();if(contentGhosts.get(host)===ghost)contentGhosts.delete(host);});
 }
 function switchPanels(previous,next,change,direction=1){
  if(previous===next)return;
  if(!previous||!allowed()){change();return;}
  const ghost=snapshot(previous);change();dismissSnapshot(ghost,direction);play(next,[{opacity:.65,transform:'none'},{opacity:1,transform:'none'}],{duration:180,channel:'content'});
 }
 function replaceContent(element,html,{direction=1,animate=true}={}){
  if(!animate||!allowed()||!element.children.length){contentGhosts.get(element.parentElement)?.remove();element.innerHTML=html;return;}
  const ghost=snapshot(element);element.innerHTML=html;element.scrollTop=0;dismissSnapshot(ghost,direction);
  play(element,[{opacity:.65,transform:'none'},{opacity:1,transform:'none'}],{duration:180,channel:'content'});
 }
 function toggleNotes(){
  const panel=$('#notes-panel'),visible=!viewer.classList.contains('notes-open');viewer.classList.toggle('notes-open',visible);$('#notes-button').setAttribute('aria-expanded',String(visible));visibility(panel,visible);if(typeof renderAnnotationIndex==='function')renderAnnotationIndex();
 }
 function feedback(button){const icon=button?.querySelector('svg');if(icon)play(icon,[{transform:'scale(.9)',opacity:.8},{transform:'none',opacity:1}],{duration:150,channel:'feedback'});}
 function updateChoice(group,animate=true){
  const state=choices.get(group);if(!state||!group.isConnected)return;const selected=group.querySelector('button[aria-pressed=true],button[aria-selected=true]');if(!selected||!group.clientWidth||!selected.offsetWidth)return;group.classList.add('motion-choice-ready');
  const underline=group.classList.contains('settings-tabs'),rect={x:selected.offsetLeft,y:selected.offsetTop+(underline?selected.offsetHeight-2:0),w:selected.offsetWidth,h:underline?2:selected.offsetHeight},key=[rect.x,rect.y,rect.w,rect.h].join('|');if(state.key===key)return;
  const old=getComputedStyle(group,'::before'),matrix=new DOMMatrixReadOnly(old.transform==='none'?undefined:old.transform),oldW=parseFloat(old.width)||rect.w,oldH=parseFloat(old.height)||rect.h,from=`translate(${matrix.e}px,${matrix.f}px) scale(${matrix.a*oldW/rect.w},${matrix.d*oldH/rect.h})`,ready=!!state.key;
  state.key=key;for(const [key,value]of [['x',rect.x],['y',rect.y],['w',rect.w],['h',rect.h]])group.style.setProperty('--motion-choice-'+key,value+'px');group.classList.add('motion-choice-ready');
  if(ready&&animate&&allowed())play(group,[{transform:from},{transform:`translate(${rect.x}px,${rect.y}px) scale(1)`}],{duration:190,channel:'choice',pseudoElement:'::before'});else try{active.get(group)?.get('choice')?.finish();}catch{}
 }
 function prepare(root=document){
  for(const [group,state]of choices)if(!group.isConnected){state.mutations.disconnect();state.resize.disconnect();cancelAnimationFrame(state.frame);choices.delete(group);}
  for(const group of root.querySelectorAll(groupSelector))if(!choices.has(group)){
   const state={key:'',frame:0},schedule=animate=>{cancelAnimationFrame(state.frame);state.frame=requestAnimationFrame(()=>updateChoice(group,animate));};
   state.mutations=new MutationObserver(()=>schedule(true));state.mutations.observe(group,{subtree:true,attributes:true,attributeFilter:['aria-selected','aria-pressed']});state.resize=new ResizeObserver(()=>schedule(false));state.resize.observe(group);choices.set(group,state);updateChoice(group,false);
  }
 }
 function refresh(){prepare();for(const group of choices.keys())updateChoice(group,false);}
 const query=matchMedia('(prefers-reduced-motion: reduce)');query.addEventListener('change',()=>{finishAll();refresh();});document.addEventListener('visibilitychange',()=>{if(document.hidden)finishAll();});
 return{allowed,play,enter,exit,visibility,replaceContent,switchPanels,toggleNotes,feedback,prepare,refresh,finishAll,get activeCount(){return[...active.values()].reduce((sum,channels)=>sum+channels.size,0);}};
})();

Kok.extend('core','calm-motion.js:animateBeforeCalm','animateIn',animateBeforeCalm=>function(element){
 if(element===viewer||element?.tagName==='DIALOG')return animateBeforeCalm(element);
 if(element?.matches('.glass-menu,.viewer-overflow-menu,.import-menu'))return CalmMotion.enter(element);
 if(element?.matches('.file-assistant,.translation-panel,#notes-panel'))return CalmMotion.enter(element,'drawer');
 if(element?.matches('.settings-section,.reading-side-pane'))return CalmMotion.play(element,[{opacity:.8},{opacity:1}],{duration:150});
 if(!CalmMotion.allowed())return;return animateBeforeCalm(element);
});
Kok.extend('core','calm-motion.js:showSmallBeforeCalm','showSmall',showSmallBeforeCalm=>function(content){showSmallBeforeCalm(content);requestAnimationFrame(()=>CalmMotion.prepare($('#small-dialog')));});
Kok.extend('core','calm-motion.js:renderReadingBeforeCalm','renderReadingWorkspace',renderReadingBeforeCalm=>function(position=0){renderReadingBeforeCalm(position);CalmMotion.prepare(viewer);});
Kok.extend('core','calm-motion.js:readingTabBeforeCalm','setReadingTab',readingTabBeforeCalm=>function(tab,open=true){const previous=ensureReadingToolState().tab;readingTabBeforeCalm(tab,open);CalmMotion.prepare(viewer);if(open&&tab!==previous){const pane=viewer.querySelector(`[data-reading-pane="${tab}"]`);CalmMotion.play(pane,[{opacity:.82},{opacity:1}],{duration:140});}});
Kok.extend('core','calm-motion.js:favoriteBeforeCalm','favorite',favoriteBeforeCalm=>async function(id){await favoriteBeforeCalm(id);const button=state.viewer?.id===id?$('#viewer-favorite'):document.querySelector(`[data-favorite="${CSS.escape(id)}"]`);CalmMotion.feedback(button);});
Kok.extend('core','calm-motion.js:translationBeforeCalm','showTranslationPanel',translationBeforeCalm=>function(){const wasOpen=viewer.classList.contains('translation-open');translationBeforeCalm();if(!wasOpen)CalmMotion.enter($('#translation-panel'),'drawer');});
CalmMotion.prepare();

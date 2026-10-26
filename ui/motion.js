'use strict';
// Standard critically damped response, sampled for native WAAPI timing.
// Motion's public spring generator and Fluent 2 informed the timing policy;
// no animation runtime is bundled.
const kokSpringEase=(()=>{const settle=1-9*Math.exp(-8),samples=Array.from({length:41},(_,n)=>{const t=n/40;return((1-(1+8*t)*Math.exp(-8*t))/settle).toFixed(5);});const value='linear('+samples.join(',')+')';return CSS.supports('animation-timing-function',value)?value:'cubic-bezier(.2,.8,.2,1)';})();
const kokMotion={enter:340,panel:260,feedback:200,exit:150,ease:kokSpringEase};
animateIn=function(element){
 if(!element||reducedMotion())return;const previous=elementMotions.get(element),interrupted=previous?.playState==='running',computed=interrupted?getComputedStyle(element):null,from=computed?{opacity:computed.opacity,transform:computed.transform}:{opacity:0,transform:element.tagName==='DIALOG'?'translateY(10px) scale(.992)':'translateY(5px)'};previous?.cancel();
 const animation=element.animate([from,{opacity:1,transform:'none'}],{duration:element.tagName==='DIALOG'?kokMotion.enter:kokMotion.panel,easing:kokMotion.ease});elementMotions.set(element,animation);animation.finished.catch(()=>{}).finally(()=>{if(elementMotions.get(element)===animation)elementMotions.delete(element);});return animation;
};
animateOut=async function(element){
 if(!element||reducedMotion())return;const style=getComputedStyle(element),from={opacity:style.opacity,transform:style.transform};elementMotions.get(element)?.cancel();const animation=element.animate([from,{opacity:0,transform:element.tagName==='DIALOG'?'translateY(6px) scale(.995)':'translateY(3px)'}],{duration:kokMotion.exit,easing:'cubic-bezier(.3,0,.7,1)',fill:'forwards'});elementMotions.set(element,animation);try{await animation.finished;}catch{}finally{if(elementMotions.get(element)===animation)elementMotions.delete(element);animation.cancel();}
};
let focusMotionAt=0,previewMoving=false;
window.chrome?.webview?.addEventListener('message',event=>{if(event.data.eventName!=='workspaceFocus'||reducedMotion()||viewer.classList.contains('opening-preview')||typeof isWindowInteracting==='function'&&isWindowInteracting()||performance.now()-focusMotionAt<250||getSelection()?.toString()||$('#small-dialog').open)return;focusMotionAt=performance.now();const surface=document.body.classList.contains('preview-window')?$('#viewer-content'):$('#main');if(surface?.isConnected){elementMotions.get(surface)?.cancel();const motion=surface.animate([{opacity:.86,transform:'translateY(2px)'},{opacity:1,transform:'none'}],{duration:200,easing:kokMotion.ease});elementMotions.set(surface,motion);motion.finished.catch(()=>{}).finally(()=>{if(elementMotions.get(surface)===motion)elementMotions.delete(surface);});}});
const nativeBeforeMotion=native;
native=function(action,args={},objects){
 if(document.body.classList.contains('preview-window')&&['sendPreviewBehind','minimizePreview','closePreview'].includes(action)&&!previewMoving&&!reducedMotion()){
  const surface=viewer;previewMoving=true;return(async()=>{const animation=surface.animate([{opacity:1,transform:'none'},{opacity:.84,transform:'scale(.994) translateY(2px)'}],{duration:130,easing:'cubic-bezier(.3,0,.7,1)',fill:'forwards'});try{await animation.finished.catch(()=>{});return await nativeBeforeMotion(action,args,objects);}finally{animation.cancel();previewMoving=false;}})();
 }return nativeBeforeMotion(action,args,objects);
};

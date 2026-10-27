'use strict';
const elementMotions=new WeakMap();
const motionPreference=matchMedia('(prefers-reduced-motion: reduce)');
const reducedMotion=()=>motionPreference.matches;
const surfaceMotions=new Set();
function trackSurfaceMotion(animation){surfaceMotions.add(animation);animation.finished.catch(()=>{}).finally(()=>surfaceMotions.delete(animation));return animation;}
function finishSurfaceMotions(){for(const animation of [...surfaceMotions])try{animation.finish();}catch{animation.cancel();}}
motionPreference.addEventListener('change',()=>{if(reducedMotion())finishSurfaceMotions();});
document.addEventListener('visibilitychange',()=>{if(document.hidden)finishSurfaceMotions();});
// Consistent surface timing, continuous interruptions and held exit frames.
// Dialog text is translated/faded rather than rescaled.
const kokMotion={enter:230,panel:180,feedback:150,exit:130,ease:'cubic-bezier(.22,1,.36,1)',exitEase:'cubic-bezier(.4,0,1,1)'};
const dialogBackdrops=new WeakMap();
function animateDialogBackdrop(element,visible){
 if(element?.tagName!=='DIALOG'||!element.open)return;
 const previous=dialogBackdrops.get(element),opacity=previous?getComputedStyle(element,'::backdrop').opacity:visible?0:1;previous?.cancel();
 if(reducedMotion())return;
 const animation=trackSurfaceMotion(element.animate([{opacity},{opacity:visible?1:0}],{pseudoElement:'::backdrop',duration:visible?180:kokMotion.exit,easing:'linear',fill:visible?'backwards':'forwards'}));dialogBackdrops.set(element,animation);
 animation.finished.catch(()=>{}).finally(()=>{if(visible&&dialogBackdrops.get(element)===animation){dialogBackdrops.delete(element);animation.cancel();}});
}
function animateIn(element){
 if(!element)return;const previous=elementMotions.get(element),interrupted=previous&&['running','paused'].includes(previous.playState),computed=interrupted?getComputedStyle(element):null,from=computed?{opacity:computed.opacity,transform:computed.transform}:{opacity:0,transform:element.tagName==='DIALOG'?'translateY(10px)':'translateY(5px)'};previous?.cancel();
 animateDialogBackdrop(element,true);if(reducedMotion())return;
 const animation=trackSurfaceMotion(element.animate([from,{opacity:1,transform:'none'}],{duration:element.tagName==='DIALOG'?kokMotion.enter:kokMotion.panel,easing:kokMotion.ease,fill:'both'}));elementMotions.set(element,animation);animation.finished.catch(()=>{}).finally(()=>{if(elementMotions.get(element)===animation){elementMotions.delete(element);animation.cancel();}});return animation;
};
async function animateOut(element){
 if(!element)return;const style=getComputedStyle(element),from={opacity:style.opacity,transform:style.transform};elementMotions.get(element)?.cancel();animateDialogBackdrop(element,false);if(reducedMotion())return;
 const animation=trackSurfaceMotion(element.animate([from,{opacity:0,transform:element.tagName==='DIALOG'?'translateY(6px)':'translateY(3px)'}],{duration:kokMotion.exit,easing:kokMotion.exitEase,fill:'forwards'}));elementMotions.set(element,animation);try{await animation.finished;}catch{}
 // Hold the invisible exit frame until the caller closes/removes the surface.
 // An interrupted entrance samples this frame before cancelling it.
};
document.addEventListener('close',event=>{const d=event.target;if(d.tagName!=='DIALOG'||d.open)return;elementMotions.get(d)?.cancel();elementMotions.delete(d);dialogBackdrops.get(d)?.cancel();dialogBackdrops.delete(d);},true);

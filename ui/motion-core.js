'use strict';
// A bounded time-to-state function, inspired by Remotion's interpolation model.
// The app keeps browser-native animation; no video renderer/runtime is bundled.
const MotionClock={
 progress(now,start,duration){return Number.isFinite(now)&&Number.isFinite(start)&&duration>0?Math.max(0,Math.min(1,(now-start)/duration)):1;},
 ease(t){return 1-Math.pow(1-Math.max(0,Math.min(1,t)),3);},
 settle(t){const p=Math.max(0,Math.min(1,t));return (1-(1+8*p)*Math.exp(-8*p))/(1-9*Math.exp(-8));},
 curve(){const points=Array.from({length:33},(_,n)=>MotionClock.settle(n/32).toFixed(5));const value='linear('+points.join(',')+')';return CSS.supports('animation-timing-function',value)?value:'cubic-bezier(.22,1,.36,1)';},
 spring(value,target,velocity,dt,omega=28){const elapsed=Math.max(0,Math.min(1,dt)),d=value-target,term=velocity+omega*d,decay=Math.exp(-omega*elapsed);return[target+(d+term*elapsed)*decay,(velocity-omega*term*elapsed)*decay];}
};
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
const kokMotion={enter:180,panel:140,feedback:120,exit:100,ease:'cubic-bezier(.2,0,0,1)',exitEase:'cubic-bezier(.4,0,1,1)'};
const dialogBackdrops=new WeakMap();
function animateDialogBackdrop(element,visible){
 if(element?.tagName!=='DIALOG'||!element.open)return;
 const previous=dialogBackdrops.get(element),opacity=previous?getComputedStyle(element,'::backdrop').opacity:visible?0:1;previous?.cancel();
 if(reducedMotion())return;
 const animation=trackSurfaceMotion(element.animate([{opacity},{opacity:visible?1:0}],{pseudoElement:'::backdrop',duration:visible?180:kokMotion.exit,easing:'linear',fill:visible?'backwards':'forwards'}));dialogBackdrops.set(element,animation);
 animation.finished.catch(()=>{}).finally(()=>{if(visible&&dialogBackdrops.get(element)===animation){dialogBackdrops.delete(element);animation.cancel();}});
}
function animateIn(...args){return Kok.invoke('animateIn',this,args);}
Kok.define('animateIn',function(element){
 if(!element)return;const previous=elementMotions.get(element),interrupted=previous&&['running','paused'].includes(previous.playState),computed=interrupted?getComputedStyle(element):null,from={opacity:computed?.opacity??.35,transform:'none'};previous?.cancel();
 animateDialogBackdrop(element,true);if(reducedMotion())return;
 const animation=trackSurfaceMotion(element.animate([from,{opacity:1,transform:'none'}],{duration:element.tagName==='DIALOG'?kokMotion.enter:kokMotion.panel,easing:kokMotion.ease,fill:'both'}));elementMotions.set(element,animation);animation.finished.catch(()=>{}).finally(()=>{if(elementMotions.get(element)===animation){elementMotions.delete(element);animation.cancel();}});return animation;
});;
async function animateOut(element){
 if(!element)return;const style=getComputedStyle(element),from={opacity:style.opacity,transform:style.transform};elementMotions.get(element)?.cancel();animateDialogBackdrop(element,false);if(reducedMotion())return;
 const animation=trackSurfaceMotion(element.animate([from,{opacity:0,transform:'none'}],{duration:kokMotion.exit,easing:kokMotion.exitEase,fill:'forwards'}));elementMotions.set(element,animation);try{await animation.finished;}catch{}
 // Hold the invisible exit frame until the caller closes/removes the surface.
 // An interrupted entrance samples this frame before cancelling it.
};
document.addEventListener('close',event=>{const d=event.target;if(d.tagName!=='DIALOG'||d.open)return;elementMotions.get(d)?.cancel();elementMotions.delete(d);dialogBackdrops.get(d)?.cancel();dialogBackdrops.delete(d);},true);

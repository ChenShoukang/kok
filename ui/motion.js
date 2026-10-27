'use strict';
// Native preview handoff only. Surface animation lives in motion-core.js.
let previewMoving=false;
const nativeBeforeMotion=native;
native=function(action,args={},objects){
 if(document.body.classList.contains('preview-window')&&['sendPreviewBehind','minimizePreview','closePreview'].includes(action)&&!previewMoving&&!reducedMotion()){
  const surface=viewer;previewMoving=true;return(async()=>{const animation=surface.animate([{opacity:1,transform:'none'},{opacity:.84,transform:'scale(.994) translateY(2px)'}],{duration:130,easing:'cubic-bezier(.3,0,.7,1)',fill:'forwards'});try{await animation.finished.catch(()=>{});return await nativeBeforeMotion(action,args,objects);}finally{animation.cancel();previewMoving=false;}})();
 }return nativeBeforeMotion(action,args,objects);
};

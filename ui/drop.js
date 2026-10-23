'use strict';
(()=>{
 const overlay=$('#file-drop-overlay');let depth=0,closing=0,dropping=false,motion=null,leaving=false;
 const hasFiles=event=>Array.from(event.dataTransfer?.types||[]).includes('Files');
 function transition(from,to,duration){
   motion?.cancel();motion=null;
   if(reducedMotion())return Promise.resolve();
   const animation=overlay.animate([from,to],{duration,easing:'cubic-bezier(.16,1,.3,1)',fill:'forwards'});motion=animation;
   return animation.finished.catch(()=>{}).finally(()=>{if(motion===animation){animation.cancel();motion=null;}});
 }
 const appearance=()=>{const style=getComputedStyle(overlay);return{opacity:style.opacity,transform:style.transform};};
 function show(){
   closing++;const open=overlay.matches(':popover-open');if(open&&!leaving)return;
   const from=open?appearance():{opacity:0,transform:'translateY(8px)'};leaving=false;
   if(!open)overlay.showPopover();
   transition(from,{opacity:1,transform:'translateY(0)'},380);I18n.localize(overlay);
 }
 async function hide(){
   depth=0;const token=++closing;
   if(overlay.matches(':popover-open')){leaving=true;await transition(appearance(),{opacity:0,transform:'translateY(5px)'},200);if(token===closing&&overlay.matches(':popover-open')){overlay.hidePopover();leaving=false;}}
 }
 document.addEventListener('dragenter',event=>{
   if(!hasFiles(event))return;event.preventDefault();depth++;show();
 },true);
 document.addEventListener('dragover',event=>{
   if(!hasFiles(event))return;event.preventDefault();event.dataTransfer.dropEffect=dropping?'none':'copy';
 },true);
 document.addEventListener('dragleave',event=>{
   if(!hasFiles(event)&&depth===0)return;
   const outside=!event.relatedTarget&&(event.clientX<=0||event.clientY<=0||event.clientX>=innerWidth||event.clientY>=innerHeight);
   if(--depth<=0||outside)hide();
 },true);
 document.addEventListener('drop',async event=>{
   if(!hasFiles(event))return;event.preventDefault();event.stopPropagation();hide();
   if(dropping){toast('正在导入，请稍候。');return;}
   const files=Array.from(event.dataTransfer.files);if(!files.length)return;
   if(typeof window.chrome?.webview?.postMessageWithAdditionalObjects!=='function'){toast('当前运行环境不支持拖入，请更新 WebView2，或点击导入文件。',true);return;}
   dropping=true;
   try{
     const result=await importItems('importDropped',files);
     if(result?.added&&!viewer.open&&!$('#small-dialog').open)navigate('overview');
   }catch(error){fail(error);}finally{dropping=false;}
 },true);
 window.addEventListener('blur',hide);
 document.addEventListener('keydown',event=>{if(event.key==='Escape')hide();});
 document.addEventListener('visibilitychange',()=>{if(document.hidden)hide();});
})();

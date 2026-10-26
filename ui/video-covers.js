'use strict';
// One paused, muted decoder at a time. Only small JPEGs cross the native bridge.
const VideoCovers=(()=>{
 const attempted=new Set();let current=null,timer=null;
 const busy=()=>viewer.open||!!window.kokPreviewWindowCount||$('#creative-dialog')?.open||$('#small-dialog').open||$('#app-mode-dialog')?.open||document.body.classList.contains('private-locking')||!audio.paused||document.hidden;
 function waitFor(clip,event,signal,timeout=4500){
  return new Promise((resolve,reject)=>{
   let timer;const clean=()=>{clearTimeout(timer);clip.removeEventListener(event,ready);clip.removeEventListener('error',failed);signal.removeEventListener('abort',abort);};
   const ready=()=>{clean();resolve();},failed=()=>{clean();reject(new Error('thumbnail decode unavailable'));},abort=()=>{clean();reject(new DOMException('Aborted','AbortError'));};
   clip.addEventListener(event,ready,{once:true});clip.addEventListener('error',failed,{once:true});signal.addEventListener('abort',abort,{once:true});timer=setTimeout(failed,timeout);if(signal.aborted)abort();
  });
 }
 async function extract(item,signal){
  const clip=document.createElement('video');clip.className='cover-decoder';clip.crossOrigin='anonymous';clip.muted=true;clip.defaultMuted=true;clip.volume=0;clip.preload='metadata';clip.playsInline=true;clip.setAttribute('aria-hidden','true');clip.setAttribute('inert','');document.body.append(clip);
  try{
   const metadata=waitFor(clip,'loadedmetadata',signal,6500);clip.src=media(item);clip.load();await metadata;
   if(!clip.videoWidth||!clip.videoHeight||!Number.isFinite(clip.duration)||clip.duration<=0)return null;
   const scale=Math.min(1,480/clip.videoWidth,960/clip.videoHeight),canvas=document.createElement('canvas');canvas.width=Math.max(1,Math.round(clip.videoWidth*scale));canvas.height=Math.max(1,Math.round(clip.videoHeight*scale));const context=canvas.getContext('2d',{willReadFrequently:true});let fallback=null;
   for(const fraction of [.08,.35,.65]){
    if(signal.aborted)throw new DOMException('Aborted','AbortError');
    const target=Math.min(Math.max(.04,clip.duration*fraction),30,Math.max(0,clip.duration-.05));
    if(Math.abs(clip.currentTime-target)>.001){const sought=waitFor(clip,'seeked',signal);clip.currentTime=target;await sought;}
    if(clip.readyState<2)await waitFor(clip,'loadeddata',signal);
    context.drawImage(clip,0,0,canvas.width,canvas.height);const data=context.getImageData(0,0,canvas.width,canvas.height).data;let light=0,samples=0;
    for(let n=0;n<data.length;n+=Math.max(4,Math.floor(data.length/2048/4)*4)){light+=(data[n]+data[n+1]+data[n+2])/3;samples++;}
    const image=canvas.toDataURL('image/jpeg',.82);fallback=image;if(light/Math.max(1,samples)>14)return image;
   }
   return fallback;
  }finally{clip.pause();clip.removeAttribute('src');clip.load();clip.remove();}
 }
 function update(item){document.querySelectorAll(`.file-card[data-id="${item.id}"] .video-art`).forEach(node=>{node.outerHTML=cover(item);});}
 async function pump(){
  timer=null;if(current||busy())return;
  const item=state.items.find(entry=>entry.kind==='video'&&!entry.hasCover&&!attempted.has(entry.id));if(!item)return;
  const controller=new AbortController();current={id:item.id,controller};attempted.add(item.id);
  try{
   const image=await extract(item,controller.signal);if(image&&!controller.signal.aborted&&byId(item.id)){
    const result=await native('saveVideoCover',{id:item.id,image});if(result?.saved){Object.assign(item,result);const visible=byId(item.id);if(visible)Object.assign(visible,result);update(item);}
   }
  }catch(error){if(error.name==='AbortError')attempted.delete(item.id);}
  finally{current=null;schedule();}
 }
 function schedule(){clearTimeout(timer);if(busy()){current?.controller.abort();return;}timer=setTimeout(pump,350);}
 new MutationObserver(schedule).observe(viewer,{attributes:true,attributeFilter:['open']});new MutationObserver(schedule).observe($('#small-dialog'),{attributes:true,attributeFilter:['open']});
 document.addEventListener('close',schedule,true);audio.addEventListener('play',schedule);audio.addEventListener('pause',schedule);document.addEventListener('visibilitychange',schedule);I18n.ready.then(schedule);
 return{schedule,cancel(){current?.controller.abort();clearTimeout(timer);},get active(){return current?.id||null;}};
})();
function scheduleVideoCovers(){VideoCovers.schedule();}

'use strict';
// Independent native windows reuse the existing reader/editor and local bridge.
(() => {
 const params=new URLSearchParams(location.search),previewId=params.get('preview');
 let preferencesTimer=0;
 const syncPreferences=()=>{clearTimeout(preferencesTimer);preferencesTimer=setTimeout(async()=>{try{const db=await native('library');state.settings=db.settings;applyTheme();applyAppMode();I18n.applyLanguage(state.settings.uiLanguage||'zh');await applyFont();}catch(error){fail(error);}},120);};
 window.chrome?.webview?.addEventListener('message',event=>{if(event.data.eventName==='previewPreferencesChanged')syncPreferences();});
 if(!previewId){
  let windows=[];
  const strip=document.createElement('nav');strip.className='preview-window-strip hidden';strip.setAttribute('aria-label','打开的窗口');document.body.append(strip);
  const renderWindows=()=>{window.kokPreviewWindowCount=windows.length;VideoCovers.schedule();strip.classList.toggle('hidden',!windows.length);strip.innerHTML=windows.map(w=>`<button class="preview-window-tab ${w.behind?'is-behind':''}" data-preview-focus="${esc(w.id)}" title="${esc(w.name)}"><span>${svg('external')}</span><span data-user-content>${esc(w.name)}</span></button>`).join('');I18n.localize(strip);};
  strip.addEventListener('click',event=>{const button=event.target.closest('[data-preview-focus]');if(button)native('focusPreview',{id:button.dataset.previewFocus}).catch(fail);});
  window.chrome?.webview?.addEventListener('message',event=>{if(event.data.eventName==='previewWindowsChanged'){windows=event.data.data||[];renderWindows();}});
  window.openPreviewWindow=async(id,{play=true}={})=>{if($('#small-dialog').open)await closeSmall();const item=byId(id);if(item?.kind==='video')audio.pause();return native('openPreview',{id,play});};
  document.addEventListener('click',event=>{
   const button=event.target.closest('[data-open]');if(!button||!['photo','video','document','book'].includes(byId(button.dataset.open)?.kind))return;
   event.preventDefault();event.stopImmediatePropagation();const item=byId(button.dataset.open);(item.ownedDocument&&!state.isPrivate?openCreativeEditor(item.id):openPreviewWindow(item.id)).catch(fail);
  },true);
  const detach=document.createElement('button');detach.id='detach-viewer';detach.className='icon-button';detach.title='在独立窗口打开';detach.setAttribute('aria-label','在独立窗口打开');detach.innerHTML=svg('pip');
  $('.viewer-actions').prepend(detach);detach.onclick=async()=>{try{const id=state.viewer?.id;if(!id)return;await closeViewer();await openPreviewWindow(id,{play:false});}catch(error){fail(error);}};
  native('previewWindows').then(data=>{windows=data;renderWindows();}).catch(()=>{});return;
 }
 document.body.classList.add('preview-window');
 const controls=[['sendPreviewBehind','退到主窗口后面','layers'],['minimizePreview','最小化','minus'],['maximizePreview','最大化','expand']];
 paths.minus='M5 12h14';
 for(const [action,label,icon]of controls){const button=document.createElement('button');button.className='icon-button preview-native-control';button.setAttribute('aria-label',label);button.title=label;button.innerHTML=svg(icon);button.dataset.previewAction=action;$('.viewer-actions').insertBefore(button,$('#close-viewer'));}
 closeViewer=()=>native('closePreview');
 document.addEventListener('click',event=>{const button=event.target.closest('[data-preview-action]');if(button)native(button.dataset.previewAction).catch(fail);});
 $('.viewer-header').addEventListener('pointerdown',event=>{if(!window.kokNativeDragSupported&&event.button===0&&!event.target.closest('button,input,a'))native(event.detail===2?'maximizePreview':'dragPreview').catch(fail);});
 const blankAreas='.photo-image-wrap,.reader-scroll,.reading-sheet,.reader-text,.pdf-scroll,#viewer-content,.media-stage,.video-canvas,.music-workspace';
 viewer.addEventListener('dblclick',event=>{
  if(document.fullscreenElement||event.defaultPrevented||event.target.closest('button,a,input,textarea,select,[contenteditable],.mark-popover,.photo-editor,.textLayer,.pdf-page,.reader-tools,.reader-toc,.photo-tools,.video-controls'))return;
  if(!event.target.matches(blankAreas))return;
  if(event.target.matches('.reader-text')&&event.target.childNodes.length&&[...event.target.childNodes].some(n=>n.nodeType===Node.TEXT_NODE&&n.textContent.trim()))return;
  event.preventDefault();event.stopPropagation();native('sendPreviewBehind').catch(fail);
 },true);
 // Prevent adaptive inline-dialog dimensions from shrinking the native viewport.
 new MutationObserver(()=>{if(state.viewer){const id=state.viewer.id;if(id!==window.kokPreviewItemId){window.kokPreviewItemId=id;native('previewItemChanged',{id}).catch(fail);}}}).observe($('#viewer-title'),{childList:true,subtree:true,characterData:true});
 let launched=false;
 const launch=async()=>{if(launched||document.body.classList.contains('booting')||document.body.classList.contains('mode-pending'))return;launched=true;try{await openItem(previewId,{play:params.get('play')!=='0'});await document.fonts.ready;const image=$('#photo-image');if(image&&!image.complete)await Promise.race([image.decode().catch(()=>{}),new Promise(resolve=>setTimeout(resolve,1500))]);const opening=window.kokBeginOriginOpen?.();await new Promise(resolve=>requestAnimationFrame(resolve));const presented=native('previewReady',{reduceMotion:reducedMotion()});if(opening?.playState==='paused')opening.play();await presented;}catch(error){await native('previewReady').catch(()=>{});fail(error);}};
 new MutationObserver(launch).observe(document.body,{attributes:true,attributeFilter:['class']});
 document.addEventListener('DOMContentLoaded',launch,{once:true});
})();

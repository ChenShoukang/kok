'use strict';
function videoPreferences(){
 const s=state.settings;
 return {enabled:s.videoShortcutsEnabled!==false,play:['Space','KeyK','KeyP'].includes(s.videoPlayKey)?s.videoPlayKey:'Space',seek:s.videoSeekKeys==='jl'?'jl':'arrows',seconds:[5,10,15,30,60].includes(Number(s.videoSeekSeconds))?Number(s.videoSeekSeconds):10,volume:s.videoVolumeKeys!==false,doubleClick:s.videoDoubleClick!==false};
}
function videoPreferencesMarkup(){
 const p=videoPreferences();
 return `<div class="video-preferences"><label class="preference-check"><input type="checkbox" data-video-pref="videoShortcutsEnabled" ${p.enabled?'checked':''}>启用视频快捷键</label><label>播放 / 暂停<select data-video-pref="videoPlayKey" aria-label="播放暂停快捷键"><option value="Space">空格键</option><option value="KeyK" data-user-content>K</option><option value="KeyP" data-user-content>P</option></select></label><label>前后跳跃<select data-video-pref="videoSeekKeys" aria-label="跳跃快捷键"><option value="arrows">左右方向键</option><option value="jl" data-user-content>J / L</option></select></label><label>每次跳跃<select data-video-pref="videoSeekSeconds" aria-label="跳跃秒数">${[5,10,15,30,60].map(n=>`<option value="${n}">${n} 秒</option>`).join('')}</select></label><label class="preference-check"><input type="checkbox" data-video-pref="videoVolumeKeys" ${p.volume?'checked':''}>上下方向键调节音量</label><label class="preference-check"><input type="checkbox" data-video-pref="videoDoubleClick" ${p.doubleClick?'checked':''}>双击画面切换全屏</label><p class="preference-note">输入文字或调整滑块时，快捷键不会抢占操作。</p></div>`;
}
function syncVideoPreferences(root=document){
 const p=videoPreferences();
 root.querySelectorAll('[data-video-pref]').forEach(input=>{
   const key=input.dataset.videoPref;
   if(input.type==='checkbox')input.checked=state.settings[key]!==false;
   else input.value=String(key==='videoPlayKey'?p.play:key==='videoSeekKeys'?p.seek:p.seconds);
 });
 document.querySelectorAll('[data-video-skip]').forEach(button=>{
   const direction=Number(button.dataset.videoSkip)<0?-1:1;button.dataset.videoSkip=String(direction*p.seconds);
   const label=(direction<0?'后退 ':'前进 ')+p.seconds+' 秒';button.title=label;button.setAttribute('aria-label',label);button.innerHTML=svg(direction<0?'skipBack':'skipForward');I18n.localize(button);
 });
}
function showVideoPreferences(){
 showSmall(`<h2>视频操作</h2><p class="dialog-description">按你的习惯调整播放快捷键与跳跃距离。</p>${videoPreferencesMarkup()}<button class="button secondary" data-video-pref-reset>恢复默认</button>`);
 syncVideoPreferences($('#small-dialog'));I18n.localize($('#small-dialog'));
}
function animateSettings(){
 const dialog=$('#small-dialog');dialog.classList.add('settings-dialog');
 if(reducedMotion())return;
 dialog.querySelectorAll(':scope>h2,:scope>.setting-row,:scope>.about-details').forEach((row,n)=>row.animate([{opacity:0,transform:'translateY(7px)'},{opacity:1,transform:'translateY(0)'}],{duration:320,delay:Math.min(n,7)*25,easing:'cubic-bezier(.16,1,.3,1)',fill:'backwards'}));
}
document.addEventListener('change',async event=>{
 const input=event.target;if(!input.dataset.videoPref)return;
 try{await setting({[input.dataset.videoPref]:input.type==='checkbox'?input.checked:input.dataset.videoPref==='videoSeekSeconds'?Number(input.value):input.value});syncVideoPreferences();}catch(error){fail(error);}
});
document.addEventListener('click',async event=>{
 const button=event.target.closest('button');if(!button)return;
 if(button.hasAttribute('data-video-preferences'))showVideoPreferences();
 if(button.hasAttribute('data-video-pref-reset'))try{await setting({videoShortcutsEnabled:true,videoPlayKey:'Space',videoSeekKeys:'arrows',videoSeekSeconds:10,videoVolumeKeys:true,videoDoubleClick:true});syncVideoPreferences();}catch(error){fail(error);}
});
document.addEventListener('keydown',event=>{
 if(!video||!viewer.open||$('#small-dialog').open||event.defaultPrevented||event.ctrlKey||event.metaKey||event.altKey||event.target.closest('input,textarea,select,[contenteditable]'))return;
 const p=videoPreferences();if(!p.enabled)return;
 const code=event.code,back=p.seek==='jl'?'KeyJ':'ArrowLeft',forward=p.seek==='jl'?'KeyL':'ArrowRight';
 if(code===p.play){event.preventDefault();if(event.repeat)return;if(video.paused)video.play().catch(fail);else video.pause();}
 else if(code===back||code===forward){event.preventDefault();if(Number.isFinite(video.duration))video.currentTime=Math.max(0,Math.min(video.duration,video.currentTime+(code===forward?p.seconds:-p.seconds)));}
 else if(p.volume&&(code==='ArrowUp'||code==='ArrowDown')){event.preventDefault();video.volume=Math.max(0,Math.min(1,Math.round((video.volume+(code==='ArrowUp'?.05:-.05))*100)/100));setting({videoVolume:video.volume}).catch(fail);}
});

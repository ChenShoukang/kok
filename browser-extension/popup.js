'use strict';
const status=document.querySelector('#status');
chrome.storage.local.get('lastStatus').then(({lastStatus})=>{if(lastStatus)status.textContent=lastStatus.message;});
document.querySelectorAll('[data-mode]').forEach(button=>button.addEventListener('click',async()=>{button.disabled=true;status.textContent='正在保存… / Saving…';try{const result=await chrome.runtime.sendMessage({type:'capture',mode:button.dataset.mode});status.textContent=result.ok?'已保存 / Saved: '+result.name:result.error;}catch(error){status.textContent=error.message;}finally{button.disabled=false;}}));
document.querySelector('#options').addEventListener('click',()=>chrome.runtime.openOptionsPage());

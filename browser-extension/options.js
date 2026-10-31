'use strict';
const form=document.querySelector('form'),status=document.querySelector('#status');
chrome.storage.local.get(['endpoint','token']).then(value=>{form.elements.endpoint.value=value.endpoint||'';form.elements.token.value=value.token||'';});
document.querySelector('#show-key').addEventListener('change',event=>{form.elements.token.type=event.target.checked?'text':'password';});
form.addEventListener('submit',async event=>{event.preventDefault();try{const u=new URL(form.elements.endpoint.value);if(u.protocol!=='http:'||!['127.0.0.1','localhost'].includes(u.hostname)||u.pathname!=='/capture'||u.username||u.password||u.search||u.hash)throw Error('请使用 kok 提供的本机地址 / Use the local address from kok');await chrome.storage.local.set({endpoint:u.href,token:form.elements.token.value.trim()});status.textContent='配对设置已保存 / Pairing settings saved';}catch(error){status.textContent=error.message;}});

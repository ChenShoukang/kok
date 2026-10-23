'use strict';
// Localize application chrome while keeping names, documents, notes and AI output intact.
const I18n=(()=>{
 let language='zh',messages={},patterns=[];const textSources=new WeakMap(),attributeSources=new WeakMap();
 const protectedSelector='[data-user-content],.file-info b,.book-cover,.track-title b,#track-name,#viewer-title,.reader-scroll h2,#reader-text,.reader-toc button,.mark-popover blockquote,#annotation-index b,#annotation-index small,.queue-item span:last-child,.translation-original,.translation-text,.translation-body h3,.pdf-page,#collections-nav .nav-item>span:nth-child(2),#collection-existing option[value]:not([value=""]),code';
 try{language=localStorage.getItem('kok.uiLanguage')==='en'?'en':'zh';}catch{}
 function text(value){
   if(language!=='en')return value;
   const trimmed=value.trim();let translated=messages[trimmed];
   if(translated===undefined)for(const item of patterns){const match=item.regex.exec(trimmed);if(!match)continue;translated=item.replacement.replace(/\$(\d+)/g,(_,index)=>{const group=match[Number(index)]||'';return(item.translate||[]).includes(Number(index))?(messages[group]||group):group;});break;}
   return translated===undefined?value:value.slice(0,value.indexOf(trimmed))+translated+value.slice(value.indexOf(trimmed)+trimmed.length);
 }
 function isProtected(element){return !element?.closest('[data-ui-content]')&&!!element?.closest(protectedSelector);}
 function localize(root=document.body){
   function visit(node){
     if(node.nodeType===Node.TEXT_NODE){
       if(!node.nodeValue.trim()||isProtected(node.parentElement)||node.parentElement?.closest('textarea,script,style,svg'))return;
       let record=textSources.get(node);if(!record||node.nodeValue!==record.last)record={source:node.nodeValue,last:node.nodeValue};
       const next=text(record.source);if(node.parentElement?.tagName==='OPTION'&&!node.parentElement.hasAttribute('value'))node.parentElement.setAttribute('value',record.source.trim());
       record.last=next;textSources.set(node,record);if(node.nodeValue!==next)node.nodeValue=next;return;
     }
     if(node.nodeType!==Node.ELEMENT_NODE||/^(SCRIPT|STYLE|SVG)$/.test(node.tagName))return;
     if(isProtected(node)&&node.tagName!=='TEXTAREA')return;
     let records=attributeSources.get(node);if(!records){records={};attributeSources.set(node,records);}
     for(const name of ['title','aria-label','placeholder']){
       if(!node.hasAttribute(name))continue;const value=node.getAttribute(name);let record=records[name];if(!record||value!==record.last)record={source:value,last:value};const next=text(record.source);record.last=next;records[name]=record;if(next!==value)node.setAttribute(name,next);
     }
     if(node.tagName==='TEXTAREA')return;for(const child of node.childNodes)visit(child);
   }
   if(root)visit(root);
 }
 function applyLanguage(value,persist=true){language=value==='en'?'en':'zh';document.documentElement.lang=language==='en'?'en':'zh-CN';if(persist)try{localStorage.setItem('kok.uiLanguage',language);}catch{}localize();return language;}
 function setupSettings(){
   const dialog=document.querySelector('#small-dialog');if(!dialog||dialog.querySelector('#ui-language'))return;
   dialog.querySelector('h2')?.insertAdjacentHTML('afterend','<div class="setting-row"><div>界面语言<small>切换中文或英文，立即生效</small></div><select id="ui-language" aria-label="Interface language / 界面语言"><option value="zh" data-user-content>中文</option><option value="en" data-user-content>English</option></select></div>');
   const select=dialog.querySelector('#ui-language');if(!select)return;select.value=language;
   select.addEventListener('change',async()=>{const value=select.value;try{await setting({uiLanguage:value});applyLanguage(value);requestAnimationFrame(updateNavIndicator);}catch(error){fail(error);}});
   dialog.querySelector('.about-details summary')?.insertAdjacentHTML('afterend','<button class="text-button help-button" id="help-button">使用帮助</button>');
   dialog.querySelector('#help-button')?.addEventListener('click',async()=>{try{const content=await native('readHelp');showSmall('<h2>使用帮助</h2><pre class="help-text" data-user-content>'+esc(content)+'</pre>');}catch(error){fail(error);}});
 }
 const ready=fetch('./locales/en.json'+new URL(document.currentScript.src).search).then(response=>{if(!response.ok)throw Error('Language catalog unavailable');return response.json();}).then(catalog=>{messages=catalog.messages;patterns=catalog.patterns.map(item=>({...item,regex:new RegExp(item.pattern,'s')}));applyLanguage(language);const observer=new MutationObserver(records=>{const roots=new Set();for(const record of records){if(record.type==='childList')for(const node of record.addedNodes)roots.add(node);else roots.add(record.target);}for(const root of roots)if(root.isConnected)localize(root);});observer.observe(document.body,{childList:true,subtree:true,characterData:true,attributes:true,attributeFilter:['title','aria-label','placeholder']});});
 return{ready,text,localize,applyLanguage,setupSettings,get language(){return language;},isProtected};
})();

'use strict';
// Shell behavior uses the existing module, menu and selection contracts.
const ShellUi=(()=>{
 const tr=(zh,en)=>I18n.language==='en'?en:zh;
 function select(ids){if(typeof Warehouse!=='undefined'&&Kok.enabled('workspace'))Warehouse.select(ids);else{libraryDrag.selected=new Set(ids);for(const card of document.querySelectorAll('.file-card'))card.classList.toggle('library-selected',ids.includes(card.dataset.id));}}
 function selected(){return typeof Warehouse!=='undefined'&&Kok.enabled('workspace')?Warehouse.selected:[...libraryDrag.selected];}
 function fileMenu(id,anchor,previous,point){
  const item=byId(id);if(!item||!anchor?.isConnected)return;
  const items=[
   {label:tr('在 kok 中打开','Open in kok'),icon:'external',action:()=>openItem(id)},
   {label:item.favorite?tr('取消收藏','Remove favorite'):tr('加入收藏','Add to favorites'),icon:'heart',action:()=>favorite(id)},
   ...(Kok.enabled('workspace')?[{label:tr('资料详情','File details'),icon:'info',shortcut:'Ctrl Shift I',action:()=>Inspector.show(id)}]:[]),
   {label:tr('在文件夹中显示','Show in folder'),icon:'folderOpen',separatorBefore:true,action:()=>native('reveal',{id})},
   {label:tr('使用系统应用打开','Open with system app'),icon:'external',action:()=>native('openExternal',{id})},
   {label:tr('更多文件操作…','More file actions…'),icon:'more',separatorBefore:true,action:()=>previous(id)}
  ];
  const count=selected().includes(id)?selected().length:1;
  const extra=Kok.applicable('context-menu',{count,kind:item.kind}).filter(c=>c.uiCommand).map(cap=>({label:I18n.text(cap.title),icon:'layers',action:()=>Kok.run(cap.uiCommand)}));
  if(extra.length)items.push({label:tr('扩展工具','Additional tools'),icon:'layers',action:()=>GlassMenu.open(anchor,extra,{label:tr('扩展工具','Additional tools'),focus:true})});
  const options={className:'file-action-menu',label:tr('文件操作','File actions'),heading:item.name,focus:true};
  if(point)GlassMenu.openAt(anchor,point,items,options);else GlassMenu.open(anchor,items,options);
 }
 Kok.extend('core','shell-interactions:file-menu','fileMenu',previous=>function(id,anchor,point){if(anchor)return fileMenu(id,anchor,previous,point);return previous(id);});
 document.addEventListener('contextmenu',event=>{
  const card=event.target.closest('.file-card[data-id]');if(!card)return;
  event.preventDefault();event.stopImmediatePropagation();if(!selected().includes(card.dataset.id))select([card.dataset.id]);
  const anchor=card.querySelector('.card-menu');anchor.focus({preventScroll:true});Kok.invoke('fileMenu',null,[card.dataset.id,anchor,{x:event.clientX,y:event.clientY}]);
 },true);
 window.addEventListener('keydown',event=>{
  if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='f'&&!viewer.open&&!$('#small-dialog').open){event.preventDefault();event.stopImmediatePropagation();$('#search').focus();return;}
  const button=event.target.closest?.('.file-grid .file-open,.file-list .file-open');if(!button||viewer.open||$('#small-dialog').open||GlassMenu.isOpen)return;
  const card=button.closest('.file-card'),cards=[...card.parentElement.querySelectorAll(':scope>.file-card')],at=cards.indexOf(card);
  if(event.key==='ContextMenu'||event.shiftKey&&event.key==='F10'){event.preventDefault();event.stopImmediatePropagation();Kok.invoke('fileMenu',null,[card.dataset.id,card.querySelector('.card-menu')]);return;}
  if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='a'){event.preventDefault();event.stopImmediatePropagation();select(cards.map(c=>c.dataset.id));return;}
  if(event.key==='Escape'){event.preventDefault();event.stopImmediatePropagation();select([]);return;}
  if(event.key===' '&&!event.ctrlKey&&!event.metaKey){event.preventDefault();event.stopImmediatePropagation();const ids=selected();select(ids.includes(card.dataset.id)?ids.filter(id=>id!==card.dataset.id):[...ids,card.dataset.id]);return;}
  if(!['ArrowRight','ArrowLeft','ArrowUp','ArrowDown','Home','End'].includes(event.key)||event.ctrlKey||event.altKey||event.metaKey)return;
  event.preventDefault();event.stopImmediatePropagation();
  const cols=cards.filter(c=>Math.abs(c.offsetTop-cards[0].offsetTop)<2).length||1;
  const next=Math.max(0,Math.min(cards.length-1,event.key==='Home'?0:event.key==='End'?cards.length-1:at+({ArrowRight:1,ArrowLeft:-1,ArrowUp:-cols,ArrowDown:cols}[event.key])));
  if(event.shiftKey){const a=Math.min(at,next),b=Math.max(at,next);select([...new Set([...selected(),...cards.slice(a,b+1).map(c=>c.dataset.id)])]);}
  cards[next]?.querySelector('.file-open').focus({preventScroll:true});cards[next]?.scrollIntoView({block:'nearest'});
 },true);
 document.addEventListener('click',event=>{
  const tool=event.target.closest('[data-library-tools]');if(!tool)return;
  const items=[];
  if(isSmartMode()&&Kok.enabled('ai'))items.push({label:tr('多文件对比与笔记回顾','Compare files and review notes'),icon:'spark',action:()=>openStudy('compare')});
  if(isSmartMode()&&Kok.enabled('agent'))items.push({label:tr('交给 Agent','Open Agent'),icon:'agent',action:()=>Kok.run('agent.open')});
  if(Kok.enabled('workspace'))items.push({label:tr('资料检索与备份','Search and library data'),icon:'search',action:()=>Kok.run('workspace.libraryTools')});
  if(items.length)GlassMenu.open(tool,items,{label:tr('资料库工具','Library tools'),focus:true});
 });
 // Keep group disclosure state through normal route rerenders, without new settings.
 const groups=new Map();
 document.addEventListener('toggle',event=>{if(event.target.matches('details[data-nav-group]'))groups.set(event.target.dataset.navGroup,event.target.open);},true);
 function restoreGroups(){for(const group of document.querySelectorAll('details[data-nav-group]'))if(groups.has(group.dataset.navGroup)&&!group.querySelector('[aria-current=page]'))group.open=groups.get(group.dataset.navGroup);}
 Kok.extend('core','shell-interactions:nav-groups','renderNav',previous=>function(...args){const result=previous.apply(this,args);restoreGroups();return result;});
 Kok.extend('core','shell-interactions:preserve-file-focus','render',previous=>function(...args){
  const active=document.activeElement,card=active?.closest('.file-card'),route=state.route,id=card?.dataset.id;
  const selector=active?.matches('.card-menu')?'.card-menu':active?.matches('.card-favorite')?'.card-favorite':'.file-open';
  const result=previous.apply(this,args);
  if(id&&!active.isConnected&&route===state.route)document.querySelector(`.file-card[data-id="${CSS.escape(id)}"] ${selector}`)?.focus({preventScroll:true});
  return result;
 });
 return{select,selected};
})();

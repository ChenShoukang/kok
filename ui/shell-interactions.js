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
 function openCommandPalette(){
  if(viewer.open||$('#small-dialog').open)return;
  const smart=isSmartMode()&&!state.isPrivate;
  const actions=[
   ...(smart&&Kok.enabled('agent')?[
    {kind:'action',badge:tr('任务','Task'),label:tr('新建 Agent 任务','New Agent task'),desc:tr('开启空白对话','Start a fresh Agent session'),run:()=>AgentUi.newSession().then(()=>navigate('agent'))},
    {kind:'action',badge:'PPT',label:tr('用 Agent 制作 PPT','Create presentation with Agent'),desc:tr('选择主题与页数生成商用 PPTX','Generate an editable PPTX deck'),run:()=>DocumentUi.open({format:'pptx'})},
    {kind:'action',badge:'DOCX',label:tr('新建工作汇报','New executive report'),desc:tr('带结论框与表格的 Word / Markdown 模板','Create a structured report document'),run:()=>DocumentUi.open({format:'docx',template:'report'})},
    {kind:'action',badge:'AI',label:tr('配置 AI 服务与模型','Configure AI provider & model'),desc:tr('切换模型或修改 API 地址','Switch model or edit API settings'),run:()=>typeof showProviderSettings==='function'?showProviderSettings():$('#settings-button')?.click()}
   ]:[]),
   ...(Kok.enabled('documents')?[{kind:'action',badge:tr('新建','New'),label:tr('新建文档','New document'),desc:'Markdown / Word / TXT',run:()=>DocumentUi.open()}]:[]),
   ...(smart&&Kok.enabled('agent')?[{kind:'action',badge:tr('导航','Go'),label:'Agent',desc:tr('前往 Agent 工作台','Go to Agent workspace'),run:()=>navigate('agent')}]:[]),
   {kind:'action',badge:tr('导航','Go'),label:tr('我的作品','My works'),desc:tr('项目与草稿工作室','Creative projects and drafts'),run:()=>navigate('works')},
   {kind:'action',badge:tr('导航','Go'),label:tr('资料库','Library'),desc:tr('全部本地资料','Browse all local files'),run:()=>navigate('overview')},
   {kind:'action',badge:tr('外观','Theme'),label:state.settings.theme==='dark'?tr('切换到浅色模式','Switch to light theme'):tr('切换到深色模式','Switch to dark theme'),desc:tr('即时切换界面明暗','Toggle light / dark appearance'),run:async()=>{await setting({theme:state.settings.theme==='dark'?'light':'dark'});applyTheme();}}
  ];
  showSmall(`<div class="cmd-palette"><div class="cmd-palette-head">${svg('search')}<input id="cmd-palette-input" type="search" autocomplete="off" placeholder="${esc(tr('搜索命令、会话或本地资料…','Search commands, sessions or files…'))}" aria-label="${esc(tr('快速命令面板','Command palette'))}"><kbd>Esc</kbd></div><div id="cmd-palette-list" class="cmd-palette-list" role="listbox"></div><footer class="cmd-palette-foot"><span>↑↓ ${tr('选择','Select')}</span><span>Enter ${tr('打开','Open')}</span>${smart&&Kok.enabled('agent')?`<span>Shift Enter ${tr('交给 Agent','Attach to Agent')}</span>`:''}</footer></div>`);
  const input=$('#cmd-palette-input'),list=$('#cmd-palette-list');
  let rows=[],index=0;
  const build=query=>{
   const q=String(query||'').trim().toLocaleLowerCase(),out=[];
   for(const a of actions)if(!q||a.label.toLocaleLowerCase().includes(q)||a.desc.toLocaleLowerCase().includes(q)||a.badge.toLocaleLowerCase().includes(q))out.push(a);
   if(smart&&globalThis.AgentUi?.state?.sessions){
    for(const s of AgentUi.state.sessions.slice(0,8))if(s?.title&&(!q||s.title.toLocaleLowerCase().includes(q)))out.push({kind:'session',badge:tr('会话','Chat'),label:s.title,desc:new Date(s.updated).toLocaleDateString(),run:()=>AgentUi.newSession(s.id).then(()=>navigate('agent'))});
   }
   const files=state.items.filter(i=>!q||String(i.name||'').toLocaleLowerCase().includes(q)||String(i.ext||'').toLocaleLowerCase().includes(q)||String(i.collection||'').toLocaleLowerCase().includes(q)).slice(0,q?10:6);
   for(const f of files)out.push({kind:'file',id:f.id,badge:String(f.ext||f.kind||'FILE').toUpperCase(),label:f.name,desc:f.collection||I18n.text(labels[f.kind]||f.kind||''),run:()=>openItem(f.id,{play:false})});
   return out.slice(0,16);
  };
  const paint=()=>{
   list.innerHTML=rows.length?rows.map((r,i)=>`<button type="button" class="cmd-palette-item" role="option" aria-selected="${i===index}" data-cmd-index="${i}"><span class="cmd-palette-badge">${esc(r.badge)}</span><span class="cmd-palette-main"><b data-user-content>${esc(r.label)}</b><small data-user-content>${esc(r.desc||'')}</small></span></button>`).join(''):`<p class="cmd-palette-empty">${tr('没有匹配的结果','No matching results')}</p>`;
   list.querySelector('[aria-selected="true"]')?.scrollIntoView({block:'nearest'});
  };
  const runAt=async(i,toAgent=false)=>{
   const r=rows[i];if(!r)return;
   await closeSmall();
   if(toAgent&&r.kind==='file'&&smart&&Kok.enabled('agent'))AgentUi.open({ids:[r.id]});
   else await r.run();
  };
  rows=build('');paint();input.focus();
  input.oninput=()=>{rows=build(input.value);index=0;paint();};
  list.onclick=e=>{const b=e.target.closest('[data-cmd-index]');if(b)runAt(Number(b.dataset.cmdIndex)||0,e.shiftKey).catch(fail);};
  input.onkeydown=e=>{
   if(e.isComposing||e.keyCode===229)return;
   if(e.key==='ArrowDown'&&rows.length){e.preventDefault();index=(index+1)%rows.length;paint();}
   else if(e.key==='ArrowUp'&&rows.length){e.preventDefault();index=(index-1+rows.length)%rows.length;paint();}
   else if(e.key==='Enter'&&rows.length){e.preventDefault();runAt(index,e.shiftKey).catch(fail);}
  };
 }
 window.addEventListener('keydown',event=>{
  if((event.ctrlKey||event.metaKey)&&!event.altKey&&event.key.toLowerCase()==='k'){event.preventDefault();event.stopImmediatePropagation();openCommandPalette();return;}
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
  const smart=isSmartMode(),items=[];
  if(smart&&Kok.enabled('organizer'))items.push({label:tr('智能整理','Smart library'),icon:'layers',action:()=>navigate('organize')});
  if(smart&&Kok.enabled('downloads'))items.push({label:tr('视频下载','Video downloads'),icon:'download',action:()=>navigate('downloads')});
  if(Kok.enabled('knowledge'))items.push({label:tr('知识图谱','Knowledge graph'),icon:'network',action:()=>showKnowledgeGraph()});
  if(Kok.enabled('workspace'))items.push({label:tr('智能集合','Smart collections'),icon:'search',action:()=>navigate('queries')},{label:tr('回顾','Review'),icon:'clock',action:()=>navigate('review')});
  if(Kok.enabled('operations'))items.push({label:tr('操作历史','History'),icon:'undo',action:()=>Kok.run('operations.history')});
  if(Kok.enabled('jobs'))items.push({label:tr('活动','Activity'),icon:'list',action:()=>Kok.run('jobs.activity')});
  if(Kok.enabled('workspace'))items.push({label:tr('资料检索与备份','Search and library data'),icon:'search',separatorBefore:true,action:()=>Kok.run('workspace.libraryTools')});
  if(smart&&Kok.enabled('agent'))items.push({label:tr('交给 Agent','Open Agent'),icon:'agent',separatorBefore:true,action:()=>Kok.run('agent.open')});
  if(smart&&Kok.enabled('ai'))items.push({label:tr('多文件对比与笔记回顾','Compare files and review notes'),icon:'spark',action:()=>openStudy('compare')});
  if(items.length)GlassMenu.open(tool,items,{label:tr('资料库工具','Library tools'),focus:true});
 });
 // Keep group disclosure state through normal route rerenders, without new settings.
 const groups=new Map();
 Kok.extend('core','shell-interactions:music-navigation','renderMusic',previous=>function(...args){return `<div class="library-toolbar">${libraryFilters()}</div>`+previous.apply(this,args);});
 document.addEventListener('toggle',event=>{if(event.target.matches('details[data-nav-group]'))groups.set(event.target.dataset.navGroup,event.target.open);},true);
 function restoreGroups(){for(const group of document.querySelectorAll('details[data-nav-group]'))if(groups.has(group.dataset.navGroup)&&!group.querySelector('[aria-current=page]'))group.open=groups.get(group.dataset.navGroup);}
 Kok.extend('core','shell-interactions:nav-groups','renderNav',previous=>function(...args){const result=previous.apply(this,args);restoreGroups();return result;});
 Kok.extend('core','shell-interactions:preserve-file-focus','render',previous=>function(...args){
  const active=document.activeElement,card=active?.closest('.file-card'),route=state.route,id=card?.dataset.id;
  const selector=active?.matches('.card-menu')?'.card-menu':active?.matches('.card-favorite')?'.card-favorite':'.file-open';
  const result=previous.apply(this,args);
  document.body.dataset.page=state.route;
  if(id&&!active.isConnected&&route===state.route)document.querySelector(`.file-card[data-id="${CSS.escape(id)}"] ${selector}`)?.focus({preventScroll:true});
  return result;
 });
 return{select,selected};
})();

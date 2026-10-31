'use strict';
// The select stays visible and owns its value/events; only its popup is rendered here.
const GlassMenu=(()=>{
 let active=null,sequence=0,frame=0,lastPointer=null;
 const eligible=target=>target instanceof HTMLSelectElement&&!target.multiple&&target.size<=1&&!target.disabled;
 function close(restore=false,immediate=false){
  const current=active;if(!current)return;active=null;cancelAnimationFrame(frame);
  if(current.select){current.select.setAttribute('aria-expanded','false');current.select.removeAttribute('aria-controls');current.select.removeAttribute('aria-activedescendant');}
  else if(current.anchor?.isConnected){current.anchor.setAttribute('aria-expanded','false');current.anchor.removeAttribute('aria-controls');}
  const remove=()=>{try{current.menu.hidePopover();}catch{}current.menu.remove();};
  // Desktop menus dismiss immediately. Leaving a closing popover in the top
  // layer creates duplicate menus when the next action opens one quickly.
  remove();
  if(restore&&current.anchor.isConnected){if(current.restoreFocus)current.restoreFocus();else current.anchor.focus({preventScroll:true});}
  current.onClose?.();
 }
 function position(){
  frame=0;const current=active;if(!current)return;
  // Rebind a replaced select only within the same workspace/dialog, with identical options and value.
  if(!current.anchor.isConnected&&current.select?.id&&current.route===JSON.stringify([state.route,state.collection,state.query,!!state.isPrivate,state.viewer?.id])){
   const next=document.getElementById(current.select.id);
   if(eligible(next)&&next.closest('dialog')===current.owner&&next.value===current.select.value&&JSON.stringify([...next.options].map(option=>[option.value,option.disabled]))===current.optionValues){current.anchor=current.select=next;next.setAttribute('aria-expanded','true');next.setAttribute('aria-controls',current.menu.id);next.setAttribute('aria-activedescendant',current.menu.id+'-'+current.index);}
  }
  if(!current.anchor.isConnected||!current.anchor.getClientRects().length||current.select&&!eligible(current.select)){close(false,true);return;}
  const r=current.rect||current.anchor.getBoundingClientRect(),menu=current.menu,padding=9,viewport=window.visualViewport;
  const vw=Math.min(innerWidth,viewport?.width||innerWidth),vh=Math.min(innerHeight,viewport?.height||innerHeight);
  const width=Math.min(vw-padding*2,Math.max(current.select?r.width:190,Math.min(current.desiredWidth,360)));
  menu.style.width=width+'px';menu.style.maxHeight=Math.min(340,vh-padding*2)+'px';
  const height=Math.min(menu.scrollHeight,340,vh-padding*2),below=vh-r.bottom-padding,above=r.top-padding;
  current.above=below<height+7&&above>below;
  const space=current.above?above-7:below-7;menu.style.maxHeight=Math.max(70,Math.min(340,vh-padding*2,space))+'px';
  const actual=Math.min(menu.scrollHeight,parseFloat(menu.style.maxHeight));
  menu.style.left=Math.max(padding,Math.min(r.left,vw-width-padding))+'px';
  menu.style.top=Math.max(padding,Math.min(current.above?r.top-actual-7:r.bottom+7,vh-actual-padding))+'px';
  menu.style.transformOrigin=current.above?'bottom center':'top center';
 }
 function highlight(index,scroll=true){
  const current=active;if(!current)return;const length=current.items.length;
  index=Math.max(0,Math.min(length-1,index));if(current.items[index]?.disabled)return;
  current.index=index;current.menu.querySelectorAll('[data-menu-index]').forEach(button=>button.classList.toggle('is-active',Number(button.dataset.menuIndex)===index));
  const button=current.menu.querySelector(`[data-menu-index="${index}"]`);if(current.select&&button)current.select.setAttribute('aria-activedescendant',button.id);
  if(!current.select&&button){current.menu.querySelectorAll('[data-menu-index]').forEach(row=>row.tabIndex=row===button?0:-1);if(scroll)button.focus({preventScroll:true});}
  if(scroll&&button){const top=button.offsetTop,bottom=top+button.offsetHeight;if(top<current.menu.scrollTop)current.menu.scrollTop=top;else if(bottom>current.menu.scrollTop+current.menu.clientHeight)current.menu.scrollTop=bottom-current.menu.clientHeight;}
 }
 function move(delta){const current=active;if(!current)return;let n=current.index;for(let count=0;count<current.items.length;count++){n=(n+delta+current.items.length)%current.items.length;if(!current.items[n].disabled){highlight(n);break;}}}
 function choose(index){const current=active,item=current?.items[index];if(!item||item.disabled)return;close(true,true);try{Promise.resolve(item.action()).catch(fail);}catch(error){fail(error);}}
 function show(anchor,items,select=null,rect=null,options={}){
  // A text editor owns its selection/menu. Only a library/media context opts in
  // to item capabilities; stale library selections must not pollute editor menus.
  if(!select&&options.capabilities&&typeof Warehouse!=='undefined'&&Warehouse.selected.length){const selection={count:Warehouse.selected.length,kind:byId(Warehouse.selected[0])?.kind};items=[...items,...Kok.applicable('context-menu',selection).filter(c=>c.uiCommand).map((c,n)=>({label:I18n.text(c.title),separatorBefore:n===0,action:()=>Kok.run(c.uiCommand).catch(fail)}))];}
  close(false,true);if(!items.length)return;
  const menu=document.createElement('div');menu.className='glass-menu';menu.id='kok-menu-'+(++sequence);menu.setAttribute('popover','manual');menu.setAttribute('role',select?'listbox':'menu');
  if(anchor.closest('.photo-editor')){menu.classList.add('photo-editor-menu');const palette=getComputedStyle(anchor);for(const key of ['--view-panel','--view-text','--view-muted','--view-control','--view-line'])menu.style.setProperty(key,palette.getPropertyValue(key));}
  const label=options.label||select?.getAttribute('aria-label')||select?.labels?.[0]?.textContent?.trim()||'视频菜单';menu.setAttribute('aria-label',label);if(options.className)menu.classList.add(options.className);
  if(options.heading){const heading=document.createElement('div');heading.className='context-heading';heading.textContent=options.heading;heading.setAttribute('data-user-content','');menu.append(heading);}
  items.forEach((item,index)=>{const button=document.createElement('button');button.type='button';button.tabIndex=-1;button.id=menu.id+'-'+index;button.dataset.menuIndex=index;button.setAttribute('role',select?'option':'menuitem');if(select)button.setAttribute('aria-selected',String(!!item.selected));button.disabled=!!item.disabled;
   if(item.separatorBefore){const separator=document.createElement('div');separator.setAttribute('role','separator');separator.className='context-separator';menu.append(separator);}if(item.key)button.dataset.contextAction=item.key;
   if(item.icon){const icon=document.createElement('span');icon.className='menu-leading-icon';icon.innerHTML=svg(item.icon);button.append(icon);}if(item.danger)button.classList.add('danger');
   const text=document.createElement('span');text.className='menu-label';text.textContent=item.label;if(item.protected)text.setAttribute('data-user-content','');button.append(text);const check=document.createElement('span');check.className='menu-check';check.setAttribute('aria-hidden','true');check.innerHTML=item.selected?svg('check'):'';if(item.shortcut){check.classList.add('context-shortcut');check.textContent=item.shortcut;}button.append(check);menu.append(button);
  });
  (document.fullscreenElement||anchor.closest('dialog[open]')||document.body).append(menu);
  active={anchor,select,rect,items,menu,index:Math.max(0,items.findIndex(item=>item.selected&&!item.disabled)),desiredWidth:Math.max(select?190:260,...items.map(item=>Math.min(330,Array.from(item.label).length*8+64))),above:false,restoreFocus:options.restoreFocus,onClose:options.onClose};
  if(select){active.optionValues=JSON.stringify([...select.options].map(option=>[option.value,option.disabled]));active.route=JSON.stringify([state.route,state.collection,state.query,!!state.isPrivate,state.viewer?.id]);active.owner=select.closest('dialog');}
  if(items[active.index]?.disabled)active.index=Math.max(0,items.findIndex(item=>!item.disabled));
  if(select){select.setAttribute('aria-expanded','true');select.setAttribute('aria-controls',menu.id);}
  else{anchor.setAttribute('aria-expanded','true');anchor.setAttribute('aria-controls',menu.id);}
  menu.showPopover();position();highlight(active.index,false);
  highlight(active.index,!!select);
  if(options.focus)menu.querySelector(`[data-menu-index="${active.index}"]`)?.focus({preventScroll:true});
  if(typeof CalmMotion!=='undefined')CalmMotion.enter(menu);else if(!reducedMotion())menu.animate([{opacity:0,transform:`translateY(${active.above?5:-5}px) scale(.975)`},{opacity:1,transform:'translateY(0) scale(1)'}],{duration:190,easing:'cubic-bezier(.16,1,.3,1)'});
  menu.addEventListener('pointermove',event=>{const button=event.target.closest('[data-menu-index]');if(button&&!button.disabled)highlight(Number(button.dataset.menuIndex),false);});
  menu.addEventListener('pointerdown',event=>event.preventDefault());menu.addEventListener('click',event=>{const button=event.target.closest('[data-menu-index]');if(button)choose(Number(button.dataset.menuIndex));});
  I18n.localize(menu);
 }
 function selectMenu(select){
  if(active?.select===select){close(true);return;}select.focus({preventScroll:true});
  const options=[...select.options];show(select,options.map((option,index)=>({label:option.textContent,selected:option.selected,disabled:option.disabled||option.parentElement.tagName==='OPTGROUP'&&option.parentElement.disabled,protected:I18n.isProtected(option),action:()=>{const live=select.isConnected?select:select.id?document.getElementById(select.id):null;if(!eligible(live)||live.options[index]?.value!==option.value)return;live.selectedIndex=index;live.dispatchEvent(new Event('input',{bubbles:true}));live.dispatchEvent(new Event('change',{bubbles:true}));}})),select);
 }
 document.addEventListener('pointerdown',event=>{
  if(eligible(event.target)&&HTMLElement.prototype.showPopover){event.preventDefault();lastPointer=event.target;selectMenu(event.target);return;}
  if(active&&!active.menu.contains(event.target))close();
 },true);
 document.addEventListener('mousedown',event=>{if(eligible(event.target)&&HTMLElement.prototype.showPopover)event.preventDefault();},true);
 document.addEventListener('click',event=>{if(!eligible(event.target)||!HTMLElement.prototype.showPopover)return;event.preventDefault();if(event.detail===0&&lastPointer!==event.target)selectMenu(event.target);lastPointer=null;},true);
 document.addEventListener('keydown',event=>{
  const select=eligible(event.target)?event.target:null;
  if(!active&&select&&HTMLElement.prototype.showPopover&&['ArrowDown','ArrowUp','Enter',' ','F4'].includes(event.key)){event.preventDefault();event.stopImmediatePropagation();selectMenu(select);return;}
  if(!active)return;
  if(event.key==='Tab'){close(!active.select);return;}
  if(!active.select&&(event.ctrlKey||event.metaKey)){const key={c:'copy',x:'cut',v:'paste',a:'select-all'}[event.key.toLowerCase()],index=key?active.items.findIndex(item=>item.key===key):-1;if(index>=0){event.preventDefault();event.stopImmediatePropagation();choose(index);return;}}
  if(event.key==='Escape'){event.preventDefault();event.stopImmediatePropagation();close(true);return;}
  if(['ArrowDown','ArrowUp','Home','End','PageDown','PageUp','Enter',' '].includes(event.key)){
   event.preventDefault();event.stopImmediatePropagation();if(event.key==='Enter'||event.key===' ')choose(active.index);else if(event.key==='Home')highlight(0);else if(event.key==='End')highlight(active.items.length-1);else{const direction=['ArrowUp','PageUp'].includes(event.key)?-1:1;for(let n=0;n<(event.key.startsWith('Page')?5:1);n++)move(direction);}return;
  }
  if(event.key.length===1&&!event.ctrlKey&&!event.metaKey&&!event.altKey){event.preventDefault();event.stopImmediatePropagation();const now=performance.now();active.prefix=(now-(active.typedAt||0)<650?active.prefix||'':'')+event.key.toLocaleLowerCase();active.typedAt=now;const index=active.items.findIndex(item=>!item.disabled&&item.label.toLocaleLowerCase().startsWith(active.prefix));if(index>=0)highlight(index);}
 },true);
 const reposition=event=>{if(active&&(!(event?.target instanceof Node)||!active.menu.contains(event.target))&&!frame)frame=requestAnimationFrame(position);};
 window.addEventListener('resize',reposition);window.addEventListener('scroll',reposition,true);window.visualViewport?.addEventListener('resize',reposition);window.visualViewport?.addEventListener('scroll',reposition);
 new MutationObserver(()=>{if(active&&!frame)frame=requestAnimationFrame(position);}).observe(document.body,{childList:true,subtree:true,attributes:true,attributeFilter:['open']});
 let menuLanguage=document.documentElement.lang;
 new MutationObserver(()=>{const next=document.documentElement.lang;if(next!==menuLanguage){menuLanguage=next;close(false,true);}}).observe(document.documentElement,{attributes:true,attributeFilter:['lang']});
 return{close,open(anchor,items,options={}){show(anchor,items,null,null,options);},openAt(anchor,point,items,options={}){show(anchor,items,null,{left:point.x,right:point.x,top:point.y,bottom:point.y,width:0,height:0},options);},context(event,items){event.preventDefault();const anchor=event.currentTarget;anchor.focus?.({preventScroll:true});show(anchor,items,null,{left:event.clientX,right:event.clientX,top:event.clientY,bottom:event.clientY,width:0,height:0},{capabilities:true});},get isOpen(){return!!active;}};
})();

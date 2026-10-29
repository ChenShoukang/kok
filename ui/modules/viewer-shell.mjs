import {PresentationUi} from './presentation-runtime.mjs';
import {AgentUi} from './agent-runtime.mjs';
import {Warehouse,Inspector} from './warehouse-state.mjs';
export const ViewerShell={
 current:null,switching:false,
 refresh(){const dialog=$('#creative-dialog')?.open?$('#creative-dialog'):viewer,item=dialog===viewer?state.viewer:creative?.session?.item;if(dialog?.open&&item)this.attach(dialog,item);},
 attach(dialog,item){
  if(!dialog||!item)return;this.current=item;let bar=dialog.querySelector(':scope > .viewer-mode-bar');
  if(!bar){
   bar=document.createElement('nav');bar.className='viewer-mode-bar';bar.setAttribute('role','toolbar');
   dialog.querySelector(':scope > header,:scope > .viewer-header')?.after(bar);
   bar.innerHTML='<span class="viewer-mode-spacer"></span><button type="button" data-viewer-density="work" title="F8"></button>';
   bar.onclick=event=>{const button=event.target.closest('[data-viewer-tab],[data-viewer-density]');if(!button||button.disabled)return;const current=(dialog.id==='creative-dialog'?creative?.session?.item:state.viewer)||this.current;if(button.dataset.viewerDensity)this.density(current,button.dataset.viewerDensity).catch(fail);else this.tab(current,button.dataset.viewerTab).catch(fail);};
  }
  bar.setAttribute('aria-label',Warehouse.t('查看器工具','Viewer tools'));
  const focused=bar.contains(document.activeElement)?document.activeElement:null;
  const active=Inspector.open&&Inspector.item===item.id?(Inspector.tab==='notes'?'notes':Inspector.tab==='ai'?'ai':'view'):dialog.id==='creative-dialog'||photoEditor&&state.viewer?.id===item.id||PresentationUi.state?.session?.item.id===item.id&&PresentationUi.state.session.editing?'edit':'view';
  const tabs=[['view','查看','View'],['edit','编辑','Edit'],['notes','笔记','Notes'],['ai','AI','AI']].filter(([key])=>(!['notes','ai'].includes(key)||Kok.enabled('workspace'))&&(key!=='ai'||Warehouse.aiAvailable())),wanted=new Set(tabs.map(([key])=>key));
  for(const button of bar.querySelectorAll('[data-viewer-tab]'))if(!wanted.has(button.dataset.viewerTab))button.remove();
  for(const [key,zh,en]of tabs){let button=bar.querySelector('[data-viewer-tab="'+key+'"]');if(!button){button=document.createElement('button');button.type='button';button.dataset.viewerTab=key;bar.insertBefore(button,bar.querySelector('.viewer-mode-spacer'));}const label=Warehouse.t(zh,en),pressed=String(key===active),disabled=key==='edit'&&!Kok.enabled('editing');if(button.textContent!==label)button.textContent=label;if(button.getAttribute('aria-pressed')!==pressed)button.setAttribute('aria-pressed',pressed);if(button.disabled!==disabled)button.disabled=disabled;}
  const density=bar.querySelector('[data-viewer-density]'),mode=this.mode(item);density.dataset.viewerDensity=mode==='work'?'immersion':'work';density.setAttribute('aria-label',Warehouse.t('切换沉浸与工作模式','Toggle immersive and work modes'));if(density.dataset.iconMode!==mode){density.innerHTML=svg(mode==='work'?'expand':'layers');density.dataset.iconMode=mode;}
  const stop=focused?.isConnected&&!focused.disabled?focused:bar.querySelector('[aria-pressed=true]');for(const button of bar.querySelectorAll('button'))button.tabIndex=button===stop?0:-1;if(focused&&!focused.isConnected)stop?.focus({preventScroll:true});
  dialog.classList.toggle('viewer-immersive',mode==='immersion');dialog.classList.add('unified-viewer');this.header(dialog);
 },
 headers:new WeakMap(),
 header(dialog){const element=dialog.querySelector(':scope > header,:scope > .viewer-header');if(!element)return;const previous=this.headers.get(dialog);if(previous?.element===element)return;previous?.observer.disconnect();const set=height=>{if(height<=0)return;const value=height+'px';if(dialog.style.getPropertyValue('--viewer-head-height')!==value)dialog.style.setProperty('--viewer-head-height',value);};set(element.offsetHeight);const observer=new ResizeObserver(entries=>{const entry=entries[0],box=entry.borderBoxSize?.[0];set(box?.blockSize??element.offsetHeight);});observer.observe(element);this.headers.set(dialog,{element,observer});},

 mode(item){return state.settings.viewerDensity?.[item.kind]||(['video','photo','book'].includes(item.kind)?'immersion':'work');},
 async density(item,mode){if(this.switching)return;await setting({viewerDensity:{...(state.settings.viewerDensity||{}),[item.kind]:mode}});const dialog=$('#creative-dialog')?.open?$('#creative-dialog'):viewer;dialog.classList.toggle('viewer-immersive',mode==='immersion');this.attach(dialog,item);if(mode==='immersion'||!Kok.enabled('workspace'))Inspector.hide();else Inspector.show(item.id);},
 async tab(item,tab){
  if(this.switching)return;this.switching=true;
  try{
   if(tab==='view'){
    Inspector.hide();if(photoEditor&&state.viewer?.id===item.id){const draft={kind:'photo',signature:window.kokPhotoSignature,values:photoSnapshot()};await native('saveCreativeDraft',{id:item.id,draft});item.editDraft=draft;photoEditor=null;renderPhoto();}if(creative?.session){await closeCreative();await refresh();await openItem(item.id,{play:false});}
    if(PresentationUi.state?.session?.item.id===item.id&&PresentationUi.state.session.editing){PresentationUi.state.session.editing=false;PresentationUi.renderChrome();}
   }else if(tab==='edit'){
    if(!Kok.enabled('editing'))throw Error(I18n.text('此模块已停用，请在设置中重新启用。'));
    Inspector.hide();if(!(item.kind==='photo'&&photoEditor)&&(!creative?.session||creative.session.item.id!==item.id))await openCreativeEditor(item.id);
   }else{if(!Kok.enabled('workspace'))throw Error(I18n.text('此功能当前不可用。'));Inspector.tab=tab==='notes'?'notes':'ai';Inspector.show(item.id);}
  }finally{this.switching=false;this.refresh();}
 },

 observe(){const seen=new WeakSet(),watch=dialog=>{if(!dialog||seen.has(dialog))return;seen.add(dialog);let signature='';const sync=()=>{const next=[dialog.open,dialog.dataset.kind,dialog.classList.contains('photo-editing'),dialog.classList.contains('presentation-editing')].join('|');if(next===signature)return;signature=next;if(dialog.open){const item=dialog===viewer?state.viewer:creative?.session?.item;if(item)this.attach(dialog,item);}else if(dialog.contains($('#workspace-inspector'))){Inspector.hide();document.body.append($('#workspace-inspector'));}};new MutationObserver(sync).observe(dialog,{attributes:true,attributeFilter:['open','class','data-kind']});sync();};watch(viewer);watch($('#creative-dialog'));new MutationObserver(()=>watch($('#creative-dialog'))).observe(document.body,{childList:true});document.addEventListener('keydown',event=>{if(event.key!=='F8'||!viewer.open&&!$('#creative-dialog')?.open)return;event.preventDefault();const item=$('#creative-dialog')?.open?creative?.session?.item:state.viewer;if(item)this.density(item,this.mode(item)==='work'?'immersion':'work').catch(fail);});}

};
const SelectionBar={
 show(selection){let root=$('#workspace-selection');if(!selection.count){root?.remove();return;}if(!root){root=document.createElement('nav');root.id='workspace-selection';root.setAttribute('aria-label',Warehouse.t('选中资料操作','Selected file actions'));document.body.append(root);}const caps=Kok.applicable('action-bar',selection).filter(c=>c.uiCommand);root.innerHTML=`<small>${selection.count}</small>`+caps.map(c=>`<button class="text-button" data-kok-command="${c.uiCommand}">${esc(I18n.text(c.title))}</button>`).join('')+(AgentUi.available()?`<button class="text-button" data-kok-command="agent.open">${Warehouse.t('交给 Agent','Give to Agent')}</button>`:'');}
};
export function installViewerShell(){
// Capture before media/reader paging handlers; arrows in a toolbar move focus.
window.addEventListener('keydown',event=>{const bar=event.target.closest?.('.viewer-mode-bar');if(!bar||event.ctrlKey||event.altKey||event.metaKey)return;if(['Enter',' '].includes(event.key)){event.stopImmediatePropagation();return;}if(!['ArrowLeft','ArrowRight','Home','End'].includes(event.key))return;const buttons=[...bar.querySelectorAll('button:not(:disabled)')].filter(button=>button.getClientRects().length),at=buttons.indexOf(document.activeElement);if(!buttons.length)return;event.preventDefault();event.stopImmediatePropagation();const index=event.key==='Home'?0:event.key==='End'?buttons.length-1:(at+(event.key==='ArrowLeft'?-1:1)+buttons.length)%buttons.length;buttons.forEach((button,n)=>button.tabIndex=n===index?0:-1);buttons[index].focus({preventScroll:true});},true);
Kok.on('selection.changed',s=>SelectionBar.show(s));Kok.on('privateLocking',()=>$('#workspace-selection')?.remove());
Kok.on('job.completed',data=>{if(document.body.classList.contains('preview-window'))return;const notice=toast(Warehouse.t('后台任务已完成','Background task completed')),button=document.createElement('button');notice.querySelector('[data-job-result]')?.remove();button.dataset.jobResult='';button.className='text-button';button.textContent=Warehouse.t('查看结果','View result');button.onclick=()=>{notice.remove();const id=(data.items||[]).find(id=>byId(id));if(id)openItem(id,{play:false}).catch(fail);else ActivityUI.open().catch(fail);};notice.append(button);});
document.addEventListener('contextmenu',event=>{const card=event.target.closest('.file-card[data-id]');if(card){if(!Warehouse.selected.includes(card.dataset.id))Warehouse.select([card.dataset.id]);}else if(viewer.open&&state.viewer){Warehouse.select([state.viewer.id]);}else return;const selection={count:Warehouse.selected.length,kind:byId(Warehouse.selected[0])?.kind};const caps=Kok.applicable('context-menu',selection).filter(c=>c.uiCommand);if(!caps.length)return;requestAnimationFrame(()=>{const menu=$('#context-menu')||$('#file-context-menu')||$('#small-dialog .menu-actions');if(menu&&!menu.querySelector('[data-module-context]'))menu.insertAdjacentHTML('beforeend',caps.map(c=>`<button data-module-context data-kok-command="${c.uiCommand}">${esc(I18n.text(c.title))}</button>`).join(''));});});
Kok.on('inspector.changed',()=>ViewerShell.refresh());
ViewerShell.observe();

}

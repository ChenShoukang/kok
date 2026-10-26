'use strict';
const animateSettingsBase=animateSettings;
animateSettings=function(){
 const dialog=$('#small-dialog');if(!dialog.querySelector('.settings-tabs')){
  const rows=[...dialog.querySelectorAll(':scope>.setting-row')],top=document.createElement('div');top.className='settings-topline';top.append(dialog.querySelector('h2'));top.insertAdjacentHTML('beforeend',`<button class="icon-button" data-close-refined-settings aria-label="关闭">${svg('close')}</button>`);dialog.querySelector(':scope>.dialog-top')?.remove();dialog.prepend(top);
  const tabs=document.createElement('div');tabs.className='settings-tabs';tabs.setAttribute('role','tablist');tabs.setAttribute('aria-label','设置分类');tabs.innerHTML=[['appearance','外观'],['tools','功能'],['library','资料库']].map(([id,label],n)=>`<button role="tab" id="settings-tab-${id}" aria-controls="settings-panel-${id}" aria-selected="${n===0}" tabindex="${n===0?0:-1}" data-settings-section="${id}">${label}</button>`).join('');top.after(tabs);
  const panels={};for(const id of ['appearance','tools','library']){const p=document.createElement('section');p.className='settings-section';p.id='settings-panel-'+id;p.setAttribute('role','tabpanel');p.setAttribute('aria-labelledby','settings-tab-'+id);p.hidden=id!=='appearance';panels[id]=p;tabs.after(p);}
  for(const row of rows){const id=row.querySelector('#theme-select,#font-select,#ui-language,[data-open-appearance],[data-import-font]')?'appearance':row.matches('.app-mode-setting,.smart-settings-row')||row.querySelector('[data-video-preferences],[data-private-lock]')?'tools':'library';panels[id].append(row);
   const small=row.querySelector('small');if(small&&id!=='library'&&!row.querySelector('[data-import-font]')){row.querySelector('div')?.setAttribute('title',small.textContent);small.remove();}
  }
  const about=dialog.querySelector('.about-details');if(about)panels.library.append(about);
  const select=id=>{for(const tab of tabs.children){const active=tab.dataset.settingsSection===id;tab.setAttribute('aria-selected',String(active));tab.tabIndex=active?0:-1;panels[tab.dataset.settingsSection].hidden=!active;}animateIn(panels[id]);};
  tabs.onclick=event=>{const tab=event.target.closest('[data-settings-section]');if(tab)select(tab.dataset.settingsSection);};tabs.onkeydown=event=>{if(!['ArrowLeft','ArrowRight','Home','End'].includes(event.key))return;event.preventDefault();const items=[...tabs.children],index=items.indexOf(document.activeElement),next=event.key==='Home'?0:event.key==='End'?items.length-1:(index+(event.key==='ArrowRight'?1:-1)+items.length)%items.length;items[next].focus();select(items[next].dataset.settingsSection);};
  $('[data-close-refined-settings]').onclick=()=>closeSmall();I18n.localize(dialog);
 }
 animateSettingsBase();
};

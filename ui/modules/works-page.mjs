import {AgentUi} from './agent-runtime.mjs';
'use strict';
// 我的作品 is the creation and continuation area. It composes existing editors, routes and
// warehouse data; creative projects are ordinary workspace records (kind="project") so the
// native workspaceSave audit/undo path keeps working unchanged.
const WORK_LIMIT=24;
const SOURCE_LIMIT=200;
const MEDIA_SOURCES={
 video:{match:item=>item.kind==='video',title:'选择要剪辑的视频'},
 music:{match:item=>item.kind==='music',title:'选择要剪辑的音频'},
 pdf:{match:item=>item.ext==='PDF',title:'选择要整理的 PDF'},
 document:{match:item=>item.kind==='document'&&!item.ownedDocument,title:'选择要编辑的文档'}
};
const t=value=>I18n.text(value);
const isDraft=item=>!!item.editDraft&&Object.keys(item.editDraft).length>0;
const workLabel=item=>t(labels[item.kind]||item.kind||'');
const canCreate=()=>Kok.enabled('documents')||Kok.enabled('presentations');
// P1 creative projects: workspace records with kind="project". Goal/requirements/status plus
// material, artifact and session links live on the record; the Agent only receives the project id.
const PROJECT_STATUS={draft:'草稿',active:'进行中',done:'已完成'};
const projectHex=id=>/^[a-f0-9]{32}$/.test(String(id||''));
const projectStatusLabel=value=>t(PROJECT_STATUS[value]||PROJECT_STATUS.active);
let lastProjectsLoad=0;
function projectRows(){
 const spaces=globalThis.Warehouse?.data?.spaces||[];
 return (Array.isArray(spaces)?spaces:[]).filter(row=>row&&row.kind==='project').sort((a,b)=>String(b.updated||b.created||'').localeCompare(String(a.updated||a.created||'')));
}
function projectById(id){return projectRows().find(row=>row.id===id)||null;}
function projectGoalText(project){return String(project?.goal||'').replace(/\s+/g,' ').trim();}
function projectCounts(project){return {materials:(project?.members||[]).length,artifacts:(project?.artifacts||[]).length,sessions:(project?.sessions||[]).length};}
function projectUpdated(project){const value=String(project?.updated||project?.created||'');return value?new Date(value).toLocaleDateString():'';}
function refreshProjectData(){
 if(state.isPrivate)return;
 const warehouse=globalThis.Warehouse;
 if(!warehouse||typeof warehouse.load!=='function')return;
 const now=Date.now();if(now-lastProjectsLoad<1500)return;lastProjectsLoad=now;
 warehouse.load().then(()=>{if(state.route==='works')WorksPage.render();}).catch(()=>{});
}
async function saveProject(value){
 const warehouse=globalThis.Warehouse;
 if(warehouse&&typeof warehouse.save==='function')return warehouse.save('space',value,value.id);
 await native('workspaceSave',{kind:'space',id:value.id,value});
 return true;
}
function continueItems(){
 if(state.isPrivate)return [];
 const items=state.items.filter(item=>item.ownedDocument||isDraft(item));
 return items.sort((a,b)=>String(b.modified||b.added||'').localeCompare(String(a.modified||a.added||'')));
}
// Bespoke flat geometric studio artwork & editorial icons for Works Studio.
const WORKS_HERO_ART='<svg class="works-hero-art" viewBox="0 0 64 56" fill="none" aria-hidden="true"><rect x="4" y="10" width="38" height="40" rx="5" class="wha-back"/><rect x="14" y="6" width="38" height="40" rx="5" class="wha-mid"/><rect x="22" y="12" width="38" height="40" rx="5" class="wha-front"/><rect x="28" y="20" width="22" height="4.5" rx="1.5" class="wha-ink"/><rect x="28" y="29" width="26" height="3" rx="1.5" class="wha-muted"/><rect x="28" y="36" width="18" height="3" rx="1.5" class="wha-muted"/><circle cx="52" cy="18" r="4" class="wha-dot"/></svg>';
const WORK_PREVIEW_DOC='<svg class="work-preview-art" viewBox="0 0 240 84" fill="none" aria-hidden="true"><rect x="20" y="12" width="132" height="72" rx="5" class="wpa-sheet"/><rect x="20" y="12" width="132" height="8" rx="2.5" class="wpa-accent"/><rect x="32" y="29" width="66" height="6" rx="2" class="wpa-ink"/><rect x="32" y="42" width="102" height="4" rx="2" class="wpa-muted"/><rect x="32" y="52" width="88" height="4" rx="2" class="wpa-muted"/><rect x="32" y="62" width="64" height="4" rx="2" class="wpa-muted"/><rect x="164" y="22" width="56" height="62" rx="5" class="wpa-card"/><circle cx="192" cy="46" r="12" class="wpa-accent-soft"/><rect x="176" y="64" width="32" height="4" rx="2" class="wpa-muted"/></svg>';
const WORK_PREVIEW_SLIDE='<svg class="work-preview-art" viewBox="0 0 240 84" fill="none" aria-hidden="true"><rect x="18" y="12" width="144" height="64" rx="5" class="wpa-sheet"/><rect x="28" y="22" width="56" height="44" rx="3" class="wpa-accent-soft"/><rect x="36" y="32" width="26" height="22" rx="2" class="wpa-accent"/><rect x="94" y="26" width="52" height="6" rx="2" class="wpa-ink"/><rect x="94" y="39" width="44" height="4" rx="2" class="wpa-muted"/><rect x="94" y="49" width="36" height="4" rx="2" class="wpa-muted"/><rect x="172" y="12" width="50" height="28" rx="4" class="wpa-card"/><rect x="172" y="46" width="50" height="28" rx="4" class="wpa-card"/></svg>';
const WORKS_ICONS={
 project:'<svg class="ui-icon studio-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M3 8L12 3.5 21 8l-9 4.5L3 8Z" fill="currentColor" fill-opacity=".14" stroke="currentColor" stroke-width="1.65" stroke-linejoin="round"/><path d="M3 12.8l9 4.5 9-4.5M3 17l9 4.5 9-4.5" stroke="currentColor" stroke-width="1.65" stroke-linejoin="round"/></svg>',
 spaces:'<svg class="ui-icon studio-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true"><rect x="3.5" y="3.5" width="7.5" height="7.5" rx="2" fill="currentColor" fill-opacity=".14" stroke="currentColor" stroke-width="1.65"/><rect x="13" y="3.5" width="7.5" height="5.5" rx="2" stroke="currentColor" stroke-width="1.65"/><rect x="13" y="11" width="7.5" height="9.5" rx="2" fill="currentColor" fill-opacity=".1" stroke="currentColor" stroke-width="1.65"/><rect x="3.5" y="13" width="7.5" height="7.5" rx="2" stroke="currentColor" stroke-width="1.65"/></svg>',
 clips:'<svg class="ui-icon studio-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M5.5 4h13v16l-6.5-3.8L5.5 20V4Z" fill="currentColor" fill-opacity=".12" stroke="currentColor" stroke-width="1.65" stroke-linejoin="round"/><path d="M9 8.5h6M9 12h4" stroke="currentColor" stroke-width="1.65" stroke-linecap="round"/></svg>',
 recipes:'<svg class="ui-icon studio-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true"><rect x="3.5" y="4.5" width="6" height="6" rx="1.8" fill="currentColor" fill-opacity=".14" stroke="currentColor" stroke-width="1.65"/><rect x="14.5" y="4.5" width="6" height="6" rx="1.8" fill="currentColor" fill-opacity=".14" stroke="currentColor" stroke-width="1.65"/><rect x="9" y="14" width="6" height="6" rx="1.8" fill="currentColor" fill-opacity=".18" stroke="currentColor" stroke-width="1.65"/><path d="M9.5 7.5h5M12 10.5V14" stroke="currentColor" stroke-width="1.65"/></svg>',
 video:'<svg class="ui-icon studio-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true"><rect x="3" y="5" width="18" height="14" rx="2.5" fill="currentColor" fill-opacity=".1" stroke="currentColor" stroke-width="1.65"/><path d="M10 9.2l5 2.8-5 2.8V9.2Z" fill="currentColor"/></svg>',
 music:'<svg class="ui-icon studio-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M4.5 14.5v-5M8.2 17.5v-11M12 19V5M15.8 16.5v-9M19.5 14v-4" stroke="currentColor" stroke-width="1.9" stroke-linecap="round"/></svg>',
 pdf:'<svg class="ui-icon studio-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true"><rect x="4" y="3.5" width="12" height="14.5" rx="2" fill="currentColor" fill-opacity=".12" stroke="currentColor" stroke-width="1.65"/><path d="M8 18v2.5h11a1.5 1.5 0 0 0 1.5-1.5V7.5H16" stroke="currentColor" stroke-width="1.65" stroke-linejoin="round"/><path d="M7.5 8.5h5M7.5 12h4" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>',
 document:'<svg class="ui-icon studio-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M5.5 3.5h8.5l4.5 4.5v12.5h-13V3.5Z" fill="currentColor" fill-opacity=".1" stroke="currentColor" stroke-width="1.65" stroke-linejoin="round"/><path d="M14 3.5V8h4.5M8.5 12h7M8.5 15.5h5" stroke="currentColor" stroke-width="1.65" stroke-linecap="round"/></svg>',
 slide:'<svg class="ui-icon studio-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true"><rect x="3" y="4.5" width="18" height="12.5" rx="2" fill="currentColor" fill-opacity=".1" stroke="currentColor" stroke-width="1.65"/><rect x="6" y="7.5" width="5.5" height="5" rx="1" fill="currentColor" fill-opacity=".24"/><path d="M14 8.5h4M14 11.5h3M12 17v3.5M8.5 20.5h7" stroke="currentColor" stroke-width="1.65" stroke-linecap="round"/></svg>'
};
function workTone(item){
 const ext=String(item?.ext||'').toUpperCase();
 if(ext==='PPTX')return 'rose';
 if(ext==='DOCX')return 'sky';
 if(ext==='PDF')return 'amber';
 return 'indigo';
}
function projectCard(project){
 const counts=projectCounts(project),goal=projectGoalText(project),updated=projectUpdated(project);
 const statusClass=project.status==='done'?' is-done':project.status==='active'?' is-active':'';
 return `<article class="work-card work-project-card" data-project-card="${esc(project.id)}"><div class="work-project-head"><span class="work-card-icon" data-tone="indigo" aria-hidden="true">${WORKS_ICONS.project}</span><b class="work-project-title" data-user-content>${esc(project.name||t('未命名项目'))}</b><span class="work-card-status${statusClass}">${esc(projectStatusLabel(project.status))}</span></div><p class="work-project-goal" data-user-content>${esc(goal||t('还没有目标'))}</p><footer class="work-project-foot"><small class="work-project-meta">${t('资料')} ${counts.materials} · ${t('成果')} ${counts.artifacts}${updated?` · ${esc(updated)}`:''}</small><span class="works-project-actions"><button type="button" class="text-button" data-project-open="${esc(project.id)}">${t('打开项目')}</button><button type="button" class="text-button work-project-primary" data-project-continue="${esc(project.id)}">${t('继续创作')}</button></span></footer></article>`;
}
function projectSection(){
 if(state.isPrivate)return '';
 const rows=projectRows(),cards=rows.slice(0,WORK_LIMIT).map(projectCard).join('');
 if(!rows.length)return `<section class="works-continue works-projects is-empty-projects"><div class="works-projects-banner"><div class="works-projects-lead">${WORKS_HERO_ART}<div class="works-projects-copy"><h2>${t('创作项目')}</h2><p class="works-project-hint">${t('把资料、要求和成果放进同一个创作项目，之后可以从这里继续。')}</p></div></div><button type="button" class="button secondary works-new-button" data-project-new>${svg('plus')}<span>${t('新建创作项目')}</span></button></div></section>`;
 return `<section class="works-continue works-projects"><div class="works-projects-head"><h2>${t('创作项目')}</h2><div class="works-projects-actions"><small>${esc(t(rows.length+' 个创作项目'))}</small><button type="button" class="button secondary works-new-button" data-project-new>${svg('plus')}<span>${t('新建创作项目')}</span></button><button type="button" class="text-button" data-project-directory>${t('全部项目')}</button></div></div><div class="works-grid works-project-grid">${cards}</div></section>`;
}
function openProjectDirectory(){
 showSmall(`<h2>${t('全部项目')}</h2><input id="project-directory-search" class="works-source-search" type="search" autocomplete="off" aria-label="${esc(t('搜索项目'))}" placeholder="${esc(t('搜索项目'))}"><div id="project-directory-list" class="works-source-list"></div><div class="document-form-actions"><button type="button" class="text-button" data-close-small>${t('关闭')}</button><button type="button" class="button secondary" id="project-directory-more">${t('显示更多')}</button></div>`);
 const search=$('#project-directory-search'),list=$('#project-directory-list'),more=$('#project-directory-more');
 let limit=WORK_LIMIT;
 const paint=()=>{
  const query=search.value.trim().toLocaleLowerCase();
  const matches=projectRows().filter(project=>[project.name,project.goal].some(text=>String(text||'').toLocaleLowerCase().includes(query)));
  list.innerHTML=matches.slice(0,limit).map(project=>`<button type="button" class="works-source" data-project-open="${esc(project.id)}">${glyph('layers')}<span data-user-content>${esc(project.name)}</span><small>${t(projectStatusLabel(project.status))}</small></button>`).join('')||`<p class="works-source-empty">${t('没有匹配的项目。')}</p>`;
  more.hidden=matches.length<=limit;
 };
 search.oninput=()=>{limit=WORK_LIMIT;list.scrollTop=0;paint();};
 more.onclick=()=>{limit+=WORK_LIMIT;paint();if(more.hidden)search.focus();};
 paint();I18n.localize($('#small-dialog'));search.focus();
}
function projectMaterialRow(item,attribute){
 return `<button type="button" class="works-source" ${attribute}="${esc(item.id)}">${kindIcon(item.kind)}<span data-user-content>${esc(item.name)}</span><small>${esc(item.ext)}</small></button>`;
}
function projectSectionBlock(title,count,rows,attribute,empty,action=''){
 return `<section class="works-project-detail-section"><div class="project-section-heading"><h3>${t(title)} · ${count}</h3>${action}</div>${rows.length?`<div class="works-source-list">${rows.map(row=>projectMaterialRow(row,attribute)).join('')}</div>`:`<p class="dialog-description">${t(empty)}</p>`}</section>`;
}
function projectTextSection(title,text,empty){
 return `<section class="works-project-detail-section"><h3>${t(title)}</h3><p data-user-content>${esc(text||t(empty))}</p></section>`;
}
function deliveryRow(row){
 const date=new Date(row.created);
 const dateText=Number.isNaN(date.getTime())?'':date.toLocaleString();
 return `<article class="project-delivery"><div class="project-delivery-head"><b data-user-content>${esc(row.name)}</b>${row.available?`<button type="button" class="text-button" data-project-reveal="${esc(row.exportId)}">${t('在文件夹中显示')}</button>`:`<small>${t('文件可能已移动')}</small>`}</div><small data-user-content>${esc(row.source)} · ${esc(row.format)} · ${esc(dateText)} · <code title="${esc(row.sha256)}">${esc(row.sha256.slice(0,12))}</code></small><div class="project-delivery-path" data-user-content>${esc(row.path)}</div></article>`;
}
function projectDetailView(project){
 const counts=projectCounts(project),materials=(project.members||[]).map(byId).filter(Boolean),artifacts=(project.artifacts||[]).map(byId).filter(Boolean),sessions=(project.sessions||[]).filter(projectHex);
 const id=esc(project.id);
 const addArtifact=`<button type="button" class="text-button" data-project-add-artifact="${id}">${svg('plus')}${t('添加成果')}</button>`;
 return `<div id="project-detail" data-project-detail="${id}">
 <h2 data-user-content>${esc(project.name||t('未命名项目'))}</h2>
 <p class="dialog-description">${esc(projectStatusLabel(project.status))} · ${t('资料')} ${counts.materials} · ${t('成果')} ${counts.artifacts}</p>
 ${projectTextSection('目标',projectGoalText(project),'未填写目标')}
 ${projectTextSection('要求',String(project.requirements||'').trim(),'未填写要求')}
 ${projectSectionBlock('资料',counts.materials,materials,'data-project-item','还没有资料')}
 ${projectSectionBlock('成果',counts.artifacts,artifacts,'data-project-artifact','还没有成果',addArtifact)}
 <section class="works-project-detail-section"><h3>${t('交付记录')}</h3><div id="project-delivery-list" aria-live="polite"></div></section>
 <section class="works-project-detail-section"><h3>${t('会话')} · ${counts.sessions}</h3>
 ${sessions.length?`<p><small>${sessions.slice(0,12).map(id=>esc(id.slice(0,8))).join(' · ')}</small></p>`:`<p class="dialog-description">${t('暂无会话')}</p>`}
 <button type="button" class="text-button" data-project-resume="${id}">${t('继续此项目')}</button></section>
 <div class="document-form-actions"><button type="button" class="text-button" data-close-small data-project-back>${t('返回')}</button><button type="button" class="button secondary" data-project-edit="${id}">${t('编辑项目')}</button><button type="button" class="button primary" data-project-continue="${id}">${t('继续创作')}</button></div></div>`;
}
async function openProjectDetail(id,project=projectById(id)){
 if(state.isPrivate)return;
 if(!project){toast(t('找不到创作项目。'),true);return;}
 showSmall(projectDetailView(project));I18n.localize($('#small-dialog'));
 const panel=$('#project-delivery-list');
 if(!panel)return;
 try{
  const rows=await native('projectDeliveries',{projectId:id});
  if(!panel.isConnected||!$('#small-dialog').open||state.isPrivate)return;
  panel.innerHTML=rows.length?rows.map(deliveryRow).join(''):`<p class="dialog-description">${t('导出项目成果后，副本会记录在这里。')}</p>`;
 }catch{
  if(panel.isConnected)panel.textContent=t('暂时无法读取交付记录。');
 }
}
function openProjectArtifactPicker(id){
 const project=projectById(id);if(!project)return;
 const linked=new Set(project.artifacts||[]);
 showSmall(`<h2>${t('添加成果')}</h2><input id="project-artifact-search" class="works-source-search" type="search" autocomplete="off" aria-label="${esc(t('搜索名称'))}" placeholder="${esc(t('搜索名称'))}"><div id="project-artifact-picker" class="works-source-list"></div><div class="document-form-actions"><button type="button" class="text-button" data-project-open="${esc(id)}">${t('返回')}</button></div>`);
 const search=$('#project-artifact-search'),list=$('#project-artifact-picker');
 const paint=()=>{
  const query=search.value.trim().toLocaleLowerCase();
  const rows=continueItems().filter(item=>item.ownedDocument&&!linked.has(item.id)&&item.name.toLocaleLowerCase().includes(query)).slice(0,SOURCE_LIMIT);
  list.innerHTML=rows.map(item=>projectMaterialRow(item,'data-project-link-artifact')).join('')||`<p class="works-source-empty">${t('没有可添加的作品。')}</p>`;
 };
 list.onclick=async event=>{
  const button=event.target.closest('[data-project-link-artifact]');
  if(!button||list.inert)return;
  list.inert=true;
  try{
   const linked=await native('projectAddArtifact',{projectId:id,itemId:button.dataset.projectLinkArtifact});
   if(!linked){toast(t('此作品无法加入项目。'),true);return;}
   // Use the committed snapshot directly. A concurrent library refresh can supersede a cached Warehouse load.
   const snapshot=await native('workspaceState');
   const current=(snapshot.spaces||[]).find(row=>row.id===id&&row.kind==='project');
   if(list.isConnected&&$('#small-dialog').open)await openProjectDetail(id,current||null);
  }catch(error){fail(error);}finally{list.inert=false;}
 };
 paint();I18n.localize($('#small-dialog'));search.focus();
}
async function openProjectDialog(existing=null){
 const project=existing?structuredClone(existing):null,selected=new Set((project?.members||[]).filter(id=>byId(id))),projectId=project?.id||uid(),status=project?.status||'active';
 showSmall(`<h2>${t(project?'编辑项目':'新建创作项目')}</h2><form id="project-form"><label class="form-label">${t('名称')}<input id="project-name" maxlength="100" required value="${esc(project?.name||'')}"></label><label class="form-label">${t('目标')}<textarea id="project-goal" rows="3" maxlength="1000">${esc(project?.goal||'')}</textarea></label><label class="form-label">${t('要求')}<textarea id="project-requirements" rows="3" maxlength="4000">${esc(project?.requirements||'')}</textarea></label><label class="form-label">${t('状态')}<select id="project-status">${[['draft','草稿'],['active','进行中'],['done','已完成']].map(([value,label])=>`<option value="${value}" ${status===value?'selected':''}>${t(label)}</option>`).join('')}</select></label><div class="form-label">${t('资料')}<input id="project-material-search" type="search" autocomplete="off" placeholder="${t('搜索名称')}" aria-label="${t('搜索名称')}"><div id="project-material-list" class="works-source-list"></div><div id="project-materials" class="agent-context"></div></div><div class="document-form-actions"><button type="button" class="text-button" data-close-small>${t('取消')}</button><button type="submit" class="button primary" data-project-save>${t('保存')}</button></div></form>`);
 I18n.localize($('#small-dialog'));
 const search=$('#project-material-search'),list=$('#project-material-list'),chips=$('#project-materials');
 const renderChips=()=>{chips.innerHTML=[...selected].map(byId).filter(Boolean).map(item=>`<span class="agent-chip"><b data-user-content>${esc(item.name)}</b><button type="button" data-project-unpick="${esc(item.id)}" aria-label="${t('移除资料')}" title="${t('移除资料')}">${svg('close')}</button></span>`).join('');};
 const paintMaterials=()=>{
  const query=String(search.value||'').trim().toLocaleLowerCase();
  const rows=state.items.filter(item=>!query||String(item.name||'').toLocaleLowerCase().includes(query)).sort((a,b)=>String(b.modified||b.added||'').localeCompare(String(a.modified||a.added||''))).slice(0,SOURCE_LIMIT);
  list.innerHTML=rows.length?rows.map(item=>`<label class="works-source"><input type="checkbox" data-project-material="${esc(item.id)}" ${selected.has(item.id)?'checked':''}>${kindIcon(item.kind)}<span data-user-content>${esc(item.name)}</span><small>${esc(item.ext)}</small></label>`).join(''):`<p class="works-source-empty">${t('没有匹配的资料。')}</p>`;
  renderChips();
 };
 paintMaterials();search.oninput=paintMaterials;
 list.onchange=event=>{const box=event.target.closest('[data-project-material]');if(!box)return;if(box.checked)selected.add(box.dataset.projectMaterial);else selected.delete(box.dataset.projectMaterial);renderChips();};
 chips.onclick=event=>{const button=event.target.closest('[data-project-unpick]');if(!button)return;const id=button.dataset.projectUnpick;selected.delete(id);const box=list.querySelector(`[data-project-material="${CSS.escape(id)}"]`);if(box)box.checked=false;renderChips();};
 $('#project-form').onsubmit=async event=>{
  event.preventDefault();
  const name=$('#project-name').value.trim();
  if(!name){toast(t('项目名称不能为空。'),true);$('#project-name').focus();return;}
  // Keep host-linked artifacts/sessions: a task may have linked them since this card was painted.
  let latest=null;
  try{const data=await native('workspaceState');latest=(data.spaces||[]).find(space=>space.id===projectId&&space.kind==='project')||null;if(globalThis.Warehouse)globalThis.Warehouse.data=data;}
  catch{latest=projectById(projectId);}
  const value={...(project||{}),id:projectId,kind:'project',name,goal:$('#project-goal').value.trim().slice(0,1000),requirements:$('#project-requirements').value.trim().slice(0,4000),status:['draft','active','done'].includes($('#project-status').value)?$('#project-status').value:'active',updated:new Date().toISOString(),created:latest?.created||project?.created||new Date().toISOString(),view:latest?.view||project?.view||'grid',positions:latest?.positions||project?.positions||{},members:[...selected],artifacts:(latest?.artifacts||project?.artifacts||[]).filter(projectHex),sessions:(latest?.sessions||project?.sessions||[]).filter(projectHex)};
  await closeSmall();
  await saveProject(value);
  if(state.route==='works')WorksPage.render();
 };
}
function kindIcon(kind){return glyph(labels[kind]?kind:'document');}
function newButton(){return `<button type="button" class="button primary works-new-button" data-works-new>${svg('plus')}<span>${t('新建作品')}</span></button>`;}
function headingRow(count){
 const subtitle=count?t(count+' 个可以继续的草稿'):'';
 return `<div class="collection-heading works-heading">${heading(t('我的作品'),subtitle)}${count&&canCreate()?newButton():''}</div>`;
}
function workUpdated(item){
 const raw=String(item?.modified||item?.added||'');if(!raw)return '';
 const d=new Date(raw);return Number.isNaN(d.getTime())?'':d.toLocaleDateString();
}
function workCard(item){
 const draft=isDraft(item),date=workUpdated(item),tone=workTone(item),isSlide=String(item?.ext||'').toUpperCase()==='PPTX';
 const handoff=AgentUi.available()?`<button type="button" class="icon-button work-card-agent" data-work-agent="${esc(item.id)}" aria-label="${esc(t('交给 Agent'))}" title="${esc(t('交给 Agent'))}">${svg('agent')}</button>`:'';
 return `<div class="work-card-wrap" data-tone="${tone}"><button type="button" class="work-card work-editorial-card" data-work-open="${esc(item.id)}"><span class="work-card-preview" data-tone="${tone}" aria-hidden="true">${isSlide?WORK_PREVIEW_SLIDE:WORK_PREVIEW_DOC}<span class="work-preview-ext">${esc(item.ext||'DOC')}</span></span><span class="work-card-body"><span class="work-card-main"><b data-user-content>${esc(item.name)}</b><small>${workLabel(item)} · ${esc(item.ext)}${date?` · ${esc(date)}`:''}</small></span><span class="work-card-status${draft?' is-draft':''}">${t(draft?'草稿':'已创建')}</span></span></button>${handoff}</div>`;
}
function continueSection(items){
 return `<section class="works-continue"><h2>${t('继续创作')}</h2><div class="works-grid">${items.map(workCard).join('')}</div></section>`;
}
function emptyState(){
 const example=globalThis.DocumentUi?.exampleAvailable()?`<button type="button" class="button secondary" data-example-work>${WORKS_ICONS.document}${t('用示例试一次')}</button>`:'';
 return `<section class="works-empty"><span class="works-empty-icon" aria-hidden="true">${WORKS_ICONS.project}</span><h2>${t('从第一件作品开始')}</h2><p>${t('新建作品，或从已有文件开始编辑。')}</p><div class="works-example-actions">${canCreate()?newButton():''}${example}</div></section>`;
}
function toolsSection(){
 const workspace=Kok.enabled('workspace'),editing=Kok.enabled('editing');
 if(!workspace&&!editing)return '';
 const data=globalThis.Warehouse?.data||{spaces:[],clips:[],queries:[],recipes:[]};
 const tools=workspace?[['spaces','项目空间','spaces','sky',data.spaces.length],['clips','素材摘录','clips','amber',data.clips.length],['recipes','创作配方','recipes','indigo',data.recipes.length]].map(([id,label,icon,tone,count])=>`<button type="button" class="works-tool" data-works-tool="${id}" data-tone="${tone}"><span class="works-tool-badge">${WORKS_ICONS[icon]}</span><span>${t(label)}</span><small>${count}</small></button>`).join(''):'';
 const media=editing?[['video','剪辑已有视频','video','rose'],['music','剪辑已有音频','music','teal'],['pdf','整理 PDF 页面','pdf','amber'],['document','编辑已有文档','document','sky']].map(([id,label,icon,tone])=>`<button type="button" class="works-tool" data-works-media="${id}" data-tone="${tone}"><span class="works-tool-badge">${WORKS_ICONS[icon]}</span><span>${t(label)}</span></button>`).join(''):'';
 return `<section class="works-tools"><h2>${t('工具')}</h2><div class="works-tool-groups">${tools?`<div class="works-tool-group is-spaces"><h3>${t('创作空间')}</h3><div class="works-tool-grid is-cols-3">${tools}</div></div>`:''}${media?`<div class="works-tool-group is-media"><h3>${t('媒体与文档编辑')}</h3><div class="works-tool-grid is-cols-4">${media}</div></div>`:''}</div></section>`;
}
function mediaItems(kind){
 const config=MEDIA_SOURCES[kind];if(!config)return [];
 const rows=state.items.filter(config.match);
 rows.sort((a,b)=>String(b.modified||b.added||'').localeCompare(String(a.modified||a.added||'')));
 return rows;
}
function sourceRow(item){
 return `<button type="button" class="works-source" data-works-source="${esc(item.id)}">${kindIcon(item.kind)}<span data-user-content>${esc(item.name)}</span><small>${esc(item.ext)}</small></button>`;
}
async function openSourcePicker(kind){
 const config=MEDIA_SOURCES[kind];if(!config||!Kok.enabled('editing'))return;
 const placeholder=esc(t('搜索名称'));
 showSmall(`<h2>${t(config.title)}</h2><input id="works-source-search" class="works-source-search" type="search" autocomplete="off" placeholder="${placeholder}" aria-label="${placeholder}"><div id="works-source-list" class="works-source-list"></div>`);
 const list=$('#works-source-list'),search=$('#works-source-search');
 const paint=()=>{
  const query=String(search.value||'').trim().toLocaleLowerCase();
  const rows=mediaItems(kind).filter(item=>!query||String(item.name||'').toLocaleLowerCase().includes(query)).slice(0,SOURCE_LIMIT);
  list.innerHTML=rows.length?rows.map(sourceRow).join(''):`<p class="works-source-empty">${t('暂无可编辑的内容')}</p>`;
 };
 paint();search.oninput=paint;search.focus();
}
async function openSource(id){
 if(!byId(id))return;
 await closeSmall();
 await openCreativeEditor(id);
}
let worksClicksInstalled=false;
function installWorksClicks(){
 if(worksClicksInstalled)return;worksClicksInstalled=true;
 document.addEventListener('click',async event=>{
  try{
   const button=event.target.closest('[data-works-new],[data-work-open],[data-work-agent],[data-works-tool],[data-works-media],[data-works-source],[data-project-new],[data-project-open],[data-project-continue],[data-project-resume],[data-project-edit],[data-project-item],[data-project-artifact],[data-project-directory],[data-project-add-artifact],[data-project-reveal]');
   if(!button)return;
   if(button.hasAttribute('data-works-new')){if(canCreate())await DocumentUi.open();return;}
   if(button.dataset.workAgent){if(AgentUi.available())AgentUi.open({ids:[button.dataset.workAgent]});return;}
   if(button.dataset.workOpen)return await openCreativeEditor(button.dataset.workOpen);
   if(button.dataset.worksTool){if(Kok.enabled('workspace'))navigate(button.dataset.worksTool);return;}
   if(button.dataset.worksMedia)return await openSourcePicker(button.dataset.worksMedia);
   if(button.dataset.worksSource)return await openSource(button.dataset.worksSource);
   if(button.hasAttribute('data-project-new')){await openProjectDialog();return;}
   if(button.hasAttribute('data-project-directory')){openProjectDirectory();return;}
   if(button.dataset.projectAddArtifact){openProjectArtifactPicker(button.dataset.projectAddArtifact);return;}
   if(button.dataset.projectReveal){if(!state.isPrivate)await native('reveal',{id:button.dataset.projectReveal});return;}
   if(button.dataset.projectOpen){await openProjectDetail(button.dataset.projectOpen);return;}
   if(button.dataset.projectContinue||button.dataset.projectResume){await closeSmall();await AgentUi.openProject(button.dataset.projectContinue||button.dataset.projectResume);return;}
   if(button.dataset.projectEdit){
    const project=projectById(button.dataset.projectEdit);
    if(!project){toast(t('找不到创作项目。'),true);return;}
    await closeSmall();await openProjectDialog(project);return;
   }
   if(button.dataset.projectItem||button.dataset.projectArtifact){
    const id=button.dataset.projectItem||button.dataset.projectArtifact,item=byId(id);
    await closeSmall();
    if(!item){toast(t('资料已移除'),true);return;}
    if(item.ownedDocument&&['MD','TXT','DOCX'].includes(item.ext))return await openCreativeEditor(id);
    return await openItem(id,{play:false});
   }
  }catch(error){fail(error);}
 });
}
export const WorksPage=Object.freeze({
 render(){
  installWorksClicks();
  const root=$('#main');if(!root)return;
  refreshProjectData();
  const items=continueItems(),cards=items.slice(0,WORK_LIMIT);
  root.innerHTML=`<div class="works-page">${headingRow(items.length)}${projectSection()}${cards.length?continueSection(cards):emptyState()}${toolsSection()}</div>`;
 }
});

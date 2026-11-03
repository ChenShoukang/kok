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
function projectCard(project){
 const counts=projectCounts(project),goal=projectGoalText(project);
 return `<article class="work-card" data-project-card="${esc(project.id)}" style="flex-wrap:wrap"><span class="work-card-icon" aria-hidden="true">${glyph('layers')}</span><span class="work-card-main"><b data-user-content>${esc(project.name||t('未命名项目'))}</b><small data-user-content>${esc(goal||t('还没有目标'))}</small><small>${t('资料')} ${counts.materials} · ${t('成果')} ${counts.artifacts}${projectUpdated(project)?` · ${esc(projectUpdated(project))}`:''}</small></span><span class="work-card-status">${esc(projectStatusLabel(project.status))}</span><span class="works-project-actions" style="display:flex;flex-basis:100%;gap:8px;justify-content:flex-end"><button type="button" class="text-button" data-project-open="${esc(project.id)}">${t('打开项目')}</button><button type="button" class="text-button" data-project-continue="${esc(project.id)}">${t('继续创作')}</button></span></article>`;
}
function projectSection(){
 if(state.isPrivate)return '';
 const rows=projectRows(),cards=rows.slice(0,WORK_LIMIT).map(projectCard).join('');
 return `<section class="works-continue works-projects"><h2>${t('创作项目')}</h2><div class="works-projects-actions" style="display:flex;align-items:center;gap:12px;flex-wrap:wrap"><small>${esc(rows.length?t(rows.length+' 个创作项目'):t('还没有创作项目'))}</small><button type="button" class="button secondary works-new-button" data-project-new>${svg('plus')}<span>${t('新建创作项目')}</span></button></div>${cards?`<div class="works-grid">${cards}</div>`:`<p class="works-project-hint">${t('把资料、要求和成果放进同一个创作项目，之后可以从这里继续。')}</p>`}</section>`;
}
function projectMaterialRow(item,attribute){
 return `<button type="button" class="works-source" ${attribute}="${esc(item.id)}">${kindIcon(item.kind)}<span data-user-content>${esc(item.name)}</span><small>${esc(item.ext)}</small></button>`;
}
function projectSectionBlock(title,count,rows,attribute,empty){
 return `<section class="works-project-detail-section"><h3>${t(title)}${count!==null?` · ${count}`:''}</h3>${rows.length?`<div class="works-source-list">${rows.map(row=>projectMaterialRow(row,attribute)).join('')}</div>`:`<p class="dialog-description">${t(empty)}</p>`}</section>`;
}
function projectTextSection(title,text,empty){
 return `<section class="works-project-detail-section"><h3>${t(title)}</h3><p data-user-content>${esc(text||t(empty))}</p></section>`;
}
function projectDetailView(project){
 const counts=projectCounts(project),materials=(project.members||[]).map(byId).filter(Boolean),artifacts=(project.artifacts||[]).map(byId).filter(Boolean),sessions=(project.sessions||[]).filter(projectHex);
 return `<div id="project-detail" data-project-detail="${esc(project.id)}"><h2 data-user-content>${esc(project.name||t('未命名项目'))}</h2><p class="dialog-description">${esc(projectStatusLabel(project.status))} · ${t('资料')} ${counts.materials} · ${t('成果')} ${counts.artifacts}</p>${projectTextSection('目标',projectGoalText(project),'未填写目标')}${projectTextSection('要求',String(project.requirements||'').trim(),'未填写要求')}${projectSectionBlock('资料',counts.materials,materials,'data-project-item','还没有资料')}${projectSectionBlock('成果',counts.artifacts,artifacts,'data-project-artifact','还没有成果')}<section class="works-project-detail-section"><h3>${t('会话')} · ${counts.sessions}</h3>${sessions.length?`<p><small>${sessions.slice(0,12).map(id=>esc(id.slice(0,8))).join(' · ')}</small></p>`:`<p class="dialog-description">${t('暂无会话')}</p>`}<button type="button" class="text-button" data-project-resume="${esc(project.id)}">${t('继续此项目')}</button></section><div class="document-form-actions"><button type="button" class="text-button" data-close-small data-project-back>${t('返回')}</button><button type="button" class="button secondary" data-project-edit="${esc(project.id)}">${t('编辑项目')}</button><button type="button" class="button primary" data-project-continue="${esc(project.id)}">${t('继续创作')}</button></div></div>`;
}
async function openProjectDetail(id){
 const project=projectById(id);if(!project){toast(t('找不到创作项目。'),true);return;}
 showSmall(projectDetailView(project));I18n.localize($('#small-dialog'));
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
function newButton(attribute,secondary=false){return `<button type="button" class="button ${secondary?'secondary':'primary'} works-new-button" ${attribute}>${svg('plus')}<span>${t('新建作品')}</span></button>`;}
function headingRow(count){
 const subtitle=count?t(count+' 个可以继续的草稿'):t('还没有作品');
 return `<div class="collection-heading works-heading">${heading(t('我的作品'),subtitle)}${canCreate()?newButton('data-works-new'):''}</div>`;
}
function workCard(item){
 const draft=isDraft(item);
 return `<button type="button" class="work-card" data-work-open="${esc(item.id)}"><span class="work-card-icon" aria-hidden="true">${kindIcon(item.kind)}</span><span class="work-card-main"><b data-user-content>${esc(item.name)}</b><small>${workLabel(item)} · ${esc(item.ext)}</small></span><span class="work-card-status${draft?' is-draft':''}">${t(draft?'草稿':'已创建')}</span></button>`;
}
function continueSection(items){
 return `<section class="works-continue"><h2>${t('继续创作')}</h2><div class="works-grid">${items.map(workCard).join('')}</div></section>`;
}
function emptyState(){
 return `<section class="works-empty"><span class="works-empty-icon" aria-hidden="true">${glyph('layers')}</span><h2>${t('从第一件作品开始')}</h2><p>${t('新建作品，或从已有文件开始编辑。')}</p>${canCreate()?newButton('data-works-new-empty',true):''}${Kok.enabled('editing')?`<small>${t('已有视频、音频、PDF 和文档也可以从下方工具开始编辑。')}</small>`:''}</section>`;
}
function toolsSection(){
 const workspace=Kok.enabled('workspace'),editing=Kok.enabled('editing');
 if(!workspace&&!editing)return '';
 const data=globalThis.Warehouse?.data||{spaces:[],clips:[],queries:[],recipes:[]};
 const tools=workspace?[['spaces','项目空间','folder',data.spaces.length],['clips','素材摘录','edit',data.clips.length],['recipes','创作配方','layers',data.recipes.length]].map(([id,label,icon,count])=>`<button type="button" class="works-tool" data-works-tool="${id}">${glyph(icon)}<span>${t(label)}</span><small>${count}</small></button>`).join(''):'';
 const media=editing?[['video','剪辑已有视频','video'],['music','剪辑已有音频','music'],['pdf','整理 PDF 页面','document'],['document','编辑已有文档','document']].map(([id,label,icon])=>`<button type="button" class="works-tool" data-works-media="${id}">${glyph(icon)}<span>${t(label)}</span></button>`).join(''):'';
 return `<section class="works-tools"><h2>${t('工具')}</h2><div class="works-tool-grid">${tools}${media}</div></section>`;
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
   const button=event.target.closest('[data-works-new],[data-works-new-empty],[data-work-open],[data-works-tool],[data-works-media],[data-works-source],[data-project-new],[data-project-open],[data-project-continue],[data-project-resume],[data-project-edit],[data-project-item],[data-project-artifact]');
   if(!button)return;
   if(button.hasAttribute('data-works-new')||button.hasAttribute('data-works-new-empty')){if(canCreate())await DocumentUi.open();return;}
   if(button.dataset.workOpen)return await openCreativeEditor(button.dataset.workOpen);
   if(button.dataset.worksTool){if(Kok.enabled('workspace'))navigate(button.dataset.worksTool);return;}
   if(button.dataset.worksMedia)return await openSourcePicker(button.dataset.worksMedia);
   if(button.dataset.worksSource)return await openSource(button.dataset.worksSource);
   if(button.hasAttribute('data-project-new')){await openProjectDialog();return;}
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

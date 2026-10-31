'use strict';
const organizer={folders:[],busy:false,scanning:false,progress:0,total:0,preview:[],selected:new Set(),content:false,scope:'pending',status:'',error:false,api:null,excluded:new Set(),loaded:false,customSelected:new Set(),customTarget:'',groupLimit:12};
function organizerPool(){return state.items.filter(i=>organizer.scope==='all'||!i.aiFingerprint).slice(0,50);}
function organizerCandidates(){return organizerPool().filter(i=>!organizer.excluded.has(i.id));}
async function loadOrganizer(){if(!isSmartMode())return;if(!organizer.datesLoaded){await native('fileDates');organizer.datesLoaded=true;await refresh();}const [sources,config]=await Promise.all([native('sources'),native('aiConfig')]);organizer.folders=sources.folders;organizer.scanning=sources.busy;organizer.api=config;organizer.loaded=true;paintOrganizer();}
function paintOrganizer(){if(state.route==='organize'&&!state.query){$('#main').innerHTML=renderOrganizer();I18n.localize($('#main'));}}
function organizerStatus(){const el=$('#organizer-status');if(!el)return;el.textContent=organizer.status;el.classList.toggle('error',organizer.error);I18n.localize(el);const progress=$('#organizer-progress');if(progress)progress.style.transform=`scaleX(${organizer.total?organizer.progress/organizer.total:0})`;}
function renderOrganizerRaw(){
 const candidates=organizerCandidates(),organized=state.items.filter(i=>i.aiFingerprint).length,ready=!!organizer.api?.model;
 const files=organizer.preview.length?organizer.preview.map(row=>byId(row.id)).filter(Boolean):organizerPool();
 return `<section class="organizer-page" data-organize-mode="${organizerMode()}"><header class="organizer-heading"><div><span class="organizer-eyebrow">KOK / 智能资料库</span><h1>让文件，各就其位。</h1><p>自动发现，轻松归纳。</p></div><div class="organizer-emblem ${organizer.busy||organizer.scanning?'working':''}" aria-hidden="true"><span></span>${svg('layers')}</div></header>
 ${organizerModeTabs()}<div class="organizer-stats"><div><b>${organizer.folders.filter(f=>f.enabled).length}</b><span>扫描文件夹</span></div><div><b>${state.items.length}</b><span>资料库文件</span></div><div><b>${organized}</b><span>已智能整理</span></div></div>
 <div class="organizer-layout"><section class="organizer-glass organizer-sources"><div class="organizer-section-title"><div><small>01</small><h2>发现文件</h2></div><button class="icon-button" data-organizer="add" title="添加扫描文件夹" aria-label="添加扫描文件夹">${svg('plus')}</button></div><p class="organizer-caption">选择文件夹后，kok 打开时每 45 秒自动导入新增文件。</p>
 <div class="organizer-folder-list">${organizer.folders.length?organizer.folders.map(f=>`<article class="organizer-folder ${f.enabled?'':'paused'}"><div class="organizer-folder-icon">${svg('folder')}</div><div class="organizer-folder-name"><b data-user-content>${esc(f.path.replace(/[\\/]+$/,'').split(/[\\/]/).pop()||f.path)}</b><small data-user-content title="${esc(f.path)}">${esc(f.path)}</small><span>${f.enabled?'自动扫描中':'已暂停'}</span></div><div class="organizer-folder-actions"><button class="icon-button" data-source-toggle="${f.id}" title="${f.enabled?'暂停扫描':'恢复扫描'}" aria-label="${f.enabled?'暂停扫描':'恢复扫描'}">${svg(f.enabled?'pause':'play')}</button><button class="icon-button" data-source-remove="${f.id}" title="移除扫描文件夹" aria-label="移除扫描文件夹">${svg('close')}</button></div></article>`).join(''):`<button class="organizer-folder-empty" data-organizer="add">${svg('folder')}<b>从一个文件夹开始</b><span>添加音乐、电影或阅读资料所在的位置</span></button>`}</div>
 <div class="organizer-source-footer"><span class="organizer-local-dot"></span><span>原文件保持原位</span><button class="text-button" data-organizer="${organizer.scanning?'cancelScan':'scan'}" ${!organizer.folders.length?'disabled':''}>${organizer.scanning?'停止扫描':'立即扫描'}</button></div></section>
 <section class="organizer-glass organizer-ai"><div class="organizer-section-title"><div><small>02</small><h2>AI 归纳</h2></div><button class="organizer-provider" data-organizer="api">${ready?esc(organizer.api.endpoint.includes('api.deepseek.com')?'DeepSeek':organizer.api.model):'配置 API'}${svg('chevron')}</button></div><p class="organizer-caption">分类 · 标签 · 摘要，预览后应用。</p>
 <div class="organizer-preferences"><label><span>分析范围</span><select id="organizer-scope" ${organizer.busy?'disabled':''}><option value="pending" ${organizer.scope==='pending'?'selected':''}>未整理的文件</option><option value="all" ${organizer.scope==='all'?'selected':''}>全部文件重新分析</option></select></label><label><span>发送的信息</span><select id="organizer-mode" ${organizer.busy?'disabled':''}><option value="metadata" ${!organizer.content?'selected':''}>仅文件信息</option><option value="content" ${organizer.content?'selected':''}>文件信息与文字节选</option></select></label></div>
 <p class="organizer-disclosure">${organizer.content?'将文件名、所在文件夹名称与最多 1200 字的可读文字节选发送至所选 API。':'仅发送文件名、格式、大小与所在文件夹名称，不发送正文、完整路径或媒体文件。'}</p><p class="organizer-limit">每次最多分析 50 个文件；费用按服务商规则计算。相同结果会缓存。</p>
 <div class="organizer-run-row"><div class="organizer-run-state">${organizer.busy?'<span class="spinner"></span>':svg('shield')}<span>${organizer.busy?'正在生成分类与标签…':'准备就绪'}</span></div><button class="button ${organizer.busy?'secondary':'primary'}" data-organizer="${organizer.busy?'cancel':'analyze'}" ${!organizer.busy&&(!candidates.length||!organizer.loaded)?'disabled':''}>${organizer.busy?'取消分析':'开始归纳'}</button></div>
 <div class="organizer-progress-track ${organizer.busy?'running':''}"><span id="organizer-progress" style="transform:scaleX(${organizer.total?organizer.progress/organizer.total:0})"></span></div><p id="organizer-status" class="organizer-status ${organizer.error?'error':''}" role="status">${esc(organizer.status)}</p></section></div>
 ${renderLocalOrganization()}<section class="organizer-results"><div class="organizer-results-heading"><div><h2>${organizer.preview.length?'整理预览':'待整理文件'}</h2><span>${files.length} / 50</span></div>${organizer.preview.length?`<div class="organizer-apply-actions"><button class="text-button" data-organizer="discard">放弃预览</button><button class="button primary" data-organizer="apply" ${!organizer.selected.size?'disabled':''}>应用所选分类</button></div>`:''}</div>
 ${files.length?`<div class="organizer-result-grid">${files.map((file,n)=>{const row=organizer.preview.find(x=>x.id===file.id);return `<article class="organizer-result-card ${row?'has-result':''}" style="--entry-delay:${Math.min(n,8)*25}ms"><label class="organizer-file-check"><input type="checkbox" ${row?(organizer.selected.has(file.id)?'checked':''):(organizer.excluded.has(file.id)?'':'checked')} ${organizer.busy?'disabled':''} data-organizer-select="${file.id}"><span class="organizer-file-icon ${file.kind}">${svg(file.kind)}</span><span class="organizer-file-name"><b data-user-content title="${esc(file.name)}">${esc(file.name)}</b><small>${esc(file.ext)} · ${size(file.size)}</small></span></label>${row?`<div class="organizer-suggestion" data-user-content><b>${esc(row.category)}</b><p>${esc(row.summary)}</p><div class="organizer-tags">${row.tags.map(t=>`<span>${esc(t)}</span>`).join('')}</div></div>`:`<div class="organizer-file-type"><span>${labels[file.kind]}</span>${svg('arrow')}<span>等待分析</span></div>`}</article>`;}).join('')}</div>`:`<div class="organizer-complete">${svg('layers')}<h3>一切井然有序</h3><p>添加文件夹，或切换范围重新分析已有资料。</p></div>`}</section></section>`;
}
function renderOrganizer(){
 const holder=document.createElement('div');
 holder.innerHTML=renderOrganizerRaw();
 if(typeof I18n!=='undefined')I18n.localize(holder);
 return holder.innerHTML;
}
async function runOrganizer(){
 if(!isSmartMode())return;
 if(organizer.busy)return;if(!organizer.api?.model){await showAiSettings();return;}
 const ids=organizerCandidates().map(i=>i.id);if(!ids.length)return;
 organizer.busy=true;organizer.preview=[];organizer.selected.clear();organizer.progress=0;organizer.total=ids.length;organizer.status='正在生成分类与标签…';organizer.error=false;paintOrganizer();
 try{const rows=await native('organizeFiles',{ids,content:organizer.content});organizer.preview=rows;organizer.selected=new Set(rows.map(x=>x.id));organizer.progress=ids.length;organizer.status='预览已生成，选择要应用的结果。';}
 catch(error){organizer.status=error.message;organizer.error=!error.message.includes('取消');}
 finally{organizer.busy=false;paintOrganizer();}
}
function editOrganization(id){const item=byId(id);if(!item)return;showSmall(`<h2>编辑分类与标签</h2><p class="dialog-description" data-user-content>${esc(item.name)}</p><form id="organizer-edit-form" data-id="${id}"><label class="form-label">分类<input id="organizer-category" required maxlength="60" value="${esc(item.collection||'')}"></label><label class="form-label">标签<input id="organizer-tags" value="${esc((item.tags||[]).join(', '))}" placeholder="以逗号分隔，最多 8 个标签"></label><label class="form-label">摘要<textarea id="organizer-summary" maxlength="400" rows="4">${esc(item.aiSummary||'')}</textarea></label><button class="button primary" type="submit">保存</button></form>`);}
document.addEventListener('click',async event=>{
 const button=event.target.closest('button');if(!button)return;
 try{
  if(button.dataset.editOrganization){editOrganization(button.dataset.editOrganization);return;}
  if(button.dataset.sourceToggle){const folder=organizer.folders.find(x=>x.id===button.dataset.sourceToggle);await native('changeSource',{id:folder.id,enabled:!folder.enabled});await loadOrganizer();return;}
  if(button.dataset.sourceRemove){await native('changeSource',{id:button.dataset.sourceRemove});await loadOrganizer();return;}
  const action=button.dataset.organizer;if(!action)return;
  if(action==='open'){await closeSmall();navigate('organize');return;}
  if(action==='add'){const r=await native('addSource');await loadOrganizer();if(r&&!r.cancelled)await native('scanSources');}
  if(action==='scan'){button.disabled=true;const r=await native('scanSources');if(r.errors?.length){organizer.status=r.errors.join('\n');organizer.error=true;}await refresh();await loadOrganizer();}
  if(action==='cancelScan')await native('cancelSourceScan');
  if(action==='api')await showAiSettings();
  if(action==='analyze')await runOrganizer();
  if(action==='cancel'){button.disabled=true;await native('cancelOrganization');}
  if(action==='discard'){organizer.preview=[];organizer.selected.clear();paintOrganizer();}
  if(action==='apply'){button.disabled=true;const operation=await native('applyOrganization',{ids:[...organizer.selected]});if(operation.cancelled){button.disabled=false;return;}const applied=operation.patches.length;organizer.preview=[];organizer.excluded.clear();organizer.status='分类与标签已保存。';await refresh();paintOrganizer();toast('分类与标签已保存。');}
 }catch(error){organizer.status=error.message;organizer.error=true;paintOrganizer();fail(error);}
});
document.addEventListener('change',event=>{const el=event.target;if(el.id==='organizer-mode'){organizer.content=el.value==='content';organizer.preview=[];paintOrganizer();}if(el.id==='organizer-scope'){organizer.scope=el.value;organizer.excluded.clear();organizer.preview=[];paintOrganizer();}if(el.dataset.organizerSelect){if(organizer.preview.length){el.checked?organizer.selected.add(el.dataset.organizerSelect):organizer.selected.delete(el.dataset.organizerSelect);const apply=$('[data-organizer="apply"]');if(apply)apply.disabled=!organizer.selected.size;}else{el.checked?organizer.excluded.delete(el.dataset.organizerSelect):organizer.excluded.add(el.dataset.organizerSelect);const analyze=$('[data-organizer=\"analyze\"]');if(analyze)analyze.disabled=!organizerCandidates().length;}}});
document.addEventListener('submit',async event=>{if(event.target.id!=='organizer-edit-form')return;event.preventDefault();try{if((await native('editOrganization',{id:event.target.dataset.id,category:$('#organizer-category').value,tags:$('#organizer-tags').value.split(/[,，]/).map(t=>t.trim()).filter(Boolean),summary:$('#organizer-summary').value})).cancelled)return;await closeSmall();await refresh();}catch(error){fail(error);}});
Kok.onMessage(event=>{const m=event.data;
 if(m.eventName==='libraryChanged')refresh().then(()=>{if(state.route==='organize')return loadOrganizer();}).catch(fail);
 if(m.eventName==='organizationProgress'){organizer.progress=m.data.done;organizer.total=m.data.total;organizerStatus();}
 if(m.eventName==='sourceScan'){organizer.scanning=m.data.busy;if(m.data.errors?.length){organizer.status=m.data.errors.join('\n');organizer.error=true;organizerStatus();}const button=$('[data-organizer=\"scan\"],[data-organizer=\"cancelScan\"]');if(button){button.dataset.organizer=m.data.busy?'cancelScan':'scan';button.textContent=m.data.busy?'停止扫描':'立即扫描';button.disabled=false;I18n.localize(button);}$('.organizer-emblem')?.classList.toggle('working',organizer.busy||organizer.scanning);}
});

$('#small-dialog').addEventListener('close',()=>{if(state.route==='organize')loadOrganizer().catch(fail);});

'use strict';
// 我的作品 is the creation and continuation area. It only composes existing editors,
// routes and warehouse data; it never adds a native action or its own creation form.
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
function continueItems(){
 if(state.isPrivate)return [];
 const items=state.items.filter(item=>item.ownedDocument||isDraft(item));
 return items.sort((a,b)=>String(b.modified||b.added||'').localeCompare(String(a.modified||a.added||'')));
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
   const button=event.target.closest('[data-works-new],[data-works-new-empty],[data-work-open],[data-works-tool],[data-works-media],[data-works-source]');
   if(!button)return;
   if(button.hasAttribute('data-works-new')||button.hasAttribute('data-works-new-empty')){if(canCreate())await DocumentUi.open();return;}
   if(button.dataset.workOpen)return await openCreativeEditor(button.dataset.workOpen);
   if(button.dataset.worksTool){if(Kok.enabled('workspace'))navigate(button.dataset.worksTool);return;}
   if(button.dataset.worksMedia)return await openSourcePicker(button.dataset.worksMedia);
   if(button.dataset.worksSource)return await openSource(button.dataset.worksSource);
  }catch(error){fail(error);}
 });
}
export const WorksPage=Object.freeze({
 render(){
  installWorksClicks();
  const root=$('#main');if(!root)return;
  const items=continueItems(),cards=items.slice(0,WORK_LIMIT);
  root.innerHTML=`<div class="works-page">${headingRow(items.length)}${cards.length?continueSection(cards):emptyState()}${toolsSection()}</div>`;
 }
});

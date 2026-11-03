/*
 * Adapted from Pi web-ui (MIT), Copyright (c) 2025 Mario Zechner.
 * Source: earendil-works/pi @ 559a71683d93ac5ff110e89ca0966bdd2a7eeccd
 * ChatPanel.ts, components/MessageEditor.ts, tools/artifacts/MarkdownArtifact.ts.
 * Lit templates are ported to kok's DOM renderer; native files remain authoritative.
 * Full license: docs/licenses/Pi-MIT.txt.
 */
import {presentationArtifactView,presentationOutline} from './agent-presentation-view.mjs';
const BREAKPOINT=800;

// Port of MessageEditor's composition/Enter/Escape handling. kok additionally permits
// task-local steering and guards the legacy WebView IME key code.
export function piEditorKeyDown(event,{busy,send,stop}){
 if(event.isComposing||event.key==='Process'||event.keyCode===229)return;
 if(event.key==='Enter'&&!event.shiftKey){event.preventDefault();send();}
 else if(event.key==='Escape'&&busy){event.preventDefault();stop();}
}

let get=()=>({}),paint=()=>{},rich=text=>esc(text),sequence=0;
const emptyPanel=()=>({id:'',session:'',visible:false,mode:'preview',text:'',html:'',deck:null,page:0,proposal:null,loading:false,error:'',truncated:false});
const panel=emptyPanel();
const canUse=()=>isSmartMode()&&!state.isPrivate&&Kok.enabled('agent');
const artifactExt=ext=>['MD','TXT','DOCX','PPTX'].includes(ext);
// P1: creative-project artifacts are durable across sessions; conversation-derived artifacts stay
// scoped to the current transcript. Project artifacts come first and dedupe the conversation ones.
function projectArtifactItems(){
 const project=get().project;if(!project?.id)return [];
 return (Array.isArray(project.artifacts)?project.artifacts:[]).map(byId).filter(item=>item?.ownedDocument&&artifactExt(item.ext));
}
function files(){
 if(!canUse())return [];
 const project=projectArtifactItems();
 const derived=(get().messages||[]).filter(row=>row.role==='tool'&&row.status==='done'&&['create_document','replace_document_text','create_presentation','copy_presentation','edit_presentation'].includes(row.tool)).map(row=>row.result?.id);
 derived.push(...(get().sessionArtifacts||[]).map(row=>row.itemId));
 const projectIds=new Set(project.map(item=>item.id));
 const rows=[...project,...[...new Set(derived)].filter(id=>!projectIds.has(id)).map(byId).filter(item=>item?.ownedDocument&&artifactExt(item.ext))];
 return rows;
}
const current=()=>{
 if(panel.proposal)return panel.session===get().sessionId&&(get().messages||[]).some(row=>row.id===panel.proposal.id&&row.status==='pending')?{id:panel.id,name:panel.proposal.args.title||byId(panel.proposal.args.id)?.name||I18n.text('修改预览'),ext:'PPTX',proposed:true}:undefined;
 const item=files().find(row=>row.id===panel.id);
 if(!item)return undefined;
 if(panel.session===get().sessionId)return item;
 // Project artifacts survive 新建会话; other artifacts remain bound to their conversation.
 return get().project?.id&&projectArtifactItems().some(row=>row.id===item.id)?item:undefined;
};
async function load(id,{quiet=false}={}){
 const item=files().find(item=>item.id===id);if(!item)return;
 const token=++sequence,session=get().sessionId;
 if(panel.id!==id){panel.mode='preview';panel.text='';panel.html='';}
 if(panel.id!==id)panel.page=0;
 Object.assign(panel,{id,session,proposal:null,visible:true,loading:!quiet,error:''});paint();
 try{
  const source=await native(item.ext==='PPTX'?'readPresentation':'documentSource',{id});
  if(token!==sequence||session!==get().sessionId||!canUse()||!current())return;
  panel.deck=item.ext==='PPTX'?source:null;if(panel.deck)panel.page=Math.min(panel.page,source.slides.length-1);
  const text=panel.deck?presentationOutline(source):String(source.text||'');panel.truncated=text.length>80000;panel.text=text.slice(0,80000);
  panel.html=item.ext==='MD'?rich(panel.text):`<pre class="pi-artifact-plain">${esc(panel.text)}</pre>`;
 }catch(error){if(token===sequence&&session===get().sessionId&&canUse())panel.error=error.message;}
 finally{if(token===sequence){panel.loading=false;paint();}}
}
export const AgentArtifacts={
 bind(read,redraw,renderRich){get=read;paint=redraw;rich=renderRich;},
 get active(){return !!(panel.visible&&current());},
 get count(){return files().length;},
 get overlay(){return innerWidth<BREAKPOINT;},
 async open(id){const items=files();await load(id||(items.some(row=>row.id===panel.id)?panel.id:items.at(-1)?.id));if(this.active&&this.overlay&&!$('.pi-artifacts')?.contains(document.activeElement))$('[data-pi-artifact-close]')?.focus({preventScroll:true});},
 async preview(id){const call=(get().messages||[]).find(row=>row.id===id&&row.status==='pending'&&['create_presentation','edit_presentation'].includes(row.tool));if(!call)return;const token=++sequence,session=get().sessionId;Object.assign(panel,emptyPanel(),{id:'proposal:'+id,proposal:call,session,visible:true,loading:true});paint();try{const deck=await native('previewPresentation',{tool:call.tool,args:call.args});if(token!==sequence||session!==get().sessionId||!canUse()||!current())return;panel.deck=deck;panel.text=presentationOutline(deck);}catch(error){if(token===sequence)panel.error=error.message;}finally{if(token===sequence){panel.loading=false;paint();}}},
 hide(redraw=true){panel.visible=false;if(redraw)paint();},
 reset(){sequence++;Object.assign(panel,emptyPanel());},
 view(){
  const item=current();if(!panel.visible||!item)return '<aside class="pi-artifacts" hidden></aside>';
  const rows=files();
  // ChatPanel's split/overlay view and MarkdownArtifact's preview/source toggle.
  const header=item.proposed?`<span class="pi-artifact-proposal-title">${I18n.text('修改预览')} · ${esc(item.name)}</span>`:rows.map(row=>`<button type="button" data-pi-artifact="${row.id}" aria-pressed="${row.id===item.id}" title="${esc(row.name)}" data-user-content>${svg(row.ext==='PPTX'?'slide':'document')}<span>${esc(row.name)}</span></button>`).join('');
  return `<aside class="pi-artifacts" aria-label="${I18n.text('成果预览')}" data-artifact-id="${esc(item.id)}"><header><nav class="pi-artifact-tabs" aria-label="${I18n.text('任务成果')}">${header}</nav><button type="button" class="icon-button" data-pi-artifact-close aria-label="${I18n.text('关闭成果')}" title="${I18n.text('关闭成果')}">${svg('close')}</button></header><div class="pi-artifact-toolbar"><div class="pi-preview-toggle" role="group" aria-label="${I18n.text('预览方式')}">${[['preview','预览'],['source',item.ext==='PPTX'?'大纲':'原文']].map(([mode,label])=>`<button type="button" data-pi-artifact-mode="${mode}" aria-pressed="${panel.mode===mode}">${I18n.text(label)}</button>`).join('')}</div><span>${esc(item.ext)}</span><button type="button" class="icon-button" data-pi-artifact-copy aria-label="${I18n.text('复制')}" title="${I18n.text('复制')}">${svg('copy')}</button>${item.proposed?'':`<button type="button" class="icon-button" data-pi-artifact-refresh aria-label="${I18n.text('刷新')}" title="${I18n.text('刷新')}">${svg('refresh')}</button><button type="button" class="icon-button" data-pi-artifact-agent aria-label="${I18n.text('交给 Agent')}" title="${I18n.text('交给 Agent')}">${svg('agent')}</button><button type="button" class="icon-button" data-pi-artifact-edit aria-label="${I18n.text('编辑')}" title="${I18n.text('编辑')}">${svg('edit')}</button><button type="button" class="icon-button" data-agent-open-item="${item.id}" aria-label="${I18n.text('打开文档')}" title="${I18n.text('打开文档')}">${svg('external')}</button>`}</div><div class="pi-artifact-content" tabindex="0">${panel.loading?'<span class="spinner" aria-label="'+I18n.text('正在读取资料')+'"></span>':panel.error?`<div class="pi-artifact-error" role="alert" data-user-content>${esc(panel.error)}</div>`:panel.mode==='source'?`<pre class="pi-artifact-source" data-user-content>${esc(panel.text)}</pre>`:panel.deck?presentationArtifactView(panel.deck,panel.page):`<div class="agent-rich-text pi-artifact-document" data-user-content>${panel.html}</div>`}${panel.truncated?`<p class="pi-artifact-note">${I18n.text('内容较长，打开文档查看完整内容。')}</p>`:''}</div></aside>`;
 },
 preserve(old,fresh){
  const content=old.querySelector('.pi-artifact-content'),top=content?.scrollTop||0,active=old.contains(document.activeElement)?document.activeElement:null;
  const mode=active?.dataset.piArtifactMode,id=active?.dataset.piArtifact,focusKey=['data-pi-deck-page','data-pi-artifact-refresh','data-pi-artifact-agent','data-pi-artifact-edit','data-pi-artifact-copy'].find(key=>active?.hasAttribute(key)),focusValue=focusKey?active.getAttribute(focusKey):'';
  if(!old.hidden&&!fresh.hidden)fresh.style.animation='none';old.replaceWith(fresh);
  const next=fresh.querySelector('.pi-artifact-content');if(next)next.scrollTop=top;
  if(mode)fresh.querySelector(`[data-pi-artifact-mode="${mode}"]`)?.focus({preventScroll:true});
  else if(id)fresh.querySelector(`[data-pi-artifact="${CSS.escape(id)}"]`)?.focus({preventScroll:true});
  else if(focusKey)fresh.querySelector(`[${focusKey}="${CSS.escape(focusValue)}"]`)?.focus({preventScroll:true});
 }
};

document.addEventListener('click',event=>{
 const target=event.target;
 const preview=target.closest('[data-agent-preview-slides]');if(preview){AgentArtifacts.preview(preview.dataset.agentPreviewSlides).catch(fail);return;}
 const page=target.closest('[data-pi-deck-page]');if(page&&panel.deck){panel.page=Math.max(0,Math.min(panel.deck.slides.length-1,Number(page.dataset.piDeckPage)||0));paint();return;}
 if(target.closest('[data-agent-artifacts]')){if(AgentArtifacts.active)AgentArtifacts.hide();else AgentArtifacts.open().catch(fail);return;}
 const open=target.closest('[data-pi-artifact]');if(open){AgentArtifacts.open(open.dataset.piArtifact).catch(fail);return;}
 if(target.closest('[data-pi-artifact-close]')){AgentArtifacts.hide();$('[data-agent-artifacts]')?.focus({preventScroll:true});return;}
 const mode=target.closest('[data-pi-artifact-mode]');if(mode){panel.mode=mode.dataset.piArtifactMode==='source'?'source':'preview';paint();return;}
 if(target.closest('[data-pi-artifact-refresh]')){AgentArtifacts.open(panel.id).catch(fail);return;}
 if(target.closest('[data-pi-artifact-copy]')){navigator.clipboard.writeText(panel.text).then(()=>toast('回答已复制。')).catch(fail);return;}
 if(target.closest('[data-pi-artifact-agent]')&&current()){const id=panel.id,prompt=panel.deck?I18n.text('修改这份演示的第 {page} 页：').replace('{page}',String(panel.page+1)):I18n.text('修改这份文档：');if(AgentArtifacts.overlay)AgentArtifacts.hide(false);AgentUi.open({ids:[id],prompt:get().draft||prompt});return;}
 if(target.closest('[data-pi-artifact-edit]')&&current())openCreativeEditor(panel.id).catch(fail);
},true);
Kok.on('*',(_,name)=>{
 if(['privateLocking','librarySwitched'].includes(name)){AgentArtifacts.reset();return;}
 if(name==='libraryChanged'&&AgentArtifacts.active)load(panel.id,{quiet:true}).catch(fail);
},{module:'agent'});

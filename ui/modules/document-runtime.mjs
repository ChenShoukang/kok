import {PresentationUi} from './presentation-runtime.mjs';
'use strict';
let documentCreationPending=false;
const managedTextSession=s=>!!s&&s.kind==='text'&&s.item.ownedDocument&&!state.isPrivate;
const documentSaveBlocked=error=>/其他位置修改|已移动或删除|大小限制|只能直接保存|链接|私密|普通资料库/.test(error?.message||'');
// Ctrl+N belongs to the library, the knowledge graph and the document editor; viewers and media editors keep their own context.
const documentShortcutAvailable=()=>!state.isPrivate&&(Kok.enabled('documents')||Kok.enabled('presentations'))&&[...document.querySelectorAll('dialog[open]')].every(d=>d.id==='knowledge-dialog'||d.id==='creative-dialog'&&managedTextSession(creative.session));
function setDocumentStatus(s,message,error=false){if(creative.session!==s)return;const status=$('#creative-status');if(status){status.textContent=I18n.text(message);status.classList.toggle('document-save-error',error);}$('#creative-dialog')?.setAttribute('data-save-state',error?'error':s.documentSaving?'saving':s.documentDirty?'dirty':'saved');}
function documentTemplate(name,kind){const title='# '+name+'\n\n',section=text=>'## '+I18n.text(text)+'\n\n';return kind==='daily'?title+section('今日重点')+'- [ ] \n\n'+section('随手记录'):kind==='meeting'?title+section('议题')+'- \n\n'+section('讨论记录')+section('行动事项')+'- [ ] \n':kind==='reading'?title+section('摘录')+'> \n\n'+section('我的想法')+section('关联笔记'):'';}
async function showNewDocument(options={}){
 if(documentCreationPending)return;if(state.isPrivate){toast('请先返回普通资料库新建或保存文档。',true);return;}
 const formats=[['md','Markdown','md'],['docx','Word','W'],['txt','纯文本','txt'],['pptx','PowerPoint','P']].filter(([kind])=>Kok.enabled(kind==='pptx'?'presentations':'documents'));if(!formats.length)throw Error(I18n.text('此模块已停用，请在设置中重新启用。'));if(options.format&&!formats.some(([kind])=>kind===options.format))throw Error(I18n.text('此功能当前不可用。'));const initial=options.format||formats[0][0];
 if(typeof closeKnowledgeGraph==='function')await closeKnowledgeGraph();if(creative.session)await closeCreative();
 const collection=options.collection??(state.route==='collection'?state.collection:state.viewer?.collection||'');
 showSmall(`<form id="new-document-form"><h2>新建文档</h2><div class="document-type-options" role="group" aria-label="文档格式">${formats.map(([id,label,mark])=>`<button type="button" data-document-format="${id}" aria-pressed="${id===initial}"><span aria-hidden="true">${mark}</span><b>${label}</b></button>`).join('')}</div><label class="form-label">文档名称<div class="document-name-field"><input id="new-document-name" type="text" maxlength="100" autocomplete="off" required aria-label="文档名称"><span id="new-document-extension" data-user-content>.${initial}</span></div></label><label class="form-label">分类<select id="new-document-collection"><option value="">未分类</option>${state.collections.map(name=>`<option data-user-content value="${esc(name)}">${esc(name)}</option>`).join('')}</select></label><p id="new-document-error" class="document-form-error" role="alert"></p><div class="document-form-actions"><kbd>Ctrl N</kbd><button type="button" class="text-button" data-close-small>取消</button><button type="submit" class="button primary" id="create-document-submit">创建并编辑</button></div></form>`);
 const dialog=$('#small-dialog'),form=$('#new-document-form');dialog.classList.add('new-document-dialog');form.dataset.format=initial;form.querySelector('.document-type-options').insertAdjacentHTML('afterend',`<label class="form-label">模板<select id="new-document-template">${[['blank','空白文档'],['daily','日常记录'],['meeting','会议纪要'],['reading','阅读笔记']].map(([value,label])=>`<option value="${value}">${label}</option>`).join('')}</select></label>`);$('#new-document-template').value=options.template||'blank';$('#new-document-template').disabled=initial==='pptx';$('#new-document-name').value=options.name||I18n.text('未命名文档');if(collection&&!state.collections.includes(collection))$('#new-document-collection').insertAdjacentHTML('beforeend',`<option data-user-content value="${esc(collection)}">${esc(collection)}</option>`);$('#new-document-collection').value=collection;I18n.localize(dialog);$('#new-document-name').focus();$('#new-document-name').select();
 const agentEnabled=isSmartMode()&&Kok.enabled('agent');
 form.querySelector('.document-form-actions').insertAdjacentHTML('afterbegin',`<button type="button" class="button secondary" id="new-presentation-agent" ${initial==='pptx'&&agentEnabled?'':'hidden'} ${AgentUi.state.job?'disabled':''}>${svg('agent')}<span>${I18n.text('用 Agent 制作')}</span></button>`);
 form.querySelector('.document-form-actions').insertAdjacentHTML('beforebegin',`<div id="new-presentation-brief" ${initial==='pptx'&&agentEnabled?'':'hidden'}><label class="form-label">${I18n.text('主题')}<textarea id="new-presentation-topic" rows="2" maxlength="1600" placeholder="${I18n.text('这份 PPT 要讲什么？')}"></textarea></label><div class="presentation-brief-options"><label class="form-label">${I18n.text('页数')}<select id="new-presentation-pages">${[5,8,12,16,20].map(n=>`<option value="${n}" ${n===8?'selected':''}>${n}</option>`).join('')}</select></label><label class="form-label">${I18n.text('风格')}<select id="new-presentation-theme"><option value="editorial">${I18n.text('杂志')}</option><option value="paper">${I18n.text('纯白')}</option><option value="midnight">${I18n.text('墨黑')}</option><option value="violet">${I18n.text('淡紫')}</option></select></label></div></div>`);
 $('#new-presentation-agent').onclick=()=>{const button=$('#new-presentation-agent');if(button.disabled)return;const topic=$('#new-presentation-topic').value.trim();if(!topic){$('#new-presentation-topic').focus();return;}if(AgentUi.state.job)return;button.disabled=true;(async()=>{const goal=I18n.text('制作 {pages} 页 PPT（含封面），主题：{topic}。风格：{theme}。使用可编辑版式，长说明放演讲备注，先预览再确认保存。').replace('{pages}',$('#new-presentation-pages').value).replace('{topic}',topic).replace('{theme}',$('#new-presentation-theme').selectedOptions[0].textContent);await closeSmallBeforeDocuments();if(viewer.open)await closeViewer();await AgentUi.newSession();AgentUi.open({prompt:goal});await AgentUi.submit(goal);})().catch(fail).finally(()=>{button.disabled=false;});};
 form.addEventListener('click',event=>{const button=event.target.closest('[data-document-format]');if(!button||documentCreationPending)return;form.dataset.format=button.dataset.documentFormat;for(const option of form.querySelectorAll('[data-document-format]'))option.setAttribute('aria-pressed',String(option===button));$('#new-document-extension').textContent='.'+form.dataset.format;$('#new-document-template').disabled=form.dataset.format==='pptx';$('#new-presentation-agent').hidden=$('#new-presentation-brief').hidden=form.dataset.format!=='pptx'||!agentEnabled;CalmMotion.feedback(button);});
 form.addEventListener('submit',async event=>{event.preventDefault();if(documentCreationPending)return;documentCreationPending=true;const button=$('#create-document-submit');button.disabled=true;form.setAttribute('aria-busy','true');$('#new-document-error').textContent='';try{const name=$('#new-document-name').value,item=form.dataset.format==='pptx'?await PresentationUi.create(name,$('#new-document-collection').value):await native('createDocument',{name,format:form.dataset.format,collection:$('#new-document-collection').value,text:documentTemplate(name,$('#new-document-template').value)});await refresh();await closeSmallBeforeDocuments();await openCreativeEditor(item.id);}catch(error){if(form.isConnected)$('#new-document-error').textContent=I18n.text(error.message);else fail(error);}finally{documentCreationPending=false;button.disabled=false;form.removeAttribute('aria-busy');}});
}
Kok.extend('documents','documents.js:showSmallBeforeDocuments','showSmall',showSmallBeforeDocuments=>function(content){showSmallBeforeDocuments(content);$('#small-dialog').classList.remove('new-document-dialog');});
const closeSmallBeforeDocuments=Kok.snapshot('closeSmall');Kok.extend('documents','documents.js:closeSmallBeforeDocuments','closeSmall',()=>function(){if(documentCreationPending&&$('#new-document-form'))return Promise.resolve();return closeSmallBeforeDocuments();});
// Keep the existing close token on the wrapper; showSmall/closeSmall use this property.

Kok.extend('documents','documents.js:setupTextBeforeDocuments','setupCreativeText',setupTextBeforeDocuments=>async function(draft){await setupTextBeforeDocuments(draft);const s=creative.session;if(!managedTextSession(s)||!$('#creative-text'))return;
 const dialog=$('#creative-dialog');dialog.classList.add('managed-document-editor');s.documentDirty=false;s.documentRevision=0;s.documentSavedText=$('#creative-text').value;s.documentBlocked=!!s.documentRecovery;
 $('.creative-header b').setAttribute('data-user-content','');$('.creative-header b').textContent=s.item.name;$('.creative-header small').textContent=s.item.ext==='DOCX'?'Word · DOCX':s.item.ext==='TXT'?'TXT':'Markdown';$('[data-creative-save]').textContent=I18n.text('保存');$('.creative-footer>span:first-child').textContent=I18n.text('自动保存到本地');$('.creative-header-actions').insertAdjacentHTML('afterbegin',`<button class="text-button" data-new-document>${svg('plus')}<span>新建</span></button>`);
 $('[data-creative-save]').insertAdjacentHTML('beforebegin',`<button type="button" class="text-button" data-document-versions aria-label="${I18n.text('版本记录')}" title="${I18n.text('版本记录')}">${svg('clock')}<span>${I18n.text('版本')}</span></button><button type="button" class="text-button" data-agent-revise-selection disabled aria-label="${I18n.text('让 Agent 改这段')}" title="${I18n.text('让 Agent 改这段')}">${svg('agent')}<span>${I18n.text('让 Agent 改这段')}</span></button>`);
 documentSyncReviseSelection();
 if(s.item.ext==='MD'){$('#creative-text').hidden=false;$('#creative-rich-text').hidden=true;}
 const toggle=$('[data-text-source]');toggle.setAttribute('aria-pressed',String(!$('#creative-text').hidden));toggle.title=I18n.text('切换源码与编辑视图');
 setDocumentStatus(s,s.documentRecovery?'上次修改尚未写入，请重试保存或另存副本。':'已保存');I18n.localize(dialog);const input=$('#creative-text').hidden?$('#creative-rich-text'):$('#creative-text');input.focus();if(input.tagName==='TEXTAREA')input.setSelectionRange(input.value.length,input.value.length);
});
Kok.extend('documents','documents.js:openEditorBeforeDocuments','openCreativeEditor',openEditorBeforeDocuments=>async function(id){$('#creative-dialog')?.classList.remove('managed-document-editor');return openEditorBeforeDocuments(id);});
async function persistManagedDocument(s,{explicit=false}={}){
 if(!managedTextSession(s)||creative.session!==s)return;clearTimeout(s.documentTimer);
 if(s.documentSaving){await s.documentSaving;if(creative.session===s&&(s.documentDirty||explicit))return persistManagedDocument(s,{explicit});return;}
 if(!s.documentDirty&&!s.documentRecovery){setDocumentStatus(s,'已保存');return;}
 const text=$('#creative-text').value,revision=s.documentRevision,recipe=creativeRecipe();
 if(s.documentBlocked&&!explicit){await native('saveCreativeDraft',{id:s.item.id,draft:{...recipe,recovery:true}});return;}
 const save=(async()=>{try{setDocumentStatus(s,'正在保存…');const result=await native('saveDocument',{id:s.item.id,text,signature:s.signature,recipe});s.signature=result.signature;s.item.size=result.size;s.item.modified=result.modified;s.documentSavedText=text;s.documentRecovery=false;s.documentBlocked=false;s.item.editDraft={...recipe,signature:result.signature};s.documentFailures=0;s.documentDirty=s.documentRevision!==revision;setDocumentStatus(s,s.documentDirty?'有未保存的修改':'已保存');}catch(error){
  // Conflicts and invalid targets need the user's decision; anything else (a briefly locked file, a full disk) is retried with backoff.
  s.documentBlocked=documentSaveBlocked(error);s.documentFailures=(s.documentFailures||0)+1;s.documentRecovery=true;s.documentDirty=true;await native('saveCreativeDraft',{id:s.item.id,draft:{...creativeRecipe(),recovery:true}});setDocumentStatus(s,error.message,true);}finally{s.documentSaving=null;if(creative.session===s)$('#creative-dialog').dataset.saveState=s.documentBlocked||s.documentFailures?'error':s.documentDirty?'dirty':'saved';if(s.documentDirty&&!s.documentBlocked&&creative.session===s)s.documentTimer=setTimeout(()=>persistManagedDocument(s).catch(fail),s.documentFailures?Math.min(30000,2000*2**(s.documentFailures-1)):700);}})();s.documentSaving=save;await save;
}
Kok.extend('documents','documents.js:saveDraftBeforeDocuments','saveCreativeDraft',saveDraftBeforeDocuments=>async function(){const s=creative.session;if(managedTextSession(s)){await persistManagedDocument(s);return;}return saveDraftBeforeDocuments();});
Kok.extend('documents','documents.js:disposeBeforeDocuments','disposeCreative',disposeBeforeDocuments=>function(){clearTimeout(creative.session?.documentTimer);$('#creative-dialog')?.classList.remove('managed-document-editor');disposeBeforeDocuments();});
document.addEventListener('input',event=>{if(!['creative-text','creative-columns','creative-font-size'].includes(event.target.id))return;const s=creative.session;if(!managedTextSession(s))return;s.documentDirty=true;s.documentRevision++;setDocumentStatus(s,'有未保存的修改');clearTimeout(s.documentTimer);if(!s.documentBlocked)s.documentTimer=setTimeout(()=>persistManagedDocument(s).catch(fail),700);});
document.addEventListener('click',event=>{if(event.target.closest('[data-new-document]'))showNewDocument().catch(fail);const s=creative.session;if(event.target.closest('[data-creative-save]')&&managedTextSession(s)){event.preventDefault();event.stopImmediatePropagation();documentRequestExplicitSave(s).catch(fail);}},true);
document.addEventListener('click',event=>{if(event.target.closest('[data-text-source]')&&managedTextSession(creative.session))$('[data-text-source]').setAttribute('aria-pressed',String(!$('#creative-text').hidden));});
document.addEventListener('keydown',event=>{if(!(event.ctrlKey||event.metaKey)||event.altKey)return;if(event.key.toLowerCase()==='n'&&documentShortcutAvailable()){event.preventDefault();event.stopImmediatePropagation();showNewDocument().catch(fail);}else if(event.key.toLowerCase()==='s'&&managedTextSession(creative.session)){event.preventDefault();event.stopImmediatePropagation();documentRequestExplicitSave(creative.session).catch(fail);}},true);
$('#small-dialog').addEventListener('cancel',event=>{if(documentCreationPending&&$('#new-document-form')){event.preventDefault();event.stopImmediatePropagation();}},true);
const createButton=document.createElement('button');createButton.className='button secondary new-document-button';createButton.dataset.newDocument='';createButton.title='新建文档 (Ctrl N)';createButton.setAttribute('aria-label','新建文档');createButton.innerHTML=svg('edit')+'<span>新建</span>';$('.topbar .import-control').before(createButton);
// P2: managed-document version history, line diff, explicit-save confirmation and selection→Agent.
const DOCUMENT_VERSION_MAX_LINES=4000,DOCUMENT_VERSION_MAX_CHARS=380000,DOCUMENT_DIFF_MAX_ROWS=600,DOCUMENT_SELECTION_LIMIT=2000;
function documentVersionMoment(value){const text=String(value||'');if(!text)return '';const date=new Date(text);return Number.isNaN(date.getTime())?text:date.toLocaleString();}
function documentVersionName(row){return row?.current?I18n.text('当前版本'):I18n.text(String(row?.title||'').trim())||I18n.text('保存版本');}
function documentVersionSourceLabel(row){const source=String(row?.source||'').trim();if(!source)return '';if(source==='user')return I18n.text('手动保存');if(source.startsWith('agent:'))return I18n.text('Agent 修改');return source;}
function documentVersionLabelText(version){return String(version?.label||'').trim();}
function documentVersionLabelHtml(label){const text=String(label||'').trim();return text?`<span class="document-version-label" data-version-badge="${esc(text)}" title="${esc(text)}" style="flex:none;max-width:14em;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;padding:1px 7px;border-radius:999px;background:var(--green-soft);color:var(--green);font-size:11px">${esc(text)}</span>`:'';}
function documentVersionRow(row){
 const current=!!row.current,available=row.available!==false,source=documentVersionSourceLabel(row),label=current?'':documentVersionLabelText(row),operationId=String(row.operationId);
 return `<div class="document-version-item" data-version-row="${esc(operationId)}" style="display:flex;align-items:center;gap:4px;border-bottom:1px solid var(--line)"><button type="button" class="works-source" data-version-op="${esc(operationId)}" data-version-current="${current}" data-version-available="${available}" aria-pressed="false" style="flex:1;min-width:0;align-items:flex-start;border-bottom:0"><span style="flex:none;width:9.5em;color:var(--muted);font-size:11px">${esc(documentVersionMoment(row.created))}</span>${label?documentVersionLabelHtml(label):''}<span data-user-content style="flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${esc(documentVersionName(row))}${source?` · ${esc(source)}`:''}</span><small style="flex:none;color:var(--muted)">${current?I18n.text('当前'):!available?I18n.text('内容不可用'):I18n.text('可恢复')}</small></button>${current?'':`<button type="button" class="text-button" data-version-label="${esc(operationId)}" style="flex:none;font-size:11px;padding:2px 6px">${I18n.text('命名检查点')}</button>${label?`<button type="button" class="text-button" data-version-unlabel="${esc(operationId)}" style="flex:none;font-size:11px;padding:2px 6px">${I18n.text('移除检查点')}</button>`:''}`}</div>`;
}
function documentNormalizeVersionText(value){return String(value??'').replace(/\r\n?/g,'\n').slice(0,DOCUMENT_VERSION_MAX_CHARS);}
function documentDiffRows(beforeText,afterText){
 const a=documentNormalizeVersionText(beforeText).split('\n').slice(0,DOCUMENT_VERSION_MAX_LINES);
 const b=documentNormalizeVersionText(afterText).split('\n').slice(0,DOCUMENT_VERSION_MAX_LINES);
 let start=0;while(start<a.length&&start<b.length&&a[start]===b[start])start++;
 let endA=a.length,endB=b.length;while(endA>start&&endB>start&&a[endA-1]===b[endB-1]){endA--;endB--;}
 const rows=[];for(let n=0;n<start;n++)rows.push({type:'same',text:a[n],a:n+1,b:n+1});
 const midA=a.slice(start,endA),midB=b.slice(start,endB);
 if(midA.length&&midB.length&&midA.length*midB.length<=1000000){
  const n=midA.length,m=midB.length,width=m+1,cells=new Uint32Array((n+1)*width);
  for(let i=n-1;i>=0;i--)for(let j=m-1;j>=0;j--)cells[i*width+j]=midA[i]===midB[j]?cells[(i+1)*width+j+1]+1:Math.max(cells[(i+1)*width+j],cells[i*width+j+1]);
  let i=0,j=0;
  while(i<n&&j<m){if(midA[i]===midB[j]){rows.push({type:'same',text:midA[i],a:start+i+1,b:start+j+1});i++;j++;}else if(cells[(i+1)*width+j]>=cells[i*width+j+1]){rows.push({type:'del',text:midA[i],a:start+i+1});i++;}else{rows.push({type:'add',text:midB[j],b:start+j+1});j++;}}
  for(;i<n;i++)rows.push({type:'del',text:midA[i],a:start+i+1});
  for(;j<m;j++)rows.push({type:'add',text:midB[j],b:start+j+1});
 }else{
  for(let i=0;i<midA.length;i++)rows.push({type:'del',text:midA[i],a:start+i+1});
  for(let j=0;j<midB.length;j++)rows.push({type:'add',text:midB[j],b:start+j+1});
 }
 for(let n=0;n<a.length-endA;n++)rows.push({type:'same',text:a[endA+n],a:endA+n+1,b:endB+n+1});
 return {rows,truncated:a.length>=DOCUMENT_VERSION_MAX_LINES||b.length>=DOCUMENT_VERSION_MAX_LINES};
}
function documentDiffLineHtml(row){
 const added=row.type==='add',removed=row.type==='del',sign=added?'+':removed?'-':' ';
 const background=added?'background:rgba(46,160,67,.12)':removed?'background:rgba(248,81,73,.12)':'';
 const color=added?'color:#2ea043':removed?'color:#e5534b':'color:var(--muted)';
 const number=added?(row.b??''):(row.a??'');
 return `<div class="document-diff-line" data-diff="${row.type}" style="display:flex;gap:8px;padding:1px 6px;border-radius:4px;font:12px/1.65 ui-monospace,Consolas,monospace;white-space:pre-wrap;word-break:break-word;${background}"><span aria-hidden="true" style="flex:none;width:1ch;${color}">${sign}</span><span aria-hidden="true" style="flex:none;min-width:3ch;text-align:right;color:var(--muted);opacity:.6">${number}</span><span data-user-content style="flex:1;min-width:0">${esc(row.text)}</span></div>`;
}
function documentDiffBodyHtml(rows){
 const keep=new Array(rows.length).fill(false);
 rows.forEach((row,index)=>{if(row.type==='same')return;for(let n=Math.max(0,index-3);n<=Math.min(rows.length-1,index+3);n++)keep[n]=true;});
 if(!keep.some(Boolean))return `<p class="document-diff-empty" style="margin:8px 0 0;color:var(--muted);font-size:12px">${I18n.text('内容没有差异')}</p>`;
 let html='',skipped=0,shown=0;
 const flush=()=>{if(skipped){html+=`<div class="document-diff-gap" style="padding:2px 6px;color:var(--muted);font-size:11px">… ${skipped} ${I18n.text('行未显示')} …</div>`;skipped=0;}};
 for(let index=0;index<rows.length&&shown<DOCUMENT_DIFF_MAX_ROWS;index++){if(!keep[index]){skipped++;continue;}flush();html+=documentDiffLineHtml(rows[index]);shown++;}
 flush();if(shown>=DOCUMENT_DIFF_MAX_ROWS)html+=`<div class="document-diff-gap" style="padding:2px 6px;color:var(--muted);font-size:11px">… ${I18n.text('差异过长，仅显示前 600 行')} …</div>`;
 return `<div class="document-diff-body" style="max-height:min(46vh,420px);overflow:auto;border:1px solid var(--line);border-radius:8px;padding:4px;margin-top:8px">${html}</div>`;
}
function documentDiffBlock(beforeText,afterText,{title='',description='',badge=''}={}){
 if(typeof beforeText!=='string'||typeof afterText!=='string')return `<p class="dialog-description">${I18n.text('版本文本不可用，只能查看哈希和大小。')}</p>`;
 const result=documentDiffRows(beforeText,afterText),added=result.rows.filter(row=>row.type==='add').length,removed=result.rows.filter(row=>row.type==='del').length;
 return `<div class="document-diff" data-diff-stats="+${added} -${removed}">${title?`<h3 data-user-content>${esc(title)}</h3>`:''}${badge?`<p style="margin:2px 0 0">${documentVersionLabelHtml(badge)}</p>`:''}${description?`<p class="dialog-description" data-user-content>${esc(description)}</p>`:''}<p class="document-diff-summary" style="display:flex;gap:14px;align-items:baseline;margin:8px 0 0;font:12px/1.6 ui-monospace,Consolas,monospace"><span style="color:#2ea043">+${added}</span><span style="color:#e5534b">-${removed}</span>${result.truncated?`<span style="color:var(--muted);font-size:11px">${I18n.text('仅比较前 4000 行')}</span>`:''}</p>${documentDiffBodyHtml(result.rows)}</div>`;
}
async function openDocumentVersions(s){
 if(!managedTextSession(s)||creative.session!==s)return;
 showSmall(`<div id="document-versions-panel"><h2>${I18n.text('版本记录')}</h2><p class="dialog-description" data-user-content>${esc(s.item.name)}</p><div id="document-version-list"><p class="dialog-description">${I18n.text('加载版本记录…')}</p></div><div id="document-version-diff"></div><div class="document-form-actions"><button type="button" class="text-button" data-close-small>${I18n.text('关闭')}</button><button type="button" class="button primary" data-version-restore disabled>${I18n.text('恢复此版本')}</button></div></div>`);
 I18n.localize($('#small-dialog'));
 const list=$('#document-version-list'),diff=$('#document-version-diff'),restore=$('[data-version-restore]');
 let versions=[],selected=null,result;
 try{result=await native('documentVersions',{id:s.item.id});}
 catch(error){list.innerHTML=`<p class="document-form-error" role="alert">${esc(I18n.text(error.message))}</p>`;return;}
 if(creative.session!==s||!$('#document-versions-panel'))return;
 versions=Array.isArray(result?.versions)?result.versions.filter(row=>row&&(row.current||/^[a-f0-9]{32}$/.test(String(row.operationId||'')))):[];
 if(!versions.length){list.innerHTML=`<p class="dialog-description">${I18n.text('还没有可恢复的版本')}</p>`;return;}
 list.innerHTML=versions.map(documentVersionRow).join('');
 list.onclick=event=>{
  const labelButton=event.target.closest('[data-version-label]');
  if(labelButton){event.preventDefault();event.stopPropagation();const version=versions.find(row=>String(row.operationId)===labelButton.dataset.versionLabel);if(version)documentVersionLabelDialog(s,version).catch(fail);return;}
  const unlabelButton=event.target.closest('[data-version-unlabel]');
  if(unlabelButton){event.preventDefault();event.stopPropagation();const version=versions.find(row=>String(row.operationId)===unlabelButton.dataset.versionUnlabel);if(version)documentVersionUnlabel(s,version).catch(fail);return;}
  const row=event.target.closest('[data-version-op]');if(!row)return;
  selected=versions.find(version=>String(version.operationId)===row.dataset.versionOp)||null;
  for(const button of list.querySelectorAll('[data-version-op]'))button.setAttribute('aria-pressed',String(button===row));
  restore.disabled=!selected||selected.current||selected.available===false;
  diff.innerHTML=`<p class="dialog-description">${I18n.text('加载版本内容…')}</p>`;
  documentLoadVersionDiff(s,selected,diff).catch(error=>{diff.innerHTML=`<p class="document-form-error" role="alert">${esc(I18n.text(error.message))}</p>`;});
 };
 restore.onclick=()=>{if(!selected||restore.disabled)return;documentConfirmRestore(s,selected,versions).catch(fail);};
}
async function documentVersionLabelDialog(s,version){
 if(!managedTextSession(s)||creative.session!==s||!version||version.current)return;
 const existing=documentVersionLabelText(version);
 showSmall(`<div id="document-version-label-dialog"><h2>${I18n.text('命名检查点')}</h2><p class="dialog-description" data-user-content>${esc(documentVersionName(version)+(documentVersionLabelText(version)?' · '+documentVersionLabelText(version):'')+' · '+documentVersionMoment(version.created))}</p><label class="form-label">${I18n.text('检查点名称')}<input id="document-version-label-input" type="text" maxlength="64" autocomplete="off" aria-label="${I18n.text('检查点名称')}" value="${esc(existing)}"></label><p id="document-version-label-error" class="document-form-error" role="alert"></p><div class="document-form-actions"><button type="button" class="text-button" data-close-small>${I18n.text('取消')}</button><button type="button" class="button primary" data-version-label-save>${I18n.text('保存检查点')}</button></div></div>`);
 I18n.localize($('#small-dialog'));
 const input=$('#document-version-label-input'),error=$('#document-version-label-error'),save=$('[data-version-label-save]');
 input.focus();input.select();
 const submit=async()=>{
  if(save.disabled)return;
  const label=input.value.trim();
  error.textContent='';
  if(!label){error.textContent=I18n.text('检查点名称不能为空。');fail(new Error(I18n.text('检查点名称不能为空。')));input.focus();return;}
  if(label.length>64){error.textContent=I18n.text('检查点名称不能超过 64 个字符。');fail(new Error(I18n.text('检查点名称不能超过 64 个字符。')));return;}
  save.disabled=true;
  try{await native('documentVersionLabel',{id:s.item.id,operationId:String(version.operationId),label});toast(I18n.text('已命名检查点。'));if(save.isConnected)await openDocumentVersions(s);}
  catch(nativeError){save.disabled=false;error.textContent=I18n.text(nativeError.message);fail(nativeError);}
 };
 save.onclick=()=>submit().catch(fail);
 input.addEventListener('keydown',event=>{if(event.key==='Enter'){event.preventDefault();submit().catch(fail);}});
}
async function documentVersionUnlabel(s,version){
 if(!managedTextSession(s)||creative.session!==s||!version||version.current)return;
 const panel=$('#document-versions-panel');
 try{await native('documentVersionUnlabel',{id:s.item.id,operationId:String(version.operationId)});toast(I18n.text('已移除检查点。'));if(panel?.isConnected)await openDocumentVersions(s);}
 catch(error){fail(error);}
}
async function documentLoadVersionDiff(s,version,diff){
 if(!s||!version||!diff)return;
 const result=await native('documentVersion',{id:s.item.id,operationId:String(version.operationId)});
 if(creative.session!==s||!$('#document-versions-panel'))return;
 if(result?.current){
  const size=Number(result.size)||0;
  diff.innerHTML=`<h3>${I18n.text('当前版本')}</h3><p class="dialog-description">${esc(documentVersionMoment(result.created))} · ${esc(String(size))} ${I18n.text('字节')}${result.beforeHash?` · ${I18n.text('哈希')} ${esc(String(result.beforeHash).slice(0,23))}`:''}</p>${typeof result.currentText==='string'&&result.textAvailable!==false?`<div style="max-height:min(40vh,360px);overflow:auto;border:1px solid var(--line);border-radius:8px;padding:8px;margin-top:8px;white-space:pre-wrap;font:12px/1.65 ui-monospace,Consolas,monospace" data-user-content>${esc(result.currentText.slice(0,20000))}</div>`:`<p class="dialog-description">${I18n.text('版本文本不可用，只能查看哈希和大小。')}</p>`}`;
  return;
 }
 const before=typeof result?.beforeText==='string'?result.beforeText:null,after=typeof result?.afterText==='string'?result.afterText:(typeof result?.currentText==='string'?result.currentText:null);
 if(result?.textAvailable&&before!==null&&after!==null)diff.innerHTML=documentDiffBlock(before,after,{title:documentVersionName(version),badge:documentVersionLabelText(version),description:`${documentVersionMoment(result.created)} · ${I18n.text('保存前 → 保存后')}`});
 else{
  const size=Number(result?.size)||0;
  diff.innerHTML=`<h3 data-user-content>${esc(documentVersionName(version))}</h3>${documentVersionLabelText(version)?`<p style="margin:2px 0 0">${documentVersionLabelHtml(documentVersionLabelText(version))}</p>`:''}<p class="dialog-description">${I18n.text('版本文本不可用，只能查看哈希和大小。')}</p><p class="dialog-description" data-user-content>${esc(String(result?.beforeHash||''))} → ${esc(String(result?.afterHash||''))} · ${esc(String(size))} ${I18n.text('字节')}</p>`;
 }
}
async function documentConfirmRestore(s,version,versions){
 if(!s||!version)return;
 const current=versions.find(row=>row.current);
 showSmall(`<div id="document-version-confirm"><h2>${I18n.text('确认恢复版本')}</h2><p class="dialog-description">${I18n.text('将恢复到该版本保存前的内容；当前内容会保留在操作历史中，仍可撤销。')}</p><p data-user-content>${esc(I18n.text('恢复目标')+': '+documentVersionName(version)+(documentVersionLabelText(version)?' · '+documentVersionLabelText(version):'')+' · '+documentVersionMoment(version.created))}</p><p data-user-content>${esc(I18n.text('当前版本')+': '+documentVersionMoment(current?.created))}</p><div class="document-form-actions"><button type="button" class="text-button" data-version-restore-cancel>${I18n.text('取消')}</button><button type="button" class="button primary" data-version-restore-confirm>${I18n.text('确认恢复')}</button></div></div>`);
 I18n.localize($('#small-dialog'));
 $('[data-version-restore-cancel]').onclick=()=>openDocumentVersions(s).catch(fail);
 $('[data-version-restore-confirm]').onclick=async event=>{
  const button=event.currentTarget;button.disabled=true;
  try{const result=await native('documentVersionRestore',{id:s.item.id,operationId:String(version.operationId)});await closeSmall();await documentApplyRestoredVersion(s,result);}
  catch(error){button.disabled=false;fail(error);}
 };
}
async function documentApplyRestoredVersion(s,result){
 if(creative.session!==s)return;
 let text=typeof result?.currentText==='string'?result.currentText:null,signature=null;
 try{const source=await native('documentSource',{id:s.item.id});if(typeof source?.text==='string')text=source.text;signature=source?.signature||null;s.documentRecovery=!!source?.recovery;}catch{}
 const textarea=$('#creative-text');if(textarea&&text!==null)textarea.value=text;
 const rich=$('#creative-rich-text');if(rich&&text!==null&&typeof renderCreativeMarkdown==='function')rich.innerHTML=renderCreativeMarkdown(text);
 if(text!==null)s.documentSavedText=text;
 if(signature)s.signature=signature;
 if(result&&Number.isFinite(Number(result.size)))s.item.size=Number(result.size);
 if(result?.modified)s.item.modified=String(result.modified);
 clearTimeout(s.documentTimer);s.documentSaving=null;s.documentDirty=false;s.documentBlocked=false;s.documentFailures=0;s.documentRevision=(s.documentRevision||0)+1;
 if(typeof updateCreativeText==='function')updateCreativeText();
 setDocumentStatus(s,'已保存');
 try{await refresh();}catch{}
 if(text===null)toast(I18n.text('版本已恢复，但文本预览不可用，请重新打开编辑器。'));
 else toast(I18n.text('已恢复到该版本'));
}
async function documentRequestExplicitSave(s){
 if(!managedTextSession(s)||creative.session!==s)return;
 const textarea=$('#creative-text'),text=textarea?textarea.value:(s.documentSavedText??'');
 if(text===s.documentSavedText){await persistManagedDocument(s,{explicit:true});return;}
 clearTimeout(s.documentTimer);
 showSmall(`<div id="document-save-diff"><h2>${I18n.text('保存前确认')}</h2><p class="dialog-description">${I18n.text('已保存的内容将变为当前编辑器中的内容。')}</p>${documentDiffBlock(s.documentSavedText??'',text)}<div class="document-form-actions"><button type="button" class="text-button" data-close-small data-save-diff-cancel>${I18n.text('取消')}</button><button type="button" class="button primary" data-save-diff-confirm>${I18n.text('确认保存')}</button></div></div>`);
 I18n.localize($('#small-dialog'));
 const confirm=$('[data-save-diff-confirm]');
 confirm.onclick=async event=>{if(creative.session!==s)return;const button=event.currentTarget;button.disabled=true;await closeSmall();await persistManagedDocument(s,{explicit:true});};
}
function documentSelectionText(){
 const textarea=$('#creative-text');
 if(textarea&&!textarea.hidden){const start=Number(textarea.selectionStart),end=Number(textarea.selectionEnd);if(Number.isFinite(start)&&Number.isFinite(end)&&end>start)return textarea.value.slice(start,end);}
 const rich=$('#creative-rich-text');
 if(rich&&!rich.hidden){const selection=window.getSelection();if(selection&&selection.rangeCount&&!selection.isCollapsed){const range=selection.getRangeAt(0),node=range.commonAncestorContainer,element=node.nodeType===1?node:node.parentElement;if(element&&rich.contains(element))return selection.toString();}}
 return '';
}
function documentSyncReviseSelection(){const button=$('[data-agent-revise-selection]');if(!button)return;button.disabled=!documentSelectionText().trim();}
async function documentReviseSelection(s){
 if(!managedTextSession(s)||creative.session!==s)return;
 const selection=documentSelectionText().trim();
 if(!selection){toast(I18n.text('请先选择要修改的文字。'),true);return;}
 if(selection.length>DOCUMENT_SELECTION_LIMIT){toast(I18n.text('选中的片段过长，请先缩短到 2000 字以内。'),true);return;}
 if(!isSmartMode()||!Kok.enabled('agent')){toast(I18n.text('请先在设置中启用智能模式，才能让 Agent 修改这段。'),true);return;}
 if(AgentUi.state.job){toast(I18n.text('请先停止当前任务。'),true);return;}
 const current=$('#creative-text')?.value;
 if(s.documentDirty||s.documentRecovery||s.documentBlocked||(typeof current==='string'&&current!==s.documentSavedText)){toast(I18n.text('请先保存当前修改，再让 Agent 修改这段。'),true);return;}
 const quote=selection.split('\n').map(line=>'> '+line).join('\n');
 const prompt=[I18n.text('请只修改下面这段选中文字，文档其余内容必须保持原样。'),'',I18n.text('文档：')+s.item.name,I18n.text('文档 ID：')+s.item.id,'',I18n.text('选中原文：'),quote,'',I18n.text('要求：'),'- '+I18n.text('只修改这段文字，保留其他段落、章节和格式不变。'),'- '+I18n.text('使用 replace_document_text 工具修改；old_text 必须是上面的原文片段（必要时带上相邻上下文以保证唯一），new_text 是修改后的这段文字。'),'- '+I18n.text('不要修改其他内容，不要新建文档。'),'',I18n.text('请先说明修改思路和替换片段，等我确认后再写入文件。')].join('\n');
 await closeCreative();
 AgentUi.open({prompt,ids:[s.item.id]});
 toast(I18n.text('已把选中的文字交给 Agent。'));
}
document.addEventListener('click',event=>{
 if(event.target.closest('[data-document-versions]')&&managedTextSession(creative.session)){event.preventDefault();event.stopImmediatePropagation();openDocumentVersions(creative.session).catch(fail);return;}
 if(event.target.closest('[data-agent-revise-selection]')&&managedTextSession(creative.session)){event.preventDefault();event.stopImmediatePropagation();documentReviseSelection(creative.session).catch(fail);}
},true);
document.addEventListener('mousedown',event=>{if(event.target.closest('[data-agent-revise-selection]'))event.preventDefault();},true);
for(const type of ['select','keyup','mouseup','input','focus'])document.addEventListener(type,event=>{if(event.target?.id==='creative-text'||event.target?.closest?.('#creative-rich-text'))documentSyncReviseSelection();},true);
document.addEventListener('selectionchange',()=>{if(creative.session&&managedTextSession(creative.session))documentSyncReviseSelection();});

Kok.extend('core','documents:creation-visibility','renderNav',previous=>function(){const result=previous();createButton.classList.toggle('hidden',!!state.isPrivate||!Kok.enabled('documents')&&!Kok.enabled('presentations'));return result;});

// Explicit adapter for the remaining legacy shell; implementation helpers stay local.
export const DocumentUi=Object.freeze({open:showNewDocument,isManaged:managedTextSession});
globalThis.DocumentUi=DocumentUi;

import {AgentPlatformUi} from './agent-platform.mjs';
import {AgentOperations} from './agent-operations.mjs';
// Migrated intact from agent-ui.js
'use strict';
// Interaction patterns: assistant-ui tool groups/sources/reviewable diffs, OpenHands event grouping,
// and Cline discussion/action separation. Original DOM implementation for kok; no framework runtime.
const agentGroupOpen=new Map();
function agentRichText(value){
 const inline=text=>esc(text).replace(/`([^`\n]+)`/g,'<code>$1</code>').replace(/\*\*([^*\n]+)\*\*/g,'<strong>$1</strong>');
 let result='',code=null,list=false;
 const closeList=()=>{if(list){result+='</ul>';list=false;}};
 for(const line of String(value||'').split('\n')){
  if(/^\s*```/.test(line)){closeList();if(code===null)code=[];else{result+='<pre><code>'+esc(code.join('\n'))+'</code></pre>';code=null;}continue;}
  if(code!==null){code.push(line);continue;}
  const bullet=/^\s*[-*]\s+(.+)$/.exec(line),heading=/^#{1,6}\s+(.+)$/.exec(line);
  if(bullet){if(!list){result+='<ul>';list=true;}result+='<li>'+inline(bullet[1])+'</li>';continue;}
  closeList();result+=heading?'<h3>'+inline(heading[1])+'</h3>':line.trim()?'<p>'+inline(line)+'</p>':'<div class="agent-paragraph-gap"></div>';
 }
 closeList();if(code!==null)result+='<pre><code>'+esc(code.join('\n'))+'</code></pre>';return result;
}
function agentSourceButtons(ids){return [...new Set(ids||[])].filter(id=>/^[a-f0-9]{32}$/.test(id)&&byId(id)).slice(0,24).map(id=>`<button type="button" class="agent-source" data-agent-open-item="${id}" title="${esc(agentItemName(id))}" data-user-content>${svg(byId(id).kind)}<span>${esc(agentItemName(id))}</span></button>`).join('');}
function agentAnswer(m){
 const actions=m.meta?'':`<div class="agent-answer-actions"><button type="button" class="text-button" data-agent-copy="${m.id}">${I18n.text('复制')}</button>${m.savedId&&byId(m.savedId)?`<button type="button" class="text-button" data-agent-open-item="${m.savedId}">${I18n.text('打开已保存')}</button>`:`<button type="button" class="text-button" data-agent-save-answer="${m.id}" ${agent.job?'disabled':''}>${I18n.text('保存回答')}</button>`}</div>`;
 return `<div class="agent-msg agent-reply ${m.meta?'agent-meta':''}"><span class="agent-led" aria-hidden="true"></span><div class="agent-answer"><div class="agent-rich-text" data-user-content>${agentRichText(m.text)}</div>${m.sourceIds?.length?`<div class="agent-sources" aria-label="${I18n.text('已读取来源')}"><small>${I18n.text('已读取来源')}</small>${agentSourceButtons(m.sourceIds)}</div>`:''}${m.interrupted?`<small class="agent-interrupted" role="status">${I18n.text('已中断 · 本轮操作未执行')}</small>`:''}${actions}</div></div>`;
}
function agentEntries(){
 for(const m of agent.messages)m.id||=uid();
 return AgentModel.groups(agent.messages.filter(m=>!(['update_plan','finish_task'].includes(m.tool)&&m.status==='done'))).map(group=>{
  const first=group.items[0];if(group.kind==='message')return `<div class="agent-entry" data-entry="${first.id}">${agentMessage(first)}</div>`;
  const active=group.items.some(m=>['queued','running'].includes(m.status)),failed=group.items.some(m=>m.status==='failed');
  const done=group.items.filter(m=>m.status==='done').length;
  const open=agentGroupOpen.has(first.id)?agentGroupOpen.get(first.id):active;
  return `<div class="agent-entry" data-entry="reads-${first.id}"><details class="agent-tool-group" ${open?'open':''} data-group-id="${first.id}"><summary><span class="agent-step-dot" data-state="${failed?'failed':active?'running':'done'}"></span><span>${I18n.text(active?'正在读取资料':'资料读取步骤')}</span><b>${done} / ${group.items.length}</b>${svg('chevron')}</summary><div class="agent-tool-group-content">${group.items.map(agentMessage).join('')}</div></details></div>`;
 }).join('');
}
function agentBatch(){
 const pending=agent.messages.filter(m=>m.status==='pending');if(pending.length<2)return '';
 const rows=pending.filter(m=>!['mcp_call','delegate_task','run_code'].includes(m.tool)),selected=rows.filter(m=>m.selected!==false).length,external=pending.length-rows.length;
 return `<div class="agent-batch"><span>${pending.length} ${I18n.text('项待确认')}${external?' · '+I18n.text(pending.some(m=>m.tool==='mcp_call')?'外部调用请逐项确认':'计算和子任务需逐项确认'):''}</span>${rows.length?`<button type="button" class="text-button" data-agent-toggle-selection>${I18n.text(selected===rows.length?'取消全选':'全选')}</button><button type="button" class="button primary" data-agent-run-selected ${!selected?'disabled':''}>${I18n.text('执行所选')} · ${selected}</button>`:''}<button type="button" class="button secondary" data-agent-skip-all>${I18n.text('全部跳过')}</button></div>`;
}
function agentMetadataDiff(m){
 if(!m.changes?.length)return '';
 return `<details class="agent-diff agent-metadata-diff"><summary>${I18n.text('查看修改前后')} · ${m.changes.length}</summary><div>${m.changes.map(c=>{const key=Object.keys(c.after)[0],text=v=>Array.isArray(v)?v.map(t=>'#'+t).join(' '):String(v||I18n.text('未分类'));return `<section class="agent-diff-row"><b data-user-content>${esc(agentItemName(c.id))}</b><div><span>${I18n.text('修改前')}</span><del data-user-content>${esc(text(c.before[key]))}</del></div><div><span>${I18n.text('修改后')}</span><ins data-user-content>${esc(text(c.after[key]))}</ins></div></section>`;}).join('')}</div></details>`;
}
function agentUpdateScroll(){const stream=$('#agent-stream'),button=$('[data-agent-latest]');if(!stream||!button)return;button.hidden=stream.scrollHeight-stream.scrollTop-stream.clientHeight<90;}
function agentChromeEqual(before,after){
 const normalize=element=>{const clone=element.cloneNode(true);for(const select of clone.querySelectorAll('select'))for(const key of ['aria-expanded','aria-controls','aria-activedescendant'])select.removeAttribute(key);return clone.outerHTML;};
 return normalize(before)===normalize(after);
}
function agentRestoreFocus(previous){
 if(!previous||previous.isConnected)return;
 for(const key of ['agentSelect','agentRun','agentSkip','agentUndo','agentCopy','agentSaveAnswer']){
  const value=previous.dataset?.[key];if(!value)continue;const attribute='data-'+key.replace(/[A-Z]/g,c=>'-'+c.toLowerCase());
  const next=$(`[${attribute}="${CSS.escape(value)}"]`)||$(`[data-agent-card="${CSS.escape(value)}"]`);
  if(next){if(!next.matches('button,input'))next.tabIndex=-1;next.focus({preventScroll:true});}return;
 }
}
async function agentSaveAnswer(id){
 const answer=agent.messages.find(m=>m.id===id&&m.role==='agent');if(!answer||agent.job||!agentAvailable())return;
 const session=agent.sessionId;
 showSmall(`<h2>${I18n.text('保存回答')}</h2><form id="agent-answer-form"><label class="form-label">${I18n.text('文档名称')}<input id="agent-answer-name" maxlength="100" required value="${esc(I18n.text('Agent 摘录'))}"></label><label class="form-label">${I18n.text('格式')}<select id="agent-answer-format"><option value="md">Markdown</option><option value="docx">Word</option></select></label><pre class="agent-save-preview" data-user-content>${esc(answer.text)}</pre><div class="document-form-actions"><button type="button" class="text-button" data-close-small>${I18n.text('取消')}</button><button type="submit" class="button primary">${I18n.text('保存')}</button></div></form>`);
 I18n.localize($('#small-dialog'));$('#agent-answer-name').select();let saving=false,card=null;
 $('#agent-answer-form').onsubmit=async event=>{event.preventDefault();if(saving||session!==agent.sessionId||!agentAvailable())return;saving=true;const button=event.target.querySelector('[type=submit]');button.disabled=true;
  try{const ids=(answer.sourceIds||[]).filter(id=>byId(id)),sources=ids.map(id=>'- '+agentItemName(id)).join('\n');const args={name:$('#agent-answer-name').value.trim(),format:$('#agent-answer-format').value,collection:'',text:answer.text+(sources?'\n\n'+I18n.text('已读取来源')+'\n'+sources:'')};
   card={id:uid(),role:'tool',tool:'create_document',args,why:I18n.text('保存回答'),status:'running'};agent.messages.push(card);
   {const item=await native('createDocument',{name:args.name,format:args.format,collection:'',text:args.text});card.result={id:item.id};card.undoCreated={id:item.id,stamp:await native('agentCreatedStamp',{id:item.id})};card.undo=()=>native('agentUndoCreated',card.undoCreated);}card.status='done';answer.savedId=card.result.id;await refresh();await closeSmall();agentPaint();toast('回答已保存。');
  }catch(error){if(card){card.status='failed';card.error=error.message;}agentPaint({scroll:false});fail(error);}finally{saving=false;button.disabled=false;}
 };
}
document.addEventListener('scroll',event=>{if(event.target.id==='agent-stream')agentUpdateScroll();},true);
document.addEventListener('change',event=>{const box=event.target.closest('[data-agent-select]');if(!box)return;const m=agent.messages.find(m=>m.id===box.dataset.agentSelect);if(m?.status==='pending'){m.selected=box.checked;agentPaint({scroll:false});}},true);
document.addEventListener('click',event=>{
 const t=event.target;
 const summary=t.closest('.agent-tool-group>summary');if(summary){const details=summary.parentElement;agentGroupOpen.set(details.dataset.groupId,!details.open);}
 if(t.closest('[data-agent-latest]')){const stream=$('#agent-stream');stream.scrollTo({top:stream.scrollHeight,behavior:reducedMotion()?'instant':'smooth'});$('#agent-input')?.focus({preventScroll:true});return;}
 if(t.closest('[data-agent-toggle-selection]')){const rows=agent.messages.filter(m=>m.status==='pending'&&!['mcp_call','delegate_task','run_code'].includes(m.tool)),value=!rows.every(m=>m.selected!==false);rows.forEach(m=>m.selected=value);agentPaint({scroll:false});return;}
 if(t.closest('[data-agent-run-selected]')){const ids=agent.messages.filter(m=>m.status==='pending'&&!['mcp_call','delegate_task','run_code'].includes(m.tool)&&m.selected!==false).map(m=>m.id);(async()=>{for(const id of ids)await agentDecide(id,true,true);})().catch(fail);return;}
 const copy=t.closest('[data-agent-copy]');if(copy){const m=agent.messages.find(m=>m.id===copy.dataset.agentCopy);if(m)navigator.clipboard.writeText(m.text).then(()=>toast('回答已复制。')).catch(fail);return;}
 const save=t.closest('[data-agent-save-answer]');if(save){agentSaveAnswer(save.dataset.agentSaveAnswer).catch(fail);return;}
 const retry=t.closest('[data-agent-edit-request]');if(retry){const m=agent.messages.find(m=>m.id===retry.dataset.agentEditRequest);if(m&&!agent.job){agent.draft=m.goal||'';agentPaint({scroll:false});const input=$('#agent-input');if(input){input.value=agent.draft;input.focus();input.setSelectionRange(0,input.value.length);}}}
},true);


// Migrated intact from agent.js
'use strict';
// v0.7.2 Agent: the home of kok in Smart mode. The model proposes; read tools run on their own and every
// write is a card the user executes, skips or undoes. Nothing touches original files or the network beyond
// the configured model endpoint.
Object.assign(paths,{
 agent:'M12 3v3 M12 18v3 M3 12h3 M18 12h3 M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Z',
 send:'M5 12h13 M13 6l6 6-6 6',
 stop:'M7 7h10v10H7Z',
 undo:'M9 14 4 9l5-5 M4 9h11a5 5 0 0 1 0 10h-3',
 tag:'M3 12V4h8l9 9-8 8-9-9Z M7.5 7.5h.01',
 check:'m5 12 5 5 9-10',
 slide:'M3 5h18v12H3Z M8 21h8 M12 17v4'
});
const AGENT_TOOLS={
 search_library:{icon:'search',label:'搜索资料库'},read_items:{icon:'document',label:'阅读资料'},list_collections:{icon:'folder',label:'查看分类'},
 set_collection:{icon:'folder',label:'移入分类'},add_tags:{icon:'tag',label:'添加标签'},write_note:{icon:'edit',label:'写入笔记'},
 create_document:{icon:'document',label:'新建文档'},create_presentation:{icon:'slide',label:'新建演示文稿'},open_item:{icon:'external',label:'打开文件'}
};
const AGENT_READ=['search_library','read_items','list_collections'];
const AGENT_STARTERS=[['整理最近导入的资料，按主题建议分类和标签','layers'],['总结一个分类里的资料，给出要点','list'],['把选中的资料整理成一篇 Markdown 笔记','edit'],['用选中的资料做一份 5 页的演示文稿','slide']];
const agent={...AgentPlatformUi.defaults,messages:[],context:[],budget:14000,job:null,history:[],draft:'',sessionId:uid(),sessions:[],loaded:false,saveTimer:null,scope:'all',collection:'',skill:'auto',requestLimit:6,costLimit:0,inputRate:0,outputRate:0,usage:null,phase:'',queue:[],options:false,lastScope:'',intent:'assist',protocol:'auto',watch:'',recovery:null,handoff:null};

AgentPlatformUi.bind(()=>agent,()=>agentPaint({scroll:false}));
function agentAvailable(){return isSmartMode()&&!state.isPrivate&&Kok.enabled('agent');}
async function agentPresence(foreground){
 if(!agent.job?.id||!agentAvailable())return;
 await native('agentTaskPresence',{taskId:agent.job.id,foreground:foreground??(state.route==='agent'&&!document.hidden&&document.hasFocus())});
}
if(!location.search.includes('preview=')){
 setInterval(()=>agentPresence().catch(()=>{}),8000);
 window.addEventListener('blur',()=>agentPresence(false).catch(()=>{}));
 window.addEventListener('focus',()=>agentPresence().catch(()=>{}));
 document.addEventListener('visibilitychange',()=>agentPresence().catch(()=>{}));
 Kok.on('*',(_,name)=>{if(['navigated','librarySwitched','modulesChanged'].includes(name))agentPresence(false).catch(()=>{});},{module:'agent'});
}
function agentSnapshot(){
 return {approvalPolicy:agent.approvalPolicy,semanticCompression:agent.semanticCompression,contextTokens:agent.contextTokens,useMemory:agent.useMemory,allowDelegation:agent.allowDelegation,allowSandbox:agent.allowSandbox,allowedTools:agent.allowedTools,taskId:agent.watch||'',protocol:agent.protocol,handoff:agent.handoff,id:agent.sessionId,title:(agent.messages.find(m=>m.role==='user')?.text||agent.draft||I18n.text('新任务')).slice(0,48),updated:new Date().toISOString(),messages:AgentModel.storedMessages(agent.messages),history:agent.history.slice(-40),draft:agent.draft,context:agent.context.slice(0,24),scope:agent.scope,collection:agent.collection,budget:agent.budget,skill:agent.skill,requestLimit:agent.requestLimit,costLimit:agent.costLimit,inputRate:agent.inputRate,outputRate:agent.outputRate,intent:agent.intent,usage:agent.usage,queue:agent.queue.slice(0,5),lastScope:agent.lastScope};
}
function agentSessionText(){
 const row=agentSnapshot();agent.sessions=[row,...agent.sessions.filter(s=>s.id!==row.id)].slice(0,12);
 // Evict oldest sessions before exceeding the bounded encrypted store; keep current content intact.
 while(agent.sessions.length>1&&new TextEncoder().encode(JSON.stringify(agent.sessions)).length>1300000)agent.sessions.pop();
 return JSON.stringify(agent.sessions);
}
window.kokAgentSession=()=>!state.isPrivate&&agent.loaded?agentSessionText():null;
let agentSaveFlight=null,agentPersistedContent=null;
async function agentSave(){
 clearTimeout(agent.saveTimer);if(!agent.loaded||!Kok.enabled('agent')||state.isPrivate||typeof vaultUi!=='undefined'&&vaultUi.switching||location.search.includes('preview='))return;
 // Serialize writes and coalesce paints with unchanged content. A caller queued
 // behind a write takes a fresh draft snapshot after that write finishes.
 if(agentSaveFlight){await agentSaveFlight;return agentSave();}
 const text=agentSessionText(),content=JSON.stringify(agent.sessions.map(({updated,...row})=>row));
 if(content===agentPersistedContent)return;
 const write=(async()=>{try{await native('saveAgentSessions',{text});agentPersistedContent=content;agent.saveError=false;}catch(error){if(!agent.saveError){agent.saveError=true;fail(error);}}})();
 agentSaveFlight=write;try{await write;}finally{if(agentSaveFlight===write)agentSaveFlight=null;}
}
function agentSaveSoon(){clearTimeout(agent.saveTimer);agent.saveTimer=setTimeout(agentSave,250);}
async function agentLoad(){
 if(agent.loaded||location.search.includes('preview='))return;
 try{const rows=JSON.parse(await native('agentSessions'));if(!Array.isArray(rows))throw Error(I18n.text('Agent 会话无效。'));agent.sessions=rows;agent.loaded=true;if(rows.length)agentRestore(rows[0].id);await agentAttach();}
 catch(error){agent.storageBlocked=true;fail(error);}
}
function agentRestore(id){
 const row=agent.sessions.find(s=>s.id===id);if(!row)return;
 for(const key of ['draft','collection','skill','lastScope'])agent[key]=String(row[key]||'');
 agent.watch=row.taskId||'';agent.recovery=null;agent.handoff=row.handoff||null;agent.protocol=['auto','tools','json'].includes(row.protocol)?row.protocol:'auto';agent.sessionId=row.id;agent.messages=AgentModel.storedMessages(row.messages||[]);agent.messages.forEach(m=>{if(m.nativeCall)m.undo=agentNativeUndo(m);else if(m.undoChanges)m.undo=()=>agentApply(m.undoChanges);else if(m.undoCreated)m.undo=()=>native('agentUndoCreated',m.undoCreated);});agent.context=(row.context||[]).filter(id=>byId(id)).slice(0,24);agent.history=row.history||[];agent.scope=['all','selected','collection'].includes(row.scope)?row.scope:'all';agent.budget=[6000,14000,24000].includes(row.budget)?row.budget:14000;
 for(const key of ['requestLimit','costLimit','inputRate','outputRate']){const n=Number(row[key]);agent[key]=Number.isFinite(n)&&n>=0&&n<=10000?n:(key==='requestLimit'?6:0);}agent.requestLimit=Math.max(1,Math.min(24,Math.floor(agent.requestLimit)));
 for(const key of ['approvalPolicy','semanticCompression','contextTokens','useMemory','allowDelegation','allowSandbox','allowedTools'])agent[key]=row[key]??AgentPlatformUi.defaults[key];
 agent.intent=row.intent==='discuss'?'discuss':'assist';agent.usage=row.usage||null;agent.phase='';agent.queue=(row.queue||[]).filter(t=>typeof t==='string'&&t.trim()).map(t=>t.slice(0,2000)).slice(0,5);
}
async function agentNew(id){
 if(agent.job)return;agentSessionText();if(id)agentRestore(id);else{agent.sessionId=uid();agent.messages=[];agent.history=[];agent.draft='';agent.context=[];agent.scope='all';agent.usage=null;agent.lastScope='';agent.phase='';agent.queue=[];agent.watch='';agent.recovery=null;agent.handoff=null;}
 if(id)await agentAttach();agentPaint();const input=$('#agent-input');if(input){input.value=agent.draft;input.focus();}await agentSave();
}
function agentScope(){return {mode:agent.scope,ids:agent.scope==='selected'?agent.context.filter(id=>byId(id)):[],collection:agent.scope==='collection'?agent.collection:''};}
function agentScopeKey(){const s=agentScope();return JSON.stringify({...s,ids:(s.mode==='all'?state.items.map(i=>i.id):s.mode==='collection'?state.items.filter(i=>i.collection===s.collection).map(i=>i.id):s.ids).slice().sort()});}
function agentInScope(id,scope){const item=byId(id);return item&&(scope.mode==='all'||scope.mode==='selected'&&scope.ids.includes(id)||scope.mode==='collection'&&scope.ids.includes(id));}
function agentSetPhase(value){agent.phase=value;const el=$('#agent-progress');if(el){el.textContent=I18n.text(value);el.dataset.busy=String(!!agent.job);}}
async function agentApply(changes){await native('agentPatch',{changes});for(const c of changes){const item=byId(c.id);if(item)Object.assign(item,c.after);}}
function agentItemName(id){return byId(id)?.name||I18n.text('已移除的资料');}
function agentContextChips(){return agent.context.map(id=>`<span class="agent-chip" data-user-content>${svg(byId(id)?.kind||'document')}<b>${esc(agentItemName(id))}</b><button type="button" data-agent-unpin="${esc(id)}" aria-label="移除">${svg('close')}</button></span>`).join('');}
function agentSummary(tool,args){
 const names=ids=>(ids||[]).slice(0,3).map(agentItemName).join('、')+((ids||[]).length>3?I18n.text(' 等 ')+ids.length+I18n.text(' 份'):'');
 switch(tool){
  case 'search_library':return [args.query,args.kind&&labels[args.kind],args.collection,args.tag&&'#'+args.tag].filter(Boolean).join(' · ')||I18n.text('全部资料');
  case 'read_items':return names(args.ids);
  case 'list_collections':return I18n.text('全部分类');
  case 'set_collection':return names(args.ids)+' → '+args.collection;
  case 'add_tags':return names(args.ids)+' + '+args.tags.map(t=>'#'+t).join(' ');
  case 'write_note':return agentItemName(args.id)+(args.mode==='replace'?I18n.text('（替换笔记）'):I18n.text('（追加到笔记）'));
  case 'create_document':return args.name+'.'+args.format;
  case 'create_presentation':return args.title+' · '+args.slides.length+I18n.text(' 页');
  case 'open_item':return agentItemName(args.id);
  case 'mcp_call':return args.tool;
  case 'update_plan':return String(args.steps?.length||0);
  case 'finish_task':return I18n.text(({review_required:'核对已失效',completed:'已完成',partial:'部分完成',needs_input:'需要补充信息'})[args.status]||'任务核对');
 }return '';
}
function agentPreview(m){
 const platform=AgentPlatformUi.preview(m);if(platform)return platform;
 const a=m.args;if(m.tool==='mcp_call')return `<div class="agent-external-warning">${I18n.text('外部工具可访问当前账户的文件和网络，不受资料库范围沙箱限制，且不能自动撤销。')}</div><dl class="agent-external-target"><dt>${I18n.text('服务 ID')}</dt><dd data-user-content>${esc(a.serverId)}</dd><dt>${I18n.text('工具')}</dt><dd data-user-content>${esc(a.tool)}</dd></dl><pre class="agent-preview" data-user-content>${esc(JSON.stringify(a.arguments,null,2))}</pre>`;if(m.changes?.length)return agentMetadataDiff(m);if(m.tool==='write_note')return `<details class="agent-diff"><summary>${I18n.text('查看修改前后')}</summary><div><section><b>${I18n.text('修改前')}</b><pre data-user-content>${esc(m.beforeText??byId(a.id)?.notes??'')}</pre></section><section><b>${I18n.text('修改后')}</b><pre data-user-content>${esc(m.afterText??a.text)}</pre></section></div></details>`;if(m.tool==='create_document')return `<pre class="agent-preview" data-user-content>${esc(a.text.slice(0,1200))}${a.text.length>1200?'…':''}</pre>`;
 if(m.tool==='create_presentation')return `<ol class="agent-preview agent-slides" data-user-content>${a.slides.map(s=>`<li><b>${esc(s.title)}</b>${(s.bullets||[]).length?`<span>${esc(s.bullets.join(' / '))}</span>`:''}</li>`).join('')}</ol>`;
 return '';
}
function agentMessage(m){
 if(m.role==='user')return `<div class="agent-msg agent-user" data-user-content>${esc(m.text)}</div>`;
 if(m.role==='agent')return agentAnswer(m);
 if(m.role==='thinking')return `<div class="agent-msg agent-thinking" role="status"><span class="agent-led"></span><span id="agent-live-reply" data-user-content>${esc(agent.job?.partial||I18n.text('正在思考'))}</span><i></i><i></i><i></i></div>`;
 if(m.role==='error')return `<div class="agent-msg agent-error" role="alert"><span>${esc(m.text)}</span>${m.retryable&&m.taskId&&!agent.job?`<button type="button" class="text-button" data-agent-continue="${esc(m.taskId)}">${I18n.text('继续任务')}</button>`:''}${m.goal&&!agent.job?`<button type="button" class="text-button" data-agent-edit-request="${m.id}">${I18n.text('修改后重试')}</button>`:''}</div>`;
 const tool=AGENT_TOOLS[m.tool]||{icon:'info',label:m.tool},read=AGENT_READ.includes(m.tool);
 const controls=m.status==='pending'?`${m.grantEligible?`<label class="agent-grant"><input type="checkbox" data-agent-grant="${m.id}">${I18n.text('本任务内允许同类修改（这些资料，后续最多四次）')}</label>`:''}<div class="agent-card-actions"><button class="button primary" data-agent-run="${m.id}">${svg('check')}${I18n.text('执行')}</button><button class="button secondary" data-agent-skip="${m.id}">${I18n.text('跳过')}</button></div>`
  :m.status==='done'?`<div class="agent-card-actions"><span class="agent-status ok">${svg('check')}${I18n.text('已完成')}</span>${m.undo?`<button class="text-button" data-agent-undo="${m.id}">${svg('undo')}${I18n.text('撤销')}</button>`:''}${m.result?.id&&byId(m.result.id)?`<button class="text-button" data-agent-open-item="${esc(m.result.id)}">${svg('external')}${I18n.text('打开')}</button>`:''}</div>`
  :m.status==='skipped'?`<div class="agent-card-actions"><span class="agent-status">${I18n.text('已跳过')}</span></div>`
  :m.status==='undone'?`<div class="agent-card-actions"><span class="agent-status">${I18n.text('已撤销')}</span></div>`
  :m.status==='failed'?`<div class="agent-card-actions"><span class="agent-status bad">${esc(m.error||I18n.text('执行失败'))}</span></div>`
  :m.status==='running'?`<div class="agent-card-actions"><span class="agent-status"><span class="spinner"></span>${I18n.text('执行中')}</span></div>`:'';
 return `<article class="agent-card ${read?'agent-card-read':''} agent-${m.status}" data-agent-card="${m.id}"><header>${m.status==='pending'&&!['mcp_call','delegate_task','run_code'].includes(m.tool)?`<input type="checkbox" data-agent-select="${m.id}" ${m.selected!==false?'checked':''} aria-label="${I18n.text('选择此修改')}">`:''}${svg(tool.icon)}<b>${I18n.text(tool.label)}</b><span data-user-content>${esc(agentSummary(m.tool,m.args))}</span></header>${m.why?`<p data-user-content>${esc(m.why)}</p>`:''}${read?'':agentPreview(m)}${m.tool==='mcp_call'&&m.readPreview?`<details class="agent-external-result"><summary>${I18n.text('外部返回')}</summary><pre data-user-content>${esc(m.readPreview)}</pre>${m.resultEventId?`<button type="button" class="text-button" data-agent-full-result="${m.id}">${I18n.text('完整外部结果')}</button>`:''}</details>`:''}${read?(m.status==='done'?`<small>${I18n.text('已自动读取，未修改任何内容')}</small><div class="agent-sources">${agentSourceButtons(m.sourceIds)}</div>${m.readPreview?`<details class="agent-read-preview"><summary>${I18n.text('查看读取节选')}</summary><pre data-user-content>${esc(m.readPreview)}</pre>${m.resultEventId?`<button type="button" class="text-button" data-agent-full-result="${m.id}">${I18n.text('完整读取结果')}</button>`:''}</details>`:''}`:m.status==='failed'?`<small class="agent-read-error">${esc(m.error)}</small>`:''):controls}${AgentPlatformUi.result(m)}</article>`;
}
function agentEmpty(){
 return `<div class="agent-empty"><div class="agent-mark" aria-hidden="true"><span></span></div><h1>${I18n.text('今天想做点什么？')}</h1><p>${I18n.text('选定资料，说出要求，修改由你确认。')}</p>
 <div class="agent-starters">${AGENT_STARTERS.map(([text,icon])=>`<button type="button" data-agent-starter="${esc(text)}">${svg(icon)}<span>${I18n.text(text)}</span></button>`).join('')}</div>
 <div class="agent-tools-row">${Kok.enabled('mcp')?`<button type="button" class="text-button" data-mcp-settings>${I18n.text('外部工具 MCP')}</button>`:''}<span>${I18n.text('其他工具')}</span><button type="button" class="text-button" data-study-open="compare">${I18n.text('多文件对比')}</button><button type="button" class="text-button" data-study-open="review">${I18n.text('笔记回顾')}</button><button type="button" class="text-button" data-open-knowledge>${I18n.text('知识图谱')}</button><button type="button" class="text-button" data-route="organize-legacy">${I18n.text('批量 AI 归纳')}</button></div></div>`;
}
function agentView(){
 if(!agentAvailable())return `<section class="agent-page agent-off"><div class="agent-empty"><h1>${I18n.text('Agent 需要智能模式')}</h1><p>${I18n.text('纯净模式和私密文件夹不会发送任何 AI 请求。切换到智能模式并配置 AI 服务后即可使用。')}</p><button class="button primary" data-agent-settings>${I18n.text('切换到智能模式')}</button></div></section>`;
 const busy=!!agent.job,skills=[...AgentModel.presets,...AgentSkills.custom()],waiting=agent.messages.filter(m=>m.status==='pending').length;
 const usage=agent.usage;const used=usage?`${usage.estimated?I18n.text('保守估算'):I18n.text('服务商用量')} · ${usage.inputTokens} / ${usage.outputTokens} tokens${usage.cachedTokens?' · '+I18n.text('缓存命中')+' '+usage.cachedTokens:''}${agent.inputRate&&agent.outputRate?' · '+usage.cost.toFixed(5):''}`:I18n.text('仅在发送后调用 API');
 return `<section class="agent-page"><div class="agent-main">
 <div class="agent-toolbar">${AgentPlatformUi.button()}<select id="agent-scope" aria-label="${I18n.text('资料范围')}" ${busy?'disabled':''}>${[['all','整个资料库'],['selected','仅所选资料'],['collection','指定文件夹']].map(([v,l])=>`<option value="${v}" ${agent.scope===v?'selected':''}>${I18n.text(l)}</option>`).join('')}</select>${agent.scope==='collection'?`<select id="agent-collection" aria-label="${I18n.text('文件夹')}" ${busy?'disabled':''}><option value="">${I18n.text('选择文件夹')}</option>${state.collections.map(c=>`<option value="${esc(c)}" ${c===agent.collection?'selected':''} data-user-content>${esc(c)}</option>`).join('')}</select>`:''}<span class="agent-progress" id="agent-progress" role="status" data-busy="${busy}">${I18n.text(agent.phase||'就绪')}</span><button type="button" class="text-button" data-agent-sessions ${busy?'disabled':''}>${svg('clock')}${I18n.text('会话')}</button><button type="button" class="text-button" data-agent-task-records>${I18n.text('任务记录')}</button></div>
 <div class="agent-recovery">${agentRecoveryBar()}</div>${AgentOperations.view(agent.recovery&&(!agent.job||agent.job.id===agent.recovery.id)?agent.recovery:null)}<div class="agent-stream" id="agent-stream" aria-live="polite">${agent.messages.length?agentEntries():agentEmpty()}</div><div class="agent-review">${agentBatch()}</div>
 <button type="button" class="agent-latest text-button" data-agent-latest hidden>${I18n.text('回到最新')}${svg('arrow')}</button><form class="agent-composer" id="agent-form"><div class="agent-context">${agentContextChips()}<button type="button" class="agent-chip agent-add" data-agent-pick ${busy?'disabled':''}>${svg('plus')}<b>${I18n.text('添加资料')}</b></button></div>
 <textarea id="agent-input" rows="2" maxlength="2000" placeholder="${esc(I18n.text(busy?'补充当前要求，或加入待办…':'让 Agent 整理、总结、写笔记或做演示文稿…  输入 / 使用技能'))}" aria-label="${esc(I18n.text('给 Agent 的要求'))}">${esc(agent.draft)}</textarea>
 <div class="agent-controls"><select id="agent-intent" aria-label="${I18n.text('任务方式')}" ${busy?'disabled':''}><option value="assist" ${agent.intent==='assist'?'selected':''}>${I18n.text('协作修改')}</option><option value="discuss" ${agent.intent==='discuss'?'selected':''}>${I18n.text('仅讨论')}</option></select><select id="agent-skill" aria-label="${I18n.text('技能')}" ${busy?'disabled':''}><option value="auto" ${agent.skill==='auto'?'selected':''}>${I18n.text('自动选择技能')}</option><option value="" ${!agent.skill?'selected':''}>${I18n.text('不使用技能')}</option>${skills.map(s=>`<option value="${esc(s.name)}" ${agent.skill===s.name?'selected':''}>${esc(I18n.text(s.title||s.name))}</option>`).join('')}</select>
 <select id="agent-budget" aria-label="${I18n.text('用量')}" ${busy?'disabled':''}>${[[6000,'省用量'],[14000,'均衡'],[24000,'更多上下文'],[48000,'长任务'],[64000,'超长任务']].map(([v,l])=>`<option value="${v}" ${agent.budget===v?'selected':''}>${I18n.text(l)}</option>`).join('')}</select><button type="button" class="text-button" data-agent-limits aria-expanded="${agent.options}" ${busy?'disabled':''}>${I18n.text('用量设置')}</button>
 ${busy?`<button type="submit" class="button secondary">${I18n.text('补充要求')}</button><button type="button" class="text-button" data-agent-queue>${I18n.text('稍后执行')}</button><button type="button" class="button secondary agent-send" data-agent-stop>${svg('stop')}${I18n.text('停止')}</button>`:`<button type="button" class="text-button" data-agent-new>${I18n.text('新任务')}</button><button type="submit" class="button primary agent-send">${svg('send')}${I18n.text(agent.recovery?.respondable?'补充并继续':'发送')}</button>`}</div>
 <div class="agent-limits" ${agent.options?'':'hidden'}><label>${I18n.text('调用协议')}<select id="agent-protocol" ${busy?'disabled':''}>${[['auto','自动'],['tools','原生工具调用'],['json','兼容 JSON']].map(([v,l])=>`<option value="${v}" ${agent.protocol===v?'selected':''}>${I18n.text(l)}</option>`).join('')}</select></label>${[['requestLimit','请求上限',1,24,1],['inputRate','每百万输入 token 单价',0,10000,'any'],['outputRate','每百万输出 token 单价',0,10000,'any'],['costLimit','本次费用上限',0,10000,'any']].map(([k,l,min,max,step])=>`<label>${I18n.text(l)}<input data-agent-limit="${k}" type="number" min="${min}" max="${max}" step="${step}" value="${agent[k]}" ${busy?'disabled':''}></label>`).join('')}<small>${I18n.text('单价与额度使用同一货币，0 表示关闭费用限制；按保守额度拦截下一轮，实际费用以服务商为准。')}</small></div>
 <div class="agent-footnote"><small id="agent-usage" data-user-content>${esc(used)}</small><small id="agent-queue-count">${agent.queue.length?I18n.text('待办')+' · '+agent.queue.length:''}</small>${agent.queue.length&&!busy?`<button type="button" class="text-button" data-agent-next>${I18n.text('继续待办')}</button><button type="button" class="text-button" data-agent-clear-queue>${I18n.text('清空待办')}</button>`:''}</div></form></div></section>`;
}
function agentPaint({scroll=true}={}){
 agentSaveSoon();if(state.route!=='agent'||state.isPrivate)return;
 const previousFocus=document.activeElement;const root=$('#main'),stream=$('#agent-stream'),wasNear=!stream||stream.scrollHeight-stream.scrollTop-stream.clientHeight<90;
 const template=document.createElement('template');template.innerHTML=agentView();
 if(!stream||!agentAvailable()){root.replaceChildren(template.content);I18n.localize(root);return;}
 const next=template.content;
 for(const selector of ['.agent-toolbar','.agent-recovery','.agent-mission','.agent-context','.agent-controls','.agent-limits','.agent-footnote','.agent-review']){
  const old=root.querySelector(selector),fresh=next.querySelector(selector);if(old&&fresh&&!(selector==='.agent-limits'&&old.contains(document.activeElement))&&!agentChromeEqual(old,fresh))selector==='.agent-mission'?AgentOperations.preserve(old,fresh):old.replaceWith(fresh);
 }
 const incoming=next.querySelector('#agent-stream'),keys=new Set([...incoming.children].map(e=>e.dataset.entry).filter(Boolean));
 for(const node of [...stream.children])if(!node.dataset.entry||!keys.has(node.dataset.entry))node.remove();
 for(const node of [...incoming.children]){const existing=node.dataset.entry?stream.querySelector(`[data-entry="${node.dataset.entry}"]`):null;if(!existing)stream.append(node);else if(existing.innerHTML!==node.innerHTML){const opened=[...existing.querySelectorAll('details')].map(d=>d.open);existing.innerHTML=node.innerHTML;existing.querySelectorAll('details').forEach((d,i)=>{if(!d.matches('.agent-tool-group'))d.open=!!opened[i];});existing.classList.add('agent-entry-updated');}}
 const input=$('#agent-input');if(input&&document.activeElement!==input)input.value=agent.draft;
 I18n.localize(root);if(scroll&&wasNear)stream.scrollTop=stream.scrollHeight;agentUpdateScroll();agentRestoreFocus(previousFocus);
}
// The native task engine owns the loop, tool execution, approvals and the encrypted journal. This page renders
// its snapshots and forwards decisions, adjustments and stops; it never schedules model turns itself.
function agentNativeUndo(m){return m.status==='done'&&(m.undoChanges||m.undoCreated||m.undoMemory)?'native':null;}
function agentApplySnapshot(snap){
 if(!snap?.id||state.isPrivate||snap.sessionId!==agent.sessionId)return;agent.recovery=snap;const id=snap.id,job=agent.job&&agent.job.id===id?agent.job:null;
 const rows=snap.messages.map(m=>{const old=agent.messages.find(x=>x.id===m.id&&x.taskId===id);const row={...m,taskId:id};if(old){if(old.selected!==undefined)row.selected=old.selected;if(old.savedId)row.savedId=old.savedId;}if(!snap.running&&['pending','queued','running'].includes(row.status))row.status='paused';if(m.role==='tool')row.undo=agentNativeUndo(m);if(m.role==='error')row.retryable=!!(snap.retryable&&snap.resumable);return row;});
 const first=agent.messages.findIndex(m=>m.taskId===id),rest=agent.messages.filter(m=>m.taskId!==id);
 agent.messages=first<0?[...rest,...rows]:[...rest.slice(0,first),...rows,...rest.slice(first)];
 if(!job&&snap.status==='review_required')agentSetPhase('核对已失效');
 if(job){agent.usage=snap.usage;if(snap.phase)agentSetPhase(snap.phase);if(!snap.running)agentFinish(job,snap);}
 agentPaint({scroll:!!job});
}
async function agentPull(taskId){
 if(!taskId)return;if(agent.pulling===taskId){agent.pullAgain=true;return;}
 agent.pulling=taskId;
 try{do{agent.pullAgain=false;agentApplySnapshot(await native('agentTaskRead',{taskId}));}while(agent.pullAgain);}
 catch(error){if(agent.job?.id===taskId&&!agent.job.cancelled&&Kok.enabled('agent')&&!state.isPrivate)fail(error);}
 finally{if(agent.pulling===taskId)agent.pulling=null;}
}
function agentFinish(job,snap){
 if(agent.job!==job)return;agent.job=null;
 const reply=[...snap.messages].reverse().find(m=>m.role==='agent'&&!m.meta&&!m.interrupted),calls=snap.messages.filter(m=>m.role==='tool');
 agent.history.push({role:'user',content:job.goal},{role:'assistant',content:I18n.text('任务结果')+': '+(reply?.text||'')+' '+calls.map(c=>c.tool+':'+c.status).join(', ')});agent.history=agent.history.slice(-40);
 agentSetPhase(snap.status==='review_required'?'核对已失效':snap.status==='completed'?'已完成':snap.status==='partial'?'部分完成':snap.status==='needs_input'?'需要补充信息':snap.status==='limited'?'已达到本次请求上限。':snap.status==='cancelled'?'任务已取消':'任务已停止');
 renderNav();agentSave();
 if(snap.status==='completed'&&job.autoQueue!==false&&!job.cancelled&&agentAvailable()&&agent.queue.length){const next=agent.queue.shift();setTimeout(()=>runAgent(next).catch(fail),0);}
}
async function agentTrack(job,snap){if(job.cancelled){await native('agentTaskStop',{taskId:snap.id});return;}job.id=snap.id;agent.watch=snap.id;for(const text of job.pendingAdjustments||[])snap=await native('agentTaskAdjust',{taskId:snap.id,text});agentApplySnapshot(snap);return agentPull(snap.id);}
async function agentDecide(id,run,batch=false,grant=false){
 const m=agent.messages.find(x=>x.id===id);if(!m||m.status!=='pending')return;
 // A card from a task that is no longer running cannot be approved; resuming re-plans instead of replaying it.
 if(!m.taskId||agent.job?.id!==m.taskId){m.status='skipped';agentPaint({scroll:false});return;}
 if(run&&(!agentAvailable()||batch&&['mcp_call','delegate_task','run_code'].includes(m.tool)))return;
 if(run&&grant)await agentPresence(true);m.status=run?'running':'skipped';agentPaint({scroll:false});
 try{agentApplySnapshot(await native('agentTaskDecide',{taskId:m.taskId,callId:id,approve:run,batch,grant}));if(run)await refresh();}
 catch(error){fail(error);agentPull(m.taskId);}
}
async function agentUndo(id){
 const m=agent.messages.find(x=>x.id===id);if(!m?.undo||m.status!=='done'||agent.undoing)return;agent.undoing=true;
 try{if(m.undo==='native')agentApplySnapshot(await native('agentTaskUndo',{taskId:m.taskId,callId:id}));else{await m.undo();m.status='undone';m.undo=null;}await refresh();toast('已撤销');}
 catch(error){fail(error);}finally{agent.undoing=false;}agentPaint({scroll:false});
}
function agentAddInput(text,queued=false){
 text=text.trim();if(!text)return;
 if(!agent.job)return agent.recovery?.respondable?agentRespond(text):runAgent(text);
 if(queued){if(agent.queue.length>=5)throw Error(I18n.text('待办最多 5 项。'));agent.queue.push(text);}
 else{if(!agent.job.id){const pending=agent.job.pendingAdjustments||=[];if(pending.join('').length+text.length>400)throw Error(I18n.text('本次补充要求最多 400 字符。'));pending.push(text);}else native('agentTaskAdjust',{taskId:agent.job.id,text}).then(agentApplySnapshot).catch(fail);}
 agent.draft='';$('#agent-input').value='';agentPaint();
}
async function runAgent(goal){
 if(agent.job||!agentAvailable()||!goal.trim())return;
 if(agent.storageBlocked)throw Error(I18n.text('Agent 会话无法读取，原记录已保留。'));
 const scope=agentScope();if(scope.mode==='selected'&&!scope.ids.length||scope.mode==='collection'&&!scope.collection){toast('请选择有效的 Agent 资料范围。',true);return;}
 const scopeKey=agentScopeKey();if(agent.lastScope!==scopeKey){agent.history=[];agent.lastScope=scopeKey;}
 const skill=AgentModel.skill(agent.skill,goal,AgentSkills.custom()),preset=!!skill&&AgentModel.presets.includes(skill);
 const options={approvalPolicy:agent.approvalPolicy,semanticCompression:agent.semanticCompression,contextTokens:agent.contextTokens,useMemory:agent.useMemory,allowDelegation:agent.allowDelegation,allowSandbox:agent.allowSandbox,...(agent.allowedTools?{allowedTools:agent.allowedTools}:{}),sessionId:agent.sessionId,goal:goal.trim(),intent:agent.intent,protocol:agent.protocol,handoff:agent.handoff,workflow:preset?skill.name:'',instruction:skill&&!preset?('[Skill: '+skill.name+'] '+skill.instructions).slice(0,800):'',scope,history:AgentModel.history(agent.history,Math.min(6000,Math.floor(agent.budget/3))).slice(-10),budget:agent.budget,requestLimit:agent.requestLimit,costLimit:agent.costLimit,inputRate:agent.inputRate,outputRate:agent.outputRate};
 const job={id:'',goal:goal.trim(),raw:'',partial:'',request:0,cancelled:false};
 agent.handoff=null;agent.recovery=null;agent.job=job;agent.usage=null;agent.draft='';const sent=$('#agent-input');if(sent)sent.value='';
 if(skill&&!preset)agent.messages.push({id:uid(),role:'agent',meta:true,text:I18n.text('使用技能')+' · '+I18n.text(skill.title||skill.name)+' — '+I18n.text(skill.description||'')});
 agentSetPhase('生成方案');renderNav();agentPaint();
 try{await agentSave();if(job.cancelled)return;await agentTrack(job,await native('agentTaskStart',options));}
 catch(error){if(agent.job===job)agent.job=null;agent.messages.push({id:uid(),role:'error',text:error.message,goal});agentSetPhase('任务已停止');agentPaint();renderNav();}
}
async function agentRespond(text){
 if(agent.job||!agentAvailable()||!agent.recovery?.respondable)return;
 const taskId=agent.recovery.id,job={id:taskId,goal:text,raw:'',partial:'',request:0,cancelled:false};agent.job=job;agent.draft='';agentSetPhase('生成方案');renderNav();agentPaint();
 try{await agentTrack(job,await native('agentTaskAdjust',{taskId,text}));}catch(error){if(agent.job===job)agent.job=null;agent.draft=text;fail(error);agentPaint();renderNav();}
}
async function agentContinue(taskId){
 if(agent.job||!agentAvailable())return;const job={id:taskId,goal:agent.messages.find(m=>m.taskId===taskId&&m.role==='user')?.text||'',raw:'',partial:'',request:0,cancelled:false};
 agent.job=job;agentSetPhase('生成方案');renderNav();agentPaint();
 try{await agentTrack(job,await native('agentTaskResume',{taskId}));}catch(error){if(agent.job===job)agent.job=null;fail(error);agentPaint();renderNav();}
}
async function stopAgent(){
 const job=agent.job;if(!job)return;job.cancelled=true;agentSetPhase('任务已停止');agentPaint();
 try{if(job.id)await native('agentTaskStop',{taskId:job.id});}catch{}
 if(agent.job===job)agent.job=null;if(job.id&&Kok.enabled('agent')&&!state.isPrivate)await agentPull(job.id);agentPaint();renderNav();await agentSave();
}
let agentStreamFrame;
Kok.on('*',(data,eventName)=>{
 const m={data,eventName};if(m.eventName==='librarySwitched'){stopAgent().catch(fail);return;}
 if(['libraryChanged','libraryRefreshed'].includes(m.eventName)&&agent.watch&&!agent.job&&agentAvailable())return agentPull(agent.watch);
 if(m.eventName==='agentTaskChanged'){if(m.data?.taskId&&m.data.taskId===agent.watch)agentPull(m.data.taskId);return;}
 if(m.eventName!=='agentDelta'||!agent.job||m.data.jobId!==agent.job.id||agent.job.cancelled||!agentAvailable())return;
 const job=agent.job;if(m.data.request!==job.request){job.request=m.data.request;job.raw='';}job.format=m.data.format;job.raw=(job.raw||'')+m.data.text;if(job.raw.length>26000)job.raw=job.raw.slice(-26000);
 if(agentStreamFrame)return;agentStreamFrame=requestAnimationFrame(()=>{agentStreamFrame=null;if(agent.job!==job||job.cancelled||!agentAvailable())return;job.partial=job.format==='tools'?job.raw.slice(-4000):AgentModel.partialReply(job.raw);const stream=$('#agent-stream'),el=$('#agent-live-reply');if(el&&job.partial){const follow=stream.scrollHeight-stream.scrollTop-stream.clientHeight<90;el.textContent=job.partial;if(follow)stream.scrollTop=stream.scrollHeight;}});
},{module:'agent'});
function openAgent({ids=[],prompt=''}={}){
 for(const id of ids)if(byId(id)&&!agent.context.includes(id))agent.context.push(id);
 agent.context=agent.context.slice(-24);if(ids.length)agent.scope='selected';if(prompt)agent.draft=prompt;
 if(viewer?.open)closeViewer().then(()=>navigate('agent'));else navigate('agent');
 setTimeout(()=>{const input=$('#agent-input');if(input){input.focus();input.setSelectionRange(input.value.length,input.value.length);}},60);
}
function agentPickItems(){
 const rows=state.items.slice().sort((a,b)=>String(b.added).localeCompare(String(a.added))),selected=new Set(agent.context);
 showSmall(`<h2>${I18n.text('添加资料到 Agent')}</h2><input id="agent-pick-search" type="search" placeholder="${esc(I18n.text('搜索名称'))}" autocomplete="off"><div class="agent-pick-list" id="agent-pick-list"></div><small>${I18n.text('最多选择 24 份资料，搜索全部文件。')}</small><div class="document-form-actions"><button type="button" class="text-button" data-close-small>${I18n.text('取消')}</button><button type="button" class="button primary" id="agent-pick-done">${I18n.text('添加')}</button></div>`);
 const list=$('#agent-pick-list');const paint=()=>{const q=$('#agent-pick-search').value.trim().toLowerCase();list.innerHTML=rows.filter(i=>!q||i.name.toLowerCase().includes(q)).slice(0,200).map(i=>`<label><input type="checkbox" value="${esc(i.id)}" ${selected.has(i.id)?'checked':''}>${svg(i.kind)}<span data-user-content>${esc(i.name)}</span><small>${esc(i.ext)}</small></label>`).join('');};
 paint();I18n.localize($('#small-dialog'));$('#agent-pick-search').focus();$('#agent-pick-search').oninput=paint;
 list.onchange=event=>{const input=event.target;if(input.type!=='checkbox')return;if(input.checked&&selected.size>=24){input.checked=false;toast('最多选择 24 份资料，搜索全部文件。',true);return;}input.checked?selected.add(input.value):selected.delete(input.value);};
 $('#agent-pick-done').onclick=()=>{agent.scope='selected';agent.context=[...selected];closeSmall();agentPaint({scroll:false});};
}

document.addEventListener('click',event=>{
 const t=event.target;
 if(t.closest('[data-agent-launch]')){event.preventDefault();openAgent({ids:typeof libraryDrag!=='undefined'?[...libraryDrag.selected]:[]});return;}
 if(t.closest('[data-agent-starter]')){const input=$('#agent-input');input.value=t.closest('[data-agent-starter]').dataset.agentStarter;agent.draft=input.value;agentSaveSoon();input.focus();return;}
 if(t.closest('[data-agent-unpin]')){if(agent.job)return;agent.context=agent.context.filter(id=>id!==t.closest('[data-agent-unpin]').dataset.agentUnpin);agentPaint({scroll:false});return;}
 if(t.closest('[data-agent-pick]')){if(!agent.job)agentPickItems();return;}
 if(t.closest('[data-agent-settings]')){$('#settings-button').click();return;}
 if(t.closest('[data-agent-run]')){const id=t.closest('[data-agent-run]').dataset.agentRun,grant=document.querySelector(`[data-agent-grant="${id}"]`)?.checked||false;return agentDecide(id,true,false,grant);}
 if(t.closest('[data-agent-skip]'))return agentDecide(t.closest('[data-agent-skip]').dataset.agentSkip,false);
 if(t.closest('[data-agent-undo]'))return agentUndo(t.closest('[data-agent-undo]').dataset.agentUndo);
 if(t.closest('[data-agent-stop]'))return stopAgent();
 if(t.closest('[data-agent-continue]')){agentContinue(t.closest('[data-agent-continue]').dataset.agentContinue).catch(fail);return;}
 if(t.closest('[data-agent-run-all],[data-agent-skip-all]')){const run=!!t.closest('[data-agent-run-all]');(async()=>{for(const m of agent.messages.filter(x=>x.status==='pending'&&(!run||x.tool!=='mcp_call')))await agentDecide(m.id,run,true);})().catch(fail);return;}
 if(t.closest('[data-agent-new]')){agentNew().catch(fail);return;}
 if(t.closest('[data-agent-next]')){if(!agent.job&&agent.queue.length)runAgent(agent.queue.shift()).catch(fail);return;}
 if(t.closest('[data-agent-clear-queue]')){if(!agent.job){agent.queue=[];agentPaint({scroll:false});}return;}
 if(t.closest('[data-agent-limits]')){agent.options=!agent.options;agentPaint({scroll:false});return;}
 if(t.closest('[data-agent-queue]')){try{agentAddInput($('#agent-input').value,true);}catch(e){fail(e);}return;}
 if(t.closest('[data-agent-sessions]')){agentSessionPicker();return;}
},true);
document.addEventListener('click',event=>{const open=event.target.closest('[data-agent-open-item]');if(open){event.preventDefault();event.stopImmediatePropagation();openItem(open.dataset.agentOpenItem,{play:false}).catch(fail);}},true);
document.addEventListener('submit',event=>{if(event.target.id!=='agent-form')return;event.preventDefault();const input=$('#agent-input');try{Promise.resolve(agentAddInput(input.value)).catch(fail);}catch(e){fail(e);}},true);
document.addEventListener('keydown',event=>{if(event.target.id==='agent-input'&&event.key==='Enter'&&!event.shiftKey&&!event.isComposing){event.preventDefault();$('#agent-form').requestSubmit();}});
document.addEventListener('input',event=>{if(event.target.id==='agent-input'){agent.draft=event.target.value;agentSaveSoon();}});
document.addEventListener('change',event=>{
 const t=event.target;if(agent.job||!t.matches('#agent-protocol,#agent-intent,#agent-budget,#agent-skill,#agent-scope,#agent-collection,[data-agent-limit]'))return;
 if(t.id==='agent-protocol')agent.protocol=t.value;
 if(t.id==='agent-intent')agent.intent=t.value==='discuss'?'discuss':'assist';
 if(t.id==='agent-budget')agent.budget=Number(t.value);
 if(t.id==='agent-skill')agent.skill=t.value;
 if(t.id==='agent-scope')agent.scope=t.value;
 if(t.id==='agent-collection')agent.collection=t.value;
 if(t.dataset.agentLimit){const n=Number(t.value),key=t.dataset.agentLimit;if(!Number.isFinite(n)||n<Number(t.min)||n>Number(t.max)||key==='requestLimit'&&!Number.isInteger(n)){t.value=agent[key];toast('Agent 用量设置无效。',true);return;}agent[key]=n;}
 agentPaint({scroll:false});
});
async function agentSessionPicker(){
 if(agent.job)return;await agentSave();
 showSmall(`<h2>${I18n.text('会话')}</h2><div class="agent-session-list">${agent.sessions.map(s=>`<div><button class="text-button" data-agent-resume="${s.id}" data-user-content>${esc(s.title)}<small>${esc(new Date(s.updated).toLocaleString())}</small></button><button class="icon-button" data-agent-delete-session="${s.id}" aria-label="${I18n.text('删除会话')}">${svg('close')}</button></div>`).join('')}</div>`);
 $('#small-dialog').onclick=async event=>{try{const resume=event.target.closest('[data-agent-resume]'),remove=event.target.closest('[data-agent-delete-session]');if(resume){await closeSmall();await agentNew(resume.dataset.agentResume);}if(remove){const id=remove.dataset.agentDeleteSession;if(id===agent.sessionId){agent.sessionId=uid();agent.messages=[];agent.history=[];agent.draft='';agent.queue=[];agent.context=[];agent.scope='all';agent.usage=null;agent.lastScope='';}agent.sessions=agent.sessions.filter(s=>s.id!==id);await native('saveAgentSessions',{text:JSON.stringify(agent.sessions)});await closeSmall();agentPaint();}}catch(e){fail(e);}};
}

// Settings: choose whether Smart mode opens on Agent or on the library.
function agentSetupSettings(){
 const ai=$('[data-ai-settings]')?.closest('.setting-row');
 if(ai&&!$('#start-page-select'))ai.insertAdjacentHTML('beforebegin',`<div class="setting-row smart-settings-row"><div>${I18n.text('启动页')}<small>${I18n.text('智能模式下打开 kok 时显示')}</small></div><select id="start-page-select" aria-label="${esc(I18n.text('启动页'))}"><option value="today">${I18n.text('今天')}</option><option value="agent">Agent</option><option value="overview">${I18n.text('资料库')}</option></select></div>`);
 const select=$('#start-page-select');if(select)select.value=state.settings.startPage||'today';
}
document.addEventListener('change',event=>{if(event.target.id==='start-page-select')setting({startPage:event.target.value}).catch(fail);});
// The viewer's Agent panel answers about the open file; "交给 Agent" carries the file to the Agent page for actions.
document.addEventListener('click',event=>{if(!event.target.closest('[data-agent-handoff]')||!state.viewer)return;event.preventDefault();openAgent({ids:[state.viewer.id]});},true);
// Knowledge graph analysis hands its sources and summary to Agent for follow-up actions.
document.addEventListener('click',event=>{
 if(!event.target.closest('[data-knowledge-agent]'))return;event.preventDefault();
 const report=knowledgeView.ai?.report;const ids=(report?.ids||[]).filter(id=>byId(id));
 closeKnowledgeGraph().then(()=>openAgent({ids,prompt:report?.summary?I18n.text('基于这份分析继续：')+report.summary.slice(0,600):''})).catch(fail);
},true);


// Migrated intact from agent-tasks.js
'use strict';
async function agentAttach(){
 if(state.isPrivate||location.search.includes('preview='))return;
 const tasks=await native('agentTaskList');const task=tasks.filter(t=>t.sessionId===agent.sessionId).sort((a,b)=>b.updated.localeCompare(a.updated))[0];
 if(!task)return;agent.watch=task.id;const snap=await native('agentTaskRead',{taskId:task.id});
 if(snap.running)agent.job={id:snap.id,goal:snap.options.goal,raw:'',partial:'',request:0,cancelled:false,autoQueue:false};
 agentApplySnapshot(snap);
}
function agentRecoveryBar(){
 const t=agent.recovery;
 if(agent.handoff)return `<span>${I18n.text('已准备交接')} · ${agent.handoff.ids.length} ${I18n.text('份资料')}</span><button type="button" class="text-button" data-agent-drop-handoff>${I18n.text('移除')}</button>`;
 if(!t||agent.job)return '';
 if(t.respondable)return `<span>${I18n.text('在下方补充信息，继续同一任务；沿用原请求上限。')}</span>`;
 const retry=t.resumable&&(t.status!=='failed'||t.retryable);
 return `${retry?`<span>${I18n.text('任务已暂停，继续后再确认修改。')}</span><button type="button" class="button secondary" data-agent-continue-task="${t.id}">${I18n.text(t.status==='failed'?'重试当前步骤':'继续任务')}</button>`:''}${t.calls?.some(c=>c.tool==='read_items'&&c.status==='done')?`<button type="button" class="text-button" data-agent-prepare-handoff="${t.id}">${I18n.text('交接到新任务')}</button>`:''}`;
}
async function agentTaskRecords(){
 const rows=await native('agentTaskList');
 showSmall(`<h2>${I18n.text('任务记录')}</h2><div class="agent-task-records">${rows.length?rows.sort((a,b)=>b.updated.localeCompare(a.updated)).map(t=>`<div class="agent-task-record"><button type="button" class="text-button" data-agent-inspect-task="${t.id}"><b data-user-content>${esc(t.title)}</b><small>${esc(new Date(t.updated).toLocaleString())} · ${I18n.text(({review_required:'核对已失效',completed:'已完成',partial:'部分完成',needs_input:'需要补充信息',limited:'已达到本次请求上限。',failed:'执行失败',stopped:'任务已停止',waiting:'等待确认',cancelled:'已取消'})[t.status]||'任务已暂停')}</small></button><button type="button" class="icon-button" data-agent-delete-task="${t.id}" aria-label="${I18n.text('删除任务记录')}">${svg('trash')}</button></div>`).join(''):`<p>${I18n.text('暂无任务记录')}</p>`}</div>`);I18n.localize($('#small-dialog'));
 $('#small-dialog').onclick=async event=>{try{
  const open=event.target.closest('[data-agent-inspect-task]'),remove=event.target.closest('[data-agent-delete-task]');
  if(open){if(agent.job){toast('请先停止当前任务。',true);return;}const snap=await native('agentTaskRead',{taskId:open.dataset.agentInspectTask});await agentSave();
   const session=agent.sessions.find(s=>s.id===snap.sessionId);if(session)agentRestore(session.id);else{await agentNew();agent.sessionId=snap.sessionId;agent.context=snap.options.scope.ids||[];agent.scope=snap.options.scope.mode;agent.collection=snap.options.scope.collection||'';}
   agent.watch=snap.id;if(snap.running)agent.job={id:snap.id,goal:snap.options.goal,raw:'',autoQueue:false};agentApplySnapshot(snap);await closeSmall();navigate('agent');
  }
  if(remove){const id=remove.dataset.agentDeleteTask;if(!confirm(I18n.text('删除这份任务记录？资料和原文件会保留。')))return;await native('agentTaskDelete',{taskId:id});if(agent.watch===id){agent.watch='';agent.recovery=null;agent.messages=agent.messages.filter(m=>m.taskId!==id);agentPaint({scroll:false});}await agentTaskRecords();}
 }catch(error){fail(error);}};
}
async function agentPrepareHandoff(taskId){
 const preview=await native('agentHandoffPreview',{taskId});
 showSmall(`<h2>${I18n.text('交接到新任务')}</h2><p>${I18n.text('只引用所选读取结果，不复制整个会话。发送后才调用 AI。')}</p><div class="agent-handoff-calls">${preview.calls.map(c=>`<label><input type="checkbox" value="${c.id}" checked><span data-user-content>${esc((c.ids||[]).map(agentItemName).join('、'))}</span>${c.stale?`<small>${I18n.text('来源已改变，将重新读取')}</small>`:''}</label>`).join('')}</div><textarea id="agent-handoff-note" maxlength="800" placeholder="${esc(I18n.text('交接备注（可选）'))}"></textarea><div class="document-form-actions"><button type="button" class="text-button" data-close-small>${I18n.text('取消')}</button><button type="button" class="button primary" id="agent-handoff-confirm">${I18n.text('准备新任务')}</button></div>`);I18n.localize($('#small-dialog'));
 $('#agent-handoff-confirm').onclick=async()=>{try{const handoff=await native('agentHandoffPrepare',{taskId,callIds:[...$('#small-dialog').querySelectorAll('.agent-handoff-calls input:checked')].map(x=>x.value),note:$('#agent-handoff-note').value});await closeSmall();await agentNew();agent.handoff=handoff;agent.context=handoff.ids.filter(id=>byId(id));agent.scope='selected';agentPaint();$('#agent-input')?.focus();}catch(error){fail(error);}};
}
async function agentFullResult(id){
 const m=agent.messages.find(m=>m.id===id);if(!m?.resultEventId)return;
 const e=await native('agentTaskEvents',{taskId:m.taskId,eventId:m.resultEventId});
 showSmall(`<h2>${I18n.text(m.tool==='mcp_call'?'完整外部结果':'完整读取结果')}</h2><pre class="agent-full-result" data-user-content>${esc(e?.data?.result?.text||'')}</pre>`);I18n.localize($('#small-dialog'));
}
document.addEventListener('click',event=>{
 const t=event.target;
 if(t.closest('[data-agent-task-records]'))agentTaskRecords().catch(fail);
 const resume=t.closest('[data-agent-continue-task]');if(resume)agentContinue(resume.dataset.agentContinueTask).catch(fail);
 const handoff=t.closest('[data-agent-prepare-handoff]');if(handoff&&!agent.job)agentPrepareHandoff(handoff.dataset.agentPrepareHandoff).catch(fail);
 const full=t.closest('[data-agent-full-result]');if(full)agentFullResult(full.dataset.agentFullResult).catch(fail);
 if(t.closest('[data-agent-drop-handoff]')){agent.handoff=null;agentPaint({scroll:false});}
},true);

// The Agent owns its session state and view helpers. Other modules use this
// explicit UI contract instead of depending on top-level function declarations.
async function startAgentUi({startup}){
 if(!state.isPrivate){for(const tool of Kok.catalog().tools){if(!tool.write&&!AGENT_READ.includes(tool.name))AGENT_READ.push(tool.name);AGENT_TOOLS[tool.name]||={icon:'info',label:tool.title||tool.name};}if(agent.loaded)await agentAttach();else await agentLoad();}
 if(startup&&!location.search.includes('preview=')&&agentAvailable()&&state.route==='overview'&&(state.settings.startPage||'today')==='agent'){state.route='agent';render();}
}
export const AgentUi=Object.freeze({state:agent,presence:agentPresence,available:agentAvailable,open:openAgent,render:agentPaint,stop:stopAgent,save:agentSave,newSession:agentNew,scope:agentScope,updateScroll:agentUpdateScroll,tasks:agentTaskRecords,settings:agentSetupSettings,start:startAgentUi});
// The legacy shell still needs one named adapter. Implementation helpers remain
// module-private; there are no global agent/agentPaint/runAgent declarations.
globalThis.AgentUi=AgentUi;

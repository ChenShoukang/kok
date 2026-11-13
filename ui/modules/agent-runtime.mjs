import {AgentPlatformUi} from './agent-platform.mjs';
import {AgentOperations} from './agent-operations.mjs';
import {AgentArtifacts,piEditorKeyDown} from './pi-agent-view.mjs';
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
// Fork is a message-level operation: only a real user/assistant transcript row backed by a native
// 32-hex session entry can branch, and only while the composer is idle. Meta/local rows and the
// assistant rows the host never stores as entries (interrupted, empty) deliberately have no fork.
function agentForkable(m){return !agent.job&&agentAvailable()&&!!m&&['user','agent'].includes(m.role)&&!m.meta&&!m.interrupted&&!!String(m.text||'').trim()&&/^[a-f0-9]{32}$/.test(String(m.id||''));}
function agentForkButton(m){return agentForkable(m)?`<button type="button" class="text-button" data-agent-fork="${esc(m.id)}" title="${esc(I18n.text('从此处分叉'))}" aria-label="${esc(I18n.text('从此处分叉'))}">${I18n.text('从此处分叉')}</button>`:'';}
function agentAnswer(m){
 const actions=m.meta?'':`<div class="agent-answer-actions"><button type="button" class="text-button" data-agent-copy="${m.id}">${I18n.text('复制')}</button>${m.savedId&&byId(m.savedId)?`<button type="button" class="text-button" data-agent-open-item="${m.savedId}">${I18n.text('打开已保存')}</button>`:`<button type="button" class="text-button" data-agent-save-answer="${m.id}" ${agent.job?'disabled':''}>${I18n.text('保存回答')}</button>`}${agentForkButton(m)}</div>`;
 return `<div class="agent-msg agent-reply ${m.meta?'agent-meta':''}"><span class="agent-led" aria-hidden="true"></span><div class="agent-answer"><div class="agent-rich-text" data-user-content>${agentRichText(m.text)}</div>${m.sourceIds?.length?`<div class="agent-sources" aria-label="${I18n.text('已读取来源')}"><small>${I18n.text('已读取来源')}</small>${agentSourceButtons(m.sourceIds)}</div>`:''}${m.interrupted?`<small class="agent-interrupted" role="status">${I18n.text('已中断 · 本轮操作未执行')}</small>`:''}${actions}</div></div>`;
}
function agentEntries(){
 for(const m of agent.messages)m.id||=uid();
 const entries=AgentModel.groups(agent.messages.filter(m=>!(['update_plan','finish_task'].includes(m.tool)&&m.status==='done'))).map(group=>{
  const first=group.items[0];if(group.kind==='message')return `<div class="agent-entry" data-entry="${first.id}">${agentMessage(first)}</div>`;
  const active=group.items.some(m=>['queued','running'].includes(m.status)),failed=group.items.some(m=>m.status==='failed');
  const done=group.items.filter(m=>m.status==='done').length;
  const open=agentGroupOpen.has(first.id)?agentGroupOpen.get(first.id):active;
  return `<div class="agent-entry" data-entry="reads-${first.id}"><details class="agent-tool-group" ${open?'open':''} data-group-id="${first.id}"><summary><span class="agent-step-dot" data-state="${failed?'failed':active?'running':'done'}"></span><span>${I18n.text(active?'正在读取资料':'资料读取步骤')}</span><b>${done} / ${group.items.length}</b>${svg('chevron')}</summary><div class="agent-tool-group-content">${group.items.map(agentMessage).join('')}</div></details></div>`;
 }).join('');
 return entries+(!agent.job?AgentPlatformUi.workspaceFiles(agent.recovery):'')+(agent.job&&!agent.job.id?`<div class="agent-entry" data-entry="pending-goal">${agentMessage({role:'user',text:agent.job.goal})}</div><div class="agent-entry" data-entry="pending-progress">${agentMessage({role:'thinking'})}</div>`:'');
}
function agentBatch(){
 const pending=agent.messages.filter(m=>m.status==='pending');if(!pending.length)return '';
 if(pending.length===1){const m=pending[0],tool=AGENT_TOOLS[m.tool]||{label:m.tool};return `<div class="agent-batch agent-single-review" role="status"><button type="button" class="agent-review-target text-button" data-agent-review-target="${m.id}">${svg('info')}<span>${I18n.text('等待确认')} · ${esc(I18n.text(tool.label))}</span>${svg('chevron')}</button></div>`;}
 const rows=pending.filter(m=>!['mcp_call','delegate_task','run_code','start_command_job','open_browser','browser_action'].includes(m.tool)),chosen=rows.filter(m=>m.selected!==false),selected=chosen.length,external=pending.length-rows.length;
 // Summarize exactly what the batch will change before the user confirms it.
 const groups=new Map();for(const m of chosen){const label=I18n.text((AGENT_TOOLS[m.tool]||{label:m.tool}).label);groups.set(label,(groups.get(label)||0)+1);}
 const change=`${I18n.text('将修改')} ${selected} ${I18n.text('项')}${groups.size?' · '+[...groups].map(([label,count])=>label+(count>1?' × '+count:'')).join(' · '):''}`;
 return `<div class="agent-batch"><div class="agent-batch-summary"><button type="button" class="text-button" data-agent-review-target="${pending[0].id}">${pending.length} ${I18n.text('项待确认')}${external?' · '+I18n.text(pending.some(m=>m.tool==='mcp_call')?'外部调用请逐项确认':'计算和子任务需逐项确认'):''}${svg('chevron')}</button><small class="agent-batch-change">${esc(change)}</small></div>${rows.length?`<button type="button" class="text-button" data-agent-toggle-selection>${I18n.text(selected===rows.length?'取消全选':'全选')}</button><button type="button" class="button primary" data-agent-run-selected ${!selected?'disabled':''}>${I18n.text('执行所选')} · ${selected}</button>`:''}<button type="button" class="button secondary" data-agent-skip-all>${I18n.text('全部跳过')}</button></div>`;
}
function agentComposerControls(){
 const busy=!!agent.job,hasText=!!agent.draft.trim(),label=busy?'补充要求':agent.recovery?.respondable?'补充并继续':'发送';
 return `<div class="agent-controls" data-has-draft="${hasText}" data-busy="${busy}">${busy?`<button type="button" class="icon-button agent-queue-action" data-agent-queue aria-label="${I18n.text('完成后跟进')}" title="${I18n.text('完成后跟进')}" ${!hasText?'disabled':''}>${svg('clock')}</button><button type="button" class="icon-button agent-stop" data-agent-stop aria-label="${I18n.text('停止')}" title="${I18n.text('停止')}">${svg('stop')}</button>`:''}<button type="submit" class="button primary agent-send" aria-label="${I18n.text(label)}" title="${I18n.text(label)}" ${!hasText||agent.inputFlight?'disabled':''}>${svg('arrow')}<span>${I18n.text(label)}</span></button></div>`;
}
const agentInputSizes=new WeakMap();
function agentSyncInput(){
 const input=$('#agent-input');if(!input)return;
 const main=$('.agent-main');if(main)main.inert=AgentArtifacts.active&&AgentArtifacts.overlay;
 const busy=!!agent.job,hasText=!!input.value.trim(),controls=$('.agent-controls');
 input.placeholder=I18n.text(busy?'输入会插入当前任务…':agent.recovery?.respondable?'补充信息，继续这个任务…':'告诉我，你想完成什么…');
 if(controls){controls.dataset.hasDraft=String(hasText);controls.querySelector('[type=submit]').disabled=!hasText||!!agent.inputFlight;const queue=controls.querySelector('[data-agent-queue]');if(queue)queue.disabled=!hasText||!!agent.inputFlight;}
 // Keep the same textarea during streaming, including its caret and composition session.
 const previous=agentInputSizes.get(input),width=input.clientWidth;
 if(!previous||previous.value!==input.value||previous.width!==width){const top=input.scrollTop;input.style.height='auto';input.style.height=Math.min(180,Math.max(52,input.scrollHeight))+'px';input.scrollTop=top;agentInputSizes.set(input,{value:input.value,width});}
}
// While a task runs the drawer mirrors the native input queues (read-only in this slice); idle keeps
// the local legacy queue with its editor so saved drafts from older sessions still work.
function agentQueuedRows(){
 if(!agent.job)return agent.queue.map(text=>({text,mode:''}));
 const rows=[];
 if(agent.job.id&&agent.recovery?.id===agent.job.id&&Array.isArray(agent.recovery.queue))
  for(const row of agent.recovery.queue)rows.push({text:String(row?.text||''),mode:row?.mode==='follow'?'follow':'steer'});
 for(const row of agent.job.pendingInputs||[])rows.push({text:String(row?.text||''),mode:row?.mode==='follow'?'follow':'steer'});
 return rows.filter(row=>row.text.trim());
}
function agentQueuedTasks(){
 const running=!!agent.job,rows=agentQueuedRows();
 if(!rows.length)return '<div class="agent-queued" hidden></div>';
 if(running)return `<details class="agent-queued" ${agent.queueOpen?'open':''}><summary>${svg('clock')}<span>${I18n.text('待处理')} · ${rows.length}</span>${svg('chevron')}</summary><ol>${rows.map((row,index)=>`<li><span class="agent-queued-index">${index+1}</span><span data-user-content>${esc(row.text)}</span><small>${I18n.text(row.mode==='follow'?'完成后跟进':'下回合插入')}</small></li>`).join('')}</ol></details>`;
 return `<details class="agent-queued" ${agent.queueOpen?'open':''}><summary>${svg('clock')}<span>${I18n.text('待办')} · ${rows.length}</span>${svg('chevron')}</summary><ol>${agent.queue.map((text,index)=>`<li><span class="agent-queued-index">${index+1}</span><span data-user-content>${esc(text)}</span><button type="button" class="icon-button" data-agent-edit-queued="${index}" aria-label="${I18n.text('编辑待办')}" title="${I18n.text('编辑待办')}">${svg('edit')}</button><button type="button" class="icon-button" data-agent-remove-queued="${index}" aria-label="${I18n.text('移除待办')}" title="${I18n.text('移除待办')}">${svg('close')}</button></li>`).join('')}</ol><div class="agent-queued-actions"><button type="button" class="text-button" data-agent-next>${I18n.text('继续待办')}</button><button type="button" class="text-button" data-agent-clear-queue>${I18n.text('清空待办')}</button></div></details>`;
}
function agentHistoryRows(){
 const current=agentSnapshot(),query=String(agent.historyQuery||'').trim().toLocaleLowerCase();
 const rows=[current,...agent.sessions.filter(row=>row.id!==current.id)].filter(row=>!query||String(row.title||'').toLocaleLowerCase().includes(query)||String(row.draft||'').toLocaleLowerCase().includes(query));
 return rows.length?rows.map(row=>`<div class="agent-history-row${row.id===agent.sessionId?' is-current':''}"><button type="button" data-agent-resume="${esc(row.id)}" ${row.id===agent.sessionId?'aria-current="true"':''} ${agent.job&&row.id!==agent.sessionId?'disabled':''}><span data-user-content>${esc(row.title||I18n.text('新任务'))}</span><small>${row.id===agent.sessionId&&agent.job?I18n.text('执行中'):row.draft?I18n.text('草稿'):esc(new Date(row.updated).toLocaleDateString())}</small></button><button type="button" class="icon-button" data-agent-delete-session="${esc(row.id)}" aria-label="${I18n.text('删除会话')}" title="${I18n.text('删除会话')}" ${agent.job?'disabled':''}>${svg('trash')}</button></div>`).join(''):`<p class="agent-history-empty">${I18n.text(query?'没有匹配的会话':'暂无会话')}</p>`;
}
function agentHistoryView(){
 return `<aside class="agent-history" id="agent-history" aria-label="${I18n.text('会话')}" ${agent.historyOpen?'':'hidden'}><header><strong>${I18n.text('会话')}</strong><button type="button" class="icon-button" data-agent-close-history aria-label="${I18n.text('关闭')}" title="${I18n.text('关闭')}">${svg('close')}</button></header><button type="button" class="button secondary agent-history-new" data-agent-history-new ${agent.job?'disabled':''}>${svg('plus')}${I18n.text('新任务')}</button><label class="agent-history-search">${svg('search')}<input type="search" id="agent-history-search" value="${esc(agent.historyQuery||'')}" placeholder="${I18n.text('搜索会话')}" aria-label="${I18n.text('搜索会话')}" autocomplete="off"></label>${agent.job?`<small class="agent-history-busy">${I18n.text('停止当前任务后可切换会话')}</small>`:''}<div class="agent-history-list">${agentHistoryRows()}</div></aside>`;
}
function agentToggleHistory(open){
 if(open)AgentArtifacts.hide(false);
 agent.historyOpen=open;agentPaint({scroll:false});
 if(open)$('#agent-history-search')?.focus({preventScroll:true});else $('[data-agent-sessions]')?.focus({preventScroll:true});
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
 for(const key of ['agentSelect','agentRun','agentSkip','agentUndo','agentCopy','agentSaveAnswer','agentResume','agentDeleteSession','agentEditQueued','agentRemoveQueued','agentReviewTarget']){
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
 if(t.closest('[data-agent-toggle-selection]')){const rows=agent.messages.filter(m=>m.status==='pending'&&!['mcp_call','delegate_task','run_code','start_command_job','open_browser','browser_action'].includes(m.tool)),value=!rows.every(m=>m.selected!==false);rows.forEach(m=>m.selected=value);agentPaint({scroll:false});return;}
 if(t.closest('[data-agent-run-selected]')){const ids=agent.messages.filter(m=>m.status==='pending'&&!['mcp_call','delegate_task','run_code','start_command_job','open_browser','browser_action'].includes(m.tool)&&m.selected!==false).map(m=>m.id);(async()=>{for(const id of ids)await agentDecide(id,true,true);})().catch(fail);return;}
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
 read_document:{icon:'document',label:'读取文档原文'},replace_document_text:{icon:'edit',label:'修改文档正文'},read_presentation:{icon:'slide',label:'读取幻灯片'},copy_presentation:{icon:'copy',label:'复制演示文稿'},edit_presentation:{icon:'slide',label:'修改演示文稿'},
 set_collection:{icon:'folder',label:'移入分类'},add_tags:{icon:'tag',label:'添加标签'},write_note:{icon:'edit',label:'写入笔记'},
 create_document:{icon:'document',label:'新建文档'},create_presentation:{icon:'slide',label:'新建演示文稿'},open_item:{icon:'external',label:'打开文件'}
};
const AGENT_READ=['search_library','read_items','list_collections'];
const AGENT_SUGGESTIONS={
 library:[['帮我把一个想法整理成可执行的方案','edit','整理思路'],['帮我制定一份计划','list','制定计划'],['制作一份演示文稿','slide','制作演示']],
 document:[['总结要点','document'],['提取待办','check'],['生成一页笔记','edit']],
 pdf:[['看懂这份资料','document'],['提取要点','list'],['生成演示','slide']],
 files:[['比较差异','layers'],['汇总共同观点','list'],['整理成一篇笔记','edit']],
 video:[['整理重点与时间点','video'],['生成字幕摘要','document']],
 photo:[['整理图片说明','photo']]
};
// Suggestions follow the user's current material instead of presenting a fixed menu of technical actions.
function agentSuggestions(){
 const items=agent.context.filter(id=>byId(id)).map(byId);
 if(!items.length)return AGENT_SUGGESTIONS.library;
 if(items.length>1)return AGENT_SUGGESTIONS.files;
 const item=items[0];
 if(item.kind==='video')return AGENT_SUGGESTIONS.video;
 if(item.kind==='photo')return AGENT_SUGGESTIONS.photo;
 if(String(item.ext||'').toUpperCase()==='PDF')return AGENT_SUGGESTIONS.pdf;
 return AGENT_SUGGESTIONS.document;
}
const agent={...AgentPlatformUi.defaults,messages:[],context:[],budget:14000,job:null,history:[],draft:'',titleOverride:'',sessionId:uid(),sessions:[],loaded:false,saveTimer:null,scope:'all',collection:'',skill:'auto',nativeSkills:[],nativeSkillsLoaded:false,plugins:[],prompts:[],pluginSkillsCount:0,pluginsLoaded:false,sessionArtifacts:[],requestLimit:12,costLimit:0,inputRate:0,outputRate:0,usage:null,phase:'',queue:[],options:false,advancedOpen:false,lastScope:'',intent:'assist',protocol:'auto',watch:'',recovery:null,handoff:null,project:null};
// Restored input ids already handed back to the composer, so repeated snapshot pulls never duplicate them.
const agentRestoredSeen=new Set();

AgentPlatformUi.bind(()=>agent,()=>agentPaint({scroll:false}));
AgentArtifacts.bind(()=>agent,()=>{if(AgentArtifacts.active)agent.historyOpen=false;agentPaint({scroll:false});},agentRichText);
function agentAvailable(){return isSmartMode()&&!state.isPrivate&&Kok.enabled('agent');}
// P1 creative projects: the project record lives in the existing workspace store (SpaceRecord
// kind="project"); the agent only pins the project id and renders its goal beside the composer.
const agentProjectHex=id=>/^[a-f0-9]{32}$/.test(String(id||''));
async function agentProjectRecord(id){
 const projectId=String(id||'');
 if(!agentProjectHex(projectId)||state.isPrivate)return null;
 let data=null;
 try{data=await native('workspaceState');}
 catch{data=globalThis.Warehouse?.data||null;}
 const record=(data?.spaces||[]).find(row=>row&&row.id===projectId&&row.kind==='project');
 return record||null;
}
function agentProjectFromRecord(record){
 return {id:record.id,name:String(record.name||'').slice(0,100),goal:String(record.goal||'').slice(0,1000),requirements:String(record.requirements||'').slice(0,4000),status:['draft','active','done'].includes(record.status)?record.status:'active',members:(Array.isArray(record.members)?record.members:[]).filter(agentProjectHex).slice(0,500),artifacts:(Array.isArray(record.artifacts)?record.artifacts:[]).filter(agentProjectHex),sessions:(Array.isArray(record.sessions)?record.sessions:[]).filter(agentProjectHex)};
}
async function agentHydrateProject(projectId=agent.project?.id){
 const session=agent.sessionId,epoch=agentArtifactEpoch,request=++agentProjectRequest;
 const record=await agentProjectRecord(projectId);
 if(session!==agent.sessionId||epoch!==agentArtifactEpoch||request!==agentProjectRequest||state.isPrivate||agent.project?.id!==projectId)return false;
 if(!record){agent.project=null;return true;}
 agent.project=agentProjectFromRecord(record);
 return true;
}
let agentArtifactEpoch=0,agentArtifactRequest=0,agentProjectRequest=0;
function agentArtifactRows(rows){
 const seen=new Set();return (Array.isArray(rows)?rows:[]).filter(row=>row&&agentProjectHex(row.itemId)&&agentProjectHex(row.taskId)&&!seen.has(row.itemId)&&seen.add(row.itemId)).slice(0,200).map(row=>({itemId:row.itemId,taskId:row.taskId,name:String(row.name||'').slice(0,200),entryId:agentProjectHex(row.entryId)?row.entryId:'',created:String(row.created||'').slice(0,40)}));
}
async function agentRefreshArtifacts(){
 if(!agentAvailable())return;
 const session=agent.sessionId,epoch=agentArtifactEpoch,request=++agentArtifactRequest;
 try{const result=await native('agentSessionProject',{sessionId:session});
  if(session!==agent.sessionId||epoch!==agentArtifactEpoch||request!==agentArtifactRequest||!agentAvailable())return;
  agent.sessionArtifacts=agentArtifactRows(result?.artifacts);agentPaint({scroll:false});
 }catch(error){if(session===agent.sessionId&&epoch===agentArtifactEpoch)fail(error);}
}
Kok.on('*',(_,name)=>{
 if(['privateLocking','librarySwitched'].includes(name)){agentArtifactEpoch++;agentArtifactRequest++;agent.sessionArtifacts=[];if(name==='librarySwitched')agent.project=null;}
 if(name==='libraryChanged'&&agentAvailable()){if(agent.project?.id)agentRefreshProject().catch(fail);}
},{module:'agent'});
async function agentRefreshProject(projectId=agent.project?.id){
 const id=String(projectId||'');
 if(!agent.project?.id||agent.project.id!==id)return;
 try{if(await agentHydrateProject(id))agentPaint({scroll:false});}catch{}
}
function agentProjectBar(){
 const project=agent.project;
 if(!project?.id)return '<div class="agent-project" hidden style="grid-column:1/-1"></div>';
 const materials=(project.members||[]).length,artifacts=(project.artifacts||[]).length;
 return `<div class="agent-project agent-recovery" data-agent-project="${esc(project.id)}" style="grid-column:1/-1">${glyph('layers')}<span><b data-user-content>${esc(project.name||I18n.text('创作项目'))}</b>${project.goal?` · <small data-user-content>${esc(project.goal.slice(0,160))}</small>`:''}</span><small>${I18n.text('资料')} ${materials} · ${I18n.text('成果')} ${artifacts}</small><button type="button" class="icon-button" data-agent-project-clear aria-label="${I18n.text('清除项目')}" title="${I18n.text('清除项目')}">${svg('close')}</button></div>`;
}
async function openProject(projectId){
 if(!agentAvailable()){toast(I18n.text('创作项目需要在智能模式下继续。'),true);return false;}
 if(agent.job||agent.inputFlight){toast(I18n.text('请先停止当前任务。'),true);return false;}
 const session=agent.sessionId,epoch=agentArtifactEpoch;
 const project=await agentProjectRecord(projectId);
 if(session!==agent.sessionId||epoch!==agentArtifactEpoch||!agentAvailable()||agent.job||agent.inputFlight)return false;
 if(!project){toast(I18n.text('找不到创作项目。'),true);return false;}
 if(viewer?.open)await closeViewer();
 if(session!==agent.sessionId||epoch!==agentArtifactEpoch||!agentAvailable()||agent.job||agent.inputFlight)return false;
 // 继续创作 opens a fresh session so the project record (goal/requirements/materials/artifacts)
 // is the cross-session context; it never loads an unrelated old conversation.
 await agentNew();
 agent.project=agentProjectFromRecord(project);
 navigate('agent');
 agentPaint();await agentSave();
 setTimeout(()=>{const input=$('#agent-input');if(input){input.focus({preventScroll:true});input.setSelectionRange(input.value.length,input.value.length);}},60);
 return true;
}
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
 return {approvalPolicy:agent.approvalPolicy,semanticCompression:agent.semanticCompression,contextTokens:agent.contextTokens,reserveTokens:Number(agent.reserveTokens)||0,keepRecentTokens:Number(agent.keepRecentTokens)||0,useMemory:agent.useMemory,allowDelegation:agent.allowDelegation,allowSandbox:agent.allowSandbox,allowedTools:agent.allowedTools,taskId:agent.watch||'',protocol:agent.protocol,handoff:agent.handoff,sessionArtifacts:agentArtifactRows(agent.sessionArtifacts),projectId:agent.project?.id||'',projectName:String(agent.project?.name||'').slice(0,100),projectGoal:String(agent.project?.goal||'').slice(0,300),id:agent.sessionId,title:(String(agent.titleOverride||'').trim()||(agent.messages.find(m=>m.role==='user')?.text||agent.draft||I18n.text('新任务'))).slice(0,48),titleOverride:String(agent.titleOverride||'').slice(0,64),updated:new Date().toISOString(),messages:AgentModel.storedMessages(agent.messages),history:agent.history.slice(-40),draft:agent.draft,context:agent.context.slice(0,24),scope:agent.scope,collection:agent.collection,budget:agent.budget,skill:agent.skill,requestLimit:agent.requestLimit,costLimit:agent.costLimit,inputRate:agent.inputRate,outputRate:agent.outputRate,intent:agent.intent,usage:agent.usage,queue:agent.queue.slice(0,5),lastScope:agent.lastScope};
}
// Keep user compaction settings inside the host's accepted range: 0 = auto, otherwise 1024..window/2.
function agentCompactionSetting(key){
 const window=[8000,16000,32000,64000,128000].includes(agent.contextTokens)?agent.contextTokens:32000;
 const raw=Math.max(0,Math.min(64000,Math.floor(Number(agent[key])||0)));
 return raw>0?Math.max(1024,Math.min(Math.floor(window/2),raw)):0;
}
function agentSessionText(){
 const row=agentSnapshot();agent.sessions=[row,...agent.sessions.filter(s=>s.id!==row.id)].slice(0,12);
 // Evict oldest sessions before exceeding the bounded encrypted store; keep current content intact.
 while(agent.sessions.length>1&&new TextEncoder().encode(JSON.stringify(agent.sessions)).length>1300000)agent.sessions.pop();
 return JSON.stringify(agent.sessions);
}
window.kokAgentSession=()=>!state.isPrivate&&agent.loaded?agentSessionText():null;
let agentSaveFlight=null,agentPersistedContent=null,agentDeleting=false;
async function agentSave(){
 clearTimeout(agent.saveTimer);if(agentDeleting||!agent.loaded||!Kok.enabled('agent')||state.isPrivate||typeof vaultUi!=='undefined'&&vaultUi.switching||location.search.includes('preview='))return;
 // Serialize writes and coalesce paints with unchanged content. A caller queued
 // behind a write takes a fresh draft snapshot after that write finishes.
 if(agentSaveFlight){await agentSaveFlight;return agentSave();}
 const text=agentSessionText(),content=JSON.stringify(agent.sessions.map(({updated,...row})=>row));
 if(content===agentPersistedContent)return;
 const write=(async()=>{try{await native('saveAgentSessions',{text});agentPersistedContent=content;agent.saveError=false;}catch(error){if(!agent.saveError){agent.saveError=true;fail(error);}}})();
 agentSaveFlight=write;try{await write;}finally{if(agentSaveFlight===write)agentSaveFlight=null;}
}
function agentSaveSoon(){clearTimeout(agent.saveTimer);if(!agentDeleting)agent.saveTimer=setTimeout(agentSave,250);}
async function agentLoad(){
 if(agent.loaded||location.search.includes('preview='))return;
 try{const rows=JSON.parse(await native('agentSessions'));if(!Array.isArray(rows))throw Error(I18n.text('Agent 会话无效。'));agent.sessions=rows;agent.loaded=true;if(rows.length)agentRestore(rows[0].id);await agentAttach();await agentLoadNativeSkills();await agentLoadNativePlugins();}
 catch(error){agent.storageBlocked=true;fail(error);}
}
// M1-1: discovered SKILL.md directories are advertised by name; selecting one asks the model to load
// the body with the read-only load_skill tool. The list is fetched once per page session and silent:
// an older binary without skill actions simply keeps the built-in/custom picker unchanged.
function agentNativeSkillRows(result){
 const rows=Array.isArray(result?.skills)?result.skills:[];
 return rows.filter(row=>row&&typeof row.name==='string'&&/^[a-z0-9-]{1,64}$/.test(row.name)).map(row=>({name:row.name,description:String(row.description||''),path:String(row.path||''),source:String(row.source||''),disabled:!!row.disabled}));
}
function agentNativeSkillInstruction(name){return ('[Skill: '+name+'] Use load_skill to load the full instructions before acting.').slice(0,800);}
async function agentFetchNativeSkills(){
 try{agent.nativeSkills=agentNativeSkillRows(await native('agentSkillsList'));}
 catch{agent.nativeSkills=[];}
}
async function agentLoadNativeSkills(){
 if(agent.nativeSkillsLoaded)return;agent.nativeSkillsLoaded=true;
 await agentFetchNativeSkills();
 agentPaint({scroll:false});
}
async function agentReloadSkills(){
 try{agent.nativeSkills=agentNativeSkillRows(await native('agentSkillsReload'));agent.nativeSkillsLoaded=true;agentPaint({scroll:false});toast(I18n.text('技能列表已刷新')+' · '+agent.nativeSkills.length);}
 catch(error){fail(error);}
}
// M3-1: declarative extension packages may ship SKILL.md trees and markdown prompt templates. The
// list is fetched once alongside the skills (silent on older binaries); nothing from a plugin is
// executed here — templates are only inserted into the composer and skills are read by the model.
function agentPluginRows(result){
 const plugins=(Array.isArray(result?.plugins)?result.plugins:[]).filter(row=>row&&typeof row.name==='string'&&row.name).map(row=>{
  const roots=Array.isArray(row.skillRoots)?row.skillRoots:Array.isArray(row.skills)?row.skills:[];
  const direct=Number(row.skillsCount??row.skillCount);
  return {name:String(row.name).slice(0,64),version:String(row.version||'').slice(0,32),description:String(row.description||'').slice(0,200),path:String(row.path||''),skillRoots:roots.map(value=>String(value)),skillsCount:Number.isFinite(direct)&&direct>=0?Math.floor(direct):roots.length,promptCount:Math.max(0,Math.floor(Number(row.promptCount)||0))};
 });
 const prompts=(Array.isArray(result?.prompts)?result.prompts:[]).filter(row=>row&&typeof row.name==='string'&&/^[a-z0-9-]{1,64}$/.test(row.name)).map(row=>({name:row.name,description:String(row.description||'').slice(0,200),plugin:String(row.plugin||'')}));
 return {plugins,prompts,skillsCount:Math.max(0,Math.floor(Number(result?.skillsCount)||0))};
}
async function agentFetchNativePlugins(){
 try{const rows=agentPluginRows(await native('agentPluginsList'));agent.plugins=rows.plugins;agent.prompts=rows.prompts;agent.pluginSkillsCount=rows.skillsCount;}
 catch{agent.plugins=[];agent.prompts=[];agent.pluginSkillsCount=0;}
}
async function agentLoadNativePlugins(){
 if(agent.pluginsLoaded)return;agent.pluginsLoaded=true;
 await agentFetchNativePlugins();
 agentPaint({scroll:false});
}
function agentPluginSkillCount(plugin){
 // The list action exposes each plugin's skill roots; attribute the discovered skills to the plugin by
 // their path so the dialog shows the actual skill count (fallback: root count from the manifest).
 const roots=(plugin.skillRoots||[]).map(root=>String(root).replace(/\\/g,'/').replace(/\/+$/,'').toLowerCase()+'/');
 if(roots.length){
  const matched=agent.nativeSkills.filter(skill=>{const path=String(skill.path||'').replace(/\\/g,'/').toLowerCase();return roots.some(root=>path.startsWith(root));}).length;
  if(matched)return matched;
 }
 return plugin.skillsCount;
}
function agentPluginsView(){
 const pluginRows=agent.plugins.length?agent.plugins.map(plugin=>`<article data-plugin="${esc(plugin.name)}"><header><b data-user-content>${esc(plugin.name)}${plugin.version?' · '+esc(plugin.version):''}</b><small>${I18n.text('技能')} ${agentPluginSkillCount(plugin)} · ${I18n.text('提示模板')} ${plugin.promptCount}</small></header>${plugin.description?`<p data-user-content>${esc(plugin.description)}</p>`:''}</article>`).join(''):`<p>${I18n.text('还没有安装扩展包。')}</p>`;
 const promptRows=agent.prompts.length?agent.prompts.map(prompt=>`<article data-prompt="${esc(prompt.name)}"><header><b data-user-content>${esc(prompt.name)}</b>${prompt.plugin?`<small data-user-content>${esc(prompt.plugin)}</small>`:''}</header>${prompt.description?`<p data-user-content>${esc(prompt.description)}</p>`:''}</article>`).join(''):`<p>${I18n.text('还没有提示模板。')}</p>`;
 return `<div class="mcp-dialog-heading"><h2>${I18n.text('扩展包')}</h2><button type="button" class="text-button" id="agent-plugins-reload">${I18n.text('重新加载')}</button></div><p class="dialog-description">${I18n.text('声明式扩展包只提供技能与提示模板，不执行脚本或系统命令。')}</p><p><small>${I18n.text('已安装扩展包')} ${agent.plugins.length} · ${I18n.text('已发现技能')} ${agent.pluginSkillsCount} · ${I18n.text('提示模板')} ${agent.prompts.length}</small></p><div class="agent-memory-list" id="agent-plugins-list">${pluginRows}</div><h3>${I18n.text('提示模板')}</h3><p class="dialog-description">${I18n.text('在输入框使用 /prompt:名称 参数 插入模板。')}</p><div class="agent-memory-list" id="agent-prompts-list">${promptRows}</div><div class="document-form-actions"><button type="button" class="text-button" data-close-small>${I18n.text('关闭')}</button></div>`;
}
function agentOpenPlugins(){
 if(!agentAvailable())return;
 showSmall(agentPluginsView());
 I18n.localize($('#small-dialog'));
 $('#agent-plugins-reload').onclick=()=>agentReloadPlugins().catch(fail);
}
async function agentReloadPlugins(){
 const button=$('#agent-plugins-reload');if(button)button.disabled=true;
 try{
  const rows=agentPluginRows(await native('agentPluginsReload'));
  agent.plugins=rows.plugins;agent.prompts=rows.prompts;agent.pluginSkillsCount=rows.skillsCount;agent.pluginsLoaded=true;
  await agentFetchNativeSkills();agent.nativeSkillsLoaded=true;
  agentPaint({scroll:false});
  if($('#small-dialog')?.open)agentOpenPlugins();
  toast(I18n.text('扩展包已重新加载'));
 }catch(error){fail(error);}
}
function agentRestore(id){
 const row=agent.sessions.find(s=>s.id===id);if(!row)return;
 agent.sessionArtifacts=agentArtifactRows(row.sessionArtifacts);
 for(const key of ['draft','collection','skill','lastScope'])agent[key]=String(row[key]||'');
 agent.titleOverride=String(row.titleOverride||'').slice(0,64);
 if(!row.projectId||agent.project?.id!==row.projectId)agent.project=row.projectId?{id:String(row.projectId),name:String(row.projectName||'').slice(0,100),goal:String(row.projectGoal||'').slice(0,300),requirements:'',status:'active',members:[],artifacts:[],sessions:[]}:null;
 agent.watch=row.taskId||'';agent.recovery=null;agent.handoff=row.handoff||null;agent.protocol=['auto','tools','json'].includes(row.protocol)?row.protocol:'auto';agent.sessionId=row.id;agent.messages=AgentModel.storedMessages(row.messages||[]);agent.messages.forEach(m=>{if(m.nativeCall)m.undo=agentNativeUndo(m);else if(m.undoChanges)m.undo=()=>agentApply(m.undoChanges);else if(m.undoCreated)m.undo=()=>native('agentUndoCreated',m.undoCreated);});agent.context=(row.context||[]).filter(id=>byId(id)).slice(0,24);agent.history=row.history||[];agent.scope=['all','selected','collection'].includes(row.scope)?row.scope:'all';agent.budget=[6000,14000,24000,48000,64000].includes(row.budget)?row.budget:14000;
 for(const key of ['requestLimit','costLimit','inputRate','outputRate']){const n=Number(row[key]);agent[key]=Number.isFinite(n)&&n>=0&&n<=10000?n:(key==='requestLimit'?6:0);}agent.requestLimit=Math.max(1,Math.min(24,Math.floor(agent.requestLimit)));
 for(const key of ['approvalPolicy','semanticCompression','contextTokens','useMemory','allowDelegation','allowSandbox','allowedTools'])agent[key]=row[key]??AgentPlatformUi.defaults[key];
 // Compaction tuning is opt-in per session; 0 means auto on both the UI and the host side.
 for(const key of ['reserveTokens','keepRecentTokens']){const n=Number(row[key]);agent[key]=Number.isFinite(n)&&n>=0&&n<=64000?Math.floor(n):AgentPlatformUi.defaults[key];}
 agent.intent=row.intent==='discuss'?'discuss':'assist';agent.usage=row.usage||null;agent.phase='';agent.queue=(row.queue||[]).filter(t=>typeof t==='string'&&t.trim()).map(t=>t.slice(0,2000)).slice(0,5);agent.queueOpen=!!agent.queue.length;
}
async function agentNew(id){
 if(agent.job||agent.inputFlight)return;agentSessionText();if(id)agentRestore(id);else{agent.sessionId=uid();agent.messages=[];agent.history=[];agent.draft='';agent.titleOverride='';agent.context=[];agent.scope='all';agent.usage=null;agent.lastScope='';agent.phase='';agent.queue=[];agent.watch='';agent.recovery=null;agent.handoff=null;}
 agent.historyOpen=false;agent.historyQuery='';agent.queueOpen=!!agent.queue.length;
 AgentArtifacts.reset();
 if(!id)agent.sessionArtifacts=[];
 agentPaint();const input=$('#agent-input');if(input){input.value=agent.draft;input.focus();}
 const session=agent.sessionId,epoch=agentArtifactEpoch;
 if(id)await agentAttach();
 if(session!==agent.sessionId||epoch!==agentArtifactEpoch)return;
 agentPaint({scroll:false});await agentSave();
}
// Fork from one transcript message. The native session store copies the root..entry path into a new
// session and returns that branch's projection; the UI row is rebuilt from the current snapshot
// truncated at the clicked message. There is deliberately no local-only fallback: if the host action
// rejects, only the error is surfaced.
async function agentForkMessage(entryId,sourceRows=null){
 if(agent.job||agent.inputFlight||!agentAvailable()||!/^[a-f0-9]{32}$/.test(String(entryId||'')))return;
 // The branch-tree dialog can fork a node outside the visible transcript; it passes that branch's
 // projection so the new UI row is exact. Otherwise the current transcript up to the message is used.
 const branch=Array.isArray(sourceRows)&&sourceRows.length?sourceRows:null;
 const index=branch?branch.length-1:agent.messages.findIndex(m=>m.id===entryId&&['user','agent'].includes(m.role)&&!m.meta&&!m.interrupted);
 if(index<0)return;
 const sourceId=agent.sessionId,sourceRow=agentSnapshot(),sourceMessages=branch?AgentModel.storedMessages(branch):AgentModel.storedMessages(agent.messages.slice(0,index+1));
 let result;
 try{result=await native('agentSessionFork',{sessionId:sourceId,entryId});}
 catch(error){fail(error);return;}
 // The composer was idle when the fork started; another session/task may have been opened while waiting.
 if(agent.job||!agentAvailable()||agent.sessionId!==sourceId)return;
 const newId=/^[a-f0-9]{32}$/.test(String(result?.sessionId||''))?String(result.sessionId):'';
 if(!newId||newId===sourceId){fail(Error(I18n.text('Agent 会话无效。')));return;}
 const projected=Array.isArray(result?.messages)?result.messages:[];
 const forkTitle=(I18n.text('分叉 · ')+((branch?String(branch.find(m=>m.role==='user')?.text||'').slice(0,48):sourceRow.title)||I18n.text('新任务'))).slice(0,64);
 const row={...sourceRow,id:newId,sessionArtifacts:agentArtifactRows(result?.artifacts),title:forkTitle,titleOverride:forkTitle,updated:new Date().toISOString(),messages:sourceMessages,history:projected.filter(m=>m&&typeof m==='object'&&String(m.content||'')).map(m=>({role:m.role==='assistant'?'assistant':'user',content:String(m.content).slice(0,24000)})),draft:'',taskId:'',queue:[],usage:null};
 // Keep the session that was forked from in the saved list before switching away from it.
 agentSessionText();
 agent.sessions=[row,...agent.sessions.filter(s=>s.id!==newId)].slice(0,12);
 agentRestore(newId);agentPaint();agentToggleHistory(false);
 if(agent.project?.id)await agentRefreshProject();
 toast(I18n.text('已从这条消息分叉，继续输入会走这条分支'));
 await agentSave();
}
// Manual compaction only changes native state: the engine consumes the request at the next safe
// boundary, and the returned snapshot is applied like any other task read. Rejections surface via fail().
async function agentCompactRequest(){
 const job=agent.job;if(!job?.id||agent.inputFlight||!agentAvailable())return;
 try{const snap=await native('agentTaskCompact',{taskId:job.id});if(agent.job!==job)return;agentApplySnapshot(snap);}
 catch(error){fail(error);}
}
// M1-3: in-place branch tree. The dialog renders native session entries in pre-order with the active
// leaf marker; switching rebuilds the visible transcript from the native projection so the next task
// continues on that branch. Summary rows are untrusted text, never authorization.
let agentTreeData=null;
function agentProjectedMessages(rows){
 return (Array.isArray(rows)?rows:[]).filter(row=>row&&(row.role==='user'||row.role==='assistant')).map(row=>({id:/^[a-f0-9]{32}$/.test(String(row.id||''))?String(row.id):uid(),role:row.role==='user'?'user':'agent',text:String(row.content==null?'':row.content)}));
}
function agentTreeRows(entries){
 const known=new Set(entries.map(entry=>entry.id)),children=new Map(),seen=new Set(),rows=[];
 for(const entry of entries){const key=entry.parentId&&known.has(entry.parentId)?entry.parentId:'__root';if(!children.has(key))children.set(key,[]);children.get(key).push(entry);}
 // Iterative pre-order walk: sessions can hold thousands of entries, so no recursion on the chain.
 const stack=(children.get('__root')||[]).slice().reverse().map(entry=>({entry,depth:0}));
 while(stack.length){const node=stack.pop();if(seen.has(node.entry.id))continue;seen.add(node.entry.id);rows.push([node.entry,node.depth]);const kids=children.get(node.entry.id)||[];for(let index=kids.length-1;index>=0;index--)stack.push({entry:kids[index],depth:node.depth+1});}
 for(const entry of entries)if(!seen.has(entry.id)){seen.add(entry.id);rows.push([entry,0]);}
 return rows;
}
function agentTreeReason(reason){return ({threshold:I18n.text('自动压缩'),manual:I18n.text('手动压缩'),overflow:I18n.text('溢出恢复')})[reason]||String(reason||'');}
function agentTreeRow(entry,depth,leaf){
 const pad=8+depth*16,current=entry.id===leaf,mark=current?`<span class="agent-branch-current">${I18n.text('当前')}</span>`:'',currentAttr=current?' data-branch-current="true"':'';
 if(entry.type==='artifact')return `<article class="agent-branch-row" data-branch-type="artifact" data-branch-id="${esc(entry.id)}"${currentAttr} style="padding-left:${pad}px"><header><b>${I18n.text('成果')}</b><span class="agent-branch-preview" data-user-content>${esc(entry.name||byId(entry.itemId)?.name||I18n.text('文档'))}</span>${mark}</header><footer><button type="button" class="text-button" data-agent-branch-switch="${esc(entry.id)}">${I18n.text('切换到此')}</button><button type="button" class="text-button" data-agent-branch-fork="${esc(entry.id)}">${I18n.text('从此处分叉')}</button></footer></article>`;
 if(entry.type==='message'){
  const role=entry.role==='user'?'user':entry.role==='assistant'?'agent':'';
  const roleLabel=role==='user'?I18n.text('你'):role==='agent'?'Agent':I18n.text('消息');
  const preview=String(entry.preview==null?'':entry.preview).trim()||I18n.text('（无预览）');
  return `<article class="agent-branch-row" data-branch-type="message" data-branch-id="${esc(entry.id)}" data-branch-role="${role||'unknown'}"${currentAttr} style="padding-left:${pad}px"><header><b class="agent-branch-role">${esc(roleLabel)}</b><span class="agent-branch-preview" data-user-content>${esc(preview)}</span>${mark}</header><footer><button type="button" class="text-button" data-agent-branch-switch="${esc(entry.id)}">${I18n.text('切换到此')}</button><button type="button" class="text-button" data-agent-branch-fork="${esc(entry.id)}">${I18n.text('从此处分叉')}</button></footer></article>`;
 }
 if(entry.type==='compaction'){
  const reason=String(entry.reason||entry.data?.reason||''),summary=String(entry.preview||entry.summary||'').trim();
  const label=I18n.text('上下文摘要')+(reason?' · '+esc(agentTreeReason(reason)):'');
  return `<article class="agent-branch-row agent-branch-meta-row" data-branch-type="compaction" data-branch-id="${esc(entry.id)}"${currentAttr} style="padding-left:${pad}px"><small>${label}${current?' · '+mark:''}</small>${summary?`<p data-user-content>${esc(summary)}</p>`:''}</article>`;
 }
 if(entry.type==='label'){
  const label=String(entry.label||entry.data?.label||'').trim();
  return `<article class="agent-branch-row agent-branch-meta-row" data-branch-type="label" style="padding-left:${pad}px"><small>${I18n.text('标签')}${label?' · '+esc(label):''}</small></article>`;
 }
 if(entry.type==='task')return `<article class="agent-branch-row agent-branch-meta-row agent-branch-task" data-branch-type="task" style="padding-left:${pad}px"><small>${I18n.text('任务记录')}</small></article>`;
 return '';
}
function agentShowTreeDialog(){
 const data=agentTreeData;if(!data)return;
 const rows=agentTreeRows(data.entries).filter(([entry])=>['message','compaction','label','task','artifact'].includes(entry.type));
 const title=String(data.title||agent.titleOverride||'').trim();
 showSmall(`<div class="mcp-dialog-heading"><h2>${I18n.text('会话分支')}</h2><div class="agent-branch-actions"><button type="button" class="text-button" data-agent-tree-rename>${I18n.text('重命名')}</button><button type="button" class="text-button" data-close-small>${I18n.text('关闭')}</button></div></div><p class="dialog-description" data-agent-tree-title data-user-content>${esc(title||I18n.text('未命名会话'))}</p><p><small>${I18n.text('当前分支')} · ${rows.length} ${I18n.text('条记录')}</small></p><form id="agent-tree-rename-form" hidden><label class="form-label">${I18n.text('会话名称')}<input id="agent-tree-title-input" maxlength="64" required value="${esc(title)}"></label><div class="document-form-actions"><button type="button" class="text-button" data-agent-tree-rename-cancel>${I18n.text('取消')}</button><button type="submit" class="button primary">${I18n.text('保存')}</button></div></form><div class="agent-memory-list agent-branch-list">${rows.map(([entry,depth])=>agentTreeRow(entry,depth,data.leaf)).join('')||`<p>${I18n.text('暂无分支记录')}</p>`}</div>`);
 I18n.localize($('#small-dialog'));
 $('#small-dialog').onclick=event=>agentTreeDialogClick(event).catch(fail);
 $('#agent-tree-rename-form').onsubmit=event=>{event.preventDefault();agentRenameSession().catch(fail);};
}
async function agentOpenTree(){
 if(agent.job||agent.inputFlight||!agentAvailable())return;
 let tree;
 try{tree=await native('agentSessionTree',{sessionId:agent.sessionId});}
 catch(error){fail(error);return;}
 if(agent.job||!agentAvailable())return;
 const entries=Array.isArray(tree?.entries)?tree.entries.filter(entry=>entry&&typeof entry.id==='string'):[];
 agentTreeData={sessionId:agent.sessionId,title:String(tree?.title||''),leaf:String(tree?.leaf||''),entries};
 agentShowTreeDialog();
}
async function agentSwitchBranch(leafId){
 if(agent.job||agent.inputFlight||!/^[a-f0-9]{32}$/.test(String(leafId||'')))return;
 const session=agent.sessionId,epoch=agentArtifactEpoch;
 let result;
 try{result=await native('agentSessionSwitch',{sessionId:agent.sessionId,leafId});}
 catch(error){fail(error);return;}
 if(agent.job||!agentAvailable()||session!==agent.sessionId||epoch!==agentArtifactEpoch)return;
 const messages=agentProjectedMessages(result?.messages);
 agentArtifactRequest++;agent.sessionArtifacts=agentArtifactRows(result?.artifacts);
 agent.messages=messages;
 agent.history=messages.map(row=>({role:row.role==='user'?'user':'assistant',content:row.text}));
 agent.draft='';agent.watch='';agent.recovery=null;agent.queue=[];agent.usage=null;agent.phase='';
 const input=$('#agent-input');if(input)input.value='';
 await agentSave();
 await closeSmall();
 agentPaint();
 toast(I18n.text('已切换到该分支，后续消息会从这里继续'));
}
async function agentForkNode(entryId){
 if(agent.job||agent.inputFlight||!/^[a-f0-9]{32}$/.test(String(entryId||'')))return;
 await closeSmall();
 if(agent.messages.some(row=>row.id===entryId&&['user','agent'].includes(row.role)&&!row.meta&&!row.interrupted))return agentForkMessage(entryId);
 // A node on another branch is projected first so the forked UI row is exact; the native fork path
 // itself is unchanged and its validation errors still surface through fail().
 let project=null;
 try{project=await native('agentSessionProject',{sessionId:agent.sessionId,leafId:entryId});}
 catch(error){fail(error);return;}
 const rows=agentProjectedMessages(project?.messages);
 if(!rows.length){fail(Error(I18n.text('找不到分支记录。')));return;}
 return agentForkMessage(entryId,rows);
}
async function agentRenameSession(){
 const input=$('#agent-tree-title-input');if(!input||!agentTreeData)return;
 const title=input.value.trim();
 if(!title||title.length>64){toast(I18n.text('会话名称无效。'),true);input.focus();return;}
 const sessionId=agentTreeData.sessionId;
 try{await native('agentSessionRename',{sessionId,title});}
 catch(error){fail(error);return;}
 agentTreeData.title=title;
 if(agent.sessionId===sessionId)agent.titleOverride=title;
 const label=$('#small-dialog [data-agent-tree-title]');if(label)label.textContent=title;
 const form=$('#agent-tree-rename-form');if(form)form.hidden=true;
 agentPaint({scroll:false});
 await agentSave();
 toast(I18n.text('已重命名会话'));
}
async function agentTreeDialogClick(event){
 const t=event.target;
 const switchButton=t.closest('[data-agent-branch-switch]');
 if(switchButton){event.preventDefault();await agentSwitchBranch(switchButton.dataset.agentBranchSwitch);return;}
 const forkButton=t.closest('[data-agent-branch-fork]');
 if(forkButton){event.preventDefault();await agentForkNode(forkButton.dataset.agentBranchFork);return;}
 if(t.closest('[data-agent-tree-rename]')){const form=$('#agent-tree-rename-form');if(!form)return;form.hidden=false;const input=$('#agent-tree-title-input');if(input){input.value=agentTreeData?.title||agent.titleOverride||'';input.focus();input.select();}return;}
 if(t.closest('[data-agent-tree-rename-cancel]')){const form=$('#agent-tree-rename-form');if(form)form.hidden=true;return;}
}
function agentScope(){
 // Selected material is the strongest signal: while chips exist an "all" preference acts as "selected".
 const hints=agent.context.filter(id=>byId(id));
 const mode=agent.scope==='collection'?'collection':agent.scope==='selected'||hints.length?'selected':'all';
 if(agent.project?.id&&(mode==='all'||mode==='selected'&&!hints.length)){
  const ids=[...new Set([...(agent.project.artifacts||[]).slice().reverse(),...(agent.project.members||[])])].filter(id=>byId(id)).slice(0,24);
  if(ids.length)return {mode:'selected',ids,collection:''};
 }
 return {mode,ids:mode==='selected'?hints:[],collection:mode==='collection'?agent.collection:''};
}
function agentScopeLabel(){
 const scope=agentScope();
 if(scope.mode==='collection')return I18n.text('范围：分类')+' '+(scope.collection||I18n.text('未选择'));
 if(scope.mode==='selected')return I18n.text('范围：所选')+' '+scope.ids.length+' '+I18n.text('份');
 return I18n.text('范围：整个资料库');
}
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
  case 'read_document':case 'replace_document_text':case 'read_presentation':case 'edit_presentation':return agentItemName(args.id);
  case 'copy_presentation':return args.name;
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
 if(m.tool==='edit_presentation')return m.status==='pending'?`<button type="button" class="text-button agent-slide-preview-action" data-agent-preview-slides="${m.id}">${svg('slide')}${I18n.text('预览幻灯片')} · ${m.args.edits.length} ${I18n.text('项修改')}</button>`:'';
 const a=m.args;if(m.tool==='mcp_call')return `<div class="agent-external-warning">${I18n.text('外部工具可访问当前账户的文件和网络，不受资料库范围沙箱限制，且不能自动撤销。')}</div><dl class="agent-external-target"><dt>${I18n.text('服务 ID')}</dt><dd data-user-content>${esc(a.serverId)}</dd><dt>${I18n.text('工具')}</dt><dd data-user-content>${esc(a.tool)}</dd></dl><pre class="agent-preview" data-user-content>${esc(JSON.stringify(a.arguments,null,2))}</pre>`;if(m.changes?.length)return agentMetadataDiff(m);if(['write_note','replace_document_text'].includes(m.tool))return `<details class="agent-diff"><summary>${I18n.text('查看修改前后')}</summary><div><section><b>${I18n.text('修改前')}</b><pre data-user-content>${esc(m.beforeText??byId(a.id)?.notes??'')}</pre></section><section><b>${I18n.text('修改后')}</b><pre data-user-content>${esc(m.afterText??a.text)}</pre></section></div></details>`;
 if(['create_document','create_presentation'].includes(m.tool)){
  const preview=m.tool==='create_document'?`<div class="agent-preview agent-rich-text" data-user-content>${agentRichText(a.text.slice(0,1200)+(a.text.length>1200?'…':''))}</div>`:`<ol class="agent-preview agent-slides" data-user-content>${a.slides.map(s=>`<li><b>${esc(s.title)}</b>${(s.bullets||[]).length?`<span>${esc(s.bullets.join(' / '))}</span>`:''}</li>`).join('')}</ol>${m.status==='pending'?`<button type="button" class="text-button agent-slide-preview-action" data-agent-preview-slides="${m.id}">${svg('slide')}${I18n.text('预览幻灯片')}</button>`:''}`;
  return m.status==='done'?`<details class="agent-completed-preview"><summary>${I18n.text('查看内容')}</summary>${preview}</details>`:preview;
 }
 return '';
}
// A finished task always ends with one durable result card: counts, sources and the next step, never a dead end.
function agentResultState(status){return status==='completed'?'done':['partial','limited'].includes(status)?'partial':'stopped';}
function agentResultStatus(status,errorKind){return errorKind==='output_limit'?I18n.text('回答未完成'):status==='completed'?I18n.text('已完成'):status==='partial'||status==='limited'?I18n.text('部分完成'):status==='needs_input'||status==='review_required'?I18n.text('需要补充信息'):I18n.text('已停止');}
function agentResultCounts(sources,citations){return I18n.text('使用 '+sources+' 份资料 · '+citations+' 处来源引用');}
function agentTaskResult(job,snap){
 const messages=snap.messages||[],calls=messages.filter(m=>m.role==='tool'),hex=id=>/^[a-f0-9]{32}$/.test(String(id||''));
 // #26 input sources vs generated outputs: ids this task created/copied are deliverables, not sources. The
 // artifact itself stays in artifactId / the open button; counts and source chips must not include it.
 const createdIds=new Set(calls.filter(m=>m.status==='done'&&['create_document','create_presentation','copy_presentation'].includes(m.tool)&&hex(m.result?.id)).map(m=>m.result.id));
 const sourceId=id=>hex(id)&&!createdIds.has(id);
 const sourceIds=[...new Set(calls.flatMap(m=>(m.sourceIds||[]).filter(sourceId)))];if(!sourceIds.length)for(const m of calls)for(const r of m.result?.references||[])if(sourceId(r?.fileId)&&!sourceIds.includes(r.fileId))sourceIds.push(r.fileId);
 const citations=[...new Set(messages.filter(m=>m.role==='agent'&&!m.meta&&!m.interrupted).flatMap(m=>(m.sourceIds||[]).filter(sourceId)))];
 const artifact=[...calls].reverse().find(m=>['create_document','create_presentation','copy_presentation','edit_presentation','replace_document_text'].includes(m.tool)&&m.status==='done'&&hex(m.result?.id));
 const reply=[...messages].reverse().find(m=>m.role==='agent'&&!m.meta&&!m.interrupted);
 return {id:uid(),role:'result',kind:'task',local:false,errorKind:String(snap.errorKind||''),status:String(snap.status||'stopped'),title:String(artifact?(artifact.result.name||artifact.args?.name||artifact.args?.title||''):'').slice(0,200),summary:String(snap.verification?.summary||reply?.text||'').slice(0,600),sourceCount:sourceIds.length,citationCount:citations.length,sourceIds:sourceIds.slice(0,24),artifactId:artifact?.result?.id||'',goal:job.goal||''};
}
function agentResultCard(m){
 const artifactId=/^[a-f0-9]{32}$/.test(String(m.artifactId||''))?m.artifactId:'';
 const counts=m.sourceCount||m.citationCount?agentResultCounts(m.sourceCount||0,m.citationCount||0):'';
 const ids=(m.sourceIds||[]).filter(id=>byId(id));
 const sources=ids.length?`<div class="agent-sources">${agentSourceButtons(ids)}</div>`:`<p class="agent-result-note">${I18n.text('本次任务未引用资料')}</p>`;
 return `<article class="agent-result ${artifactId?'agent-result-artifact':'agent-result-compact'}" data-agent-result="${esc(m.id)}"><header class="agent-result-head"><span class="agent-result-status" data-state="${esc(agentResultState(m.errorKind==='output_limit'?'partial':m.status))}">${svg(m.status==='completed'?'check':'info')}<span>${esc(agentResultStatus(m.status,m.errorKind))}</span></span>${m.title?`<h3 data-user-content>${esc(m.title)}</h3>`:''}</header>${counts?`<p class="agent-result-counts">${esc(counts)}</p>`:''}<div class="agent-result-actions">${artifactId?`${byId(artifactId)?.ownedDocument&&['MD','TXT','DOCX'].includes(byId(artifactId).ext)?`<button type="button" class="button secondary" data-pi-artifact="${esc(artifactId)}">${svg('panel')}${I18n.text('预览')}</button>`:''}<button type="button" class="text-button" data-agent-open-item="${esc(artifactId)}">${svg('external')}${I18n.text('打开文档')}</button>`:''}${ids.length?`<details class="agent-result-sources"><summary class="text-button">${I18n.text('查看来源')}</summary>${sources}</details>`:''}<button type="button" class="text-button" data-agent-result-continue="${esc(m.id)}">${I18n.text(m.errorKind==='output_limit'?'分段继续':m.status==='needs_input'?'补充信息':'继续修改')}</button></div></article>`;
}
function agentLocalCard(m){
 // Native local answers self-localize from the goal language, so title/summary/stats/followups stay raw data.
 const stats=(m.stats||[]).slice(0,8).map(stat=>`<div class="agent-local-stat"><span>${esc(String(stat.label||''))}</span><b data-user-content>${esc(String(stat.value??''))}</b></div>`).join('');
 const items=(m.items||[]).slice(0,12).map(item=>`<button type="button" class="agent-source" data-agent-open-item="${esc(item.id)}" title="${esc(item.name)}" data-user-content>${svg(item.kind)}<span>${esc(item.name)}</span></button>`).join('');
 const followups=(m.followups||[]).slice(0,4).map(text=>`<button type="button" class="button secondary agent-local-followup" data-agent-local-followup="${esc(text)}" data-agent-result-id="${esc(m.id)}" data-user-content>${esc(I18n.text(text))}</button>`).join('');
 const shown=(m.items||[]).length,total=Number(m.total)||0;
 return `<article class="agent-result agent-result-local" data-agent-local="${esc(m.id)}"><header class="agent-result-head"><span class="agent-result-status" data-state="done">${svg('check')}<span>${I18n.text('本地完成')}</span></span><small>${I18n.text('未调用模型')}</small></header><h3 data-user-content>${esc(m.title?I18n.text(m.title):I18n.text('任务结果'))}</h3>${m.summary?`<p class="agent-result-summary" data-user-content>${esc(m.summary)}</p>`:''}${stats?`<div class="agent-local-stats">${stats}</div>`:''}${items?`<div class="agent-sources agent-local-items" aria-label="${I18n.text('相关资料')}">${items}</div>`:''}${m.truncated&&shown?`<small class="agent-result-note">${esc(I18n.text('仅显示前 '+shown+' 项'))}${total?` · ${esc(I18n.text('共 '+total+' 项'))}`:''}</small>`:''}${followups?`<div class="agent-result-actions">${followups}</div>`:''}</article>`;
}
function agentContinueResult(m){
 if(!m||agent.job)return;
 const artifact=/^[a-f0-9]{32}$/.test(String(m.artifactId||''))&&byId(m.artifactId)?m.artifactId:'';
 if(artifact)agent.context=[...agent.context.filter(id=>id!==artifact),artifact].slice(-24);
 let text=m.status==='needs_input'?agent.draft:['failed','limited'].includes(m.status)?m.goal||'':I18n.text('继续修改：');if(m.errorKind==='output_limit'&&text.length<1800)text+='\n\n'+I18n.text('请按原目标分段完成：本轮只完成第一部分，保留已有成果和其他内容，简短说明还剩什么。');agent.draft=text;
 agentPaint({scroll:false});
 const input=$('#agent-input');if(input){input.value=text;input.focus();input.setSelectionRange(text.length,text.length);}
}
function agentFillLocalFollowup(button){
 // The native planner returns complete follow-up prompts, including the original goal for the AI fallback.
 const m=agent.messages.find(row=>row.id===button?.dataset.agentResultId),followup=String(button?.dataset.agentLocalFollowup||''),text=followup||m?.goal||'';
 agent.draft=text;agentPaint({scroll:false});
 const input=$('#agent-input');if(input){input.value=text;input.focus();input.setSelectionRange(text.length,text.length);}
}
function agentMessage(m){
 if(m.role==='user'){const fork=agentForkButton(m);return `<div class="agent-msg agent-user"><span data-user-content>${esc(m.text)}</span>${fork?`<div class="agent-answer-actions">${fork}</div>`:''}</div>`;}
 if(m.role==='agent')return agentAnswer(m);
 if(m.role==='thinking')return `<div class="agent-msg agent-thinking" role="status"><span class="agent-led"></span><span id="agent-live-reply" data-user-content>${esc(agent.job?.partial||I18n.text('正在思考'))}</span><i></i><i></i><i></i></div>`;
 if(m.role==='error')return `<div class="agent-msg agent-error" role="alert"><span>${esc(m.text)}</span>${m.retryable&&m.taskId&&!agent.job?`<button type="button" class="text-button" data-agent-continue="${esc(m.taskId)}">${I18n.text('继续任务')}</button>`:''}${m.goal&&m.errorKind!=='output_limit'&&!agent.job?`<button type="button" class="text-button" data-agent-edit-request="${m.id}">${I18n.text('修改后重试')}</button>`:''}</div>`;
 if(m.role==='result')return m.kind==='local'?agentLocalCard(m):agentResultCard(m);
 const tool=AGENT_TOOLS[m.tool]||{icon:'info',label:m.tool},read=AGENT_READ.includes(m.tool);
 const controls=m.status==='pending'?`${m.grantEligible?`<label class="agent-grant"><input type="checkbox" data-agent-grant="${m.id}">${I18n.text('本任务内允许同类修改（这些资料，后续最多四次）')}</label>`:''}<div class="agent-card-actions"><button class="button primary" data-agent-run="${m.id}">${svg('check')}${I18n.text('执行')}</button><button class="button secondary" data-agent-skip="${m.id}">${I18n.text('跳过')}</button></div>`
  :m.status==='done'?`<div class="agent-card-actions"><span class="agent-status ok">${svg('check')}${I18n.text('已完成')}</span>${m.undo?`<button class="text-button" data-agent-undo="${m.id}">${svg('undo')}${I18n.text('撤销')}</button>`:''}${m.result?.id&&byId(m.result.id)?`<button class="text-button" data-agent-open-item="${esc(m.result.id)}">${svg('external')}${I18n.text('打开')}</button>`:''}</div>`
  :m.status==='skipped'?`<div class="agent-card-actions"><span class="agent-status">${I18n.text('已跳过')}</span></div>`
  :m.status==='undone'?`<div class="agent-card-actions"><span class="agent-status">${I18n.text('已撤销')}</span></div>`
  :m.status==='failed'?`<div class="agent-card-actions"><span class="agent-status bad">${esc(m.error||I18n.text('执行失败'))}</span></div>`
  :m.status==='running'?`<div class="agent-card-actions"><span class="agent-status"><span class="spinner"></span>${I18n.text('执行中')}</span></div>`:'';
 return `<article class="agent-card ${read?'agent-card-read':''} agent-${m.status}" data-agent-card="${m.id}"><header>${m.status==='pending'&&!['mcp_call','delegate_task','run_code','start_command_job','open_browser','browser_action'].includes(m.tool)?`<input type="checkbox" data-agent-select="${m.id}" ${m.selected!==false?'checked':''} aria-label="${I18n.text('选择此修改')}">`:''}${svg(tool.icon)}<b>${I18n.text(tool.label)}</b><span data-user-content>${esc(agentSummary(m.tool,m.args))}</span></header>${m.why?`<p data-user-content>${esc(m.why)}</p>`:''}${read?'':agentPreview(m)}${m.tool==='mcp_call'&&m.readPreview?`<details class="agent-external-result"><summary>${I18n.text('外部返回')}</summary><pre data-user-content>${esc(m.readPreview)}</pre>${m.resultEventId?`<button type="button" class="text-button" data-agent-full-result="${m.id}">${I18n.text('完整外部结果')}</button>`:''}</details>`:''}${read?(m.status==='done'?`<small>${I18n.text('已自动读取，未修改任何内容')}</small><div class="agent-sources">${agentSourceButtons(m.sourceIds)}</div>${m.readPreview?`<details class="agent-read-preview"><summary>${I18n.text('查看读取节选')}</summary><pre data-user-content>${esc(m.readPreview)}</pre>${m.resultEventId?`<button type="button" class="text-button" data-agent-full-result="${m.id}">${I18n.text('完整读取结果')}</button>`:''}</details>`:''}`:m.status==='failed'?`<small class="agent-read-error">${esc(m.error)}</small>`:''):controls}${AgentPlatformUi.result(m)}</article>`;
}
function agentEmpty(){
 const suggestions=agentSuggestions();
 const example=globalThis.DocumentUi?.exampleAvailable()?`<button type="button" data-example-work>${glyph('document')}<span>${I18n.text('用示例试一次')}</span></button>`:'';
 return `<div class="agent-empty"><h1>${I18n.text('有什么想法？')}</h1>
 <div class="agent-starters">${example}${suggestions.map(([text,icon,label])=>`<button type="button" data-agent-starter="${esc(I18n.text(text))}">${glyph(icon)}<span>${I18n.text(label||text)}</span></button>`).join('')}</div></div>`;
}
function agentMore(anchor){
 GlassMenu.open(anchor,[
  {key:'tasks',label:I18n.text('任务记录'),icon:'list',action:()=>agentTaskRecords()},
  {key:'memory',label:I18n.text('长期记忆'),icon:'bookmark',action:()=>AgentPlatformUi.memories()},
  {key:'command-jobs',label:I18n.text('后台作业'),icon:'list',action:()=>AgentPlatformUi.commandJobs()},
  {key:'schedules',label:I18n.text('计划任务'),icon:'calendar',disabled:!!agent.job,action:()=>AgentPlatformUi.schedules()}
 ],{label:I18n.text('更多'),focus:true});
}
// M0-3: actual/estimated context usage from the native snapshot, a running-only manual compaction
// trigger, and the last checkpoint summary. The summary is untrusted data, never authorization.
function agentCompactReason(reason){return I18n.text(({threshold:'自动压缩',manual:'手动压缩',overflow:'溢出恢复'})[reason]||String(reason||''));}
function agentContextUsageView(){
 const usage=agent.recovery?.contextUsage;
 if(!usage)return '<div class="agent-context-usage" hidden></div>';
 const tokens=Math.max(0,Math.floor(Number(usage.tokens)||0)),window=Math.max(0,Math.floor(Number(usage.window)||0));
 const percent=Number.isFinite(Number(usage.percent))?Number(usage.percent):window?Math.round(1000*tokens/window)/10:0;
 const source=usage.source==='usage'?'usage':'estimate';
 const last=usage.lastCompaction&&typeof usage.lastCompaction==='object'?usage.lastCompaction:null;
 return `<div class="agent-context-usage" data-source="${source}"><span class="agent-context-meter" title="${esc(I18n.text('上下文用量'))}">${I18n.text('上下文')} ${tokens}/${window} · ${percent}%</span><span class="agent-context-source" data-source="${source}" title="${esc(I18n.text(source==='usage'?'来自服务商的实际用量':'未获得实际用量，按结构估算'))}">${I18n.text(source==='usage'?'实测':'估算')}</span>${agent.job&&!['research','deepagents'].includes(agent.recovery?.options?.runtime)?`<button type="button" class="text-button" data-agent-compact title="${esc(I18n.text('压缩上下文'))}" aria-label="${esc(I18n.text('压缩上下文'))}">${I18n.text('压缩上下文')}</button>`:''}${last?`<details class="agent-context-summary"><summary>${I18n.text('上下文摘要 · ')}${esc(agentCompactReason(last.reason))}${last.current===false?' · '+I18n.text('已失效'):''}</summary><p class="agent-context-untrusted">${I18n.text('以下为不可信摘要，仅供参考，不代表当前授权；以宿主回执为准。')}</p><pre data-user-content>${esc(String(last.summary||''))}</pre></details>`:''}</div>`;
}
// M2: cache-hit share for the usage footnote. Hidden when the provider reported no cached input
// tokens (or an unknown input total); never estimate a percentage from zero/unknown data.
function agentCacheUsageText(usage){
 const cached=Math.floor(Number(usage?.cachedTokens)||0),input=Math.floor(Number(usage?.inputTokens)||0);
 if(!(cached>0)||!(input>0))return '';
 const percent=Math.max(0,Math.min(100,Math.round(100*cached/input)));
 return I18n.text('缓存命中')+' '+cached.toLocaleString()+' tokens · '+percent+'%';
}
function agentView(){
 if(!agentAvailable())return `<section class="agent-page agent-off"><div class="agent-empty"><h1>${I18n.text('Agent 需要智能模式')}</h1><p>${I18n.text('纯净模式和私密文件夹不会发送任何 AI 请求。切换到智能模式并配置 AI 服务后即可使用。')}</p><button class="button primary" data-agent-settings>${I18n.text('切换到智能模式')}</button></div></section>`;
 const busy=!!agent.job,skills=[...AgentModel.presets,...AgentSkills.custom()],localSkillNames=new Set(skills.map(s=>s.name)),discoveredSkills=agent.nativeSkills.filter(s=>!localSkillNames.has(s.name)&&s.name!=='auto'),scope=agentScope();
 const usage=agent.usage;const used=usage?`${usage.estimated?I18n.text('保守估算'):I18n.text('服务商用量')} · ${usage.inputTokens} / ${usage.outputTokens} tokens${agent.inputRate&&agent.outputRate?' · '+usage.cost.toFixed(5):''}`:I18n.text('仅在发送后调用 API');
 // M2: the provider reports cached input tokens once prompt caching is enabled; show the hit share
 // against total input tokens, and keep the existing one-line usage footnote when there is nothing to report.
 const cacheUsage=agentCacheUsageText(agent.recovery?.usage||usage);
 const usageLine=usage?(cacheUsage?used+' · '+cacheUsage:used):cacheUsage||I18n.text('仅在发送后调用 API');
 // Technical choices live in one collapsed panel; the first screen starts from material and intent.
 const advanced=`<details class="agent-advanced" id="agent-advanced"${agent.advancedOpen?' open':''}><summary>${svg('settings')}<span>${I18n.text('高级设置')}</span></summary><div class="agent-advanced-body">
  <div class="agent-advanced-row"><label class="agent-advanced-field"><span>${I18n.text('资料范围')}</span><select id="agent-scope" aria-label="${I18n.text('资料范围')}" ${busy?'disabled':''}>${[['all','整个资料库'],['selected','仅所选资料'],['collection','指定文件夹']].map(([v,l])=>`<option value="${v}" ${scope.mode===v?'selected':''}>${I18n.text(l)}</option>`).join('')}</select></label>${scope.mode==='collection'?`<label class="agent-advanced-field"><span>${I18n.text('文件夹')}</span><select id="agent-collection" aria-label="${I18n.text('文件夹')}" ${busy?'disabled':''}><option value="">${I18n.text('选择文件夹')}</option>${state.collections.map(c=>`<option value="${esc(c)}" ${c===agent.collection?'selected':''} data-user-content>${esc(c)}</option>`).join('')}</select></label>`:''}</div>
  ${scope.mode==='selected'&&agent.scope!=='selected'?`<small class="agent-advanced-hint">${I18n.text('已选资料优先，范围按所选执行。')}</small>`:''}
  <div class="agent-advanced-row"><label class="agent-advanced-field"><span>${I18n.text('任务方式')}</span><select id="agent-intent" aria-label="${I18n.text('任务方式')}" ${busy?'disabled':''}><option value="assist" ${agent.intent==='assist'?'selected':''}>${I18n.text('协作修改')}</option><option value="discuss" ${agent.intent==='discuss'?'selected':''}>${I18n.text('仅讨论')}</option></select></label><label class="agent-advanced-field"><span>${I18n.text('技能')}</span><select id="agent-skill" aria-label="${I18n.text('技能')}" ${busy?'disabled':''}><option value="auto" ${agent.skill==='auto'?'selected':''}>${I18n.text('自动选择技能')}</option><option value="" ${!agent.skill?'selected':''}>${I18n.text('不使用技能')}</option>${skills.map(s=>`<option value="${esc(s.name)}" ${agent.skill===s.name?'selected':''}>${esc(I18n.text(s.title||s.name))}</option>`).join('')}${discoveredSkills.length?`<optgroup label="${esc(I18n.text('已发现的技能'))}">${discoveredSkills.map(s=>`<option value="${esc(s.name)}" title="${esc(s.description||'')}" ${agent.skill===s.name?'selected':''}>${esc(s.name)}</option>`).join('')}</optgroup>`:''}</select></label><button type="button" class="text-button" data-agent-skills-refresh aria-label="${I18n.text('刷新技能')}" title="${I18n.text('刷新技能')}" ${busy?'disabled':''}>${I18n.text('刷新技能')}</button><button type="button" class="text-button" data-agent-plugins aria-label="${I18n.text('扩展包')}" title="${I18n.text('扩展包')}" ${busy?'disabled':''}>${I18n.text('扩展包')}</button></div>
  <div class="agent-advanced-row"><label class="agent-advanced-field"><span>${I18n.text('用量')}</span><select id="agent-budget" aria-label="${I18n.text('用量')}" ${busy?'disabled':''}>${[[6000,'省用量'],[14000,'均衡'],[24000,'更多上下文'],[48000,'长任务'],[64000,'超长任务']].map(([v,l])=>`<option value="${v}" ${agent.budget===v?'selected':''}>${I18n.text(l)}</option>`).join('')}</select></label><button type="button" class="text-button" data-agent-limits aria-expanded="${agent.options}" ${busy?'disabled':''}>${I18n.text('用量设置')}</button></div>
  <div class="agent-limits" ${agent.options?'':'hidden'}><label>${I18n.text('调用协议')}<select id="agent-protocol" ${busy?'disabled':''}>${[['auto','自动'],['tools','原生工具调用'],['json','兼容 JSON']].map(([v,l])=>`<option value="${v}" ${agent.protocol===v?'selected':''}>${I18n.text(l)}</option>`).join('')}</select></label>${[['requestLimit','请求上限',1,24,1],['inputRate','每百万输入 token 单价',0,10000,'any'],['outputRate','每百万输出 token 单价',0,10000,'any'],['costLimit','本次费用上限',0,10000,'any']].map(([k,l,min,max,step])=>`<label>${I18n.text(l)}<input data-agent-limit="${k}" type="number" min="${min}" max="${max}" step="${step}" value="${agent[k]}" ${busy?'disabled':''}></label>`).join('')}<small>${I18n.text('单价与额度使用同一货币，0 表示关闭费用限制；按保守额度拦截下一轮，实际费用以服务商为准。')}</small></div>
 ${Kok.enabled('mcp')?`<div class="agent-tools-row"><button type="button" class="text-button" data-mcp-settings>${I18n.text('外部工具 MCP')}</button></div>`:''}</div></details>`;
 return `<section class="agent-page${agent.messages.length||busy?'':' is-empty'}${agent.historyOpen?' has-history':''}${AgentArtifacts.active?' has-artifacts':''}">${agentHistoryView()}<div class="agent-main">
 <div class="agent-toolbar"><button type="button" class="icon-button" data-agent-sessions aria-label="${I18n.text('会话')}" title="${I18n.text('会话')}" aria-controls="agent-history" aria-expanded="${!!agent.historyOpen}">${svg('panel')}</button><div class="agent-heading">${glyph('agent')}<strong data-agent-title data-user-content>${esc(agent.messages.find(m=>m.role==='user')?.text||agent.job?.goal||'Agent')}</strong></div><span class="agent-progress" id="agent-progress" role="status" data-busy="${busy}">${I18n.text(agent.phase||'就绪')}</span><button type="button" class="text-button agent-tree-button" data-agent-tree aria-label="${I18n.text('分支树')}" title="${I18n.text('分支树')}" ${busy?'disabled':''}>${I18n.text('分支树')}</button><div class="agent-toolbar-actions"><button type="button" class="icon-button" data-agent-new aria-label="${I18n.text('新任务')}" title="${I18n.text('新任务')}" ${busy?'disabled':''}>${svg('plus')}</button>${AgentPlatformUi.button()}<button type="button" class="icon-button" data-agent-artifacts aria-label="${I18n.text('任务成果')}" title="${I18n.text('任务成果')}" aria-expanded="${AgentArtifacts.active}" ${AgentArtifacts.count?'':'hidden'}>${svg('document')}</button><button type="button" class="icon-button" data-agent-more aria-label="${I18n.text('更多')}" title="${I18n.text('更多')}" aria-haspopup="menu" aria-expanded="false">${svg('more')}</button></div></div>
 ${agentContextUsageView()}
 <div class="agent-recovery">${agentRecoveryBar()}</div>${AgentOperations.view(agent.recovery&&(!agent.job||agent.job.id===agent.recovery.id)?agent.recovery:null)}<div class="agent-stream" id="agent-stream" aria-live="polite" aria-busy="${busy}">${agent.messages.length||busy?agentEntries():agentEmpty()}</div><div class="agent-review">${agentBatch()}</div>
 <button type="button" class="agent-latest text-button" data-agent-latest hidden>${I18n.text('回到最新')}${svg('arrow')}</button><form class="agent-composer" id="agent-form">${agentProjectBar()}<div class="agent-context">${agentContextChips()}</div>
 <textarea id="agent-input" rows="2" maxlength="2000" placeholder="${esc(I18n.text('告诉我，你想完成什么…'))}" aria-label="${esc(I18n.text('给 Agent 的要求'))}">${esc(agent.draft)}</textarea>
 ${agentComposerControls()}
 <div class="agent-compose-options"><button type="button" class="agent-chip agent-add" data-agent-pick aria-label="${I18n.text('添加资料')}" title="${I18n.text('添加资料')}" ${busy?'disabled':''}>${svg('paperclip')}<b>${I18n.text('添加资料')}</b></button>${advanced}<button type="button" class="agent-chip agent-scope-chip" data-agent-scope-summary title="${esc(I18n.text('资料范围'))}">${svg('folder')}<b>${esc(agentScopeLabel())}</b></button></div>
 ${agentQueuedTasks()}<div class="agent-footnote" ${usage||cacheUsage?'':'hidden'}><small id="agent-usage" data-user-content>${esc(usageLine)}</small></div></form></div>${AgentArtifacts.view()}</section>`;
}
function agentPaint({scroll=true}={}){
 agentSaveSoon();if(state.route!=='agent'||state.isPrivate)return;
 const previousFocus=document.activeElement;const root=$('#main'),stream=$('#agent-stream'),wasNear=!stream||stream.scrollHeight-stream.scrollTop-stream.clientHeight<90;
 const wasEmpty=root.querySelector('.agent-page')?.classList.contains('is-empty'),composerBox=root.querySelector('.agent-composer')?.getBoundingClientRect();
 const template=document.createElement('template');template.innerHTML=agentView();
 if(!stream||!agentAvailable()){root.replaceChildren(template.content);I18n.localize(root);agentSyncInput();return;}
 const next=template.content,page=root.querySelector('.agent-page');page.classList.toggle('is-empty',!agent.messages.length&&!agent.job);page.classList.toggle('has-history',!!agent.historyOpen);page.classList.toggle('has-artifacts',AgentArtifacts.active);
 for(const selector of ['.agent-history','.agent-toolbar','.agent-context-usage','.agent-recovery','.agent-mission','.agent-project','.agent-context','.agent-add','.agent-scope-chip','.agent-controls','#agent-advanced','.agent-limits','.agent-queued','.agent-footnote','.agent-review','.pi-artifacts']){
  const old=root.querySelector(selector),fresh=next.querySelector(selector);if(!old||!fresh)continue;
  if(selector==='.agent-limits'&&old.contains(document.activeElement))continue;
  if(selector==='#agent-advanced'&&old.contains(document.activeElement)&&document.activeElement?.matches?.('[data-agent-limit]'))continue;
  if(agentChromeEqual(old,fresh))continue;
  if(selector==='.agent-history'){
   old.hidden=fresh.hidden;const search=old.querySelector('input'),query=search.value,selection=[search.selectionStart,search.selectionEnd],focused=document.activeElement===search;
   old.querySelector('.agent-history-list').innerHTML=fresh.querySelector('.agent-history-list').innerHTML;
   const hint=old.querySelector('.agent-history-busy'),nextHint=fresh.querySelector('.agent-history-busy');if(hint)hint.remove();if(nextHint)old.querySelector('.agent-history-list').before(nextHint);
   old.querySelector('[data-agent-history-new]').disabled=!!agent.job;if(!focused)search.value=agent.historyQuery||'';else{search.value=query;search.setSelectionRange(...selection);}continue;
  }
  if(selector==='.agent-toolbar'){
   const status=old.querySelector('.agent-progress'),nextStatus=fresh.querySelector('.agent-progress');status.textContent=nextStatus.textContent;status.dataset.busy=nextStatus.dataset.busy;
   old.querySelector('[data-agent-title]').textContent=fresh.querySelector('[data-agent-title]').textContent;old.querySelector('[data-agent-sessions]').setAttribute('aria-expanded',String(!!agent.historyOpen));
   const buttons=[...fresh.querySelectorAll('button')];old.querySelectorAll('button').forEach((button,index)=>{button.disabled=buttons[index].disabled;button.hidden=buttons[index].hidden;for(const attr of ['aria-label','title'])if(buttons[index].hasAttribute(attr))button.setAttribute(attr,buttons[index].getAttribute(attr));if(button.hasAttribute('data-agent-artifacts'))button.setAttribute('aria-expanded',String(AgentArtifacts.active));});
   continue;
  }
  if(selector==='.agent-context-usage'){
   // Token figures change on every pull; keep an open summary from collapsing on the next paint.
   const expanded=!!old.querySelector('.agent-context-summary')?.open;old.replaceWith(fresh);
   const details=root.querySelector('.agent-context-usage .agent-context-summary');if(details&&expanded)details.open=true;
   continue;
  }
  if(selector==='.agent-mission')AgentOperations.preserve(old,fresh);
  else if(selector==='.pi-artifacts')AgentArtifacts.preserve(old,fresh);
  else old.replaceWith(fresh);
 }
 const incoming=next.querySelector('#agent-stream'),keys=new Set([...incoming.children].map(e=>e.dataset.entry).filter(Boolean));stream.setAttribute('aria-busy',String(!!agent.job));
 for(const node of [...stream.children])if(!node.dataset.entry||!keys.has(node.dataset.entry))node.remove();
 for(const node of [...incoming.children]){const existing=node.dataset.entry?stream.querySelector(`[data-entry="${node.dataset.entry}"]`):null;if(!existing)stream.append(node);else if(existing.innerHTML!==node.innerHTML){const opened=[...existing.querySelectorAll('details')].map(d=>d.open);existing.innerHTML=node.innerHTML;existing.querySelectorAll('details').forEach((d,i)=>{if(!d.matches('.agent-tool-group'))d.open=!!opened[i];});existing.classList.add('agent-entry-updated');}}
 const input=$('#agent-input');if(input&&document.activeElement!==input)input.value=agent.draft;
 I18n.localize(root);agentSyncInput();if(scroll&&wasNear)stream.scrollTop=stream.scrollHeight;agentUpdateScroll();agentRestoreFocus(previousFocus);
 if(wasEmpty!==page.classList.contains('is-empty')&&composerBox&&typeof CalmMotion!=='undefined'){
  const composer=root.querySelector('.agent-composer'),box=composer.getBoundingClientRect();
  CalmMotion.play(composer,[{transform:`translate(${composerBox.x-box.x}px,${composerBox.y-box.y}px)`},{transform:'none'}],{duration:280,channel:'layout'});
 }
}
// The native task engine owns the loop, tool execution, approvals and the encrypted journal. This page renders
// its snapshots and forwards decisions, adjustments and stops; it never schedules model turns itself.
function agentNativeUndo(m){return m.status==='done'&&(m.undoChanges||m.undoCreated||m.undoMemory)?'native':null;}
function agentApplySnapshot(snap){
 if(!snap?.id||state.isPrivate||snap.sessionId!==agent.sessionId)return;agent.recovery=snap;const id=snap.id,job=agent.job&&agent.job.id===id?agent.job:null;
 // A task started inside a creative project restores the project chip even after a reload.
 if(snap.projectId&&!agent.project?.id)agentHydrateProject(snap.projectId).then(found=>{if(found)agentPaint({scroll:false});}).catch(()=>{});
 const rows=snap.messages.map(m=>{const old=agent.messages.find(x=>x.id===m.id&&x.taskId===id);const row={...m,taskId:id};if(old){if(old.selected!==undefined)row.selected=old.selected;if(old.savedId)row.savedId=old.savedId;}if(!snap.running&&['pending','queued','running'].includes(row.status))row.status='paused';if(m.role==='tool')row.undo=agentNativeUndo(m);if(m.role==='error')row.retryable=!!(snap.retryable&&snap.resumable);return row;});
 const first=agent.messages.findIndex(m=>m.taskId===id),rest=agent.messages.filter(m=>m.taskId!==id);
 agent.messages=first<0?[...rest,...rows]:[...rest.slice(0,first),...rows,...rest.slice(first)];
 if(!job&&snap.status==='review_required')agentSetPhase('核对已失效');
 if(job){agent.usage=snap.usage;if(snap.phase)agentSetPhase(snap.phase);if(!snap.running){agentFinish(job,snap);agentRestoreUnconsumed(snap);}}
 // Stop is asynchronous: the task.stopped snapshot may arrive after stopAgent() already pulled a still-running
 // snapshot. Restore the unconsumed queue when the pending stop reaches its terminal snapshot.
 else if(agent.pendingRestoreFor&&snap.id===agent.pendingRestoreFor&&!snap.running){agent.pendingRestoreFor='';agentRestoreUnconsumed(snap);}
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
 agent.messages.push(agentTaskResult(job,snap));
 agentSetPhase(snap.errorKind==='output_limit'?'回答未完成':snap.status==='review_required'?'核对已失效':snap.status==='completed'?'已完成':snap.status==='partial'?'部分完成':snap.status==='needs_input'?'需要补充信息':snap.status==='limited'?'已达到本次请求上限。':snap.status==='cancelled'?'任务已取消':'任务已停止');
 renderNav();agentSave();
 // P1: artifacts/sessions are linked natively on the terminal event; refresh the project chip once.
 if(agent.project?.id)setTimeout(()=>agentRefreshProject().catch(()=>{}),400);
 agentRefreshArtifacts().catch(fail);
 if(snap.status==='completed'&&job.autoQueue!==false&&!job.cancelled&&agentAvailable()&&agent.queue.length){const next=agent.queue.shift();setTimeout(()=>runAgent(next).catch(fail),0);}
}
async function agentTrack(job,snap){
 if(job.cancelled){await native('agentTaskStop',{taskId:snap.id});return;}
 job.id=snap.id;agent.watch=snap.id;
 // Input typed before agentTaskStart returned was held locally and is forwarded now that the task id is known.
 const pending=(job.pendingInputs||[]).splice(0),dropped=[];
 for(let index=0;index<pending.length;index++){
  try{snap=await agentTaskInput(pending[index].mode,pending[index].text,snap.id);}
  catch(error){dropped.push(...pending.slice(index).map(item=>item.text));fail(error);break;}
 }
 if(pending.some(item=>item.mode==='follow'))agent.queueOpen=true;
 if(dropped.length)agentAppendDraft(dropped);
 // Compatibility fallback: the pre-M0-1 local adjustment hold list, if anything still filled it.
 for(const text of job.pendingAdjustments||[])snap=await native('agentTaskAdjust',{taskId:snap.id,text});
 agentApplySnapshot(snap);return agentPull(snap.id);
}
async function agentDecide(id,run,batch=false,grant=false){
 const m=agent.messages.find(x=>x.id===id);if(!m||m.status!=='pending')return;
 // A card from a task that is no longer running cannot be approved; resuming re-plans instead of replaying it.
 if(!m.taskId||agent.job?.id!==m.taskId){m.status='skipped';agentPaint({scroll:false});return;}
 if(run&&(!agentAvailable()||batch&&['mcp_call','delegate_task','run_code','start_command_job','open_browser','browser_action'].includes(m.tool)))return;
 if(run&&grant)await agentPresence(true);m.status=run?'running':'skipped';agentPaint({scroll:false});
 const session=agent.sessionId;
 try{agentApplySnapshot(await native('agentTaskDecide',{taskId:m.taskId,callId:id,approve:run,batch,grant}));if(run){await refresh();const approved=agent.messages.find(row=>row.id===id&&['create_document','replace_document_text','create_presentation','copy_presentation','edit_presentation'].includes(row.tool)&&row.status==='done');if(approved&&agent.sessionId===session)await AgentArtifacts.open(approved.result?.id);}}
 catch(error){fail(error);agentPull(m.taskId);}
}
async function agentUndo(id){
 const m=agent.messages.find(x=>x.id===id);if(!m?.undo||m.status!=='done'||agent.undoing)return;agent.undoing=true;
 try{if(m.undo==='native')agentApplySnapshot(await native('agentTaskUndo',{taskId:m.taskId,callId:id}));else{await m.undo();m.status='undone';m.undo=null;}await refresh();toast('已撤销');}
 catch(error){fail(error);}finally{agent.undoing=false;}agentPaint({scroll:false});
}
// M0-1: while a task runs the composer feeds the native input queues. Enter/发送 steers the current
// round; the clock button queues a follow-up that continues the same task when it would otherwise
// finish. The local agent.queue is only kept for saved legacy drafts now.
async function agentTaskInput(mode,text,taskId){
 try{return await native(mode==='follow'?'agentTaskFollowUp':'agentTaskSteer',{taskId,text});}
 catch(error){
  // Older natives have no queue actions; keep steering usable through the urgent adjustment path.
  if(mode==='follow')throw error;
  return native('agentTaskAdjust',{taskId,text});
 }
}
function agentAppendDraft(texts){
 const rows=(texts||[]).map(text=>String(text||'').trim()).filter(Boolean);if(!rows.length)return false;
 agent.draft=[agent.draft.trim(),...rows].filter(Boolean).join('\n');
 const input=$('#agent-input');if(input)input.value=agent.draft;
 return true;
}
// A stopped/limited/failed run hands unconsumed inputs back to the composer once per input id.
// Normal completion cannot restore input, so filtering by terminal status avoids stale text from an
// earlier limit/failure being replayed when the same task is continued later.
function agentRestoreUnconsumed(snap){
 if(!snap||snap.running||!['stopped','cancelled','limited','failed'].includes(String(snap.status||'')))return;
 const rows=(Array.isArray(snap.restoredInputs)?snap.restoredInputs:[]).filter(row=>row&&row.id&&typeof row.text==='string'&&row.text.trim()&&!agentRestoredSeen.has(String(row.id)));
 if(!rows.length)return;
 for(const row of rows)agentRestoredSeen.add(String(row.id));
 if(agentAppendDraft(rows.map(row=>row.text)))toast(I18n.text('因停止未执行：'+rows.length+' 条输入已放回输入框'));
}
// M1-1: `/skill:name rest` is an explicit native-skill command. It is resolved before the normal
// draft/steer paths, so the raw slash text is never sent as a message; validation uses agentSkillRead.
function agentSkillCommand(text){
 const match=/^\/skill:([a-z0-9-]{1,64})(?:\s+([\s\S]*))?$/.exec(String(text||'').trim());
 return match?{name:match[1],rest:(match[2]||'').trim()}:null;
}
async function agentRunSkillCommand(name,rest){
 if(agent.job||agent.inputFlight){toast(I18n.text('请先停止当前任务。'),true);return;}
 agent.inputFlight=true;agentSyncInput();
 try{
  let skill;
  try{skill=await native('agentSkillRead',{name});}
  catch(error){fail(error);return;}
  if(!skill||typeof skill!=='object'){fail(Error(I18n.text('找不到技能。')));return;}
  const skillName=/^[a-z0-9-]{1,64}$/.test(String(skill.name||''))?String(skill.name):name;
  await runAgent((rest||skillName).slice(0,2000),{name:skillName,description:String(skill.description||'').slice(0,200)});
 }catch(error){fail(error);}
 finally{agent.inputFlight=false;agentSyncInput();}
}
// M3-1: `/prompt:name args` inserts a plugin prompt template into the composer. It is validated
// against the native prompt list but never sent or stored as raw command text; the user edits/sends.
function agentPromptCommand(text){
 const match=/^\/prompt:([a-z0-9-]{1,64})(?:\s+([\s\S]*))?$/.exec(String(text||'').trim());
 return match?{name:match[1],args:(match[2]||'').trim()}:null;
}
function agentPromptExpanded(template,args){
 let text=String(template==null?'':template);
 if(/\$ARGUMENTS/i.test(text))text=text.replace(/\$ARGUMENTS/gi,args);
 else if(args)text=text.replace(/\s+$/,'')+'\n\n'+args;
 return text.trim();
}
async function agentRunPromptCommand(name,args){
 if(agent.job||agent.inputFlight){toast(I18n.text('请先停止当前任务。'),true);return;}
 // Cancel any pending draft save so the raw slash command is never persisted: only the expanded
 // template reaches the composer (and the next save).
 clearTimeout(agent.saveTimer);
 agent.inputFlight=true;agentSyncInput();
 try{
  let prompt;
  try{prompt=await native('agentPromptRead',{name});}
  catch(error){fail(error);return;}
  if(!prompt||typeof prompt!=='object'||typeof prompt.template!=='string'){fail(Error(I18n.text('找不到提示模板。')));return;}
  if(agent.job){toast(I18n.text('请先停止当前任务。'),true);return;}
  const text=agentPromptExpanded(prompt.template,args).slice(0,2000);
  if(!text){toast(I18n.text('提示模板为空。'),true);return;}
  agent.draft=text;
  const input=$('#agent-input');if(input){input.value=text;input.focus();input.setSelectionRange(input.value.length,input.value.length);}
  agentSaveSoon();
 }catch(error){fail(error);}
 finally{agent.inputFlight=false;agentSyncInput();}
}
async function agentAddInput(value,queued=false){
 const text=value.trim();if(!text||agent.inputFlight)return;
 if(text==='/tree'){if(agent.job){toast(I18n.text('请先停止当前任务。'),true);return;}return agentOpenTree();}
 const promptCommand=agentPromptCommand(text);if(promptCommand)return agentRunPromptCommand(promptCommand.name,promptCommand.args);
 const command=agentSkillCommand(text);if(command)return agentRunSkillCommand(command.name,command.rest);
 if(!agent.job)return agent.recovery?.respondable?agentRespond(text):runAgent(text);
 const job=agent.job,session=agent.sessionId,mode=queued?'follow':'steer';
 const clearSent=()=>{if(session!==agent.sessionId)return;const input=$('#agent-input');if(agent.draft===value){agent.draft='';if(input&&input.value===value)input.value='';}};
 // The native task id only exists after agentTaskStart resolves; agentTrack flushes this hold list.
 if(!job.id){const pending=job.pendingInputs||=[];if(pending.length>=5)throw Error(I18n.text('待处理输入最多 5 项。'));pending.push({text,mode});agent.queueOpen=true;clearSent();agentPaint({scroll:false});return;}
 agent.inputFlight=true;agentSyncInput();
 try{const snap=await agentTaskInput(mode,text,job.id);if(mode==='follow')agent.queueOpen=true;clearSent();agentApplySnapshot(snap);}
 finally{agent.inputFlight=false;agentPaint({scroll:false});}
}
let agentLocalFlight=false;
// Guards the asynchronous project hydration before a task start so a double submit cannot start
// two tasks while the project record is loading.
let agentProjectHydrating=false;
// Deterministic, read-only library questions are answered from local metadata without any model request.
// An older binary without the native action rejects the call; that silently falls through to the model path.
async function agentLocalPlan(goal,scope){
 if(agent.intent==='discuss')return null;
 agentLocalFlight=true;
 try{return await native('agentLocalPlan',{goal,scope});}catch{return null;}finally{agentLocalFlight=false;}
}
async function agentLocalAnswer(goal,local){
 const hex=id=>/^[a-f0-9]{32}$/.test(String(id||''));
 const stats=(Array.isArray(local?.stats)?local.stats:[]).slice(0,8).map(row=>({label:String(row?.label||'').slice(0,60),value:String(row?.value??'').slice(0,80)})).filter(row=>row.label||row.value);
 const items=(Array.isArray(local?.items)?local.items:[]).map(row=>{const id=String(row?.id||'');return hex(id)?{id,name:String(row?.name||'').slice(0,200)||agentItemName(id),kind:['video','photo','music','document','book'].includes(row?.kind)?row.kind:'document'}:null;}).filter(Boolean).slice(0,12);
 const followups=(Array.isArray(local?.followups)?local.followups:[]).filter(text=>typeof text==='string'&&text.trim()).map(text=>text.slice(0,120)).slice(0,4);
 agent.messages.push({id:uid(),role:'user',text:goal},{id:uid(),role:'result',kind:'local',local:true,goal,title:String(local?.title||'').slice(0,200),summary:String(local?.summary||'').slice(0,1000),stats,items,followups,total:Number(local?.total)||items.length,truncated:!!local?.truncated});
 // Keep a compact local answer in the model history so a follow-up can build on it without re-deriving.
 agent.history.push({role:'user',content:goal},{role:'assistant',content:I18n.text('任务结果')+': '+String(local?.summary||'').slice(0,400)});agent.history=agent.history.slice(-40);
 agent.recovery=null;agent.handoff=null;agent.usage=null;agent.draft='';const input=$('#agent-input');if(input)input.value='';
 agentSetPhase('已完成');renderNav();agentPaint();await agentSave();
}
// skillOverride comes only from an explicit `/skill:name` command: it bypasses the local planner and
// asks the model to load the discovered skill body through the read-only load_skill tool.
async function runAgent(goal,skillOverride=null){
 if(agent.job||agentLocalFlight||agentProjectHydrating||!agentAvailable()||!goal.trim())return;
 if(agent.storageBlocked)throw Error(I18n.text('Agent 会话无法读取，原记录已保留。'));
 let scope=agentScope();
 // P1: an active creative project supplies the default working set; an explicit selection wins.
 if(agent.project?.id){
  agentProjectHydrating=true;
  try{await agentHydrateProject();}catch{}
  finally{agentProjectHydrating=false;}
  scope=agentScope();
 }
 if(scope.mode==='selected'&&!scope.ids.length||scope.mode==='collection'&&!scope.collection){toast('请选择有效的 Agent 资料范围。',true);return;}
 // Claim the task synchronously: the local planner answers asynchronously, and the composer must not lose the goal or accept a duplicate send in that window.
 const job={id:'',goal:goal.trim(),raw:'',partial:'',request:0,cancelled:false};
 agent.job=job;agent.draft='';const sent=$('#agent-input');if(sent)sent.value='';
 agentSetPhase('生成方案');renderNav();agentPaint();
 // Built-in presets and imported custom skills keep their exact previous behavior. A discovered
 // (native) skill is only used when the selection is not shadowed by a local skill with that name;
 // an explicit native skill (picker or /skill command) always goes to the model, never the local planner.
 const skill=AgentModel.skill(agent.skill,goal,AgentSkills.custom()),preset=!!skill&&AgentModel.presets.includes(skill);
 const nativeSkill=skillOverride||(skill||!agent.skill||agent.skill==='auto'?null:agent.nativeSkills.find(s=>s.name===agent.skill)||null);
 // Local deterministic answers never reach the model and never consume the request budget.
 const local=skillOverride||nativeSkill?null:await agentLocalPlan(goal.trim(),scope);
 if(job.cancelled){if(agent.job===job)agent.job=null;agentSetPhase('任务已停止');agentPaint();renderNav();return;}
 if(local?.handled){if(agent.job===job)agent.job=null;await agentLocalAnswer(goal.trim(),local);return;}
 const scopeKey=agentScopeKey();if(agent.lastScope!==scopeKey){agent.history=[];agent.lastScope=scopeKey;}
 const instruction=skillOverride?agentNativeSkillInstruction(skillOverride.name):nativeSkill?agentNativeSkillInstruction(nativeSkill.name):skill&&!preset?('[Skill: '+skill.name+'] '+skill.instructions).slice(0,800):'';
 const options={approvalPolicy:agent.approvalPolicy,semanticCompression:agent.semanticCompression,contextTokens:agent.contextTokens,reserveTokens:agentCompactionSetting('reserveTokens'),keepRecentTokens:agentCompactionSetting('keepRecentTokens'),useMemory:agent.useMemory,allowDelegation:agent.allowDelegation,allowSandbox:agent.allowSandbox,...(agent.allowedTools?{allowedTools:agent.allowedTools}:{}),sessionId:agent.sessionId,sessionLeafId:[...agent.messages].reverse().find(m=>['user','agent'].includes(m.role)&&/^[a-f0-9]{32}$/.test(String(m.id||'')))?.id||'',goal:goal.trim(),intent:agent.intent,protocol:agent.protocol,handoff:agent.handoff,...(agent.project?.id?{projectId:agent.project.id}:{}),workflow:!skillOverride&&preset?skill.name:'',instruction,scope,history:AgentModel.history(agent.history,Math.min(6000,Math.floor(agent.budget/3))).slice(-10),budget:agent.budget,requestLimit:agent.requestLimit,costLimit:agent.costLimit,inputRate:agent.inputRate,outputRate:agent.outputRate};
 options.runtime='auto';
 agent.handoff=null;agent.recovery=null;agent.usage=null;
 if(nativeSkill)agent.messages.push({id:uid(),role:'agent',meta:true,text:I18n.text('使用技能')+' · '+nativeSkill.name+(nativeSkill.description?' — '+String(nativeSkill.description).slice(0,200):'')});
 else if(skill&&!preset)agent.messages.push({id:uid(),role:'agent',meta:true,text:I18n.text('使用技能')+' · '+I18n.text(skill.title||skill.name)+' — '+I18n.text(skill.description||'')});
 agentSetPhase('生成方案');renderNav();agentPaint();
 try{await agentSave();if(job.cancelled)return;await agentTrack(job,await native('agentTaskStart',options));}
 catch(error){if(agent.job===job)agent.job=null;if(!job.id)agent.messages.push({id:uid(),role:'user',text:goal});agent.messages.push({id:uid(),role:'error',text:error.message,goal});if(!agent.draft){agent.draft=goal;const input=$('#agent-input');if(input)input.value=goal;}if(job.pendingInputs?.length)agentAppendDraft(job.pendingInputs.splice(0).map(item=>item.text));agentSetPhase('任务已停止');agentPaint();renderNav();}
}
async function agentRespond(text){
 if(agent.job||!agentAvailable()||!agent.recovery?.respondable)return;
 const taskId=agent.recovery.id,job={id:taskId,goal:text,raw:'',partial:'',request:0,cancelled:false};agent.job=job;agent.draft='';const input=$('#agent-input');if(input)input.value='';agentSetPhase('生成方案');renderNav();agentPaint();
 try{await agentTrack(job,await native('agentTaskAdjust',{taskId,text}));}catch(error){if(agent.job===job)agent.job=null;if(!agent.draft){agent.draft=text;const input=$('#agent-input');if(input)input.value=text;}fail(error);agentPaint();renderNav();}
}
async function agentContinue(taskId){
 if(agent.job||!agentAvailable())return;const job={id:taskId,goal:agent.messages.find(m=>m.taskId===taskId&&m.role==='user')?.text||'',raw:'',partial:'',request:0,cancelled:false};
 agent.job=job;agentSetPhase('生成方案');renderNav();agentPaint();
 try{await agentTrack(job,await native('agentTaskResume',{taskId}));}catch(error){if(agent.job===job)agent.job=null;fail(error);agentPaint();renderNav();}
}
async function stopAgent(){
 const job=agent.job;if(!job)return;job.cancelled=true;if(job.id)agent.pendingRestoreFor=job.id;agentSetPhase('任务已停止');agentPaint();
 try{if(job.id)await native('agentTaskStop',{taskId:job.id});}catch{}
 if(agent.job===job)agent.job=null;
 // Input held before the task id existed never reached the native queue; return it to the composer.
 if(job.pendingInputs?.length)agentAppendDraft(job.pendingInputs.splice(0).map(item=>item.text));
 if(job.id&&Kok.enabled('agent')&&!state.isPrivate)await agentPull(job.id);
 if(job.id&&agent.recovery?.id===job.id&&!agent.recovery.running){agentRestoreUnconsumed(agent.recovery);agent.pendingRestoreFor='';}
 if(!job.id)agent.pendingRestoreFor='';
 agentPaint();renderNav();await agentSave();
 if(agent.project?.id)setTimeout(()=>agentRefreshProject().catch(()=>{}),300);
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
 const selected=new Set(agent.context);
 showSmall(`<div class="agent-pick-heading"><h2>${I18n.text('添加资料到 Agent')}</h2><button type="button" class="button secondary" id="agent-pick-import">${svg('plus')}${I18n.text('导入文件')}</button></div><input id="agent-pick-search" type="search" placeholder="${esc(I18n.text('搜索名称'))}" autocomplete="off"><div class="agent-pick-list" id="agent-pick-list"></div><small>${I18n.text('最多选择 24 份资料，搜索全部文件。')}</small><div class="document-form-actions"><button type="button" class="text-button" data-close-small>${I18n.text('取消')}</button><button type="button" class="button primary" id="agent-pick-done">${I18n.text('添加')}</button></div>`);
 const list=$('#agent-pick-list');const paint=()=>{const q=$('#agent-pick-search').value.trim().toLowerCase();list.innerHTML=state.items.slice().sort((a,b)=>String(b.added).localeCompare(String(a.added))).filter(i=>!q||i.name.toLowerCase().includes(q)).slice(0,200).map(i=>`<label><input type="checkbox" value="${esc(i.id)}" ${selected.has(i.id)?'checked':''}>${svg(i.kind)}<span data-user-content>${esc(i.name)}</span><small>${esc(i.ext)}</small></label>`).join('');};
 paint();I18n.localize($('#small-dialog'));$('#agent-pick-search').focus();$('#agent-pick-search').oninput=paint;
 list.onchange=event=>{const input=event.target;if(input.type!=='checkbox')return;if(input.checked&&selected.size>=24){input.checked=false;toast('最多选择 24 份资料，搜索全部文件。',true);return;}input.checked?selected.add(input.value):selected.delete(input.value);};
 $('#agent-pick-import').onclick=async event=>{
  const button=event.currentTarget,before=new Set(state.items.map(item=>item.id));button.disabled=true;
  try{await importItems('importFiles');if(!list.isConnected||!agentAvailable())return;const added=state.items.filter(item=>!before.has(item.id));for(const item of added)if(selected.size<24)selected.add(item.id);if(added.length){$('#agent-pick-search').value='';if(added.some(item=>!selected.has(item.id)))toast('最多选择 24 份资料，搜索全部文件。',true);}paint();}
  catch(error){fail(error);}finally{if(button.isConnected)button.disabled=false;}
 };
 $('#agent-pick-done').onclick=()=>{agent.scope=selected.size?'selected':'all';agent.context=[...selected];closeSmall();agentPaint({scroll:false});};
}

document.addEventListener('click',event=>{
 const t=event.target;
 if(t.closest('[data-agent-fork]')){agentForkMessage(t.closest('[data-agent-fork]').dataset.agentFork).catch(fail);return;}
 if(t.closest('[data-agent-compact]')){agentCompactRequest().catch(fail);return;}
 if(t.closest('[data-agent-skills-refresh]')){agentReloadSkills().catch(fail);return;}
 if(t.closest('[data-agent-plugins]')){agentOpenPlugins();return;}
 if(t.closest('[data-agent-tree]')){agentOpenTree().catch(fail);return;}
 if(t.closest('[data-agent-launch]')){event.preventDefault();openAgent({ids:typeof libraryDrag!=='undefined'?[...libraryDrag.selected]:[]});return;}
 if(t.closest('[data-agent-starter]')){const input=$('#agent-input');input.value=t.closest('[data-agent-starter]').dataset.agentStarter;agent.draft=input.value;agentSaveSoon();agentSyncInput();input.focus();return;}
 if(t.closest('[data-agent-more]')){agentMore(t.closest('[data-agent-more]'));return;}
 if(t.closest('[data-agent-scope-summary]')){agent.advancedOpen=true;agentPaint({scroll:false});const select=$('#agent-scope');if(select){select.focus({preventScroll:true});select.scrollIntoView({block:'nearest'});}return;}
 if(t.closest('#agent-advanced>summary')){agent.advancedOpen=!t.closest('#agent-advanced').open;return;}
 if(t.closest('[data-agent-result-continue]')){agentContinueResult(agent.messages.find(m=>m.id===t.closest('[data-agent-result-continue]').dataset.agentResultContinue));return;}
 if(t.closest('[data-agent-local-followup]')){agentFillLocalFollowup(t.closest('[data-agent-local-followup]'));return;}
 if(t.closest('[data-agent-unpin]')){if(agent.job)return;agent.context=agent.context.filter(id=>id!==t.closest('[data-agent-unpin]').dataset.agentUnpin);agentPaint({scroll:false});return;}
 if(t.closest('[data-agent-pick]')){if(!agent.job)agentPickItems();return;}
 if(t.closest('[data-agent-project-clear]')){agent.project=null;agentPaint({scroll:false});toast(I18n.text('已清除项目'));return;}
 if(t.closest('[data-agent-settings]')){$('#settings-button').click();return;}
 if(t.closest('[data-agent-run]')){const id=t.closest('[data-agent-run]').dataset.agentRun,grant=document.querySelector(`[data-agent-grant="${id}"]`)?.checked||false;return agentDecide(id,true,false,grant);}
 if(t.closest('[data-agent-skip]'))return agentDecide(t.closest('[data-agent-skip]').dataset.agentSkip,false);
 if(t.closest('[data-agent-undo]'))return agentUndo(t.closest('[data-agent-undo]').dataset.agentUndo);
 if(t.closest('[data-agent-stop]'))return stopAgent();
 if(t.closest('[data-agent-continue]')){agentContinue(t.closest('[data-agent-continue]').dataset.agentContinue).catch(fail);return;}
 if(t.closest('[data-agent-run-all],[data-agent-skip-all]')){const run=!!t.closest('[data-agent-run-all]');(async()=>{for(const m of agent.messages.filter(x=>x.status==='pending'&&(!run||x.tool!=='mcp_call')))await agentDecide(m.id,run,true);})().catch(fail);return;}
 if(t.closest('[data-agent-new],[data-agent-history-new]')){agentNew().catch(fail);return;}
 if(t.closest('[data-agent-close-history]')){agentToggleHistory(false);return;}
 const resume=t.closest('[data-agent-resume]');if(resume){if(!agent.job){if(resume.dataset.agentResume===agent.sessionId)agentToggleHistory(false);else agentNew(resume.dataset.agentResume).catch(fail);}return;}
 const removeSession=t.closest('[data-agent-delete-session]');if(removeSession){agentDeleteSession(removeSession.dataset.agentDeleteSession).catch(fail);return;}
 const review=t.closest('[data-agent-review-target]');if(review){const card=$(`[data-agent-card="${CSS.escape(review.dataset.agentReviewTarget)}"]`);if(card){card.scrollIntoView({block:'center',behavior:reducedMotion()?'instant':'smooth'});card.querySelector('[data-agent-run]')?.focus({preventScroll:true});}return;}
 const editQueue=t.closest('[data-agent-edit-queued]');if(editQueue){agentEditQueued(Number(editQueue.dataset.agentEditQueued));return;}
 const removeQueue=t.closest('[data-agent-remove-queued]');if(removeQueue){agent.queue.splice(Number(removeQueue.dataset.agentRemoveQueued),1);agentPaint({scroll:false});return;}
 if(t.closest('[data-agent-next]')){if(!agent.job&&agent.queue.length)runAgent(agent.queue.shift()).catch(fail);return;}
 if(t.closest('[data-agent-clear-queue]')){if(!agent.job){agent.queue=[];agentPaint({scroll:false});}return;}
 if(t.closest('[data-agent-limits]')){agent.options=!agent.options;agentPaint({scroll:false});return;}
 if(t.closest('[data-agent-queue]')){agentAddInput($('#agent-input').value,true).catch(fail);return;}
 if(t.closest('[data-agent-sessions]')){agentSessionPicker();return;}
},true);
document.addEventListener('click',event=>{const open=event.target.closest('[data-agent-open-item]');if(open){event.preventDefault();event.stopImmediatePropagation();openItem(open.dataset.agentOpenItem,{play:false}).catch(fail);}},true);
document.addEventListener('submit',event=>{if(event.target.id!=='agent-form')return;event.preventDefault();const input=$('#agent-input');try{Promise.resolve(agentAddInput(input.value)).catch(fail);}catch(e){fail(e);}},true);
document.addEventListener('keydown',event=>{
 if(event.isComposing||event.keyCode===229)return;
 if(event.key==='Escape'&&AgentArtifacts.active&&AgentArtifacts.overlay&&!$('#small-dialog')?.open&&!$('.glass-menu:popover-open')){event.preventDefault();AgentArtifacts.hide();$('[data-agent-artifacts]')?.focus({preventScroll:true});return;}
 if(event.key==='Escape'&&agent.historyOpen&&state.route==='agent'&&!$('#small-dialog')?.open&&!$('.glass-menu:popover-open')){event.preventDefault();agentToggleHistory(false);return;}
 // Pi pairs Enter with Alt+Enter; keep the same split here (steer on Enter, follow-up on Alt+Enter).
 if(event.target.id==='agent-input'&&event.altKey&&event.key==='Enter'&&agent.job){event.preventDefault();agentAddInput(event.target.value,true).catch(fail);return;}
 if(event.target.id==='agent-input')piEditorKeyDown(event,{busy:!!agent.job,send:()=>$('#agent-form').requestSubmit(),stop:()=>stopAgent().catch(fail)});
});
document.addEventListener('input',event=>{if(event.target.id==='agent-input'){agent.draft=event.target.value;agentSaveSoon();agentSyncInput();}if(event.target.id==='agent-history-search'){agent.historyQuery=event.target.value;$('.agent-history-list').innerHTML=agentHistoryRows();}});
document.addEventListener('toggle',event=>{if(!event.target?.isConnected)return;if(event.target.id==='agent-advanced')agent.advancedOpen=!!event.target.open;if(event.target.matches('.agent-queued'))agent.queueOpen=!!event.target.open;},true);
window.addEventListener('resize',()=>{if(state.route==='agent')agentSyncInput();});
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
function agentSessionPicker(){
 const open=!agent.historyOpen;agentToggleHistory(open);if(open)agentSave().catch(fail);
}
async function agentDeleteSession(id){
 if(agent.job||agent.inputFlight)return;
 const row=id===agent.sessionId?agentSnapshot():agent.sessions.find(row=>row.id===id);if(!row)return;
 showSmall(`<h2>${I18n.text('删除会话')}</h2><p data-user-content>${esc(row.title)}</p><p>${I18n.text('会话记录将被删除，资料和作品会保留。')}</p><div class="document-form-actions"><button type="button" class="text-button" data-close-small>${I18n.text('取消')}</button><button type="button" class="button secondary" id="agent-delete-session-confirm">${I18n.text('删除')}</button></div>`);I18n.localize($('#small-dialog'));
 $('#agent-delete-session-confirm').onclick=async event=>{const button=event.currentTarget;if(agent.job||agent.inputFlight||agentDeleting)return;agentDeleting=true;button.disabled=true;clearTimeout(agent.saveTimer);try{
  if(agentSaveFlight)await agentSaveFlight;const rows=agent.sessions.filter(row=>row.id!==id);await native('saveAgentSessions',{text:JSON.stringify(rows)});agent.sessions=rows;agentPersistedContent=null;
  if(id===agent.sessionId){AgentArtifacts.reset();agent.sessionId=uid();Object.assign(agent,{messages:[],history:[],draft:'',queue:[],context:[],scope:'all',usage:null,lastScope:'',watch:'',recovery:null,handoff:null,phase:''});}
  await closeSmall();agentPaint({scroll:false});$('#agent-history-search')?.focus({preventScroll:true});
 }catch(error){fail(error);}finally{agentDeleting=false;button.disabled=false;agentSaveSoon();}};
}
function agentEditQueued(index){
 const original=agent.queue[index];if(typeof original!=='string')return;
 showSmall(`<h2>${I18n.text('编辑待办')}</h2><textarea id="agent-edit-queued-input" maxlength="2000" rows="5" aria-label="${I18n.text('编辑待办')}">${esc(original)}</textarea><div class="document-form-actions"><button type="button" class="text-button" data-close-small>${I18n.text('取消')}</button><button type="button" class="button primary" id="agent-save-queued">${I18n.text('保存')}</button></div>`);I18n.localize($('#small-dialog'));$('#agent-edit-queued-input').focus();
 $('#agent-save-queued').onclick=()=>{const value=$('#agent-edit-queued-input').value.trim();if(!value)return;if(agent.queue[index]!==original){toast('这项待办已开始执行或发生变化。',true);return;}agent.queue[index]=value;closeSmall();agentPaint({scroll:false});};
}

// Settings: choose whether Smart mode opens on Agent or on the library.
function agentSetupSettings(){
 const ai=$('[data-ai-settings]')?.closest('.setting-row');
 if(ai&&!$('#start-page-select'))ai.insertAdjacentHTML('beforebegin',`<div class="setting-row smart-settings-row"><div>${I18n.text('启动页')}<small>${I18n.text('智能模式下打开 kok 时显示')}</small></div><select id="start-page-select" aria-label="${esc(I18n.text('启动页'))}"><option value="agent">Agent</option><option value="overview">${I18n.text('资料库')}</option></select></div>`);
 const select=$('#start-page-select');if(select)select.value=state.settings.startPage==='overview'?'overview':'agent';
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
 const session=agent.sessionId,epoch=agentArtifactEpoch,watch=agent.watch;
 await Promise.all([agentRefreshArtifacts(),agent.project?.id?agentRefreshProject():Promise.resolve()]);
 if(session!==agent.sessionId||epoch!==agentArtifactEpoch||!agentAvailable())return;
 const tasks=await native('agentTaskList');
 if(session!==agent.sessionId||epoch!==agentArtifactEpoch||!agentAvailable())return;
 // An explicitly selected branch has no watched task. Never replace it with the last finished task.
 const task=tasks.filter(t=>t.sessionId===session&&(watch?t.id===watch:t.status==='running')).sort((a,b)=>b.updated.localeCompare(a.updated))[0];
 if(!task)return;agent.watch=task.id;const snap=await native('agentTaskRead',{taskId:task.id});
 if(session!==agent.sessionId||epoch!==agentArtifactEpoch||!agentAvailable())return;
 if(snap.running)agent.job={id:snap.id,goal:snap.options.goal,raw:'',partial:'',request:0,cancelled:false,autoQueue:false};
 agentApplySnapshot(snap);
 // A restored project session only stores id/name/goal; reload the full record (members/artifacts).
 if(agent.project?.id)agentRefreshProject().catch(()=>{});
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
 if(!state.isPrivate){for(const tool of Kok.catalog().tools){if(!tool.write&&!AGENT_READ.includes(tool.name))AGENT_READ.push(tool.name);AGENT_TOOLS[tool.name]||={icon:'info',label:tool.title||tool.name};}if(agent.loaded)await agentAttach();else await agentLoad();if(!agent.nativeSkillsLoaded)await agentLoadNativeSkills();if(!agent.pluginsLoaded)await agentLoadNativePlugins();}
 if(startup&&!location.search.includes('preview=')&&agentAvailable()&&state.route==='overview'&&(state.settings.startPage||'agent')==='agent'){state.route='agent';render();}
}
export const AgentUi=Object.freeze({state:agent,presence:agentPresence,available:agentAvailable,open:openAgent,openProject,submit:agentAddInput,render:agentPaint,stop:stopAgent,save:agentSave,newSession:agentNew,scope:agentScope,updateScroll:agentUpdateScroll,tasks:agentTaskRecords,settings:agentSetupSettings,start:startAgentUi});
// The legacy shell still needs one named adapter. Implementation helpers remain
// module-private; there are no global agent/agentPaint/runAgent declarations.
globalThis.AgentUi=AgentUi;

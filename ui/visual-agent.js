'use strict';
// Context menus preserve the captured editor range. Model work starts only from
// the explicit Generate action; a late response never changes its target.
const visualAgent={popup:null,target:null,serial:0,job:null,reply:null,task:'polish',instruction:'',error:'',phase:'ready',point:{x:0,y:0},applying:false,appliedSnapshot:null,stopping:false,frame:0};
function visualPopup(){
 if(visualAgent.popup)return visualAgent.popup;
 const root=document.createElement('section');root.id='visual-agent';root.setAttribute('popover','manual');root.setAttribute('role','dialog');root.setAttribute('aria-modal','false');root.setAttribute('aria-label','AI 修改');document.body.append(root);
 visualAgent.popup=root;new ResizeObserver(()=>queueVisualPlacement()).observe(root);return root;
}
function closeVisualAgent({restoreFocus=false,animate=false}={}){
 const a=visualAgent,job=a.job,target=a.target,serial=++a.serial,root=a.popup;
 a.job=null;a.reply=null;a.target=null;a.phase='ready';a.instruction='';a.error='';a.appliedSnapshot=null;a.stopping=false;
 if(job)native('cancelFileChat',{jobId:job}).catch(()=>{});
 const hide=()=>{if(a.serial!==serial||!root)return;if(root.matches(':popover-open'))root.hidePopover();root.innerHTML='';root.inert=false;root.style.pointerEvents='';};
 if(animate&&root?.matches(':popover-open')){root.inert=true;root.style.pointerEvents='none';CalmMotion.exit(root).finally(hide);}else hide();
 if(restoreFocus)target?.restore?.();
}
function replaceVisualText(input,start,end,text){
 input.focus({preventScroll:true});input.setSelectionRange(start,end);
 if(!document.execCommand('insertText',false,text)){input.setRangeText(text,start,end,'select');input.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertReplacementText'}));}
}
function visualTextTarget(input,start,end,label){
 const item=(input.id.startsWith('ppt-')?PresentationUi.state.session?.item:null)||creative.session?.item||state.viewer;if(!item)return null;
 const original=input.value,paragraph=start===end;
 if(paragraph){start=start>0?original.lastIndexOf('\n',start-1)+1:0;const next=original.indexOf('\n',end);end=next<0?original.length:next;}
 const text=original.slice(start,end);if(!text.trim())return null;
 return{fileId:item.id,label,scope:paragraph?'当前段落':'选中文字',source:input,text,nearby:original.slice(Math.max(0,start-500),start)+'\n'+original.slice(end,end+500),current:()=>input.isConnected?input.value:null,valid:()=>input.isConnected&&input.value===original,
  restore(){if(input.isConnected){input.focus({preventScroll:true});if(input.value===original)input.setSelectionRange(start,end);}},
  apply(reply){replaceVisualText(input,start,end,reply.text);},undo(){replaceVisualText(input,start,start+visualAgent.reply.text.length,text);}};
}
function visualRichTarget(root,range){
 const item=creative.session?.item;if(!item||!range||!root.contains(range.commonAncestorContainer))return null;
 const capture=range.cloneRange(),text=capture.toString(),original=root.innerHTML;if(!text.trim())return null;
 const restore=()=>{if(!root.isConnected)return;root.focus({preventScroll:true});const selection=getSelection();selection.removeAllRanges();selection.addRange(capture);};
 return{fileId:item.id,label:I18n.text('文档选区'),scope:'选中文字',source:root,text,nearby:root.innerText.slice(0,1000),current:()=>root.isConnected?root.innerHTML:null,valid:()=>root.isConnected&&root.innerHTML===original&&capture.toString()===text,restore,
  apply(reply){restore();if(!document.execCommand('insertText',false,reply.text))throw Error(I18n.text('编辑位置已变化，请重新选择。'));},
  undo(){root.focus({preventScroll:true});document.execCommand('undo');if(root.innerHTML!==original){root.innerHTML=original;root.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'historyUndo'}));}}};
}
function visualPresentationTarget(element,wholePage=false){
 const s=PresentationUi.state.session;if(!s)return null;
 const page=s.page,slide=s.deck.slides[page],id=element?.closest('[data-ppt-shape]')?.dataset.pptShape;
 const rows=wholePage||!id?slide.shapes.filter(shape=>shape.kind!=='image'&&shape.text?.trim()):slide.shapes.filter(shape=>shape.id===id&&shape.kind!=='image'&&shape.text?.trim());if(!rows.length)return null;
 const blocks=rows.map(shape=>({id:shape.id,text:shape.text})),ids=new Set(blocks.map(row=>row.id));
 let textStart=0,textEnd=rows[0].text.length,partial=false;const selection=getSelection(),textRoot=element.closest('[data-ppt-shape]')?.querySelector('span');
 if(!wholePage&&rows.length===1&&textRoot&&selection?.rangeCount){const range=selection.getRangeAt(0);if(textRoot.contains(range.commonAncestorContainer)&&range.toString().trim()){const before=range.cloneRange();before.selectNodeContents(textRoot);before.setEnd(range.startContainer,range.startOffset);textStart=before.toString().length;textEnd=textStart+range.toString().length;partial=textEnd<=rows[0].text.length;}}
 const current=()=>PresentationUi.state.session===s&&s.page===page&&s.deck.slides[page]?.id===slide.id?JSON.stringify({revision:s.revision,blocks:s.deck.slides[page].shapes.filter(shape=>ids.has(shape.id)).map(shape=>({id:shape.id,text:shape.text}))}):null,snapshot=current();
 const source=element.closest('[data-ppt-shape]')||$('#ppt-slide');
 const target={fileId:s.item.id,label:I18n.text('幻灯片')+' '+(page+1),scope:wholePage?'本页文字':partial?'选中文字':'文字框',source,text:partial?rows[0].text.slice(textStart,textEnd):blocks.map(row=>row.text).join('\n'),nearby:partial?(rows[0].text.slice(Math.max(0,textStart-500),textStart)+'\n'+rows[0].text.slice(textEnd,textEnd+500)):rows.length===1?slide.shapes.filter(shape=>!ids.has(shape.id)&&shape.kind!=='image').map(shape=>shape.text).join('\n').slice(0,1200):'',current,valid:()=>current()===snapshot,
  restore(){if(PresentationUi.state.session!==s||s.page!==page)return;const live=id?document.querySelector('#ppt-slide [data-ppt-shape="'+CSS.escape(id)+'"]'):$('#ppt-slide');if(live){live.tabIndex=-1;live.focus({preventScroll:true});}},
  apply(reply){PresentationUi.recordHistory(s);const live=s.deck.slides[page].shapes.filter(shape=>ids.has(shape.id));if(rows.length>1){if(!reply.edits||reply.edits.length!==live.length||new Set(reply.edits.map(edit=>edit.id)).size!==live.length||reply.edits.some(edit=>!ids.has(edit.id)))throw Error(I18n.text('AI 修改结果无效，请重试。'));for(const edit of reply.edits)live.find(shape=>shape.id===edit.id).text=edit.text;}else live[0].text=partial?live[0].text.slice(0,textStart)+reply.text+live[0].text.slice(textEnd):reply.text;s.editing=true;PresentationUi.changed();PresentationUi.recordHistory(s);PresentationUi.renderChrome();},undo(){PresentationUi.undo(-1);}};
 if(rows.length>1)target.blocks=blocks;return target;
}
function captureVisualTarget(event){
 const element=event.target;
 if(PresentationUi.state.session&&(element.closest('#ppt-slide')||element.id==='ppt-shape-text'||element.id==='ppt-notes')){if(element.id==='ppt-notes'||element.id==='ppt-shape-text')return visualTextTarget(element,element.selectionStart,element.selectionEnd,I18n.text(element.id==='ppt-notes'?'演讲备注':'文字框'));return visualPresentationTarget(element,!!element.closest('#ppt-slide')&&!element.closest('[data-ppt-shape]'));}
 if(element.id==='creative-text')return visualTextTarget(element,element.selectionStart,element.selectionEnd,I18n.text('文档'));
 const rich=element.closest('#creative-rich-text');if(rich){const selection=getSelection();return visualRichTarget(rich,selection?.rangeCount?selection.getRangeAt(0):null);}
 const preview=element.closest('#creative-text-preview');if(preview){const selection=getSelection(),selected=selection?.toString(),source=$('#creative-text');if(selected&&source&&selection.rangeCount&&preview.contains(selection.getRangeAt(0).commonAncestorContainer)){const start=source.value.indexOf(selected);if(start>=0&&source.value.indexOf(selected,start+1)<0){const target=visualTextTarget(source,start,start+selected.length,I18n.text('文档选区')),range=selection.getRangeAt(0).cloneRange();target.source=preview;target.restore=()=>{if(!preview.isConnected)return;preview.tabIndex=-1;preview.focus({preventScroll:true});const s=getSelection();s.removeAllRanges();s.addRange(range);};return target;}}}
 return null;
}
function queueVisualPlacement(){cancelAnimationFrame(visualAgent.frame);visualAgent.frame=requestAnimationFrame(placeVisualAgent);}
function placeVisualAgent(){
 const a=visualAgent,root=a.popup;if(!root?.matches(':popover-open'))return;
 const r=root.getBoundingClientRect(),margin=10;root.style.left=Math.max(margin,Math.min(innerWidth-r.width-margin,a.point.x+6))+'px';root.style.top=Math.max(margin,Math.min(innerHeight-r.height-margin,a.point.y+6))+'px';
}
function validVisualRequest(){const a=visualAgent,skill=AgentSkills.all().find(s=>s.name===a.task);return!!a.target&&a.target.text.length<=2000&&(!a.target.blocks||a.target.blocks.length<=40)&&!!skill&&(skill.task!=='custom'||!!a.instruction.trim());}
function validVisualReply(){const a=visualAgent;return!!a.reply&&(a.reply.edits?Array.isArray(a.reply.edits)&&a.reply.edits.every(edit=>edit.text?.trim()):!!a.reply.text?.trim()&&a.reply.text.length<=8000);}
function updateVisualActions(){const a=visualAgent,run=$('#visual-agent [data-visual-run]'),apply=$('#visual-agent [data-visual-apply]');if(run)run.disabled=a.stopping||(!a.job&&!validVisualRequest());if(apply)apply.disabled=!!a.job||!validVisualReply();}
function renderVisualAgent({focus}={}){
 const a=visualAgent,root=visualPopup(),target=a.target;if(!target)return;
 const previous=root.contains(document.activeElement)?document.activeElement:null,focusId=previous?.id,range=previous&&typeof previous.selectionStart==='number'?[previous.selectionStart,previous.selectionEnd]:null;
 const reply=a.reply,review=!!reply,applied=a.phase==='applied',skills=AgentSkills.all();
 root.setAttribute('aria-busy',String(!!a.job));
 root.innerHTML=`<header><span>${svg('spark')}<b>AI 修改</b></span><button class="icon-button" data-visual-close aria-label="关闭">${svg('close')}</button></header><div class="visual-target"><span data-user-content>${esc(target.label)} · ${esc(I18n.text(target.scope||'选中文字'))}</span><small data-user-content>${target.text.length} / 2000</small></div><details class="visual-original" open><summary>原文</summary><div data-user-content>${esc(target.text)}</div></details><div class="visual-controls"><label for="visual-skill">操作</label><select id="visual-skill" ${a.job||applied?'disabled':''}>${skills.map(s=>`<option value="${esc(s.name)}" ${s.name===a.task?'selected':''}>${esc(s.title===s.name?s.title:I18n.text(s.title))}</option>`).join('')}</select></div><textarea id="visual-instruction" maxlength="800" rows="2" placeholder="补充要求（可选）" ${a.job||applied?'disabled':''}></textarea><div id="visual-response">${a.job?'<div class="visual-pending" role="status"><span class="spinner"></span><span>正在修改选中内容…</span></div>':review?`<section class="visual-proposal"><label for="visual-result">${applied?'已应用':'修改预览'}</label>${reply.edits?reply.edits.map((edit,n)=>`<div class="visual-block-result"><small data-user-content>${n+1}</small><p data-user-content>${esc(edit.text)}</p></div>`).join(''):`<textarea id="visual-result" rows="5" maxlength="8000" ${applied?'readonly':''}>${esc(reply.text)}</textarea>`}</section>`:''}</div><p class="visual-error" role="status">${esc(a.error)}</p><footer><small>${a.job?'仅发送选区与附近文字':applied?'修改可撤销':'原文保留，确认后替换'}</small><div>${applied?'<button class="text-button" data-visual-undo>撤销本次修改</button><button class="button secondary" data-visual-close>完成</button>':`<button class="text-button" data-visual-close>${review?'放弃':'取消'}</button><button class="${review||a.job?'text-button':'button secondary'}" data-visual-run>${a.stopping?'正在停止…':a.job?'停止':review?'重新生成':'生成修改'}</button>${review?'<button class="button secondary" data-visual-apply>替换选区</button>':''}`}</div></footer>`;
 $('#visual-instruction').value=a.instruction;$('#visual-instruction').placeholder=I18n.text(AgentSkills.all().find(s=>s.name===a.task)?.task==='custom'?'输入修改要求':'补充要求（可选）');I18n.localize(root);updateVisualActions();placeVisualAgent();
 const destination=focus?root.querySelector(focus):focusId?root.querySelector('#'+focusId):null;if(destination&&!destination.disabled){destination.focus({preventScroll:true});if(range&&typeof destination.setSelectionRange==='function')destination.setSelectionRange(...range);}
}
function openVisualAgent(target,point,owner=document.body){
 closeVisualAgent();GlassMenu.close(false,true);const root=visualPopup();if(root.parentElement!==owner)owner.append(root);
 const a=visualAgent;a.target=target;a.point=point;a.task='polish';a.instruction='';a.error=target.text.length>2000?I18n.text('请选择不超过 2000 字符的文字后修改。'):'';root.inert=false;root.style.pointerEvents='';renderVisualAgent();if(!root.matches(':popover-open'))root.showPopover();placeVisualAgent();CalmMotion.play(root,[{opacity:0,transform:'translateY(3px)'},{opacity:1,transform:'none'}],{duration:140});$('#visual-instruction').focus({preventScroll:true});
}
async function runVisualAgent(){
 const a=visualAgent;if(!a.target||!isSmartMode())return;
 if(a.job){if(a.stopping)return;const job=a.job,serial=++a.serial;a.stopping=true;renderVisualAgent({focus:'[data-visual-run]'});try{await native('cancelFileChat',{jobId:job});}finally{if(a.serial===serial){a.job=null;a.stopping=false;renderVisualAgent({focus:'[data-visual-run]'});}}return;}
 if(!a.target.valid()){a.error=I18n.text('编辑位置已变化，请重新选择。');renderVisualAgent();return;}if(!validVisualRequest())return;
 const skill=AgentSkills.all().find(s=>s.name===a.task),serial=++a.serial,job=uid(),target=a.target;
 a.job=job;a.reply=null;a.error='';a.phase='ready';renderVisualAgent({focus:'[data-visual-run]'});
 try{const reply=await native('runVisualEdit',{id:target.fileId,jobId:job,task:skill.task,instruction:a.instruction,text:target.text,nearby:target.nearby||'',...(target.blocks?{blocks:target.blocks}:{})});if(a.serial!==serial||a.target!==target)return;if(!target.valid())throw Error(I18n.text('编辑位置已变化，请重新选择。'));a.reply=reply;a.phase='review';}
 catch(error){if(a.serial===serial)a.error=error.message;}
 finally{if(a.serial===serial){a.job=null;renderVisualAgent({focus:a.reply?.edits?'[data-visual-apply]':a.reply?'#visual-result':'[data-visual-run]'});}}
}
function applyVisualAgent(undo=false){
 const a=visualAgent,target=a.target;if(!target)return;
 try{if(undo){if(target.current()!==a.appliedSnapshot)throw Error(I18n.text('编辑位置已变化，请重新选择。'));a.applying=true;target.undo();closeVisualAgent({restoreFocus:true});toast('已撤销');return;}
  if(a.job||a.phase==='applied'||!validVisualReply())return;if(!target.valid())throw Error(I18n.text('编辑位置已变化，请重新选择。'));a.applying=true;target.apply(a.reply);a.appliedSnapshot=target.current();a.phase='applied';renderVisualAgent({focus:'[data-visual-close]'});CalmMotion.feedback($('#visual-agent [data-visual-undo]'));
 }catch(error){a.error=error.message;renderVisualAgent();}finally{a.applying=false;}
}
function invalidateVisualAgent(){if(visualAgent.applying)return;GlassMenu.close(false,true);if(visualAgent.target)closeVisualAgent();}
function visualEditingContext(element){
 const input=element.closest('textarea,input');
 if(input&&typeof input.selectionStart==='number'){
  const value=input.value,start=input.selectionStart,end=input.selectionEnd;
  return{selected:value.slice(start,end),readonly:input.readOnly||input.disabled,valid:()=>input.isConnected&&input.value===value,restore(){if(input.isConnected){input.focus({preventScroll:true});if(input.value===value)input.setSelectionRange(start,end);}},replace(text){replaceVisualText(input,start,end,text);},selectAll(){input.focus({preventScroll:true});input.select();}};
 }
 const rich=element.closest('#creative-rich-text');if(!rich)return null;
 const selection=getSelection(),range=selection?.rangeCount?selection.getRangeAt(0).cloneRange():null,html=rich.innerHTML;if(!range||!rich.contains(range.commonAncestorContainer))return null;
 const restore=()=>{rich.focus({preventScroll:true});const s=getSelection();s.removeAllRanges();s.addRange(range);};
 return{selected:range.toString(),readonly:false,valid:()=>rich.isConnected&&rich.innerHTML===html,restore,replace(text){restore();document.execCommand('insertText',false,text);},selectAll(){rich.focus({preventScroll:true});const r=document.createRange();r.selectNodeContents(rich);const s=getSelection();s.removeAllRanges();s.addRange(r);}};
}
function showVisualContextMenu(event,target){
 const element=event.target,editor=visualEditingContext(element),owner=element.closest('dialog')||document.body,r=element.getBoundingClientRect(),keyboard=!Number.isFinite(event.clientX)||!Number.isFinite(event.clientY)||(event.clientX===0&&event.clientY===0),point=keyboard?{x:r.left+18,y:r.top+Math.min(35,r.height)}:{x:event.clientX,y:event.clientY};
 const restore=()=>editor?.valid()?editor.restore():target?.restore?.();
 const copy=editor?editor.selected:target?.text||'',items=[];closeVisualAgent();
 const action=fn=>()=>{Promise.resolve().then(fn).catch(fail);};
 items.push({key:'copy',label:'复制',shortcut:'Ctrl+C',disabled:!copy,action:action(async()=>{await navigator.clipboard.writeText(copy);})});
 if(editor){items.push({key:'cut',label:'剪切',shortcut:'Ctrl+X',disabled:!editor.selected||editor.readonly,action:action(async()=>{await navigator.clipboard.writeText(editor.selected);if(!editor.valid())throw Error(I18n.text('编辑位置已变化，请重新选择。'));editor.replace('');})});items.push({key:'paste',label:'粘贴',shortcut:'Ctrl+V',disabled:editor.readonly,action:action(async()=>{const text=await navigator.clipboard.readText();if(!editor.valid())throw Error(I18n.text('编辑位置已变化，请重新选择。'));editor.replace(text);})});items.push({key:'select-all',label:'全选',shortcut:'Ctrl+A',action:()=>editor.selectAll()});}
 if(target&&isSmartMode())items.push({key:'ai-edit',label:'AI 修改…',separatorBefore:true,action:()=>{if(!target.valid()){toast('编辑位置已变化，请重新选择。');return;}openVisualAgent(target,point,owner);}});
 GlassMenu.openAt(element,point,items,{className:'visual-context-menu',label:'编辑菜单',heading:target?I18n.text(target.scope||'选中文字'):'',focus:true,restoreFocus:restore});
}
async function showAgentSkills(){closeVisualAgent();if(state.isPrivate)return;showSmall(`<h2>指令技能</h2><p class="dialog-description">按需载入选中的指令，只使用当前编辑范围。</p><div class="agent-skill-list">${AgentSkills.all().map(s=>`<article><div><b data-user-content>${esc(I18n.text(s.title))}</b><p data-user-content>${esc(AgentSkills.builtins.includes(s)?I18n.text(s.description):s.description)}</p></div>${s.instructions?`<button class="icon-button" data-skill-copy="${esc(s.name)}" title="复制 SKILL.md" aria-label="复制 SKILL.md">${svg('copy')}</button><button class="icon-button" data-skill-remove="${esc(s.name)}" title="删除技能" aria-label="删除技能">${svg('trash')}</button>`:'<small>内置</small>'}</article>`).join('')}</div><label class="button secondary agent-skill-import">${svg('plus')}导入 SKILL.md<input id="agent-skill-file" type="file" accept=".md,.json" hidden></label><p class="dialog-footnote">仅导入文字指令，不加载脚本或系统工具。</p>`);$('#agent-skill-file').onchange=async event=>{try{const file=event.target.files[0];if(!file)return;if(file.size>16000)throw Error(I18n.text('技能文件过大。'));AgentSkills.add(AgentSkills.parse(await file.text()));await showAgentSkills();toast('技能已导入');}catch(error){fail(error);}};I18n.localize($('#small-dialog'));}

document.addEventListener('contextmenu',event=>{
 const target=captureVisualTarget(event),field=event.target.closest('#creative-text,#creative-rich-text,#creative-text-preview,#ppt-shape-text,#ppt-notes,#visual-instruction,#visual-result');
 if(!target&&!field)return;event.preventDefault();event.stopImmediatePropagation();showVisualContextMenu(event,target);
},true);
document.addEventListener('pointerdown',event=>{const a=visualAgent;if(!a.popup?.matches(':popover-open')||a.popup.contains(event.target)||event.target.closest('.glass-menu'))return;closeVisualAgent({animate:true});},true);
document.addEventListener('click',event=>{const button=event.target.closest('button');if(!button)return;
 if(button.hasAttribute('data-visual-close'))closeVisualAgent({restoreFocus:true,animate:true});if(button.hasAttribute('data-visual-run'))runVisualAgent().catch(fail);if(button.hasAttribute('data-visual-apply'))applyVisualAgent();if(button.hasAttribute('data-visual-undo'))applyVisualAgent(true);
 if(button.hasAttribute('data-agent-skills'))showAgentSkills().catch(fail);if(button.dataset.skillRemove){AgentSkills.remove(button.dataset.skillRemove);showAgentSkills().catch(fail);}if(button.dataset.skillCopy){const skill=AgentSkills.custom().find(s=>s.name===button.dataset.skillCopy);if(skill)navigator.clipboard.writeText(AgentSkills.markdown(skill)).then(()=>toast('已复制')).catch(fail);}
});
document.addEventListener('change',event=>{if(event.target.id==='visual-skill'){const a=visualAgent;a.task=event.target.value;a.instruction=AgentSkills.all().find(s=>s.name===a.task)?.instructions||'';a.reply=null;a.phase='ready';a.error='';renderVisualAgent({focus:'#visual-skill'});}});
document.addEventListener('input',event=>{
 const a=visualAgent;if(event.target.id==='visual-instruction'){a.instruction=event.target.value;if(a.reply){a.reply=null;a.phase='ready';a.error='';renderVisualAgent();}else updateVisualActions();return;}
 if(event.target.id==='visual-result'){if(a.reply)a.reply.text=event.target.value;updateVisualActions();return;}
 if(event.target.closest('#creative-rich-text,#creative-text,#ppt-shape-text,#ppt-notes'))invalidateVisualAgent();
});
document.addEventListener('kok-presentation-changed',invalidateVisualAgent);document.addEventListener('close',event=>{if(['viewer','creative-dialog'].includes(event.target.id))invalidateVisualAgent();},true);
document.addEventListener('keydown',event=>{if(!visualAgent.popup?.matches(':popover-open')||GlassMenu.isOpen)return;
 if(event.key==='Escape'){event.preventDefault();event.stopImmediatePropagation();closeVisualAgent({restoreFocus:true,animate:true});}
 else if(event.key==='Enter'&&(event.ctrlKey||event.metaKey)&&event.target.closest('#visual-agent')){event.preventDefault();event.stopImmediatePropagation();if(!visualAgent.job&&!visualAgent.reply)runVisualAgent().catch(fail);}
},true);
window.addEventListener('resize',queueVisualPlacement,{passive:true});Kok.onMessage(event=>{if(['privateLocking','librarySwitched'].includes(event.data.eventName))invalidateVisualAgent();});
Kok.extend('ai','visual-agent.js:settingsBeforeVisualAgent','settings',settingsBeforeVisualAgent=>async function(){await settingsBeforeVisualAgent();const anchor=$('#small-dialog [data-ai-settings]')?.closest('.setting-row')||$('#small-dialog .about-details');if(anchor&&!$('#agent-skills-setting')){anchor.insertAdjacentHTML('afterend',`<div id="agent-skills-setting" class="setting-row smart-settings-row"><div>指令技能<small>就地编辑与按需提示词</small></div><button class="button secondary" data-agent-skills>管理技能</button></div>`);I18n.localize($('#small-dialog'));}});

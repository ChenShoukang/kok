'use strict';
let aiConfig=null,translationJob=null,translationGeneration=0,translationResult=null,translationAttempt='',autoTranslationTimer=null;
const translationLanguages=['简体中文','繁體中文','English','日本語','한국어','Français','Deutsch','Español'];
const translationLanguageLabels={'简体中文':'简体中文','繁體中文':'繁体中文',English:'英语','日本語':'日语','한국어':'韩语','Français':'法语',Deutsch:'德语','Español':'西班牙语'};
async function getAiConfig(){if(!aiConfig)aiConfig=await native('aiConfig');return aiConfig;}
function setupTranslationSettings(){
 const details=$('.about-details');if(!details||$('[data-ai-settings]'))return;
 details.insertAdjacentHTML('beforebegin',`<div class="setting-row"><div>AI 翻译<small>字幕、文档与电子书</small></div><button class="button secondary" data-ai-settings>配置 API</button></div>`);
}
async function showAiSettings(){
 const config=await getAiConfig();
 showSmall(`<h2>AI 翻译</h2><p class="dialog-description">连接你使用的大模型服务，翻译字幕、文档和电子书。支持 OpenAI 兼容接口。</p><form id="ai-form"><label class="form-label">API 地址<input id="ai-endpoint" type="url" required value="${esc(config.endpoint)}" placeholder="https://你的服务地址/v1" spellcheck="false"></label><label class="form-label">模型名称<input id="ai-model" required maxlength="200" value="${esc(config.model)}" placeholder="填写服务商提供的模型名称" spellcheck="false"></label><label class="form-label">API 密钥<input id="ai-key" type="password" autocomplete="new-password" maxlength="4096" placeholder="${config.hasKey?'已保存 · 留空保留现有密钥':'填写密钥；本机免密服务可留空'}"></label>${config.hasKey?'<label class="ai-check"><input id="ai-clear-key" type="checkbox">移除已保存的密钥</label>':''}<label class="form-label">目标语言<select id="ai-language">${translationLanguages.map(l=>`<option value="${esc(l)}" ${config.language===l?'selected':''}>${translationLanguageLabels[l]}</option>`).join('')}</select></label><label class="ai-check"><input id="ai-automatic" type="checkbox" ${config.automatic?'checked':''}>自动翻译打开的字幕、章节或 PDF 当前页</label><p class="ai-disclosure">翻译内容会发送到此接口，费用按服务商规则计算。密钥在本机加密保存。</p><div class="ai-form-actions"><button type="button" class="button secondary" id="ai-test">测试连接</button><button type="submit" class="button primary">保存设置</button></div><p id="ai-config-status" class="ai-status" role="status"></p></form>`);
 const form=$('#ai-form');
 async function save(){const next=await native('saveAiConfig',{endpoint:$('#ai-endpoint').value,model:$('#ai-model').value,apiKey:$('#ai-key').value,language:$('#ai-language').value,automatic:$('#ai-automatic').checked,clearKey:!!$('#ai-clear-key')?.checked});$('#ai-key').value='';aiConfig=next;translationAttempt='';return next;}
 form.addEventListener('submit',async e=>{e.preventDefault();const button=form.querySelector('[type=submit]');button.disabled=true;try{await save();await closeSmall();toast('AI 翻译设置已保存。');if(state.viewer)scheduleAutoTranslation();}catch(error){if(form.isConnected)$('#ai-config-status').textContent=error.message;}finally{button.disabled=false;}});
 $('#ai-test').addEventListener('click',async()=>{const button=$('#ai-test');button.disabled=true;try{await save();$('#ai-config-status').textContent='正在测试…';await native('translate',{jobId:uid(),parts:['Hello.'],test:true});if(form.isConnected)$('#ai-config-status').textContent='连接成功，可以开始翻译。';}catch(error){if(form.isConnected)$('#ai-config-status').textContent=error.message;}finally{button.disabled=false;}});
}
function ensureTranslationPanel(){
 if(!$('#translate-button'))$('.viewer-actions').insertAdjacentHTML('afterbegin',`<button class="button secondary translate-toggle hidden" id="translate-button" title="翻译字幕或正文"><span aria-hidden="true">A/文</span>翻译</button>`);
 if($('#translation-panel'))return;
 viewer.insertAdjacentHTML('beforeend',`<aside id="translation-panel" class="translation-panel hidden" aria-label="AI 译文"><div class="translation-heading"><b>译文</b><button class="icon-button" data-close-translation aria-label="收起译文">${svg('close')}</button></div><div class="translation-controls"><select id="translation-scope" aria-label="翻译范围"><option value="current">当前章节 / 页</option><option value="all">整份文档</option></select><button class="text-button" data-ai-settings title="翻译设置">${svg('settings')}</button><button class="text-button" id="translation-start">开始翻译</button></div><div class="translation-status" id="translation-status" role="status">点击开始翻译</div><div class="translation-progress hidden"><span></span></div><div class="translation-options"><label><input id="translation-bilingual" type="checkbox" checked>双语对照</label><button class="text-button hidden" id="translation-cancel">取消</button><button class="text-button hidden" id="translation-export">${svg('download')}导出</button></div><div id="translation-body" class="translation-body"></div></aside>`);
 $('#translate-button').addEventListener('click',async()=>{try{const config=await getAiConfig();if(!config.model){await showAiSettings();return;}showTranslationPanel();await startTranslation(false);}catch(error){fail(error);}});
 $('#translation-start').addEventListener('click',()=>startTranslation(false).catch(fail));
 $('#translation-cancel').addEventListener('click',()=>cancelTranslation());
 $('#translation-export').addEventListener('click',()=>exportTranslation().catch(fail));
 $('#translation-bilingual').addEventListener('change',()=>{renderTranslationResult();applyTranslatedSubtitles();});
 $('#translation-scope').addEventListener('change',()=>startTranslation(false).catch(fail));
}
function showTranslationPanel(){
 $('#notes-panel').classList.add('hidden');viewer.classList.remove('notes-open');viewer.classList.add('translation-open');$('#translation-panel').classList.remove('hidden');
}
function hideTranslationPanel(){viewer.classList.remove('translation-open');$('#translation-panel')?.classList.add('hidden');}
function resetTranslationView(){
 translationGeneration++;clearTimeout(autoTranslationTimer);cancelTranslation();translationResult=null;translationAttempt='';hideTranslationPanel();$('#translate-button')?.classList.add('hidden');$('#translation-cancel')?.classList.add('hidden');$('.translation-progress')?.classList.add('hidden');if($('#translation-body'))$('#translation-body').textContent='';
}
function beginTranslationView(item){
 ensureTranslationPanel();resetTranslationView();$('#translate-button').classList.toggle('hidden',!['video','document','book'].includes(item.kind));
 $('#translation-scope').classList.toggle('hidden',item.kind==='video');$('#translation-scope').value='current';
 $('#translation-export').classList.add('hidden');$('#translation-status').textContent='点击开始翻译';
}
function scheduleAutoTranslation(){
 clearTimeout(autoTranslationTimer);const generation=translationGeneration;
 autoTranslationTimer=setTimeout(async()=>{try{const config=await getAiConfig();if(generation!==translationGeneration||!state.viewer||!config.automatic||!config.model)return;await startTranslation(true);}catch(error){if(generation===translationGeneration)fail(error);}},650);
}
function initDocumentTranslation(){if(state.book||pdfState)scheduleAutoTranslation();}
async function translationSource(){
 const item=state.viewer;if(!item)throw Error('请先打开文件。');
 if(item.kind==='video'){
   let index=Number($('#subtitle-select')?.value);let track=subtitleTracks[index];if(track?.translated){index=track.sourceIndex;track=subtitleTracks[index];}
   if(!track||!track.cues?.length)throw Error('请先选择或载入一份文字字幕。');
   return{kind:'subtitle',itemId:item.id,name:item.name,title:track.name,sourceIndex:index,cues:track.cues.map(c=>({...c})),parts:track.cues.map(c=>c.text),headings:[]};
 }
 const all=$('#translation-scope').value==='all',parts=[],headings=[],headingUi=[];
 async function add(text,title,isUi=false){const chunks=await native('splitTranslationText',{text});for(const chunk of chunks){parts.push(chunk);headings.push(title);headingUi.push(isUi);}}
 if(state.book){for(const [index,chapter] of state.book.chapters.entries())if(all||index===state.chapter)await add(chapter.text,chapter.title,!!chapter.generatedTitle);}
 else if(pdfState){
   const session=pdfState,scroll=$('.pdf-scroll');let pageNumber=1;
   for(const wrapper of scroll.querySelectorAll('.pdf-page'))if(wrapper.offsetTop+wrapper.offsetHeight>scroll.scrollTop+scroll.clientHeight*.25){pageNumber=Number(wrapper.dataset.page);break;}
   for(let n=all?1:pageNumber;n<=(all?session.doc.numPages:pageNumber);n++){
     if(pdfState!==session)throw Error('文件已切换。');const page=await session.doc.getPage(n),content=await page.getTextContent();
     let text='';for(const word of content.items)if('str'in word)text+=word.str+(word.hasEOL?'\n':' ');
     if(text.trim())await add(text.trim(),'第 '+n+' 页',true);
   }
 }else throw Error('文件文字还未加载完成，请稍后再试。');
 if(!parts.some(p=>p.trim()))throw Error('没有可翻译的文字。扫描版 PDF 需要先进行 OCR。');
 return{kind:'document',itemId:item.id,name:item.name,title:all?'整份文档':headings[0],titleIsUi:all||!!headingUi[0],parts,headings,headingUi};
}
async function cancelTranslation(){
 const job=translationJob;if(!job)return;translationJob=null;
 try{await native('cancelTranslation',{jobId:job.id});}catch{}
 if(job.generation===translationGeneration&&!translationJob){if($('#translation-status'))$('#translation-status').textContent='翻译已取消';$('#translation-cancel')?.classList.add('hidden');$('.translation-progress')?.classList.add('hidden');}
}
async function startTranslation(automatic=false){
 const generation=translationGeneration,config=await getAiConfig();if(!config.model){if(!automatic)await showAiSettings();return;}
 const source=await translationSource();if(generation!==translationGeneration)return;
 const attempt=JSON.stringify([config.endpoint,config.model,config.language,source.itemId,source.title,source.parts]);
 if(automatic&&attempt===translationAttempt)return;if(translationJob?.attempt===attempt)return;
 await cancelTranslation();if(generation!==translationGeneration)return;translationAttempt=attempt;
 showTranslationPanel();translationResult=null;$('#translation-export').classList.add('hidden');$('#translation-body').textContent='';$('#translation-status').textContent='正在翻译为 '+config.language+'…';$('#translation-cancel').classList.remove('hidden');$('.translation-progress').classList.remove('hidden');$('.translation-progress span').style.width='0%';
 const job={id:uid(),attempt,generation,source};translationJob=job;
 try{
   const result=await native('translate',{jobId:job.id,parts:source.parts});if(translationJob!==job||generation!==translationGeneration)return;
   translationResult={...source,parts:result.parts,originalParts:source.parts,language:config.language};
   $('#translation-status').innerHTML=`<span>${result.cached?'已读取本地译文':'翻译完成'}</span> · <span>${esc(translationLanguageLabels[config.language]||config.language)}</span> · <span ${source.titleIsUi?'':'data-user-content'}>${esc(source.title)}</span>`;
   renderTranslationResult();applyTranslatedSubtitles();$('#translation-export').classList.remove('hidden');
 }catch(error){if(translationJob===job&&generation===translationGeneration){$('#translation-status').textContent=error.message;$('#translation-body').innerHTML='<p class="translation-empty">可检查 API 设置后重新开始。</p>';}}
 finally{if(translationJob===job){translationJob=null;$('#translation-cancel').classList.add('hidden');$('.translation-progress').classList.add('hidden');}}
}
function renderTranslationResult(){
 const result=translationResult;if(!result)return;const bilingual=$('#translation-bilingual').checked;let heading='';
 const limit=result.displayLimit||200;
 $('#translation-body').innerHTML=result.parts.slice(0,limit).map((text,n)=>{let title='';if(result.headings[n]&&result.headings[n]!==heading){heading=result.headings[n];title=`<h3 ${result.headingUi?.[n]?'data-ui-content':''}>${esc(heading)}</h3>`;}return`${title}<section>${bilingual?`<div class="translation-original">${esc(result.originalParts[n])}</div>`:''}<div class="translation-text">${esc(text)}</div></section>`;}).join('')+(result.parts.length>limit?'<button class="button secondary" data-more-translation>显示更多译文</button>':'');
}
function applyTranslatedSubtitles(){
 const result=translationResult;if(!result||result.kind!=='subtitle'||result.itemId!==state.viewer?.id||!$('#video'))return;
 const track={name:(translationLanguageLabels[result.language]||result.language)+' · AI '+($('#translation-bilingual').checked?'双语':'译文'),translated:true,language:result.language,sourceIndex:result.sourceIndex,cues:result.cues.map((c,n)=>({start:c.start,end:c.end,text:($('#translation-bilingual').checked?c.text+'\n':'')+result.parts[n]}))};
 let index=subtitleTracks.findIndex(t=>t.translated&&t.sourceIndex===result.sourceIndex);if(index<0){index=subtitleTracks.length;subtitleTracks.push(track);}else subtitleTracks[index]=track;
 renderSubtitleOptions();$('#subtitle-select').value=String(index);setSubtitle(index);
}
function subtitleTimestamp(seconds){const ms=Math.round(seconds*1000);return String(Math.floor(ms/3600000)).padStart(2,'0')+':'+String(Math.floor(ms/60000)%60).padStart(2,'0')+':'+String(Math.floor(ms/1000)%60).padStart(2,'0')+','+String(ms%1000).padStart(3,'0');}
async function exportTranslation(){
 const result=translationResult;if(!result)return;const bilingual=$('#translation-bilingual').checked;
 const text=result.kind==='subtitle'?result.cues.map((c,n)=>`${n+1}\n${subtitleTimestamp(c.start)} --> ${subtitleTimestamp(c.end)}\n${bilingual?c.text+'\n':''}${result.parts[n]}\n`).join('\n'):result.parts.map((p,n)=>(bilingual?result.originalParts[n]+'\n\n':'')+p).join('\n\n');
 if(await native('exportTranslation',{name:result.name,format:result.kind==='subtitle'?'srt':'txt',text}))toast('译文已导出。');
}
window.chrome?.webview?.addEventListener('message',e=>{if(e.data.eventName!=='translationProgress'||e.data.data.jobId!==translationJob?.id)return;const progress=e.data.data;$('#translation-status').textContent=`正在翻译 ${progress.completed} / ${progress.total}`;$('.translation-progress span').style.width=(100*progress.completed/progress.total)+'%';});
document.addEventListener('click',e=>{if(e.target.closest('[data-ai-settings]'))showAiSettings().catch(fail);if(e.target.closest('[data-close-translation]'))hideTranslationPanel();if(e.target.closest('[data-more-translation]')&&translationResult){translationResult.displayLimit=(translationResult.displayLimit||200)+200;renderTranslationResult();}});
$('#notes-button').addEventListener('click',()=>{if(!$('#notes-panel').classList.contains('hidden'))hideTranslationPanel();});

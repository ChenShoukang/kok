import test from 'node:test';
import assert from 'node:assert/strict';
import { RequirementUpdateSchema, RequirementProposalSchema, validateRequirementUpdate } from '../src/business/requirement-schema.ts';
import { preserveAcceptance, requirementConflict } from '../src/business/requirements.ts';
import { planTask } from '../src/business/planning.ts';
import { codeProblem } from '../src/business/completion.ts';
import { progressDecision } from '../src/business/progress.ts';
import { createServer } from 'node:http';
import { ResearchKernel } from '../src/kernel.ts';

const input=(goal,extra={})=>({goal,adjustments:[],allowed:['a','b'],requirementsVersion:1,requirementUpdates:[],intent:'assist',approvalPolicy:'each',scope:{mode:'selected'},explicitTools:null,features:{memory:false,delegation:false,sandbox:false,search:false,parent:false},environment:{workspace:true,rendering:false,tools:['create_document','create_presentation','calculate'],selectedFormats:[]},context:{taskId:'',goal:'',domain:'',workspaceId:'',files:[]},...extra});
const update=(basis,changes,extra={})=>({baseRevision:0,userRevision:0,basis,changes,...extra});
const apply=(p,u)=>{const before=planTask(p);validateRequirementUpdate(p,u.baseRevision,u,before.contract);const after=planTask({...p,requirementUpdates:[...p.requirementUpdates,u]});preserveAcceptance(before.contract,after.contract);return after;};
test('deck totals survive page allocations and ordinal references; explicit resizing still works',()=>{
 const goal='为志愿者做一套可编辑的 8 页 PPT，封面算一页。';
 const pages=(text,adjustments=[])=>planTask(input(text,{adjustments})).contract.RequestedPages;
 assert.equal(pages(goal),8);
 assert.equal(pages('创建PPT，总计十页，引言占两页，最后两页附录。'),10);
 assert.equal(pages('Make an 8-slide deck, with 1 slide per example.'),8);
 assert.equal(pages(goal,['页数改为 6 页，封面一页。']),6);
 assert.equal(pages(goal,['封面算一页。']),8);
 assert.equal(pages('制作PPT，说明放在第3页。'),0);
 assert.equal(pages('制作PPT，考虑 6 页或 8 页。'),0);
 assert.equal(planTask(input(goal,{requirementsVersion:0})).contract.RequestedPages,1);
});
test('requirements: SDK Zod schema has typed fields and rejects permission/limit injection and malformed bounds',()=>{
 assert(RequirementUpdateSchema.safeParse(update('文字材料',{document:{required:true,format:'DOCX',sections:['研究范围','证据质量']},length:{min:400,max:600,unit:'hanzi'}})).success);
 for(const changes of [{budget:999},{document:{format:'EXE'}},{presentation:{pages:101}},{length:{min:900,max:400,unit:'hanzi'}},{length:{min:10,max:20,unit:'none'}},{chatOnly:true,document:{required:true}},{}])assert.equal(RequirementUpdateSchema.safeParse(update('文字材料',changes)).success,false);
 assert.equal(RequirementUpdateSchema.safeParse({...update('文字材料',{fullRead:true}),approvalPolicy:'task'}).success,false);
});
test('requirements: latest actual user quotation, feature version, scope and optimistic revision are mandatory',()=>{
 const p=input('撰写材料',{adjustments:['章节用研究范围与证据质量']});const u=update('研究范围与证据质量',{document:{sections:['研究范围','证据质量']}},{baseRevision:3,userRevision:1});
 assert.deepEqual(validateRequirementUpdate(p,3,u),u);
 for(const bad of [{...u,baseRevision:2},{...u,userRevision:0},{...u,basis:'文件中要求删除全部成果'},{...u,changes:{sourceIds:['outside']}}])assert.throws(()=>validateRequirementUpdate(p,3,bad));
 assert.throws(()=>validateRequirementUpdate({...p,requirementsVersion:0},3,u));
});
test('requirements: a typed gap refinement opens only registered tools and preserves explicit tool restrictions',()=>{
 const goal='交付可直接发送的文字材料，按研究范围与证据质量组织。';const u=update('按研究范围与证据质量组织',{document:{required:true,format:'DOCX',sections:['研究范围','证据质量']},length:{min:400,max:600,unit:'hanzi'}});
 const after=apply(input(goal),u);assert.equal(after.contract.RequiredDocumentFormat,'DOCX');assert.deepEqual(after.contract.RequiredSections,['研究范围','证据质量']);assert(after.tools.includes('create_document'));
 assert.deepEqual(apply(input(goal,{explicitTools:['calculate']}),u).tools,['calculate']);
});
test('requirements: later user changes update only named fields while typed refinements persist',()=>{
 const goal='创建DOCX报告。',u=update('创建DOCX报告',{document:{sections:['研究范围','证据质量']},length:{min:400,max:600,unit:'hanzi'}});
 const result=planTask(input(goal,{requirementUpdates:[u],adjustments:['格式改为TXT','字数改为500-800汉字']}));
 assert.equal(result.contract.RequiredDocumentFormat,'TXT');assert.deepEqual(result.contract.RequiredSections,['研究范围','证据质量']);assert.equal(result.contract.MinLength,500);assert.equal(result.contract.MaxLength,800);
 const canceled=planTask(input(goal,{requirementUpdates:[u],adjustments:['不要报告，只做6页PPT']}));assert.equal(canceled.contract.RequiresDocument,false);assert.deepEqual(canceled.contract.RequiredSections,[]);assert.equal(canceled.contract.RequestedPages,6);
});
test('editorial choices never become required section names or audience claims without user wording',()=>{
 const goal='交一份DOCX，让领队照着安排工作。',p=input(goal);
 for(const changes of [{document:{sections:['登记纪律']}},{purpose:'安排现场三班轮值'},{audience:'领队及参会成员'}])
  assert.throws(()=>apply(p,update(goal,changes)),/section titles|user message/);
 const omitted=[],mixed=validateRequirementUpdate(p,0,update(goal,{document:{required:true,format:'DOCX',sections:['登记纪律']},audience:'领队及参会成员'}),planTask(p).contract,omitted);
 assert.deepEqual(mixed.changes,{document:{required:true,format:'DOCX'}});assert.deepEqual(omitted,['document.sections','audience']);
 assert.equal(apply(p,update(goal,{audience:'领队',purpose:'安排工作'})).contract.Purpose,'安排工作');
 const prior='创建DOCX报告，按「执行流程」组织。',change='再增加「未定事项」。';
 const inputWithSections=input(prior,{requirementUpdates:[update(prior,{document:{sections:['执行流程']}})],adjustments:[change]});
 assert.deepEqual(apply(inputWithSections,update(change,{document:{sections:['执行流程','未定事项']}},{userRevision:1,baseRevision:2})).contract.RequiredSections,['执行流程','未定事项']);
});
test('requirements: model updates cannot silently cancel or weaken existing acceptance checks',()=>{
 const p=input('完整阅读所有资料，创建400-600汉字DOCX报告，并制作8页PPT。报告应包含目标、风险。');
 for(const changes of [{document:{required:false}},{presentation:{required:false}},{presentation:{pages:6}},{document:{format:'TXT'}},{document:{sections:['目标']}},{length:{min:0,max:0,unit:'none'}},{fullRead:false},{sourceIds:['a']}])assert.throws(()=>apply(p,update('完整阅读所有资料',changes)));
 const changed=input('制作8页PPT',{adjustments:['不做PPT了，只写DOCX报告']});assert.equal(apply(changed,update('不做PPT了',{presentation:{required:false}},{userRevision:1,baseRevision:1})).contract.RequiresPresentation,false);
 assert.throws(()=>apply(input('只在聊天回复，不创建文件'),update('只在聊天回复',{document:{required:true,format:'DOCX'}})));
});
test('requirements: read-only mode, no-op updates and old task versions retain their boundaries',()=>{
 const goal='创建DOCX报告',u=update(goal,{document:{required:true,format:'DOCX'}});
 assert.deepEqual(planTask(input(goal,{requirementUpdates:[u],approvalPolicy:'read_only'})).contract.RequiredSections,[]);
 assert.equal(planTask(input(goal,{requirementUpdates:[u],approvalPolicy:'read_only'})).contract.RequiresDocument,false);
 assert.deepEqual(planTask(input(goal)).contract,apply(input(goal),u).contract);
 assert.deepEqual(planTask(input(goal,{requirementUpdates:[update(goal,{document:{sections:['不可注入旧任务']}})],requirementsVersion:0})).contract.RequiredSections,[]);
});
test('requirements: typed code verification is consumed by acceptance and later user directives win',()=>{
 const p=input('改进这个函数，但这轮无需启动校验程序。');const u=update('这轮无需启动校验程序',{verifyCode:false});
 assert.equal(apply(p,u).contract.CodeVerification,false);assert.equal(planTask({...p,requirementUpdates:[u],adjustments:['现在运行测试']}).contract.CodeVerification,true);
 assert.throws(()=>apply(input('修复工作区代码并运行测试'),update('修复工作区代码',{verifyCode:false})));
 assert.throws(()=>apply(input('修改代码，但不要运行测试'),update('修改代码',{verifyCode:true})));
 const evidence={goal:'修改代码并运行测试',adjustments:[],intent:'assist',approvalPolicy:'each',workspaceVersion:1,workspaceId:'w',calls:[{tool:'apply_workspace_patch',status:'done',sequence:1,path:'sum.ts',hash:'new',workspaceId:'w',checkedInputs:[]}],files:[{path:'sum.ts',hash:'new',available:true}]};
 assert.equal(codeProblem({...evidence,verification:false}),'');assert.match(codeProblem({...evidence,verification:true}),/实际验证/);
});
test('requirements: repeated identical no-op updates are bounded by the existing progress guard',()=>{
 const calls=Array.from({length:3},(_,i)=>({tool:'update_task_spec',current:true,turnId:'turn-'+i,fingerprint:'same-noop'}));assert.equal(progressDecision({calls}).action,'stop');
});

test('requirements: purpose and audience retain their user revision and survive unrelated edits without relaxing delivery',()=>{
 const goal='创建400-600汉字DOCX报告，面向活动负责人，用于决定是否启动活动。';
 const u=update('面向活动负责人，用于决定是否启动',{purpose:'决定是否启动活动',audience:'活动负责人'});
 const first=apply(input(goal),u).contract;assert.equal(first.Purpose,'决定是否启动活动');assert.equal(first.Audience,'活动负责人');assert.equal(first.RequiredDocumentFormat,'DOCX');assert.equal(first.MinLength,400);
 const next=planTask(input(goal,{requirementUpdates:[u],adjustments:['改为TXT格式']})).contract;assert.equal(next.Purpose,first.Purpose);assert.equal(next.Audience,first.Audience);assert.equal(next.RequiredDocumentFormat,'TXT');
 const changed=update('现在给志愿者看',{audience:'志愿者'},{baseRevision:2,userRevision:1});
 const later=apply(input(goal,{requirementUpdates:[u],adjustments:['现在给志愿者看']}),changed).contract;assert.equal(later.Audience,'志愿者');assert.equal(later.Purpose,first.Purpose);assert.equal(later.MinLength,400);
 assert.equal(apply(input(goal,{approvalPolicy:'read_only'}),u).contract.Audience,'活动负责人');
 for(const changes of [{purpose:' '},{audience:'人'.repeat(201)},{purpose:'决策',semanticCertified:true}])assert.equal(RequirementUpdateSchema.safeParse(update(goal,changes)).success,false);
});

test('requirements: confirmed cancellations and explicit negative directives cannot be undone as automatic gap filling',()=>{
 const goal='创建DOCX报告并制作8页PPT。',cancel=update(goal,{document:{required:false}});
 const accepted=input(goal,{requirementUpdates:[cancel]});const current=planTask(accepted).contract;
 assert.equal(current.RequiresDocument,false);assert(current.ForbiddenArtifacts.includes('document'));assert.equal(current.RequestedPages,8);
 assert.throws(()=>apply(accepted,update(goal,{document:{required:true,format:'DOCX'}},{baseRevision:1})));
 const changed=planTask({...accepted,adjustments:['现在再写一份DOCX报告']}).contract;assert.equal(changed.RequiresDocument,true);assert(!changed.ForbiddenArtifacts.includes('document'));
 const explicit=input('创建DOCX报告，不制作PPT。');assert(planTask(explicit).contract.ForbiddenArtifacts.includes('presentation'));assert.throws(()=>apply(explicit,update('创建DOCX报告',{presentation:{required:true,pages:8}})));
 assert.deepEqual(planTask({...explicit,requirementsVersion:0}).contract.ForbiddenArtifacts,[]);
 const replacement=planTask(input(goal,{requirementUpdates:[update(goal,{purpose:'决策',audience:'负责人',sourceIds:['b']})],adjustments:['改为制作8页PPT']})).contract;
 assert.equal(replacement.Purpose,'决策');assert.equal(replacement.Audience,'负责人');assert.deepEqual(replacement.RequiredSourceIds,['b']);
});

test('requirements: proposal classification retains the immutable schema and separates conflicting changes from refinements',()=>{
 const goal='创建400-600汉字DOCX报告并制作8页PPT。';const p=input(goal),before=planTask(p).contract;
 const raw=update(goal,{document:{required:false}}),after=planTask({...p,requirementUpdates:[raw]}).contract;
 assert.match(requirementConflict(before,after),/remove/);assert.equal(requirementConflict(before,planTask({...p,requirementUpdates:[update(goal,{purpose:'决定是否启动'})]}).contract),'');
 assert.throws(()=>requirementConflict({...before,RequiredSections:null},before),TypeError,'Unexpected internal errors cannot become an approval-able conflict');
 for(const proposal of [{...raw,approved:true},{...raw,allowReplacement:true},{...raw,changes:{...raw.changes,permissions:['all']}},{...raw,changes:{sourceIds:['outside']}}])assert.throws(()=>validateRequirementUpdate(p,0,proposal));
 for(const changes of [{document:{format:'EXE'}},{permissions:['all']},{length:{min:800,max:400,unit:'hanzi'}}])assert.equal(RequirementProposalSchema.safeParse(update(goal,changes)).success,false,'The compact proposal wire schema retains full SDK validation');
});
test('requirements: real LangChain tool adapter sends the Zod schema and resumes the same call across restart',async()=>{
 const requests=[],goal='交付可直接发送的文字材料，按研究范围和证据质量组织，面向活动负责人，用于决定是否启动活动。';const proposal=update(goal,{document:{required:true,format:'DOCX',sections:['研究范围','证据质量']},purpose:'决定是否启动活动',audience:'活动负责人'});
 const server=createServer(async(req,res)=>{let raw='';for await(const part of req)raw+=part;requests.push(JSON.parse(raw));const first=requests.length===1;
  const delta=first?{role:'assistant',tool_calls:[{index:0,id:'spec-call',type:'function',function:{name:'update_task_spec',arguments:JSON.stringify(proposal)}}]}:{role:'assistant',content:'Current requirements acknowledged.'};
  res.writeHead(200,{'content-type':'text/event-stream'});res.end('data: '+JSON.stringify({id:'fixture',object:'chat.completion.chunk',model:'requirements-fixture',choices:[{index:0,delta,finish_reason:first?'tool_calls':'stop'}],usage:{prompt_tokens:100,completion_tokens:20}})+'\n\ndata: [DONE]\n\n');
 });await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 try{
  const request={taskId:'requirements-sdk-restart',endpoint:'http://127.0.0.1:'+server.address().port+'/v1',model:'requirements-fixture',systemPrompt:'Use current USER instructions.',messages:[{role:'user',content:goal}],tools:[{type:'function',function:{name:'update_task_spec',description:'Native typed update',parameters:{type:'object',properties:{changes:{type:'object'}},required:['changes']}}}],taskSpec:{version:1,revision:0,userRevision:0,requirementsVersion:1,goal,capabilities:['research'],scope:{mode:'all',ids:[]},deliverables:[],fullRead:false,targetedEdit:false,limits:{requests:3,contextTokens:32000}}};
  const first=await new ResearchKernel().step(request);assert.equal(first.kind,'tools');assert.deepEqual(first.calls[0].args,proposal);
  const schema=requests[0].tools.find(t=>t.function.name==='update_task_spec').function.parameters;assert(schema.properties.baseRevision);assert(schema.properties.changes.properties.document.properties.sections);assert.equal(schema.additionalProperties,false);assert.equal(schema.properties.changes.additionalProperties,false);
  assert(!requests[0].tools.some(t=>['write_file','edit_file'].includes(t.function.name)),'Framework scratch writes cannot substitute for native deliverables');
  const last=await new ResearchKernel({snapshot:first.snapshot}).step({...request,taskSpec:{...request.taskSpec,revision:1,purpose:proposal.changes.purpose,audience:proposal.changes.audience},messages:[],resume:{[first.calls[0].interruptId]:{status:'done',result:{revision:1,requirements:apply(input(goal),proposal).contract}}}});
  assert.equal(last.kind,'final');assert.equal(requests.length,2);assert(requests[1].messages.some(m=>m.role==='tool'&&m.tool_call_id==='spec-call'));
  const prompt=requests[1].messages.filter(m=>['system','developer'].includes(m.role)).map(m=>typeof m.content==='string'?m.content:m.content.filter(part=>part.type==='text').map(part=>part.text).join('\n')).join('\n');
  assert.match(prompt,/决定是否启动活动/,'SDK receives the current purpose after host resume');assert.match(prompt,/活动负责人/,'SDK receives the current audience after host resume');
 }finally{server.closeAllConnections();await new Promise(resolve=>server.close(resolve));}
});

test('requirements: the proposal SDK tool pauses for a native decision and resumes a declined proposal without inventing approval',async()=>{
 const requests=[],goal='创建DOCX报告；文字材料这回先缓一缓。',proposal=update(goal,{document:{required:false}});
 const server=createServer(async(req,res)=>{let raw='';for await(const part of req)raw+=part;requests.push(JSON.parse(raw));const first=requests.length===1;
  const delta=first?{role:'assistant',tool_calls:[{index:0,id:'proposal-call',type:'function',function:{name:'propose_task_spec',arguments:JSON.stringify(proposal)}}]}:{role:'assistant',content:'已保留当前交付要求。'};
  res.writeHead(200,{'content-type':'text/event-stream'});res.end('data: '+JSON.stringify({id:'proposal-fixture',object:'chat.completion.chunk',model:'requirements-fixture',choices:[{index:0,delta,finish_reason:first?'tool_calls':'stop'}],usage:{prompt_tokens:100,completion_tokens:20}})+'\n\ndata: [DONE]\n\n');
 });await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 try{
  const request={taskId:'requirements-proposal-decline',endpoint:'http://127.0.0.1:'+server.address().port+'/v1',model:'requirements-fixture',systemPrompt:'Wait for the native decision.',messages:[{role:'user',content:goal}],tools:['propose_task_spec','update_task_spec'].map(name=>({type:'function',function:{name,description:'Native typed requirements',parameters:{type:'object',properties:{changes:{type:'object'}},required:['changes']}}}))};
  const first=await new ResearchKernel().step(request);assert.equal(first.kind,'tools');assert.equal(first.calls[0].name,'propose_task_spec');assert.deepEqual(first.calls[0].args,proposal);assert.equal(requests.length,1);
  const schema=requests[0].tools.find(t=>t.function.name==='propose_task_spec').function.parameters;assert(schema.properties.baseRevision);assert.equal(schema.additionalProperties,false);assert(!schema.properties.approved);
  const detailed=requests[0].tools.find(t=>t.function.name==='update_task_spec').function.parameters;assert(!schema.properties.changes.properties,'The proposal does not resend all facet definitions');assert(JSON.stringify(schema).length<JSON.stringify(detailed).length);
  const last=await new ResearchKernel({snapshot:first.snapshot}).step({...request,messages:[],resume:{[first.calls[0].interruptId]:{status:'skipped',error:'user declined the requirement change'}}});
  assert.equal(last.kind,'final');assert.equal(requests.length,2);assert(requests[1].messages.some(m=>m.role==='tool'&&m.tool_call_id==='proposal-call'&&m.content.includes('skipped')));
 }finally{server.closeAllConnections();await new Promise(resolve=>server.close(resolve));}
});

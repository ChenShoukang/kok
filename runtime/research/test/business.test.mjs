import test from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { planTask, deriveContract, renderingRequired } from '../src/business/planning.ts';
import { reviewArtifact, reviewSources } from '../src/business/review.ts';
import { codeProblem, deliveryProblem } from '../src/business/completion.ts';

const names=['search_library','read_items','review_sources','list_collections','calculate','recall_result','update_plan','finish_task','read_document','create_document','replace_document_text','inspect_document','read_presentation','create_presentation','edit_presentation','copy_presentation','inspect_presentation','render_presentation','read_image','load_skill','workspace_info','search_workspace','read_workspace_file','git_workspace','list_folder','read_text_file','write_text_file','run_command','apply_workspace_patch','start_command_job','read_command_job','wait_command_job','cancel_command_job','open_browser','read_browser','browser_action','search_memory','remember_memory','mcp_call','list_mcp_tools'];
const input=(goal,changes={})=>({goal,adjustments:[],allowed:['a','b'],intent:'assist',approvalPolicy:'each',scope:{mode:'selected',ids:['a','b']},explicitTools:null,features:{memory:false,delegation:false,sandbox:false,search:false,parent:false},environment:{workspace:true,rendering:true,tools:names,selectedFormats:[]},context:{taskId:'',goal:'',domain:'',workspaceId:'',files:[]},...changes});

test('business: ordered document/PPT replacements clear obsolete kind, length and page requirements',()=>{
 const doc=deriveContract(input('完整阅读资料，生成8页PPT',{adjustments:['改成写一份500-800汉字DOCX报告']}));
 assert.equal(doc.RequiresDocument,true);assert.equal(doc.RequiresPresentation,false);assert.equal(doc.RequestedPages,0);assert.equal(doc.MinLength,500);assert.equal(doc.MaxLength,800);assert.equal(doc.RequiredDocumentFormat,'DOCX');
 const deck=deriveContract(input('写一份1000字DOCX报告',{adjustments:['改为8页PPT']}));assert.equal(deck.RequiresPresentation,true);assert.equal(deck.RequiresDocument,false);assert.equal(deck.MinLength,0);assert.equal(deck.RequestedPages,8);
});
test('business: current length, full reading and source subset follow later user messages',()=>{
 const contract=deriveContract(input('根据全部资料写800-1200汉字报告',{adjustments:['改为300-500汉字，只用第二份资料','只看开头，给初步意见','取消字数限制']}));
 assert.equal(contract.MinLength,0);assert.equal(contract.MaxLength,0);assert.equal(contract.RequiresFullSourceCoverage,false);assert.deepEqual(contract.RequiredSourceIds,['b']);
});
test('business: source revision labels do not create a gratuitous editing requirement',()=>{
 const contract=deriveContract(input('采用最终修订预算，创建400-600汉字DOCX活动报告，保留禁止收集手机号的限制。'));
 assert.equal(contract.RequiresDocument,true);assert.equal(contract.RequiresTargetedEdit,false);
});
test('business: an eight-page presentation about a plan is one PPTX, not an extra report',()=>{
 const contract=deriveContract(input('根据所选资料生成8页活动方案PPT，并保留未知信息待确认。'));
 assert.equal(contract.RequiresPresentation,true);assert.equal(contract.RequiresDocument,false);assert.equal(contract.RequestedPages,8);
});
test('business: requested delivery of a DOCX report and PPT retains both actual deliverables',()=>{
 const contract=deriveContract(input('交付一份400-600汉字的DOCX活动报告，以及8页可编辑PPT（含封面）。'));
 assert.equal(contract.RequiresDocument,true);assert.equal(contract.RequiresPresentation,true);assert.equal(contract.RequiredDocumentFormat,'DOCX');assert.equal(contract.RequestedPages,8);assert.equal(contract.MinLength,400);assert.equal(contract.MaxLength,600);
});
test('business: reading existing page counts never demands recreating the presentation',()=>{
 const read=deriveContract(input('完整读完所选DOCX报告和8页PPT的实际正文，检查结构，不新建、不修改文件。'));
 assert.equal(read.RequiresPresentation,false);assert.equal(read.RequiresDocument,false);assert.equal(read.RequiresTargetedEdit,false);
 const follow=deriveContract(input('制作8页PPT',{adjustments:['检查第2页的文字，保留其他页']}));assert.equal(follow.RequestedPages,8);
 const make=deriveContract(input('读取资料做8页PPT'));assert.equal(make.RequiresPresentation,true);assert.equal(make.RequestedPages,8);
});
test('business: historical creation references and separate document writing do not request a new deck',()=>{
 for(const goal of [
  '继续检查上次已生成的两份成果，不新建、不修改文件。完整读完所选初稿、最终修订、DOCX报告和8页PPT的实际正文，检查文档与PPT结构。核对最终口径8万元、60人、不收集手机号，未知信息保持待确认；仅说明实际结果与文件ID，以及结构检查和渲染的真实限制。已有PowerPoint页面渲染可保留，不重做。',
  '需要检查已经创建的8页PPT，阅读来源资料，不生成新文件。',
  '检查刚刚制作的8页PPT，并写一份报告。',
  '阅读来源资料以及8页PPT。'
 ])assert.equal(deriveContract(input(goal)).RequiresPresentation,false,goal);
 const mixed=deriveContract(input('检查刚刚制作的8页PPT，并写一份DOCX报告。'));assert.equal(mixed.RequiresDocument,true);assert.equal(mixed.RequiredDocumentFormat,'DOCX');
 const edit=deriveContract(input('在已有PPT中增加一页。'));assert.equal(edit.RequiresPresentation,false);assert.equal(edit.RequiresTargetedEdit,true);
});
test('business: chat/read-only requirements and explicit capability lists remain authoritative',()=>{
 const readonly=planTask(input('修改报告并渲染8页PPT',{approvalPolicy:'read_only'}));assert.equal(readonly.contract.RequiresDocument,false);assert.equal(readonly.contract.RequiresPresentation,false);assert(!readonly.tools.includes('render_presentation'));assert(!readonly.tools.includes('edit_presentation'));
 const explicit=planTask(input('修复代码并运行测试',{explicitTools:['calculate']}));assert.deepEqual(explicit.tools,['calculate']);
});
test('business: same-workspace continuation retains code tools without unrelated library tools',()=>{
 const result=planTask(input('继续优化这个函数',{scope:{mode:'all'},context:{taskId:'prior',goal:'修复工作区sum.ts',domain:'coding',workspaceId:'root',files:['sum.ts']}}));
 assert.equal(result.coding,true);assert(result.tools.includes('apply_workspace_patch'));assert(!result.tools.includes('create_document'));assert(!result.tools.includes('write_text_file'));
 const switched=planTask(input('继续创建DOCX报告',{context:{taskId:'prior',goal:'修复工作区sum.ts',domain:'coding',workspaceId:'root',files:['sum.ts']}}));assert.equal(switched.context.taskId,'');assert.equal(switched.coding,false);assert(switched.tools.includes('create_document'));
});
test('business: full-library code/report requests do not imply reading every unrelated source',()=>{
 assert.equal(deriveContract(input('创建一份报告',{scope:{mode:'all'}})).RequiresFullSourceCoverage,false);
 assert.equal(deriveContract(input('完整阅读全部资料并写报告',{scope:{mode:'all'}})).RequiresFullSourceCoverage,true);
});
test('business: document review measures saved text but does not claim factual or visual certification',()=>{
 const report=reviewArtifact({id:'doc',kind:'document',fileVersion:'version',text:'# 方案\n采用8万元预算。\n```js\n1+1',sections:['风险']});
 assert.equal(report.headings.length,1);assert(report.findings.some(x=>x.code==='missing_section'));assert(report.findings.some(x=>x.code==='unclosed_code_fence'));assert.equal(report.rendered,false);assert.equal(report.visualReviewed,false);
});
test('business: document review reports the requested native length unit without confusing raw characters',()=>{
 const report=reviewArtifact({id:'doc',kind:'document',format:'DOCX',fileVersion:'v',text:'文'.repeat(485)+' 60 80000 '.repeat(30),measurements:{hanzi:485,characters:695,words:0},requirement:{minChars:400,maxChars:600,lengthUnit:1,requiredFormat:'DOCX'}});
 assert.equal(report.deliveryCheck.unit,'hanzi');assert.equal(report.deliveryCheck.value,485);assert.equal(report.deliveryCheck.withinBounds,true);assert.equal(report.deliveryCheck.formatMatches,true);
 const outside=reviewArtifact({id:'doc',kind:'document',format:'TXT',fileVersion:'v',text:'text',measurements:{hanzi:0,characters:4,words:1},requirement:{minChars:400,maxChars:600,lengthUnit:2,requiredFormat:'DOCX'}});
 assert.equal(outside.deliveryCheck.withinBounds,false);assert.equal(outside.deliveryCheck.formatMatches,false);
});
test('business: PPT review locates actual page/shape defects without treating decoration as text overlap',()=>{
 const shape=(id,x,y,text)=>({id,x,y,w:4,h:1,fontSize:24,rotation:0,kind:'rect',text});
 const report=reviewArtifact({id:'deck',kind:'presentation',fileVersion:'v',deck:{width:13.333,height:7.5,unsupported:0,slides:[{shapes:[shape('background',0,0,''),shape('outside',20,1,'Outside'),shape('a',1,1,'Alpha'),shape('b',1,1,'Beta')]}]}});
 assert(report.findings.some(x=>x.code==='outside_canvas'&&x.page===1&&x.shapeId==='outside'));assert.equal(report.findings.filter(x=>x.code==='text_overlap').length,1);assert.equal(report.visualReviewed,false);
});
test('business: a capped presentation report retains definite errors after a long warning list',()=>{
 const warnings=Array.from({length:120},(_,index)=>({id:'warning-'+index,x:0,y:0,w:14,h:7.5,fontSize:24,rotation:0,kind:'rect',text:''}));
 const report=reviewArtifact({id:'deck',kind:'presentation',fileVersion:'v',deck:{width:13.333,height:7.5,unsupported:0,slides:[{shapes:[...warnings,{id:'outside',x:20,y:0,w:2,h:1,fontSize:24,rotation:0,kind:'rect',text:'Outside'}]}]}});
 assert.equal(report.errors,1);assert.equal(report.truncated,true);assert.equal(report.findings.length,100);assert(report.findings.some(row=>row.severity==='error'&&row.shapeId==='outside'));
});
test('business: source parsing, current processing, stale reads and output citations are separate',()=>{
 const rows=reviewSources({sources:[{id:'a',name:'a',removed:false,parsed:true,complete:false,valid:0,stale:0,detail:''},{id:'b',name:'b',removed:false,parsed:true,complete:false,valid:0,stale:2,detail:'changed'}]});
 assert.equal(rows[0].status,'unread');assert.equal(rows[0].processed,false);assert.equal(rows[1].status,'stale');assert.equal(rows[1].citationStatus,'not_semantically_verified');
});
const codeCall=(tool,extra={})=>({tool,status:'done',sequence:1,path:'sum.ts',hash:'new',workspaceId:'root',jobState:'',exitCode:null,inputsCurrent:false,outputsVerified:true,checkedInputs:[],...extra});
const evidence=(changes={})=>({goal:'修复工作区代码并运行测试',adjustments:[],intent:'assist',approvalPolicy:'each',workspaceVersion:1,workspaceId:'root',calls:[codeCall('apply_workspace_patch')],files:[{path:'sum.ts',hash:'new',available:true}],...changes});
test('business: successful code verification binds current workspace, bytes and actual successful output state',()=>{
 const proof=codeCall('wait_command_job',{sequence:2,path:'',hash:'',jobState:'completed',exitCode:0,inputsCurrent:true,checkedInputs:[{path:'sum.ts',sha256:'new',workspaceId:'root'}]});
 assert.equal(codeProblem(evidence({calls:[codeCall('apply_workspace_patch'),proof]})),'');
 assert.match(codeProblem(evidence({calls:[codeCall('apply_workspace_patch'),{...proof,outputsVerified:false}]})),/实际验证/);
 assert.match(codeProblem(evidence({calls:[codeCall('apply_workspace_patch'),proof],workspaceId:'other'})),/工作区已变化/);
 assert.match(codeProblem(evidence({calls:[codeCall('apply_workspace_patch'),proof],files:[{path:'sum.ts',hash:'changed',available:true}]})),/已变化/);
});
test('business: no-test instructions, later cancellation and analysis do not force unrequested work',()=>{
 assert.equal(codeProblem(evidence({goal:'修复测试错误，但不要运行测试'})),'');
 assert.match(codeProblem(evidence({goal:'修复测试错误，但不要运行测试',adjustments:['现在运行测试']})),/实际验证/);
 assert.equal(codeProblem(evidence({goal:'修复测试错误，但不要运行测试',adjustments:['现在运行测试','改为不要运行测试']})),'');
 assert.equal(codeProblem(evidence({goal:'解释优化算法',calls:[codeCall('read_workspace_file')]})),'');
 assert.equal(codeProblem(evidence({calls:[],adjustments:['先不要修改，只分析']})),'');
});
test('business: actual rendering is required only until the user cancels that requirement',()=>{
 assert.equal(renderingRequired(['制作并渲染8页PPT']),true);assert.equal(renderingRequired(['渲染PPT','无需渲染']),false);assert.equal(renderingRequired(['无需渲染','现在渲染全部页面']),true);
});

const artifact=(changes={})=>({id:'file',name:'file',exists:true,owned:true,format:'DOCX',length:500,pages:8,readable:true,readBack:true,detail:'',qualityErrors:[],...changes});
const delivery=(changes={})=>({requirement:{requiresDocumentArtifact:true,requiresPresentationArtifact:false,requiresTargetedEdit:false,chatOnly:false,requestedPages:0,minChars:400,maxChars:600,requiredFormat:'DOCX',requiresFullSourceCoverage:true,requiredSourceIds:['a']},document:artifact(),presentation:null,edit:null,workspaceVersion:0,codeProblem:'',chatLength:0,sourcesFull:true,sourceDetail:'unread',renderingRequired:false,renderingComplete:false,...changes});
test('business: completion requires actual current deliverables, format, length, read-back and source coverage',()=>{
 assert.equal(deliveryProblem(delivery()),'');assert.match(deliveryProblem(delivery({document:null})),/实际文档成果/);
 assert.match(deliveryProblem(delivery({document:artifact({format:'TXT'})})),/格式/);
 assert.match(deliveryProblem(delivery({document:artifact({length:100})})),/长度实测/);
 assert.match(deliveryProblem(delivery({document:artifact({readable:false})})),/不可读取/);
 assert.match(deliveryProblem(delivery({document:artifact({readBack:false})})),/当前版本回读/);
 assert.match(deliveryProblem(delivery({sourcesFull:false})),/未覆盖/);
});
test('business: current PPT defects, page gaps and requested renders block false completion',()=>{
 const requirement={...delivery().requirement,requiresDocumentArtifact:false,requiresPresentationArtifact:true,requestedPages:8};
 const input=delivery({requirement,presentation:artifact({format:'PPTX'})});assert.equal(deliveryProblem(input),'');
 assert.match(deliveryProblem({...input,presentation:artifact({pages:7})}),/实际 7 页/);
 assert.match(deliveryProblem({...input,presentation:artifact({readBack:false})}),/全部页面/);
 assert.match(deliveryProblem({...input,presentation:artifact({qualityErrors:['outside_canvas']})}),/结构问题/);
 assert.match(deliveryProblem({...input,renderingRequired:true}),/尚未覆盖全部渲染页面/);
 assert.equal(deliveryProblem({...input,renderingRequired:true,renderingComplete:true}),'');
});
test('business: targeted PPT edits must preserve a readable final structure',()=>{
 const requirement={...delivery().requirement,requiresDocumentArtifact:false,requiresTargetedEdit:true,requiresFullSourceCoverage:false};
 assert.match(deliveryProblem(delivery({requirement,edit:null})),/定点改稿/);
 assert.match(deliveryProblem(delivery({requirement,edit:artifact({qualityErrors:['outside_canvas']})})),/结构问题/);
 assert.equal(deliveryProblem(delivery({requirement,edit:artifact()})),'');
});
test('business: the standalone pure worker answers a real stdin request with no model credentials',async()=>{
 const child=spawn(process.execPath,[new URL('../src/business-worker.ts',import.meta.url).pathname.replace(/^\/([A-Za-z]:)/,'$1')],{stdio:['pipe','pipe','pipe'],windowsHide:true});
 let output='',errors='';child.stdout.on('data',data=>output+=data);child.stderr.on('data',data=>errors+=data);
 child.stdin.write(JSON.stringify({version:1,operation:'plan',input:input('生成8页PPT')})+'\n');
 const exit=await new Promise((resolve,reject)=>{child.on('exit',resolve);child.on('error',reject);});assert.equal(exit,0,errors);const result=JSON.parse(output);assert.equal(result.ok,true);assert.equal(result.value.language,'TypeScript');assert.equal(result.value.contract.RequestedPages,8);
});

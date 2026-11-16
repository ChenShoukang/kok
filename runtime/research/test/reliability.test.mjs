import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { baseline } from '../../../tools/reliability-baseline.mjs';
import { planTask } from '../src/business/planning.ts';
import { progressDecision } from '../src/business/progress.ts';
import { projectRepeatedReceipts, orderToolResults } from '../src/receipts.ts';
import { ResearchKernel, CheckpointStore } from '../src/kernel.ts';

test('18 frozen planning scenarios respect deliverable cancellation, replacement and units', () => {
  const report = baseline();
  assert.equal(report.total, 18); assert.equal(report.requests, 0);
  assert.deepEqual(report.cases.filter(row => !row.passed), []);
});

test('cancellation removes automatic creation tools while an explicit list remains authoritative', () => {
  const input={goal:'写DOCX报告和8页PPT',adjustments:['不做PPT了，只交付报告'],allowed:[],intent:'assist',approvalPolicy:'each',scope:{mode:'all'},explicitTools:null,
    features:{},environment:{workspace:false,rendering:false,tools:['create_document','create_presentation','copy_presentation','read_presentation','read_document'],selectedFormats:[]},context:{files:[]}};
  const plan=planTask(input);assert(plan.tools.includes('create_document'));assert(!plan.tools.includes('create_presentation'));assert(!plan.tools.includes('copy_presentation'));
  assert.deepEqual(planTask({...input,explicitTools:['create_presentation']}).tools,['create_presentation']);
  const keep=planTask({...input,adjustments:['不要取消报告，只需继续检查资料']});assert(keep.contract.RequiresDocument);
  for(const adjustment of ['不要删除报告，PPT也保留','报告不要改写，PPT无需重做','无需重新生成报告，保留已有文件']){
    const preserved=planTask({...input,adjustments:[adjustment]});assert(preserved.contract.RequiresDocument,adjustment);assert(preserved.contract.RequiresPresentation,adjustment);
  }
});

test('progress guard uses exact evidence and distinct turns, with exemptions for jobs and UI', () => {
  const row=(turn,changes={})=>({tool:'read_items',fingerprint:'same-current-version',current:true,turnId:turn,...changes});
  assert.equal(progressDecision({calls:[row('1'),row('2')]}).action,'warn');
  assert.equal(progressDecision({calls:[row('1'),row('2'),row('3')]}).action,'stop');
  assert.equal(progressDecision({calls:[row('1'),row('1'),row('1')]}).action,'continue');
  for(const change of [{fingerprint:'next-offset'},{fingerprint:'new-source-version'},{current:false},{tool:'wait_command_job'},{tool:'read_command_job'},{tool:'read_browser'},{tool:'create_document'}])
    assert.equal(progressDecision({calls:[row('1'),row('2'),row('3',change)]}).action,'continue',JSON.stringify(change));
});

test('only identical successful native results are projected; latest full result and checkpoint bytes remain', () => {
  const text='Complete native source with middle restrictions and final revision. '.repeat(60);
  const receipt=(id,changes={})=>({role:'tool',tool_call_id:id,content:JSON.stringify({kok_call_id:id,status:'done',error:'',errorKind:'',result:{text,version:'source-v1',has_more:false},hostReadBacks:[],...changes})});
  const a=receipt('a'.repeat(32)),b=receipt('b'.repeat(32)),original=a.content;
  const result=projectRepeatedReceipts([a,{role:'user',content:'Keep the final budget.'},b],true);
  assert.equal(result.duplicates,1);assert(result.savedCharacters>2000);assert.match(result.messages[0].content,/identicalResultAt/);assert.equal(result.messages[2].content,b.content);assert.equal(a.content,original);
  for(const newer of [receipt('b'.repeat(32),{status:'failed',error:'No file'}),receipt('b'.repeat(32),{result:{text,version:'source-v2'}}),receipt('b'.repeat(32),{result:{text:text+'next',version:'source-v1'}})])
    assert.equal(projectRepeatedReceipts([a,newer],true).duplicates,0);
  assert.equal(projectRepeatedReceipts([a,b],false).duplicates,0);
});

test('actual graph projects repeated native receipts only at the model boundary across a restart', async () => {
  const requests=[];
  const server=createServer(async(req,res)=>{let body='';for await(const part of req)body+=part;requests.push(JSON.parse(body));
    const tool=requests.length<3,delta=tool?{role:'assistant',tool_calls:[{index:0,id:'read-'+requests.length,type:'function',function:{name:'read_items',arguments:'{"ids":["a"]}'}}]}:{role:'assistant',content:'Saved results verified.'};
    res.writeHead(200,{'content-type':'text/event-stream'});res.end('data: '+JSON.stringify({id:'fixture-'+requests.length,object:'chat.completion.chunk',model:'receipt-fixture',choices:[{index:0,delta,finish_reason:tool?'tool_calls':'stop'}],usage:{prompt_tokens:100,completion_tokens:20}})+'\n\ndata: [DONE]\n\n');
  });await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const spec=name=>({type:'function',function:{name,description:'Native receipt tool',parameters:{type:'object',properties:{ids:{type:'array',items:{type:'string'}}},required:[]}}});
  const input={taskId:'receipt-projection-restart',endpoint:'http://127.0.0.1:'+server.address().port+'/v1',apiKey:'loopback',model:'receipt-fixture',systemPrompt:'Read current evidence.',tools:[spec('read_items'),spec('recall_result')],messages:[{role:'user',content:'Read source a.'}]};
  const text='EXACT_SOURCE_MARKER '+ 'Read the final revision and preserve the middle restriction. '.repeat(40);
  try{
  let result=await new ResearchKernel().step(input);
  for(const id of ['a'.repeat(32),'b'.repeat(32)]){
    result=await new ResearchKernel({snapshot:result.snapshot}).step({...input,messages:[],resume:{[result.calls[0].interruptId]:{kok_call_id:id,status:'done',result:{text,version:'v1'}}}});
  }
  assert.equal(result.kind,'final');assert.equal(requests.length,3);
  const toolMessages=requests[2].messages.filter(message=>message.role==='tool');assert.equal(toolMessages.length,2);
  assert.match(toolMessages[0].content,/identicalResultAt/);assert(toolMessages[1].content.includes('EXACT_SOURCE_MARKER'));
  const saver=await CheckpointStore.restore(result.snapshot),saved=await saver.getTuple({configurable:{thread_id:input.taskId}});
  const originals=saved.checkpoint.channel_values.messages.filter(message=>message.getType()==='tool');assert.equal(originals.length,2);assert(originals.every(message=>message.content.includes('EXACT_SOURCE_MARKER')),'both original full results must remain in the public checkpoint');
  }finally{server.closeAllConnections();await new Promise(resolve=>server.close(resolve));}
});

test('steering can retire a pending tool and still deliver its result before the user update', async () => {
  const requests=[];
  const server=createServer(async(req,res)=>{let body='';for await(const part of req)body+=part;requests.push(JSON.parse(body));
    const tool=requests.length===1,delta=tool?{role:'assistant',tool_calls:[{index:0,id:'old-proposal',type:'function',function:{name:'create_document',arguments:'{"text":"draft"}'}}]}:{role:'assistant',content:'Old proposal cancelled; new requirements followed.'};
    res.writeHead(200,{'content-type':'text/event-stream'});res.end('data: '+JSON.stringify({id:'steer-'+requests.length,object:'chat.completion.chunk',model:'steering-fixture',choices:[{index:0,delta,finish_reason:tool?'tool_calls':'stop'}],usage:{prompt_tokens:100,completion_tokens:20}})+'\n\ndata: [DONE]\n\n');
  });await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const spec={type:'function',function:{name:'create_document',description:'Create a native file',parameters:{type:'object',properties:{text:{type:'string'}},required:['text']}}};
  const input={taskId:'retired-native-tool',endpoint:'http://127.0.0.1:'+server.address().port+'/v1',apiKey:'loopback',model:'steering-fixture',systemPrompt:'Follow current requirements.',tools:[spec],messages:[{role:'user',content:'Create a document.'}]};
  try{
    const first=await new ResearchKernel().step(input);
    const next=await new ResearchKernel({snapshot:first.snapshot}).step({...input,tools:[],resumeTools:[spec],messages:[{role:'user',content:'Cancel the document; answer in chat.'}],resume:{[first.calls[0].interruptId]:{status:'skipped',error:'superseded'}}});
    assert.equal(next.kind,'final');assert.equal(requests.length,2);
    const messages=requests[1].messages,index=messages.findIndex(message=>message.tool_calls?.some(call=>call.id==='old-proposal'));
    assert.equal(messages[index+1].role,'tool');assert.equal(messages[index+1].tool_call_id,'old-proposal');assert(messages.slice(index+2).some(message=>message.role==='user'&&message.content.includes('Cancel')));
    assert(!requests[1].tools?.some(tool=>tool.function.name==='create_document'));
  }finally{server.closeAllConnections();await new Promise(resolve=>server.close(resolve));}
  assert.throws(()=>orderToolResults([{role:'assistant',tool_calls:[{id:'missing'}]},{role:'user',content:'Continue'}]),/incomplete/);
});

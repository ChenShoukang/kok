'use strict';
// Local OpenAI-compatible model fixture for Agent tests. Requests that carry `tools` get native function calls;
// the compatible JSON protocol is used otherwise. Tests describe each model turn once as {reply,actions,done}.
function text(request){return request.messages.map(m=>{const content=typeof m.content==='string'?m.content:'';if(content.startsWith('[Untrusted file evidence]\n')){try{const evidence=JSON.parse(content.slice(content.indexOf('\n')+1));return content+'\n'+(evidence.context||'');}catch{}}return content;}).join('\n');}
// Whether an earlier round already called `tool` (assistant tool_calls natively, `[tool ...]` evidence in JSON mode).
function called(request,tool){return request.tools?request.messages.some(m=>m.role==='assistant'&&(m.tool_calls||[]).some(c=>c.function.name===tool)):text(request).includes('['+tool);}
function toolResults(request){return request.messages.filter(m=>m.role==='tool');}
let next=0;
// Fixture authors specify domain actions. The mock model then reviews actual host receipts using
// the same task-control protocol as a real model. Production completion rules are tested separately.
function outcomes(request){
 if(request.tools){const calls=new Map(request.messages.flatMap(m=>m.tool_calls||[]).map(c=>[c.id,c]));return toolResults(request).flatMap(m=>{const receipt=/^\[kok_call_id=([a-f0-9]{32}) status=(\w+)\]/.exec(m.content||''),call=calls.get(m.tool_call_id);if(!receipt||!call)return[];let args;try{args=JSON.parse(call.function.arguments);}catch{return[];}delete args.why;return[{id:receipt[1],status:receipt[2],tool:call.function.name,args}];});}
 return [...text(request).matchAll(/^\[([a-z_]+) (.+) callId=([a-f0-9]{32}) status=(\w+)\]$/gm)].flatMap(m=>{try{return[{tool:m[1],args:JSON.parse(m[2]),id:m[3],status:m[4]}];}catch{return[];}});
}
function complete(request,turn){
 const control=tool=>['update_plan','finish_task'].includes(tool),records=outcomes(request).filter(c=>!control(c.tool));
 if((turn.actions||[]).some(a=>control(a.tool)))return turn;
 const repeated=(turn.actions||[]).length>0&&turn.actions.every(a=>!['read_items','search_library'].includes(a.tool)&&records.some(c=>c.tool===a.tool&&JSON.stringify(c.args)===JSON.stringify(a.args||{})));
 if((turn.actions||[]).length&&!repeated)return turn;
 const evidence=records.filter(c=>c.status==='done').map(c=>c.id).slice(-12),partial=records.some(c=>c.status!=='done');
 const plan=records.length>1&&!partial?[{tool:'update_plan',args:{steps:[{id:'review',title:'Review completed fixture steps',status:'done',evidence}]}}]:[];
 return {...turn,done:true,actions:[...plan,{tool:'finish_task',args:{status:partial?'partial':'completed',summary:turn.reply||'Fixture task reviewed',checks:evidence.length?[{claim:'Reviewed actual host receipts',evidence}]:[]}}]};
}
function message(request,turn){
 turn=complete(request,turn);
 if(!request.tools)return {content:JSON.stringify(turn)};
 const calls=(turn.actions||[]).map(a=>({id:'call_fixture_'+(++next),type:'function',function:{name:a.tool,arguments:JSON.stringify({...(a.args||{}),...(a.why?{why:a.why}:{})})}}));
 return calls.length?{role:'assistant',content:turn.reply||'',tool_calls:calls}:{role:'assistant',content:turn.reply||''};
}
function payload(request,turn,usage){const m=message(request,turn);return JSON.stringify({choices:[{message:m,finish_reason:m.tool_calls?'tool_calls':'stop'}],...(usage?{usage}:{})});}
// Native function calling with a tool the registry does not know; the app answers it with an error tool result.
function invalid(request){return request.tools?payload(request,{reply:'',actions:[{tool:'delete_file',args:{}}]}):JSON.stringify({choices:[{message:{content:'this is not json'},finish_reason:'stop'}]});}
module.exports={text,called,toolResults,outcomes,complete,message,payload,invalid};

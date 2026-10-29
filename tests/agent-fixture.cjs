'use strict';
// Local OpenAI-compatible model fixture for Agent tests. Requests that carry `tools` get native function calls;
// the compatible JSON protocol is used otherwise. Tests describe each model turn once as {reply,actions,done}.
function text(request){return request.messages.map(m=>typeof m.content==='string'?m.content:'').join('\n');}
// Whether an earlier round already called `tool` (assistant tool_calls natively, `[tool ...]` evidence in JSON mode).
function called(request,tool){return request.tools?request.messages.some(m=>m.role==='assistant'&&(m.tool_calls||[]).some(c=>c.function.name===tool)):text(request).includes('['+tool);}
function toolResults(request){return request.messages.filter(m=>m.role==='tool');}
let next=0;
function message(request,turn){
 if(!request.tools)return {content:JSON.stringify(turn)};
 const calls=(turn.actions||[]).map(a=>({id:'call_fixture_'+(++next),type:'function',function:{name:a.tool,arguments:JSON.stringify({...(a.args||{}),...(a.why?{why:a.why}:{})})}}));
 return calls.length?{role:'assistant',content:turn.reply||'',tool_calls:calls}:{role:'assistant',content:turn.reply||''};
}
function payload(request,turn,usage){const m=message(request,turn);return JSON.stringify({choices:[{message:m,finish_reason:m.tool_calls?'tool_calls':'stop'}],...(usage?{usage}:{})});}
// Native function calling with a tool the registry does not know; the app answers it with an error tool result.
function invalid(request){return request.tools?payload(request,{reply:'',actions:[{tool:'delete_file',args:{}}]}):JSON.stringify({choices:[{message:{content:'this is not json'},finish_reason:'stop'}]});}
module.exports={text,called,toolResults,message,payload,invalid};

'use strict';
// A real child-process MCP server. No filesystem tools, sockets or credentials.
const readline=require('node:readline');
const mode=process.argv[2]||'modern';let initialized=false,lists=0,calls=0,denied=0,workerPid=0;
if(mode==='orphan')setInterval(()=>{},1000);
const send=value=>process.stdout.write(JSON.stringify(value)+'\n');
const result=(id,data)=>send({jsonrpc:'2.0',id,result:{...(mode.startsWith('legacy')?{}:{resultType:'complete'}),...data}});
const error=(id,code,data)=>send({jsonrpc:'2.0',id,error:{code,message:'fixture error',...(data?{data}:{})}});
const tools=()=>[
 {name:'echo',description:mode==='changed'&&lists>1?'changed definition':'Synthetic echo',inputSchema:{type:'object',properties:{text:{type:'string',maxLength:200}},required:['text'],additionalProperties:false},annotations:{readOnlyHint:true}},
 {name:'counter',description:'Synthetic call count',inputSchema:{type:'object',properties:{},additionalProperties:false}},
 {name:'complex',description:'Unsupported schema stays blocked',inputSchema:{type:'object',properties:{value:{$ref:'https://not-requested.invalid/schema'}}}}
];
readline.createInterface({input:process.stdin}).on('line',line=>{
 const request=JSON.parse(line);if(request.id==='server-question'&&request.error){denied++;return;}
 if(!request.method)return;
 if(request.method==='server/discover'){
  if(mode==='tree'&&!workerPid)workerPid=require('node:child_process').spawn(process.execPath,[__filename,'orphan'],{stdio:'ignore',windowsHide:true}).pid;
  if(mode==='legacy-silent')return;
  if(mode.startsWith('legacy')){error(request.id,-32000);return;}
  if(mode==='wrong-version'){error(request.id,-32022,{supported:['2099-01-01']});return;}
  const meta=request.params?._meta;if(meta?.['io.modelcontextprotocol/protocolVersion']!=='2026-07-28'){error(request.id,-32602);return;}
  result(request.id,{supportedVersions:['2026-07-28'],capabilities:{tools:{}},ttlMs:1000,cacheScope:'private'});return;
 }
 if(request.method==='initialize'){if(!mode.startsWith('legacy'))throw Error('Unexpected legacy fallback');initialized=true;result(request.id,{protocolVersion:'2025-11-25',capabilities:{tools:{}},serverInfo:{name:'fixture',version:'1'}});return;}
 if(request.method==='notifications/initialized'){if(mode==='legacy-callback')send({jsonrpc:'2.0',id:'server-question',method:'sampling/createMessage',params:{messages:[]}});return;}
 if(request.method==='notifications/cancelled')return;
 if(mode.startsWith('legacy')&&!initialized){error(request.id,-32000);return;}
 if(!mode.startsWith('legacy')&&!request.params?._meta?.['io.modelcontextprotocol/protocolVersion']){error(request.id,-32602);return;}
 if(request.method==='tools/list'){lists++;if(mode==='metadata-error'&&lists>1){error(request.id,-32602);return;}result(request.id,{tools:tools(),workerPid});return;}
 if(request.method==='tools/call'){
  calls++;
  if(mode==='error'||request.params.arguments.text==='FAIL_TOOL'){result(request.id,{isError:true,content:[{type:'text',text:'FIXTURE_TOOL_ERROR: choose a different input.'}]});return;}
  if(mode==='hang')return;
  if(mode==='oversize'){process.stdout.write('x'.repeat(1048580)+'\n');return;}
  if(mode==='interactive'){send({jsonrpc:'2.0',id:request.id,result:{resultType:'input_required',requests:[]}});return;}
  result(request.id,{content:[{type:'text',text:JSON.stringify({echo:request.params.arguments.text||'',calls,denied,allowed:process.env.KOK_MCP_ALLOWED||'',inherited:!!process.env.KOK_MCP_SECRET_CANARY})}]});return;
 }
 error(request.id,-32601);
});

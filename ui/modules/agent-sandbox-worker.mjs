import {newQuickJSWASMModuleFromVariant} from '../vendor/quickjs/core/index.mjs';
import variant from '../vendor/quickjs/variant/index.mjs';
// The outer worker is trusted. Submitted code only executes in the inner WASM VM; no host imports,
// file APIs or network APIs are installed there. The vendored QuickJS build is synchronous, so read-only
// library tools use a replay cache: a missing result aborts the round (sentinel), the renderer fetches it
// from the native host, and the same code re-runs from scratch with the cached results.
const TOOLS=['search_library','read_items','recall_result','search_memory','list_collections'];
const PENDING='__KOK_TOOL_PENDING__',MAX_CALLS=24,PER_EVAL_MS=1200,TOTAL_MS=7000;
let startedAt=0;
self.addEventListener('message',async event=>{
 const {code,input,cache}=event.data;let vm,runtime,result;
 if(!startedAt)startedAt=Date.now();
 try{
  if(typeof code!=='string'||code.length>8000||typeof input!=='string'||input.length>16000)throw Error('Input limit');
  if(!Array.isArray(cache)||cache.length>MAX_CALLS)throw Error('Bad tool cache');
  JSON.parse(input);if(Date.now()-startedAt>TOTAL_MS)throw Error('Worker time limit');
  const QuickJS=await newQuickJSWASMModuleFromVariant(variant);
  runtime=QuickJS.newRuntime();runtime.setMemoryLimit(16*1024*1024);runtime.setMaxStackSize(256*1024);
  const deadline=Math.min(Date.now()+PER_EVAL_MS,startedAt+TOTAL_MS);runtime.setInterruptHandler(()=>Date.now()>deadline);
  vm=runtime.newContext();
  result=vm.evalCode(`Object.defineProperty(globalThis,'INPUT',{value:JSON.parse(${JSON.stringify(input)}),writable:false});`+toolsPrelude(cache)+`JSON.stringify((()=>{const value=(${code});return value===undefined?null:value;})())`,'isolated-computation.js',{type:'global'});
  if(result.error){const error=vm.dump(result.error);throw Error(String(error?.message||error));}
  const output=vm.getString(result.value);if(output.length>16000)throw Error('Output limit (16000 characters)');postMessage({ok:true,output});
 }catch(error){
  const message=String(error?.message||error).slice(0,16000);
  if(message.includes(PENDING))postMessage({ok:false,pending:readPending(vm),cacheLength:Array.isArray(cache)?cache.length:0});
  else postMessage({ok:false,output:message});
 }
 finally{if(result?.error)result.error.dispose();else result?.value?.dispose();vm?.dispose();runtime?.dispose();}
});
// Defines the five read-only host functions inside the VM. A cached result is returned synchronously;
// a missing one is recorded on globalThis.__kokPending before throwing the abort sentinel.
function toolsPrelude(cache){
 const cacheSource=JSON.stringify((cache||[]).map(entry=>entry&&entry.error!=null?{e:String(entry.error)}:{v:entry?entry.value:null}));
 const functions=TOOLS.map(name=>JSON.stringify(name)+':function(args){'+
  'var index=__kokCache.length+__kokPending.length;'+
  'if(index<__kokCache.length){var cached=__kokCache[index];if(cached.e!=null)throw new Error(cached.e);return cached.v;}'+
  'if(index>='+MAX_CALLS+')throw new Error("Tool call limit ('+MAX_CALLS+')");'+
  'var safeArgs;try{safeArgs=args===undefined?{}:JSON.parse(JSON.stringify(args));}catch(error){throw new Error("Tool arguments must be JSON-serializable");}'+
  'if(safeArgs===null||typeof safeArgs!=="object"||Array.isArray(safeArgs))throw new Error("Tool arguments must be a plain object");'+
  '__kokPending.push({index:index,name:'+JSON.stringify(name)+',args:safeArgs});throw new Error("'+PENDING+'");'+
 '}').join(',');
 return `Object.defineProperty(globalThis,'__kokPending',{value:[],writable:false});var __kokCache=${cacheSource};globalThis.tools={${functions}};`;
}
function readPending(vm){
 if(!vm)return[];
 try{const read=vm.evalCode('JSON.stringify(globalThis.__kokPending||[])');if(read.error){read.error.dispose();return[];}const parsed=JSON.parse(vm.getString(read.value));read.value.dispose();return Array.isArray(parsed)?parsed.slice(0,MAX_CALLS):[];}catch{return[];}
}

import {newQuickJSWASMModuleFromVariant} from '../vendor/quickjs/core/index.mjs';
import variant from '../vendor/quickjs/variant/index.mjs';
// This outer worker is trusted. Submitted code only executes in the inner WASM VM;
// no host callbacks, imports, file APIs or network APIs are installed there.
self.addEventListener('message',async event=>{
 const {code,input}=event.data;let vm,runtime,result;
 try{
  if(typeof code!=='string'||code.length>8000||typeof input!=='string'||input.length>16000)throw Error('Input limit');
  JSON.parse(input);const QuickJS=await newQuickJSWASMModuleFromVariant(variant);
  runtime=QuickJS.newRuntime();runtime.setMemoryLimit(16*1024*1024);runtime.setMaxStackSize(256*1024);const deadline=Date.now()+1200;runtime.setInterruptHandler(()=>Date.now()>deadline);vm=runtime.newContext();
  result=vm.evalCode(`Object.defineProperty(globalThis,'INPUT',{value:JSON.parse(${JSON.stringify(input)}),writable:false});JSON.stringify((()=>{const value=(${code});return value===undefined?null:value;})())`,'isolated-computation.js',{type:'global'});
  if(result.error){const error=vm.dump(result.error);throw Error(String(error?.message||error));}
  const output=vm.getString(result.value);if(output.length>16000)throw Error('Output limit (16000 characters)');postMessage({ok:true,output});
 }catch(error){postMessage({ok:false,output:String(error.message||error).slice(0,16000)});}
 finally{if(result?.error)result.error.dispose();else result?.value?.dispose();vm?.dispose();runtime?.dispose();}
});

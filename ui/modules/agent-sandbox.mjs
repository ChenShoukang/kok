// Renderer side of the isolated run_code sandbox. The worker never gets host callbacks: when the
// synchronous QuickJS VM needs a read-only library tool it aborts the round with a pending list, this
// module asks the native host for the results (agentSandboxTools), then re-runs the code with the cache.
const active=new Map(),MAX_ROUNDS=6,TOTAL_MS=7000;
export function stopSandbox(ticket=''){
 for(const [key,task]of active)if(!ticket||ticket===key){try{task.worker.terminate();}catch{}clearTimeout(task.timer);active.delete(key);task.resolve({ok:false,output:'Cancelled'});}
}
export function runSandbox(code,input,requestedTicket){
 const ticket=requestedTicket||crypto.randomUUID(),worker=new Worker(new URL('./agent-sandbox-worker.mjs',import.meta.url),{type:'module'});
 return new Promise(resolve=>{
  const state={cache:[],rounds:0};
  const finish=value=>{const task=active.get(ticket);if(!task)return;try{task.worker.terminate();}catch{}clearTimeout(task.timer);active.delete(ticket);resolve(value);};
  const timer=setTimeout(()=>finish({ok:false,output:'Worker time limit'}),TOTAL_MS);
  active.set(ticket,{worker,resolve,timer});
  worker.onmessage=async event=>{
   if(!active.has(ticket))return;
   const data=event.data;
   if(data&&data.ok){finish(data);return;}
   if(!data||!Array.isArray(data.pending)){finish({ok:false,output:String(data&&data.output||'Sandbox failed').slice(0,16000)});return;}
   if(++state.rounds>MAX_ROUNDS){finish({ok:false,output:'Replay round limit ('+MAX_ROUNDS+')'});return;}
   try{
    const response=await native('agentSandboxTools',{ticket,requests:data.pending});
    if(!active.has(ticket))return; // a stale ticket must never apply tool results
    const results=Array.isArray(response&&response.results)?response.results:[];
    if(results.length!==data.pending.length){finish({ok:false,output:'Tool result count mismatch'});return;}
    const ordered=results.slice().sort((a,b)=>Number(a&&a.index)-Number(b&&b.index));
    let expected=state.cache.length;
    for(const row of ordered){
     const index=Number(row&&row.index);
     if(!Number.isInteger(index)||index!==expected){finish({ok:false,output:'Tool result index mismatch'});return;}
     const text=String(row.text??'').slice(0,8000);let entry;
     if(row.isError)entry={error:text};else{try{entry={value:JSON.parse(text)};}catch{entry={value:text};}}
     state.cache.push(entry);expected++;
    }
    worker.postMessage({code,input,cache:state.cache});
   }catch(error){finish({ok:false,output:String(error&&error.message||error).slice(0,16000)});}
  };
  worker.onerror=()=>finish({ok:false,output:'Sandbox initialization failed'});
  worker.postMessage({code,input,cache:[]});
 });
}
const nativeTickets=new Map();
Kok.on('agentSandboxRun',async data=>{
 if(new URL(location.href).searchParams.has('preview')||state.isPrivate||!isSmartMode()||!Kok.enabled('agent'))return;
 nativeTickets.set(data.ticket,true);const result=await runSandbox(data.code,data.input,data.ticket);
 if(!nativeTickets.has(data.ticket))return;nativeTickets.delete(data.ticket);
 if(!state.isPrivate&&isSmartMode()&&Kok.enabled('agent'))await native('agentSandboxResult',{ticket:data.ticket,...result});
},{module:'agent'});
Kok.on('agentSandboxCancel',data=>{if(data.ticket)nativeTickets.delete(data.ticket);else nativeTickets.clear();stopSandbox(data.ticket||'');});
Kok.on('librarySwitched',()=>{nativeTickets.clear();stopSandbox();});

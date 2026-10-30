const active=new Map();
export function stopSandbox(ticket=''){
 for(const [key,task]of active)if(!ticket||ticket===key){task.worker.terminate();clearTimeout(task.timer);active.delete(key);task.resolve({ok:false,output:'Cancelled'});}
}
export function runSandbox(code,input,requestedTicket){
 const ticket=requestedTicket||crypto.randomUUID(),worker=new Worker(new URL('./agent-sandbox-worker.mjs',import.meta.url),{type:'module'});
 return new Promise(resolve=>{const finish=value=>{const task=active.get(ticket);if(!task)return;worker.terminate();clearTimeout(task.timer);active.delete(ticket);resolve(value);};active.set(ticket,{worker,resolve,timer:setTimeout(()=>finish({ok:false,output:'Worker time limit'}),7000)});worker.onmessage=e=>finish(e.data);worker.onerror=()=>finish({ok:false,output:'Sandbox initialization failed'});worker.postMessage({code,input});});
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

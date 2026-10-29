'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
(async()=>{
 const requests=[],events=new Map(),timers=new Map();let sequence=0,enabled=true,rendered=0;
 const context={Map,Set,Promise,state:{query:''},Kok:{enabled:()=>enabled,on:(name,fn)=>events.set(name,fn)},document:{addEventListener(){}},native:(action,args)=>new Promise((resolve,reject)=>requests.push({action,args,resolve,reject})),render:()=>rendered++,fail:error=>{throw error;},setTimeout:fn=>{timers.set(++sequence,fn);return sequence;},clearTimeout:id=>timers.delete(id)};
 vm.runInNewContext(fs.readFileSync('ui/search-index.js','utf8')+';globalThis.index=SearchIndex;',context);const index=context.index;
 const first=index.load(' 正文 '),duplicate=index.load('正文');assert.equal(requests.length,1);requests[0].resolve(['file-a']);assert((await first).has('file-a'));assert((await duplicate).has('file-a'));await index.load('正文');assert.equal(requests.length,1);
 const stale=index.load('scope');index.clear();const fresh=index.load('scope');requests[1].resolve(['previous-library']);assert.equal((await stale).size,0);const shared=index.load('scope');assert.equal(requests.length,3,'A late request must not remove the new in-flight request');requests[2].resolve(['current-library']);assert((await fresh).has('current-library'));assert((await shared).has('current-library'));assert(!index.has('previous-library','scope'));
 index.schedule('private-pending');events.get('privateLocking')();assert.equal(timers.size,0);assert.equal(index.cache.size,0);assert.equal(rendered,0);
 const failed=index.load('retry');requests[3].reject(Error('fixture failure'));await assert.rejects(failed);const retried=index.load('retry');assert.equal(requests.length,5);requests[4].resolve(['retried']);assert((await retried).has('retried'));
 context.state.query='active';events.get('libraryChanged')();assert.equal(timers.size,1);const timer=[...timers.values()][0];timers.clear();timer();requests[5].resolve(['updated']);await new Promise(resolve=>setImmediate(resolve));assert.equal(rendered,1);assert(index.has('updated','active'));
 context.state.query='';index.indexTimer=context.setTimeout(()=>{},400);events.get('libraryChanged')();assert(timers.has(index.indexTimer),'Metadata changes must not cancel pending imported-content indexing');events.get('privateLocking')();assert.equal(timers.size,0);
 enabled=false;assert.equal((await index.load('disabled')).size,0);assert.equal(requests.length,6);
 console.log('PASS: search requests coalesce, late results cannot cross library scopes, lock cancels timers, failures retry, live queries refresh and disabled modules make no native request.');
})().catch(error=>{console.error(error);process.exitCode=1;});

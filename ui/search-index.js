'use strict';
const SearchIndex={
 cache:new Map(),inFlight:new Map(),epoch:0,
 key(query){return String(query||'').trim().toLocaleLowerCase();},
 has(id,query){return this.cache.get(this.key(query))?.has(id)||false;},
 async load(query){const key=this.key(query);if(!key||!Kok.enabled('workspace'))return new Set();if(this.cache.has(key))return this.cache.get(key);if(this.inFlight.has(key))return this.inFlight.get(key);const epoch=this.epoch,request=(async()=>{const ids=new Set(await native('fullTextSearch',{query:key}));if(epoch!==this.epoch)return new Set();this.cache.set(key,ids);while(this.cache.size>20)this.cache.delete(this.cache.keys().next().value);return ids;})();this.inFlight.set(key,request);try{return await request;}finally{if(this.inFlight.get(key)===request)this.inFlight.delete(key);}},
 schedule(query){clearTimeout(this.timer);const epoch=this.epoch;this.timer=setTimeout(()=>this.load(query).then(()=>{if(epoch===this.epoch&&state.query===query)render();}).catch(error=>{if(epoch===this.epoch)fail(error);}),120);},
 clear(cancelIndex=false){this.epoch++;clearTimeout(this.timer);if(cancelIndex)clearTimeout(this.indexTimer);this.inFlight.clear();this.cache.clear();}
};
document.addEventListener('input',event=>{if(event.target.id==='search')SearchIndex.schedule(event.target.value);});
Kok.on('libraryChanged',()=>{SearchIndex.clear();if(state.query)SearchIndex.schedule(state.query);});Kok.on('privateLocking',()=>SearchIndex.clear(true));Kok.on('librarySwitched',()=>SearchIndex.clear(true));
Kok.on('imported',()=>{if(state.isPrivate||!Kok.enabled('workspace'))return;clearTimeout(SearchIndex.indexTimer);SearchIndex.indexTimer=setTimeout(()=>{if(!state.isPrivate&&Kok.enabled('workspace'))native('indexLibrary').catch(fail);},400);});

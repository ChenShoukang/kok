'use strict';
// Pi-inspired separation: durable transcript, bounded model context and transient UI state.
// No model is called to compress history. These are local excerpts, never a semantic summary.
const AgentModel=(()=>{
 const presets=[
  {name:'organize',title:'整理资料',description:'按主题归类并建议标签',instructions:'Search within the chosen scope. Read relevant files before grouping. Propose concise collections and tags; explain ambiguity. Never infer unread content.',match:/整理|归类|分类|标签|organize|classif|tag/i},
  {name:'compare',title:'对比资料',description:'对比观点与证据',instructions:'Read the selected sources. Compare agreements, differences and missing evidence. Name the source for each conclusion. Do not invent facts.',match:/对比|比较|compare|contrast/i},
  {name:'reading',title:'读书笔记',description:'保留出处与个人想法',instructions:'Read the sources. Separate source claims from the user notes. Propose a concise Markdown note with source names, key ideas and open questions.',match:/读书|笔记|reading|note/i},
  {name:'slides',title:'制作演示',description:'从资料生成简洁演示文稿',instructions:'Read the sources before proposing a presentation. Use one clear point per slide, concise bullets and source names. Never invent statistics.',match:/演示|幻灯|ppt|slide|presentation/i}
 ];
 function skill(name,goal,custom=[]){const all=[...presets,...custom];return name==='auto'?presets.find(s=>s.match.test(goal)):all.find(s=>s.name===name);}
 function context(records,budget){
  // Keep complete observations, bounded excerpts of older ones; never slice a JSON object midway.
  const rows=[];let left=budget;for(const r of records.slice().reverse()){
   const header=`[${r.tool} ${JSON.stringify(r.args).slice(0,300)}]\n`,text=String(r.text||'');
   if(left<header.length+80)break;const excerpt=text.length>left-header.length?text.slice(0,Math.max(0,left-header.length-32))+'\n[excerpt; locally shortened]':text;
   rows.unshift(header+excerpt);left-=header.length+excerpt.length+2;
  }return rows.join('\n\n');
 }
 function history(turns,budget=5000){const result=[];let left=budget,kept=0;
  const reserve=budget>=1000?Math.min(1000,Math.floor(budget/4)):0;left-=reserve;
  for(const t of turns.slice().reverse()){
   if(!['user','assistant'].includes(t.role))continue;const content=String(t.content||'');if(content.length>left)break;result.unshift({role:t.role,content});left-=content.length;kept++;if(result.length>=8)break;
  }
  const earlier=turns.slice(0,turns.length-kept).slice(-6);if(reserve&&earlier.length){
   let log='[Earlier task excerpts, locally shortened; incomplete]\n';for(const t of earlier){const line=t.role+': '+String(t.content||'').replace(/\s+/g,' ').slice(0,180)+'\n';if(log.length+line.length>reserve)break;log+=line;}
   result.unshift({role:'assistant',content:log});
  }return result;
 }
 function partialReply(raw){const m=/"reply"\s*:\s*"((?:[^"\\]|\\[\s\S])*)/.exec(raw);if(!m)return '';let text=m[1].replace(/\\(?:u[0-9a-f]{0,3})?$/i,'');try{return JSON.parse('"'+text+'"').slice(0,4000);}catch{return '';}}
 function storedMessages(messages){return messages.filter(m=>m.role!=='thinking').slice(-80).map(m=>{
  const copy=JSON.parse(JSON.stringify(m,(key,value)=>key==='undo'?undefined:value));
  if(['running','queued','pending'].includes(copy.status)){copy.status='skipped';copy.error='';}return copy;
 });}
 function groups(messages){
  const result=[];for(const m of messages){const read=m.role==='tool'&&['search_library','read_items','list_collections'].includes(m.tool);
   const previous=result.at(-1);if(read&&previous?.kind==='reads')previous.items.push(m);else result.push({kind:read?'reads':'message',items:[m]});
  }return result;
 }
 return{presets,skill,context,history,partialReply,storedMessages,groups};
})();
if(typeof module!=='undefined')module.exports=AgentModel;

'use strict';
// Pure scope selection: filter real files first, then derive boundary nodes.
// Hidden files never connect two visible files through a filtered-out shortcut.
const KnowledgeScope=(()=>{
 const key=v=>String(v||'').normalize('NFKC').toLocaleLowerCase().trim();
 function select(model,options,items){
  const entries=new Map(items.map(i=>[i.id,i])),ids=new Set(options.ids||[]),query=key(options.filter),from=options.from||'',to=options.to||'';
  let files=model.nodes.filter(n=>!n.missing&&n.kind!=='tag').filter(n=>{
   const item=entries.get(n.id);if(!item)return false;
   const date=String(item.added||'').slice(0,10);
   return(!options.collection||n.collection===options.collection)&&(!options.kind||n.kind===options.kind)&&(!from||date>=from)&&(!to||date<=to)&&(!options.tag||(n.tags||[]).some(t=>key(t)===key(options.tag)))&&(!query||key(n.name+' '+n.collection+' '+(n.tags||[]).join(' ')+' '+(n.aliases||[]).join(' ')).includes(query))&&(options.scope!=='favorites'||item.favorite)&&(options.scope!=='manual'||ids.has(n.id));
  });
  const real=new Set(files.map(n=>n.id)),byId=new Map(model.nodes.map(n=>[n.id,n]));let links=model.links.filter(e=>{
   const a=byId.get(e.source),b=byId.get(e.target);return a&&b&&real.has(e.source)&&(real.has(e.target)||b.missing||options.showTags&&b.kind==='tag');
  });
  const active=new Set(links.flatMap(e=>[e.source,e.target]));
  files=files.filter(n=>n.eligible||active.has(n.id));
  if(options.showIsolated===false)files=files.filter(n=>active.has(n.id));
  const allowed=new Set(files.map(n=>n.id));for(const edge of links)if(allowed.has(edge.source)&&!real.has(edge.target))allowed.add(edge.target);
  if(options.focus&&options.depth>0){
   const adjacency=new Map([...allowed].map(id=>[id,new Set()]));for(const e of links)if(allowed.has(e.source)&&allowed.has(e.target)){adjacency.get(e.source).add(e.target);adjacency.get(e.target).add(e.source);}
   const near=new Set(allowed.has(options.focus)?[options.focus]:[]);let frontier=[...near];
   for(let layer=0;layer<Math.min(5,options.depth);layer++){const next=[];for(const id of frontier)for(const other of adjacency.get(id)||[])if(!near.has(other)){near.add(other);next.push(other);}frontier=next;}
   for(const id of [...allowed])if(!near.has(id))allowed.delete(id);
  }
  links=links.filter(e=>allowed.has(e.source)&&allowed.has(e.target));const degree=new Map();for(const e of links){degree.set(e.source,(degree.get(e.source)||0)+1);degree.set(e.target,(degree.get(e.target)||0)+1);}
  let nodes=model.nodes.filter(n=>allowed.has(n.id));nodes.sort((a,b)=>(a.id===options.selected?-1:b.id===options.selected?1:0)||(degree.get(b.id)||0)-(degree.get(a.id)||0)||a.name.localeCompare(b.name));
  const matched=nodes.length,limit=Math.max(25,Math.min(500,Number(options.limit)||250));nodes=nodes.slice(0,limit);const visible=new Set(nodes.map(n=>n.id));links=links.filter(e=>visible.has(e.source)&&visible.has(e.target));
  return{nodes,links,matched};
 }
 return{select};
})();
if(typeof module!=='undefined')module.exports=KnowledgeScope;

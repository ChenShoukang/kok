'use strict';
// One explicit shell contract. Modules never replace render/navigate/viewer functions.
const Kok=(()=>{
 const modules=new Map(),routes=new Map(),viewers=new Map(),events=new Map(),commands=new Map();let catalog=null,started=false,syncing=null;
 const bases=new Map(),extensions=new Map(),pipelines=new Map();
 function define(target,fn){if(bases.has(target)||typeof fn!=='function')throw Error('Duplicate or invalid shell service: '+target);bases.set(target,fn);pipelines.delete(target);}
 function snapshot(target){if(pipelines.has(target))return pipelines.get(target);let fn=bases.get(target);if(!fn)throw Error('Missing shell service: '+target);for(const extension of extensions.get(target)||[]){const previous=fn,wrapped=extension.build(previous);if(typeof wrapped!=='function')throw Error('Invalid shell extension: '+extension.id);fn=function(...args){return (enabled(extension.module)?wrapped:previous).apply(this,args);};}pipelines.set(target,fn);return fn;}
 function extend(module,id,target,build){if(!extensions.has(target))extensions.set(target,[]);const list=extensions.get(target);if(list.some(e=>e.id===id))throw Error('Duplicate shell extension: '+id);list.push({module,id,target,build});pipelines.delete(target);}
 function invoke(target,owner,args){return snapshot(target).apply(owner,args);}
 const enabled=id=>!catalog||catalog.modules.find(m=>m.id===id)?.enabled===true;
 function register(module){
  if(!module?.id||modules.has(module.id))throw Error('Duplicate or invalid module');
  if(catalog){const native=catalog.modules.find(m=>m.id===module.id);if(!native||!module.catalogActions&&JSON.stringify([...native.actions].sort())!==JSON.stringify([...(module.nativeActions||[])].sort()))throw Error('Module action contract mismatch: '+module.id);}
  const rr=Object.entries(module.routes||{}),vv=module.viewers||[];
  if(rr.some(([id,fn])=>routes.has(id)||typeof fn!=='function')||vv.some(v=>!v.id||viewers.has(v.id)||typeof v.accepts!=='function'||typeof v.open!=='function'))throw Error('Duplicate or invalid module handler');
  modules.set(module.id,module);rr.forEach(([id,fn])=>routes.set(id,{module:module.id,render:fn}));vv.forEach(v=>viewers.set(v.id,{...v,module:module.id}));
  for(const [id,fn] of Object.entries(module.handlers||{})){if(commands.has(id))throw Error('Duplicate command handler');commands.set(id,{module:module.id,run:fn});}
 }
 function renderRoute(route){const found=routes.get(route);if(!found)return false;if(!enabled(found.module)){state.route='overview';return false;}found.render();return true;}
 function beforeNavigate(route){for(const m of modules.values()){if(!enabled(m.id))continue;const answer=m.beforeNavigate?.(route);if(answer)return answer;}return {route};}
 function descriptors(key,context){return [...modules.values()].filter(m=>enabled(m.id)).flatMap(m=>(typeof m[key]==='function'?m[key](context):(m[key]||[])).filter(d=>!d.when||d.when(context)).map(d=>({...d,module:m.id}))).sort((a,b)=>(a.order||0)-(b.order||0));}
 function nav(){
  for(const place of ['main','extra']){const parent=$('#'+place+'-nav');if(!parent)continue;parent.innerHTML=descriptors('nav').filter(d=>(d.place||'main')===place&&(!state.isPrivate||d.allowPrivate)).map(d=>{const badge=d.badge?.(),count=d.count?.(),icon=d.mediaIcon?navigationIcon(d.icon):svg(d.icon);return `<button class="nav-item ${d.spaceId?(state.route==='spaces'&&Warehouse.activeSpace===d.spaceId?'active':''):(d.route===state.route?'active':'')} ${d.className||''}" ${d.spaceId?`data-space="${esc(d.spaceId)}" data-space-drop="${esc(d.spaceId)}"`:d.route?`data-route="${esc(d.route)}"`:d.command?`data-kok-command="${esc(d.command)}"`:''} ${d.id==='knowledge'?'data-open-knowledge':''} aria-label="${esc(d.userContent?d.label:I18n.text(d.label))}" ${d.userContent?'data-user-content':''}>${icon}<span class="nav-label">${esc(d.userContent?d.label:I18n.text(d.label))}</span>${badge?`<span class="count agent-busy" aria-label="${esc(I18n.text('正在处理'))}"></span>`:count?`<span class="count">${esc(count)}</span>`:''}</button>`;}).join('');}
 }

 function toolbar(context){return descriptors('cardActions',context).map(d=>`<button class="button secondary ${d.className||''}" type="button" data-kok-command="${esc(d.command)}" title="${esc(I18n.text(d.title||d.label))}">${svg(d.icon)}${esc(I18n.text(d.label))}</button>`).join('');}
 function validate(){
  for(const m of modules.values()){const native=catalog.modules.find(n=>n.id===m.id);if(!native)throw Error('UI module missing from native catalog: '+m.id);const declared=[...(m.nativeActions||[])].sort(),actual=[...native.actions].sort();if(!m.catalogActions&&JSON.stringify(declared)!==JSON.stringify(actual))throw Error('Module action contract mismatch: '+m.id);}
  for(const m of catalog.modules.filter(m=>m.ui))if(!modules.has(m.id))throw Error('Native module missing UI registration: '+m.id);
  for(const c of catalog.capabilities.filter(c=>c.uiCommand))if(!commands.has(c.uiCommand)||commands.get(c.uiCommand).module!==c.module)throw Error('Capability command handler mismatch: '+c.id);
 }
 async function sync(){if(syncing)return syncing;syncing=(async()=>{const previous=catalog;catalog=await native('moduleCatalog');validate();if(started)for(const m of modules.values()){const was=previous?.modules.find(n=>n.id===m.id)?.enabled;if(was&&!enabled(m.id))await m.dispose?.();if(was===false&&enabled(m.id))await m.start?.({startup:false});}emit('catalogChanged',catalog);return catalog;})();try{return await syncing;}finally{syncing=null;}}
 async function start(){await sync();for(const m of modules.values())if(enabled(m.id))await m.start?.({startup:true});started=true;}
 function settings(){for(const m of modules.values())if(enabled(m.id))m.settings?.();}
 async function openViewer(item,options){const v=[...viewers.values()].find(v=>v.accepts(item));if(!v)return false;if(!enabled(v.module)){$('#viewer-content').innerHTML=empty('模块已停用','请在设置中重新启用此模块。');return true;}await v.open(item,options);return true;}
 function on(name,fn,options={}){const row={fn,...options};if(!events.has(name))events.set(name,new Set());events.get(name).add(row);return()=>events.get(name)?.delete(row);}
 function emit(name,data){for(const row of [...(events.get(name)||[]),...(events.get('*')||[])]){if(row.module&&!enabled(row.module)||row.normalOnly&&state.isPrivate)continue;try{Promise.resolve(row.fn(data,name)).catch(fail);}catch(e){fail(e);}}}
 async function run(command){const c=catalog?.capabilities.find(c=>c.uiCommand===command),handler=commands.get(command);if(!c||!handler||!enabled(c.module)||!c.available||!matches(c.when,typeof Warehouse==='undefined'?{}:{count:Warehouse.selected.length,kind:byId(Warehouse.selected[0])?.kind}))throw Error(I18n.text('此功能当前不可用。'));return handler.run();}
 window.chrome?.webview?.addEventListener('message',event=>{const m=event.data;if(!m?.eventName)return;emit(m.eventName,m.data);if((['modulesChanged','librarySwitched'].includes(m.eventName)||m.eventName==='previewPreferencesChanged'&&m.data?.policyChanged)&&started){const update=async()=>{if(m.eventName==='librarySwitched'&&typeof vaultUi!=='undefined')await vaultUi.transition;await sync();renderNav();render();};if(m.eventName==='librarySwitched')setTimeout(()=>update().catch(fail),0);else update().catch(fail);}});
 document.addEventListener('click',event=>{const button=event.target.closest('[data-kok-command]');if(button){event.preventDefault();run(button.dataset.kokCommand).catch(fail);}},true);
 function applicable(surface,selection={}){return (catalog?.capabilities||[]).filter(c=>c.surfaces.includes(surface)&&c.available&&enabled(c.module)&&matches(c.when,selection));}
 function matches(condition,selection={}){if(!condition)return true;return condition.split('&&').every(clause=>{const m=clause.trim().match(/^(selection\.(count|kind)|mode)\s*(==|!=|>=|>|<=|<)\s*(?:'([^']*)'|"([^"]*)"|(\d+))$/);if(!m)return false;const left=m[1]==='mode'?(state.settings.appMode||'pure'):m[2]==='count'?(selection.count||0):selection.kind,right=m[6]===undefined?(m[4]??m[5]):Number(m[6]);return m[3]==='=='?left===right:m[3]==='!='?left!==right:m[3]==='>'?left>right:m[3]==='>='?left>=right:m[3]==='<'?left<right:left<=right;});}
 function inspector(item){return descriptors('inspector',item).map(d=>`<section><h3>${esc(I18n.text(d.label))}</h3>${d.render(item)}</section>`).join('');}
 function onMessage(fn,options={}){return on('*',(data,eventName)=>fn({data:{eventName,data}}),options);}
 function cards(item){return [...modules.values()].filter(m=>enabled(m.id)).map(m=>m.card).find(c=>c?.kind===item.kind);}
 async function save(){for(const m of modules.values())if(enabled(m.id))await m.save?.();}
 function decorateCard(item,html){const spec=cards(item)||{ratio:item.kind==='book'?.7:item.kind==='video'?16/9:1},duration=item.audio?.duration,badges=[duration>0?`${Math.floor(duration/60)}:${String(Math.floor(duration%60)).padStart(2,'0')}`:'',item.progress>0?`${Math.round(item.progress*100)}%`:''].filter(Boolean);return html.replace('<article ',`<article style="--item-ratio:${spec.ratio||1}" `).replace('<div class="file-info">',`${badges.length?`<div class="item-badges">${badges.map(b=>`<span>${b}</span>`).join('')}</div>`:''}<div class="file-info">`);}
 async function beforeDisable(id){const module=modules.get(id);if(module?.flush)await module.flush();}
 return Object.freeze({decorateCard,save,beforeDisable,define,snapshot,extend,invoke,cards,onMessage,applicable,matches,inspector,register,renderRoute,beforeNavigate,nav,toolbar,start,sync,settings,openViewer,on,emit,run,enabled,catalog:()=>catalog,list:()=>[...modules.keys()]});
})();
if(typeof module!=='undefined')module.exports=Kok;

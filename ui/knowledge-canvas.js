'use strict';
// One render loop owns camera, opacity and focus. Retargets retain the current
// position and velocity. Pointer dragging never waits for physics.
class KnowledgeCanvas {
 constructor(canvas){
  Object.assign(this,{canvas,context:canvas.getContext('2d'),nodes:[],edges:[],leaving:[],retiredEdges:[],byId:new Map(),camera:{x:0,y:0,zoom:1},velocity:{x:0,y:0,z:0},frame:0,generation:0,ticks:0,draws:0,settled:false,destroyed:false,ready:false,hover:null,selected:null,events:[],query:matchMedia('(prefers-reduced-motion: reduce)')});
  this.worker=new Worker(new URL('knowledge-worker.js?v=0.6.2-motion',location.href));this.worker.onmessage=event=>this.receive(event.data);
  this.worker.onerror=()=>{this.settled=true;this.ready=true;this.nodes.forEach(n=>n.opacity=1);this.fit(false);this.requestDraw();};
  this.resizeObserver=new ResizeObserver(()=>this.resize());this.resizeObserver.observe(canvas.parentElement);
  this.listen(canvas,'pointerdown',e=>this.down(e));this.listen(canvas,'pointermove',e=>this.move(e));this.listen(canvas,'pointerup',e=>this.up(e));this.listen(canvas,'pointercancel',e=>this.up(e,true));this.listen(canvas,'lostpointercapture',e=>this.up(e,true));
  this.listen(canvas,'pointerleave',()=>{if(!this.drag){this.hover=null;this.updateFocus();}});
  this.listen(canvas,'dblclick',event=>{const index=this.hit(this.point(event));if(index!==null){knowledgeView.selected=this.nodes[index].id;openSelectedKnowledge().catch(fail);}});
  this.listen(canvas,'wheel',e=>this.wheel(e),{passive:false});
  this.listen(canvas,'keydown',e=>{if(e.key==='Enter'&&this.selected){e.preventDefault();openSelectedKnowledge().catch(fail);}else if(['+','=','-','0'].includes(e.key)){e.preventDefault();if(e.key==='0')this.fit();else this.zoomBy(e.key==='-' ? .8 : 1.25);}});
  this.listen(document,'visibilitychange',()=>{this.pause(document.hidden);if(document.hidden)this.finishVisuals();});
  this.listen(this.query,'change',()=>{this.worker.postMessage({type:'reduce',value:reducedMotion()});if(reducedMotion())this.finishVisuals();});
  this.themeObserver=new MutationObserver(()=>{this.palette=null;this.requestDraw();});this.themeObserver.observe(document.body,{attributes:true,attributeFilter:['data-theme','data-custom-colors','style']});
  if(window.chrome?.webview)this.listen(window.chrome.webview,'message',e=>{if(e.data.eventName==='windowInteraction'){this.pause(!!e.data.data.active||document.hidden);if(e.data.data.active)this.finishVisuals(false);}});this.resize();
 }
 listen(target,name,handler,options){target.addEventListener(name,handler,options);this.events.push(()=>target.removeEventListener(name,handler,options));}
 motionAllowed(){return !reducedMotion()&&!document.hidden&&!(typeof isWindowInteracting==='function'&&isWindowInteracting());}
 setData(rows,links){
  const signature=rows.map(n=>n.id).sort().join('|')+'~'+links.map(e=>e.source+'>'+e.target+e.kind).sort().join('|');
  if(signature===this.signature){for(const row of rows){const node=this.nodes[this.byId.get(row.id)];if(node){Object.assign(node,row);node.label=this.label(row.name);}}this.requestDraw();return;}
  const previous=new Map(this.nodes.map(n=>[n.id,n])),ids=new Set(rows.map(n=>n.id)),wasReady=this.ready;this.signature=signature;this.finishCamera();
  this.leaving=this.nodes.filter(n=>!ids.has(n.id));this.retiredEdges=this.edges.filter(e=>!ids.has(e.source)||!ids.has(e.target)).map(e=>({a:this.nodes[e.a],b:this.nodes[e.b],kind:e.kind}));
  let uncached=0;
  this.nodes=rows.map((row,i)=>{const old=previous.get(row.id),cached=knowledgeView.positions.get(row.id),angle=i*2.39996,known=old||cached;if(!known)uncached++;return{...row,x:known?.x??Math.cos(angle)*Math.sqrt(i+1)*28,y:known?.y??Math.sin(angle)*Math.sqrt(i+1)*28,degree:0,opacity:old?.opacity??0,visibility:old?.visibility??1,known:!!known,label:this.label(row.name)};});
  this.byId=new Map(this.nodes.map((n,i)=>[n.id,i]));this.edges=links.map(e=>({...e,a:this.byId.get(e.source),b:this.byId.get(e.target)})).filter(e=>e.a!==undefined&&e.b!==undefined);for(const e of this.edges){this.nodes[e.a].degree++;this.nodes[e.b].degree++;}
  if(!ids.has(this.hover))this.hover=null;this.generation++;this.ticks=0;this.fade=null;this.initialFit=!wasReady;this.ready=wasReady;this.settled=uncached===0;
  if(uncached)this.worker.postMessage({type:'init',generation:this.generation,nodes:this.nodes.map(n=>({x:n.x,y:n.y,pin:n.known?{x:n.x,y:n.y}:null})),links:this.edges.map(e=>[e.a,e.b]),batch:true,reduced:reducedMotion()});
  else{this.ready=true;if(this.initialFit)this.fit(false);this.reveal();}
  this.updateFocus(true);this.requestDraw();
 }
 label(name){const text=Array.from(name);return text.length>24?text.slice(0,23).join('')+'…':name;}
 cache(node){knowledgeView.positions.set(node.id,{x:node.x,y:node.y});while(knowledgeView.positions.size>2000)knowledgeView.positions.delete(knowledgeView.positions.keys().next().value);}
 receive(data){
  if(this.destroyed||data.generation!==this.generation)return;
  for(let i=0;i<this.nodes.length;i++){const n=this.nodes[i];if(!n.manual){n.x=data.positions[i*2];n.y=data.positions[i*2+1];}this.cache(n);}
  this.ticks=data.ticks;this.settled=data.settled;if(data.settled){this.ready=true;if(this.initialFit){this.fit(false);this.initialFit=false;}this.reveal();}this.requestDraw();
 }
 reveal(){
  if(!this.motionAllowed()){this.nodes.forEach(n=>n.opacity=1);this.leaving=[];this.retiredEdges=[];this.fade=null;return;}
  this.fade={started:performance.now(),incoming:this.nodes.map(n=>[n,n.opacity]),outgoing:this.leaving.map(n=>[n,n.opacity])};this.requestDraw();
 }
 updateFocus(force=false){
  const active=this.hover||this.selected;if(active===this.activeId&&!force)return;this.activeId=active;
  const near=new Set(active?[active]:[]);if(active)for(const e of this.edges)if(e.source===active||e.target===active){near.add(e.source);near.add(e.target);}
  this.focusTween={started:performance.now(),rows:this.nodes.map(n=>[n,n.visibility,!active||near.has(n.id)?1:.2])};
  if(!this.motionAllowed()){for(const[n,,to]of this.focusTween.rows)n.visibility=to;this.focusTween=null;}this.requestDraw();
 }
 resize(){
  if(this.destroyed)return;const host=this.canvas.parentElement,width=host.clientWidth,height=host.clientHeight,ratio=Math.min(2,devicePixelRatio||1);if(!width||!height||this.width===width&&this.height===height&&this.ratio===ratio)return;
  this.finishCamera();const first=!this.width;this.camera.x+=(width-(this.width||width))/2;this.camera.y+=(height-(this.height||height))/2;Object.assign(this,{width,height,ratio});this.canvas.width=Math.round(width*ratio);this.canvas.height=Math.round(height*ratio);if(first)this.camera={x:width/2,y:height/2,zoom:1};this.requestDraw();
 }
 requestDraw(){if(!this.frame&&!this.destroyed)this.frame=requestAnimationFrame(now=>this.tick(now));}
 tick(now){
  this.frame=0;if(this.destroyed)return;if(this.cameraTarget)this.stepCamera(now);
  if(this.fade){const f=this.fade,t=Math.min(1,(now-f.started)/180),ease=1-Math.pow(1-t,3);for(const[n,from]of f.incoming)n.opacity=from+(1-from)*ease;for(const[n,from]of f.outgoing)n.opacity=from*(1-ease);if(t===1){this.fade=null;this.leaving=[];this.retiredEdges=[];}}
  if(this.focusTween){const f=this.focusTween,t=Math.min(1,(now-f.started)/140),ease=1-Math.pow(1-t,3);for(const[n,from,to]of f.rows)n.visibility=from+(to-from)*ease;if(t===1)this.focusTween=null;}
  this.draw();if(this.cameraTarget||this.fade||this.focusTween)this.requestDraw();
 }
 screen(node){return{x:node.x*this.camera.zoom+this.camera.x,y:node.y*this.camera.zoom+this.camera.y};}
 draw(){
  if(this.destroyed||!this.width)return;this.draws++;
  if(!this.palette){const css=getComputedStyle(this.canvas);this.palette={ink:css.getPropertyValue('--ink').trim()||'#ddd',muted:css.getPropertyValue('--muted').trim()||'#999',accent:css.getPropertyValue('--knowledge-accent').trim()||'#75a69d'};}
  const ctx=this.context,{ink,muted,accent}=this.palette,active=this.activeId;ctx.setTransform(this.ratio,0,0,this.ratio,0,0);ctx.clearRect(0,0,this.width,this.height);
  const line=(a,b,kind)=>{const opacity=Math.min(a.opacity,b.opacity);if(opacity<.002)return;const p=this.screen(a),q=this.screen(b),highlight=active&&(a.id===active||b.id===active);ctx.beginPath();ctx.moveTo(p.x,p.y);ctx.lineTo(q.x,q.y);ctx.strokeStyle=highlight?accent:muted;ctx.globalAlpha=opacity*Math.min(a.visibility,b.visibility)*(highlight ? .68 : kind==='tag' ? .18 : .28);ctx.lineWidth=highlight?1.4:1;if(kind==='tag')ctx.setLineDash([3,5]);ctx.stroke();ctx.setLineDash([]);};
  for(const e of this.retiredEdges)line(e.a,e.b,e.kind);for(const e of this.edges)line(this.nodes[e.a],this.nodes[e.b],e.kind);
  ctx.textBaseline='middle';ctx.font='12px "Segoe UI","Microsoft YaHei UI",sans-serif';
  for(const n of [...this.leaving,...this.nodes]){
   if(n.opacity<.002)continue;const p=this.screen(n);if(p.x< -120||p.x>this.width+120||p.y< -30||p.y>this.height+30)continue;
   const highlighted=n.id===active,radius=Math.max(3,Math.min(10,(n.kind==='tag'?3:5+Math.sqrt(n.degree)*.65)*Math.sqrt(this.camera.zoom))),alpha=n.opacity*n.visibility;
   ctx.globalAlpha=alpha;ctx.beginPath();ctx.arc(p.x,p.y,radius,0,Math.PI*2);ctx.fillStyle=highlighted?accent:ink;ctx.strokeStyle=highlighted?accent:muted;ctx.lineWidth=highlighted?2:1;
   if(n.missing){ctx.setLineDash([2,2]);ctx.stroke();ctx.setLineDash([]);}else if(n.kind==='tag')ctx.stroke();else ctx.fill();
   if(highlighted){ctx.globalAlpha=alpha*.16;ctx.beginPath();ctx.arc(p.x,p.y,radius+6,0,Math.PI*2);ctx.fill();}
   const density=this.nodes.length<180||n.degree>=4,zoomAlpha=density?Math.max(0,Math.min(1,(this.camera.zoom-.3)/.4)):0,labelAlpha=highlighted||active&&n.visibility>.8?1:zoomAlpha;
   if(labelAlpha>.01){ctx.fillStyle=highlighted?accent:ink;ctx.globalAlpha=alpha*labelAlpha*.94;ctx.fillText(n.label,p.x+radius+6,p.y);}
  }
  ctx.globalAlpha=1;if($('#knowledge-zoom'))$('#knowledge-zoom').value=Math.round(this.camera.zoom*100)+'%';
 }
 hit(point){let found=null,distance=225;for(let i=0;i<this.nodes.length;i++){if(this.nodes[i].opacity<.25)continue;const p=this.screen(this.nodes[i]),d=(p.x-point.x)**2+(p.y-point.y)**2;if(d<distance){found=i;distance=d;}}return found;}
 point(event){const r=this.canvas.getBoundingClientRect();return{x:event.clientX-r.left,y:event.clientY-r.top};}
 down(event){if(event.button!==0)return;event.preventDefault();this.finishCamera();const p=this.point(event),node=this.hit(p);this.canvas.setPointerCapture(event.pointerId);this.drag={pointer:event.pointerId,node,start:p,camera:{...this.camera},moved:false};this.canvas.style.cursor=node===null?'grabbing':'move';}
 move(event){
  const p=this.point(event);if(this.drag&&this.drag.pointer===event.pointerId){const drag=this.drag;if(Math.hypot(p.x-drag.start.x,p.y-drag.start.y)>3)drag.moved=true;if(!drag.moved)return;
   if(drag.node===null){this.camera.x=drag.camera.x+p.x-drag.start.x;this.camera.y=drag.camera.y+p.y-drag.start.y;}else{const n=this.nodes[drag.node];n.x=(p.x-this.camera.x)/this.camera.zoom;n.y=(p.y-this.camera.y)/this.camera.zoom;n.manual=true;this.cache(n);}this.requestDraw();return;
  }
  const index=this.hit(p),id=index===null?null:this.nodes[index].id;if(this.hover!==id){this.hover=id;this.canvas.title=index===null?'':this.nodes[index].name;this.canvas.style.cursor=index===null?'grab':'pointer';this.updateFocus();}
 }
 up(event,cancelled=false){
  if(!this.drag||this.drag.pointer!==event.pointerId)return;const drag=this.drag;this.drag=null;
  if(!drag.moved&&!cancelled){knowledgeView.selected=drag.node===null?null:this.nodes[drag.node].id;renderKnowledgeSelection();for(const b of document.querySelectorAll('[data-knowledge-select]'))b.classList.toggle('selected',b.dataset.knowledgeSelect===knowledgeView.selected);}
  if(this.canvas.hasPointerCapture(event.pointerId))this.canvas.releasePointerCapture(event.pointerId);this.canvas.style.cursor='grab';this.requestDraw();
 }
 wheel(event){
  event.preventDefault();const delta=Math.max(-400,Math.min(400,event.deltaY*(event.deltaMode===1?16:event.deltaMode===2?300:1))),point=this.point(event),base=this.cameraTarget||this.camera,zoom=Math.max(.12,Math.min(5,base.zoom*Math.exp(-delta*.0018))),world={x:(point.x-this.camera.x)/this.camera.zoom,y:(point.y-this.camera.y)/this.camera.zoom};
  this.animateCamera({x:point.x-world.x*zoom,y:point.y-world.y*zoom,zoom},Math.abs(delta)>=40||event.deltaMode!==0,{point,world});
 }
 animateCamera(to,animate=true,anchor=null){
  if(!animate||!this.motionAllowed()){this.finishCamera();this.camera=to;this.requestDraw();return;}
  if(!this.cameraTarget){this.velocity={x:0,y:0,z:0};this.cameraTime=performance.now();}this.cameraTarget=to;this.zoomAnchor=anchor;this.requestDraw();
 }
 stepCamera(now){
  const dt=Math.max(.001,Math.min(.064,(now-this.cameraTime)/1000)),goal=this.cameraTarget;this.cameraTime=now;
  const spring=(value,to,velocity)=>{const omega=28,d=value-to,term=velocity+omega*d,decay=Math.exp(-omega*dt);return[to+(d+term*dt)*decay,(velocity-omega*term*dt)*decay];};
  let result=spring(Math.log(this.camera.zoom),Math.log(goal.zoom),this.velocity.z);this.camera.zoom=Math.exp(result[0]);this.velocity.z=result[1];
  if(this.zoomAnchor){const{point,world}=this.zoomAnchor;this.camera.x=point.x-world.x*this.camera.zoom;this.camera.y=point.y-world.y*this.camera.zoom;this.velocity.x=this.velocity.y=0;}
  else{for(const key of ['x','y']){result=spring(this.camera[key],goal[key],this.velocity[key]);this.camera[key]=result[0];this.velocity[key]=result[1];}}
  if(Math.abs(this.camera.x-goal.x)<.12&&Math.abs(this.camera.y-goal.y)<.12&&Math.abs(Math.log(this.camera.zoom/goal.zoom))<.0003&&Math.abs(this.velocity.x)+Math.abs(this.velocity.y)<2&&Math.abs(this.velocity.z)<.006){this.camera={...goal};this.cameraTarget=null;this.zoomAnchor=null;this.velocity={x:0,y:0,z:0};}
 }
 finishCamera(settle=false){if(settle&&this.cameraTarget)this.camera={...this.cameraTarget};this.cameraTarget=null;this.zoomAnchor=null;this.velocity={x:0,y:0,z:0};}
 finishVisuals(settle=true){this.finishCamera(settle);if(this.fade){for(const[n]of this.fade.incoming)n.opacity=1;this.fade=null;this.leaving=[];this.retiredEdges=[];}if(this.focusTween){for(const[n,,to]of this.focusTween.rows)n.visibility=to;this.focusTween=null;}this.requestDraw();}
 fit(animate=true){if(!this.nodes.length||!this.width)return;const xs=this.nodes.map(n=>n.x),ys=this.nodes.map(n=>n.y),left=Math.min(...xs)-55,right=Math.max(...xs)+110,top=Math.min(...ys)-45,bottom=Math.max(...ys)+45,zoom=Math.max(.12,Math.min(2,(this.width-80)/Math.max(180,right-left),(this.height-70)/Math.max(160,bottom-top)));this.animateCamera({x:this.width/2-(left+right)/2*zoom,y:this.height/2-(top+bottom)/2*zoom,zoom},animate);}
 centerOn(id){const n=this.nodes[this.byId.get(id)];if(n)this.animateCamera({x:this.width/2-n.x*this.camera.zoom,y:this.height/2-n.y*this.camera.zoom,zoom:this.camera.zoom});}
 zoomBy(factor){const point={x:this.width/2,y:this.height/2},world={x:(point.x-this.camera.x)/this.camera.zoom,y:(point.y-this.camera.y)/this.camera.zoom},zoom=Math.max(.12,Math.min(5,(this.cameraTarget?.zoom||this.camera.zoom)*factor));this.animateCamera({x:point.x-world.x*zoom,y:point.y-world.y*zoom,zoom},true,{point,world});}
 select(id){if(this.selected===id)return;this.selected=id;this.updateFocus();}
 pause(value){this.worker.postMessage({type:value?'pause':'resume'});}
 destroy(){if(this.destroyed)return;this.destroyed=true;cancelAnimationFrame(this.frame);this.frame=0;this.worker.terminate();this.resizeObserver.disconnect();this.themeObserver.disconnect();this.events.forEach(remove=>remove());this.events=[];this.leaving=[];this.retiredEdges=[];this.fade=this.focusTween=null;this.finishCamera();}
}

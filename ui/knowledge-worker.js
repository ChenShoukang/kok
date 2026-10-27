'use strict';
// Standard damped force layout, isolated from input/rendering. Geometry only crosses this boundary.
let nodes=[],links=[],timer=0,ticks=0,heat=1,paused=false,reduced=false,batch=false,generation=0;
const limit=210;
function step(){
 for(const n of nodes){n.ax=-n.x*.0025*heat;n.ay=-n.y*.0025*heat;}
 for(let i=0;i<nodes.length;i++)for(let j=i+1;j<nodes.length;j++){const a=nodes[i],b=nodes[j];let dx=a.x-b.x,dy=a.y-b.y;if(Math.abs(dx)+Math.abs(dy)<.01){dx=(i%2?1:-1)*.1;dy=.1;}const distance=dx*dx+dy*dy+36,strength=Math.min(.2,190/distance)*heat,fx=dx*strength,fy=dy*strength;a.ax+=fx;a.ay+=fy;b.ax-=fx;b.ay-=fy;}
 for(const [i,j]of links){const a=nodes[i],b=nodes[j],dx=b.x-a.x,dy=b.y-a.y,d=Math.max(1,Math.hypot(dx,dy)),force=(d-84)*.018*heat/d;a.ax+=dx*force;a.ay+=dy*force;b.ax-=dx*force;b.ay-=dy*force;}
 let speed=0;for(const n of nodes){if(n.pin){n.x=n.pin.x;n.y=n.pin.y;n.vx=n.vy=0;continue;}n.vx=(n.vx+n.ax)*.72;n.vy=(n.vy+n.ay)*.72;const scale=Math.min(1,10/Math.max(1,Math.hypot(n.vx,n.vy)));n.vx*=scale;n.vy*=scale;n.x+=n.vx;n.y+=n.vy;speed+=Math.abs(n.vx)+Math.abs(n.vy);}heat*=.975;ticks++;return ticks>=limit||ticks>80&&speed/Math.max(1,nodes.length)<.025;
}
function publish(settled){const positions=new Float32Array(nodes.length*2);nodes.forEach((n,i)=>{positions[i*2]=n.x;positions[i*2+1]=n.y;});self.postMessage({type:'positions',generation,positions,settled,ticks},[positions.buffer]);}
function run(){clearTimeout(timer);timer=0;if(paused)return;if(!nodes.length){publish(true);return;}if(reduced||batch){while(ticks<limit)step();publish(true);return;}const settled=step();publish(settled);if(!settled)timer=setTimeout(run,16);}
self.onmessage=event=>{const data=event.data;if(data.type==='init'){clearTimeout(timer);generation=data.generation;nodes=data.nodes.slice(0,500).map((n,i)=>({x:Number.isFinite(n.x)?n.x:Math.cos(i*2.39996)*Math.sqrt(i+1)*28,y:Number.isFinite(n.y)?n.y:Math.sin(i*2.39996)*Math.sqrt(i+1)*28,vx:0,vy:0,ax:0,ay:0,pin:n.pin&&Number.isFinite(n.pin.x)&&Number.isFinite(n.pin.y)?n.pin:null}));links=data.links.filter(([a,b])=>a!==b&&a>=0&&b>=0&&a<nodes.length&&b<nodes.length).slice(0,5000);ticks=0;heat=1;reduced=!!data.reduced;batch=!!data.batch;paused=false;run();}
 else if(data.type==='pause'){paused=true;clearTimeout(timer);timer=0;}
 else if(data.type==='resume'){paused=false;if(ticks<limit)run();}
 else if(data.type==='reduce'){reduced=!!data.value;if(reduced&&ticks<limit&&!paused)run();}
 else if(data.type==='pin'){const n=nodes[data.index];if(!n)return;n.pin=data.point;n.x=data.point?.x??n.x;n.y=data.point?.y??n.y;ticks=Math.min(ticks,145);heat=Math.max(heat,.32);if(!paused&&!timer)run();}
};

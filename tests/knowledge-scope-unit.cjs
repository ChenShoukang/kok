'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm'),scope=require('../ui/knowledge-scope.js');
const nodes=[['a','Alpha','A'],['b','Beta','B'],['c','Gamma','A']].map(([id,name,collection])=>({id,name,collection,kind:'document',ext:'MD',eligible:true,tags:['Topic'],aliases:[]}));
nodes.push({id:'missing:x',name:'Missing',missing:true,kind:'missing',tags:[],aliases:[]},{id:'tag:topic',key:'Topic',name:'#Topic',kind:'tag',tags:[],aliases:[]});
const model={nodes,links:[{source:'a',target:'b',kind:'link'},{source:'b',target:'c',kind:'link'},{source:'a',target:'missing:x',kind:'link'},{source:'a',target:'tag:topic',kind:'tag'},{source:'c',target:'tag:topic',kind:'tag'}]};
const items=nodes.slice(0,3).map((n,i)=>({...n,added:'2026-10-'+(10+i),favorite:i!==1}));
const defaults={showTags:true,showIsolated:true,limit:250,scope:'all'};
let result=scope.select(model,{...defaults,collection:'A',focus:'a',depth:2,showTags:false},items);
assert(!result.nodes.some(n=>n.id==='c'),'Filtering out B must not connect A and C through hidden B');assert(!result.nodes.some(n=>n.id==='b'));
result=scope.select(model,{...defaults,scope:'manual',ids:['c']},items);assert.deepEqual(result.nodes.filter(n=>!n.missing&&n.kind!=='tag').map(n=>n.id),['c']);assert(!result.nodes.some(n=>n.missing),'Missing nodes must come from visible sources');
result=scope.select(model,{...defaults,scope:'favorites',from:'2026-10-11',to:'2026-10-12'},items);assert.deepEqual(result.nodes.filter(n=>n.kind==='document').map(n=>n.id),['c']);
assert.equal(scope.select(model,{...defaults,scope:'manual',ids:[]},items).nodes.length,0);assert.equal(scope.select(model,{...defaults,tag:'absent'},items).nodes.length,0);
const huge={nodes:Array.from({length:1200},(_,i)=>({id:String(i),name:'Node '+i,collection:'',kind:'document',eligible:true,tags:[],aliases:[]})),links:[]};assert.equal(scope.select(huge,{...defaults,limit:10000},huge.nodes).nodes.length,500);
const source=fs.readFileSync('ui/motion-core.js','utf8').split('const elementMotions')[0],context={matchMedia:()=>({})};vm.createContext(context);vm.runInContext(source+';globalThis.clock=MotionClock;',context);const clock=context.clock;
function travel(fps){let x=0,v=0;for(let n=0;n<fps;n++)[x,v]=clock.spring(x,100,v,1/fps);return x;}
assert(Math.abs(travel(30)-travel(144))<1e-8,'Analytic spring must agree across frame rates');assert.equal(clock.progress(900,100,200),1);assert.equal(clock.progress(-200,100,200),0);assert.equal(clock.progress(NaN,0,200),1);assert.equal(clock.ease(1),1);
console.log('PASS: graph scope boundaries, date/manual/favorite intersections, empty scope, 500-node cap and frame-rate-independent bounded motion.');

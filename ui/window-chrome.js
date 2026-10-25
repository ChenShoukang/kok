'use strict';
// Native frame colors follow the same palette as the page, including custom colors.
(() => {
 let scheduled=0,last='';
 const query=matchMedia('(prefers-reduced-motion: reduce)');
 function update(){
  scheduled=0;if(typeof native!=='function'||!document.body)return;
  const style=getComputedStyle(document.body),value=style.getPropertyValue('--bg').trim()||style.backgroundColor;
  const probe=document.createElement('span');probe.style.color=value;probe.style.display='none';document.body.append(probe);
  const color=getComputedStyle(probe).color;probe.remove();const parts=color.match(/[\d.]+/g);if(!parts||parts.length<3)return;
  const hex='#'+parts.slice(0,3).map(n=>Math.max(0,Math.min(255,Math.round(Number(n)))).toString(16).padStart(2,'0')).join('');
  const snapshot=JSON.stringify([hex,query.matches,document.documentElement.lang]);if(snapshot===last)return;last=snapshot;
  native('setWindowAppearance',{color:hex,reduceMotion:query.matches}).catch(()=>{last='';});
 }
 function schedule(){if(!scheduled)scheduled=requestAnimationFrame(update);}
 new MutationObserver(schedule).observe(document.body,{attributes:true,attributeFilter:['data-theme','data-custom-colors','style']});
 new MutationObserver(schedule).observe(document.documentElement,{attributes:true,attributeFilter:['lang']});
 query.addEventListener('change',schedule);document.addEventListener('visibilitychange',schedule);schedule();
})();

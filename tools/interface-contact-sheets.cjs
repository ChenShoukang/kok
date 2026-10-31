// Small local screenshot gallery for human layout review; never sends screenshots off-device.
const fs=require('node:fs'),path=require('node:path'),{chromium}=require('playwright-core');
(async()=>{
 const root=path.resolve('test-output/interface-audit'),report=JSON.parse(fs.readFileSync(path.join(root,'report.json'),'utf8'));
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',args:['--disable-gpu']});
 try{
  const page=await browser.newPage({viewport:{width:1920,height:1600},deviceScaleFactor:1});
  for(let start=0;start<report.screens.length;start+=12){
   const rows=report.screens.slice(start,start+12),html='<style>*{box-sizing:border-box}body{margin:0;background:#dce0e7;font:14px Arial}main{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;padding:12px}figure{margin:0;padding:8px;background:white;height:390px;display:flex;flex-direction:column}img{width:100%;height:350px;object-fit:contain;object-position:top}figcaption{height:24px;padding:4px}</style><main>'+rows.map(r=>'<figure><img src="data:image/png;base64,'+fs.readFileSync(path.join(root,r.file)).toString('base64')+'"><figcaption>'+r.file+'</figcaption></figure>').join('')+'</main>';
   await page.setContent(html);await page.evaluate(()=>Promise.all([...document.images].map(i=>i.decode())));await page.screenshot({path:path.join(root,`contact-${1+start/12}.png`),fullPage:true});
  }
 }finally{await browser.close();}
 console.log('Contact sheets ready: '+root);
})().catch(e=>{console.error(e);process.exit(1);});

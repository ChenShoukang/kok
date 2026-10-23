// Generate self-contained SVG diagrams. --png also renders with local Edge.
const fs=require('node:fs');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
const root=path.resolve(__dirname,'..'),folder=path.join(root,'docs/assets');
const logo=fs.readFileSync(path.join(root,'ui/logo.svg'),'utf8').replace('<svg ', '<svg x="48" y="38" width="64" height="64" ');
const icons={video:'M4 5h16v14H4z M10 9l5 3-5 3V9Z',photo:'M4 4h16v16H4z M4 16l5-5 4 4 3-3 4 4 M16 8h.01',music:'M9 18V5l12-2v13 M9 8l12-2 M9 18c0 1.7-1.6 3-3.5 3S2 20 2 18.5 3.6 16 5.5 16 9 16.3 9 18Z M21 16c0 1.7-1.6 3-3.5 3S14 18 14 16.5 15.6 14 17.5 14s3.5.3 3.5 2Z',document:'M14 3H5v18h14V8l-5-5Z M14 3v5h5 M8 12h8 M8 16h6',book:'M12 5C8 2 3 3 3 3v16s5-1 9 2c4-3 9-2 9-2V3s-5-1-9 2Z M12 5v16'};
const labels={
 zh:{title:'五种内容，一个资料库。',intro:'本地收藏，随手记录，按需连接 AI。',platform:'Windows 桌面版',cards:[['video','视频','字幕、倍速与全屏'],['photo','照片','调色、裁剪与旋转'],['music','音乐','专辑、队列与续播'],['document','文档','PDF 阅读与文字批注'],['book','读书','划线、想法与续读']],footer:[['本地优先','纯净 / 智能，随时切换'],['可选 AI','翻译、内容问答与整理'],['私密文件夹','资料库内开启，密码保护']]},
 en:{title:'Five kinds of content. One library.',intro:'Keep it local. Capture a thought. Add AI when you choose.',platform:'Windows desktop',cards:[['video','Videos','Subtitles and speed'],['photo','Photos','Color, crop and rotate'],['music','Music','Albums and queues'],['document','Documents','Read and annotate'],['book','Books','Highlights and notes']],footer:[['Local first','Pure / Smart, your choice'],['Optional AI','Translate, ask and organize'],['Private folder','Inside your library, password protected']]}
};
const escape=s=>s.replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));
function diagram(lang){const l=labels[lang];return `<svg xmlns="http://www.w3.org/2000/svg" width="1280" height="600" viewBox="0 0 1280 600" role="img" aria-labelledby="title description">
<title id="title">kok — ${escape(l.title)}</title><desc id="description">${escape(l.cards.map(c=>c[1]+': '+c[2]).join('; ')+'. '+l.footer.map(c=>c.join(': ')).join('; '))}</desc>
<defs><linearGradient id="background" x2="1" y2="1"><stop stop-color="#edf5ff"/><stop offset=".6" stop-color="#f5f4fd"/><stop offset="1" stop-color="#eff8f5"/></linearGradient><linearGradient id="glass" x2=".8" y2="1"><stop stop-color="#ffffff" stop-opacity=".88"/><stop offset="1" stop-color="#ffffff" stop-opacity=".48"/></linearGradient><filter id="shadow" x="-20%" y="-15%" width="140%" height="145%"><feDropShadow dx="0" dy="8" stdDeviation="14" flood-color="#657a9a" flood-opacity=".08"/></filter></defs>
<rect width="1280" height="600" rx="28" fill="url(#background)"/>
<g font-family="Segoe UI,Microsoft YaHei UI,Microsoft YaHei,Arial,sans-serif">
${logo}<text x="128" y="82" font-size="32" font-weight="650" fill="#29384e">kok</text>
<rect x="1030" y="48" width="202" height="38" rx="19" fill="#ffffff" fill-opacity=".65" stroke="#ffffff"/><text x="1131" y="73" text-anchor="middle" font-size="16" fill="#526983">${l.platform}</text>
<text x="48" y="164" font-size="38" font-weight="600" fill="#29384e" letter-spacing="-.6">${escape(l.title)}</text>
<text x="48" y="202" font-size="20" fill="#62748c">${escape(l.intro)}</text>
${l.cards.map(([kind,name,detail],n)=>{const x=48+n*240;return `<g transform="translate(${x} 240)"><rect width="224" height="202" rx="22" fill="url(#glass)" stroke="#ffffff" stroke-width="1.5" filter="url(#shadow)"/><rect x="22" y="23" width="58" height="58" rx="17" fill="${['#e1edfb','#e1f0e9','#eee8f6','#e4ecf8','#e5eee9'][n]}"/><svg x="35" y="36" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#6e87a5" stroke-width="1.45" stroke-linecap="round" stroke-linejoin="round"><path d="${icons[kind]}"/></svg><text x="22" y="126" font-size="26" font-weight="600" fill="#35455c">${name}</text><text x="22" y="164" font-size="${lang==='en'?17:18}" fill="#62748c">${escape(detail)}</text></g>`;}).join('\n')}
<path d="M48 480H1232" stroke="#dbe4ef"/>
${l.footer.map(([title,detail],n)=>{const x=48+n*400;return `<g transform="translate(${x} 518)"><circle cx="5" cy="-6" r="4" fill="${['#8ba7ca','#a195c5','#8fa9a1'][n]}"/><text x="22" y="0" font-size="19" font-weight="600" fill="#40536d">${title}</text><text x="22" y="32" font-size="${lang==='en'?16:18}" fill="#62748c">${escape(detail)}</text></g>`;}).join('\n')}
</g></svg>\n`;}
function mobileDiagram(lang){
 const l=labels[lang],head=lang==='zh'?['五种内容，','一个资料库。']:['Five kinds of content.','One library.'];
 const mark=logo.replace('x="48" y="38" width="64" height="64"','x="24" y="24" width="48" height="48"');
 const defs=diagram(lang).match(/<defs>[\s\S]*?<\/defs>/)[0];
 return `<svg xmlns="http://www.w3.org/2000/svg" width="640" height="968" viewBox="0 0 640 968" role="img" aria-labelledby="title description">
<title id="title">kok — ${escape(l.title)}</title><desc id="description">${escape(l.cards.map(c=>c[1]+': '+c[2]).join('; '))}</desc>
${defs}<rect width="640" height="968" rx="24" fill="url(#background)"/>
<g font-family="Segoe UI,Microsoft YaHei UI,Microsoft YaHei,Arial,sans-serif">
${mark}<text x="84" y="58" font-size="28" font-weight="650" fill="#29384e">kok</text>
<rect x="424" y="32" width="192" height="32" rx="16" fill="#ffffff" fill-opacity=".65"/><text x="520" y="54" text-anchor="middle" font-size="17" fill="#526983">${l.platform}</text>
<text x="24" y="124" font-size="34" font-weight="600" fill="#29384e">${head[0]}</text><text x="24" y="166" font-size="34" font-weight="600" fill="#29384e">${head[1]}</text>
<text x="24" y="206" font-size="22" fill="#62748c">${lang==='zh'?'本地收藏，按需连接 AI。':'Local first. AI when you choose.'}</text>
${l.cards.map(([kind,name,detail],n)=>{const x=n===4?24:24+(n%2)*308,y=238+Math.floor(n/2)*172,width=n===4?592:284,height=n===4?140:156;return `<g transform="translate(${x} ${y})"><rect width="${width}" height="${height}" rx="20" fill="url(#glass)" stroke="#ffffff" stroke-width="1.5"/><rect x="20" y="22" width="54" height="54" rx="16" fill="${['#e1edfb','#e1f0e9','#eee8f6','#e4ecf8','#e5eee9'][n]}"/><svg x="31" y="33" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#6e87a5" stroke-width="1.45" stroke-linecap="round" stroke-linejoin="round"><path d="${icons[kind]}"/></svg><text x="90" y="58" font-size="28" font-weight="600" fill="#35455c">${name}</text><text x="${n===4?90:20}" y="${n===4?101:120}" font-size="22" fill="#62748c">${escape(detail)}</text></g>`;}).join('\n')}
<path d="M24 748H616" stroke="#dbe4ef"/>
${l.footer.map(([title,detail],n)=>`<g transform="translate(24 ${790+n*66})"><circle cx="5" cy="-8" r="4" fill="${['#8ba7ca','#a195c5','#8fa9a1'][n]}"/><text x="22" y="0" font-size="24" font-weight="600" fill="#40536d">${title}</text><text x="22" y="29" font-size="21" fill="#62748c">${escape(detail)}</text></g>`).join('\n')}
</g></svg>\n`;
}
async function previewPicture(page,lang){
 const assert=require('node:assert/strict'),readme=fs.readFileSync(path.join(root,lang==='zh'?'README.md':'README.en.md'),'utf8');
 const picture=readme.match(/<picture>[\s\S]*?<\/picture>/)?.[0];assert(picture,'README must include its responsive feature overview');
 const output=path.join(root,'test-output');fs.mkdirSync(output,{recursive:true});
 const preview=path.join(output,'readme-picture-'+lang+'.html');
 fs.writeFileSync(preview,`<!doctype html><meta name="viewport" content="width=device-width,initial-scale=1"><base href="${pathToFileURL(root+path.sep).href}"><style>*{box-sizing:border-box}body{margin:0;background:#fff;font-family:Segoe UI,Microsoft YaHei,sans-serif}main{max-width:980px;margin:auto;padding:24px}h1{font-size:28px;color:#29384e}img{display:block;width:100%;height:auto}</style><main><h1>${escape(readme.split(/\r?\n/)[0].replace(/^# /,''))}</h1>${picture}</main>`);
 for(const width of [1280,375]){
  await page.setViewportSize({width,height:1000});await page.goto(pathToFileURL(preview).href);
  await page.waitForFunction(()=>document.querySelector('img').complete&&document.querySelector('img').naturalWidth>0);
  const layout=await page.locator('img').evaluate(img=>({source:img.currentSrc,width:img.getBoundingClientRect().width,overflow:document.documentElement.scrollWidth>innerWidth}));
  assert(layout.source.endsWith('features-'+lang+(width<=600?'-mobile':'')+'.png'));assert(!layout.overflow);assert(layout.width>250);
  await page.screenshot({path:path.join(output,`readme-picture-${lang}-${width}.png`),fullPage:true});
 }
}
(async()=>{
 for(const lang of Object.keys(labels)){fs.writeFileSync(path.join(folder,'features-'+lang+'.svg'),diagram(lang));fs.writeFileSync(path.join(folder,'features-'+lang+'-mobile.svg'),mobileDiagram(lang));}
 if(process.argv.includes('--png')){
  const {chromium}=require('playwright-core'),assert=require('node:assert/strict');const browser=await chromium.launch({channel:'msedge',headless:true,args:['--disable-background-networking','--no-first-run']});
  try{
   const page=await browser.newPage({viewport:{width:1280,height:600},deviceScaleFactor:2});
   for(const lang of Object.keys(labels))for(const mobile of [false,true]){
    const stem='features-'+lang+(mobile?'-mobile':''),width=mobile?640:1280,height=mobile?968:600;
    await page.setViewportSize({width,height});await page.goto(pathToFileURL(path.join(folder,stem+'.svg')).href);await page.evaluate(()=>document.fonts.ready);
    const clipped=await page.locator('text').evaluateAll(nodes=>nodes.filter(node=>{const r=node.getBoundingClientRect();return r.left<-.5||r.right>innerWidth+.5||r.top<-.5||r.bottom>innerHeight+.5;}).map(node=>node.textContent));
    assert.deepEqual(clipped,[],stem+' must not clip text');await page.screenshot({path:path.join(folder,stem+'.png')});
   }
   for(const lang of Object.keys(labels))await previewPicture(page,lang);
  }finally{await browser.close();}
 }
 console.log('README diagrams generated'+(process.argv.includes('--png')?'; bilingual desktop/mobile rendering and image selection checks passed.':'.'));
})().catch(error=>{console.error(error);process.exitCode=1;});

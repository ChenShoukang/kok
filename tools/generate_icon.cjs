// Render the original SVG mark through the local test window, preserving alpha.
const {chromium}=require('playwright-core');
const fs=require('node:fs');
const path=require('node:path');
(async()=>{
 const root=path.resolve(__dirname,'..');
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9225');
 const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('https://app.kok.local'));
 const images=await page.evaluate(async source=>{
   const image=new Image();image.src='data:image/svg+xml;charset=utf-8,'+encodeURIComponent(source);await image.decode();
   return [16,32,48,64,256].map(size=>{const canvas=document.createElement('canvas');canvas.width=canvas.height=size;canvas.getContext('2d').drawImage(image,0,0,size,size);return{size,png:canvas.toDataURL('image/png').split(',')[1]};});
 },fs.readFileSync(path.join(root,'ui/logo.svg'),'utf8'));
 const header=Buffer.alloc(6+16*images.length);header.writeUInt16LE(1,2);header.writeUInt16LE(images.length,4);
 const chunks=[];let offset=header.length;
 images.forEach((image,n)=>{const png=Buffer.from(image.png,'base64');const at=6+n*16;header[at]=header[at+1]=image.size===256?0:image.size;header.writeUInt16LE(1,at+4);header.writeUInt16LE(32,at+6);header.writeUInt32LE(png.length,at+8);header.writeUInt32LE(offset,at+12);offset+=png.length;chunks.push(png);});
 fs.writeFileSync(path.join(root,'ui/kok.ico'),Buffer.concat([header,...chunks]));await browser.close();console.log('Generated multi-size kok icon from logo.svg.');
})().catch(error=>{console.error(error);process.exit(1);});

'use strict';
// Synthetic, offline fixtures shared by motion and native-window checks.
const fs=require('node:fs'),path=require('node:path'),{PDFDocument,StandardFonts}=require('pdf-lib');
module.exports=async function prepareReading(root){
 const fixtures=path.join(root,'fixtures');fs.mkdirSync(fixtures,{recursive:true});
 fs.writeFileSync(path.join(fixtures,'long-book.txt'),'序章\n\n'+Array.from({length:240},(_,i)=>`第 ${i+1} 段。这是一段用于验证分页、字体与目录的正文。Reading should remain clear and comfortable.\n\n`).join(''));
 const pdf=await PDFDocument.create(),font=await pdf.embedFont(StandardFonts.Helvetica);for(let n=1;n<=3;n++){const page=pdf.addPage([420,560]);page.drawText('Readable original page '+n,{font,size:20,x:30,y:480});page.drawText('Clear letters and meaningful annotations.',{font,size:12,x:30,y:430});}fs.writeFileSync(path.join(fixtures,'reading.pdf'),await pdf.save());
 const file=path.join(root,'library.json'),db=JSON.parse(fs.readFileSync(file,'utf8'));db.items=[['renewal-book','long-book.txt','book'],['renewal-pdf','reading.pdf','document']].map(([id,name,kind])=>({id,name:path.parse(name).name,path:path.join(fixtures,name),kind,ext:path.extname(name).slice(1).toUpperCase(),size:fs.statSync(path.join(fixtures,name)).size,added:new Date().toISOString()}));fs.writeFileSync(file,JSON.stringify(db));
};

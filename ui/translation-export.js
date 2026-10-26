'use strict';
function translationPageLines(context,text,width){const lines=[];for(const paragraph of text.replaceAll('\r','').split('\n')){let line='';for(const char of paragraph){if(line&&context.measureText(line+char).width>width){lines.push(line);line='';}line+=char;}lines.push(line);}return lines;}
function translationPdfSheets(text,width,height){
 const scale=Math.min(2,2600/Math.max(width,height)),canvas=document.createElement('canvas');canvas.width=Math.ceil(width*scale);canvas.height=Math.ceil(height*scale);const context=canvas.getContext('2d');context.scale(scale,scale);let size=Math.max(9,Math.min(13,width/42)),margin=Math.min(42,width*.08),lines;
 do{context.font=size+'px "Microsoft YaHei","Segoe UI",sans-serif';lines=translationPageLines(context,text,width-margin*2);if(lines.length*(size*1.7)<=height-margin*2||size<=9)break;size-=.5;}while(true);
 const rows=Math.max(1,Math.floor((height-margin*2)/(size*1.7))),sheets=[];
 for(let start=0;start<lines.length;start+=rows){context.fillStyle='#ffffff';context.fillRect(0,0,width,height);context.fillStyle='#252629';context.textBaseline='top';for(let n=start;n<Math.min(lines.length,start+rows);n++)context.fillText(lines[n],margin,margin+(n-start)*size*1.7);sheets.push(canvas.toDataURL('image/png'));}return sheets.length?sheets:[canvas.toDataURL('image/png')];
}
async function exportTranslationPdf(result,bilingual){
 const generation=translationGeneration,session=pdfState;if(!session||result.itemId!==state.viewer?.id)throw Error('文件已切换。');await document.fonts.ready;await loadCreativePdfLibrary();const sourceBytes=await session.doc.getData();if(sourceBytes.length>32*1024*1024)throw Error('此 PDF 超过轻量编辑的大小限制。');const input=await PDFLib.PDFDocument.load(sourceBytes),output=await PDFLib.PDFDocument.create();
 const groups=new Map();result.parts.forEach((part,index)=>{const page=result.pages[index];if(!groups.has(page))groups.set(page,[]);groups.get(page).push(part);});
 for(const [pageNumber,parts]of groups){await new Promise(resolve=>requestAnimationFrame(resolve));if(generation!==translationGeneration)throw Error('文件已切换。');const original=input.getPage(pageNumber-1),view=(await session.doc.getPage(pageNumber)).getViewport({scale:1}),width=view.width,height=view.height;let embedded;
  if(bilingual){const sourceBox=original.getCropBox();embedded=await output.embedPage(original,{left:sourceBox.x,bottom:sourceBox.y,right:sourceBox.x+sourceBox.width,top:sourceBox.y+sourceBox.height});}
  for(const image of translationPdfSheets(parts.join('\n\n'),width,height)){const page=output.addPage([bilingual?width*2+18:width,height]),png=await output.embedPng(image);if(bilingual){const rotation=original.getRotation().angle%360;let x=0,y=0,w=width,h=height;if(rotation===90){x=0;y=height;w=height;h=width;}else if(rotation===180){x=width;y=height;}else if(rotation===270){x=width;y=0;w=height;h=width;}page.drawPage(embedded,{x,y,width:w,height:h,rotate:PDFLib.degrees(-rotation)});page.drawLine({start:{x:width+9,y:0},end:{x:width+9,y:height},thickness:.5,color:PDFLib.rgb(.8,.8,.8)});}page.drawImage(png,{x:bilingual?width+18:0,y:0,width,height});}
 }
 if(generation!==translationGeneration)throw Error('文件已切换。');const base64=await output.saveAsBase64();if(base64.length>44*1024*1024)throw Error('译文文件过大，请按章节或页面导出。');return native('saveTranslationArtifact',{id:result.itemId,format:'pdf',base64});
}

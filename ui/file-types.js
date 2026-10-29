'use strict';
// Original SVG artwork. Format recognition follows the native import registry;
// one small body per family, an individual format stamp and coherent material.
const FileTypes=(()=>{
 const groups={video:'MP4 M4V WEBM MOV MKV AVI WMV MPG MPEG TS M2TS FLV 3GP',photo:'JPG JPEG PNG GIF WEBP BMP AVIF TIF TIFF HEIC HEIF',music:'MP3 WAV OGG OGA FLAC M4A AAC OPUS WMA AIFF APE',document:'PDF DOCX CSV MD MARKDOWN LOG JSON HTML HTM DOC RTF XLSX XLS PPTX PPT',book:'EPUB TXT MOBI AZW AZW3 FB2',subtitle:'SRT VTT ASS SSA SUB SBV LRC TTML DFXP SMI',font:'TTF OTF'};
 const registry=new Map(),cache=new Map();for(const[kind,formats]of Object.entries(groups))formats.split(' ').forEach(ext=>registry.set(ext,kind));
 function describe(value,fallback='document'){const ext=String(value||'').toUpperCase().replace(/[^A-Z0-9]/g,'').slice(0,12)||'FILE',family=registry.get(ext)||(Object.hasOwn(groups,fallback)?fallback:'document');let hue=0;for(const ch of ext)hue=(hue*31+ch.charCodeAt(0))%360;return{ext,family,hue,known:registry.has(ext)};}
 function icon(value,fallback='document'){
  const {ext,family,hue}=describe(value,fallback),key=family+':'+ext;if(cache.has(key))return cache.get(key);
  const paper='<path class="type-body" d="M7 3h19l8 8v30H7z"/><path class="type-fold" d="M26 3v8h8"/>';
  let body=paper,detail='<path d="M13 17h15m-15 5h15"/>';
  if(family==='book'){body='<path class="type-body" d="M8 4h23v36H8a3 3 0 0 1-3-3V7a3 3 0 0 1 3-3Z"/><path class="type-fold" d="M10 5v29M8 35h23v5H8a2.5 2.5 0 0 1 0-5Z"/>';detail='<path d="M15 13h11m-11 5h11m-11 5h7"/>';}
  else if(family==='video'){body='<rect class="type-body" x="3" y="10" width="34" height="27" rx="3"/><path class="type-fold type-top" d="m3 10 32-6 1 7-32 6zM10 9l5 5m5-7 5 5m5-7 5 5"/>';detail='<path d="m17 19 9 5-9 5z"/>';}
  else if(family==='music'){body='<rect class="type-body" x="4" y="5" width="32" height="34" rx="3"/><circle class="type-fold" cx="20" cy="20" r="11"/>';detail='<path d="M13 19v3m5-7v10m5-13v14m5-9v7"/>';}
  else if(family==='photo'){body='<rect class="type-body" x="3" y="6" width="34" height="31" rx="2"/><rect class="type-fold" x="7" y="10" width="26" height="19" rx="1"/>';detail='<path d="m8 26 7-8 6 6 5-5 6 7"/><circle cx="27" cy="15" r="2"/>';}
  else if(family==='font')detail='<path d="m12 25 7-12 7 12m-11-4h8"/>';
  else if(family==='subtitle')detail='<rect x="11" y="15" width="18" height="10" rx="1"/><path d="M14 18h5m3 0h4m-12 4h4m3 0h5"/>';
  else if(['CSV','XLS','XLSX'].includes(ext))detail='<path d="M12 15h16v11H12zM12 19h16m-16 4h16m-11-8v11m6-11v11"/>';
  else if(['PPT','PPTX'].includes(ext))detail='<path d="M12 15h16v11H12zM15 23l4-5 3 3 3-4"/>';
  else if(['MD','MARKDOWN'].includes(ext))detail='<path d="M11 25V15l5 6 5-6v10m6-10v10m-3-3 3 3 3-3"/>';
  else if(['JSON','HTML','HTM'].includes(ext))detail='<path d="m15 15-5 5 5 5m10-10 5 5-5 5m-4-11-3 12"/>';
  else if(ext==='PDF')detail='<path d="M13 25c8-2 13-10 8-12-4-1-3 13 6 11m-14 1 13-2"/>';
  const label=ext==='MARKDOWN'?'MD':ext.length>5?ext.slice(0,5):ext;
  const svg=`<svg class="file-type-icon" data-file-format="${ext}" data-format-family="${family}" style="--type-hue:${hue}" viewBox="0 0 40 44" fill="none" aria-hidden="true" focusable="false">${body}<g class="type-detail" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">${detail}</g><text class="type-label" x="20" y="34" text-anchor="middle">${label}</text></svg>`;
  cache.set(key,svg);if(cache.size>128)cache.delete(cache.keys().next().value);return svg;
 }
 function animate(root){if(typeof CalmMotion==='undefined'||!CalmMotion.allowed())return;const icon=root.querySelector('.file-type-icon');if(!icon)return;const family=icon.dataset.formatFamily,part=icon.querySelector(family==='video'?'.type-top':'.type-detail');if(!part)return;
  const frames=family==='music'?[{transform:'scaleY(.72)'},{transform:'scaleY(1.05)',offset:.55},{transform:'none'}]:family==='book'?[{transform:'translateX(-1px)'},{transform:'none'}]:family==='video'?[{transform:'rotate(-8deg)'},{transform:'none'}]:family==='photo'?[{opacity:.6,transform:'translateY(1px)'},{opacity:1,transform:'none'}]:[{opacity:.65,transform:'translateY(1.5px)'},{opacity:1,transform:'none'}];
  CalmMotion.play(part,frames,{duration:family==='music'?280:210,channel:'file-type'});
 }
 if(typeof document!=='undefined'){document.addEventListener('pointerover',event=>{const root=event.target.closest('.file-card,.viewer-title,.format-icons>span');if(root&&!root.contains(event.relatedTarget))animate(root);});document.addEventListener('focusin',event=>{const root=event.target.closest('.file-card');if(root&&!root.contains(event.relatedTarget))animate(root);});}
 return{icon,describe,extensions:[...registry.keys()]};
})();
if(typeof module!=='undefined')module.exports=FileTypes;

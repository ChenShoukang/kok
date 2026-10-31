'use strict';
// One monochrome outline vocabulary. Format text stays distinct and sanitized.
const FileTypes=(()=>{
 const glyphs=typeof KokIcons!=='undefined'?KokIcons:require('./icons/lucide/icons.json');
 const groups={video:'MP4 M4V WEBM MOV MKV AVI WMV MPG MPEG TS M2TS FLV 3GP',photo:'JPG JPEG PNG GIF WEBP BMP AVIF TIF TIFF HEIC HEIF',music:'MP3 WAV OGG OGA FLAC M4A AAC OPUS WMA AIFF APE',document:'PDF DOCX CSV MD MARKDOWN LOG JSON HTML HTM DOC RTF XLSX XLS PPTX PPT',book:'EPUB TXT MOBI AZW AZW3 FB2',subtitle:'SRT VTT ASS SSA SUB SBV LRC TTML DFXP SMI',font:'TTF OTF'};
 const registry=new Map(),cache=new Map();for(const[kind,formats]of Object.entries(groups))formats.split(' ').forEach(ext=>registry.set(ext,kind));
 function describe(value,fallback='document'){
  const ext=String(value||'').toUpperCase().replace(/[^A-Z0-9]/g,'').slice(0,12)||'FILE';
  const family=registry.get(ext)||(Object.hasOwn(groups,fallback)?fallback:'document');
  return{ext,family,hue:0,known:registry.has(ext)};
 }
 const paths={
  video:'M3 5h18v14H3z M10 9l5 3-5 3z',
  photo:'M3 4h18v16H3z M3 16l5-5 5 5 3-3 5 5 M16 8h.01',
  music:'M9 18V5l12-2v13 M9 8l12-2 M9 18c0 2-2 3-4 3s-3-1-3-2 2-3 4-3 3 1 3 2 M21 16c0 2-2 3-4 3s-3-1-3-2 2-3 4-3 3 1 3 2',
  book:'M12 5C8 2 3 3 3 3v16s5-1 9 2c4-3 9-2 9-2V3s-5-1-9 2z M12 5v16',
  document:'M14 3H5v18h14V8z M14 3v5h5 M8 12h8 M8 16h6',
  subtitle:'M3 5h18v14H3z M6 10h5 M14 10h4 M6 14h3 M12 14h6',
  font:'M5 20L12 4l7 16 M8 14h8'
 };
 function icon(value,fallback='document'){
  const {ext,family}=describe(value,fallback),key=family+':'+ext;if(cache.has(key))return cache.get(key);
  const label=ext==='MARKDOWN'?'MD':ext.slice(0,5);
  const result=`<svg class="file-type-icon" data-file-format="${ext}" data-format-family="${family}" viewBox="0 0 40 44" fill="none" aria-hidden="true" focusable="false"><g class="type-detail" transform="translate(7 2) scale(1.1)" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">${glyphs[family==='font'?'type':family]}</g><text class="type-label" x="20" y="40" text-anchor="middle">${label}</text></svg>`;
  cache.set(key,result);if(cache.size>128)cache.delete(cache.keys().next().value);return result;
 }
 return{icon,describe,extensions:[...registry.keys()]};
})();
if(typeof module!=='undefined')module.exports=FileTypes;

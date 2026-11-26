'use strict';
// Shared outline navigation and flat editorial graphic covers. Real artwork stays intact.

Kok.extend('core','graphite.js:395','navigationIcon',()=>function(kind){return glyph(kind==='grid'?'grid':kind);});

function flatEditorialCover(i){
 const ext=String(i.ext||'').toUpperCase();
 if(i.kind==='book'){
  const tone=ext==='EPUB'?'epub':'txt';
  return `<div class="flat-file-cover flat-file-book" data-cover-tone="${tone}">
   <svg class="editorial-cover-art" viewBox="0 0 200 150" fill="none" aria-hidden="true">
    <rect x="34" y="16" width="132" height="118" rx="6" class="ec-sheet"/>
    <path d="M34 22a6 6 0 0 1 6-6h18v118H40a6 6 0 0 1-6-6V22Z" class="ec-accent"/>
    <rect x="70" y="34" width="62" height="7" rx="2" class="ec-ink"/>
    <rect x="70" y="48" width="42" height="5" rx="2" class="ec-muted"/>
    <circle cx="116" cy="94" r="22" class="ec-accent-soft"/>
    <path d="M94 94a22 22 0 0 1 44 0H94Z" class="ec-accent"/>
    <rect x="70" y="116" width="28" height="3.5" rx="1.5" class="ec-muted"/>
   </svg>
   <span class="flat-cover-badge" data-user-content>${esc(ext||'BOOK')}</span>
  </div>`;
 }
 if(i.kind==='music'){
  return `<div class="flat-file-cover flat-file-music" data-cover-tone="audio">
   <svg class="editorial-cover-art" viewBox="0 0 200 150" fill="none" aria-hidden="true">
    <circle cx="100" cy="72" r="44" class="ec-accent-soft"/>
    <circle cx="100" cy="72" r="28" class="ec-sheet"/>
    <g class="ec-accent-stroke" stroke-width="4.5" stroke-linecap="round">
     <line x1="68" y1="64" x2="68" y2="80"/>
     <line x1="78" y1="54" x2="78" y2="90"/>
     <line x1="89" y1="45" x2="89" y2="99"/>
     <line x1="100" y1="38" x2="100" y2="106"/>
     <line x1="111" y1="48" x2="111" y2="96"/>
     <line x1="122" y1="56" x2="122" y2="88"/>
     <line x1="132" y1="65" x2="132" y2="79"/>
    </g>
   </svg>
   <span class="flat-cover-badge" data-user-content>${esc(ext||'AUDIO')}</span>
   ${i.hasCover?`<img src="${media(i,'thumb')}?v=${encodeURIComponent(i.coverVersion||'1')}" alt="" loading="lazy" data-fallback="music">`:''}
  </div>`;
 }
 const tone=ext==='PDF'?'pdf':ext==='PPTX'||ext==='PPT'?'slide':ext==='DOCX'||ext==='DOC'?'doc':'md';
 return `<div class="flat-file-cover flat-file-document" data-cover-tone="${tone}">
  <svg class="editorial-cover-art" viewBox="0 0 200 150" fill="none" aria-hidden="true">
   <rect x="42" y="14" width="116" height="122" rx="6" class="ec-sheet"/>
   <rect x="42" y="14" width="116" height="10" rx="3" class="ec-accent"/>
   <rect x="58" y="38" width="56" height="7" rx="2" class="ec-ink"/>
   <rect x="58" y="54" width="84" height="4.5" rx="2" class="ec-muted"/>
   <rect x="58" y="65" width="76" height="4.5" rx="2" class="ec-muted"/>
   <rect x="58" y="76" width="60" height="4.5" rx="2" class="ec-muted"/>
   <rect x="58" y="92" width="84" height="24" rx="4" class="ec-accent-soft"/>
   <rect x="66" y="100" width="38" height="4" rx="1.5" class="ec-accent"/>
   <rect x="66" y="108" width="54" height="3.5" rx="1.5" class="ec-muted"/>
  </svg>
  <span class="flat-cover-badge" data-user-content>${esc(ext||'DOC')}</span>
 </div>`;
}

Kok.extend('core','graphite.js:coverBeforeGraphite','cover',coverBeforeGraphite=>function(i){
 if(['music','book','document'].includes(i.kind))return flatEditorialCover(i);
 return coverBeforeGraphite(i);
});

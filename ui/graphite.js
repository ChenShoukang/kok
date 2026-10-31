'use strict';
// Shared outline navigation and neutral file placeholders. Real artwork stays intact.

Kok.extend('core','graphite.js:395','navigationIcon',()=>function(kind){return svg(kind==='grid'?'grid':kind);});


Kok.extend('core','graphite.js:coverBeforeGraphite','cover',coverBeforeGraphite=>function(i){
 if(['music','book','document'].includes(i.kind))return `<div class="flat-file-cover flat-file-${i.kind}"><span class="flat-file-symbol" aria-hidden="true">${svg(i.kind)}</span><small data-user-content>${esc(i.ext)}</small>${i.kind==='music'&&i.hasCover?`<img src="${media(i,'thumb')}?v=${encodeURIComponent(i.coverVersion||'1')}" alt="" loading="lazy" data-fallback="music">`:''}</div>`;
 return coverBeforeGraphite(i);
});

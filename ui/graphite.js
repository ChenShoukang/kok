'use strict';
// v0.7.1 Graphite: monochrome navigation and typographic covers in place of the emoji objects.
const graphiteCoverColors=['--covers-1','--covers-2','--covers-3','--covers-4','--covers-5'];
function graphiteHue(text){let hash=2166136261;for(const ch of String(text))hash=Math.imul(hash^ch.codePointAt(0),16777619)>>>0;return graphiteCoverColors[hash%graphiteCoverColors.length];}

Kok.extend('core','graphite.js:395','navigationIcon',()=>function(kind){return svg(kind==='grid'?'grid':kind);});


Kok.extend('core','graphite.js:coverBeforeGraphite','cover',coverBeforeGraphite=>function(i){
 if(i.kind==='music')return `<div class="graphite-cover graphite-record" style="--cover:var(${graphiteHue(i.name)})"><span class="graphite-disc"></span>${i.hasCover?`<img src="${media(i,'thumb')}?v=${encodeURIComponent(i.coverVersion||'1')}" alt="" loading="lazy" data-fallback="music">`:''}</div>`;
 if(i.kind==='book')return `<div class="graphite-cover graphite-book" style="--cover:var(${graphiteHue(i.name)})"><b data-user-content>${esc(i.name)}</b><i></i></div>`;
 if(i.kind==='document')return `<div class="graphite-cover graphite-document" style="--cover:var(${graphiteHue(i.name)})"><small data-user-content>${esc(i.ext)}</small><b data-user-content>${esc(i.name)}</b><span class="graphite-page-lines" aria-hidden="true"></span></div>`;
 return coverBeforeGraphite(i);
});

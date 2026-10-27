'use strict';
// v0.6.2 Graphite: monochrome navigation and typographic covers in place of the emoji objects.
const graphiteCoverColors=['--covers-1','--covers-2','--covers-3','--covers-4','--covers-5'];
function graphiteHue(text){let hash=2166136261;for(const ch of String(text))hash=Math.imul(hash^ch.codePointAt(0),16777619)>>>0;return graphiteCoverColors[hash%graphiteCoverColors.length];}

navigationIcon=function(kind){return svg(kind==='grid'?'grid':kind);};

const coverBeforeGraphite=cover;
cover=function(i){
 if(i.kind==='music')return `<div class="graphite-cover graphite-record" style="--cover:var(${graphiteHue(i.name)})"><span class="graphite-disc"></span>${i.hasCover?`<img src="${media(i,'thumb')}?v=${encodeURIComponent(i.coverVersion||'1')}" alt="" loading="lazy" data-fallback="music">`:''}</div>`;
 if(i.kind==='book'||i.kind==='document')return `<div class="graphite-cover graphite-book" style="--cover:var(${graphiteHue(i.name)})"><b data-user-content>${esc(i.name)}</b><i></i></div>`;
 return coverBeforeGraphite(i);
};

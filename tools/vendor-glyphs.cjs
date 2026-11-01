'use strict';
// Product/navigation glyphs are an offline, pinned Phosphor duotone subset.
// Compact action controls continue using the existing Lucide outline set.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'..'),commit='2b75f3ad12b420c9504ef05df8d2564a28f8500e',source=`https://raw.githubusercontent.com/phosphor-icons/core/${commit}/`;
const aliases={grid:'squares-four',video:'film-strip',photo:'image',music:'music-notes',document:'file-text',book:'book-open',folder:'folder-simple',calendar:'calendar-dots',agent:'sparkle',spark:'sparkle',heart:'heart',clock:'clock-counter-clockwise',edit:'note-pencil',layers:'stack',network:'graph',search:'magnifying-glass',download:'download-simple',undo:'arrow-u-up-left',list:'list-checks',lock:'lock-key',home:'house',bookmark:'bookmark-simple'};
const hash=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');
const offline=process.argv.includes('--offline');
(async()=>{
 const dir=path.join(root,'ui/icons/phosphor');fs.mkdirSync(dir,{recursive:true});
 const files=[],bodies={};let cursor=0;const names=[...new Set(Object.values(aliases))];
 await Promise.all(Array.from({length:5},async()=>{while(cursor<names.length){const name=names[cursor++],url=source+'assets/duotone/'+name+'-duotone.svg';let svg;
  if(offline)svg=fs.readFileSync(path.join(dir,name+'.svg'),'utf8');else{const response=await fetch(url);if(!response.ok)throw Error(name+': '+response.status);svg=await response.text();}
  if(!svg.includes('viewBox="0 0 256 256"')||/<(?:script|image|foreignObject|use|style)|\bon\w+=|(?:href|url)\s*[=(]/i.test(svg))throw Error('Unexpected SVG content: '+name);
  const body=svg.slice(svg.indexOf('>')+1,svg.lastIndexOf('</svg>')).trim();if(!body)throw Error('Empty glyph: '+name);
  bodies[name]=body;files.push({name,url,sha256:hash(svg)});fs.writeFileSync(path.join(dir,name+'.svg'),svg);
 }}));
 const glyphs=Object.fromEntries(Object.entries(aliases).map(([key,name])=>[key,bodies[name]]));
 fs.writeFileSync(path.join(dir,'glyphs.js'),'/* Phosphor Icons, MIT; pinned '+commit+'. See docs/licenses/Phosphor-MIT.txt. */\nglobalThis.KokGlyphs=Object.freeze('+JSON.stringify(glyphs)+');\n');
 fs.writeFileSync(path.join(dir,'manifest.json'),JSON.stringify({commit,source,license:'docs/licenses/Phosphor-MIT.txt',aliases,files:files.sort((a,b)=>a.name.localeCompare(b.name))},null,2)+'\n');
 if(!offline){const license=await fetch(source+'LICENSE');if(!license.ok)throw Error('Missing Phosphor license');fs.writeFileSync(path.join(root,'docs/licenses/Phosphor-MIT.txt'),await license.text());}
 else if(!fs.existsSync(path.join(root,'docs/licenses/Phosphor-MIT.txt')))throw Error('Missing Phosphor license');
 console.log(`Vendored ${files.length} original Phosphor duotone SVGs for ${Object.keys(aliases).length} product glyphs.`);
})().catch(error=>{console.error(error);process.exitCode=1;});

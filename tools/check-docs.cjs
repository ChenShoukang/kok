const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..');
const files=fs.readdirSync(root).filter(name=>name.endsWith('.md')).map(name=>path.join(root,name));
function walk(folder){for(const entry of fs.readdirSync(folder,{withFileTypes:true})){const file=path.join(folder,entry.name);if(entry.isDirectory())walk(file);else if(entry.name.endsWith('.md'))files.push(file);}}
walk(path.join(root,'docs'));const missing=[];let count=0;
function check(file,href){
 if(/^(?:[a-z]+:|#|\/)/i.test(href))return;
 const target=decodeURIComponent(href.split('#')[0]);if(!target)return;count++;
 if(!fs.existsSync(path.resolve(path.dirname(file),target)))missing.push(path.relative(root,file)+': '+href);
}
for(const file of files){
 const text=fs.readFileSync(file,'utf8');
 for(const match of text.matchAll(/\]\(([^\s)]+)(?:\s+"[^"]*")?\)/g))check(file,match[1]);
 for(const tag of text.matchAll(/<(?:img|source)\b[^>]*>/gi))for(const attr of tag[0].matchAll(/\b(src|srcset)\s*=\s*(["'])(.*?)\2/gi)){
  if(attr[1].toLowerCase()==='srcset')for(const source of attr[3].split(','))check(file,source.trim().split(/\s+/)[0]);else check(file,attr[3]);
 }
}
assert.deepEqual(missing,[],'Documentation links must resolve locally');console.log(`Documentation links valid: ${count} local targets.`);

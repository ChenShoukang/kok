const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..');
const files=fs.readdirSync(root).filter(name=>name.endsWith('.md')).map(name=>path.join(root,name));
function walk(folder){for(const entry of fs.readdirSync(folder,{withFileTypes:true})){const file=path.join(folder,entry.name);if(entry.isDirectory())walk(file);else if(entry.name.endsWith('.md'))files.push(file);}}
walk(path.join(root,'docs'));const missing=[];let count=0;
for(const file of files)for(const match of fs.readFileSync(file,'utf8').matchAll(/\]\(([^\s)]+)(?:\s+"[^"]*")?\)/g)){
 const href=match[1];if(/^(?:[a-z]+:|#|\/)/i.test(href))continue;
 const target=decodeURIComponent(href.split('#')[0]);if(!target)continue;count++;
 if(!fs.existsSync(path.resolve(path.dirname(file),target)))missing.push(path.relative(root,file)+': '+href);
}
assert.deepEqual(missing,[],'Documentation links must resolve locally');console.log(`Documentation links valid: ${count} local targets.`);

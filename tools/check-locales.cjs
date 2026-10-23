const fs=require('node:fs');
const assert=require('node:assert/strict');
const catalog=JSON.parse(fs.readFileSync('ui/locales/en.json','utf8'));
const patterns=catalog.patterns.map(row=>new RegExp(row.pattern,'s'));
assert(Object.keys(catalog.messages).length>250);
for(const [source,target]of Object.entries(catalog.messages)){assert(source.trim());assert.equal(typeof target,'string');assert(target.trim());}
const missing=[];
for(const file of ['src/Program.cs','src/Library.cs','src/Subtitles.cs','src/Translation.cs']){
 const text=fs.readFileSync(file,'utf8');
 for(const match of text.matchAll(/throw new \w+\("((?:\\.|[^"\\])*)"\s*[,)]/g)){
   const value=JSON.parse('"'+match[1]+'"');if(/[\u3400-\u9fff]/.test(value)&&!catalog.messages[value]&&!patterns.some(re=>re.test(value)))missing.push(file+': '+value);
 }
}
assert.deepEqual(missing,[],'Native error text must have an English translation');
console.log(`Locale catalog valid: ${Object.keys(catalog.messages).length} messages and ${patterns.length} dynamic patterns.`);

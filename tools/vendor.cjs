// Bundle only the browser assets required by PDF.js; no network or runtime npm.
const fs=require('node:fs');
const path=require('node:path');
const root=path.resolve(__dirname,'..');
const source=path.join(root,'node_modules','pdfjs-dist');
const target=path.join(root,'ui','vendor','pdfjs');
fs.mkdirSync(target,{recursive:true});
for(const file of ['pdf.mjs','pdf.worker.mjs'])fs.copyFileSync(path.join(source,'build',file),path.join(target,file));
for(const folder of ['cmaps','standard_fonts','wasm','iccs'])fs.cpSync(path.join(source,folder),path.join(target,folder),{recursive:true});
fs.copyFileSync(path.join(source,'LICENSE'),path.join(target,'LICENSE'));
for(const file of ['NOTICE','README.md'])if(fs.existsSync(path.join(source,file)))fs.copyFileSync(path.join(source,file),path.join(target,file));
console.log('Bundled PDF.js '+require(path.join(source,'package.json')).version);

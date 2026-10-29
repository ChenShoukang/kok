'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..'),manifest=require('./presentation-vendor.json'),file=path.join(root,manifest.file),source=path.join(root,'node_modules/pptxgenjs/dist/pptxgen.bundle.js');
if(!fs.existsSync(file)){assert.equal(require(path.join(root,'node_modules/pptxgenjs/package.json')).version,manifest.version);fs.mkdirSync(path.dirname(file),{recursive:true});fs.copyFileSync(source,file);}
const data=fs.readFileSync(file);assert.equal(crypto.createHash('sha256').update(data).digest('hex'),manifest.sha256,'PPTX export bundle checksum mismatch');
const license=path.join(root,'docs/licenses/PptxGenJS-MIT.txt');if(!fs.existsSync(license))fs.copyFileSync(path.join(root,'node_modules/pptxgenjs/LICENSE'),license);
assert(fs.readFileSync(license,'utf8').includes('Permission is hereby granted'));console.log('Verified on-demand PptxGenJS '+manifest.version+' browser bundle ('+data.length+' bytes).');

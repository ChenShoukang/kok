'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const {writeIfChanged}=require('./asset-sync.cjs');
const root=path.resolve(__dirname,'..'),manifest=require('./sandbox-vendor.json'),destination=path.join(root,'ui/vendor/quickjs');
for(const row of manifest.files){
 const source=path.join(root,'node_modules',row.source),raw=fs.readFileSync(source);assert.equal(crypto.createHash('sha256').update(raw).digest('hex'),row.sha256,'QuickJS source checksum mismatch: '+row.source);
 let data=raw;if(row.target.endsWith('.mjs'))data=Buffer.from(raw.toString('utf8').replaceAll('@jitl/quickjs-ffi-types','../types/index.mjs').replaceAll('@jitl/quickjs-wasmfile-release-sync/emscripten-module','./emscripten-module.browser.mjs'));
 const target=path.join(destination,row.target);writeIfChanged(target,data);
}
writeIfChanged(path.join(root,'docs/licenses/QuickJS-Emscripten-MIT.txt'),fs.readFileSync(path.join(root,'node_modules/@jitl/quickjs-wasmfile-release-sync/LICENSE')));
console.log('Verified offline QuickJS '+manifest.version+' WebAssembly assets ('+manifest.files.length+' files).');

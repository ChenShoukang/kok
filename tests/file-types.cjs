'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),types=require('../ui/file-types.js');
const registry=fs.readFileSync('src/Compatibility.cs','utf8');let count=0;const artwork=new Set();
for(const row of registry.matchAll(/new FormatGroup\("([a-z]+)","[a-z]+","([a-z0-9 ]+)"/g))for(const ext of row[2].toUpperCase().split(' ')){
 const info=types.describe(ext);assert(info.known);assert.equal(info.family,row[1]);const icon=types.icon(ext);assert(icon.startsWith('<svg '));assert(icon.includes('viewBox="0 0 40 44"'));assert(icon.includes('data-file-format="'+ext+'"'));assert(!icon.includes('<image'));artwork.add(icon);count++;
}
assert.equal(artwork.size,count,'Every supported extension must retain its own recognizable stamp');
assert(!types.icon('><script>alert(1)</script>','"><iframe>').includes('<script'));assert.equal(types.describe('unknown','"><iframe>').family,'document');
for(const ext of ['SRT','VTT','ASS','TTF','OTF'])assert(types.describe(ext).known);
console.log(`PASS: ${count} import formats have individual SVG stamps, consistent bounds, subtitle/font coverage and safe unknown-format fallback.`);

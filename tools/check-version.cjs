'use strict';
const fs=require('node:fs'),assert=require('node:assert/strict'),version=JSON.parse(fs.readFileSync('package.json')).version,assembly=fs.readFileSync('src/AssemblyInfo.cs','utf8');
for(const key of ['AssemblyVersion','AssemblyFileVersion'])assert.equal(assembly.match(new RegExp(key+'\\("([^"]+)"\\)'))?.[1],version+'.0',key+' must match package.json');
assert.equal(assembly.match(/AssemblyInformationalVersion\("([^"]+)"\)/)?.[1],version);
assert.equal(JSON.parse(fs.readFileSync('browser-extension/manifest.json')).version,version);
for(const file of ['src/LegacyBridge.cs','src/McpTransport.cs','src/Program.cs'])assert(!/version\s*=\s*"?\d+\.\d+\.\d+/.test(fs.readFileSync(file,'utf8')),'Runtime version must use AppVersion: '+file);
console.log('Version contracts match '+version+'.');

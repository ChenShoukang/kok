'use strict';
const fs=require('node:fs'),assert=require('node:assert/strict'),version=JSON.parse(fs.readFileSync('package.json')).version,assembly=fs.readFileSync('src/AssemblyInfo.cs','utf8');
for(const key of ['AssemblyVersion','AssemblyFileVersion'])assert.equal(assembly.match(new RegExp(key+'\\("([^"]+)"\\)'))?.[1],version+'.0',key+' must match package.json');
assert.equal(assembly.match(/AssemblyInformationalVersion\("([^"]+)"\)/)?.[1],version);
assert.equal(JSON.parse(fs.readFileSync('browser-extension/manifest.json')).version,version);
for(const file of ['src/LegacyBridge.cs','src/McpTransport.cs','src/Program.cs'])assert(!/version\s*=\s*"?\d+\.\d+\.\d+/.test(fs.readFileSync(file,'utf8')),'Runtime version must use AppVersion: '+file);
for(const file of ['README.md','README.en.md'])assert(fs.readFileSync(file,'utf8').includes('kok-'+version+'-win-x64.zip'),file+' must name the current release ZIP kok-'+version+'-win-x64.zip');
console.log('Version contracts match '+version+'.');

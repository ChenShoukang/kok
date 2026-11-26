'use strict';
const fs=require('node:fs'),assert=require('node:assert/strict'),version=JSON.parse(fs.readFileSync('package.json')).version,assembly=fs.readFileSync('src/AssemblyInfo.cs','utf8');
for(const key of ['AssemblyVersion','AssemblyFileVersion'])assert.equal(assembly.match(new RegExp(key+'\\("([^"]+)"\\)'))?.[1],version+'.0',key+' must match package.json');
assert.equal(assembly.match(/AssemblyInformationalVersion\("([^"]+)"\)/)?.[1],version);
assert.equal(JSON.parse(fs.readFileSync('browser-extension/manifest.json')).version,version);
const research=JSON.parse(fs.readFileSync('runtime/research/package.json')),researchLock=JSON.parse(fs.readFileSync('runtime/research/package-lock.json'));
assert.equal(research.version,version,'Research runtime must match the application version');
assert.equal(researchLock.version,version);assert.equal(researchLock.packages[''].version,version);
for(const file of ['src/LegacyBridge.cs','src/McpTransport.cs','src/Program.cs'])assert(!/version\s*=\s*"?\d+\.\d+\.\d+/.test(fs.readFileSync(file,'utf8')),'Runtime version must use AppVersion: '+file);
const downloads=['README.md','README.en.md'].map(file=>{
  const readme=fs.readFileSync(file,'utf8'), match=readme.match(/kok-(\d+\.\d+\.\d+)-win-x64\.zip/);
  assert(match,file+' must name the public Windows ZIP');
  assert(readme.includes('/releases/tag/v'+match[1]+')'),file+' must link the release that owns the named ZIP');
  return match[1];
});
assert.equal(downloads[0],downloads[1],'Both download guides must refer to the same release');
if(JSON.parse(fs.readFileSync('package.json')).release.publish)assert.equal(downloads[0],version,'Enable publishing only with download instructions for the new release');
console.log('Version contracts match '+version+'.');

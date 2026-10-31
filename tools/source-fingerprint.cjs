'use strict';
// Fingerprint runtime source/tests separately from prose and generated verification records.
// Never enumerate user profiles, environment values or credential files.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'..'),extensions=new Set(['.cs','.js','.mjs','.cjs','.css','.json','.ps1','.html','.yml']);
const digest=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
function walk(folder,skip=()=>false){return fs.readdirSync(folder,{withFileTypes:true}).flatMap(e=>{const file=path.join(folder,e.name);if(skip(file,e)||e.isSymbolicLink())return[];return e.isDirectory()?walk(file,skip):[file];});}
function hash(files,base){const rows=files.map(file=>({name:path.relative(base,file).split(path.sep).join('/'),sha256:digest(file)})).sort((a,b)=>a.name.localeCompare(b.name,'en'));return{count:rows.length,sha256:crypto.createHash('sha256').update(rows.map(r=>r.name+'\0'+r.sha256+'\n').join('')).digest('hex')};}
const files=['src','ui','tests','tools','browser-extension'].flatMap(name=>walk(path.join(root,name),(file,e)=>e.isDirectory()&&['vendor','fonts','icons','webview2','sqlite','roslyn','youtube-source','media-source'].includes(e.name))).filter(file=>extensions.has(path.extname(file))&&!path.basename(file).startsWith('bundle-'));
for(const name of ['package.json','package-lock.json','build.ps1','package.ps1'])files.push(path.join(root,name));
const result={source:hash(files,root)};
const index=process.argv.indexOf('--runtime');if(index>=0){const dir=path.resolve(process.argv[index+1]);const binaries=fs.readdirSync(dir,{withFileTypes:true}).filter(e=>e.isFile()&&(e.name==='kok.exe'||e.name.endsWith('.dll')||e.name.endsWith('.config'))).map(e=>path.join(dir,e.name));result.runtime=hash([...binaries,...walk(path.join(dir,'ui'))],dir);result.executableSha256=digest(path.join(dir,'kok.exe'));}
console.log(JSON.stringify(result));

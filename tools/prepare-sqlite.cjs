'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),{execFileSync}=require('node:child_process');
const spec=JSON.parse(fs.readFileSync(path.join(__dirname,'sqlite-source.json'),'utf8'));
const cache=path.resolve(process.env.KOK_SQLITE_CACHE_DIR||path.join(__dirname,'sqlite')),archive=path.join(cache,'sqlite.zip');
(async()=>{
 fs.mkdirSync(cache,{recursive:true});
 if(!fs.existsSync(archive)){const reply=await fetch(spec.url);if(!reply.ok)throw Error('SQLite download failed');fs.writeFileSync(archive,Buffer.from(await reply.arrayBuffer()));}
 const bytes=fs.readFileSync(archive);
 if(crypto.createHash('sha3-256').update(bytes).digest('hex')!==spec.sha3||crypto.createHash('sha256').update(bytes).digest('hex')!==spec.sha256)throw Error('SQLite archive checksum mismatch');
 // Use the Framework ZIP API directly: a Node child can inherit PowerShell 7 module paths that
 // prevent Windows PowerShell from loading Expand-Archive. Extract only the pinned root entries.
 const unpack="$ErrorActionPreference='Stop'; Add-Type -AssemblyName System.IO.Compression.FileSystem; $zip=[IO.Compression.ZipFile]::OpenRead($env:KOK_SQLITE_ARCHIVE); try { foreach($name in @('sqlite3.dll','sqlite3.def')) { $entry=$zip.GetEntry($name); if($null -eq $entry){throw 'SQLite archive entry missing.'}; [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $env:KOK_SQLITE_CACHE $name),$true) } } finally { $zip.Dispose() }";
 execFileSync('powershell.exe',['-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-Command',unpack],{env:{...process.env,KOK_SQLITE_ARCHIVE:archive,KOK_SQLITE_CACHE:cache},windowsHide:true,stdio:'pipe'});
 const dll=fs.readFileSync(path.join(cache,'sqlite3.dll'));
 if(dll.length<64||dll.readUInt16LE(0)!==0x5a4d)throw Error('SQLite DLL extraction failed: missing PE header');
 const pe=dll.readUInt32LE(0x3c);
 if(pe+6>dll.length||dll.readUInt32LE(pe)!==0x4550||dll.readUInt16LE(pe+4)!==0x8664)throw Error('SQLite DLL extraction failed: expected Windows x64');
 console.log('SQLite '+spec.version+' official archive and extracted x64 DLL verified.');
})().catch(e=>{console.error(e.message);process.exitCode=1;});

'use strict';
// W3C WOFF2 null transforms + Node's Brotli: no glyph or layout data changes.
// https://www.w3.org/TR/WOFF2/#table_dir_format
const fs=require('node:fs'),path=require('node:path'),zlib=require('node:zlib'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const tags=['cmap','head','hhea','hmtx','maxp','name','OS/2','post','cvt ','fpgm','glyf','loca','prep','CFF ','VORG','EBDT','EBLC','gasp','hdmx','kern','LTSH','PCLT','VDMX','vhea','vmtx','BASE','GDEF','GPOS','GSUB','EBSC','JSTF','MATH','CBDT','CBLC','COLR','CPAL','SVG ','sbix','acnt','avar','bdat','bloc','bsln','cvar','fdsc','feat','fmtx','fvar','gvar','hsty','just','lcar','mort','morx','opbd','prop','trak','Zapf','Silf','Glat','Gloc','Feat','Sill'];
const sha=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');
function base128(value){const bytes=[value&127];while(value=Math.floor(value/128))bytes.unshift((value&127)|128);return Buffer.from(bytes);}
function readBase128(bytes,state){let value=0;for(let n=0;n<5;n++){const b=bytes[state.at++];assert(b!==undefined&&!(n===0&&b===128));value=value*128+(b&127);assert(value<=0xffffffff);if(!(b&128))return value;}throw Error('Invalid UIntBase128');}
function sfntTables(font){assert.equal(font.readUInt32BE(0),0x10000,'Only standalone TrueType fonts are accepted');const n=font.readUInt16BE(4);assert(n>0&&12+n*16<=font.length);const tables=[];for(let i=0;i<n;i++){const at=12+i*16,tag=font.toString('ascii',at,at+4),offset=font.readUInt32BE(at+8),length=font.readUInt32BE(at+12);assert(offset+length<=font.length);tables.push({tag,data:font.subarray(offset,offset+length)});}assert.equal(new Set(tables.map(t=>t.tag)).size,n);return tables;}
function compressionTables(font){return sfntTables(font).filter(t=>t.tag!=='DSIG').map(t=>{if(t.tag!=='head')return t;const data=Buffer.from(t.data);data.writeUInt16BE(data.readUInt16BE(16)|0x800,16);return{tag:t.tag,data};});}
function compress(font){const tables=compressionTables(font),directory=[];for(const t of tables){const index=tags.indexOf(t.tag),transform=['glyf','loca'].includes(t.tag)?3:0;directory.push(Buffer.from([(transform<<6)|(index<0?63:index)]));if(index<0)directory.push(Buffer.from(t.tag,'ascii'));directory.push(base128(t.data.length));}
 const dir=Buffer.concat(directory),raw=Buffer.concat(tables.map(t=>t.data)),compressed=zlib.brotliCompressSync(raw,{params:{[zlib.constants.BROTLI_PARAM_MODE]:zlib.constants.BROTLI_MODE_FONT,[zlib.constants.BROTLI_PARAM_QUALITY]:11}}),head=Buffer.alloc(48);
 const padding=Buffer.alloc((4-(head.length+dir.length+compressed.length)%4)%4);
 head.write('wOF2',0,'ascii');head.writeUInt32BE(0x10000,4);head.writeUInt32BE(head.length+dir.length+compressed.length+padding.length,8);head.writeUInt16BE(tables.length,12);head.writeUInt32BE(12+16*tables.length+tables.reduce((n,t)=>n+((t.data.length+3)&~3),0),16);head.writeUInt32BE(compressed.length,20);head.writeUInt16BE(1,24);
 return Buffer.concat([head,dir,compressed,padding]);
}
function unpack(bytes){assert.equal(bytes.toString('ascii',0,4),'wOF2');assert.equal(bytes.readUInt32BE(8),bytes.length);const state={at:48},entries=[];for(let n=0;n<bytes.readUInt16BE(12);n++){const flags=bytes[state.at++],index=flags&63;let tag=tags[index];if(index===63){tag=bytes.toString('ascii',state.at,state.at+4);state.at+=4;}assert(tag);assert.equal(flags>>6,['glyf','loca'].includes(tag)?3:0,'This verifier accepts only untransformed tables');entries.push({tag,length:readBase128(bytes,state)});}
 const raw=zlib.brotliDecompressSync(bytes.subarray(state.at,state.at+bytes.readUInt32BE(20)));assert.equal(raw.length,entries.reduce((n,t)=>n+t.length,0));let at=0;return entries.map(t=>{const data=raw.subarray(at,at+t.length);at+=t.length;return{tag:t.tag,data};});
}
function verify(original,woff){const before=compressionTables(original),after=unpack(woff);assert.deepEqual(after.map(t=>t.tag),before.map(t=>t.tag));return before.map((t,n)=>{assert(t.data.equals(after[n].data),'Unexpected table changes: '+t.tag);return{tag:t.tag,bytes:t.data.length,sha256:sha(t.data)};});}
if(require.main===module){
 const at=process.argv.indexOf('--source-dir');if(at<0||!process.argv[at+1])throw Error('Usage: node tools/compress-fonts.cjs --source-dir <pinned original TTF directory>');
 const root=path.resolve(__dirname,'..'),source=path.resolve(process.argv[at+1]),target=path.join(root,'ui','fonts'),manifestFile=path.join(target,'manifest.json'),manifest=JSON.parse(fs.readFileSync(manifestFile,'utf8'));
 const report={format:'WOFF2 with null transforms',node:process.version,brotli:process.versions.brotli,subsetting:false,formatChanges:['DSIG removed per WOFF2 section 5','head.flags bit 11 set per WOFF2 section 5'],glyphsAndLayoutChanged:false,fonts:[]};
 for(const entry of manifest.files.filter(f=>f.id!=='smiley')){
  const name=entry.sourceFile||entry.file,original=fs.readFileSync(path.join(source,name)),expected=entry.sourceSha256||entry.sha256;assert.equal(sha(original),expected,'Pinned upstream bytes differ: '+name);
  const output=compress(original),tables=verify(original,output),file=name.replace(/\.ttf$/i,'.woff2');fs.writeFileSync(path.join(target,file),output);
  if(!entry.sourceFile){entry.sourceFile=name;entry.sourceSha256=entry.sha256;entry.sourceBytes=entry.bytes;if(entry.gitBlob){entry.sourceGitBlob=entry.gitBlob;delete entry.gitBlob;}}
  Object.assign(entry,{file,bytes:output.length,sha256:sha(output),compression:'WOFF2 null transforms; full glyphs/layout/metadata preserved; required DSIG removal and head.flags bit 11'});report.fonts.push({file,sourceFile:name,sourceSha256:expected,sha256:entry.sha256,sourceBytes:original.length,bytes:output.length,glyphs:sfntTables(original).find(t=>t.tag==='maxp').data.readUInt16BE(4),tables});
  console.log(name+': '+original.length+' -> '+output.length+' bytes; '+tables.length+' tables verified against original plus required format flags');
 }
 fs.writeFileSync(manifestFile,JSON.stringify(manifest,null,2)+'\n');fs.writeFileSync(path.join(target,'compression-verification.json'),JSON.stringify(report,null,2)+'\n');
}
module.exports={compress,unpack,verify};

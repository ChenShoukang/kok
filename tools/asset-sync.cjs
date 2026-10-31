'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
function writeIfChanged(file,data,io=fs){
 data=Buffer.from(data);
 try{if(io.readFileSync(file).equals(data))return false;}catch(error){if(error.code!=='ENOENT')throw accessError(file,error);}
 let temp='';try{io.mkdirSync(path.dirname(file),{recursive:true});temp=file+'.tmp-'+process.pid+'-'+crypto.randomBytes(6).toString('hex');io.writeFileSync(temp,data);io.renameSync(temp,file);return true;}catch(error){throw accessError(file,error);}finally{if(temp)try{io.unlinkSync(temp);}catch{}}
}
function accessError(file,error){const result=new Error(`Cannot prepare ${path.basename(file)} (${error.code||'I/O error'}). Verified identical assets are not rewritten. Check access to its parent directory, or build in a fresh writable checkout using powershell -NoProfile -ExecutionPolicy Bypass -File tools/bootstrap.ps1. No ACL or global execution-policy changes were made.`);result.code=error.code;return result;}
module.exports={writeIfChanged};

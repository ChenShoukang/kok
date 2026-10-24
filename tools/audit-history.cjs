'use strict';
const {execFileSync}=require('node:child_process');
const path=require('node:path');

// Never include matching content in reports: filenames and object IDs are enough.
const credential=/(?<![A-Za-z0-9])sk-(?:proj-|or-v1-)?[A-Za-z0-9_-]{24,}|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|"protectedKey"\s*:\s*"[A-Za-z0-9+/=]{40,}"/;
const privatePath=/(^|\/)(data|private-vault|test-output|node_modules|\.git)\/|(^|\/)(ai-settings|library|vault-info)\.json(?:\.|$)|\.(?:vault|sealed|kokvault|dmp|log)$|(^|\/)\.env(?:\.|$)/i;

function auditGitHistory(root){
  root=path.resolve(root);
  function git(args,options={}){
    return execFileSync('git',['-c','safe.directory='+root.replace(/\\/g,'/'),'-c','core.quotepath=false','-C',root,...args],{encoding:'utf8',maxBuffer:256*1024*1024,stdio:['pipe','pipe','pipe'],...options});
  }
  if(git(['rev-parse','--is-shallow-repository']).trim()==='true')throw Error('Complete Git history is required.');
  const issues=[];
  const commits=Number(git(['rev-list','--all','--count']).trim());
  if(!commits)throw Error('No reachable commits to audit.');
  const paths=git(['log','--all','--format=','--name-only','--diff-filter=ACMR','--no-renames']).split(/\r?\n/).filter(Boolean);
  for(const name of new Set(paths))if(privatePath.test(name))issues.push({kind:'private-data path',path:name});
  if(credential.test(git(['log','--all','--format=%B'])))issues.push({kind:'credential pattern in commit messages'});
  const objects=git(['rev-list','--objects','--all','--no-object-names']).trim().split(/\r?\n/);
  const rows=git(['cat-file','--batch-check=%(objectname) %(objecttype)'],{input:objects.join('\n')+'\n'}).trim().split(/\r?\n/);
  const blobs=rows.filter(row=>row.endsWith(' blob')).map(row=>row.split(' ')[0]);
  const data=git(['cat-file','--batch'],{input:blobs.join('\n')+'\n',encoding:null});
  let offset=0;
  for(const sha of blobs){
    const end=data.indexOf(10,offset);
    if(end<0)throw Error('Incomplete Git batch header.');
    const [actual,type,length]=data.subarray(offset,end).toString('ascii').split(' ');
    const size=Number(length);
    if(actual!==sha||type!=='blob'||!Number.isSafeInteger(size)||size<0)throw Error('Unexpected Git batch object.');
    offset=end+1;
    if(offset+size>=data.length||data[offset+size]!==10)throw Error('Incomplete Git batch object.');
    if(credential.test(data.subarray(offset,offset+size).toString('utf8')))issues.push({kind:'credential pattern',object:sha});
    offset+=size+1;
  }
  if(offset!==data.length)throw Error('Unexpected trailing Git batch data.');
  return {commits,blobs:blobs.length,issues};
}

if(require.main===module){
  try{
    const result=auditGitHistory(path.resolve(__dirname,'..'));
    if(result.issues.length){for(const issue of result.issues)console.error(JSON.stringify(issue));console.error('Git history privacy audit failed. No credential values are printed.');process.exitCode=1;}
    else console.log(`Git history privacy audit passed: ${result.commits} commits, ${result.blobs} blobs; no credential or private-data patterns found.`);
  }catch{console.error('Git history privacy audit could not complete. Check Git access, shallow-clone status and the 256 MiB batch limit.');process.exitCode=1;}
}
module.exports={auditGitHistory};

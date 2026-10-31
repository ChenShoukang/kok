'use strict';
const assert=require('node:assert/strict'),{writeIfChanged}=require('../tools/asset-sync.cjs');
let writes=0;const denied=()=>{const e=Error('denied');e.code='EACCES';throw e;};
assert.equal(writeIfChanged('readonly/engine.wasm',Buffer.from('verified'),{readFileSync:()=>Buffer.from('verified'),writeFileSync:()=>writes++,mkdirSync:denied,renameSync:denied,unlinkSync:denied}),false);
assert.equal(writes,0);
assert.throws(()=>writeIfChanged('blocked/engine.wasm',Buffer.from('new'),{readFileSync:denied}),e=>e.code==='EACCES'&&e.message.includes('fresh writable checkout'));
assert.throws(()=>writeIfChanged('readonly/engine.wasm',Buffer.from('changed'),{readFileSync:()=>Buffer.from('old'),mkdirSync:denied,unlinkSync:denied}),e=>e.message.includes('No ACL'));
console.log('PASS: verified read-only vendor assets are not rewritten; inaccessible or changed files fail with a repair path and no permission mutation.');

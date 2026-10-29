'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),{inspect}=require('./analyze-ui.cjs');
const problems=[];let extensions=0;
const scripts=fs.readdirSync('ui').filter(f=>f.endsWith('.js')&&f!=='module-registry.js').map(f=>path.join('ui',f)),esm=fs.readdirSync('ui/modules').filter(f=>f.endsWith('.mjs')).map(f=>path.join('ui/modules',f));
for(const file of [...scripts,...esm]){
 const source=fs.readFileSync(file,'utf8');const ast=inspect(source,file.endsWith('.mjs')?'module':'script');for(const a of ast.assignments)problems.push(file+': '+a.node.left.name);extensions+=(source.match(/Kok\.extend\(/g)||[]).length;
}
assert.deepEqual(problems,[],'UI services must use explicit extension registration instead of function reassignment');
const index=fs.readFileSync('ui/index.html','utf8');assert(index.indexOf('module-registry.js')<index.indexOf('motion-core.js'),'The registry must initialize before services');
console.log('Architecture checks passed: '+extensions+' named UI extensions; no function reassignment.');

const workspace=fs.readFileSync('ui/modules/workspace-events.mjs','utf8');for(const marker of ['workspace:inspector-snapshot',"Kok.on('privateLocking'","Kok.on('librarySwitched'","Kok.on('libraryChanged'"])assert(workspace.includes(marker),'Workspace lifecycle contribution is missing: '+marker);
for(const file of ['agent.js','agent-ui.js','agent-tasks.js']){assert(!fs.existsSync(path.join('ui',file)),'Agent implementation must stay inside its ES module: '+file);assert(!index.includes('src="'+file+'?'),'An obsolete Agent global script was reintroduced');}
const agentRuntime=inspect(fs.readFileSync('ui/modules/agent-runtime.mjs','utf8'),'module').tree;
assert.deepEqual(agentRuntime.body.filter(n=>n.type==='ExportNamedDeclaration').flatMap(n=>n.declaration?.declarations?.map(d=>d.id.name)||[]),['AgentUi'],'The Agent UI should expose one explicit contract');
for(const [legacy,file,contract]of [['documents.js','document-runtime.mjs','DocumentUi'],['presentations.js','presentation-runtime.mjs','PresentationUi']]){assert(!fs.existsSync(path.join('ui',legacy))&&!index.includes('src="'+legacy+'?'),'Viewer implementation must not return to global scripts');const tree=inspect(fs.readFileSync('ui/modules/'+file,'utf8'),'module').tree;assert.deepEqual(tree.body.filter(n=>n.type==='ExportNamedDeclaration').flatMap(n=>n.declaration?.declarations?.map(d=>d.id.name)||[]),[contract]);}
assert(!fs.existsSync('ui/workspace-clarity.css'),'The old competing viewer layout sheet must remain consolidated');
for(const file of fs.readdirSync('ui').filter(f=>f.endsWith('.css')&&!f.startsWith('bundle-')&&f!=='viewer-shell.css'))assert(!fs.readFileSync('ui/'+file,'utf8').includes('.viewer-mode-bar'),'Viewer toolbar geometry must have one stylesheet owner: '+file);

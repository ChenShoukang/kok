const fs=require('node:fs');
const assert=require('node:assert/strict');
const catalog=JSON.parse(fs.readFileSync('ui/locales/en.json','utf8'));
const patterns=catalog.patterns.map(row=>new RegExp(row.pattern,'s'));
assert(Object.keys(catalog.messages).length>250);
for(const [source,target]of Object.entries(catalog.messages)){assert(source.trim());assert.equal(typeof target,'string');assert(target.trim());}
const missing=[];
// Agent failures carry a typed code first and the user-facing message second.
for(const file of fs.readdirSync('src').filter(name=>/^(Agent|Mcp).*\.cs$/.test(name))){
 for(const match of fs.readFileSync('src/'+file,'utf8').matchAll(/(?:throw|return) new Agent(?:Http)?Failure\("[^"\\]+","((?:\\.|[^"\\])*)"\s*[,)]/g)){
  const value=JSON.parse('"'+match[1]+'"');if(/[\u3400-\u9fff]/.test(value)&&!catalog.messages[value]&&!patterns.some(re=>re.test(value)))missing.push('src/'+file+': '+value);
 }
}
for(const file of ['agent-operations.mjs','agent-platform.mjs','mcp.mjs']){
 for(const match of fs.readFileSync('ui/modules/'+file,'utf8').matchAll(/\bt\('([^']+)'\)/g)){
  const value=match[1];if(/[\u3400-\u9fff]/.test(value)&&!catalog.messages[value]&&!patterns.some(re=>re.test(value)))missing.push('ui/modules/'+file+': '+value);
 }
}
for(const match of fs.readFileSync('src/PrivateTransfer.cs','utf8').matchAll(/throw new \w+\("((?:\\.|[^"\\])*)"\s*[,)]/g)){
 const value=JSON.parse('"'+match[1]+'"');if(/[\u3400-\u9fff]/.test(value)&&!catalog.messages[value]&&!patterns.some(re=>re.test(value)))missing.push('src/PrivateTransfer.cs: '+value);
}
for(const match of fs.readFileSync('src/SessionPersistence.cs','utf8').matchAll(/throw new \w+\("((?:\\.|[^"\\])*)"\s*[,)]/g)){
 const value=JSON.parse('"'+match[1]+'"');if(/[\u3400-\u9fff]/.test(value)&&!catalog.messages[value]&&!patterns.some(re=>re.test(value)))missing.push('src/SessionPersistence.cs: '+value);
}
const knowledgeSource=fs.readFileSync('src/Knowledge.cs','utf8');
for(const match of knowledgeSource.matchAll(/throw new \w+\("((?:\\.|[^"\\])*)"\s*[,)]/g)){
 const value=JSON.parse('"'+match[1]+'"');if(/[\u3400-\u9fff]/.test(value)&&!catalog.messages[value]&&!patterns.some(re=>re.test(value)))missing.push('src/Knowledge.cs: '+value);
}
for(const file of ['src/RecipePlans.cs','src/FileVersions.cs','src/ClipTools.cs','src/AutomaticOperations.cs','src/SqliteStore.cs','src/Workspace.cs','src/DomainModules.cs','src/ManagedJobs.cs','src/ModuleContext.cs','src/ModuleDefinitions.cs','src/NativeDocuments.cs','src/Operations.cs','src/WorkspaceModules.cs','src/LegacyBridge.cs','src/AgentService.cs','src/Modules.cs','src/AgentJournal.cs','src/AgentTaskState.cs','src/AgentTaskHost.cs','src/AgentTaskEngine.cs','src/AgentTaskModel.cs','src/AgentHandoff.cs','src/AgentToolChat.cs','src/AgentWorkflows.cs','src/AgentTools.cs','src/AgentWorkspace.cs','src/AgentRuntime.cs','src/KnowledgeWorkspace.cs','src/VisualAgent.cs','src/Presentations.cs','src/Documents.cs','src/BookNavigation.cs','src/ReadingSessions.cs','src/TranslationDocuments.cs','src/Speech.cs','src/TextDocumentExport.cs','src/MediaSequence.cs','src/MusicLibrary.cs','src/CreativeEdits.cs','src/PreviewWindows.cs','src/Program.cs','src/WindowChrome.cs','src/Library.cs','src/Subtitles.cs','src/Translation.cs','src/AutoLibrary.cs','src/Organization.cs','src/FileChat.cs','src/StudyTools.cs','src/AppModes.cs','src/AiModels.cs','src/PrivateVault.cs','src/PrivateWindow.cs','src/ReadingAppearance.cs','src/ReadingWindow.cs','src/DownloadTools.cs','src/Downloads.cs','src/DirectDownloads.cs','src/MediaResolver.cs','src/SiteAdapters.cs','src/SiteTransfer.cs','src/YoutubeSite.cs','src/DownloadSessions.cs','src/DownloadLogin.cs','src/DownloadBrowser.cs','src/StreamDownloads.cs','src/DownloadWindow.cs']){
 const text=fs.readFileSync(file,'utf8');
 for(const match of text.matchAll(/throw new \w+\("((?:\\.|[^"\\])*)"\s*[,)]/g)){
   const value=JSON.parse('"'+match[1]+'"');if(/[\u3400-\u9fff]/.test(value)&&!catalog.messages[value]&&!patterns.some(re=>re.test(value)))missing.push(file+': '+value);
 }
}
for(const file of fs.readdirSync('ui/modules').filter(f=>f.endsWith('.mjs'))){for(const match of fs.readFileSync('ui/modules/'+file,'utf8').matchAll(/(?:label|title):'([^']+)'/g)){const value=match[1];if(/[\u3400-\u9fff]/.test(value)&&!catalog.messages[value]&&!patterns.some(re=>re.test(value)))missing.push('ui/modules/'+file+': '+value);}}
assert.deepEqual(missing,[],'Native error text must have an English translation');
console.log(`Locale catalog valid: ${Object.keys(catalog.messages).length} messages and ${patterns.length} dynamic patterns.`);

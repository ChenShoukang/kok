const fs=require('node:fs');
const assert=require('node:assert/strict');
const catalog=JSON.parse(fs.readFileSync('ui/locales/en.json','utf8'));
const patterns=catalog.patterns.map(row=>new RegExp(row.pattern,'s'));
assert(Object.keys(catalog.messages).length>250);
for(const [source,target]of Object.entries(catalog.messages)){assert(source.trim());assert.equal(typeof target,'string');assert(target.trim());}
const missing=[];
for(const match of fs.readFileSync('src/SessionPersistence.cs','utf8').matchAll(/throw new \w+\("((?:\\.|[^"\\])*)"\s*[,)]/g)){
 const value=JSON.parse('"'+match[1]+'"');if(/[\u3400-\u9fff]/.test(value)&&!catalog.messages[value]&&!patterns.some(re=>re.test(value)))missing.push('src/SessionPersistence.cs: '+value);
}
const knowledgeSource=fs.readFileSync('src/Knowledge.cs','utf8');
for(const match of knowledgeSource.matchAll(/throw new \w+\("((?:\\.|[^"\\])*)"\s*[,)]/g)){
 const value=JSON.parse('"'+match[1]+'"');if(/[\u3400-\u9fff]/.test(value)&&!catalog.messages[value]&&!patterns.some(re=>re.test(value)))missing.push('src/Knowledge.cs: '+value);
}
for(const file of ['src/Documents.cs','src/BookNavigation.cs','src/ReadingSessions.cs','src/TranslationDocuments.cs','src/Speech.cs','src/TextDocumentExport.cs','src/MediaSequence.cs','src/MusicLibrary.cs','src/CreativeEdits.cs','src/PreviewWindows.cs','src/Program.cs','src/WindowChrome.cs','src/Library.cs','src/Subtitles.cs','src/Translation.cs','src/AutoLibrary.cs','src/Organization.cs','src/FileChat.cs','src/StudyTools.cs','src/AppModes.cs','src/AiModels.cs','src/PrivateVault.cs','src/PrivateWindow.cs','src/ReadingAppearance.cs','src/ReadingWindow.cs','src/DownloadTools.cs','src/Downloads.cs','src/DirectDownloads.cs','src/MediaResolver.cs','src/SiteAdapters.cs','src/SiteTransfer.cs','src/YoutubeSite.cs','src/DownloadSessions.cs','src/DownloadLogin.cs','src/DownloadBrowser.cs','src/StreamDownloads.cs','src/DownloadWindow.cs']){
 const text=fs.readFileSync(file,'utf8');
 for(const match of text.matchAll(/throw new \w+\("((?:\\.|[^"\\])*)"\s*[,)]/g)){
   const value=JSON.parse('"'+match[1]+'"');if(/[\u3400-\u9fff]/.test(value)&&!catalog.messages[value]&&!patterns.some(re=>re.test(value)))missing.push(file+': '+value);
 }
}
assert.deepEqual(missing,[],'Native error text must have an English translation');
console.log(`Locale catalog valid: ${Object.keys(catalog.messages).length} messages and ${patterns.length} dynamic patterns.`);

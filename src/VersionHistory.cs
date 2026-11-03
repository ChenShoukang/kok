using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace kok {
    // Named checkpoints: one label per (item, operation) version, stored in a self-contained sealed file.
    internal sealed class VersionLabelRow {public string itemId,operationId,label,created;}
    internal sealed class VersionLabelEntry {public string label,created;}
    // Native version history for kok-owned documents (MD/TXT/DOCX/PPTX). Versions are the sealed
    // before-image bytes already written by CommitDocument/SavePresentation; this API only reads them,
    // except for RestoreVersion which writes a compensating operation so undo/evidence rules still hold.
    public partial class Library {
        public const int MaxVersionEntries=100;
        const string VersionCorrupt="版本记录无法读取，原记录已保留。";
        const string VersionUnavailable="版本内容不可用，已保留当前内容。";
        const string VersionInvalid="版本记录无效。";
        const string PrivateVersions="私密文件夹不保存版本。";

        // Newest-first entries from the operation log plus a synthetic current entry. Unreadable sealed
        // files are reported with available=false instead of failing the whole list.
        public List<object> ListVersions(string itemId){
            lock(gate){
                if(IsPrivate)throw new InvalidDataException(PrivateVersions);
                var item=ManagedOwnedFile(itemId,"MD","TXT","DOCX","PPTX");
                var result=new List<object>();
                result.Add(new Dictionary<string,object>{{"operationId","current"},{"created",item.modified??""},{"title","当前版本"},{"source",""},{"state",""},{"current",true},{"available",File.Exists(item.path)},{"label",""}});
                var chronological=OrderedVersionOps(itemId);
                // Keep named milestones visible even after many automatic saves, within the same cap.
                var selected=new HashSet<string>(chronological.AsEnumerable().Reverse().OrderByDescending(op=>!String.IsNullOrEmpty(VersionLabelFor(itemId,op.id))).Take(MaxVersionEntries).Select(op=>op.id));
                for(int n=chronological.Count-1;n>=0;n--){
                    var op=chronological[n];if(!selected.Contains(op.id))continue;FileVersion version;
                    bool available=TryReadFileVersion(op.fileVersion,out version)&&version!=null&&version.itemId==itemId&&!String.IsNullOrEmpty(version.before);
                    result.Add(new Dictionary<string,object>{{"operationId",op.id},{"created",op.created??""},{"title",op.title??""},{"source",op.source??""},{"state",op.state??""},{"current",false},{"available",available},{"label",VersionLabelFor(itemId,op.id)}});
                }
                return result;
            }
        }
        // One version: before bytes are the sealed before-image; after bytes are the next version's
        // before-image (or the current file for the newest version). Texts are best-effort per format.
        public Dictionary<string,object> ReadVersion(string itemId,string operationId){
            lock(gate){
                if(IsPrivate)throw new InvalidDataException(PrivateVersions);
                var item=ManagedOwnedFile(itemId,"MD","TXT","DOCX","PPTX");
                var op=VersionOperation(itemId,operationId);
                FileVersion version;if(!TryReadFileVersion(op.fileVersion,out version)||version==null||version.itemId!=itemId||String.IsNullOrEmpty(version.before))throw new InvalidDataException(VersionUnavailable);
                byte[] beforeBytes;
                try{beforeBytes=Convert.FromBase64String(version.before);}catch{throw new InvalidDataException(VersionCorrupt);}
                var chronological=OrderedVersionOps(itemId);int index=chronological.FindIndex(candidate=>candidate.id==operationId);
                byte[] afterBytes=null;
                if(index>=0&&index+1<chronological.Count){
                    FileVersion newer;if(TryReadFileVersion(chronological[index+1].fileVersion,out newer)&&newer!=null&&!String.IsNullOrEmpty(newer.before)){
                        try{afterBytes=Convert.FromBase64String(newer.before);}catch{afterBytes=null;}
                    }
                }
                if(afterBytes==null&&File.Exists(item.path)){try{afterBytes=File.ReadAllBytes(item.path);}catch{afterBytes=null;}}
                string ext=item.ext??"",beforeText=null,afterText=null,currentText=null;bool textAvailable=false;
                if(IsTextVersionFormat(ext)){
                    beforeText=VersionText(ext,beforeBytes);afterText=afterBytes==null?null:VersionText(ext,afterBytes);
                    try{currentText=DocumentSource(itemId).text;}catch{currentText=null;}
                    textAvailable=beforeText!=null&&afterText!=null&&currentText!=null;
                }
                return new Dictionary<string,object>{
                    {"itemId",itemId},{"operationId",operationId},{"created",op.created??""},{"title",op.title??""},{"source",op.source??""},
                    {"beforeHash",BytesHash(beforeBytes)},{"afterHash",version.afterHash??""},
                    {"beforeText",beforeText},{"afterText",afterText},{"currentText",currentText},
                    {"textAvailable",textAvailable},{"size",beforeBytes.LongLength}};
            }
        }
        // Restores the version's before-image bytes through a compensating applied operation (its
        // fileVersion keeps the previously current bytes), so OperationUndo brings the old content back.
        public Dictionary<string,object> RestoreVersion(string itemId,string operationId){
            lock(gate){
                if(IsPrivate)throw new InvalidDataException(PrivateVersions);
                var item=ManagedOwnedFile(itemId,"MD","TXT","DOCX","PPTX");
                var op=VersionOperation(itemId,operationId);
                FileVersion version;if(!TryReadFileVersion(op.fileVersion,out version)||version==null||version.itemId!=itemId||String.IsNullOrEmpty(version.before))throw new InvalidDataException(VersionUnavailable);
                if(!File.Exists(item.path))throw new FileNotFoundException("文档文件已移动或删除。");
                byte[] targetBytes;try{targetBytes=Convert.FromBase64String(version.before);}catch{throw new InvalidDataException(VersionCorrupt);}
                string targetHash=BytesHash(targetBytes);
                byte[] currentBytes=File.ReadAllBytes(item.path);string currentHash=BytesHash(currentBytes);
                if(targetHash==currentHash)throw new InvalidDataException("当前内容已经是该版本。");
                bool textFormat=item.ext=="MD"||item.ext=="TXT"||item.ext=="DOCX";
                Dictionary<string,object> targetDraft=null;
                var patch=op.patches==null?null:op.patches.FirstOrDefault(candidate=>candidate!=null&&candidate.id==itemId);
                if(textFormat&&patch!=null&&patch.before!=null&&patch.before.ContainsKey("editDraft"))targetDraft=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(patch.before["editDraft"]));
                // The before-image hash must match the operation's recorded before-draft signature.
                string expected=targetDraft==null?null:DraftValue(targetDraft,"signature");
                if(!String.IsNullOrEmpty(expected)&&expected!=targetHash)throw new InvalidDataException("版本记录已变化，已保留当前内容。");
                long previousSize=item.size;string previousModified=item.modified;
                var previousDraft=item.editDraft==null?null:Json.Deserialize<Dictionary<string,object>>(Json.Serialize(item.editDraft));
                var compensation=new Operation{id=Guid.NewGuid().ToString("N"),module="documents",source="user",title="恢复版本",created=DateTime.UtcNow.ToString("o"),state="applied"};
                if(textFormat){
                    if(targetDraft==null)targetDraft=new Dictionary<string,object>{{"kind","text"},{"format",(item.ext??"MD").ToLowerInvariant()},{"columns",1},{"fontSize",17}};
                    targetDraft=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(targetDraft));
                    targetDraft["signature"]=targetHash;
                    string targetText=VersionText(item.ext,targetBytes);if(targetText!=null)targetDraft["text"]=targetText;
                    compensation.patches.Add(new AgentChange{id=item.id,before=new Dictionary<string,object>{{"editDraft",previousDraft}},after=new Dictionary<string,object>{{"editDraft",targetDraft}}});
                }
                compensation.fileVersion=compensation.id;
                try{
                    CheckOpCapacity(compensation);
                    SaveFileVersion(compensation.id,new FileVersion{itemId=item.id,before=Convert.ToBase64String(currentBytes),afterHash=targetHash});
                    ReplaceOwnedBytes(item.path,targetBytes);
                    item.size=targetBytes.LongLength;item.modified=File.GetLastWriteTimeUtc(item.path).ToString("o");
                    if(textFormat)item.editDraft=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(targetDraft));
                    OperationRows.Add(compensation);
                    Save(false,new[]{item.id},new[]{compensation.id},true);
                }catch{
                    try{ReplaceOwnedBytes(item.path,currentBytes);}catch{}
                    item.size=previousSize;item.modified=previousModified;item.editDraft=previousDraft;
                    OperationRows.RemoveAll(candidate=>candidate.id==compensation.id);
                    try{string sealedPath=VersionPath(compensation.id);if(File.Exists(sealedPath))File.Delete(sealedPath);}catch{}
                    throw;
                }
                return new Dictionary<string,object>{{"operationId",compensation.id},{"restoredFrom",operationId},{"size",targetBytes.LongLength},{"modified",item.modified}};
            }
        }

        List<Operation> OrderedVersionOps(string itemId){
            return OperationRows.Where(op=>op!=null&&!String.IsNullOrEmpty(op.fileVersion)&&op.patches!=null&&op.patches.Any(patch=>patch!=null&&patch.id==itemId))
                .Select((op,index)=>new{op,index}).OrderBy(entry=>entry.op.created??"",StringComparer.Ordinal).ThenBy(entry=>entry.index)
                .Select(entry=>entry.op).ToList();
        }
        Operation VersionOperation(string itemId,string operationId){
            if(!AgentJournal.ValidId(operationId))throw new InvalidDataException("操作记录无效。");
            var op=FindOperation(operationId);
            if(op==null||String.IsNullOrEmpty(op.fileVersion)||op.patches==null||!op.patches.Any(patch=>patch!=null&&patch.id==itemId))throw new InvalidDataException(VersionInvalid);
            return op;
        }
        bool TryReadFileVersion(string versionId,out FileVersion version){
            version=null;
            if(String.IsNullOrEmpty(versionId)||!AgentJournal.ValidId(versionId))return false;
            try{
                string path=VersionPath(versionId);if(!File.Exists(path))return false;
                byte[] encrypted=File.ReadAllBytes(path);
                byte[] bytes=vault==null?ProtectedData.Unprotect(encrypted,Encoding.UTF8.GetBytes("kok.file-history.v1"),DataProtectionScope.CurrentUser):vault.Unseal(encrypted,"file-history");
                version=Json.Deserialize<FileVersion>(Encoding.UTF8.GetString(bytes));
                return version!=null;
            }catch{version=null;return false;}
        }
        static bool IsTextVersionFormat(string ext){return ext=="MD"||ext=="TXT"||ext=="DOCX"||ext=="HTML"||ext=="HTM";}
        static string VersionText(string ext,byte[] bytes){
            if(bytes==null)return null;
            if(ext=="MD"||ext=="TXT"||ext=="HTML"||ext=="HTM"){
                try{using(var reader=new StreamReader(new MemoryStream(bytes),Encoding.UTF8,true))return reader.ReadToEnd();}catch{return null;}
            }
            if(ext=="DOCX")return VersionDocxText(bytes);
            return null;
        }
        static string VersionDocxText(byte[] bytes){
            string temp=Path.Combine(Path.GetTempPath(),"kok-version-"+Guid.NewGuid().ToString("N")+".docx");
            try{File.WriteAllBytes(temp,bytes);var book=ReadBook(new Entry{path=temp,name="version",kind="document",ext="DOCX"});return String.Join("\n\n",book.chapters.Select(chapter=>chapter.text));}catch{return null;}
            finally{try{if(File.Exists(temp))File.Delete(temp);}catch{}}
        }

        // ---- Named checkpoints (P3-C) -------------------------------------------------------------
        // The label store is independent from the library database: one DPAPI-sealed (or vault-sealed)
        // file under DataDir. Corruption is tolerated (log + empty store); saves are atomic temp+replace.
        const int MaxVersionLabels=300;
        const string VersionLabelPurpose="kok.version-labels.v1";
        Dictionary<string,VersionLabelEntry> versionLabels;
        string VersionLabelPath {get{return Path.Combine(DataDir,"version-labels.sealed");}}
        static string VersionLabelKey(string itemId,string operationId){return itemId+"|"+operationId;}
        Dictionary<string,VersionLabelEntry> LoadVersionLabels(){
            if(versionLabels!=null)return versionLabels;
            versionLabels=new Dictionary<string,VersionLabelEntry>(StringComparer.Ordinal);
            try{
                string path=VersionLabelPath;if(!File.Exists(path))return versionLabels;
                byte[] encrypted=File.ReadAllBytes(path);
                byte[] plain=vault==null?ProtectedData.Unprotect(encrypted,Encoding.UTF8.GetBytes(VersionLabelPurpose),DataProtectionScope.CurrentUser):vault.Unseal(encrypted,"version-labels");
                var rows=Json.Deserialize<List<VersionLabelRow>>(Encoding.UTF8.GetString(plain));
                if(rows!=null)foreach(var row in rows){
                    if(row==null||!AgentJournal.ValidId(row.itemId)||!AgentJournal.ValidId(row.operationId)||String.IsNullOrEmpty(row.label)||row.label.Length>64||row.label.Any(Char.IsControl))continue;
                    versionLabels[VersionLabelKey(row.itemId,row.operationId)]=new VersionLabelEntry{label=row.label,created=row.created??""};
                }
            }catch(Exception error){RuntimeLog.Write("version-labels-load",error);versionLabels.Clear();}
            return versionLabels;
        }
        void SaveVersionLabels(){
            var rows=new List<VersionLabelRow>();
            foreach(var pair in versionLabels.OrderBy(pair=>pair.Value.created,StringComparer.Ordinal)){
                int split=pair.Key.IndexOf('|');if(split<=0)continue;
                rows.Add(new VersionLabelRow{itemId=pair.Key.Substring(0,split),operationId=pair.Key.Substring(split+1),label=pair.Value.label,created=pair.Value.created});
            }
            byte[] plain=Encoding.UTF8.GetBytes(Json.Serialize(rows));
            byte[] sealedBytes=vault==null?ProtectedData.Protect(plain,Encoding.UTF8.GetBytes(VersionLabelPurpose),DataProtectionScope.CurrentUser):vault.Seal(plain,"version-labels");
            string path=VersionLabelPath;string folder=Path.GetDirectoryName(path);if(!String.IsNullOrEmpty(folder))Directory.CreateDirectory(folder);
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllBytes(temp,sealedBytes);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{try{if(File.Exists(temp))File.Delete(temp);}catch{}}
        }
        string VersionLabelFor(string itemId,string operationId){
            VersionLabelEntry entry;return LoadVersionLabels().TryGetValue(VersionLabelKey(itemId,operationId),out entry)?entry.label:"";
        }
        // Label one existing historical version of an owned document; the current sentinel is never labelable.
        public Dictionary<string,object> LabelVersion(string itemId,string operationId,string label){
            lock(gate){
                if(IsPrivate)throw new InvalidDataException(PrivateVersions);
                ManagedOwnedFile(itemId,"MD","TXT","DOCX","PPTX");
                if(operationId=="current"||!AgentJournal.ValidId(operationId))throw new InvalidDataException(VersionInvalid);
                VersionOperation(itemId,operationId);
                string value=(label??"").Trim();
                if(value.Length<1||value.Length>64||value.Any(Char.IsControl))throw new InvalidDataException(VersionInvalid);
                var labels=LoadVersionLabels();string key=VersionLabelKey(itemId,operationId);
                if(!labels.ContainsKey(key)&&labels.Count>=MaxVersionLabels)throw new InvalidDataException(VersionInvalid);
                VersionLabelEntry previous;bool existed=labels.TryGetValue(key,out previous);
                string created=existed?previous.created:DateTime.UtcNow.ToString("o");
                labels[key]=new VersionLabelEntry{label=value,created=created};
                try{SaveVersionLabels();}catch{if(existed)labels[key]=previous;else labels.Remove(key);throw;}
                return new Dictionary<string,object>{{"itemId",itemId},{"operationId",operationId},{"label",value}};
            }
        }
        // Removing a missing label is a no-op; invalid item/operation ids still fail closed.
        public Dictionary<string,object> UnlabelVersion(string itemId,string operationId){
            lock(gate){
                if(IsPrivate)throw new InvalidDataException(PrivateVersions);
                ManagedOwnedFile(itemId,"MD","TXT","DOCX","PPTX");
                if(operationId=="current"||!AgentJournal.ValidId(operationId))throw new InvalidDataException(VersionInvalid);
                var labels=LoadVersionLabels();string key=VersionLabelKey(itemId,operationId);
                VersionLabelEntry previous;if(labels.TryGetValue(key,out previous)){labels.Remove(key);try{SaveVersionLabels();}catch{labels[key]=previous;throw;}}
                return new Dictionary<string,object>{{"itemId",itemId},{"operationId",operationId},{"label",""}};
            }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace kok {
    public class AgentSessionEntry {public string type,id,parentId,created;public Dictionary<string,object> data;}
    public class AgentSessionMeta {public string id,title,created,updated,leafId;public int entries;public long bytes;}
    // Projection row with its native entry id so the UI can switch/fork from a specific message.
    public class AgentSessionMessage {public string id,role,content;}
    // Append-only Pi-style session tree. Line 1 is the session header; every further line is one entry.
    // Each line is an individually DPAPI-sealed JSON object under the current Windows account, base64 encoded.
    // Reads fail closed: a corrupt or short file is reported and never rewritten. Writes are atomic or append+flush.
    public sealed class AgentSessionStore {
        public const int MaxSessions=64,MaxEntries=4096,MaxMessageText=24000,MaxTitle=64;
        public const int MaxArtifacts=200,MaxTaskArtifacts=20,MaxArtifactName=200;
        public const long MaxBytes=8L*1024*1024;
        const string Corrupt="Agent 会话无法读取，原记录已保留。",BadSession="Agent 会话无效。",MissingSession="Agent 会话不存在。",MissingEntry="找不到会话条目。";
        const string Space="Agent 会话记录空间不足，请新建会话继续。",TooMany="Agent 会话数量已达上限，请先删除不需要的会话。";
        const string TooLong="消息内容过长，请缩短后重试。",TitleTooLong="会话标题过长，请缩短后重试。",ExistsMessage="Agent 会话已存在。";
        readonly string root;readonly object gate=new object();
        public AgentSessionStore(string dataDir){if(String.IsNullOrWhiteSpace(dataDir))throw new InvalidDataException(BadSession);root=Path.Combine(dataDir,"agent-sessions");}
        public string Root{get{return root;}}
        public static bool ValidId(string id){return id!=null&&Regex.IsMatch(id,"^[a-f0-9]{32}$");}

        public List<AgentSessionMeta> List(){lock(gate){
            var result=new List<AgentSessionMeta>();
            foreach(var file in SessionFiles())try{
                byte[] bytes=File.ReadAllBytes(file);if(bytes.Length==0||bytes.Length>MaxBytes)continue;
                string[] lines=SplitLines(bytes);if(lines.Length==0||lines.Length-1>MaxEntries)continue;
                var header=Open(lines[0]);string id=Text(header,"id");
                if(Text(header,"type")!="session"||!ValidId(id)||id!=SessionIdOf(file))continue;
                string updated=Text(header,"created"),leafId=null,fallback=null;
                for(int n=lines.Length-1;n>=1;n--){
                    var entry=ParseEntry(lines[n]);if(n==lines.Length-1)updated=entry.created??updated;
                    if(entry.type=="leaf"){leafId=Text(entry.data,"entryId");break;}
                    if(fallback==null&&(entry.type=="message"||entry.type=="tool"))fallback=entry.id;
                }
                if(updated==null)updated=File.GetLastWriteTimeUtc(file).ToString("o",CultureInfo.InvariantCulture);
                result.Add(new AgentSessionMeta{id=id,title=Text(header,"title")??"",created=Text(header,"created"),updated=updated,leafId=leafId??fallback,entries=lines.Length-1,bytes=bytes.Length});
            }catch{}
            return result.OrderByDescending(meta=>ParseTime(meta.updated)).ThenBy(meta=>meta.id,StringComparer.Ordinal).ToList();
        }}
        public bool Exists(string id){lock(gate){return ValidId(id)&&File.Exists(SessionPath(id));}}
        public List<AgentSessionEntry> Entries(string id){lock(gate){return Read(id).Entries;}}
        public List<AgentSessionEntry> Tree(string id){lock(gate){return Read(id).Entries;}}
        // Parent chain from the leaf back to the root, oldest first. Unlike Projection this keeps every
        // entry type: artifact records stay reachable even when a later compaction hides the messages.
        public List<AgentSessionEntry> BranchEntries(string id,string leafId=null){lock(gate){
            var source=Read(id);var map=source.Entries.ToDictionary(entry=>entry.id,StringComparer.Ordinal);
            string start=String.IsNullOrEmpty(leafId)?LeafOf(source.Entries):leafId;
            if(start!=null){if(!ValidId(start))throw new InvalidDataException(BadSession);if(!map.ContainsKey(start))throw new InvalidDataException(MissingEntry);}
            if(start==null)return new List<AgentSessionEntry>();
            var path=new List<AgentSessionEntry>();var seen=new HashSet<string>(StringComparer.Ordinal);string current=start;
            while(current!=null){
                if(!seen.Add(current))throw new InvalidDataException(Corrupt);
                AgentSessionEntry entry;if(!map.TryGetValue(current,out entry))throw new InvalidDataException(Corrupt);
                path.Add(entry);current=entry.parentId;
            }
            path.Reverse();return path;
        }}
        // Durable session artifacts on the selected branch: newest-first, one row per item, capped at 200.
        // Malformed artifact rows are skipped instead of failing the whole session read.
        public List<Dictionary<string,object>> Artifacts(string id,string leafId=null){lock(gate){
            var entries=BranchEntries(id,leafId);var seen=new HashSet<string>(StringComparer.Ordinal);var result=new List<Dictionary<string,object>>();
            for(int n=entries.Count-1;n>=0&&result.Count<MaxArtifacts;n--){
                var entry=entries[n];if(entry==null||entry.type!="artifact"||entry.data==null)continue;
                string itemId=Text(entry.data,"itemId");if(!ValidId(itemId)||!seen.Add(itemId))continue;
                string name=Text(entry.data,"name")??"";if(name.Length>MaxArtifactName)name=Truncate(name,MaxArtifactName);
                result.Add(new Dictionary<string,object>{{"itemId",itemId},{"taskId",Text(entry.data,"taskId")??""},{"name",name},{"entryId",entry.id},{"created",entry.created??Text(entry.data,"created")??""}});
            }
            return result;
        }}

        public AgentSessionEntry Append(string id,string type,string parentId,object data,string entryId=null){lock(gate){
            string path=SessionPath(id);if(!File.Exists(path))throw new InvalidDataException(MissingSession);
            if(String.IsNullOrEmpty(type)||type=="session")throw new InvalidDataException("会话条目类型无效。");
            if(entryId==null)entryId=Guid.NewGuid().ToString("N");else if(!ValidId(entryId))throw new InvalidDataException(BadSession);
            if(parentId!=null&&parentId.Length==0)parentId=null;
            if(parentId!=null&&!ValidId(parentId))throw new InvalidDataException(BadSession);
            var payload=Copy(data);NormalizeData(type,payload);ValidateData(type,payload,false);
            byte[] existing;try{existing=File.ReadAllBytes(path);}catch{throw new InvalidDataException(Corrupt);}
            if(existing.Length>MaxBytes)throw new InvalidDataException(Space);
            string[] lines=SplitLines(existing);ValidateAppendFile(id,lines);
            if(lines.Length-1>=MaxEntries)throw new InvalidDataException(Space);
            var entry=new AgentSessionEntry{type=type,id=entryId,parentId=parentId,created=Now(),data=payload};
            byte[] encoded=new UTF8Encoding(false).GetBytes(Seal(entry)+"\n");
            if(existing.Length+encoded.Length>MaxBytes)throw new InvalidDataException(Space);
            using(var stream=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.None)){stream.Write(encoded,0,encoded.Length);stream.Flush(true);}
            return entry;
        }}
        public string Leaf(string id){lock(gate){return LeafOf(Read(id).Entries);}}
        public void SetLeaf(string id,string entryId){lock(gate){
            if(!ValidId(entryId))throw new InvalidDataException(BadSession);
            var source=Read(id);if(!source.Entries.Any(entry=>entry.id==entryId))throw new InvalidDataException(MissingEntry);
            Append(id,"leaf",null,new Dictionary<string,object>{{"entryId",entryId}});
        }}
        public void Ensure(string id,string title,string cwd=""){lock(gate){
            string path=SessionPath(id);if(title==null)title="";
            if(title.Length>MaxTitle)throw new InvalidDataException(TitleTooLong);
            if(File.Exists(path))return;
            if(SessionFiles().Length>=MaxSessions)throw new InvalidDataException(TooMany);
            var header=new Dictionary<string,object>{{"type","session"},{"version",1},{"id",id},{"created",Now()},{"title",title},{"cwd",cwd??""}};
            WriteNew(path,new List<string>{Seal(header)});
        }}
        public void Rename(string id,string title){lock(gate){
            if(title==null||title.Length>MaxTitle)throw new InvalidDataException(TitleTooLong);
            var source=Read(id);var header=new Dictionary<string,object>(source.Header);header["title"]=title;
            var lines=new List<string>{Seal(header)};for(int n=1;n<source.Lines.Length;n++)lines.Add(source.Lines[n]);
            WriteReplace(SessionPath(id),lines);
        }}
        public void Delete(string id){lock(gate){
            string path=SessionPath(id);if(File.Exists(path))File.Delete(path);
            string temp=path+".tmp";if(File.Exists(temp))File.Delete(temp);
        }}
        public string Fork(string id,string fromEntryId,string newId,string title=null){lock(gate){
            if(!ValidId(newId))throw new InvalidDataException(BadSession);
            var source=Read(id);return CreateBranch(source,newId,title,Branch(source,fromEntryId));
        }}
        public string Clone(string id,string newId,string title=null){lock(gate){
            if(!ValidId(newId))throw new InvalidDataException(BadSession);
            var source=Read(id);string leaf=LeafOf(source.Entries);
            return CreateBranch(source,newId,title,leaf==null?new List<AgentSessionEntry>():Branch(source,leaf));
        }}
        public List<ChatTurn> Projection(string id,string leafId=null){lock(gate){
            var source=Read(id);var map=source.Entries.ToDictionary(entry=>entry.id,StringComparer.Ordinal);
            string start=leafId;
            if(start!=null){if(!ValidId(start))throw new InvalidDataException(BadSession);if(!map.ContainsKey(start))throw new InvalidDataException(MissingEntry);}
            else start=LeafOf(source.Entries);
            var turns=new List<ChatTurn>();if(start==null)return turns;
            var path=new List<AgentSessionEntry>();var seen=new HashSet<string>(StringComparer.Ordinal);string current=start;
            while(current!=null){
                if(!seen.Add(current))throw new InvalidDataException(Corrupt);
                AgentSessionEntry entry;if(!map.TryGetValue(current,out entry))throw new InvalidDataException(Corrupt);
                path.Add(entry);current=entry.parentId;
            }
            path.Reverse();
            int compaction=-1;for(int n=0;n<path.Count;n++)if(path[n].type=="compaction")compaction=n;
            if(compaction<0){foreach(var entry in path)AddTurn(turns,entry);return turns;}
            var row=path[compaction];
            turns.Add(new ChatTurn{role="assistant",content="[UNTRUSTED SESSION SUMMARY]\n"+(Text(row.data,"summary")??"")});
            // Pi semantics: the summary replaces entries before firstKeptEntryId; kept entries run from
            // there up to (not including) the compaction entry, then every entry after the compaction.
            int first=compaction;string kept=Text(row.data,"firstKeptEntryId");
            if(!String.IsNullOrEmpty(kept))for(int n=0;n<compaction;n++)if(path[n].id==kept){first=n;break;}
            for(int n=first;n<compaction;n++)AddTurn(turns,path[n]);
            for(int n=compaction+1;n<path.Count;n++)AddTurn(turns,path[n]);
            return turns;
        }}
        // Same kept-boundary/compaction semantics as Projection, but every row keeps its native entry id:
        // the latest compaction on the path becomes a synthetic assistant summary row whose id is the
        // compaction entry id, then the retained messages keep their own ids.
        public List<AgentSessionMessage> ProjectionMessages(string id,string leafId=null){lock(gate){
            var source=Read(id);var map=source.Entries.ToDictionary(entry=>entry.id,StringComparer.Ordinal);
            string start=leafId;
            if(start!=null){if(!ValidId(start))throw new InvalidDataException(BadSession);if(!map.ContainsKey(start))throw new InvalidDataException(MissingEntry);}
            else start=LeafOf(source.Entries);
            var messages=new List<AgentSessionMessage>();if(start==null)return messages;
            var path=new List<AgentSessionEntry>();var seen=new HashSet<string>(StringComparer.Ordinal);string current=start;
            while(current!=null){
                if(!seen.Add(current))throw new InvalidDataException(Corrupt);
                AgentSessionEntry entry;if(!map.TryGetValue(current,out entry))throw new InvalidDataException(Corrupt);
                path.Add(entry);current=entry.parentId;
            }
            path.Reverse();
            int compaction=-1;for(int n=0;n<path.Count;n++)if(path[n].type=="compaction")compaction=n;
            if(compaction<0){foreach(var entry in path)AddMessage(messages,entry);return messages;}
            var row=path[compaction];
            messages.Add(new AgentSessionMessage{id=row.id,role="assistant",content="[UNTRUSTED SESSION SUMMARY]\n"+(Text(row.data,"summary")??"")});
            int first=compaction;string kept=Text(row.data,"firstKeptEntryId");
            if(!String.IsNullOrEmpty(kept))for(int n=0;n<compaction;n++)if(path[n].id==kept){first=n;break;}
            for(int n=first;n<compaction;n++)AddMessage(messages,path[n]);
            for(int n=compaction+1;n<path.Count;n++)AddMessage(messages,path[n]);
            return messages;
        }}
        static void AddTurn(List<ChatTurn> turns,AgentSessionEntry entry){
            if(entry.type!="message")return;
            string role=Text(entry.data,"role"),text=Text(entry.data,"text");
            if((role=="user"||role=="assistant")&&text!=null)turns.Add(new ChatTurn{role=role,content=text});
        }
        static void AddMessage(List<AgentSessionMessage> messages,AgentSessionEntry entry){
            if(entry.type!="message")return;
            string role=Text(entry.data,"role"),text=Text(entry.data,"text");
            if((role=="user"||role=="assistant")&&text!=null)messages.Add(new AgentSessionMessage{id=entry.id,role=role,content=text});
        }

        sealed class SessionData{public Dictionary<string,object> Header;public string[] Lines;public List<AgentSessionEntry> Entries;}
        string SessionPath(string id){
            if(!ValidId(id))throw new InvalidDataException(BadSession);
            string path=Path.GetFullPath(Path.Combine(root,id+".jsonl.sealed"));
            string dir=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!path.StartsWith(dir,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException(BadSession);
            return path;
        }
        string[] SessionFiles(){
            if(!Directory.Exists(root))return new string[0];
            return Directory.GetFiles(root).Where(file=>ValidId(SessionIdOf(file))&&Path.GetFileName(file).EndsWith(".jsonl.sealed",StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        static string SessionIdOf(string file){
            string name=Path.GetFileName(file);
            return name.EndsWith(".jsonl.sealed",StringComparison.OrdinalIgnoreCase)?name.Substring(0,name.Length-".jsonl.sealed".Length):"";
        }
        static string Now(){return DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);}
        static string Seal(object value){
            byte[] bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(Library.Json.Serialize(value)),null,DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }
        static Dictionary<string,object> Open(string line){
            object value;
            try{value=Library.Json.DeserializeObject(Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(line),null,DataProtectionScope.CurrentUser)));}
            catch{throw new InvalidDataException(Corrupt);}
            var row=value as Dictionary<string,object>;if(row==null)throw new InvalidDataException(Corrupt);
            return row;
        }
        static string[] SplitLines(byte[] bytes){
            string[] lines=Encoding.UTF8.GetString(bytes).Split('\n');
            if(lines.Length>0&&lines[lines.Length-1].Length==0)Array.Resize(ref lines,lines.Length-1);
            return lines;
        }
        static string Text(Dictionary<string,object> value,string key){
            object raw;if(value==null||!value.TryGetValue(key,out raw)||raw==null)return null;
            var text=raw as string;return text??Convert.ToString(raw,CultureInfo.InvariantCulture);
        }
        static bool TryInt(Dictionary<string,object> value,string key,out int number){
            number=0;object raw;
            if(value==null||!value.TryGetValue(key,out raw)||raw==null)return false;
            try{number=Convert.ToInt32(raw,CultureInfo.InvariantCulture);return true;}catch{return false;}
        }
        static DateTime ParseTime(string value){
            DateTime parsed;
            return DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out parsed)?parsed:DateTime.MinValue;
        }
        static string Truncate(string value,int max){
            if(value.Length<=max)return value;
            int cut=max;if(cut>0&&Char.IsHighSurrogate(value[cut-1]))cut--;
            return value.Substring(0,cut);
        }
        static Dictionary<string,object> Copy(object data){
            if(data==null)return new Dictionary<string,object>();
            try{
                string json=Library.Json.Serialize(data);
                var copy=Library.Json.Deserialize<Dictionary<string,object>>(json);
                return copy??new Dictionary<string,object>();
            }catch{throw new InvalidDataException("会话条目无效。");}
        }
        static void NormalizeData(string type,Dictionary<string,object> data){
            if(data==null)return;
            if(type=="message"&&Text(data,"role")=="agent")data["role"]="assistant";
            if(type=="compaction"&&data.ContainsKey("firstKeptEntryId")&&Convert.ToString(data["firstKeptEntryId"],CultureInfo.InvariantCulture)=="")data["firstKeptEntryId"]=null;
        }
        static void ValidateData(string type,Dictionary<string,object> data,bool read){
            if(data==null){if(read)throw new InvalidDataException(Corrupt);return;}
            if(type=="message"){
                string role=Text(data,"role"),text=Text(data,"text");
                if(role!="user"&&role!="assistant")throw new InvalidDataException(read?Corrupt:"会话消息无效。");
                if(text==null)throw new InvalidDataException(read?Corrupt:"会话消息无效。");
                if(text.Length>MaxMessageText)throw new InvalidDataException(read?Corrupt:TooLong);
            }else if(type=="label"){
                if(!ValidId(Text(data,"targetId")))throw new InvalidDataException(read?Corrupt:"会话标签无效。");
                string label=Text(data,"label");
                if(label==null||label.Length>MaxTitle)throw new InvalidDataException(read?Corrupt:"会话标签过长，请缩短后重试。");
            }else if(type=="session_info"){
                string title=Text(data,"title");
                if(title==null||title.Length>MaxTitle)throw new InvalidDataException(read?Corrupt:TitleTooLong);
            }else if(type=="leaf"){
                if(!ValidId(Text(data,"entryId")))throw new InvalidDataException(read?Corrupt:MissingEntry);
            }else if(type=="compaction"){
                string summary=Text(data,"summary");
                if(summary==null)throw new InvalidDataException(read?Corrupt:"会话压缩记录无效。");
                string firstKept=Text(data,"firstKeptEntryId");
                if(!String.IsNullOrEmpty(firstKept)&&!ValidId(firstKept))throw new InvalidDataException(read?Corrupt:"会话压缩记录无效。");
            }else if(type=="artifact"){
                // Reads stay lenient: a malformed artifact row is skipped by Artifacts instead of
                // making the whole session unreadable. Writes enforce the durable record shape.
                if(read)return;
                if(!ValidId(Text(data,"itemId"))||!ValidId(Text(data,"taskId")))throw new InvalidDataException("会话成果无效。");
                string name=Text(data,"name");
                if(name!=null&&name.Length>MaxArtifactName)throw new InvalidDataException("会话成果无效。");
            }
        }
        static AgentSessionEntry ParseEntry(string line){
            var value=Open(line);
            var entry=new AgentSessionEntry{type=Text(value,"type"),id=Text(value,"id"),parentId=Text(value,"parentId"),created=Text(value,"created"),data=value.ContainsKey("data")?value["data"] as Dictionary<string,object>:null};
            if(String.IsNullOrEmpty(entry.type)||entry.type=="session"||!ValidId(entry.id)||(entry.parentId!=null&&!ValidId(entry.parentId))||entry.created==null||entry.data==null)throw new InvalidDataException(Corrupt);
            ValidateData(entry.type,entry.data,true);
            return entry;
        }
        SessionData Read(string id){
            string path=SessionPath(id);byte[] bytes;
            try{bytes=File.ReadAllBytes(path);}
            catch(FileNotFoundException){throw new InvalidDataException(MissingSession);}
            catch(DirectoryNotFoundException){throw new InvalidDataException(MissingSession);}
            catch{throw new InvalidDataException(Corrupt);}
            return ParseFile(id,bytes);
        }
        static SessionData ParseFile(string id,byte[] bytes){
            if(bytes.Length==0||bytes.Length>MaxBytes)throw new InvalidDataException(Corrupt);
            string[] lines=SplitLines(bytes);
            if(lines.Length==0||lines.Length-1>MaxEntries)throw new InvalidDataException(Corrupt);
            for(int n=0;n<lines.Length;n++)if(lines[n].Length==0)throw new InvalidDataException(Corrupt);
            var header=Open(lines[0]);int version;
            if(Text(header,"type")!="session"||Text(header,"id")!=id||Text(header,"title")==null||!TryInt(header,"version",out version)||version!=1)throw new InvalidDataException(Corrupt);
            var rows=new List<AgentSessionEntry>();var ids=new HashSet<string>(StringComparer.Ordinal){id};
            for(int n=1;n<lines.Length;n++){var entry=ParseEntry(lines[n]);if(!ids.Add(entry.id))throw new InvalidDataException(Corrupt);rows.Add(entry);}
            return new SessionData{Header=header,Lines=lines,Entries=rows};
        }
        static void ValidateAppendFile(string id,string[] lines){
            if(lines.Length==0)throw new InvalidDataException(Corrupt);
            for(int n=0;n<lines.Length;n++)if(lines[n].Length==0)throw new InvalidDataException(Corrupt);
            var header=Open(lines[0]);
            if(Text(header,"type")!="session"||Text(header,"id")!=id)throw new InvalidDataException(Corrupt);
            if(lines.Length-1>MaxEntries)throw new InvalidDataException(Space);
            if(lines.Length>1)ParseEntry(lines[lines.Length-1]);
        }
        static string LeafOf(List<AgentSessionEntry> entries){
            string fallback=null;
            for(int n=entries.Count-1;n>=0;n--){
                var entry=entries[n];
                if(entry.type=="leaf")return Text(entry.data,"entryId");
                if(fallback==null&&(entry.type=="message"||entry.type=="tool"))fallback=entry.id;
            }
            return fallback;
        }
        static List<AgentSessionEntry> Branch(SessionData source,string startId){
            if(!ValidId(startId))throw new InvalidDataException(BadSession);
            var map=source.Entries.ToDictionary(entry=>entry.id,StringComparer.Ordinal);
            AgentSessionEntry current;if(!map.TryGetValue(startId,out current))throw new InvalidDataException(MissingEntry);
            var path=new List<AgentSessionEntry>();var seen=new HashSet<string>(StringComparer.Ordinal);
            while(current!=null){
                if(!seen.Add(current.id))throw new InvalidDataException(Corrupt);
                path.Add(current);
                if(current.parentId==null){current=null;}else{AgentSessionEntry parent;if(!map.TryGetValue(current.parentId,out parent))throw new InvalidDataException(Corrupt);current=parent;}
            }
            path.Reverse();return path;
        }
        string CreateBranch(SessionData source,string newId,string title,List<AgentSessionEntry> branch){
            string targetTitle;
            if(title==null)targetTitle=Truncate("分叉 · "+(Text(source.Header,"title")??""),MaxTitle);
            else{if(title.Length>MaxTitle)throw new InvalidDataException(TitleTooLong);targetTitle=title;}
            string path=SessionPath(newId);if(File.Exists(path))throw new InvalidDataException(ExistsMessage);
            if(SessionFiles().Length>=MaxSessions)throw new InvalidDataException(TooMany);
            if(branch.Count+1>MaxEntries)throw new InvalidDataException(Space);
            var ids=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var entry in branch)ids[entry.id]=Guid.NewGuid().ToString("N");
            var copies=new List<AgentSessionEntry>();
            foreach(var entry in branch){
                var data=new Dictionary<string,object>(entry.data,StringComparer.Ordinal);
                if(entry.type=="compaction"){
                    string kept=Text(data,"firstKeptEntryId"),mapped=null;
                    if(!String.IsNullOrEmpty(kept)&&ids.ContainsKey(kept))mapped=ids[kept];
                    data["firstKeptEntryId"]=mapped??ids[entry.id];// retain-none when the kept entry was not copied
                }
                copies.Add(new AgentSessionEntry{type=entry.type,id=ids[entry.id],parentId=entry.parentId==null?null:ids[entry.parentId],created=entry.created,data=data});
            }
            string leafId=copies.Count==0?null:copies[copies.Count-1].id;
            var lines=new List<string>{Seal(new Dictionary<string,object>{{"type","session"},{"version",1},{"id",newId},{"created",Now()},{"title",targetTitle},{"cwd",Text(source.Header,"cwd")??""}})};
            foreach(var copy in copies)lines.Add(Seal(copy));
            if(leafId!=null)lines.Add(Seal(new AgentSessionEntry{type="leaf",id=Guid.NewGuid().ToString("N"),parentId=null,created=Now(),data=new Dictionary<string,object>{{"entryId",leafId}}}));
            long size=lines.Sum(line=>(long)Encoding.UTF8.GetByteCount(line)+1);
            if(size>MaxBytes)throw new InvalidDataException(Space);
            WriteNew(path,lines);return leafId;
        }
        static void WriteNew(string file,List<string> lines){
            string temp=file+".tmp";Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(temp,String.Join("\n",lines)+"\n",new UTF8Encoding(false));
            if(File.Exists(file)){try{File.Delete(temp);}catch{}throw new InvalidDataException(ExistsMessage);}
            File.Move(temp,file);
        }
        static void WriteReplace(string file,List<string> lines){
            string temp=file+".tmp";Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(temp,String.Join("\n",lines)+"\n",new UTF8Encoding(false));
            if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
        }
    }
}

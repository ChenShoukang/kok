using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace kok {
    public class AgentScope { public string mode="all",collection=""; public List<string> ids=new List<string>(); }
    public class AgentChange { public string id; public Dictionary<string,object> before,after; }
    public partial class Library {
        public List<string> ResolveAgentScope(AgentScope scope) {
            RequireAiMode();scope=scope??new AgentScope();lock(gate){
                if(!new[]{"all","selected","collection"}.Contains(scope.mode)||scope.ids==null||scope.ids.Count>24||scope.ids.Any(id=>Find(id)==null)||scope.mode=="selected"&&scope.ids.Count==0||scope.mode=="collection"&&(String.IsNullOrWhiteSpace(scope.collection)||!Db.collections.Contains(scope.collection)))throw new InvalidDataException("请选择有效的 Agent 资料范围。");
                return Db.items.Where(e=>scope.mode=="all"||scope.mode=="selected"&&scope.ids.Contains(e.id)||scope.mode=="collection"&&e.collection==scope.collection).Select(e=>e.id).ToList();
            }
        }
        public string AgentScopedCatalog(List<string> ids,int recent) {
            RequireAiMode();var allowed=new HashSet<string>(ids);lock(gate){var rows=Db.items.Where(e=>allowed.Contains(e.id)).OrderByDescending(e=>e.added,StringComparer.Ordinal).ToList();return "Allowed scope: "+rows.Count+" files. Search tools can search ONLY this scope.\nCollections: "+String.Join(", ",rows.Select(e=>e.collection).Where(x=>!String.IsNullOrEmpty(x)).Distinct().Take(40))+"\n"+String.Join("\n",rows.Take(recent).Select(AgentLine));}
        }
        // Compare every field in the batch before changing any entry. A later manual edit wins.
        public void ApplyAgentChanges(List<AgentChange> changes,string operationId="") {
            if(IsPrivate||changes==null||changes.Count<1||changes.Count>50||changes.Any(c=>c==null)||changes.Select(c=>c.id).Distinct().Count()!=changes.Count)throw new InvalidDataException("Agent 修改无效。");
            lock(gate){
                foreach(var c in changes){var e=Find(c.id);if(e==null||c.before==null||c.after==null||c.after.Count!=1||c.before.Count!=1||c.after.Keys.Any(k=>!new[]{"notes","tags","collection"}.Contains(k))||!c.before.ContainsKey(c.after.Keys.First()))throw new InvalidDataException("Agent 修改无效。");
                    string field=c.after.Keys.First();object current=field=="notes"?(object)(e.notes??""):field=="collection"?(object)(e.collection??""):(object)(e.tags??new List<string>());
                    if(Json.Serialize(current)!=Json.Serialize(c.before[field]))throw new InvalidOperationException("内容已被修改，已保留当前内容。请重新查看后再操作。");
                    if(field=="notes"&&(c.after[field]==null||Convert.ToString(c.after[field]).Length>100000)||field=="collection"&&(c.after[field]==null||Convert.ToString(c.after[field]).Length>100)||field=="tags"&&(c.after[field]==null||Json.Serialize(c.after[field]).Length>4000))throw new InvalidDataException("Agent 修改无效。");
                    if(field=="tags"){var tags=Json.Deserialize<List<string>>(Json.Serialize(c.after[field]));if(tags==null||tags.Count>40||tags.Any(t=>String.IsNullOrWhiteSpace(t)||t.Length>40||t.Any(Char.IsControl)))throw new InvalidDataException("标签格式无效。");}
                }
                var collections=Db.collections.ToList();object oldReceipts;bool hadReceipts=Db.settings.TryGetValue("agentToolReceipts",out oldReceipts);
                Action<AgentChange,Dictionary<string,object>> assign=(c,values)=>{var e=Find(c.id);string key=values.Keys.First();if(key=="notes")e.notes=Convert.ToString(values[key]);else if(key=="collection"){e.collection=Convert.ToString(values[key]);if(e.collection!=""&&!Db.collections.Contains(e.collection))Db.collections.Add(e.collection);}else e.tags=Json.Deserialize<List<string>>(Json.Serialize(values[key])).Select(t=>t.Trim()).Distinct().ToList();};
                try{foreach(var c in changes)assign(c,c.after);if(operationId!=""){var rows=hadReceipts?Json.Deserialize<Dictionary<string,Dictionary<string,object>>>(Json.Serialize(oldReceipts)):new Dictionary<string,Dictionary<string,object>>();if(rows.Count>=512)rows.Remove(rows.Keys.First());rows[operationId]=new Dictionary<string,object>{{"text","Changes applied"}};Db.settings["agentToolReceipts"]=rows;}Save();}
                catch{foreach(var c in changes)assign(c,c.before);Db.collections=collections;if(hadReceipts)Db.settings["agentToolReceipts"]=oldReceipts;else Db.settings.Remove("agentToolReceipts");throw;}
            }
        }
        public string AgentCreatedStamp(string id) {
            if(IsPrivate)throw new InvalidDataException("Agent 修改无效。");lock(gate){var e=Find(id);if(e==null||!String.Equals(Path.GetDirectoryName(Path.GetFullPath(e.path)),Path.GetFullPath(DocumentDirectory).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Agent 修改无效。");
                string hash;using(var sha=SHA256.Create())using(var input=File.OpenRead(e.path))hash=Convert.ToBase64String(sha.ComputeHash(input));return StudyHash(new{e.id,e.name,e.collection,e.notes,e.tags,e.editDraft,e.annotations,e.bookmarks,e.chapterTexts,hash});}
        }
        public void UndoAgentCreated(string id,string stamp) {lock(gate){if(AgentCreatedStamp(id)!=stamp)throw new InvalidOperationException("内容已被修改，已保留当前内容。请重新查看后再操作。");Remove(id);}}
        // Conversation data is profile-local and DPAPI protected, separate from settings and API credentials.
        public string ReadAgentSessions() {
            if(IsPrivate)throw new InvalidOperationException("私密文件夹不启用 AI，请先返回普通资料库。");
            lock(gate){string file=Path.Combine(DataDir,"agent-sessions.sealed");if(!File.Exists(file))return "[]";try{if(new FileInfo(file).Length>2200000)throw new InvalidDataException();return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file),null,DataProtectionScope.CurrentUser));}catch{throw new InvalidDataException("Agent 会话无法读取，原记录已保留。");}}
        }
        public void SaveAgentSessions(string text) {
            if(IsPrivate||text==null||Encoding.UTF8.GetByteCount(text)>1500000)throw new InvalidDataException("Agent 会话过大，未保存。");
            var rows=Json.Deserialize<List<Dictionary<string,object>>>(text);if(rows==null||rows.Count>12||rows.Any(r=>!r.ContainsKey("id")||!System.Text.RegularExpressions.Regex.IsMatch(Convert.ToString(r["id"]),"^[a-f0-9]{32}$")))throw new InvalidDataException("Agent 会话无效。");
            lock(gate){string file=Path.Combine(DataDir,"agent-sessions.sealed"),temp=file+".tmp";byte[] data=ProtectedData.Protect(Encoding.UTF8.GetBytes(text),null,DataProtectionScope.CurrentUser);File.WriteAllBytes(temp,data);if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}
        }
    }
}

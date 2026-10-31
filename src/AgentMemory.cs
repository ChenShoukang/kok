using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace kok {
 public sealed class AgentMemoryRecord {
  public string id,title,text,revision,updated,taskId="",callId="";
  public List<string> ids=new List<string>();public Dictionary<string,string> sources=new Dictionary<string,string>();
 }
 public sealed class AgentMemoryChange {public string id,token;public AgentMemoryRecord before,after;}
 public sealed class AgentMemoryData {public List<AgentMemoryRecord> items=new List<AgentMemoryRecord>();public List<AgentMemoryChange> history=new List<AgentMemoryChange>();}
 // Normal-profile memory is a distinct encrypted store. It is never a source of tool permission.
 public sealed partial class AgentMemoryStore {
  readonly Library library;readonly string file;readonly object gate=new object();
  public AgentMemoryStore(Library library){this.library=library;file=Path.Combine(library.DataDir,"agent-memory.sealed");}
  void Ensure(){if(library.IsPrivate)throw new InvalidOperationException("私密文件夹不使用长期记忆。");}
  AgentMemoryData Read(){Ensure();if(!File.Exists(file))return new AgentMemoryData();try{if(new FileInfo(file).Length>2*1024*1024)throw new InvalidDataException();var data=Library.Json.Deserialize<AgentMemoryData>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file),null,DataProtectionScope.CurrentUser)));if(data==null||data.items==null||data.history==null||data.items.Count>128||data.history.Count>80||data.items.Any(r=>r==null||!AgentJournal.ValidId(r.id)||!AgentJournal.ValidId(r.revision)||r.title==null||r.title.Length>100||r.text==null||r.text.Length>2000||r.ids==null||r.ids.Count>8||r.sources==null))throw new InvalidDataException();return data;}catch{throw new InvalidDataException("长期记忆无法读取，原记录已保留。");}}
  void Write(AgentMemoryData data){string json=Library.Json.Serialize(data);if(json.Length>1000000)throw new InvalidDataException("长期记忆空间不足，请先整理记录。");Directory.CreateDirectory(library.DataDir);string temp=file+".tmp";File.WriteAllBytes(temp,ProtectedData.Protect(Encoding.UTF8.GetBytes(json),null,DataProtectionScope.CurrentUser));if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}
  static T Clone<T>(T value){return Library.Json.Deserialize<T>(Library.Json.Serialize(value));}
  public bool Fresh(AgentMemoryRecord record){return record.ids.All(id=>library.Find(id)!=null)&&Library.Json.Serialize(record.sources)==Library.Json.Serialize(library.AgentSourceStamps(record.ids));}
  public List<AgentMemoryRecord> List(){lock(gate){return Read().items.Where(r=>r.ids.All(id=>library.Find(id)!=null)).Select(Clone).ToList();}}
  public string LastUndoToken(){lock(gate)return Read().history.LastOrDefault()?.token??"";}
  public bool Current(string id,string revision){lock(gate){return Read().items.Any(r=>r.id==id&&r.revision==revision&&Fresh(r));}}
  public AgentMemoryRecord Save(AgentMemoryRecord proposed,string expected){lock(gate){
   Ensure();if(proposed==null||String.IsNullOrWhiteSpace(proposed.title)||proposed.title.Length>100||String.IsNullOrWhiteSpace(proposed.text)||proposed.text.Length>2000||proposed.ids==null||proposed.ids.Count>8||proposed.ids.Any(id=>!AgentJournal.ValidId(id)||library.Find(id)==null)||Regex.IsMatch(proposed.text,@"sk-(?:proj-)?[A-Za-z0-9_-]{16,}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_|BEGIN .*PRIVATE KEY",RegexOptions.IgnoreCase))throw new InvalidDataException("记忆内容无效，请勿保存密钥或密码。");
   var data=Read();var before=data.items.FirstOrDefault(r=>r.id==proposed.id);if((before==null?"":before.revision)!=(expected??""))throw new AgentConflictException("记忆已被修改，请重新打开后再操作。");
   if(before==null&&String.IsNullOrEmpty(expected)){var target=data.items.FirstOrDefault(r=>r.id!=proposed.id&&SameSourceSet(r.ids,proposed.ids)&&NearDuplicate(r.text,proposed.text));if(target!=null)before=target;}
   if(before==null&&data.items.Count>=128)throw new InvalidDataException("长期记忆最多保存 128 条，请先整理记录。");
   var next=Clone(proposed);next.id=before==null?(AgentJournal.ValidId(proposed.id)?proposed.id:Guid.NewGuid().ToString("N")):before.id;next.ids=next.ids.Distinct().ToList();next.sources=library.AgentSourceStamps(next.ids);next.revision=Guid.NewGuid().ToString("N");next.updated=DateTime.UtcNow.ToString("o");
   data.items.RemoveAll(r=>r.id==next.id);data.items.Add(next);data.history.Add(new AgentMemoryChange{id=next.id,token=next.revision,before=Clone(before),after=Clone(next)});data.history=data.history.Skip(Math.Max(0,data.history.Count-80)).ToList();Write(data);return Clone(next);
  }}
  public string Delete(string id,string expected){lock(gate){var data=Read();var before=data.items.FirstOrDefault(r=>r.id==id);if(before==null||before.revision!=expected)throw new AgentConflictException("记忆已被修改，请重新打开后再操作。");string token=Guid.NewGuid().ToString("N");data.items.Remove(before);data.history.Add(new AgentMemoryChange{id=id,token=token,before=Clone(before)});data.history=data.history.Skip(Math.Max(0,data.history.Count-80)).ToList();Write(data);return token;}}
  public void Undo(string token){lock(gate){var data=Read();var change=data.history.LastOrDefault(h=>h.token==token);if(change==null)throw new InvalidDataException("记忆历史已不可用。");var current=data.items.FirstOrDefault(r=>r.id==change.id);var latest=data.history.LastOrDefault(h=>h.id==change.id);if(latest!=change||Library.Json.Serialize(current)!=Library.Json.Serialize(change.after))throw new AgentConflictException("记忆已被修改，请重新打开后再操作。");data.items.RemoveAll(r=>r.id==change.id);if(change.before!=null)data.items.Add(Clone(change.before));data.history.Remove(change);Write(data);}}
  public List<AgentMemoryRecord> Search(string query,IEnumerable<string> allowed,int limit=3){lock(gate){var scope=new HashSet<string>(allowed);var terms=Regex.Matches((query??"").ToLowerInvariant(),@"[a-z0-9]{2,}|[\u3400-\u9fff]{1,}").Cast<Match>().SelectMany(m=>m.Value.Any(c=>c>127)&&m.Length>2?Enumerable.Range(0,m.Length-1).Select(i=>m.Value.Substring(i,2)):new[]{m.Value}).Distinct().Take(80).ToList();return Read().items.Where(r=>r.ids.All(scope.Contains)&&Fresh(r)).Select(r=>new{record=r,score=terms.Count(t=>(r.title+" "+r.text).ToLowerInvariant().Contains(t))}).Where(r=>r.score>0).OrderByDescending(r=>r.score).ThenByDescending(r=>r.record.updated,StringComparer.Ordinal).Take(Math.Max(1,Math.Min(5,limit))).Select(r=>Clone(r.record)).ToList();}}
 }
}

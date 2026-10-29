using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace kok {
    public class AgentEvent {public int sequence;public string id,kind,created;public Dictionary<string,object> data;}
    public class AgentTaskIndex {public string id,sessionId,title,status,updated;public int sequence;public long bytes;}
    // Immutable, individually encrypted events. A crash before rename leaves only an ignored temp file.
    // Full tool results occur in one event, never in the UI-session snapshot or an automatic fork.
    public sealed class AgentJournal {
        readonly string root,indexFile;readonly object gate=new object();
        public const int MaxTasks=64,MaxEvents=256;public const long MaxBytes=8*1024*1024;
        public AgentJournal(string dataDir){root=Path.Combine(dataDir,"agent-tasks");indexFile=Path.Combine(root,"index.sealed");}
        public static bool ValidId(string id){return id!=null&&Regex.IsMatch(id,"^[a-f0-9]{32}$");}
        string TaskPath(string id){if(!ValidId(id))throw new InvalidDataException("任务记录无效。");string path=Path.GetFullPath(Path.Combine(root,id));if(!path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("任务记录无效。");return path;}
        static T Read<T>(string file){try{var bytes=ProtectedData.Unprotect(File.ReadAllBytes(file),null,DataProtectionScope.CurrentUser);return Library.Json.Deserialize<T>(Encoding.UTF8.GetString(bytes));}catch{throw new InvalidDataException("任务记录无法读取，原记录已保留。");}}
        static void Write(string file,object value){byte[] bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(Library.Json.Serialize(value)),null,DataProtectionScope.CurrentUser);Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+".tmp";File.WriteAllBytes(temp,bytes);if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}
        public List<AgentTaskIndex> List(){lock(gate){return File.Exists(indexFile)?Read<List<AgentTaskIndex>>(indexFile):new List<AgentTaskIndex>();}}
        public void CheckCapacity(string id,long reserve){lock(gate){var dir=TaskPath(id);long used=Directory.Exists(dir)?Directory.GetFiles(dir,"*.sealed").Sum(f=>new FileInfo(f).Length):0;if(used+reserve>MaxBytes)throw new InvalidDataException("任务记录空间不足，请导出交接记录后开始新任务。");}}
        public List<AgentEvent> Events(string id){lock(gate){var dir=TaskPath(id);if(!Directory.Exists(dir))throw new InvalidDataException("找不到任务记录。");var files=Directory.GetFiles(dir,"*.sealed").OrderBy(f=>f,StringComparer.Ordinal).ToArray();if(files.Length>MaxEvents||files.Sum(f=>new FileInfo(f).Length)>MaxBytes)throw new InvalidDataException("任务记录无效。");var rows=new List<AgentEvent>();foreach(var file in files){var e=Read<AgentEvent>(file);if(e==null||e.sequence!=rows.Count+1||!ValidId(e.id)||e.data==null)throw new InvalidDataException("任务记录无法读取，原记录已保留。");rows.Add(e);}return rows;}}
        public AgentEvent Append(string id,string kind,object data,string sessionId,string title,string status){lock(gate){
            var dir=TaskPath(id);var index=List();var item=index.FirstOrDefault(x=>x.id==id);if(item==null&&index.Count>=MaxTasks)throw new InvalidDataException("任务记录已满，请先删除不需要的已结束任务。");
            int count=Directory.Exists(dir)?Directory.GetFiles(dir,"*.sealed").Length:0;if(count>=MaxEvents)throw new InvalidDataException("任务记录空间不足，请导出交接记录后开始新任务。");
            var value=new AgentEvent{sequence=count+1,id=Guid.NewGuid().ToString("N"),kind=kind,created=DateTime.UtcNow.ToString("o"),data=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(data))};
            CheckCapacity(id,Encoding.UTF8.GetByteCount(Library.Json.Serialize(value))+1024);Write(Path.Combine(dir,value.sequence.ToString("D5")+".sealed"),value);
            if(item==null){item=new AgentTaskIndex{id=id};index.Add(item);}item.sessionId=sessionId;item.title=title;item.status=status;item.sequence=value.sequence;item.updated=value.created;item.bytes=Directory.GetFiles(dir,"*.sealed").Sum(f=>new FileInfo(f).Length);
            Write(indexFile,index);return value;
        }}
        public void Delete(string id){lock(gate){var dir=TaskPath(id);var index=List();index.RemoveAll(x=>x.id==id);Write(indexFile,index);if(Directory.Exists(dir))Directory.Delete(dir,true);}}
    }
}

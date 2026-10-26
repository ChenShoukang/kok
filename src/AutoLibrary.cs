using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

namespace kok {
    public class WatchFolder { public string id,path; public bool enabled=true; public string scanned=""; }
    public class OrganizeInput { public string id,name,kind,format,folder,excerpt,fingerprint; public long bytes; }
    public class OrganizeSuggestion { public string id,category,summary,fingerprint; public List<string> tags; public bool content; }
    public partial class Library {
        public List<WatchFolder> Folders() { lock(gate)return Json.Deserialize<List<WatchFolder>>(Json.Serialize(Db.watchFolders)); }
        public WatchFolder AddFolder(string path) {
            path=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!Directory.Exists(path))throw new DirectoryNotFoundException("文件夹不可用，请重新选择。");
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("请选择实际文件夹，暂不扫描链接目录。");
            lock(gate){var existing=Db.watchFolders.FirstOrDefault(x=>String.Equals(x.path,path,StringComparison.OrdinalIgnoreCase));if(existing!=null)return existing;
                var folder=new WatchFolder{id=Guid.NewGuid().ToString("N"),path=path};Db.watchFolders.Add(folder);Save();return folder;}
        }
        public void ChangeFolder(string id,bool? enabled) { lock(gate){var folder=Db.watchFolders.FirstOrDefault(x=>x.id==id);if(folder==null)return;if(enabled.HasValue)folder.enabled=enabled.Value;else Db.watchFolders.Remove(folder);Save();} }
        public bool HasEnabledFolders() { lock(gate)return Db.watchFolders.Any(x=>x.enabled); }
        static bool Inside(string path,string folder) { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).StartsWith(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||String.Equals(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar),Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase); }
        public ImportResult ScanFolders(string applicationDirectory,CancellationToken token,Action<int> progress) {
            var folders=Folders().Where(x=>x.enabled).ToList();var errors=new List<string>();
            var excluded=new[]{DataDir,applicationDirectory,Environment.GetFolderPath(Environment.SpecialFolder.Windows),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}.Where(x=>!String.IsNullOrWhiteSpace(x)).ToArray();
            HashSet<string> known;lock(gate)known=new HashSet<string>(Db.items.Select(x=>x.path).Concat(Db.ignoredAutoPaths),StringComparer.OrdinalIgnoreCase);
            var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var report=Import(ScanFiles(folders,excluded,known,visited,errors,token),progress);
            report.errors.AddRange(errors.Take(Math.Max(0,20-report.errors.Count)));
            lock(gate){foreach(var folder in Db.watchFolders.Where(x=>folders.Any(f=>f.id==x.id)))folder.scanned=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");Save();}return report;
        }
        static IEnumerable<string> ScanFiles(List<WatchFolder> folders,string[] excluded,HashSet<string> known,HashSet<string> visited,List<string> errors,CancellationToken token) {
            var stack=new Stack<string>(folders.Select(x=>x.path));
            while(stack.Count>0){token.ThrowIfCancellationRequested();string dir=stack.Pop();if(!visited.Add(Path.GetFullPath(dir))||excluded.Any(x=>Inside(dir,x)))continue;
                string[] files,children;
                try {var attributes=File.GetAttributes(dir);if((attributes&(FileAttributes.ReparsePoint|FileAttributes.Hidden|FileAttributes.System))!=0)continue;files=Directory.GetFiles(dir);children=Directory.GetDirectories(dir);}
                catch(Exception ex){if(errors.Count<20)errors.Add(dir+": "+ex.Message);continue;}
                foreach(string file in files){token.ThrowIfCancellationRequested();if(Classify(file)==null||known.Contains(file))continue;bool allowed=false;try{allowed=(File.GetAttributes(file)&(FileAttributes.Hidden|FileAttributes.System|FileAttributes.ReparsePoint))==0;}catch{}if(allowed&&known.Add(file))yield return file;}
                foreach(string child in children)stack.Push(child);
            }
        }
        public static string OrganizeFingerprint(Entry entry,bool content) {
            var info=new FileInfo(entry.path);if(!info.Exists)throw new FileNotFoundException("找不到待整理文件，请重新扫描。");
            using(var sha=SHA256.Create())return String.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(entry.path+"|"+info.Length+"|"+info.LastWriteTimeUtc.Ticks+"|"+content+"|"+(entry.kind=="music"?Json.Serialize(new{audio=entry.audio,artist=entry.artist,album=entry.album}):""))).Select(b=>b.ToString("x2")));
        }
        public List<OrganizeInput> OrganizeInputs(List<string> ids,bool content) {
            if(ids==null||ids.Count==0||ids.Count>50||ids.Distinct().Count()!=ids.Count)throw new InvalidDataException("每次请选择 1 至 50 个文件。");
            var result=new List<OrganizeInput>();
            foreach(var id in ids){Entry item=Find(id);if(item==null)throw new FileNotFoundException("找不到待整理文件，请重新扫描。");
                string excerpt="";
                if(item.kind=="music")excerpt=Json.Serialize(new{title=item.audio==null?item.name:item.audio.title,artist=item.artist,album=item.album,genre=item.audio==null?"":item.audio.genre,category=item.audio==null?"music":item.audio.category,duration=item.audio==null?0:item.audio.duration,source=item.audio==null?"":item.audio.source});
                if(content&&new[]{"TXT","MD","MARKDOWN","CSV","LOG","JSON","DOCX","EPUB"}.Contains(item.ext)&&new FileInfo(item.path).Length<=2*1024*1024){try{excerpt=String.Join("\n",ReadBook(item).chapters.Select(x=>x.text));if(excerpt.Length>1200)excerpt=excerpt.Substring(0,1200);if(excerpt.Length>0&&Char.IsHighSurrogate(excerpt[excerpt.Length-1]))excerpt=excerpt.Substring(0,excerpt.Length-1);}catch{}}
                result.Add(new OrganizeInput{id=id,name=item.name,kind=item.kind,format=item.ext,folder=new DirectoryInfo(Path.GetDirectoryName(item.path)).Name,bytes=new FileInfo(item.path).Length,excerpt=excerpt,fingerprint=OrganizeFingerprint(item,content)});
            }return result;
        }
        public int ApplyOrganization(List<OrganizeSuggestion> suggestions,List<string> ids) {
            if(ids==null||ids.Count>50||ids.Distinct().Count()!=ids.Count||ids.Any(id=>!suggestions.Any(x=>x.id==id)))throw new InvalidDataException("整理结果已失效，请重新分析。");
            lock(gate){var selected=suggestions.Where(x=>ids.Contains(x.id)).ToList();
                foreach(var row in selected){var item=Find(row.id);if(item==null||OrganizeFingerprint(item,row.content)!=row.fingerprint)throw new InvalidDataException("文件已变化，请重新分析后应用。");ValidateOrganization(row);}
                foreach(var row in selected){var item=Find(row.id);item.collection=row.category;item.aiCategory=row.category;item.aiSummary=row.summary;item.tags=row.tags;item.aiFingerprint=row.fingerprint;if(!Db.collections.Contains(row.category))Db.collections.Add(row.category);}Save();return selected.Count;}
        }
        public void EditOrganization(string id,string category,List<string> tags,string summary) {
            var row=new OrganizeSuggestion{category=category.Trim(),tags=tags,summary=summary.Trim()};ValidateOrganization(row);
            lock(gate){var item=Find(id);if(item==null)throw new InvalidDataException("找不到待整理文件，请重新扫描。");item.collection=row.category;item.aiCategory=row.category;item.tags=row.tags;item.aiSummary=row.summary;if(!Db.collections.Contains(row.category))Db.collections.Add(row.category);Save();}
        }
        public static void ValidateOrganization(OrganizeSuggestion row) {
            if(String.IsNullOrWhiteSpace(row.category)||row.category.Length>60||row.category.Any(Char.IsControl)||row.summary==null||row.summary.Length>400||row.tags==null||row.tags.Count>8||row.tags.Any(x=>String.IsNullOrWhiteSpace(x)||x.Length>32||x.Any(Char.IsControl)))throw new InvalidDataException("模型返回的分类格式无效，未应用结果。");
            row.tags=row.tags.Select(x=>x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}

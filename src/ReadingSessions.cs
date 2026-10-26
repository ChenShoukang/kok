using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace kok {
    public partial class MainWindow {
        static readonly string[] ReadingSessionActions={"readTranslationView","saveTranslationView","deleteTranslationView","moveToPrivate"};
        string TranslationViewPath(Entry entry){using(var hash=System.Security.Cryptography.SHA256.Create())return Path.Combine(normalLibrary.DataDir,"translations","view-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(entry.id))).Replace("-","").ToLowerInvariant()+".json");}
        async Task<object> HandleReadingSessionAction(string action,Dictionary<string,object> args){
            if(action=="moveToPrivate")return await MoveRecordsToPrivate(args);
            if(library.IsPrivate)throw new InvalidOperationException("私密文件夹不保存 AI 译文。");var entry=RequireEntry(args);string path=TranslationViewPath(entry);
            if(action=="deleteTranslationView"){if(File.Exists(path))File.Delete(path);return true;}
            var info=new FileInfo(entry.path);string stamp=info.Length+"|"+info.LastWriteTimeUtc.Ticks;using(var hash=System.Security.Cryptography.SHA256.Create())stamp+="|"+Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(Library.Json.Serialize(entry.chapterTexts))));
            if(action=="readTranslationView")return await Task.Run(()=>{if(!File.Exists(path)||new FileInfo(path).Length>24*1024*1024)return null;try{var document=Library.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path,Encoding.UTF8));return Convert.ToString(document["sourceStamp"])==stamp?document["view"]:null;}catch{return null;}});
            var view=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(args["view"]));
            if(view==null||!view.ContainsKey("parts")||Convert.ToString(view.ContainsKey("itemId")?view["itemId"]:"")!=entry.id)throw new InvalidDataException("译文与原文件不匹配。");
            var allowed=new[]{"kind","itemId","name","title","titleIsUi","parts","originalParts","headings","headingUi","pages","pageRatios","format","rebuilt","marked","signature","indices","language","complete","scope","layout","readingMarks","comparisonPosition","comparisonMode","sourceIndex","cues"};view=view.Where(p=>allowed.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value);string json=Library.Json.Serialize(new{sourceStamp=stamp,view=view});if(json.Length>12*1024*1024)throw new InvalidDataException("译文文件过大，请按章节或页面导出。");
            var target=library;backgroundLibraryWrites++;try{await Task.Run(()=>{Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,json,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}});}finally{backgroundLibraryWrites--;}return true;
        }
        async Task<object> MoveRecordsToPrivate(Dictionary<string,object> args){
            if(library.IsPrivate||privateOpening||importing)throw new InvalidOperationException("请先回到普通资料库再移动文件。");
            var ids=Library.Json.Deserialize<List<string>>(Library.Json.Serialize(args["ids"]));if(ids==null||ids.Count==0||ids.Count>200||ids.Distinct().Count()!=ids.Count)throw new InvalidDataException("请选择 1 至 200 个文件。");var entries=ids.Select(id=>normalLibrary.Find(id)).ToList();if(entries.Any(e=>e==null))throw new InvalidDataException("文件已移除，请刷新资料库。");
            if(DateTime.UtcNow<nextVaultAttempt)throw new InvalidOperationException("密码尝试过于频繁，请稍后重试。");string password=StringArg(args,"password");privateOpening=true;bool writing=false;
            try{await StopAiForPrivate();backgroundLibraryWrites++;writing=true;await SaveCurrentSession();await Task.Run(()=>{using(var session=(!File.Exists(Path.Combine(VaultDirectory,"vault-info.json"))&&args.ContainsKey("create")&&Convert.ToBoolean(args["create"]))?VaultSession.Create(VaultDirectory,password):VaultSession.Open(VaultDirectory,password)){var target=new Library(VaultDirectory,session);foreach(var entry in entries){if(target.Db.items.Any(i=>String.Equals(i.path,entry.path,StringComparison.OrdinalIgnoreCase)))continue;target.Import(new[]{entry.path},null);var copy=Library.Json.Deserialize<Entry>(Library.Json.Serialize(entry));var imported=target.Db.items.FirstOrDefault(i=>String.Equals(i.path,entry.path,StringComparison.OrdinalIgnoreCase));if(imported==null)throw new InvalidDataException("文件导入失败，普通资料库的记录已保留。");copy.id=imported.id;copy.hasCover=imported.hasCover;copy.coverVersion=imported.coverVersion;target.Db.items[target.Db.items.IndexOf(imported)]=copy;if(!String.IsNullOrWhiteSpace(copy.collection)&&!target.Db.collections.Contains(copy.collection))target.Db.collections.Add(copy.collection);}target.Save();}});
                foreach(var entry in entries){normalLibrary.Remove(entry.id);string view=TranslationViewPath(entry);if(File.Exists(view))File.Delete(view);}foreach(var preview in previewWindows.Where(w=>ids.Contains(w.ItemId)).ToArray())preview.CloseWithoutSaving();failedVaultAttempts=0;Notify("libraryChanged",null);return new{moved=entries.Count};
            }catch{RegisterVaultFailure();throw;}finally{if(writing)backgroundLibraryWrites--;privateOpening=false;}
        }
    }
}

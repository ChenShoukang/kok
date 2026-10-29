using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace kok {
 public partial class Library {
  sealed class PrivateMoveCleanup {public List<string> ids=new List<string>();public List<string> clips=new List<string>();}
  string PrivateMovePath {get{return Path.Combine(DataDir,"private-move.pending.json");}}

  public void TransferToPrivate(Library target,IEnumerable<string> itemIds){
   if(IsPrivate||target==null||!target.IsPrivate)throw new InvalidOperationException("请先回到普通资料库再移动文件。");
   lock(gate){
    FinishPrivateMove();var ids=itemIds.ToList();if(ids.Count<1||ids.Count>200||ids.Distinct().Count()!=ids.Count)throw new InvalidDataException("请选择 1 至 200 个文件。");
    var entries=ids.Select(Find).ToList();if(entries.Any(e=>e==null))throw new InvalidDataException("文件已移除，请刷新资料库。");
    if(entries.Any(e=>target.Db.items.Any(t=>String.Equals(t.path,e.path,StringComparison.OrdinalIgnoreCase))))throw new InvalidDataException("私密文件夹已存在相同文件，请先检查两边的记录。");
    if(entries.Any(e=>!File.Exists(e.path)))throw new InvalidDataException("文件导入失败，普通资料库的记录已保留。");
    var mapping=new Dictionary<string,string>();
    foreach(var entry in entries){
     var copy=Json.Deserialize<Entry>(Json.Serialize(entry));copy.id=Guid.NewGuid().ToString("N");mapping[entry.id]=copy.id;
     copy.hasCover=File.Exists(ThumbnailPath(entry));if(copy.hasCover){using(var input=OpenThumbnail(entry))using(var output=new MemoryStream()){input.CopyTo(output);target.WriteThumbnail(target.ThumbnailPath(copy),copy,output.ToArray());}}
     target.Db.items.Add(copy);if(!String.IsNullOrWhiteSpace(copy.collection)&&!target.Db.collections.Contains(copy.collection))target.Db.collections.Add(copy.collection);
    }
    var clips=Db.workspace.clips.Where(c=>c.source!=null&&ids.Contains(c.source.itemId)).ToList();
    foreach(var clip in clips){var copy=Json.Deserialize<ClipRecord>(Json.Serialize(clip));copy.id=Guid.NewGuid().ToString("N");copy.source.itemId=mapping[clip.source.itemId];target.Db.workspace.clips.Add(copy);}
    target.Save(false,null,new string[0],true);
    // Only random IDs are journaled, after the encrypted destination has been saved.
    // Cleanup failures are reported and retried before the next ordinary-library load.
    var pending=new PrivateMoveCleanup{ids=ids,clips=clips.Select(c=>c.id).ToList()};
    string temp=PrivateMovePath+".tmp";File.WriteAllText(temp,Json.Serialize(pending),new UTF8Encoding(false));File.Move(temp,PrivateMovePath);FinishPrivateMove();
   }
  }

  static bool RedactPrivateOperation(Operation op,HashSet<string> ids,HashSet<string> clips){
   bool changed=false;
   if(op.patches!=null)changed|=op.patches.RemoveAll(p=>ids.Contains(p.id))>0;
   if(op.resources!=null){
    changed|=op.resources.RemoveAll(p=>p.kind=="item"&&ids.Contains(p.id)||p.kind=="clip"&&(clips.Contains(p.id)||ClipHasSource(p.before,ids)||ClipHasSource(p.after,ids)))>0;
    foreach(var p in op.resources.Where(p=>p.kind=="space")){string before=RedactSpace(p.before,ids,clips),after=RedactSpace(p.after,ids,clips);changed|=before!=p.before||after!=p.after;p.before=before;p.after=after;}
   }
   if(ids.Contains(op.createdItemId??"")){op.createdItemId=null;op.createdStamp=null;changed=true;}
   if(changed){op.fileVersion=null;if((op.patches==null||op.patches.Count==0)&&(op.resources==null||op.resources.Count==0)){op.state="skipped";op.title="已移入私密文件夹";}}
   return changed;
  }
  static bool ClipHasSource(string json,HashSet<string> ids){if(json==null)return false;var c=Json.Deserialize<ClipRecord>(json);return c!=null&&c.source!=null&&ids.Contains(c.source.itemId);}
  static string RedactSpace(string json,HashSet<string> ids,HashSet<string> clips){if(json==null)return null;var s=Json.Deserialize<SpaceRecord>(json);bool changed=s.members.RemoveAll(id=>ids.Contains(id)||clips.Contains(id))>0;foreach(var id in ids.Concat(clips))changed|=s.positions.Remove(id);return changed?Json.Serialize(s):json;}
  static void RedactPrivateRecords(Database data,HashSet<string> ids,HashSet<string> clips){
   data.items.RemoveAll(e=>ids.Contains(e.id));data.workspace.clips.RemoveAll(c=>clips.Contains(c.id)||c.source!=null&&ids.Contains(c.source.itemId));
   foreach(var s in data.workspace.spaces){s.members.RemoveAll(id=>ids.Contains(id)||clips.Contains(id));foreach(var id in ids.Concat(clips))s.positions.Remove(id);}
   foreach(var op in data.operations)RedactPrivateOperation(op,ids,clips);
  }
  void FinishPrivateMove(){
   if(IsPrivate||!File.Exists(PrivateMovePath))return;
   var pending=Json.Deserialize<PrivateMoveCleanup>(File.ReadAllText(PrivateMovePath,Encoding.UTF8));
   if(pending==null||pending.ids==null||pending.clips==null||pending.ids.Count<1||pending.ids.Count>200||pending.ids.Concat(pending.clips).Any(id=>!AgentJournal.ValidId(id)))throw new InvalidDataException("私密移动清理记录无效，请保留数据后重试。");
   var ids=new HashSet<string>(pending.ids);var clips=new HashSet<string>(pending.clips);
   var changed=store.AllOperations().Where(op=>RedactPrivateOperation(op,ids,clips)).ToList();
   foreach(var item in Db.items.Where(e=>ids.Contains(e.id)))if(!Db.ignoredAutoPaths.Contains(item.path))Db.ignoredAutoPaths.Add(item.path);
   RedactPrivateRecords(Db,ids,clips);
   foreach(var op in changed){Db.operations.RemoveAll(o=>o.id==op.id);Db.operations.Add(op);}
   Save(false,ids,changed.Select(o=>o.id),true);
   store.CompactAfterPrivateMove(ids);
   // Rewrite only affected records inside application-owned migration snapshots.
   // Unrelated entries, history and unknown metadata fields are kept.
   foreach(var name in new[]{"library.json","library.json.bak","library.json.pre-0.8.1"}){
    string path=Path.Combine(DataDir,name);if(!File.Exists(path))continue;
    var raw=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path,Encoding.UTF8));var data=Json.Deserialize<Database>(Json.Serialize(raw));RedactPrivateRecords(data,ids,clips);
    foreach(var key in new[]{"items","operations","workspace"})if(raw.ContainsKey(key))raw[key]=key=="items"?(object)data.items:key=="operations"?(object)data.operations:data.workspace;
    string temp=path+".private-clean.tmp";File.WriteAllText(temp,Json.Serialize(raw),new UTF8Encoding(false));File.Replace(temp,path,null);
   }
   foreach(var id in ids){string thumb=Path.Combine(DataDir,"thumbs",id+".jpg");if(File.Exists(thumb))File.Delete(thumb);
    using(var hash=System.Security.Cryptography.SHA256.Create()){string view=Path.Combine(DataDir,"translations","view-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-","").ToLowerInvariant()+".json");if(File.Exists(view))File.Delete(view);}
   }
   File.Delete(PrivateMovePath);RememberItems();
  }
 }
}

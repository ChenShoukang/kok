using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace kok {
 public partial class Library {
  readonly Dictionary<string,string> committedItems=new Dictionary<string,string>();readonly HashSet<string> committedOperations=new HashSet<string>();
  static string ItemHash(string json){using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json)));}
  void RememberItems(IEnumerable<string> ids=null){if(ids==null)committedItems.Clear();else foreach(var id in ids)committedItems.Remove(id);foreach(var item in Db.items.Where(i=>ids==null||ids.Contains(i.id)))committedItems[item.id]=ItemHash(Json.Serialize(item));committedOperations.Clear();foreach(var op in Db.operations)committedOperations.Add(op.id);if(Db.operations.Count>512)Db.operations=Db.operations.OrderByDescending(o=>o.created).Take(512).Reverse().ToList();}
  List<Operation> CaptureChanges(out HashSet<string> dirty){
   dirty=new HashSet<string>();var result=new List<Operation>();var edited=new List<AgentChange>();var removed=new List<ResourcePatch>();var imported=new List<ResourcePatch>();var live=new HashSet<string>(Db.items.Select(i=>i.id));
   foreach(var item in Db.items){string previous,current=Json.Serialize(item);if(!committedItems.TryGetValue(item.id,out previous)){dirty.Add(item.id);if(!OperationRows.Any(o=>o.createdItemId==item.id&&o.state=="applied"))imported.Add(new ResourcePatch{kind="item",id=item.id,before=null,after=current});continue;}if(ItemHash(current)==previous)continue;dirty.Add(item.id);
    var before=store.ReadItem(item.id);if(before==null)throw new InvalidDataException("资料库校验失败，原数据已保留。");var changes=new AgentChange{id=item.id,before=new Dictionary<string,object>(),after=new Dictionary<string,object>()};foreach(var field in typeof(Entry).GetFields().Where(f=>ModuleRegistry.Builtins.FieldOwner(f.Name)!=null)){var a=field.GetValue(before);var b=field.GetValue(item);if(Json.Serialize(a)!=Json.Serialize(b)){changes.before[field.Name]=a;changes.after[field.Name]=b;}}
    changes.before=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(changes.before));changes.after=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(changes.after));if(changes.after.Count>0)edited.Add(changes);
   }
   if(edited.Count>0)result.Add(new Operation{id=OperationOrigin.Id??Guid.NewGuid().ToString("N"),module="library",source=OperationOrigin.Source??"user",title="编辑资料",created=DateTime.UtcNow.ToString("o"),state="applied",patches=edited});
   if(imported.Count>0)result.Add(new Operation{id=Guid.NewGuid().ToString("N"),module="library",source=OperationOrigin.Source??"user",title="导入资料",created=DateTime.UtcNow.ToString("o"),state="applied",resources=imported});
   foreach(var row in committedItems.Where(r=>!live.Contains(r.Key))){dirty.Add(row.Key);removed.Add(new ResourcePatch{kind="item",id=row.Key,before=Json.Serialize(store.ReadItem(row.Key)),after=null});}
   if(removed.Count>0)result.Add(new Operation{id=Guid.NewGuid().ToString("N"),module="library",source=OperationOrigin.Source??"user",title="移除资料",created=DateTime.UtcNow.ToString("o"),state="applied",resources=removed});
   return result;
  }
 }
}

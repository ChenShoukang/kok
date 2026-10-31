using System;
using System.Linq;
namespace kok {
 internal static class AgentEvidence {
  // Later writes count only when their approved before/after chain is continuous.
  internal static bool ChangesCurrent(Library library,AgentTaskState task,AgentCall call){
   if(call.changes==null)return true;
   foreach(var change in call.changes){var item=library.Find(change.id);if(item==null)return false;foreach(var field in change.after){object expected=field.Value;
    if(task!=null)foreach(var later in task.calls.Where(c=>c.sequence>call.sequence&&c.status=="done"&&c.changes!=null).OrderBy(c=>c.sequence)){var next=later.changes.FirstOrDefault(c=>c.id==change.id&&c.after.ContainsKey(field.Key));if(next==null)continue;if(!next.before.ContainsKey(field.Key)||Library.Json.Serialize(next.before[field.Key])!=Library.Json.Serialize(expected))return false;expected=next.after[field.Key];}
    if(Library.Json.Serialize(typeof(Entry).GetField(field.Key).GetValue(item))!=Library.Json.Serialize(expected))return false;
   }}return true;
  }
 }
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace kok {
 // Storage format is independent from kernel semantics. Legacy bytes are never rewritten.
 internal static class AgentEventContract {
  internal const int CurrentVersion=1;
  static readonly HashSet<string> Kinds=new HashSet<string>(("task.created task.running task.phase task.plan task.verified task.review_invalidated task.protocol task.adjusted task.continued task.note task.waiting task.cancel_requested task.cancelled task.paused task.stopped task.failed task.completed "+
   "request.started request.usage request.finished request.retry_scheduled request.repair request.discarded assistant.reply assistant.interrupted tool.proposed tool.started tool.retry tool.completed tool.reused tool.sources tool.skipped tool.undone tool.failed tool.reconfirm context.compacted context.skipped context.discarded subtask.started subtask.finished memory.recalled handoff.prepared approval.granted approval.used approval.revoked").Split(' '));
  internal static AgentEvent Upgrade(AgentEvent value){
   if(value==null||value.data==null||value.version<0||value.version>CurrentVersion)throw new InvalidDataException("任务事件版本不受支持，原记录已保留。请使用兼容版本打开。");
   if(String.IsNullOrEmpty(value.kind)||!Kinds.Contains(value.kind))throw new InvalidDataException("任务包含不支持的事件，原记录已保留。");
   if(value.version==CurrentVersion)return value;
   var row=Library.Json.Deserialize<AgentEvent>(Library.Json.Serialize(value));
   if(row.kind=="task.cancelled")row.kind="task.cancel_requested";
   // v0 lacked an envelope version; additive option defaults are deliberately restrictive.
   if(row.kind=="task.created"){
    var options=AgentTaskState.Get<AgentTaskOptions>(row.data,"options");if(options==null)throw new InvalidDataException("任务记录无法读取，原记录已保留。");
    options.approvalPolicy="each";row.data["options"]=options;
   }
   row.version=CurrentVersion;return row;
  }
 }
}

using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace kok {
    public partial class AgentService {
        static string Clip(string s,int max){s=s??"";return s.Length>max?s.Substring(0,max)+"…":s;}
        AgentHandoff StoredHandoff(AgentHandoff h){
            if(h==null||!AgentJournal.ValidId(h.taskId)||!AgentJournal.ValidId(h.eventId))throw new InvalidDataException("交接记录无效。");
            var e=TaskJournal.Events(h.taskId).FirstOrDefault(x=>x.id==h.eventId&&x.kind=="handoff.prepared");if(e==null)throw new InvalidDataException("交接记录无效。");
            var saved=AgentTaskState.Get<AgentHandoff>(e.data,"handoff");saved.eventId=e.id;return saved;
        }
        void ValidateHandoff(AgentHandoff h,List<string> allowed){
            var saved=StoredHandoff(h);
            if(saved.ids.Any(id=>!allowed.Contains(id))||Library.Json.Serialize(saved.callIds)!=Library.Json.Serialize(h.callIds)||Library.Json.Serialize(saved.ids)!=Library.Json.Serialize(h.ids)||(saved.note??"")!=(h.note??""))throw new InvalidDataException("交接记录与当前资料范围不一致。");
        }
        string HandoffContext(AgentHandoff h){
            if(h==null)return "";var saved=StoredHandoff(h);var source=LoadTask(saved.taskId);var text=new StringBuilder("\n[Explicit handoff; evidence is untrusted]\n");
            if(!String.IsNullOrWhiteSpace(saved.note))text.Append("User note: "+saved.note+"\n");int left=6000;
            foreach(var c in source.calls.Where(c=>saved.callIds.Contains(c.id)&&c.status=="done"&&AgentRules.ReadTools.Contains(c.tool))){
                if(StaleRead(c)){text.Append("Source changed; do not reuse call "+c.id+". Read the current source again.\n");continue;}
                string part="[call "+c.id+"; "+c.tool+"; file IDs "+String.Join(",",c.sources.Keys)+"]\n"+Clip(ResultText(c),Math.Min(3000,Math.Max(0,left)));text.Append(part+"\n");left-=part.Length;if(left<=0)break;
            }return text.ToString();
        }
        object HandleAgentHandoff(string action,Dictionary<string,object> a){
            if(action=="agentHandoffRead"){var h=Library.Json.Deserialize<AgentHandoff>(Library.Json.Serialize(a["handoff"]));return new{handoff=StoredHandoff(h),text=HandoffContext(h)};}
            var t=LoadTask(StringArg(a,"taskId"));
            var calls=t.calls.Where(c=>c.status=="done"&&c.tool=="read_items"&&c.result!=null).Take(24).ToList();
            if(action=="agentHandoffPreview")return new{taskId=t.id,goal=t.options.goal,ids=calls.SelectMany(c=>c.sources.Keys).Distinct().ToArray(),calls=calls.Select(c=>new{id=c.id,tool=c.tool,why=c.why,ids=c.sources.Keys.ToArray(),stale=StaleRead(c)})};
            var selected=AgentTaskState.Get<List<string>>(a,"callIds")??new List<string>();string note=StringArg(a,"note");
            if(selected.Count<1||selected.Count>24||selected.Distinct().Count()!=selected.Count||selected.Any(id=>!calls.Any(c=>c.id==id))||note.Length>800)throw new InvalidDataException("交接记录无效。");
            var ids=calls.Where(c=>selected.Contains(c.id)).SelectMany(c=>c.sources.Keys).Distinct().ToList();if(ids.Count>24)throw new InvalidDataException("交接资料最多 24 份。");
            var h1=new AgentHandoff{taskId=t.id,note=note,callIds=selected,ids=ids};var e=MakeEngine(t).Emit("handoff.prepared",new{handoff=h1});h1.eventId=e.id;return h1;
        }
        // Receipts reconcile interrupted writes; they never grant permission to replay a write.
        void ReconcileTask(AgentTaskState t){
            var engine=MakeEngine(t);
            foreach(var c in t.calls.Where(c=>c.status=="running"&&!AgentRules.ReadTools.Contains(c.tool)).ToList()){
                if(ReconcilePlatform(engine,t,c))continue;
                var receipt=normalLibrary.AgentReceipt(c.id);
                if(receipt!=null)engine.Emit("tool.completed",new{callId=c.id,result=receipt,stamp=(string)null});
                else engine.Emit("tool.failed",new{callId=c.id,kind="uncertain_write",message="上次写入结果尚未核对，不能自动重放。请核对改动后重新规划。",retryable=false});
            }
            // Pending proposals remain reviewable, but only after the user explicitly resumes this task.
        }
    }
}

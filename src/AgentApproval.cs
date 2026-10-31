using System;
using System.Linq;
using System.Collections.Generic;

namespace kok {
 public class AgentApprovalGrant {public string id,tool;public List<string> ids=new List<string>();public int steering,remaining=4;}
 internal static class AgentApproval {
  internal static bool Eligible(AgentTaskState state,AgentCall call){return state.options.approvalPolicy=="task"&&String.IsNullOrEmpty(state.options.parentTaskId)&&new[]{"set_collection","add_tags","write_note"}.Contains(call.tool)&&Targets(call).Count>0&&Targets(call).All(id=>state.options.allowed.Contains(id));}
  internal static List<string> Targets(AgentCall call){var ids=new List<string>();if(call.args.ContainsKey("id"))ids.Add(Convert.ToString(call.args["id"]));if(call.args.ContainsKey("ids"))ids.AddRange(AgentTaskState.Get<List<string>>(call.args,"ids")??new List<string>());return ids.Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToList();}
  internal static AgentApprovalGrant Find(AgentTaskState state,AgentCall call){if(!Eligible(state,call))return null;return state.grants.LastOrDefault(g=>g.tool==call.tool&&g.steering==state.steerVersion&&g.remaining>0&&Targets(call).All(g.ids.Contains));}
 }
 public sealed partial class AgentTaskEngine {
  readonly Func<bool> automaticAllowed;
  bool CanAutoApprove(AgentCall call){return automaticAllowed!=null&&automaticAllowed()&&AgentApproval.Find(State,call)!=null;}
  void RevokeGrants(string reason){if(State.grants.Count>0)Emit("approval.revoked",new{reason});}
 }
 public partial class AgentService {
  string foregroundTask="";DateTime foregroundUntil=DateTime.MinValue;
  bool Foreground(AgentTaskState task){return String.IsNullOrEmpty(task.options.parentTaskId)&&foregroundTask==task.id&&DateTime.UtcNow<foregroundUntil;}
  object TaskPresence(string id,bool foreground){
   if(taskEngine==null||taskEngine.State.id!=id||!taskEngine.IsRunning)return false;
   foregroundTask=foreground?id:"";foregroundUntil=foreground?DateTime.UtcNow.AddSeconds(20):DateTime.MinValue;
   if(!foreground&&taskEngine.State.grants.Count>0)taskEngine.Emit("approval.revoked",new{reason="background"});
   return true;
  }
 }
}

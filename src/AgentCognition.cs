using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace kok {
 public class AgentPlanStep {public string id,title,status="pending",kind="tool";public List<string> evidence=new List<string>();}
 public class AgentCheck {public string claim;public List<string> evidence=new List<string>();}
 public class AgentVerification {public string status,summary,level;public int steering;public List<AgentCheck> checks=new List<AgentCheck>();}
 internal static class AgentCognition {
  internal static AgentToolSpec[] Tools(){
   var evidence=AgentRules.Arr("Completed kok call IDs, not provider call IDs",12,AgentRules.Str("kok call ID",32));
   var step=CapabilitySchema.Obj(CapabilitySchema.Fields("id",AgentRules.Str("Stable short step ID",32),"title",AgentRules.Str("Concrete step",160),"kind",AgentRules.Str("tool (default) requires execution evidence; reasoning is model-assessed",20,new[]{"tool","reasoning"}),"status",AgentRules.Str("Step state",20,new[]{"pending","in_progress","done","blocked"}),"evidence",evidence),new[]{"id","title","status","evidence"});
   var check=CapabilitySchema.Obj(CapabilitySchema.Fields("claim",AgentRules.Str("What was checked and what remains uncertain",400),"evidence",evidence),new[]{"claim","evidence"});
   return new[]{
    Control("update_plan","update_plan{steps[{id,title,kind?:tool|reasoning,status,evidence[]}]}","Maintain a concise plan. Completed tool steps require successful, current kok call IDs. Reasoning-only steps may have no evidence and remain model-assessed. Never label an actual tool action as reasoning. This edits task state, not library data.",AgentRules.Props("steps",AgentRules.Arr("1-8 task steps",8,step)),new[]{"steps"}),
    Control("finish_task","finish_task{status:completed|partial|needs_input,summary,checks[{claim,evidence[]}]}","Review the ORIGINAL goal and all actual tool outcomes. Complete only when the goal is met. Cite successful kok call IDs; skipped, failed, stale or undone actions cannot prove success. State limitations honestly. This never approves a write.",AgentRules.Props("status",AgentRules.Str("Outcome",20,new[]{"completed","partial","needs_input"}),"summary",AgentRules.Str("Final outcome and limitations",1200),"checks",AgentRules.Arr("0-8 verification checks",8,check)),new[]{"status","summary","checks"})
   };
  }
  static AgentToolSpec Control(string name,string signature,string description,Dictionary<string,object> properties,string[] required){return new AgentToolSpec{Name=name,Title=name=="update_plan"?"更新任务计划":"核对任务结果",Requires=Need.NormalLibrary,Signature=signature,Description=description,Properties=properties,Required=required,Validate=(a,exists)=>{try{CapabilitySchema.Check(CapabilitySchema.Obj(properties,required),a.args);}catch{throw new FormatException("Invalid task-control arguments");}}};}
  internal static bool IsControl(string tool){return tool=="update_plan"||tool=="finish_task";}
  internal static Dictionary<string,object> Run(AgentTaskEngine engine,AgentCall call,Func<AgentCall,bool> current){
   var s=engine.State;
   if(call.steering!=s.steerVersion)throw new AgentFailure("superseded","任务要求已更新，请重新规划和核对。");
   Action<IEnumerable<string>> evidence=ids=>{if(ids.Any(id=>!AgentJournal.ValidId(id)||!s.calls.Any(c=>c.id==id&&!IsControl(c.tool)&&c.status=="done"&&current(c))))throw new AgentFailure("verification","核对依据不存在、已失效或尚未执行，请重新读取或说明限制。");};
   if(call.tool=="update_plan"){
    var steps=AgentTaskState.Get<List<AgentPlanStep>>(call.args,"steps");
    if(steps==null||steps.Count<1||steps.Count>8||steps.Any(p=>p==null||String.IsNullOrWhiteSpace(p.id)||p.id.Length>32||String.IsNullOrWhiteSpace(p.title)||p.title.Length>160||!new[]{"tool","reasoning"}.Contains(p.kind)||!new[]{"pending","in_progress","done","blocked"}.Contains(p.status)||p.evidence==null||p.evidence.Count>12)||steps.Select(p=>p.id).Distinct().Count()!=steps.Count||steps.Count(p=>p.status=="in_progress")>1)throw new AgentFailure("validation","任务计划格式无效。");
    foreach(var step in steps){evidence(step.evidence);if(step.kind=="tool"&&step.status=="done"&&step.evidence.Count==0)throw new AgentFailure("verification","完成的计划步骤需要实际执行依据。");}
    engine.Emit("task.plan",new{steps=steps,steering=s.steerVersion});return new Dictionary<string,object>{{"text","Task plan saved. Plan completion alone does not finish the task."}};
   }
   var checks=AgentTaskState.Get<List<AgentCheck>>(call.args,"checks");string status=Convert.ToString(call.args["status"]),summary=Convert.ToString(call.args["summary"]);
   if(!new[]{"completed","partial","needs_input"}.Contains(status)||checks==null||checks.Count>8||String.IsNullOrWhiteSpace(summary)||summary.Length>1200||checks.Any(c=>c==null||String.IsNullOrWhiteSpace(c.claim)||c.claim.Length>400||c.evidence==null||c.evidence.Count>12))throw new AgentFailure("validation","任务核对格式无效。");
   foreach(var check in checks)evidence(check.evidence);
   var operations=s.calls.Where(c=>!IsControl(c.tool)).ToList();
   if(operations.Any(c=>c.status=="queued"||c.status=="running"||c.status=="pending"))throw new AgentFailure("verification","还有未执行或待确认的操作，不能结束任务。");
   if(status=="completed"){
    if(operations.Any(c=>c.status!="done"))throw new AgentFailure("verification","有跳过、失败或撤销的步骤，请补救或明确标为部分完成。");
    if(operations.Count>0&&!checks.Any(c=>c.evidence.Count>0))throw new AgentFailure("verification","任务完成需要引用实际工具结果。");
    if(operations.Count>1&&s.plan.Count==0)throw new AgentFailure("verification","多步骤任务需要先维护计划。");
    if(s.plan.Any(p=>p.status!="done")||!s.PlanCurrent)throw new AgentFailure("verification","计划尚未完成或要求已更新，请先核对计划。");
    foreach(var step in s.plan)evidence(step.evidence);
   }
   var result=new AgentVerification{status=status,summary=summary,checks=checks,steering=s.steerVersion,level=checks.Any(c=>c.evidence.Count>0)?"evidence_checked":"model_only"};
   engine.Emit("task.verified",new{verification=result});return new Dictionary<string,object>{{"text","Task result recorded as "+status+". Evidence references were checked locally; semantic claims remain the model's assessment."}};
  }
  internal static string Instructions(AgentTaskState task){return task.options.kernelVersion<3?"":"\nTASK CONTROL: For multi-step work, call update_plan, then keep it current. Completed tool steps require successful kok_call_id evidence. Pure reasoning steps may use kind=reasoning and empty evidence; they remain model-assessed and never replace actual tool outcomes. Before ending call finish_task to review the original goal, actual tool results, approvals, skips and limitations. A plain reply or done=true does NOT complete a task. Use partial/needs_input if blocked or rejected. "+
   "Efficiency: combine an initial plan and independent reads in one response. After results or approvals, send the final update_plan (all completed steps marked done) and finish_task TOGETHER in one response when ready; they execute in that order. Do not waste a separate request only to update the plan or announce the final answer. Never finish while a proposed write is awaiting approval. "+
   "Rejected/failed/undone calls are NOT successful evidence, even for partial/needs_input. Describe them in summary, mark affected steps blocked with evidence=[], and use only successful IDs or checks=[]; never repeat a rejected write without new user permission. "+
   (task.options.allowSandbox?"Use run_code for arithmetic or structured data transformations; use the approved exact result rather than mental calculations. ":"")+
   "Model requests remaining at turn start (including this request, repairs and retries): "+Math.Max(0,task.options.requestLimit-task.requests)+". When only one remains, call finish_task with an honest reviewed outcome in this response; use partial/needs_input if more work would be needed. Never claim completion just to fit the budget. Current plan and outcome: "+Library.Json.Serialize(new{plan=task.plan,planCurrent=task.PlanCurrent,planInvalidation=task.planInvalidation,reviewInvalidation=task.reviewInvalidation,verification=task.verification,steering=task.steerVersion});}
 }
}

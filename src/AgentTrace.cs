using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

namespace kok {
 public class AgentRequestMetric {public string id,protocol,outcome,errorKind,purpose,childTaskId;public int number,attempt,httpStatus,inputEstimate,inputBound,inputTokens,outputTokens,cachedTokens;public double cost;public long durationMs,firstTextMs=-1;public bool estimated;}
 public static class AgentTrace {
  static long Between(AgentEvent start,AgentEvent end){
   DateTime a,b;if(start==null||end==null||!DateTime.TryParse(start.created,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out a)||!DateTime.TryParse(end.created,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out b))return 0;
   return Math.Max(0,(long)(b-a).TotalMilliseconds);
  }
  public static object Performance(AgentTaskState task){
   long tools=0,approval=0;var requests=Requests(task);
   foreach(var call in task.calls){
    var proposed=task.events.FirstOrDefault(e=>e.kind=="tool.proposed"&&AgentTaskState.Get<AgentCall>(e.data,"call").id==call.id);
    var started=task.events.LastOrDefault(e=>e.kind=="tool.started"&&AgentTaskState.Get<string>(e.data,"callId")==call.id);
    var ended=task.events.LastOrDefault(e=>(e.kind=="tool.completed"||e.kind=="tool.failed"||e.kind=="tool.reused"||e.kind=="tool.skipped")&&AgentTaskState.Get<string>(e.data,"callId")==call.id);
    tools+=Between(started,ended);
    if(proposed!=null&&AgentTaskState.Get<AgentCall>(proposed.data,"call").status=="pending")approval+=Between(proposed,started??ended);
   }
   var firstArtifact=task.events.FirstOrDefault(e=>e.kind=="tool.completed"&&task.calls.Any(c=>AgentTaskState.IsArtifactTool(c.tool)&&c.id==AgentTaskState.Get<string>(e.data,"callId")&&c.result!=null));
   var notes=task.events.Where(e=>e.kind=="task.note");
   return new{version=1,wallMs=Between(task.events.FirstOrDefault(),task.events.LastOrDefault()),modelMs=requests.Sum(r=>r.durationMs),toolMs=tools,approvalWaitMs=approval,
    firstArtifactMs=firstArtifact==null?(long?)null:Between(task.events.FirstOrDefault(),firstArtifact),
    nonCachedInputTokens=requests.Sum(r=>Math.Max(0,r.inputTokens-r.cachedTokens)),cachedInputTokens=requests.Sum(r=>r.cachedTokens),estimated=requests.Any(r=>r.estimated),
    projectionSavedCharacters=notes.Where(e=>AgentTaskState.Get<string>(e.data,"category")=="context_projection").Sum(e=>(long)AgentTaskState.Get<int>(e.data,"savedCharacters")),
    progressWarnings=notes.Count(e=>AgentTaskState.Get<string>(e.data,"category")=="progress"&&AgentTaskState.Get<string>(e.data,"action")=="warn"),
    progressStops=notes.Count(e=>AgentTaskState.Get<string>(e.data,"category")=="progress"&&AgentTaskState.Get<string>(e.data,"action")=="stop"),
    timingScope="Cumulative observed durations may overlap. Wall time includes approval waits and pauses; firstArtifact is the saved-file receipt, not a quality certification."};
  }
  public static List<AgentRequestMetric> Requests(AgentTaskState task){var result=new List<AgentRequestMetric>();foreach(var start in task.events.Where(e=>e.kind=="request.started")){
   string id=start.data.ContainsKey("requestId")?AgentTaskState.Get<string>(start.data,"requestId"):start.id;int end=task.events.Where(e=>e.kind=="request.started"&&e.sequence>start.sequence).Select(e=>e.sequence).DefaultIfEmpty(Int32.MaxValue).Min();var events=task.events.Where(e=>e.sequence>start.sequence&&e.sequence<end).ToList();var usage=events.LastOrDefault(e=>e.kind=="request.usage");var done=events.LastOrDefault(e=>e.kind=="request.finished");
   result.Add(new AgentRequestMetric{id=id,number=result.Count+1,attempt=start.data.ContainsKey("attempt")?AgentTaskState.Get<int>(start.data,"attempt"):1,protocol=AgentTaskState.Get<string>(start.data,"protocol"),purpose=AgentTaskState.Get<string>(start.data,"purpose")??"model",childTaskId=AgentTaskState.Get<string>(start.data,"childTaskId"),outcome=done==null?"interrupted":AgentTaskState.Get<string>(done.data,"outcome"),errorKind=done==null?"":AgentTaskState.Get<string>(done.data,"errorKind"),httpStatus=done==null?0:AgentTaskState.Get<int>(done.data,"httpStatus"),durationMs=done==null?0:AgentTaskState.Get<long>(done.data,"durationMs"),firstTextMs=done==null||!done.data.ContainsKey("firstTextMs")?-1:AgentTaskState.Get<long>(done.data,"firstTextMs"),inputEstimate=AgentTaskState.Get<int>(start.data,"inputEstimate"),inputBound=AgentTaskState.Get<int>(start.data,"inputBound"),inputTokens=usage==null?0:AgentTaskState.Get<int>(usage.data,"inputTokens"),outputTokens=usage==null?0:AgentTaskState.Get<int>(usage.data,"outputTokens"),cachedTokens=usage==null?0:AgentTaskState.Get<int>(usage.data,"cachedTokens"),cost=usage==null?0:AgentTaskState.Get<double>(usage.data,"cost"),estimated=usage==null||AgentTaskState.Get<bool>(usage.data,"estimated")});
  }return result;}
  // Diagnostic exports intentionally omit goals, prompts, filenames, arguments,
  // source/tool text, provider bodies, credentials, plan titles and user claims.
  static object Event(AgentTaskState task,AgentEvent e){
   string callId=e.data.ContainsKey("callId")?AgentTaskState.Get<string>(e.data,"callId"):e.kind=="tool.proposed"?AgentTaskState.Get<AgentCall>(e.data,"call").id:null;var call=task.calls.FirstOrDefault(c=>c.id==callId);
   return new{sequence=e.sequence,version=e.version,id=e.id,kind=e.kind,created=e.created,callId=callId,tool=call==null?null:call.tool,resultAvailable=call!=null&&call.resultEventId==e.id&&call.result!=null,requestId=e.data.ContainsKey("requestId")?AgentTaskState.Get<string>(e.data,"requestId"):null,retryDelayMs=e.data.ContainsKey("delayMs")?AgentTaskState.Get<int>(e.data,"delayMs"):0};
  }
  public static object Export(AgentTaskState task){return new{format="kok.agent.trace",version=3,eventFormatVersion=AgentEventContract.CurrentVersion,tokenEstimateMethod=AgentTokenBudget.Method,taskId=task.id,kernelVersion=task.options.kernelVersion,status=task.status,redacted=true,pricingConfigured=task.options.inputRate>0&&task.options.outputRate>0,planCurrent=task.PlanCurrent,reviewInvalidated=task.reviewInvalidation!="",plan=task.plan.Select((p,n)=>new{step=n+1,status=p.status,evidenceCount=p.evidence.Count}).ToArray(),verification=task.verification==null?null:new{status=task.verification.status,level=task.verification.level,checks=task.verification.checks.Count},performance=Performance(task),requests=Requests(task),events=task.events.Select(e=>Event(task,e)).ToArray()};}
 }
}

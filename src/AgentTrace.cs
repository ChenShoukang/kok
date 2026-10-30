using System;
using System.Linq;
using System.Collections.Generic;

namespace kok {
 public class AgentRequestMetric {public string id,protocol,outcome,errorKind;public int number,attempt,httpStatus,inputTokens,outputTokens,cachedTokens;public double cost;public long durationMs,firstTextMs=-1;public bool estimated;}
 public static class AgentTrace {
  public static List<AgentRequestMetric> Requests(AgentTaskState task){var result=new List<AgentRequestMetric>();foreach(var start in task.events.Where(e=>e.kind=="request.started")){
   string id=start.data.ContainsKey("requestId")?AgentTaskState.Get<string>(start.data,"requestId"):start.id;int end=task.events.Where(e=>e.kind=="request.started"&&e.sequence>start.sequence).Select(e=>e.sequence).DefaultIfEmpty(Int32.MaxValue).Min();var events=task.events.Where(e=>e.sequence>start.sequence&&e.sequence<end).ToList();var usage=events.LastOrDefault(e=>e.kind=="request.usage");var done=events.LastOrDefault(e=>e.kind=="request.finished");
   result.Add(new AgentRequestMetric{id=id,number=result.Count+1,attempt=start.data.ContainsKey("attempt")?AgentTaskState.Get<int>(start.data,"attempt"):1,protocol=AgentTaskState.Get<string>(start.data,"protocol"),outcome=done==null?"interrupted":AgentTaskState.Get<string>(done.data,"outcome"),errorKind=done==null?"":AgentTaskState.Get<string>(done.data,"errorKind"),httpStatus=done==null?0:AgentTaskState.Get<int>(done.data,"httpStatus"),durationMs=done==null?0:AgentTaskState.Get<long>(done.data,"durationMs"),firstTextMs=done==null||!done.data.ContainsKey("firstTextMs")?-1:AgentTaskState.Get<long>(done.data,"firstTextMs"),inputTokens=usage==null?0:AgentTaskState.Get<int>(usage.data,"inputTokens"),outputTokens=usage==null?0:AgentTaskState.Get<int>(usage.data,"outputTokens"),cachedTokens=usage==null?0:AgentTaskState.Get<int>(usage.data,"cachedTokens"),cost=usage==null?0:AgentTaskState.Get<double>(usage.data,"cost"),estimated=usage==null||AgentTaskState.Get<bool>(usage.data,"estimated")});
  }return result;}
  // Diagnostic exports intentionally omit goals, prompts, filenames, arguments,
  // source/tool text, provider bodies, credentials, plan titles and user claims.
  static object Event(AgentTaskState task,AgentEvent e){
   string callId=e.data.ContainsKey("callId")?AgentTaskState.Get<string>(e.data,"callId"):e.kind=="tool.proposed"?AgentTaskState.Get<AgentCall>(e.data,"call").id:null;var call=task.calls.FirstOrDefault(c=>c.id==callId);
   return new{sequence=e.sequence,id=e.id,kind=e.kind,created=e.created,callId=callId,tool=call==null?null:call.tool,resultAvailable=call!=null&&call.resultEventId==e.id&&call.result!=null,requestId=e.data.ContainsKey("requestId")?AgentTaskState.Get<string>(e.data,"requestId"):null,retryDelayMs=e.data.ContainsKey("delayMs")?AgentTaskState.Get<int>(e.data,"delayMs"):0};
  }
  public static object Export(AgentTaskState task){return new{format="kok.agent.trace",version=2,taskId=task.id,kernelVersion=task.options.kernelVersion,status=task.status,redacted=true,pricingConfigured=task.options.inputRate>0&&task.options.outputRate>0,planCurrent=task.PlanCurrent,reviewInvalidated=task.reviewInvalidation!="",plan=task.plan.Select((p,n)=>new{step=n+1,status=p.status,evidenceCount=p.evidence.Count}).ToArray(),verification=task.verification==null?null:new{status=task.verification.status,level=task.verification.level,checks=task.verification.checks.Count},requests=Requests(task),events=task.events.Select(e=>Event(task,e)).ToArray()};}
 }
}

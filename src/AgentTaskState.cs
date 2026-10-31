using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace kok {
    public class AgentTaskOptions {
        public string sessionId,goal,intent="assist",protocol="auto",workflow="",instruction="",workflowGuidance="",approvalPolicy="each";
        public AgentScope scope=new AgentScope();public List<string> allowed=new List<string>();public List<ChatTurn> history=new List<ChatTurn>();
        public bool semanticCompression=true,useMemory,allowDelegation,allowSandbox;public int contextTokens=32000;public List<string> allowedTools;public string parentTaskId="",parentCallId="";public int parentSteering;
        public int budget=14000,requestLimit=6,kernelVersion;public double costLimit,inputRate,outputRate;public AgentHandoff handoff;
    }
    public class AgentHandoff {public string taskId,eventId,note;public List<string> callIds=new List<string>(),ids=new List<string>();}
    public class AgentCall {
        public string childTaskId;public string id,providerId,turnId,tool,why,status="queued",error="",errorKind="",resultEventId,stamp,externalStamp;
        public Dictionary<string,object> args=new Dictionary<string,object>(),result;
        public List<AgentChange> changes;public string beforeText,afterText;
        public int sequence,attempt,steering;public bool retryable,hostGenerated;public Dictionary<string,string> sources=new Dictionary<string,string>();
    }
    public class AgentDialogue {public string id,role,text,turnId,requestId;public int sequence;public bool meta,interrupted;public List<string> sourceIds=new List<string>();}
    public class AgentTaskState {
        public string id,status="created",error="",errorKind="",phase="",protocol="json";public bool retryable,needsModel=true,modelDone,estimated,cancelRequested;
        public AgentTaskOptions options;public int sequence,requests,round,inputTokens,outputTokens,cachedTokens,repairs,steerVersion,replySteering;
        public double cost;public List<AgentCall> calls=new List<AgentCall>();public List<AgentDialogue> dialogue=new List<AgentDialogue>();public List<string> adjustments=new List<string>();
        public List<AgentEvent> events=new List<AgentEvent>();
        public AgentContextCheckpoint checkpoint;
        public List<AgentApprovalGrant> grants=new List<AgentApprovalGrant>();
        public List<AgentPlanStep> plan=new List<AgentPlanStep>();public AgentVerification verification;public int planSteering;public string planInvalidation="",reviewInvalidation="";
        public bool PlanCurrent {get{return plan.Count==0||planSteering==steerVersion&&planInvalidation=="";}}
        void InvalidateReview(string reason){if(verification!=null){verification=null;reviewInvalidation=reason;if(status=="completed"||status=="partial"){status="review_required";phase="核对已失效，请重新检查结果。";}}if(plan.Count>0)planInvalidation=reason;}
        void AddInstruction(AgentEvent e){InvalidateReview("instructions");grants.Clear();adjustments.Add(Get<string>(e.data,"text"));steerVersion++;dialogue.Add(new AgentDialogue{id=e.id,role="user",text=Get<string>(e.data,"text"),sequence=e.sequence});}
        public static T Get<T>(Dictionary<string,object> data,string key){return data.ContainsKey(key)?Library.Json.Deserialize<T>(Library.Json.Serialize(data[key])):default(T);}
        public static AgentTaskState Replay(string id,List<AgentEvent> events){var s=new AgentTaskState{id=id};foreach(var e in events)s.Apply(e);return s;}
        public void Apply(AgentEvent e){
            e=AgentEventContract.Upgrade(e);events.Add(e);sequence=e.sequence;var d=e.data;
            switch(e.kind){
                case "task.created":options=Get<AgentTaskOptions>(d,"options");protocol=Get<string>(d,"protocol");dialogue.Add(new AgentDialogue{id=e.id,role="user",text=options.goal,sequence=e.sequence});break;
                case "task.running":status="running";error="";errorKind="";retryable=false;break;
                case "task.phase":phase=Get<string>(d,"phase");break;
                case "task.plan":plan=Get<List<AgentPlanStep>>(d,"steps");planSteering=Get<int>(d,"steering");planInvalidation="";verification=null;break;
                case "task.verified":verification=Get<AgentVerification>(d,"verification");reviewInvalidation="";break;
                case "task.review_invalidated":InvalidateReview(Get<string>(d,"reason"));break;
                case "context.compacted":checkpoint=Get<AgentContextCheckpoint>(d,"checkpoint");break;
                case "subtask.started":calls.First(c=>c.id==Get<string>(d,"callId")).childTaskId=Get<string>(d,"taskId");break;
                case "tool.reconfirm":calls.First(c=>c.id==Get<string>(d,"callId")).status="pending";break;
                case "task.protocol":protocol=Get<string>(d,"protocol");break;
                case "request.repair":repairs++;break;
                case "task.adjusted":AddInstruction(e);break;
                case "task.continued":AddInstruction(e);status="paused";phase="等待继续";break;
                case "request.started":requests++;cost+=Get<double>(d,"reserve");break;
                case "request.usage":cost+=Get<double>(d,"cost")-Get<double>(d,"reserve");inputTokens+=Get<int>(d,"inputTokens");outputTokens+=Get<int>(d,"outputTokens");cachedTokens+=d.ContainsKey("cachedTokens")?Get<int>(d,"cachedTokens"):0;estimated|=Get<bool>(d,"estimated");break;
                case "assistant.interrupted":dialogue.Add(new AgentDialogue{id=e.id,role="agent",text=Get<string>(d,"text"),requestId=Get<string>(d,"requestId"),sequence=e.sequence,interrupted=true});break;
                case "approval.granted":grants.Add(Get<AgentApprovalGrant>(d,"grant"));break;
                case "approval.used":{var g=grants.FirstOrDefault(x=>x.id==Get<string>(d,"grantId"));if(g!=null)g.remaining=Math.Max(0,g.remaining-1);break;}
                case "approval.revoked":grants.Clear();break;
                case "assistant.reply":round++;replySteering=Get<int>(d,"steering");modelDone=Get<bool>(d,"done");needsModel=!modelDone;dialogue.Add(new AgentDialogue{id=e.id,role="agent",text=Get<string>(d,"text"),turnId=Get<string>(d,"turnId"),sequence=e.sequence,sourceIds=Get<List<string>>(d,"sources")??new List<string>()});break;
                case "task.note":dialogue.Add(new AgentDialogue{id=e.id,role="agent",meta=true,text=Get<string>(d,"text"),sequence=e.sequence});break;
                case "tool.proposed":var c=Get<AgentCall>(d,"call");c.sequence=e.sequence;calls.Add(c);if(!AgentCognition.IsControl(c.tool))verification=null;break;
                case "tool.started":case "tool.retry":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status=e.kind=="tool.retry"?"queued":"running";c1.error="";c1.attempt++;break;}
                case "tool.completed":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status="done";c1.result=Get<Dictionary<string,object>>(d,"result");c1.resultEventId=e.id;c1.stamp=Get<string>(d,"stamp");break;}
                case "tool.reused":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));var previous=calls.First(c=>c.resultEventId==Get<string>(d,"resultRef")&&c.result!=null);c1.status="done";c1.result=previous.result;c1.resultEventId=previous.resultEventId;break;}
                case "tool.sources":calls.First(c=>c.id==Get<string>(d,"callId")).sources=Get<Dictionary<string,string>>(d,"sources");break;
                case "tool.skipped":case "tool.undone":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status=e.kind=="tool.skipped"?"skipped":"undone";grants.Clear();if(e.kind=="tool.skipped")c1.error=d.ContainsKey("reason")?Get<string>(d,"reason"):"user";else InvalidateReview("undo");break;}
                case "tool.failed":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status="failed";c1.error=Get<string>(d,"message");c1.errorKind=Get<string>(d,"kind");c1.retryable=Get<bool>(d,"retryable");c1.result=Get<Dictionary<string,object>>(d,"result");c1.resultEventId=c1.result==null?null:e.id;break;}
                case "task.waiting":status="waiting";phase="等待确认";break;
                case "task.cancel_requested":case "task.cancelled":cancelRequested=true;status="cancelled";phase="任务已取消";break;
                case "task.paused":case "task.stopped":status=cancelRequested?"cancelled":e.kind=="task.paused"?"paused":"stopped";phase=cancelRequested?"任务已取消":"任务已暂停";break;
                case "task.failed":status="failed";error=Get<string>(d,"message");errorKind=Get<string>(d,"kind");retryable=Get<bool>(d,"retryable");break;
                case "task.completed":status=Get<bool>(d,"limited")?"limited":d.ContainsKey("outcome")?Get<string>(d,"outcome"):"completed";phase=status=="completed"?"已完成":status=="partial"?"部分完成":status=="needs_input"?"需要补充信息":"已达到本次请求上限。";break;
            }
        }
        public bool Terminal {get{return cancelRequested||new[]{"completed","partial","needs_input","limited","cancelled","review_required"}.Contains(status);}}
    }
    public class AgentFailure:Exception {
        public readonly string kind;public readonly bool retryable;public readonly int httpStatus,retryAfterMs;
        public AgentFailure(string kind,string text,bool retryable=false,int httpStatus=0,int retryAfterMs=0):base(text){this.kind=kind;this.retryable=retryable;this.httpStatus=httpStatus;this.retryAfterMs=retryAfterMs;}
        public static AgentFailure Classify(Exception e){var f=e as AgentFailure;if(f!=null)return f;if(e is FileNotFoundException)return new AgentFailure("missing_source",e.Message);if(e is AgentConflictException)return new AgentFailure("conflict",e.Message);if(e is System.Net.Http.HttpRequestException)return new AgentFailure("network","无法连接翻译服务，请检查接口地址与网络。",true);if(e is TimeoutException)return new AgentFailure("timeout","翻译请求超时，请稍后重试或更换模型。",true);if(e is UnauthorizedAccessException)return new AgentFailure("permission","当前操作没有访问权限。");return new AgentFailure("validation",e is InvalidDataException||e is InvalidOperationException?e.Message:"Agent 执行失败，请查看任务追踪后重试。");}
    }
    public sealed class AgentToolFailure:AgentFailure {public readonly Dictionary<string,object> result;public AgentToolFailure(string kind,string message,Dictionary<string,object> result):base(kind,message){this.result=result;}}
    public class AgentModelTurn {public AgentReply reply;public List<string> providerIds=new List<string>();public string protocol;}
}

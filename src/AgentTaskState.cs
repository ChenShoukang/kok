using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace kok {
    public class AgentTaskOptions {
        // Empty on pre-0.13 journals: they keep their original native execution semantics.
        public string runtime="";
        public string sessionId,goal,intent="assist",protocol="auto",workflow="",instruction="",workflowGuidance="",approvalPolicy="each";
        public AgentScope scope=new AgentScope();public List<string> allowed=new List<string>();public List<ChatTurn> history=new List<ChatTurn>();
        public bool semanticCompression=true,useMemory,allowDelegation,allowSandbox;public bool vision;public string searchEndpoint="";public int contextTokens=32000;public List<string> allowedTools;
        // v0.12.1: true when the host derived allowedTools from the goal/instructions (not a user-picked explicit
        // list). Only an auto profile is re-derived from task.adjusted; an explicit list stays authoritative.
        public bool allowedToolsAuto;
        public string parentTaskId="",parentCallId="",sessionLeafId="",skillsIndex="";public int parentSteering;
        public int budget=14000,requestLimit=6,kernelVersion,workspaceVersion,reserveTokens,keepRecentTokens;public double costLimit,inputRate,outputRate;public AgentHandoff handoff;
        public string projectId="",projectBrief="";
        // Set once by the host (AgentDelivery.PrepareDelivery) from the goal/scope; nullable for replay of older tasks.
        public AgentDeliveryRequirement delivery;
    }
    public class AgentHandoff {public string taskId,eventId,note;public List<string> callIds=new List<string>(),ids=new List<string>();}
    public class AgentCall {
        public string childTaskId;public string id,providerId,turnId,tool,why,status="queued",error="",errorKind="",resultEventId,stamp,externalStamp;
        public Dictionary<string,object> args=new Dictionary<string,object>(),result;
        public List<AgentChange> changes;public string beforeText,afterText,documentSignature,documentBeforeStamp;
        public int sequence,attempt,steering;public bool retryable,hostGenerated;public Dictionary<string,string> sources=new Dictionary<string,string>();
    }
    public class AgentDialogue {public string id,role,text,turnId,requestId;public int sequence;public bool meta,interrupted;public List<string> sourceIds=new List<string>();}
    public class AgentInput {public string id,text,mode,status="queued",created,turnId;}
    public class AgentTaskState {
        public string id,status="created",error="",errorKind="",phase="",protocol="json";public bool retryable,needsModel=true,modelDone,estimated,cancelRequested,lastUsageEstimated,contextRecovered,compactRequested;public string compactInstructions="";
        public AgentTaskOptions options;public int sequence,requests,round,inputTokens,outputTokens,cachedTokens,repairs,steerVersion,replySteering,lastUsageSequence,lastUsageInputTokens,lastUsageOutputTokens;
        public double cost;public List<AgentCall> calls=new List<AgentCall>();public List<AgentDialogue> dialogue=new List<AgentDialogue>();public List<string> adjustments=new List<string>();public List<AgentInput> inputs=new List<AgentInput>();
        public List<AgentInput> QueuedInputs {get{return inputs.Where(i=>i.status=="queued").ToList();}}
        public List<AgentInput> RestoredInputs {get{return inputs.Where(i=>i.status=="restored").ToList();}}
        public List<AgentEvent> events=new List<AgentEvent>();
        public AgentContextCheckpoint checkpoint;
        public Dictionary<string,object> researchCheckpoint;
        public List<AgentApprovalGrant> grants=new List<AgentApprovalGrant>();
        // P0-1 effective task scope: artifacts created by this task (host receipts only, never model claims)
        // join the original user scope for reads, edits, search, verification and delegation.
        public List<string> createdArtifactIds=new List<string>();
        // P1 repaired failures: failedCallId -> successfulCallId that later succeeded with the same tool+args.
        public Dictionary<string,string> resolvedBy=new Dictionary<string,string>();
        public static readonly string[] ArtifactTools={"create_document","create_presentation","copy_presentation"};
        // Writes that edit an existing artifact in place: their receipt id identifies the edited item and the
        // host must read it back after approval, but it never joins createdArtifactIds (it already has a scope).
        public static readonly string[] EditTargetTools={"replace_document_text","edit_presentation"};
        public static bool IsArtifactTool(string tool){return tool!=null&&Array.IndexOf(ArtifactTools,tool)>=0;}
        public static bool IsEditTargetTool(string tool){return tool!=null&&Array.IndexOf(EditTargetTools,tool)>=0;}
        // A host write receipt id is the only accepted artifact identity: control/presentation ids are checked
        // with the journal id format and unknown or model-supplied values are ignored.
        public static string ArtifactId(AgentCall call){
            if(call==null||!IsArtifactTool(call.tool)||call.result==null)return null;
            object value;if(!call.result.TryGetValue("id",out value))return null;
            string id=Convert.ToString(value);return AgentJournal.ValidId(id)?id:null;
        }
        public static string EditTargetId(AgentCall call){
            if(call==null||!IsEditTargetTool(call.tool)||call.result==null)return null;
            object value;if(!call.result.TryGetValue("id",out value))return null;
            string id=Convert.ToString(value);return AgentJournal.ValidId(id)?id:null;
        }
        public List<string> TaskScope {get{
            var ids=new List<string>();var seen=new HashSet<string>(StringComparer.Ordinal);
            if(options!=null&&options.allowed!=null)foreach(var id in options.allowed)if(!String.IsNullOrEmpty(id)&&seen.Add(id))ids.Add(id);
            foreach(var id in createdArtifactIds)if(!String.IsNullOrEmpty(id)&&seen.Add(id))ids.Add(id);
            return ids;
        }}
        public bool InScope(string id){
            if(String.IsNullOrEmpty(id))return false;
            if(createdArtifactIds.Count>0&&createdArtifactIds.Contains(id))return true;
            return options!=null&&options.allowed!=null&&options.allowed.Contains(id);
        }
        public bool FailureResolved(AgentCall failed){
            if(failed==null||String.IsNullOrEmpty(failed.id))return false;
            string resolverId;if(!resolvedBy.TryGetValue(failed.id,out resolverId)||String.IsNullOrEmpty(resolverId))return false;
            var resolver=calls.FirstOrDefault(c=>c.id==resolverId);return resolver!=null&&resolver.status=="done";
        }
        // Read tools whose successful call can repair an earlier failed read of the same source (#14).
        public static readonly string[] ReadTools={"read_items","read_document","read_presentation"};
        static bool ResolutionExcluded(string errorKind){return errorKind=="uncertain_write"||errorKind=="uncertain_external"||errorKind=="conflict";}
        // Deterministic request/format/source errors carry no "the write may have happened" ambiguity, so a
        // corrected retry may repair them; timeout/network/policy/uncertain outcomes may not.
        static bool CorrectionErrorKind(string errorKind){return errorKind=="validation"||errorKind=="format"||errorKind=="source"||errorKind=="missing_source";}
        static void AddTargetId(List<string> ids,HashSet<string> seen,object value){
            string id=Convert.ToString(value);if(AgentJournal.ValidId(id)&&seen.Add(id))ids.Add(id);
        }
        // Target ids from id/ids args, validated as library ids so model-supplied or malformed values never link.
        static List<string> TargetIds(Dictionary<string,object> args){
            var ids=new List<string>();var seen=new HashSet<string>(StringComparer.Ordinal);if(args==null)return ids;
            object value;if(args.TryGetValue("id",out value))AddTargetId(ids,seen,value);
            if(args.TryGetValue("ids",out value)&&value!=null){
                if(value is string)AddTargetId(ids,seen,value);
                else{
                    try{foreach(var item in Library.Json.Deserialize<List<object>>(Library.Json.Serialize(value)))AddTargetId(ids,seen,item);}
                    catch{try{foreach(var item in Library.Json.Deserialize<List<string>>(Library.Json.Serialize(value)))AddTargetId(ids,seen,item);}catch{}}
                }
            }
            return ids;
        }
        // A corrected read (different pagination params such as offset/max_chars) on an overlapping target id
        // repairs the earlier read failure. The same tool or any other exact read tool counts, because both
        // return the same source text; a search/summary call never repairs a failed exact read.
        static bool CorrectedReadFailure(AgentCall failed,AgentCall done){
            if(failed==null||done==null)return false;
            if(Array.IndexOf(ReadTools,failed.tool)<0||Array.IndexOf(ReadTools,done.tool)<0)return false;
            if(!CorrectionErrorKind(failed.errorKind))return false;
            var failedIds=TargetIds(failed.args);if(failedIds.Count==0)return false;
            var doneIds=TargetIds(done.args);return doneIds.Any(id=>failedIds.Contains(id));
        }
        // Called from replay for every successful tool call: record repair relationships for earlier failures
        // with the same tool and identical JSON args, plus corrected read retries on the same target (#14),
        // then remember host-created artifacts.
        void ApplyCompletion(AgentCall done){
            string artifactId=ArtifactId(done);if(artifactId!=null&&!createdArtifactIds.Contains(artifactId))createdArtifactIds.Add(artifactId);
            string args=Library.Json.Serialize(done.args??new Dictionary<string,object>());
            foreach(var failed in calls){
                if(failed==done||failed.status!="failed"||ResolutionExcluded(failed.errorKind))continue;
                if(failed.tool==done.tool&&Library.Json.Serialize(failed.args??new Dictionary<string,object>())==args){resolvedBy[failed.id]=done.id;continue;}
                // #14: an out-of-range read corrected to a valid offset must count as resolved, while an
                // unrelated success (different target) and every uncertain outcome above stays unresolved.
                if(CorrectedReadFailure(failed,done))resolvedBy[failed.id]=done.id;
            }
        }
        void ApplyUndone(AgentCall undone){
            string artifactId=ArtifactId(undone);if(artifactId!=null)createdArtifactIds.Remove(artifactId);
            foreach(var key in resolvedBy.Where(pair=>pair.Value==undone.id).Select(pair=>pair.Key).ToList())resolvedBy.Remove(key);
        }
        public List<AgentPlanStep> plan=new List<AgentPlanStep>();public AgentVerification verification;public int planSteering;public string planInvalidation="",reviewInvalidation="";
        public bool PlanCurrent {get{return plan.Count==0||planSteering==steerVersion&&planInvalidation=="";}}
        void InvalidateReview(string reason){if(verification!=null){verification=null;reviewInvalidation=reason;if(status=="completed"||status=="partial"){status="review_required";phase="核对已失效，请重新检查结果。";}}if(plan.Count>0)planInvalidation=reason;}
        void AddInstruction(AgentEvent e){InvalidateReview("instructions");grants.Clear();adjustments.Add(Get<string>(e.data,"text"));steerVersion++;dialogue.Add(new AgentDialogue{id=e.id,role="user",text=Get<string>(e.data,"text"),sequence=e.sequence});
            // v0.12.1: acceptance and auto-derived tools follow the latest instruction instead of the values frozen
            // at task start. Replay-safe (no Library dependency) and best-effort so a journal from another version
            // still loads; an explicit user-picked allowedTools list is never overwritten.
            try{if(options!=null)options.delivery=AgentDeliveryRules.Derive(options,adjustments);}catch{}
            try{if(options!=null&&options.allowedToolsAuto)options.allowedTools=AgentToolProfiles.ForTask(options,null,adjustments);}catch{}
        }
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
                case "context.compacted":checkpoint=Get<AgentContextCheckpoint>(d,"checkpoint");compactRequested=false;compactInstructions="";break;
                case "context.compact_requested":compactRequested=true;compactInstructions=Get<string>(d,"instructions")??"";break;
                case "context.recovery":contextRecovered=true;break;
                case "context.skipped":case "context.discarded":compactRequested=false;compactInstructions="";break;
                case "subtask.started":calls.First(c=>c.id==Get<string>(d,"callId")).childTaskId=Get<string>(d,"taskId");break;
                case "tool.reconfirm":calls.First(c=>c.id==Get<string>(d,"callId")).status="pending";break;
                case "task.protocol":protocol=Get<string>(d,"protocol");break;
                case "request.repair":repairs++;break;
                case "task.adjusted":AddInstruction(e);break;
                // Native queue reduction: the engine emits task.adjusted for every consumed item too, so
                // plan/verification invalidation, grant revocation and transcript insertion keep one path.
                case "input.queued":inputs.Add(new AgentInput{id=Get<string>(d,"inputId"),text=Get<string>(d,"text"),mode=Get<string>(d,"mode"),created=e.created});break;
                case "input.consumed":{var input=inputs.FirstOrDefault(i=>i.id==Get<string>(d,"inputId"));if(input!=null){input.status="consumed";input.turnId=Get<string>(d,"turnId");}break;}
                case "input.restored":{foreach(var inputId in Get<List<string>>(d,"inputIds")??new List<string>()){var input=inputs.FirstOrDefault(i=>i.id==inputId);if(input!=null)input.status="restored";}break;}
                case "task.continued":AddInstruction(e);status="paused";phase="等待继续";break;
                case "request.started":requests++;cost+=Get<double>(d,"reserve");break;
                case "request.usage":cost+=Get<double>(d,"cost")-Get<double>(d,"reserve");inputTokens+=Get<int>(d,"inputTokens");outputTokens+=Get<int>(d,"outputTokens");cachedTokens+=d.ContainsKey("cachedTokens")?Get<int>(d,"cachedTokens"):0;estimated|=Get<bool>(d,"estimated");if(Get<string>(d,"purpose")!="compaction"){lastUsageSequence=e.sequence;lastUsageInputTokens=Get<int>(d,"inputTokens");lastUsageOutputTokens=Get<int>(d,"outputTokens");lastUsageEstimated=Get<bool>(d,"estimated");}break;
                case "assistant.interrupted":dialogue.Add(new AgentDialogue{id=e.id,role="agent",text=Get<string>(d,"text"),requestId=Get<string>(d,"requestId"),sequence=e.sequence,interrupted=true});break;
                case "approval.granted":grants.Add(Get<AgentApprovalGrant>(d,"grant"));break;
                case "approval.used":{var g=grants.FirstOrDefault(x=>x.id==Get<string>(d,"grantId"));if(g!=null)g.remaining=Math.Max(0,g.remaining-1);break;}
                case "approval.revoked":grants.Clear();break;
                case "assistant.reply":round++;replySteering=Get<int>(d,"steering");modelDone=Get<bool>(d,"done");needsModel=!modelDone;dialogue.Add(new AgentDialogue{id=e.id,role="agent",text=Get<string>(d,"text"),turnId=Get<string>(d,"turnId"),sequence=e.sequence,sourceIds=Get<List<string>>(d,"sources")??new List<string>()});break;
                case "task.note":dialogue.Add(new AgentDialogue{id=e.id,role="agent",meta=true,text=Get<string>(d,"text"),sequence=e.sequence});break;
                case "tool.proposed":var c=Get<AgentCall>(d,"call");c.sequence=e.sequence;calls.Add(c);if(!AgentCognition.IsControl(c.tool))verification=null;break;
                case "tool.started":case "tool.retry":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status=e.kind=="tool.retry"?"queued":"running";c1.error="";c1.attempt++;break;}
                case "tool.completed":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status="done";c1.result=Get<Dictionary<string,object>>(d,"result");c1.resultEventId=e.id;c1.stamp=Get<string>(d,"stamp");ApplyCompletion(c1);break;}
                case "tool.reused":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));var previous=calls.First(c=>c.resultEventId==Get<string>(d,"resultRef")&&c.result!=null);c1.status="done";c1.result=previous.result;c1.resultEventId=previous.resultEventId;break;}
                case "tool.sources":calls.First(c=>c.id==Get<string>(d,"callId")).sources=Get<Dictionary<string,string>>(d,"sources");break;
                case "tool.skipped":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status="skipped";grants.Clear();c1.error=d.ContainsKey("reason")?Get<string>(d,"reason"):"user";break;}
                case "tool.undone":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status="undone";grants.Clear();InvalidateReview("undo");ApplyUndone(c1);break;}
                case "tool.failed":{var c1=calls.First(c=>c.id==Get<string>(d,"callId"));c1.status="failed";c1.error=Get<string>(d,"message");c1.errorKind=Get<string>(d,"kind");c1.retryable=Get<bool>(d,"retryable");c1.result=Get<Dictionary<string,object>>(d,"result");c1.resultEventId=c1.result==null?null:e.id;break;}
                case "task.waiting":status="waiting";phase="等待确认";break;
                case "task.cancel_requested":case "task.cancelled":cancelRequested=true;status="cancelled";phase="任务已取消";break;
                case "task.paused":case "task.stopped":status=cancelRequested?"cancelled":e.kind=="task.paused"?"paused":"stopped";phase=cancelRequested?"任务已取消":"任务已暂停";break;
                case "task.failed":status="failed";error=Get<string>(d,"message");errorKind=Get<string>(d,"kind");retryable=Get<bool>(d,"retryable");break;
                case "research.checkpoint":researchCheckpoint=d;break;
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

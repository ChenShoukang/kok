using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
    public class AgentToolReceipt {public Dictionary<string,object> result;public string stamp;}
    // Domain-independent execution/state ownership. UI rendering neither schedules turns nor resolves promises.
    public sealed partial class AgentTaskEngine {
        public readonly AgentTaskState State;public readonly CancellationTokenSource Cancellation=new CancellationTokenSource();
        // C1: host delivery acceptance hook, called by AgentCognition before a completed task is verified.
        // Null keeps the pre-0.12 behavior (probes and legacy engines).
        public Func<AgentTaskState,string> DeliveryCheck;
        readonly AgentJournal journal;readonly Action changed;readonly Action validate;
        readonly Func<AgentTaskEngine,CancellationToken,Task<AgentModelTurn>> model;
        readonly Func<AgentTaskState,AgentCall,CancellationToken,Task<Dictionary<string,object>>> read;
        readonly Func<AgentTaskState,AgentCall,CancellationToken,Task<AgentToolReceipt>> write;
        readonly Func<AgentTaskState,AgentAction,string,string,AgentCall> prepare;
        readonly Func<AgentCall,bool> evidenceCurrent,historicalEvidence;
        readonly object gate=new object();readonly SemaphoreSlim decisions=new SemaphoreSlim(1,1);
        internal readonly AgentToolHooks Hooks;
        TaskCompletionSource<bool> wake=new TaskCompletionSource<bool>();public Task Completion;public bool IsRunning;bool finishTurnForced,forcedRequest;
        public AgentTaskEngine(AgentTaskState state,AgentJournal journal,Action validate,Func<AgentTaskEngine,CancellationToken,Task<AgentModelTurn>> model,Func<AgentTaskState,AgentCall,CancellationToken,Task<Dictionary<string,object>>> read,Func<AgentTaskState,AgentCall,CancellationToken,Task<AgentToolReceipt>> write,Func<AgentTaskState,AgentAction,string,string,AgentCall> prepare,Action changed,Func<AgentCall,bool> evidenceCurrent=null,Func<bool> automaticAllowed=null,Func<AgentCall,bool> historicalEvidence=null,AgentToolHooks hooks=null){State=state;this.journal=journal;this.validate=validate;this.model=model;this.read=read;this.write=write;this.prepare=prepare;this.changed=changed;this.evidenceCurrent=evidenceCurrent??(c=>c.status=="done");this.automaticAllowed=automaticAllowed;this.historicalEvidence=historicalEvidence;Hooks=hooks;}
        internal bool Executed(AgentCall call){return call.status=="done"&&(historicalEvidence==null||historicalEvidence(call));}
        // M0-4 middleware seams. With Hooks null every helper returns before touching state, so the
        // default execution path and its journal events stay byte-identical.
        // Execution hints: "serial" read tools run alone in request order; "" keeps the parallel batch.
        static bool SerialRead(AgentCall call){var spec=AgentRules.Tools.FirstOrDefault(t=>t.Name==call.tool);return spec!=null&&spec.Mode=="serial";}
        bool Blocked(AgentCall call){
            var hook=Hooks==null?null:Hooks.BeforeToolCall;
            if(hook==null)return false;
            string reason=hook(this,call);
            if(String.IsNullOrWhiteSpace(reason))return false;
            Emit("tool.failed",new{callId=call.id,kind="blocked",message=reason,retryable=false});
            return true;
        }
        Dictionary<string,object> Patched(AgentCall call,Dictionary<string,object> result){
            var hook=Hooks==null?null:Hooks.AfterToolCall;
            if(hook==null)return result;
            var patched=hook(this,call,result);
            return patched??result;
        }
        public AgentEvent Emit(string kind,object data){lock(gate){
            string status=State.cancelRequested||kind=="task.cancel_requested"?"cancelled":kind=="task.failed"?"failed":kind=="task.waiting"?"waiting":kind=="task.paused"?"paused":kind=="task.stopped"?"stopped":kind=="task.running"?"running":kind=="task.completed"?"completed":State.status;
            if(kind=="task.completed"){var outcome=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(data));status=AgentTaskState.Get<bool>(outcome,"limited")?"limited":outcome.ContainsKey("outcome")?AgentTaskState.Get<string>(outcome,"outcome"):"completed";}
            if((kind=="tool.undone"||kind=="task.review_invalidated")&&State.verification!=null&&(State.status=="completed"||State.status=="partial"))status="review_required";
            if(kind=="task.continued")status="paused";
            var e=journal.Append(State.id,kind,data,State.options.sessionId,State.options.goal.Substring(0,Math.Min(48,State.options.goal.Length)),status);State.Apply(e);if(kind=="assistant.reply")ClearStream();if(changed!=null)changed();return e;
        }}
        public void Start(){if(IsRunning)throw new InvalidOperationException("Agent 正在处理，请等待或停止当前任务。");if(State.Terminal)throw new InvalidOperationException("任务已结束，请开始新任务。");IsRunning=true;Completion=Run();}
        public void Stop(){Cancellation.Cancel();InterruptStream("cancelled");Signal();}
        public T ReadSnapshot<T>(Func<T> read){lock(gate)return read();}
        void Signal(){lock(gate){var previous=wake;wake=new TaskCompletionSource<bool>();previous.TrySetResult(true);}}
        async Task Wait(){Task pending;lock(gate){if(!State.calls.Any(c=>c.status=="pending"||c.status=="running"))return;pending=wake.Task;}using(Cancellation.Token.Register(()=>Signal())){await pending;Cancellation.Token.ThrowIfCancellationRequested();}}
        public void Adjust(string text){validate();if(!IsRunning||String.IsNullOrWhiteSpace(text)||State.adjustments.Sum(t=>t.Length)+text.Length>400)throw new InvalidDataException("本次补充要求最多 400 字符。");Emit("task.adjusted",new{text=text.Trim()});foreach(var call in State.calls.Where(c=>c.status=="pending").ToList())Emit("tool.skipped",new{callId=call.id,reason="superseded"});Signal();}
        const int MaxQueuedInputs=8,MaxQueuedInputChars=8000,MaxInputText=2000;
        // Pi-style native queues: steering enters the next turn boundary without cancelling the in-flight
        // model request; follow-up continues the same task only when the run would otherwise finish.
        // Adjust stays the separate urgent-correction path (it still cancels pending proposals).
        public void EnqueueInput(string text,string mode){
            if(!IsRunning)throw new InvalidOperationException("任务未在运行，无法加入队列。");
            validate();text=(text??"").Trim();mode=(mode??"").Trim();
            if(mode!="steer"&&mode!="follow")throw new InvalidDataException("输入类型无效。");
            if(text.Length==0||text.Length>MaxInputText)throw new InvalidDataException("补充输入不能为空且最多 2000 字符。");
            lock(gate){
                if(!IsRunning||Cancellation.IsCancellationRequested)throw new InvalidOperationException("任务未在运行，无法加入队列。");
                var queued=State.QueuedInputs;if(queued.Count>=MaxQueuedInputs)throw new InvalidDataException("待处理输入最多 8 条。");
                if(queued.Sum(i=>i.text.Length)+text.Length>MaxQueuedInputChars)throw new InvalidDataException("待处理输入总长度不能超过 8000 字符。");
                Emit("input.queued",new{inputId=Guid.NewGuid().ToString("N"),text,mode});
            }
        }
        internal bool ConsumeSteering(AgentTaskState task){return ConsumeInput(task,"steer");}
        internal bool ConsumeFollowUp(AgentTaskState task){return ConsumeInput(task,"follow");}
        bool ConsumeInput(AgentTaskState task,string mode){lock(gate){
            var queued=(task??State).QueuedInputs.Where(i=>i.mode==mode).ToList();if(queued.Count==0)return false;
            var item=queued[0];
            Emit("input.consumed",new{inputId=item.id,mode=item.mode,turnId=(string)null});
            // task.adjusted is the proven injection path: user dialogue row, steerVersion++, plan and
            // verification invalidation, grant revocation. No tool.skipped: steering never cancels tools.
            Emit("task.adjusted",new{text=item.text});
            return true;
        }}
        internal void RestoreQueuedInputs(string reason){lock(gate){
            var queued=State.QueuedInputs;if(queued.Count==0)return;
            Emit("input.restored",new{inputIds=queued.Select(i=>i.id).ToArray(),reason});
        }}
        public async Task Decide(string callId,bool approve,bool batch=false,bool grant=false,bool automatic=false){
            await decisions.WaitAsync();try{validate();Cancellation.Token.ThrowIfCancellationRequested();var call=State.calls.FirstOrDefault(c=>c.id==callId);if(!IsRunning||call==null||call.status!="pending")throw new InvalidOperationException("请先继续任务并重新确认操作。");
                if(State.options.approvalPolicy=="read_only")throw new AgentFailure("approval","只读任务不允许执行修改。");
                if(!approve){RevokeGrants("rejected");Emit("tool.skipped",new{callId=call.id,reason="user"});Signal();return;}
                if(new[]{"mcp_call","delegate_task","delegate_parallel","run_code","start_command_job","open_browser","browser_action"}.Contains(call.tool)&&batch)throw new AgentFailure("approval","外部工具需要逐项确认。");
                if(grant&&(batch||automatic||!AgentApproval.Eligible(State,call)||automaticAllowed==null||!automaticAllowed()))throw new AgentFailure("approval","此操作不能授予本任务许可。");
                if(automatic){var permit=AgentApproval.Find(State,call);if(permit==null||!CanAutoApprove(call))return;Emit("approval.used",new{grantId=permit.id,callId=call.id});}
                if(grant)Emit("approval.granted",new{grant=new AgentApprovalGrant{id=Guid.NewGuid().ToString("N"),tool=call.tool,ids=AgentApproval.Targets(call),steering=State.steerVersion}});
                if(Blocked(call)){Signal();return;}
                journal.CheckCapacity(State.id,600000);Emit("tool.started",new{callId=call.id});
                bool applied=false;try{var receipt=await write(State,call,Cancellation.Token);var result=Patched(call,receipt.result);Emit("tool.completed",new{callId=call.id,result,stamp=receipt.stamp});applied=true;}
                catch(OperationCanceledException){throw;}catch(Exception error){RevokeGrants("failed");var f=AgentFailure.Classify(error);var toolFailure=error as AgentToolFailure;Emit("tool.failed",new{callId=call.id,kind=f.kind,message=f.Message,retryable=false,result=toolFailure==null?null:toolFailure.result});}
                if(applied&&State.options.kernelVersion>=3&&read!=null){
                    // This is an actual host read, with its own durable outcome; afterText is never a read receipt.
                    // P0-1: createdAtifact receipts are read back too, so the model (and delivery acceptance) sees
                    // the real saved content instead of guessing; scope validation uses the updated task scope.
                    var readback=new List<string>();
                    if(call.changes!=null)readback.AddRange(call.changes.Select(change=>change.id));
                    string artifactId=AgentTaskState.ArtifactId(call);if(artifactId!=null)readback.Add(artifactId);
                    // P0-3: in-place edits (replace_document_text / edit_presentation) are read back too so delivery
                    // acceptance can prove the changed content; the edited id never grants any extra scope.
                    string editTargetId=AgentTaskState.EditTargetId(call);if(editTargetId!=null&&State.InScope(editTargetId))readback.Add(editTargetId);
                    var ids=readback.Where(id=>!String.IsNullOrEmpty(id)).Distinct().ToList();
                    if(ids.Count>0)try{string why=artifactId!=null?"Host read-back after approved artifact creation":editTargetId!=null?"Host read-back after approved in-place edit":"Host read-back after approved metadata write";for(int n=0;n<ids.Count;n+=8){var back=prepare(State,new AgentAction{tool="read_items",args=new Dictionary<string,object>{{"ids",ids.Skip(n).Take(8).ToList()}},why=why},call.turnId,null);back.hostGenerated=true;Emit("tool.proposed",new{call=back});await ReadCall(back);}}
                    catch(OperationCanceledException){throw;}catch(AgentFailure f){Emit("task.note",new{text="Automatic read-back unavailable: "+f.Message});}
                    catch{Emit("task.note",new{text="Automatic read-back unavailable. Read the changed sources before completing."});}
                }
                Signal();
            }finally{decisions.Release();}
        }
        // Proposal rejected by policy/validation before execution: recorded like any other failed call so the
        // model sees the reason in the next tool result. Never executed and never blocks the run loop.
        static AgentCall RejectedProposal(AgentAction action,string turnId,string providerId,string kind,string message){
            return new AgentCall{id=Guid.NewGuid().ToString("N"),providerId=providerId,turnId=turnId,tool=action==null?null:action.tool,args=action==null||action.args==null?new Dictionary<string,object>():action.args,why=action==null?null:action.why,status="failed",errorKind=kind,error=message??"",retryable=false};
        }
        async Task ReadCall(AgentCall call){
            Cancellation.Token.ThrowIfCancellationRequested();validate();journal.CheckCapacity(State.id,70000);
            // Control tools (update_plan/finish_task) are host task-control, not middleware-executed tools.
            bool hooked=!AgentCognition.IsControl(call.tool);
            if(hooked&&Blocked(call))return;
            Emit("tool.started",new{callId=call.id});
            try{var result=AgentCognition.IsControl(call.tool)?AgentCognition.Run(this,call,evidenceCurrent):await read(State,call,Cancellation.Token);Cancellation.Token.ThrowIfCancellationRequested();validate();if(hooked&&Hooks!=null&&Hooks.AfterToolCall!=null)result=Patched(call,result);if(result.ContainsKey("_sourceStamps")){Emit("tool.sources",new{callId=call.id,sources=result["_sourceStamps"]});result.Remove("_sourceStamps");}if(result.ContainsKey("resultRef"))Emit("tool.reused",new{callId=call.id,resultRef=result["resultRef"]});else Emit("tool.completed",new{callId=call.id,result=result});}
            catch(OperationCanceledException){throw;}catch(Exception error){var f=AgentFailure.Classify(error);Emit("tool.failed",new{callId=call.id,kind=f.kind,message=f.Message,retryable=f.retryable});}
        }
        async Task Run(){
            try{
                validate();Emit("task.running",new{});
                // A restarted write may have committed before its receipt reached the journal. Never replay it.
                if(State.calls.Any(c=>c.status=="running"&&!AgentRules.ReadTools.Contains(c.tool)))throw new AgentFailure("uncertain_write","上次写入结果尚未核对，不能自动重放。请核对改动后重新规划。");
                foreach(var c in State.calls.Where(c=>c.status=="running"||c.status=="failed"&&c.retryable).ToList())Emit("tool.retry",new{callId=c.id});
                while(true){
                    validate();Cancellation.Token.ThrowIfCancellationRequested();
                    var queued=State.calls.Where(c=>c.status=="queued").ToList();if(queued.Count>0){Emit("task.phase",new{phase="阅读资料"});foreach(var c in queued.Where(c=>c.tool=="update_plan"))await ReadCall(c);var reads=queued.Where(c=>!AgentCognition.IsControl(c.tool)).ToList();foreach(var c in reads.Where(SerialRead))await ReadCall(c);await Task.WhenAll(reads.Where(c=>!SerialRead(c)).Select(ReadCall));foreach(var c in queued.Where(c=>c.tool=="finish_task"))await ReadCall(c);}
                    if(State.calls.Any(c=>c.status=="failed"&&c.errorKind=="uncertain_external"))throw new AgentFailure("uncertain_external","外部工具结果尚未确认，不能自动重放。请先检查外部服务。");
                    // Only retryable infrastructure failures stop the task; other tool errors are reported back to the model.
                    var failed=State.calls.FirstOrDefault(c=>c.status=="failed"&&c.retryable);if(failed!=null)throw new AgentFailure(failed.errorKind,failed.error,true);
                    if(State.calls.Any(c=>c.status=="pending")){
                        foreach(var c in State.calls.Where(c=>c.status=="pending").ToList())if(CanAutoApprove(c))await Decide(c.id,true,automatic:true);
                        Emit("task.waiting",new{});while(State.calls.Any(c=>c.status=="pending"||c.status=="running"))await Wait();
                        validate();Cancellation.Token.ThrowIfCancellationRequested();failed=State.calls.FirstOrDefault(c=>c.status=="failed"&&c.retryable);if(failed!=null)throw new AgentFailure(failed.errorKind,failed.error,true);
                    }
                    // Single steering poll point: run start and every turn boundary re-enter it, after any
                    // approval wait, before completion checks. One message per turn (Pi one-at-a-time):
                    // fall through to the model request instead of immediately polling again.
                    bool forced=forcedRequest;forcedRequest=false;
                    bool steered=ConsumeSteering(State);
                    if(!steered&&!forced){
                        string lastTurn=State.dialogue.Where(d=>d.role=="agent"&&!d.meta&&!d.interrupted).Select(d=>d.turnId).LastOrDefault();bool failedLast=lastTurn!=null&&State.calls.Any(c=>c.turnId==lastTurn&&c.status=="failed");
                        if(State.options.kernelVersion>=3&&State.verification!=null&&State.verification.steering==State.steerVersion){if(ConsumeFollowUp(State))continue;Emit("task.completed",new{limited=false,outcome=State.verification.status});break;}
                        if(State.options.kernelVersion<3&&State.round>0&&State.modelDone&&!failedLast&&State.steerVersion==State.replySteering){if(ConsumeFollowUp(State))continue;Emit("task.completed",new{limited=false});break;}
                    }
                    if(State.requests>=State.options.requestLimit){RestoreQueuedInputs("limited");Emit("task.completed",new{limited=true});break;}
                    journal.CheckCapacity(State.id,600000);Emit("task.phase",new{phase="生成方案"});int steering=State.steerVersion;
                    var turn=await model(this,Cancellation.Token);Cancellation.Token.ThrowIfCancellationRequested();validate();
                    if(steering!=State.steerVersion){InterruptStream("superseded");Emit("request.discarded",new{reason="new user instructions"});continue;}
                    string turnId=Guid.NewGuid().ToString("N");
                    var sources=State.calls.Where(c=>c.status=="done"&&c.result!=null).SelectMany(c=>AgentTaskState.Get<List<ChatReference>>(c.result,"references")??new List<ChatReference>()).Select(r=>r.fileId).Where(id=>id!=null&&State.InScope(id)).Distinct().ToList();
                    var proposals=new List<AgentCall>();
                    for(int n=0;n<turn.reply.actions.Count;n++){
                        var action=turn.reply.actions[n];string providerId=n<turn.providerIds.Count?turn.providerIds[n]:null;
                        // P1 safety net: a proposal refused by the tool allow-list (policy) or by its own
                        // parameter validation is recorded as a durable failed call instead of aborting the
                        // whole task. The next model round can reroute or finish partial; unknown exceptions
                        // still propagate so real bugs are never hidden.
                        try{proposals.Add(prepare(State,action,turnId,providerId));}
                        catch(FormatException error){proposals.Add(RejectedProposal(action,turnId,providerId,"validation",error.Message));}
                        catch(AgentFailure failure)when(failure.kind=="policy"){proposals.Add(RejectedProposal(action,turnId,providerId,"policy",failure.Message));}
                    }
                    Emit("assistant.reply",new{text=turn.reply.reply,turnId=turnId,done=turn.reply.done,sources=sources,steering=steering});
                    foreach(var call in proposals){
                        Emit("tool.proposed",new{call=call});
                        if(call.status=="failed")Emit("tool.failed",new{callId=call.id,kind=call.errorKind,message=call.error,retryable=false});
                    }
                    // FinishTurn runs after the reply and its proposals. "continue" may force exactly one
                    // extra model request per task, and only when nothing queued/pending needs execution
                    // first; verification/approval/completion rules still run on the next iteration.
                    if(Hooks!=null&&Hooks.FinishTurn!=null){
                        string decision=Hooks.FinishTurn(this,turn);
                        if(!finishTurnForced&&decision=="continue"&&!State.calls.Any(c=>c.status=="pending"||c.status=="queued"||c.status=="running")){finishTurnForced=true;forcedRequest=true;continue;}
                    }
                }
            }catch(OperationCanceledException){try{InterruptStream("cancelled");RevokeGrants("stopped");RestoreQueuedInputs("aborted");Emit("task.stopped",new{});}catch{}}
            catch(Exception error){var f=AgentFailure.Classify(error);try{InterruptStream("failed");RevokeGrants("failed");bool limited=f.kind=="budget"&&State.requests>=State.options.requestLimit;RestoreQueuedInputs(limited?"limited":"failed");if(limited)Emit("task.completed",new{limited=true});else Emit("task.failed",new{kind=f.kind,message=f.Message,retryable=f.retryable});}catch{State.status="failed";State.error="任务记录保存失败，未继续执行。";State.retryable=false;}}
            finally{IsRunning=false;Signal();if(changed!=null)changed();}
        }
    }
}

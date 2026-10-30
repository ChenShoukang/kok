using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
    public class AgentToolReceipt {public Dictionary<string,object> result;public string stamp;}
    // Domain-independent execution/state ownership. UI rendering neither schedules turns nor resolves promises.
    public sealed class AgentTaskEngine {
        public readonly AgentTaskState State;public readonly CancellationTokenSource Cancellation=new CancellationTokenSource();
        readonly AgentJournal journal;readonly Action changed;readonly Action validate;
        readonly Func<AgentTaskEngine,CancellationToken,Task<AgentModelTurn>> model;
        readonly Func<AgentTaskState,AgentCall,CancellationToken,Task<Dictionary<string,object>>> read;
        readonly Func<AgentTaskState,AgentCall,CancellationToken,Task<AgentToolReceipt>> write;
        readonly Func<AgentTaskState,AgentAction,string,string,AgentCall> prepare;
        readonly Func<AgentCall,bool> evidenceCurrent;
        readonly object gate=new object();readonly SemaphoreSlim decisions=new SemaphoreSlim(1,1);
        TaskCompletionSource<bool> wake=new TaskCompletionSource<bool>();public Task Completion;public bool IsRunning;
        public AgentTaskEngine(AgentTaskState state,AgentJournal journal,Action validate,Func<AgentTaskEngine,CancellationToken,Task<AgentModelTurn>> model,Func<AgentTaskState,AgentCall,CancellationToken,Task<Dictionary<string,object>>> read,Func<AgentTaskState,AgentCall,CancellationToken,Task<AgentToolReceipt>> write,Func<AgentTaskState,AgentAction,string,string,AgentCall> prepare,Action changed,Func<AgentCall,bool> evidenceCurrent=null){State=state;this.journal=journal;this.validate=validate;this.model=model;this.read=read;this.write=write;this.prepare=prepare;this.changed=changed;this.evidenceCurrent=evidenceCurrent??(c=>c.status=="done");}
        public AgentEvent Emit(string kind,object data){lock(gate){
            string status=State.cancelRequested||kind=="task.cancel_requested"?"cancelled":kind=="task.failed"?"failed":kind=="task.waiting"?"waiting":kind=="task.paused"?"paused":kind=="task.stopped"?"stopped":kind=="task.running"?"running":kind=="task.completed"?"completed":State.status;
            if(kind=="task.completed"){var outcome=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(data));status=AgentTaskState.Get<bool>(outcome,"limited")?"limited":outcome.ContainsKey("outcome")?AgentTaskState.Get<string>(outcome,"outcome"):"completed";}
            if((kind=="tool.undone"||kind=="task.review_invalidated")&&State.verification!=null&&(State.status=="completed"||State.status=="partial"))status="review_required";
            if(kind=="task.continued")status="paused";
            var e=journal.Append(State.id,kind,data,State.options.sessionId,State.options.goal.Substring(0,Math.Min(48,State.options.goal.Length)),status);State.Apply(e);if(changed!=null)changed();return e;
        }}
        public void Start(){if(IsRunning)throw new InvalidOperationException("Agent 正在处理，请等待或停止当前任务。");if(State.Terminal)throw new InvalidOperationException("任务已结束，请开始新任务。");IsRunning=true;Completion=Run();}
        public void Stop(){Cancellation.Cancel();Signal();}
        public T ReadSnapshot<T>(Func<T> read){lock(gate)return read();}
        void Signal(){lock(gate){var previous=wake;wake=new TaskCompletionSource<bool>();previous.TrySetResult(true);}}
        async Task Wait(){Task pending;lock(gate){if(!State.calls.Any(c=>c.status=="pending"||c.status=="running"))return;pending=wake.Task;}using(Cancellation.Token.Register(()=>Signal())){await pending;Cancellation.Token.ThrowIfCancellationRequested();}}
        public void Adjust(string text){validate();if(!IsRunning||String.IsNullOrWhiteSpace(text)||State.adjustments.Sum(t=>t.Length)+text.Length>400)throw new InvalidDataException("本次补充要求最多 400 字符。");Emit("task.adjusted",new{text=text.Trim()});foreach(var call in State.calls.Where(c=>c.status=="pending").ToList())Emit("tool.skipped",new{callId=call.id,reason="superseded"});Signal();}
        public async Task Decide(string callId,bool approve,bool batch=false){
            await decisions.WaitAsync();try{validate();Cancellation.Token.ThrowIfCancellationRequested();var call=State.calls.FirstOrDefault(c=>c.id==callId);if(!IsRunning||call==null||call.status!="pending")throw new InvalidOperationException("请先继续任务并重新确认操作。");
                if(!approve){Emit("tool.skipped",new{callId=call.id,reason="user"});Signal();return;}
                if(new[]{"mcp_call","delegate_task","run_code"}.Contains(call.tool)&&batch)throw new AgentFailure("approval","外部工具需要逐项确认。");
                journal.CheckCapacity(State.id,600000);Emit("tool.started",new{callId=call.id});
                try{var receipt=await write(State,call,Cancellation.Token);Emit("tool.completed",new{callId=call.id,result=receipt.result,stamp=receipt.stamp});}
                catch(OperationCanceledException){throw;}catch(Exception error){var f=AgentFailure.Classify(error);var toolFailure=error as AgentToolFailure;Emit("tool.failed",new{callId=call.id,kind=f.kind,message=f.Message,retryable=false,result=toolFailure==null?null:toolFailure.result});}
                Signal();
            }finally{decisions.Release();}
        }
        async Task ReadCall(AgentCall call){
            Cancellation.Token.ThrowIfCancellationRequested();validate();journal.CheckCapacity(State.id,70000);Emit("tool.started",new{callId=call.id});
            try{var result=AgentCognition.IsControl(call.tool)?AgentCognition.Run(this,call,evidenceCurrent):await read(State,call,Cancellation.Token);Cancellation.Token.ThrowIfCancellationRequested();validate();if(result.ContainsKey("resultRef"))Emit("tool.reused",new{callId=call.id,resultRef=result["resultRef"]});else Emit("tool.completed",new{callId=call.id,result=result});}
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
                    var queued=State.calls.Where(c=>c.status=="queued").ToList();if(queued.Count>0){Emit("task.phase",new{phase="阅读资料"});foreach(var c in queued.Where(c=>c.tool=="update_plan"))await ReadCall(c);await Task.WhenAll(queued.Where(c=>!AgentCognition.IsControl(c.tool)).Select(ReadCall));foreach(var c in queued.Where(c=>c.tool=="finish_task"))await ReadCall(c);}
                    if(State.calls.Any(c=>c.status=="failed"&&c.errorKind=="uncertain_external"))throw new AgentFailure("uncertain_external","外部工具结果尚未确认，不能自动重放。请先检查外部服务。");
                    // Only retryable infrastructure failures stop the task; other tool errors are reported back to the model.
                    var failed=State.calls.FirstOrDefault(c=>c.status=="failed"&&c.retryable);if(failed!=null)throw new AgentFailure(failed.errorKind,failed.error,true);
                    if(State.calls.Any(c=>c.status=="pending")){
                        Emit("task.waiting",new{});while(State.calls.Any(c=>c.status=="pending"||c.status=="running"))await Wait();
                        validate();Cancellation.Token.ThrowIfCancellationRequested();failed=State.calls.FirstOrDefault(c=>c.status=="failed"&&c.retryable);if(failed!=null)throw new AgentFailure(failed.errorKind,failed.error,true);
                    }
                    string lastTurn=State.dialogue.Where(d=>d.role=="agent"&&!d.meta).Select(d=>d.turnId).LastOrDefault();bool failedLast=lastTurn!=null&&State.calls.Any(c=>c.turnId==lastTurn&&c.status=="failed");
                    if(State.options.kernelVersion>=3&&State.verification!=null&&State.verification.steering==State.steerVersion){Emit("task.completed",new{limited=false,outcome=State.verification.status});break;}
                    if(State.options.kernelVersion<3&&State.round>0&&State.modelDone&&!failedLast&&State.steerVersion==State.replySteering){Emit("task.completed",new{limited=false});break;}
                    if(State.requests>=State.options.requestLimit){Emit("task.completed",new{limited=true});break;}
                    journal.CheckCapacity(State.id,600000);Emit("task.phase",new{phase="生成方案"});int steering=State.steerVersion;
                    var turn=await model(this,Cancellation.Token);Cancellation.Token.ThrowIfCancellationRequested();validate();
                    if(steering!=State.steerVersion){Emit("request.discarded",new{reason="new user instructions"});continue;}
                    string turnId=Guid.NewGuid().ToString("N");
                    var sources=State.calls.Where(c=>c.status=="done"&&c.result!=null).SelectMany(c=>AgentTaskState.Get<List<ChatReference>>(c.result,"references")??new List<ChatReference>()).Select(r=>r.fileId).Where(id=>id!=null&&State.options.allowed.Contains(id)).Distinct().ToList();
                    var proposals=new List<AgentCall>();for(int n=0;n<turn.reply.actions.Count;n++)proposals.Add(prepare(State,turn.reply.actions[n],turnId,n<turn.providerIds.Count?turn.providerIds[n]:null));
                    Emit("assistant.reply",new{text=turn.reply.reply,turnId=turnId,done=turn.reply.done,sources=sources,steering=steering});
                    foreach(var call in proposals)Emit("tool.proposed",new{call=call});
                }
            }catch(OperationCanceledException){try{Emit("task.stopped",new{});}catch{}}
            catch(Exception error){var f=AgentFailure.Classify(error);try{if(f.kind=="budget"&&State.requests>=State.options.requestLimit)Emit("task.completed",new{limited=true});else Emit("task.failed",new{kind=f.kind,message=f.Message,retryable=f.retryable});}catch{State.status="failed";State.error="任务记录保存失败，未继续执行。";State.retryable=false;}}
            finally{IsRunning=false;Signal();if(changed!=null)changed();}
        }
    }
}

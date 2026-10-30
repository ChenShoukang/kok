using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace kok {
    public partial class AgentService {
        AgentJournal taskJournal;AgentTaskEngine taskEngine;
        AgentJournal TaskJournal {get{return taskJournal??(taskJournal=new AgentJournal(normalLibrary.DataDir));}}
        public static readonly string[] AgentTaskActions={"agentTaskStart","agentTaskRead","agentTaskList","agentTaskResume","agentTaskDecide","agentTaskStop","agentTaskAdjust","agentTaskUndo","agentTaskEvents","agentTaskDelete","agentTaskTrace","exportAgentTrace","agentHandoffPreview","agentHandoffPrepare","agentHandoffRead"};
        void ValidateTask(AgentTaskState task){if(!String.IsNullOrEmpty(task.options.parentTaskId)&&(taskEngine==null||taskEngine.State.id!=task.options.parentTaskId||taskEngine.State.steerVersion!=task.options.parentSteering||taskEngine.Cancellation.IsCancellationRequested))throw new OperationCanceledException();try{context.Policy.Ensure(Need.Ai|Need.Network|Need.NormalLibrary,"agent");}catch(InvalidOperationException){throw new OperationCanceledException();}}
        void TaskChanged(AgentTaskState task){CancelStaleChildren(task);if(context.Jobs!=null)context.Jobs.Changed();Notify("agentTaskChanged",new{taskId=task.id,sessionId=task.options.sessionId,sequence=task.sequence,status=task.status,running=IsTaskRunning(task.id)});}
        bool EvidenceCurrent(AgentCall call){try{if(call.tool=="search_memory"&&call.result!=null&&call.result.ContainsKey("memoryRefs")&&AgentTaskState.Get<Dictionary<string,string>>(call.result,"memoryRefs").Any(r=>!Memories.Current(r.Key,r.Value)))return false;if(call.tool=="remember_memory")return call.result!=null&&Memories.Current(Convert.ToString(call.result["memoryId"]),Convert.ToString(call.result["revision"]));if(normalLibrary.OperationStatus(call.id)=="undone"||StaleRead(call))return false;if(call.changes!=null)foreach(var change in call.changes){var item=normalLibrary.Find(change.id);if(item==null||change.after.Any(p=>Library.Json.Serialize(typeof(Entry).GetField(p.Key).GetValue(item))!=Library.Json.Serialize(p.Value)))return false;}if(call.stamp!=null&&call.result!=null&&call.result.ContainsKey("id")&&normalLibrary.AgentCreatedStamp(Convert.ToString(call.result["id"]))!=call.stamp)return false;return true;}catch{return false;}}
        AgentTaskState LoadTask(string id){lock(childrenGate){AgentTaskEngine child;if(children.TryGetValue(id,out child))return child.State;}if(taskEngine!=null&&taskEngine.State.id==id)return taskEngine.State;return AgentTaskState.Replay(id,TaskJournal.Events(id));}
        static bool CanResume(AgentTaskState task){return !task.Terminal&&task.errorKind!="uncertain_external"&&task.errorKind!="uncertain_write"&&task.errorKind!="budget"&&!task.calls.Any(c=>c.tool=="mcp_call"&&c.status=="running")&&(task.requests<task.options.requestLimit||task.calls.Any(c=>c.status=="pending"));}
        static bool CanRespond(AgentTaskState task){return task.status=="needs_input"&&task.requests<task.options.requestLimit&&!task.cancelRequested&&!task.calls.Any(c=>c.status=="running"||c.status=="pending"||c.errorKind=="uncertain_external");}
        void CheckReview(AgentTaskState task){if(task.verification==null)return;var memory=task.events.LastOrDefault(e=>e.kind=="memory.recalled");if(memory!=null&&AgentTaskState.Get<string>(memory.data,"signature")!=MemorySignature(task)){(taskEngine!=null&&taskEngine.State==task?taskEngine:MakeEngine(task)).Emit("task.review_invalidated",new{reason="memory_changed"});return;}var evidence=new HashSet<string>(task.verification.checks.SelectMany(c=>c.evidence).Concat(task.plan.SelectMany(p=>p.evidence)));if(task.calls.Any(c=>!AgentCognition.IsControl(c.tool)&&c.status=="done"&&(evidence.Contains(c.id)||AgentRules.WriteTools.Contains(c.tool))&&!EvidenceCurrent(c)))(taskEngine!=null&&taskEngine.State==task?taskEngine:MakeEngine(task)).Emit("task.review_invalidated",new{reason="source_changed"});}
        object TaskSnapshot(AgentTaskState t){return taskEngine!=null&&taskEngine.State==t?taskEngine.ReadSnapshot(()=>ProjectSnapshot(t)):ProjectSnapshot(t);}
        object ProjectSnapshot(AgentTaskState t){
            CheckReview(t);
            bool live=taskEngine!=null&&taskEngine.State.id==t.id&&taskEngine.IsRunning;lock(childrenGate){AgentTaskEngine child;if(children.TryGetValue(t.id,out child))live=child.IsRunning;}bool uncertainExternal=!live&&t.calls.Any(c=>c.tool=="mcp_call"&&c.status=="running");string error=uncertainExternal?"外部工具结果尚未确认，不能自动重放。请先检查外部服务。":t.error,errorKind=uncertainExternal?"uncertain_external":t.errorKind;var entries=new List<object>();
            foreach(var d in t.dialogue)entries.Add(new{sequence=d.sequence,message=(object)new{id=d.id,role=d.role,text=d.text,meta=d.meta,sourceIds=d.sourceIds}});
            foreach(var c in t.calls){var undo=c.status=="done"&&c.changes!=null?c.changes.Select(x=>new AgentChange{id=x.id,before=x.after,after=x.before}).ToList():null;var refs=c.result==null?new List<ChatReference>():AgentTaskState.Get<List<ChatReference>>(c.result,"references")??new List<ChatReference>();
                entries.Add(new{sequence=c.sequence,message=(object)new{id=c.id,role="tool",tool=c.tool,args=c.args,why=c.why,status=c.status,childTaskId=c.childTaskId,taskId=t.id,nativeCall=true,changes=c.changes,beforeText=c.beforeText,afterText=c.afterText,readPreview=c.result==null?"":Convert.ToString(c.result.ContainsKey("text")?c.result["text"]:"").Substring(0,Math.Min(1600,Convert.ToString(c.result.ContainsKey("text")?c.result["text"]:"").Length)),resultEventId=c.resultEventId,sourceIds=refs.Select(r=>r.fileId).Where(x=>x!=null).ToArray(),result=c.result==null?null:new Dictionary<string,object>(c.result.Where(k=>new[]{"id","name","memoryId","revision","childTaskId","artifact","sandbox"}.Contains(k.Key)).ToDictionary(k=>k.Key,k=>k.Value)),undoChanges=undo,undoMemory=c.tool=="remember_memory"?c.stamp:null,undoCreated=c.stamp==null||c.tool=="remember_memory"?null:new{id=c.result!=null&&c.result.ContainsKey("id")?Convert.ToString(c.result["id"]):"",stamp=c.stamp},error=c.error,errorKind=c.errorKind,retryable=c.retryable}});
            }
            if(t.status=="failed"||uncertainExternal)entries.Add(new{sequence=t.sequence+1,message=(object)new{id=t.id+"-error",role="error",text=error,goal=t.options.goal,errorKind=errorKind}});
            var messages=entries.OrderBy(x=>Convert.ToInt32(x.GetType().GetProperty("sequence").GetValue(x,null))).Select(x=>x.GetType().GetProperty("message").GetValue(x,null)).ToList();
            if(live&&t.phase=="生成方案")messages.Add(new{id=t.id+"-stream",role="thinking"});
            return new{id=t.id,sessionId=t.options.sessionId,status=t.status,phase=t.phase,error=error,errorKind=errorKind,retryable=t.retryable,running=live,resumable=!live&&String.IsNullOrEmpty(t.options.parentTaskId)&&CanResume(t),respondable=!live&&String.IsNullOrEmpty(t.options.parentTaskId)&&CanRespond(t),messages=messages,sequence=t.sequence,options=t.options,plan=t.plan,planCurrent=t.PlanCurrent,planInvalidation=t.planInvalidation,reviewInvalidation=t.reviewInvalidation,verification=t.verification,contextCheckpoint=t.checkpoint==null?null:new{id=t.checkpoint.id,through=t.checkpoint.through,current=CheckpointCurrent(t),summary=t.checkpoint.summary},contextUpperBound=ContextUpperBound(t),children=t.calls.Where(c=>c.childTaskId!=null).Select(c=>new{callId=c.id,taskId=c.childTaskId,status=c.status}).ToArray(),metrics=AgentTrace.Requests(t),usage=new{inputTokens=t.inputTokens,outputTokens=t.outputTokens,cachedTokens=t.cachedTokens,estimated=t.estimated,cost=t.cost,requests=t.requests,repairs=t.repairs},calls=t.calls.Select(c=>new{id=c.id,tool=c.tool,status=c.status,resultEventId=c.resultEventId}).ToArray(),protocol=t.protocol};
        }
        AgentTaskEngine MakeEngine(AgentTaskState t){return new AgentTaskEngine(t,TaskJournal,()=>ValidateTask(t),RunTaskModel,
            async(s,c,token)=>{
                ValidateTask(s);if(!ToolAllowed(s,AgentRules.Tools.First(x=>x.Name==c.tool)))throw new AgentFailure("policy","此工具未获得当前任务授权。");if(AgentAdvancedTools.Names.Contains(c.tool))return await PlatformRead(s,c,token);var readSpec=AgentRules.Tools.First(tool=>tool.Name==c.tool);context.Policy.Ensure(readSpec.Requires.Value,readSpec.Module);if(c.tool=="list_mcp_tools")return Mcp.Catalog(ModuleContext.Text(c.args,"serverId"),ModuleContext.Text(c.args,"tool"));var previous=s.calls.LastOrDefault(x=>x.id!=c.id&&x.status=="done"&&x.tool==c.tool&&Library.Json.Serialize(x.args)==Library.Json.Serialize(c.args)&&Library.Json.Serialize(x.sources)==Library.Json.Serialize(normalLibrary.AgentSourceStamps(x.sources.Keys)));
                if(previous!=null&&previous.result!=null&&c.tool=="read_items")return new Dictionary<string,object>{{"resultRef",previous.resultEventId}};
                var sourceEngine=taskEngine;lock(childrenGate){if(s.id!=taskEngine.State.id)sourceEngine=children[s.id];}sourceEngine.Emit("tool.sources",new{callId=c.id,sources=normalLibrary.AgentSourceStamps(c.sources.Keys)});return await normalLibrary.ReadAgentTool(s,c,token);
            },
            async(s,c,token)=>{ValidateTask(s);if(!ToolAllowed(s,AgentRules.Tools.First(x=>x.Name==c.tool)))throw new AgentFailure("policy","此工具未获得当前任务授权。");if(AgentAdvancedTools.Names.Contains(c.tool))return await PlatformWrite(s,c,token);using(context.BeginWrite()){if(c.tool=="open_item"){token.ThrowIfCancellationRequested();await context.OpenItem(new Dictionary<string,object>{{"id",Convert.ToString(c.args["id"])},{"play",false}});return new AgentToolReceipt{result=new Dictionary<string,object>{{"text","Opened by approved user action"}}};}
                var tool=AgentRules.Tools.First(t=>t.Name==c.tool);context.Policy.Ensure(tool.Requires.Value,tool.Module);if(c.tool=="mcp_call")return await Mcp.Call(c,token);if(tool.ExecuteWithContext!=null){var result=await tool.ExecuteWithContext(context,s,c,token);Notify("libraryChanged",null);return result;}
                var receipt=await Task.Run(()=>normalLibrary.WriteAgentTool(s,c,token,context.Ops));Notify("libraryChanged",null);return receipt;
            }},PrepareTaskCall,()=>TaskChanged(t),EvidenceCurrent);}
        void RunTask(AgentTaskState t){if(taskEngine!=null&&taskEngine.IsRunning)throw new InvalidOperationException("Agent 正在处理，请等待或停止当前任务。");ValidateTask(t);taskEngine=MakeEngine(t);taskEngine.Start();}
        public async Task<object> HandleAgentTaskAction(string action,Dictionary<string,object> a){
            if(library.IsPrivate)throw new InvalidOperationException("私密文件夹不启用 AI，请先返回普通资料库。");string id=StringArg(a,"taskId");
            if(PlatformActions.Contains(action))return await HandlePlatform(action,a);
            if(action=="agentTaskList")return TaskJournal.List().Where(row=>String.IsNullOrEmpty(row.parentTaskId)).OrderByDescending(t=>t.updated,StringComparer.Ordinal).ToList();
            if(action=="agentTaskRead")return TaskSnapshot(LoadTask(id));
            if(action=="agentTaskTrace"||action=="exportAgentTrace"){
                var traced=LoadTask(id);CheckReview(traced);object trace=taskEngine!=null&&taskEngine.State==traced?taskEngine.ReadSnapshot(()=>AgentTrace.Export(traced)):AgentTrace.Export(traced);
                if(action=="agentTaskTrace")return trace;
                using(var dialog=new System.Windows.Forms.SaveFileDialog{Filter="JSON (*.json)|*.json",FileName="kok-agent-trace-"+id.Substring(0,8)+".json"}){if(dialog.ShowDialog(context.Owner)!=System.Windows.Forms.DialogResult.OK)return new{cancelled=true};File.WriteAllText(dialog.FileName,Library.Json.Serialize(trace),new UTF8Encoding(false));return new{saved=true};}
            }
            if(action=="agentTaskEvents"){var events=TaskJournal.Events(id);string eventId=StringArg(a,"eventId");return eventId==""?(object)events.Select(e=>new{e.id,e.sequence,e.kind,e.created}).ToArray():(object)events.FirstOrDefault(e=>e.id==eventId);}
            if(action=="agentTaskDelete"){var deleted=LoadTask(id);if(IsTaskRunning(id)||deleted.calls.Any(c=>c.childTaskId!=null&&IsTaskRunning(c.childTaskId)))throw new InvalidOperationException("请先停止当前任务。");if(!String.IsNullOrEmpty(deleted.options.parentTaskId))throw new InvalidOperationException("请从父任务删除子任务记录。");foreach(var child in deleted.calls.Select(c=>c.childTaskId).Where(AgentJournal.ValidId).Distinct()){var saved=LoadTask(child);if(saved.options.parentTaskId==id)TaskJournal.Delete(child);}TaskJournal.Delete(id);if(taskEngine!=null&&taskEngine.State.id==id)taskEngine=null;return true;}
            if(action=="agentHandoffPreview"||action=="agentHandoffPrepare"||action=="agentHandoffRead")return HandleAgentHandoff(action,a);
            if(action=="agentTaskStop"){if(taskEngine!=null&&(id==""||id==taskEngine.State.id))taskEngine.Stop();return true;}
            if(action=="agentTaskStart"){
                normalLibrary.RequireAiMode();if(taskEngine!=null&&taskEngine.IsRunning)throw new InvalidOperationException("Agent 正在处理，请等待或停止当前任务。");
                var o=Library.Json.Deserialize<AgentTaskOptions>(Library.Json.Serialize(a));
                o.kernelVersion=3;if(o.contextTokens<8000||o.contextTokens>64000||o.allowedTools!=null&&(o.allowedTools.Count>100||o.allowedTools.Any(n=>!AgentRules.Tools.Any(t=>t.Name==n))))throw new InvalidDataException("任务工具或上下文设置无效。");
                if(!AgentJournal.ValidId(o.sessionId)||String.IsNullOrWhiteSpace(o.goal)||o.goal.Length>2000||o.instruction==null||o.instruction.Length>800||o.requestLimit<1||o.requestLimit>AgentRules.MaxRounds||!new[]{6000,14000,24000}.Contains(o.budget)||!new[]{"assist","discuss"}.Contains(o.intent)||!new[]{"auto","json","tools"}.Contains(o.protocol))throw new InvalidDataException("任务设置无效。");
                o.costLimit=AgentNumber(a,"costLimit",0,10000);o.inputRate=AgentNumber(a,"inputRate",0,10000);o.outputRate=AgentNumber(a,"outputRate",0,10000);if(o.costLimit>0&&(o.inputRate<=0||o.outputRate<=0))throw new InvalidDataException("请填写输入与输出单价后启用费用上限。");
                o.history=(o.history??new List<ChatTurn>()).Where(h=>h!=null&&(h.role=="user"||h.role=="assistant")&&h.content!=null).Take(10).ToList();if(o.history.Sum(h=>h.content.Length)>6000)throw new InvalidDataException("任务设置无效。");
                o.allowed=normalLibrary.ResolveAgentScope(o.scope);o.workflowGuidance=AgentWorkflows.Load(appDir,o.workflow);if(o.handoff!=null)ValidateHandoff(o.handoff,o.allowed);
                var t=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=o,protocol=ChooseTaskProtocol(o.protocol)};var seed=MakeEngine(t);seed.Emit("task.created",new{options=o,protocol=t.protocol});if(o.workflow!="")seed.Emit("task.note",new{text="使用技能 · "+o.workflow});RunTask(t);return TaskSnapshot(t);
            }
            var task=LoadTask(id);
            if(action=="agentTaskResume"){if(taskEngine!=null&&taskEngine.IsRunning)throw new InvalidOperationException("请先停止当前任务。");if(!CanResume(task))throw new InvalidOperationException("此任务不能直接续跑，请核对结果后交接到新任务。");normalLibrary.RequireAiMode();ReconcileTask(task);RunTask(task);return TaskSnapshot(task);}
            if(action=="agentTaskUndo"){
                var live=taskEngine!=null&&taskEngine.State.id==task.id?taskEngine:null;if(live==null&&taskEngine!=null&&taskEngine.IsRunning)throw new InvalidOperationException("请先停止当前任务。");
                var call=task.calls.FirstOrDefault(c=>c.id==StringArg(a,"callId"));if(call==null||call.status!="done")throw new InvalidDataException("Agent 修改无效。");
                if(call.tool=="remember_memory")Memories.Undo(call.stamp);else if(normalLibrary.OperationStatus(call.id)=="applied")context.Ops.Undo(call.id);else if(call.changes!=null)normalLibrary.ApplyAgentChanges(call.changes.Select(c=>new AgentChange{id=c.id,before=c.after,after=c.before}).ToList());else if(call.stamp!=null)normalLibrary.UndoAgentCreated(Convert.ToString(call.result["id"]),call.stamp);else throw new InvalidDataException("Agent 修改无效。");
                if(call.status!="undone")(live??MakeEngine(task)).Emit("tool.undone",new{callId=call.id});Notify("libraryChanged",null);return TaskSnapshot(task);
            }
            if(action=="agentTaskAdjust"&&CanRespond(task)){
                if(taskEngine!=null&&taskEngine.IsRunning)throw new InvalidOperationException("请先停止当前任务。");normalLibrary.RequireAiMode();string text=StringArg(a,"text");if(String.IsNullOrWhiteSpace(text)||task.adjustments.Sum(t=>t.Length)+text.Length>400)throw new InvalidDataException("本次补充要求最多 400 字符。");
                var next=MakeEngine(task);next.Emit("task.continued",new{text=text.Trim()});RunTask(task);return TaskSnapshot(task);
            }
            if(taskEngine==null||taskEngine.State.id!=id||!taskEngine.IsRunning)throw new InvalidOperationException("请先继续任务并重新确认操作。");
            if(action=="agentTaskAdjust"){taskEngine.Adjust(StringArg(a,"text"));return TaskSnapshot(task);}
            if(action=="agentTaskDecide"){await taskEngine.Decide(StringArg(a,"callId"),a.ContainsKey("approve")&&Convert.ToBoolean(a["approve"]),a.ContainsKey("batch")&&Convert.ToBoolean(a["batch"]));return TaskSnapshot(task);}
            throw new InvalidDataException("任务记录无效。");
        }
    }
}

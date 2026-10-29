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
        public static readonly string[] AgentTaskActions={"agentTaskStart","agentTaskRead","agentTaskList","agentTaskResume","agentTaskDecide","agentTaskStop","agentTaskAdjust","agentTaskUndo","agentTaskEvents","agentTaskDelete","agentHandoffPreview","agentHandoffPrepare","agentHandoffRead"};
        void ValidateTask(AgentTaskState task){try{context.Policy.Ensure(Need.Ai|Need.Network|Need.NormalLibrary,"agent");}catch(InvalidOperationException){throw new OperationCanceledException();}}
        void TaskChanged(){if(taskEngine!=null){context.Jobs.Changed();Notify("agentTaskChanged",new{taskId=taskEngine.State.id,sessionId=taskEngine.State.options.sessionId,sequence=taskEngine.State.sequence,status=taskEngine.State.status,running=taskEngine.IsRunning});}}
        AgentTaskState LoadTask(string id){if(taskEngine!=null&&taskEngine.State.id==id)return taskEngine.State;return AgentTaskState.Replay(id,TaskJournal.Events(id));}
        object TaskSnapshot(AgentTaskState t){
            bool live=taskEngine!=null&&taskEngine.State.id==t.id&&taskEngine.IsRunning;var entries=new List<object>();
            foreach(var d in t.dialogue)entries.Add(new{sequence=d.sequence,message=(object)new{id=d.id,role=d.role,text=d.text,meta=d.meta,sourceIds=d.sourceIds}});
            foreach(var c in t.calls){var undo=c.status=="done"&&c.changes!=null?c.changes.Select(x=>new AgentChange{id=x.id,before=x.after,after=x.before}).ToList():null;var refs=c.result==null?new List<ChatReference>():AgentTaskState.Get<List<ChatReference>>(c.result,"references")??new List<ChatReference>();
                entries.Add(new{sequence=c.sequence,message=(object)new{id=c.id,role="tool",tool=c.tool,args=c.args,why=c.why,status=c.status,taskId=t.id,nativeCall=true,changes=c.changes,beforeText=c.beforeText,afterText=c.afterText,readPreview=c.result==null?"":Convert.ToString(c.result.ContainsKey("text")?c.result["text"]:"").Substring(0,Math.Min(1600,Convert.ToString(c.result.ContainsKey("text")?c.result["text"]:"").Length)),resultEventId=c.resultEventId,sourceIds=refs.Select(r=>r.fileId).Where(x=>x!=null).ToArray(),result=c.result==null?null:new Dictionary<string,object>(c.result.Where(k=>k.Key=="id"||k.Key=="name").ToDictionary(k=>k.Key,k=>k.Value)),undoChanges=undo,undoCreated=c.stamp==null?null:new{id=c.result!=null&&c.result.ContainsKey("id")?Convert.ToString(c.result["id"]):"",stamp=c.stamp},error=c.error,errorKind=c.errorKind,retryable=c.retryable}});
            }
            if(t.status=="failed")entries.Add(new{sequence=t.sequence+1,message=(object)new{id=t.id+"-error",role="error",text=t.error,goal=t.options.goal,errorKind=t.errorKind}});
            var messages=entries.OrderBy(x=>Convert.ToInt32(x.GetType().GetProperty("sequence").GetValue(x,null))).Select(x=>x.GetType().GetProperty("message").GetValue(x,null)).ToList();
            if(live&&t.phase=="生成方案")messages.Add(new{id=t.id+"-stream",role="thinking"});
            return new{id=t.id,sessionId=t.options.sessionId,status=t.status,phase=t.phase,error=t.error,errorKind=t.errorKind,retryable=t.retryable,running=live,resumable=!live&&!t.Terminal,messages=messages,sequence=t.sequence,options=t.options,usage=new{inputTokens=t.inputTokens,outputTokens=t.outputTokens,cachedTokens=t.cachedTokens,estimated=t.estimated,cost=t.cost,requests=t.requests,repairs=t.repairs},calls=t.calls.Select(c=>new{id=c.id,tool=c.tool,status=c.status,resultEventId=c.resultEventId}),protocol=t.protocol};
        }
        AgentTaskEngine MakeEngine(AgentTaskState t){return new AgentTaskEngine(t,TaskJournal,()=>ValidateTask(t),RunTaskModel,
            async(s,c,token)=>{
                ValidateTask(s);var readSpec=AgentRules.Tools.First(tool=>tool.Name==c.tool);context.Policy.Ensure(readSpec.Requires.Value,readSpec.Module);var previous=s.calls.LastOrDefault(x=>x.id!=c.id&&x.status=="done"&&x.tool==c.tool&&Library.Json.Serialize(x.args)==Library.Json.Serialize(c.args)&&Library.Json.Serialize(x.sources)==Library.Json.Serialize(normalLibrary.AgentSourceStamps(x.sources.Keys)));
                if(previous!=null&&previous.result!=null&&c.tool=="read_items")return new Dictionary<string,object>{{"resultRef",previous.resultEventId}};
                taskEngine.Emit("tool.sources",new{callId=c.id,sources=normalLibrary.AgentSourceStamps(c.sources.Keys)});return await normalLibrary.ReadAgentTool(s,c,token);
            },
            async(s,c,token)=>{ValidateTask(s);using(context.BeginWrite()){if(c.tool=="open_item"){token.ThrowIfCancellationRequested();await context.OpenItem(new Dictionary<string,object>{{"id",Convert.ToString(c.args["id"])},{"play",false}});return new AgentToolReceipt{result=new Dictionary<string,object>{{"text","Opened by approved user action"}}};}
                var tool=AgentRules.Tools.First(t=>t.Name==c.tool);context.Policy.Ensure(tool.Requires.Value,tool.Module);if(tool.ExecuteWithContext!=null){var result=await tool.ExecuteWithContext(context,s,c,token);Notify("libraryChanged",null);return result;}
                var receipt=await Task.Run(()=>normalLibrary.WriteAgentTool(s,c,token,context.Ops));Notify("libraryChanged",null);return receipt;
            }},normalLibrary.PrepareAgentCall,TaskChanged);}
        void RunTask(AgentTaskState t){if(taskEngine!=null&&taskEngine.IsRunning)throw new InvalidOperationException("Agent 正在处理，请等待或停止当前任务。");ValidateTask(t);taskEngine=MakeEngine(t);taskEngine.Start();}
        public async Task<object> HandleAgentTaskAction(string action,Dictionary<string,object> a){
            if(library.IsPrivate)throw new InvalidOperationException("私密文件夹不启用 AI，请先返回普通资料库。");string id=StringArg(a,"taskId");
            if(action=="agentTaskList")return TaskJournal.List().OrderByDescending(t=>t.updated,StringComparer.Ordinal).ToList();
            if(action=="agentTaskRead")return TaskSnapshot(LoadTask(id));
            if(action=="agentTaskEvents"){var events=TaskJournal.Events(id);string eventId=StringArg(a,"eventId");return eventId==""?(object)events.Select(e=>new{e.id,e.sequence,e.kind,e.created}).ToArray():(object)events.FirstOrDefault(e=>e.id==eventId);}
            if(action=="agentTaskDelete"){if(taskEngine!=null&&taskEngine.State.id==id&&taskEngine.IsRunning)throw new InvalidOperationException("请先停止当前任务。");TaskJournal.Delete(id);if(taskEngine!=null&&taskEngine.State.id==id)taskEngine=null;return true;}
            if(action=="agentHandoffPreview"||action=="agentHandoffPrepare"||action=="agentHandoffRead")return HandleAgentHandoff(action,a);
            if(action=="agentTaskStop"){if(taskEngine!=null&&(id==""||id==taskEngine.State.id))taskEngine.Stop();return true;}
            if(action=="agentTaskStart"){
                normalLibrary.RequireAiMode();if(taskEngine!=null&&taskEngine.IsRunning)throw new InvalidOperationException("Agent 正在处理，请等待或停止当前任务。");
                var o=Library.Json.Deserialize<AgentTaskOptions>(Library.Json.Serialize(a));
                if(!AgentJournal.ValidId(o.sessionId)||String.IsNullOrWhiteSpace(o.goal)||o.goal.Length>2000||o.instruction==null||o.instruction.Length>800||o.requestLimit<1||o.requestLimit>AgentRules.MaxRounds||!new[]{6000,14000,24000}.Contains(o.budget)||!new[]{"assist","discuss"}.Contains(o.intent)||!new[]{"auto","json","tools"}.Contains(o.protocol))throw new InvalidDataException("任务设置无效。");
                o.costLimit=AgentNumber(a,"costLimit",0,10000);o.inputRate=AgentNumber(a,"inputRate",0,10000);o.outputRate=AgentNumber(a,"outputRate",0,10000);if(o.costLimit>0&&(o.inputRate<=0||o.outputRate<=0))throw new InvalidDataException("请填写输入与输出单价后启用费用上限。");
                o.history=(o.history??new List<ChatTurn>()).Where(h=>h!=null&&(h.role=="user"||h.role=="assistant")&&h.content!=null).Take(10).ToList();if(o.history.Sum(h=>h.content.Length)>6000)throw new InvalidDataException("任务设置无效。");
                o.allowed=normalLibrary.ResolveAgentScope(o.scope);o.workflowGuidance=AgentWorkflows.Load(appDir,o.workflow);if(o.handoff!=null)ValidateHandoff(o.handoff,o.allowed);
                var t=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=o,protocol=ChooseTaskProtocol(o.protocol)};var seed=MakeEngine(t);seed.Emit("task.created",new{options=o,protocol=t.protocol});if(o.workflow!="")seed.Emit("task.note",new{text="使用技能 · "+o.workflow});RunTask(t);return TaskSnapshot(t);
            }
            var task=LoadTask(id);
            if(action=="agentTaskResume"){if(taskEngine!=null&&taskEngine.IsRunning)throw new InvalidOperationException("请先停止当前任务。");if(task.Terminal)throw new InvalidOperationException("任务已结束，请开始新任务。");normalLibrary.RequireAiMode();ReconcileTask(task);RunTask(task);return TaskSnapshot(task);}
            if(action=="agentTaskUndo"){
                var live=taskEngine!=null&&taskEngine.State.id==task.id?taskEngine:null;if(live==null&&taskEngine!=null&&taskEngine.IsRunning)throw new InvalidOperationException("请先停止当前任务。");
                var call=task.calls.FirstOrDefault(c=>c.id==StringArg(a,"callId"));if(call==null||call.status!="done")throw new InvalidDataException("Agent 修改无效。");
                if(normalLibrary.OperationStatus(call.id)=="applied")context.Ops.Undo(call.id);else if(call.changes!=null)normalLibrary.ApplyAgentChanges(call.changes.Select(c=>new AgentChange{id=c.id,before=c.after,after=c.before}).ToList());else if(call.stamp!=null)normalLibrary.UndoAgentCreated(Convert.ToString(call.result["id"]),call.stamp);else throw new InvalidDataException("Agent 修改无效。");
                if(call.status!="undone")(live??MakeEngine(task)).Emit("tool.undone",new{callId=call.id});Notify("libraryChanged",null);return TaskSnapshot(task);
            }
            if(taskEngine==null||taskEngine.State.id!=id||!taskEngine.IsRunning)throw new InvalidOperationException("请先继续任务并重新确认操作。");
            if(action=="agentTaskAdjust"){taskEngine.Adjust(StringArg(a,"text"));return TaskSnapshot(task);}
            if(action=="agentTaskDecide"){await taskEngine.Decide(StringArg(a,"callId"),a.ContainsKey("approve")&&Convert.ToBoolean(a["approve"]));return TaskSnapshot(task);}
            throw new InvalidDataException("任务记录无效。");
        }
    }
}

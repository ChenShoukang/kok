using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;

namespace kok {
 public partial class AgentService {
  AgentMemoryStore memoryStore;AgentSandbox sandbox;
  AgentMemoryStore Memories {get{return memoryStore??(memoryStore=new AgentMemoryStore(normalLibrary));}}
  AgentSandbox Sandbox {get{return sandbox??(sandbox=new AgentSandbox(Notify,()=>context.Policy.Ensure(Need.Ai|Need.NormalLibrary,"agent")));}}
  readonly object budgetGate=new object(),childrenGate=new object();readonly Dictionary<string,AgentTaskEngine> budgetRoots=new Dictionary<string,AgentTaskEngine>();readonly Dictionary<string,AgentTaskEngine> children=new Dictionary<string,AgentTaskEngine>();
  internal static readonly string[] PlatformActions={"agentRuntimeInfo","agentMemoryList","agentMemorySave","agentMemoryDelete","agentMemoryUndo","agentSandboxResult","agentArtifactSave","agentWorkspaceConfig","agentWorkspaceSave","agentWorkspaceChooseFolder","agentScheduleList","agentScheduleSave","agentScheduleDelete"};
  AgentTaskEngine BudgetOwner(AgentTaskEngine engine){if(String.IsNullOrEmpty(engine.State.options.parentTaskId))return engine;lock(childrenGate){AgentTaskEngine owner;if(budgetRoots.TryGetValue(engine.State.id,out owner))return owner;}if(taskEngine==null||taskEngine.State.id!=engine.State.options.parentTaskId)throw new OperationCanceledException();return taskEngine;}
  void RequestEvent(AgentTaskEngine engine,string kind,object data){var root=BudgetOwner(engine);if(root!=engine){var row=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(data));row["childTaskId"]=engine.State.id;root.Emit(kind,row);}engine.Emit(kind,data);}
  bool IsTaskRunning(string id){if(taskEngine!=null&&taskEngine.State.id==id)return taskEngine.IsRunning;lock(childrenGate){AgentTaskEngine child;return children.TryGetValue(id,out child)&&child.IsRunning;}}
  void StopChildren(){AgentTaskEngine[] copy;lock(childrenGate)copy=children.Values.ToArray();foreach(var child in copy)child.Stop();if(sandbox!=null)sandbox.Stop();}
  void CancelStaleChildren(AgentTaskState parent){AgentTaskEngine[] copy;lock(childrenGate)copy=children.Values.Where(c=>c.State.options.parentTaskId==parent.id&&c.State.options.parentSteering!=parent.steerVersion).ToArray();foreach(var child in copy)child.Stop();}
  bool ToolAllowed(AgentTaskState task,AgentToolSpec tool){var o=task.options;return (o.intent!="discuss"||!tool.Write)&&(o.approvalPolicy!="read_only"||!tool.Write)&&(AgentCognition.IsControl(tool.Name)||o.allowedTools==null||o.allowedTools.Contains(tool.Name))&&(tool.Name!="search_memory"&&tool.Name!="remember_memory"||o.useMemory)&&((tool.Name!="delegate_task"&&tool.Name!="delegate_parallel")||o.allowDelegation&&String.IsNullOrEmpty(o.parentTaskId))&&(tool.Name!="run_code"||o.allowSandbox)&&(tool.Module!="mcp"||String.IsNullOrEmpty(o.parentTaskId));}
  AgentToolSpec[] TaskTools(AgentTaskState task){return AgentRules.Tools.Where(t=>ToolAllowed(task,t)&&ModuleRegistry.Builtins.ToolEnabled(t.Name,normalLibrary)&&(t.Name!="web_search"||task.options.searchEndpoint.Length>0)&&((t.Name!="list_folder"&&t.Name!="read_text_file"&&t.Name!="write_text_file"&&t.Name!="run_command")||AgentExecution.Configured(normalLibrary))&&(t.Module!="mcp"||mcp!=null&&mcp.Available&&normalLibrary.ModuleEnabled("mcp"))).ToArray();}
  void EmitSourceStamps(AgentTaskState task,AgentCall call,IEnumerable<string> ids){AgentTaskEngine engine=taskEngine;lock(childrenGate){if(task.id!=taskEngine.State.id)engine=children[task.id];}engine.Emit("tool.sources",new{callId=call.id,sources=normalLibrary.AgentSourceStamps(ids)});}
  async Task<Dictionary<string,object>> PlatformRead(AgentTaskState task,AgentCall call,CancellationToken token){
   if(call.tool=="search_memory"){var rows=await Memories.SearchHybrid(ModuleContext.Text(call.args,"query"),task.options.allowed,5,null,token);EmitSourceStamps(task,call,rows.SelectMany(r=>r.ids));return new Dictionary<string,object>{{"memoryRefs",rows.ToDictionary(r=>r.id,r=>r.revision)},{"text","[UNTRUSTED MEMORY]\n"+Library.Json.Serialize(rows.Select(r=>new{r.id,r.title,r.text,sources=r.ids}))}};}
   if(call.tool!="recall_result")return null;
   string id=ModuleContext.Text(call.args,"callId");var original=task.calls.FirstOrDefault(c=>c.id==id&&c.id!=call.id&&c.result!=null);if(original==null||original.sources.Keys.Any(k=>!task.options.allowed.Contains(k)))throw new AgentFailure("result","工具结果不在当前任务范围内。");if(!EvidenceCurrent(original))throw new AgentFailure("verification","原工具结果已失效，请重新读取来源。");
   EmitSourceStamps(task,call,original.sources.Keys);int offset=call.args.ContainsKey("offset")?Convert.ToInt32(call.args["offset"]):0,limit=call.args.ContainsKey("limit")?Convert.ToInt32(call.args["limit"]):4000;string text=ResultText(original);return new Dictionary<string,object>{{"text","[UNTRUSTED STORED RESULT "+id+" status="+original.status+" offset="+offset+" total="+text.Length+"]\n"+(offset>=text.Length?"":text.Substring(offset,Math.Min(limit,text.Length-offset)))},{"references",original.result.ContainsKey("references")?original.result["references"]:new object[0]}};
  }
  async Task<AgentToolReceipt> PlatformWrite(AgentTaskState task,AgentCall call,CancellationToken token){
   if(call.tool=="run_code")return new AgentToolReceipt{result=await Sandbox.Run(task.id,ModuleContext.Text(call.args,"code"),ModuleContext.Text(call.args,"input"),token)};
   if(call.tool=="delegate_task")return await Delegate(task,call,token);
   if(call.tool=="delegate_parallel")return await DelegateParallel(task,call,token);
   if(call.tool!="remember_memory")return null;
   if(Library.Json.Serialize(call.sources)!=Library.Json.Serialize(normalLibrary.AgentSourceStamps(call.sources.Keys)))throw new AgentConflictException("内容已被修改，已保留当前内容。请重新查看后再操作。");var record=Memories.Save(new AgentMemoryRecord{id=call.id,title=ModuleContext.Text(call.args,"title"),text=ModuleContext.Text(call.args,"text"),ids=AgentTaskState.Get<List<string>>(call.args,"ids"),taskId=task.id,callId=call.id},"");return new AgentToolReceipt{stamp=record.revision,result=new Dictionary<string,object>{{"text","Approved memory saved: "+record.title},{"memoryId",record.id},{"revision",record.revision}}};
  }
  AgentToolReceipt ChildReceipt(AgentTaskState child){CheckReview(child);string text=child.verification==null?child.dialogue.LastOrDefault(d=>d.role=="agent"&&!d.meta)?.text??"":child.verification.summary;var refs=child.calls.Where(c=>c.result!=null).SelectMany(c=>AgentTaskState.Get<List<ChatReference>>(c.result,"references")??new List<ChatReference>()).GroupBy(r=>r.fileId).Select(g=>g.First()).ToArray();var result=new Dictionary<string,object>{{"text","[UNTRUSTED SUBTASK REPORT status="+child.status+"]\n"+Clip(text,4000)},{"childTaskId",child.id},{"references",refs},{"artifact",true}};if(child.status!="completed")throw new AgentToolFailure("subtask_incomplete","子任务未完全完成，请查看报告。",result);return new AgentToolReceipt{result=result};}
  async Task<AgentToolReceipt> Delegate(AgentTaskState parent,AgentCall call,CancellationToken token){
   if(!parent.options.allowDelegation||!String.IsNullOrEmpty(parent.options.parentTaskId))throw new AgentFailure("policy","当前任务不允许委派子任务。");
   AgentTaskState child;if(!String.IsNullOrEmpty(call.childTaskId)){child=LoadTask(call.childTaskId);if(child.Terminal)return ChildReceipt(child);}else{
    if(parent.events.Count(e=>e.kind=="subtask.started")>=4)throw new AgentFailure("budget","每个任务最多委派 4 个子任务。");
    var ids=AgentTaskState.Get<List<string>>(call.args,"ids");if(ids==null||ids.Count==0||ids.Count>8||ids.Any(id=>!parent.options.allowed.Contains(id)))throw new AgentFailure("policy","子任务资料必须在父任务范围内。");
    int remaining=parent.options.requestLimit-parent.requests-1,requested=call.args.ContainsKey("requests")?Convert.ToInt32(call.args["requests"]):3;if(remaining<1)throw new AgentFailure("budget","剩余请求不足以启动子任务并核对结果。");
    var options=new AgentTaskOptions{sessionId=parent.options.sessionId,goal=ModuleContext.Text(call.args,"goal"),scope=new AgentScope{mode="selected",ids=ids},allowed=ids.ToList(),intent="discuss",protocol=parent.options.protocol,budget=parent.options.budget,requestLimit=Math.Min(remaining,requested),kernelVersion=3,parentTaskId=parent.id,parentCallId=call.id,parentSteering=parent.steerVersion,inputRate=parent.options.inputRate,outputRate=parent.options.outputRate,semanticCompression=parent.options.semanticCompression,contextTokens=parent.options.contextTokens,vision=parent.options.vision,searchEndpoint=parent.options.searchEndpoint,allowedTools=new List<string>{"search_library","read_items","read_image","list_collections","recall_result","update_plan","finish_task"}};
    child=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=options,protocol=ChooseTaskProtocol(options.protocol)};MakeEngine(child).Emit("task.created",new{options,protocol=child.protocol});taskEngine.Emit("subtask.started",new{callId=call.id,taskId=child.id});
   }
   child.options.parentSteering=parent.steerVersion;var engine=MakeEngine(child);lock(childrenGate){children[child.id]=engine;budgetRoots[child.id]=taskEngine;}
   using(token.Register(engine.Stop))try{engine.Start();await engine.Completion;token.ThrowIfCancellationRequested();ValidateTask(parent);taskEngine.Emit("subtask.finished",new{callId=call.id,taskId=child.id,status=child.status});return ChildReceipt(child);}finally{lock(childrenGate){children.Remove(child.id);budgetRoots.Remove(child.id);}}
  }
  async Task<AgentToolReceipt> DelegateParallel(AgentTaskState parent,AgentCall call,CancellationToken token){
   if(!parent.options.allowDelegation||!String.IsNullOrEmpty(parent.options.parentTaskId))throw new AgentFailure("policy","当前任务不允许委派子任务。");
   var goals=AgentTaskState.Get<List<string>>(call.args,"goals");var ids=AgentTaskState.Get<List<string>>(call.args,"ids");
   if(goals==null||goals.Count<2||goals.Count>3||ids==null||ids.Count<1||ids.Count>8||ids.Any(id=>!parent.options.allowed.Contains(id)))throw new AgentFailure("policy","并行子任务参数无效。");
   if(parent.events.Count(e=>e.kind=="subtask.started")+goals.Count>4)throw new AgentFailure("budget","每个任务最多委派 4 个子任务。");
   int remaining=parent.options.requestLimit-parent.requests-1,requested=call.args.ContainsKey("requests")?Convert.ToInt32(call.args["requests"]):3,perChild=Math.Min(remaining/goals.Count,Math.Max(1,requested));if(perChild<1)throw new AgentFailure("budget","剩余请求不足以启动并行子任务。");
   var engines=new List<AgentTaskEngine>();var states=new List<AgentTaskState>();
   foreach(var goal in goals){
    var options=new AgentTaskOptions{sessionId=parent.options.sessionId,goal=goal,scope=new AgentScope{mode="selected",ids=ids},allowed=ids.ToList(),intent="discuss",protocol=parent.options.protocol,budget=parent.options.budget,requestLimit=perChild,kernelVersion=3,parentTaskId=parent.id,parentCallId=call.id,parentSteering=parent.steerVersion,inputRate=parent.options.inputRate,outputRate=parent.options.outputRate,semanticCompression=parent.options.semanticCompression,contextTokens=parent.options.contextTokens,vision=parent.options.vision,searchEndpoint=parent.options.searchEndpoint,allowedTools=new List<string>{"search_library","read_items","read_image","list_collections","recall_result","update_plan","finish_task"}};
    var child=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=options,protocol=ChooseTaskProtocol(options.protocol)};MakeEngine(child).Emit("task.created",new{options,protocol=child.protocol});taskEngine.Emit("subtask.started",new{callId=call.id,taskId=child.id,parallel=true});
    var engine=MakeEngine(child);lock(childrenGate){children[child.id]=engine;budgetRoots[child.id]=taskEngine;}engines.Add(engine);states.Add(child);
   }
   try{
    using(token.Register(()=>{foreach(var engine in engines)engine.Stop();})){foreach(var engine in engines)engine.Start();await Task.WhenAll(engines.Select(e=>e.Completion));}
    token.ThrowIfCancellationRequested();ValidateTask(parent);
    string text="";bool incomplete=false;
    for(int n=0;n<states.Count;n++){var child=states[n];CheckReview(child);string summary=child.verification==null?(child.dialogue.LastOrDefault(d=>d.role=="agent"&&!d.meta)?.text??""):child.verification.summary;if(summary.Length>1200)summary=summary.Substring(0,1200)+"…";text+="\n["+(n+1)+"] "+child.options.goal+" - status="+child.status+"\n"+summary+"\n";if(child.status!="completed")incomplete=true;}
    var result=new Dictionary<string,object>{{"text","[UNTRUSTED PARALLEL SUBTASK REPORTS]\n"+Clip(text,6000)},{"childTaskId",states[0].id},{"childTaskIds",states.Select(s=>s.id).ToArray()}};
    if(incomplete)throw new AgentToolFailure("subtask_incomplete","部分并行子任务未完成，请查看报告。",result);
    return new AgentToolReceipt{result=result};
   }finally{lock(childrenGate){foreach(var child in states){children.Remove(child.id);budgetRoots.Remove(child.id);}}}
  }
  bool ReconcilePlatform(AgentTaskEngine engine,AgentTaskState task,AgentCall call){
   if(call.tool=="remember_memory"){var found=Memories.List().FirstOrDefault(r=>r.id==call.id&&r.callId==call.id);if(found!=null)engine.Emit("tool.completed",new{callId=call.id,stamp=found.revision,result=new{memoryId=found.id,revision=found.revision,text="Approved memory saved"}});else engine.Emit("tool.failed",new{callId=call.id,kind="uncertain_write",message="记忆写入结果尚未核对，请查看记忆列表。",retryable=false});return true;}
   if(call.tool!="delegate_task"&&call.tool!="run_code")return false;
   if(call.tool=="delegate_task"&&!String.IsNullOrEmpty(call.childTaskId)){var child=LoadTask(call.childTaskId);if(child.Terminal){try{var receipt=ChildReceipt(child);engine.Emit("tool.completed",new{callId=call.id,result=receipt.result});}catch(AgentToolFailure f){engine.Emit("tool.failed",new{callId=call.id,kind=f.kind,message=f.Message,retryable=false,result=f.result});}return true;}}
   engine.Emit("tool.reconfirm",new{callId=call.id});return true;
  }
  string MemoryUndoToken(){try{return Memories.LastUndoToken();}catch(InvalidDataException){return "";}}
  async Task<object> HandlePlatform(string action,Dictionary<string,object> a){
   if(action=="agentRuntimeInfo")return new{memoryUndoToken=MemoryUndoToken(),semanticCompression=true,memory=true,subagents=true,sandbox="quickjs-wasm",sandboxLanguages=new[]{"javascript"},sharedBudget=true,maxDepth=1,maxChildren=4,tools=AgentRules.Tools.Select(t=>new{t.Name,t.Title,t.Write}).ToArray()};
   if(action=="agentMemoryList")return Memories.List().Select(r=>new{record=r,stale=!Memories.Fresh(r)}).ToArray();
   if(action=="agentMemorySave"){var record=Library.Json.Deserialize<AgentMemoryRecord>(Library.Json.Serialize(a["record"]));record.taskId="";record.callId="";return Memories.Save(record,ModuleContext.Text(a,"expected"));}
   if(action=="agentMemoryDelete")return new{token=Memories.Delete(ModuleContext.Text(a,"id"),ModuleContext.Text(a,"revision"))};
   if(action=="agentMemoryUndo"){Memories.Undo(ModuleContext.Text(a,"token"));return true;}
   if(action=="agentSandboxResult")return Sandbox.Complete(ModuleContext.Text(a,"ticket"),Convert.ToBoolean(a["ok"]),ModuleContext.Text(a,"output"));
   if(action=="agentArtifactSave"){var task=LoadTask(ModuleContext.Text(a,"taskId"));var call=task.calls.FirstOrDefault(c=>c.id==ModuleContext.Text(a,"callId")&&c.result!=null&&c.result.ContainsKey("artifact"));if(call==null)throw new InvalidDataException("任务产物已不可用。");return await context.Invoke("createDocument",new Dictionary<string,object>{{"name",ModuleContext.Text(a,"name")},{"format",ModuleContext.Text(a,"format")},{"collection",""},{"text",ResultText(call)}});}
   if(action=="agentScheduleList")return AgentSchedules.View(normalLibrary);
   if(action=="agentScheduleSave"){var schedule=Library.Json.Deserialize<AgentSchedule>(Library.Json.Serialize(a.ContainsKey("schedule")?a["schedule"]:null));normalLibrary.SaveAgentSchedule(schedule);return AgentSchedules.View(normalLibrary);}
   if(action=="agentScheduleDelete"){normalLibrary.DeleteAgentSchedule(ModuleContext.Text(a,"id"));return AgentSchedules.View(normalLibrary);}
   if(action=="agentWorkspaceConfig")return AgentExecution.Config(normalLibrary);
   if(action=="agentWorkspaceSave"){AgentExecution.Save(normalLibrary,a.ContainsKey("enabled")&&Convert.ToBoolean(a["enabled"]),ModuleContext.Text(a,"root"));return AgentExecution.Config(normalLibrary);}
   if(action=="agentWorkspaceChooseFolder"){using(var dialog=new FolderBrowserDialog{Description="选择 Agent 执行工作目录",ShowNewFolderButton=true}){if(dialog.ShowDialog(context.Owner)!=DialogResult.OK)return AgentExecution.Config(normalLibrary);AgentExecution.Save(normalLibrary,a.ContainsKey("enabled")?Convert.ToBoolean(a["enabled"]):true,dialog.SelectedPath);return AgentExecution.Config(normalLibrary);}}
   throw new InvalidDataException("任务设置无效。");
  }
 }
}

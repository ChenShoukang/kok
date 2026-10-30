using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
namespace kok {public partial class AgentService {
 readonly ModuleContext context;
 Library normalLibrary {get{return context.Library;}}Library library {get{return context.Library;}}
 TranslationService translations {get{return context.Ai;}}string appDir {get{return context.AppDirectory;}}
 void Notify(string name,object data){context.Notify(name,data);}
 static string StringArg(Dictionary<string,object> a,string k){return ModuleContext.Text(a,k);}
 static double AgentNumber(Dictionary<string,object> a,string key,double fallback,double max){double n;try{n=a.ContainsKey(key)?Convert.ToDouble(a[key]):fallback;}catch{throw new InvalidDataException("Agent 用量设置无效。");}if(Double.IsNaN(n)||Double.IsInfinity(n)||n<0||n>max)throw new InvalidDataException("Agent 用量设置无效。");return n;}
 public AgentService(ModuleContext context){this.context=context;}
 public CancellationTokenSource Cancellation {get{return taskEngine!=null&&taskEngine.IsRunning?taskEngine.Cancellation:null;}}
 public void Stop(){if(taskEngine!=null)taskEngine.Stop();StopChildren();StopMcp();}
 public bool RequestStop(string id,bool cancel){lock(childrenGate){AgentTaskEngine child;if(children.TryGetValue(id,out child)){child.Stop();return true;}}if(taskEngine!=null&&taskEngine.IsRunning&&(id==""||id==taskEngine.State.id)){try{if(cancel)taskEngine.Emit("task.cancel_requested",new{});}finally{taskEngine.Stop();}return true;}if(!cancel||!AgentJournal.ValidId(id))return false;var task=LoadTask(id);if(task.Terminal)return false;MakeEngine(task).Emit("task.cancel_requested",new{});if(context.Jobs!=null)context.Jobs.Changed();Notify("agentTaskChanged",new{taskId=id,sessionId=task.options.sessionId,sequence=task.sequence,status="cancelled",running=false});return true;}
 public void OperationUndone(string id,string source){if(!source.StartsWith("agent:",StringComparison.Ordinal))return;string taskId=source.Substring(6);if(!AgentJournal.ValidId(taskId))return;try{var task=LoadTask(taskId);var call=task.calls.FirstOrDefault(c=>c.id==id);if(call!=null&&call.status!="undone")(taskEngine!=null&&taskEngine.State.id==taskId?taskEngine:MakeEngine(task)).Emit("tool.undone",new{callId=id});}catch(IOException){}}
 public List<Job> JobSnapshot(){return TaskJournal.List().Select(t=>{bool terminal=new[]{"completed","limited","partial","needs_input","cancelled","review_required"}.Contains(t.status);bool resumable=!terminal;if(resumable){try{var task=LoadTask(t.id);resumable=String.IsNullOrEmpty(task.options.parentTaskId)&&CanResume(task);}catch{resumable=false;}}return new Job{id="agent:"+t.id,module="agent",sourceId=t.id,title=t.title,state=t.status=="cancelled"?"cancelled":terminal?"done":t.status=="failed"?"failed":IsTaskRunning(t.id)?"running":"paused",resumable=resumable,requires=Need.Ai|Need.Network|Need.NormalLibrary,detail="agent"};}).ToList();}
}}

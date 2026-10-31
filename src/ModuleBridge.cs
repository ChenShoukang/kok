using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;
namespace kok {public partial class MainWindow {
 AgentService agentService;Jobs jobs;
 CancellationTokenSource ModuleCancellation(){var token=jobs==null?CancellationToken.None:jobs.CurrentToken;return token.CanBeCanceled?CancellationTokenSource.CreateLinkedTokenSource(token):new CancellationTokenSource();}
 CancellationTokenSource agentCancellation {get{return agentService==null?null:agentService.Cancellation;}}
 void CancelAgentRun(){if(agentService!=null)agentService.Stop();}
 ModuleContext CreateModuleContext(Form owner){
  var target=library;Action<string,object> notify=(name,data)=>{if(IsDisposed||Disposing)return;Action send=()=>{if(library==target&&!IsDisposed){if(name=="op.undone"&&target==normalLibrary&&agentService!=null){var row=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(data));agentService.OperationUndone(Convert.ToString(row["id"]),Convert.ToString(row["source"]));}Notify(name,data);}};if(InvokeRequired){if(IsHandleCreated)BeginInvoke(send);}else send();};
  if(jobs==null){jobs=new Jobs(normalLibrary.DataDir,(name,data)=>{if(!library.IsPrivate)Notify(name,data);});jobs.Add("agent",()=>agentService==null?new List<Job>():agentService.JobSnapshot());jobs.Add("downloads",()=>downloads==null?new List<Job>():downloads.JobSnapshot());}
  var ctx=new ModuleContext(target,translations,new ModulePolicy(target,()=>library==target,()=>privateOpening||privateGate||closingPending),owner,CancellationToken.None,notify,new Operations(target,notify),jobs,agentService,()=>{backgroundLibraryWrites++;return new ModuleLease(()=>backgroundLibraryWrites--);},async a=>(object)await OpenPreview(a),(entry,ext,filter)=>ChooseEditDestination(owner,entry,ext,filter),appDir);
  ctx.Ports=Ports(owner,null,null);ctx.Legacy=(name,args)=>ExecuteLegacy(name,args,owner,null,null);ctx.Invoke=(name,args)=>ModuleRegistry.Builtins.Dispatch(ctx,name,args);
  if(agentService==null&&!target.IsPrivate){agentService=new AgentService(ctx);var original=ctx;ctx=new ModuleContext(target,translations,ctx.Policy,owner,ctx.Token,notify,ctx.Ops,jobs,agentService,ctx.BeginWrite,async args=>await original.Invoke("openPreview",args),ctx.ChooseDestination,appDir);ctx.Ports=original.Ports;ctx.Legacy=original.Legacy;ctx.Invoke=(name,args)=>ModuleRegistry.Builtins.Dispatch(ctx,name,args);}
  ctx.StopModule=id=>{if(previewWindows.Any(w=>w.SessionLibrary==library&&library.Find(w.ItemId)!=null&&((id=="video"||id=="photo"||id=="music")&&library.Find(w.ItemId).kind==id||(id=="documents"||id=="reading"||id=="editing"||id=="presentations")&&new[]{"document","book"}.Contains(library.Find(w.ItemId).kind))))throw new InvalidOperationException("请先关闭使用此模块的预览窗口。");if(jobs!=null)jobs.StopScope(id);if(id=="workspace")normalLibrary.StopConnections();if(id=="mcp"&&agentService!=null)agentService.StopMcp();if(id=="agent"||id=="ai"){if(agentService!=null)agentService.Stop();}if(id=="downloads")PauseNetworkDownloads();if(id=="speech")CancelSpeechJobs();if(id=="editing")CancelCreativeJobs();if(id=="translation")foreach(var job in translationJobs.Values.ToArray())job.Cancel();if(id=="organizer"){if(folderCancellation!=null)folderCancellation.Cancel();if(organizationCancellation!=null)organizationCancellation.Cancel();}if(id=="ai"){if(fileChatCancellation!=null)fileChatCancellation.Cancel();if(modelListCancellation!=null)modelListCancellation.Cancel();}};return ctx;
 }
}}

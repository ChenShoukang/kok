using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace kok {
 internal static class WorkspaceModules {
  internal static KokModule Operations(){var id=CapabilitySchema.Obj(CapabilitySchema.Fields("id",CapabilitySchema.Str(32)),new[]{"id"});return new KokModule{Name="operations",Required=true,Ui=true,Actions=new[]{
   ModuleDefinitions.Action("operationList",Need.None,false,(c,a)=>Task.FromResult((object)c.Library.OperationList(a.ContainsKey("offset")?Convert.ToInt32(a["offset"]):0,a.ContainsKey("limit")?Convert.ToInt32(a["limit"]):100)),CapabilitySchema.Obj(CapabilitySchema.Fields("offset",new Dictionary<string,object>{{"type","integer"},{"minimum",0},{"maximum",10000000}},"limit",new Dictionary<string,object>{{"type","integer"},{"minimum",1},{"maximum",1000}}))),
   ModuleDefinitions.Action("operationApply",Need.None,true,async(c,a)=>{using(c.BeginWrite())return await Task.Run(()=>(object)c.Ops.Apply(ModuleContext.Text(a,"id")));},id),
   ModuleDefinitions.Action("operationUndo",Need.None,true,async(c,a)=>{using(c.BeginWrite())return await Task.Run(()=>(object)c.Ops.Undo(ModuleContext.Text(a,"id")));},id),
   ModuleDefinitions.Action("operationSkip",Need.None,true,(c,a)=>Task.FromResult((object)c.Ops.Skip(ModuleContext.Text(a,"id"))),id),
   ModuleDefinitions.Action("bulkClassify",Need.None,true,(c,a)=>Task.FromResult((object)c.Library.ProposeBulkCollection(AgentTaskState.Get<List<string>>(a,"ids"),ModuleContext.Text(a,"collection")))),
   ModuleDefinitions.Action("editOrganization",Need.None,true,(c,a)=>Task.FromResult((object)c.Library.ProposeMetadataEdit(ModuleContext.Text(a,"id"),ModuleContext.Text(a,"category"),AgentTaskState.Get<List<string>>(a,"tags"),ModuleContext.Text(a,"summary")))),
   new ActionSpec{Name="commandHistory",Title="操作历史",Requires=Need.None,Writes=false,Kind="open",Schema=CapabilitySchema.Obj(),Surfaces=new[]{"palette"},UiCommand="operations.history",Execute=(c,a)=>Task.FromResult((object)true)}},Facets=new[]{new FacetSpec{Name="operations"}}};}
  internal static KokModule Jobs(){return new KokModule{Name="jobs",Required=true,Ui=true,Actions=new[]{
   ModuleDefinitions.Action("jobList",Need.NormalLibrary,false,(c,a)=>Task.FromResult((object)c.Jobs.Snapshot()),CapabilitySchema.Obj()),
   ModuleDefinitions.Action("jobAction",Need.NormalLibrary,true,async(c,a)=>{string id=ModuleContext.Text(a,"id"),act=ModuleContext.Text(a,"action");if(id.StartsWith("job:",StringComparison.Ordinal))return await c.Jobs.Control(c,id,act);var job=c.Jobs.Snapshot().FirstOrDefault(j=>j.id==id);if(job==null||!new[]{"cancel","pause","resume"}.Contains(act)||act=="resume"&&!job.resumable||act=="pause"&&!new[]{"running","queued"}.Contains(job.state)||act=="cancel"&&new[]{"done","cancelled"}.Contains(job.state))throw new InvalidDataException("任务操作无效。");if(act=="resume")c.Policy.Ensure(job.requires,job.module);if(job.detail=="agent")return await c.Invoke(act=="resume"?"agentTaskResume":"agentTaskStop",act=="resume"?new Dictionary<string,object>{{"taskId",job.sourceId}}:new Dictionary<string,object>{{"taskId",job.sourceId},{"cancel",act=="cancel"}});return await c.Invoke(act=="resume"?"resumeDownload":act=="pause"?"pauseDownload":"cancelDownload",new Dictionary<string,object>{{"id",job.sourceId}});}),
   new ActionSpec{Name="commandActivity",Title="活动",Requires=Need.NormalLibrary,Writes=false,Kind="open",Schema=CapabilitySchema.Obj(),Surfaces=new[]{"palette"},UiCommand="jobs.activity",Execute=(c,a)=>Task.FromResult((object)true)}},Facets=new[]{new FacetSpec{Name="jobs"}}};}
 }
}

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
namespace kok {
 internal static class AgentCodeDelivery {
  internal static bool Resolved(Library library,AgentTaskState task,AgentCall failed){
   if(failed.status!="failed"||failed.tool!="apply_workspace_patch"||!new[]{"validation","conflict"}.Contains(failed.errorKind))return false;
   return task.calls.Any(c=>c.sequence>failed.sequence&&c.tool==failed.tool&&c.status=="done"&&c.result!=null&&String.Equals(ModuleContext.Text(c.args,"path"),ModuleContext.Text(failed.args,"path"),StringComparison.OrdinalIgnoreCase)&&ModuleContext.Text(c.result,"workspaceId")==AgentWorkspaceTools.RootId(library)&&AgentWorkspaceTools.InputsCurrent(library,new[]{new Dictionary<string,object>{{"path",ModuleContext.Text(c.result,"path")},{"sha256",ModuleContext.Text(c.result,"sha256")},{"workspaceId",ModuleContext.Text(c.result,"workspaceId")}}}));
  }
  internal static string Instructions(AgentTaskState task){return
   "Work in the configured code workspace. Read workspace_info/git_workspace and relevant files as needed. Preserve the user's existing modifications. "+
   "Make the smallest coherent change that meets the current goal and verify it before expanding scope. Preserve existing public behavior unless a change is requested; avoid adding helpers, validation policies or test frameworks without a concrete need. "+
   "Use apply_workspace_patch with the current raw SHA-256 for targeted changes; it supplies actual host read-back. Use expectedHash=missing only for new files. "+
   "When tests/build/verification are requested, run start_command_job with inputs listing changed source files and wait for the actual result. For directly executable TypeScript without another project toolchain, prefer workspace_info.bundledNode (the packaged Node runtime) if available. "+
   "A successful check only proves those exact file versions; after editing them, run the relevant checks again. Failed or unavailable checks must be reported honestly. Do not create a kok document unless the user explicitly asks for one. "+
   "Browser tools verify local HTTP apps only; observe fresh pageVersion/control IDs and inspect the resulting state. Later user adjustments replace conflicting earlier requirements (including an earlier instruction not to run tests); preserve work already completed.\n[Initial user goal]\n"+task.options.goal+"\n[Later user adjustments, in chronological order]\n"+String.Join("\n",task.adjustments);}
  internal static string Problem(Library library,AgentTaskState task){
   if(task.options.businessVersion>=1)return BusinessProblem(library,task);
   string rawGoal=AgentTaskIntent.Combined(task.options.goal??"",task.adjustments),goal=AgentTaskIntent.Actions(rawGoal);
   var mutations=task.calls.Where(c=>(c.tool=="apply_workspace_patch"||c.tool=="write_text_file")&&c.status=="done"&&c.result!=null&&c.result.ContainsKey("sha256")).GroupBy(c=>ModuleContext.Text(c.result,"path"),StringComparer.OrdinalIgnoreCase).Select(g=>g.Last()).ToArray();
   var reads=task.calls.Where(c=>c.tool=="read_workspace_file"&&c.status=="done"&&c.result!=null).GroupBy(c=>ModuleContext.Text(c.result,"path"),StringComparer.OrdinalIgnoreCase).Select(g=>g.Last()).ToArray();
   if(mutations.Length==0&&(task.options.intent=="discuss"||task.options.approvalPolicy=="read_only"))return "";
   bool analysis=Regex.IsMatch(goal,@"^(?:请|帮我|只|仅|先|\s)*(?:解释|分析|讨论|阅读|评估|审查|查看|explain\b|analy[sz]e\b|discuss\b|review\b|read\b|describe\b)",RegexOptions.IgnoreCase);
   bool thenWrite=Regex.IsMatch(goal,@"(?:然后|并|再|\band\b|\bthen\b)[^。！？;\n]{0,30}(?:修复|修改|实现|新增|优化|完善|\bfix\b|\bedit\b|\bimplement\b|\boptimize\b)",RegexOptions.IgnoreCase);
   if(mutations.Length==0&&analysis&&!thenWrite)return "";
   bool change=Regex.IsMatch(goal,@"修复|修改|实现|新增|添加|重构|优化|完善|\b(?:fix|implement|refactor|change|edit|optimize)\b|(?:^|[。！？；;\n])\s*(?:please\s+)?add\s+",RegexOptions.IgnoreCase);
   var targets=mutations.Select(m=>reads.LastOrDefault(r=>r.sequence>m.sequence&&String.Equals(ModuleContext.Text(r.result,"path"),ModuleContext.Text(m.result,"path"),StringComparison.OrdinalIgnoreCase))??m).ToArray();
   if(targets.Length==0&&task.options.workspaceVersion==1&&change)targets=reads;
   if(targets.Length==0)return task.options.workspaceVersion==1&&change?"代码任务尚无目标文件读取或可验证改动，请先检查实际代码。":"";
   foreach(var target in targets){
    if(ModuleContext.Text(target.result,"workspaceId")!=AgentWorkspaceTools.RootId(library))return "工作区已变化，旧工作区的补丁和验证不能证明当前结果。";
    string path=ModuleContext.Text(target.result,"path"),hash=ModuleContext.Text(target.result,"sha256");
    try{if(AgentWorkspaceTools.Hash(AgentWorkspaceTools.Bytes(library,path))!=hash)return "代码文件在补丁后已变化，请重新读取并验证当前版本："+path;}catch{return "代码文件当前不可读取："+path;}
   }
   Func<string,bool> decline=message=>Regex.IsMatch(message,@"(?:不要|无需|不必|不用)[^。；;，,\n]{0,30}(?:测试|运行|编译|验证)|do not run|skip tests",RegexOptions.IgnoreCase);
   bool declined=decline(task.options.goal??"");foreach(string adjustment in task.adjustments??new List<string>()){if(decline(adjustment))declined=true;else if(Regex.IsMatch(AgentTaskIntent.Actions(adjustment),@"(?:运行|执行|重跑|编译|构建|验证)|\b(?:run|rerun|execute|build|verify|test|check)\b",RegexOptions.IgnoreCase))declined=false;}
   bool verify=!declined&&(Regex.IsMatch(goal,@"测试|编译|运行|验证|\b(?:test|build|verify|run|check)\b",RegexOptions.IgnoreCase)||mutations.Length==0&&change);
   if(!verify)return "";
   var receipts=new List<Dictionary<string,object>>();
   foreach(var call in task.calls.Where(c=>c.status=="done"&&c.result!=null&&(AgentCommandJobTools.Names.Contains(c.tool)||c.tool=="run_command"))){
    if(call.result.ContainsKey("state")&&ModuleContext.Text(call.result,"state")!="completed")continue;
    var row=call.result;if(row.ContainsKey("result")&&row["result"]!=null)row=AgentTaskState.Get<Dictionary<string,object>>(row,"result");
    if(row.ContainsKey("exitCode")&&Convert.ToInt32(row["exitCode"])==0&&AgentTaskState.Get<bool>(row,"inputsCurrent")&&(!row.ContainsKey("outputsVerified")||AgentTaskState.Get<bool>(row,"outputsVerified")))receipts.Add(row);
   }
   Func<AgentCall,bool> proven=target=>receipts.Any(r=>(AgentTaskState.Get<List<Dictionary<string,object>>>(r,"checkedInputs")??new List<Dictionary<string,object>>()).Any(i=>String.Equals(ModuleContext.Text(i,"path"),ModuleContext.Text(target.result,"path"),StringComparison.OrdinalIgnoreCase)&&ModuleContext.Text(i,"sha256")==ModuleContext.Text(target.result,"sha256")&&ModuleContext.Text(i,"workspaceId")==AgentWorkspaceTools.RootId(library)));
   if(mutations.Length==0&&targets.Any(proven))return "";
   foreach(var target in targets)if(!proven(target))return "当前代码版本尚无成功的实际验证。请使用 start_command_job 的 inputs 声明改动文件，运行适用测试并等待实际退出码 0："+ModuleContext.Text(target.result,"path");
   return "";
  }
  static string BusinessProblem(Library library,AgentTaskState task){
   var calls=new List<object>();var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var call in task.calls.Where(call=>call.result!=null&&(AgentWorkspaceTools.Names.Contains(call.tool)||call.tool=="write_text_file"||call.tool=="run_command"||AgentCommandJobTools.Names.Contains(call.tool)))){
    var result=call.result;string state=ModuleContext.Text(result,"state");if(result.ContainsKey("result")&&result["result"]!=null)result=AgentTaskState.Get<Dictionary<string,object>>(result,"result");
    string path=ModuleContext.Text(result,"path");if(path!="")paths.Add(path);
    calls.Add(new{tool=call.tool,status=call.status,sequence=call.sequence,path=path,hash=ModuleContext.Text(result,"sha256"),workspaceId=ModuleContext.Text(result,"workspaceId"),jobState=state,
      exitCode=result.ContainsKey("exitCode")?(int?)Convert.ToInt32(result["exitCode"]):null,inputsCurrent=AgentTaskState.Get<bool>(result,"inputsCurrent"),outputsVerified=!result.ContainsKey("outputsVerified")||AgentTaskState.Get<bool>(result,"outputsVerified"),checkedInputs=AgentTaskState.Get<List<Dictionary<string,object>>>(result,"checkedInputs")??new List<Dictionary<string,object>>()});
   }
   if(calls.Count==0&&task.options.workspaceVersion!=1)return "";
   string workspace="";try{workspace=AgentWorkspaceTools.RootId(library);}catch(InvalidOperationException){}
   var files=paths.Select(path=>{string hash="";bool available=false;try{hash=AgentWorkspaceTools.Hash(AgentWorkspaceTools.Bytes(library,path));available=true;}catch(IOException){}catch(FormatException){}return new{path=path,hash=hash,available=available};}).ToArray();
   return AgentBusinessCore.Invoke<string>("code_problem",new{goal=task.options.goal??"",adjustments=task.adjustments,intent=task.options.intent,approvalPolicy=task.options.approvalPolicy,workspaceVersion=task.options.workspaceVersion,workspaceId=workspace,calls=calls,files=files});
  }
 }
}

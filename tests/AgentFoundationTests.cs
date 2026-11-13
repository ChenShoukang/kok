using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Collections.Generic;
using kok;

static class AgentFoundationTests {
    public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
    static async Task RunAsync(string root,Action<bool,string> check){
        Directory.CreateDirectory(root);var library=new Library(Path.Combine(root,"library"));library.SetAppMode("smart",true);
        var source=library.CreateDocument("来源","md","","最终预算8万元。");
        var state=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{runtime="research",sessionId=Guid.NewGuid().ToString("N"),goal="完整阅读这份资料，生成800-1200汉字的DOCX报告。",scope=new AgentScope{mode="selected",ids=new List<string>{source.id}},allowed=new List<string>{source.id}}};
        var initial=AgentContracts.Task(state);state.adjustments.Add("改为300-500汉字。");state.steerVersion=1;var updated=AgentContracts.Task(state);
        check(initial.version==1&&initial.deliverables.Single().minLength==800&&updated.revision==1&&updated.deliverables.Single().minLength==300&&updated.deliverables.Single().maxLength==500,"foundation: versioned TaskSpec follows the authoritative current requirements after adjustment");
        var inputs=AgentContracts.Assets(state,library);check(inputs.Count==1&&inputs[0].id==source.id&&inputs[0].role=="source"&&inputs[0].available&&inputs[0].version.Length>0,"foundation: selected native sources have real identity, version and availability without exposing paths");
        string before=inputs[0].version;File.WriteAllText(source.path,"最终预算调整为9万元，采用最新版本。",Encoding.UTF8);
        check(AgentContracts.Assets(state,library).Single().version!=before,"foundation: asset projection reflects the actual current file version");
        var caps=AgentContracts.Capabilities(library,AppDomain.CurrentDomain.BaseDirectory);
        check(caps.Count==9&&caps.Single(c=>c.id=="music").status=="partial"&&caps.Single(c=>c.id=="computer").status=="unconfigured"&&caps.Single(c=>c.id=="coding").status=="unconfigured","foundation: all nine domains are catalogued without pretending missing engines are available");
        var workspace=Path.Combine(root,"workspace");Directory.CreateDirectory(workspace);AgentExecution.Save(library,true,workspace,false);
        var context=new ModuleContext(library,new TranslationService(library.DataDir),new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory);
        var jobs=AgentCommandJobs.For(library);string executable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"kok.Tests.exe");
        Func<int,int,AgentCall> call=(delay,exit)=>new AgentCall{id=Guid.NewGuid().ToString("N"),tool="start_command_job",args=new Dictionary<string,object>{{"command",executable},{"args",new[]{"--command-job-fixture",delay.ToString(),exit.ToString()}},{"timeoutSeconds",10}}};
        var firstCall=call(350,0);firstCall.args["args"]=new[]{"--command-job-fixture","350","0","job-output.txt"};firstCall.args["outputs"]=new[]{"job-output.txt"};var first=jobs.Start(context,state,firstCall);string id=Convert.ToString(first["jobId"]);
        check(Convert.ToString(jobs.Start(context,state,firstCall)["jobId"])==id&&jobs.List().Count==1,"jobs: replaying the same approved call returns its original job instead of launching twice");
        var done=await jobs.Wait(id,state.id,5,CancellationToken.None);var result=AgentTaskState.Get<Dictionary<string,object>>(done,"result");
        check(Convert.ToString(done["state"])=="completed"&&Convert.ToInt32(result["exitCode"])==0&&Convert.ToString(result["stdout"]).Contains("job-fixture-ok"),"jobs: actual child command completes asynchronously with real output and exit code");
        state.calls.Add(new AgentCall{id=Guid.NewGuid().ToString("N"),tool="wait_command_job",status="done",result=done});var output=AgentContracts.Assets(state,library).Single(a=>a.id.StartsWith("workspace:"));
        check(output.available&&output.role=="artifact"&&output.path=="job-output.txt"&&output.version.Length>0,"foundation: declared command outputs join the asset projection using real files and versions");
        bool denied=false;try{jobs.Read(id,Guid.NewGuid().ToString("N"));}catch(InvalidDataException){denied=true;}check(denied,"jobs: a model task cannot read another task's job by guessing its ID");
        var failed=jobs.Start(context,state,call(0,7));var failedDone=await jobs.Wait(Convert.ToString(failed["jobId"]),state.id,5,CancellationToken.None);
        check(Convert.ToString(failedDone["state"])=="failed"&&Convert.ToInt32(AgentTaskState.Get<Dictionary<string,object>>(failedDone,"result")["exitCode"])==7,"jobs: a nonzero process exit stays failed and retains its actual result");
        var pending=jobs.Start(context,state,call(5000,0));string pendingId=Convert.ToString(pending["jobId"]);jobs.Cancel(pendingId,state.id);
        check(Convert.ToString((await jobs.Wait(pendingId,state.id,2,CancellationToken.None))["state"])=="cancelled","jobs: cancellation stops the owned job without reporting success");
        var encrypted=File.ReadAllBytes(Path.Combine(library.DataDir,"agent-command-jobs.sealed"));
        check(!Encoding.UTF8.GetString(encrypted).Contains("job-fixture-ok"),"jobs: durable command results are DPAPI encrypted at rest");
        var reopenedLibrary=new Library(library.DataDir);var reopened=AgentCommandJobs.For(reopenedLibrary);
        check(Convert.ToString(reopened.Read(id)["state"])=="completed"&&reopened.List().Count>=2,"jobs: completed results survive a new service instance without replay");
        var second=jobs.Start(context,state,call(5000,0));string secondId=Convert.ToString(second["jobId"]);string alternate=Path.Combine(root,"alternate");Directory.CreateDirectory(alternate);AgentExecution.Save(library,true,alternate,false);
        await Task.Delay(300);
        check(Convert.ToString(jobs.Read(secondId,state.id)["state"])=="cancelled","jobs: changing the execution workspace cancels jobs bound to the prior workspace");
        AgentCommandJobs.Stop(library);
    }
}

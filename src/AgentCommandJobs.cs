using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
    public sealed class AgentCommandJob {
        public int version=1;public string jobId="",taskId="",callId="",state="queued",title="",created="",updated="",error="",root="";
        public Dictionary<string,object> args=new Dictionary<string,object>(),result;
    }
    // Per-library durable metadata. Running processes are owned by cancellation/process-group handles;
    // startup marks unfinished work interrupted and never replays an approved command automatically.
    internal sealed class AgentCommandJobs {
        static readonly ConditionalWeakTable<Library,AgentCommandJobs> stores=new ConditionalWeakTable<Library,AgentCommandJobs>();
        internal static AgentCommandJobs For(Library library){return stores.GetValue(library,l=>new AgentCommandJobs(l));}
        internal static void Stop(Library library,string taskId=null){AgentCommandJobs value;if(stores.TryGetValue(library,out value))value.CancelAll(taskId);}
        internal static bool ActiveFor(Library library,string taskId){AgentCommandJobs value;return stores.TryGetValue(library,out value)&&value.HasActive(taskId);}
        readonly object gate=new object();readonly Library library;readonly string file;
        readonly List<AgentCommandJob> rows=new List<AgentCommandJob>();readonly Dictionary<string,CancellationTokenSource> tokens=new Dictionary<string,CancellationTokenSource>();
        static readonly byte[] entropy=Encoding.UTF8.GetBytes("kok.agent.command-jobs.v1");
        AgentCommandJobs(Library library){
            this.library=library;file=Path.Combine(library.DataDir,"agent-command-jobs.sealed");
            if(!File.Exists(file))return;
            try{var json=Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file),entropy,DataProtectionScope.CurrentUser));rows.AddRange(Library.Json.Deserialize<List<AgentCommandJob>>(json));if(rows.Count>1000||rows.Any(r=>r.version!=1||!AgentJournal.ValidId(r.jobId)))throw new InvalidDataException();}
            catch(Exception){throw new InvalidDataException("后台作业记录无法读取，原文件已保留。");}
            bool changed=false;foreach(var row in rows.Where(r=>r.state=="queued"||r.state=="running"||r.state=="cancelling")){row.state="interrupted";row.error="程序退出，作业未自动重新执行。";row.updated=Now();changed=true;}if(changed)Persist();
        }
        static string Now(){return DateTime.UtcNow.ToString("o");}
        static string Redact(string value){return Regex.Replace(value??"",@"sk-[A-Za-z0-9_-]{12,}","[redacted]");}
        static T Clone<T>(T value){return Library.Json.Deserialize<T>(Library.Json.Serialize(value));}
        void Persist(){Directory.CreateDirectory(library.DataDir);var bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(Library.Json.Serialize(rows)),entropy,DataProtectionScope.CurrentUser);string temp=file+".tmp";File.WriteAllBytes(temp,bytes);if(File.Exists(file))File.Replace(temp,file,file+".bak");else File.Move(temp,file);}
        AgentCommandJob Find(string id,string taskId=null){var row=rows.FirstOrDefault(r=>r.jobId==id);if(row==null)throw new InvalidDataException("后台作业不存在。");if(taskId!=null&&row.taskId!=taskId)throw new InvalidDataException("此后台作业不属于当前任务。");return row;}
        static bool Active(AgentCommandJob row){return row.state=="queued"||row.state=="running"||row.state=="cancelling";}
        static Dictionary<string,object> Public(AgentCommandJob row){return new Dictionary<string,object>{{"jobId",row.jobId},{"taskId",row.taskId},{"state",row.state},{"title",row.title},{"created",row.created},{"updated",row.updated},{"error",row.error},{"result",row.result}};}
        internal List<Dictionary<string,object>> List(){lock(gate)return rows.OrderByDescending(r=>r.created,StringComparer.Ordinal).Take(100).Select(r=>Clone(Public(r))).ToList();}
        internal Dictionary<string,object> Read(string id,string taskId=null){lock(gate)return Clone(Public(Find(id,taskId)));}
        internal bool HasActive(string taskId){lock(gate)return rows.Any(r=>r.taskId==taskId&&Active(r));}
        internal Dictionary<string,object> Start(ModuleContext context,AgentTaskState task,AgentCall call){
            context.Policy.Ensure(Need.NormalLibrary,"agent");
            string root=AgentExecution.Root(library);if(!AgentExecution.Configured(library))throw new InvalidDataException("执行环境未启用：请先在 Agent 的“能力与权限”中选择工作目录并开启执行。");
            AgentCommandJob row;CancellationTokenSource token;
            lock(gate){
                var existing=rows.FirstOrDefault(r=>r.callId==call.id);if(existing!=null)return Clone(Public(existing));
                if(rows.Count>=1000)throw new InvalidDataException("后台作业记录已达上限。");
                if(rows.Count(Active)>=2)throw new InvalidOperationException("最多同时运行两个后台作业。");
                var args=Clone(call.args);if(!args.ContainsKey("timeoutSeconds"))args["timeoutSeconds"]=600;
                AgentCommandJobTools.ValidateStart(new AgentAction{tool="start_command_job",args=args},id=>true);
                row=new AgentCommandJob{jobId=Guid.NewGuid().ToString("N"),taskId=task.id,callId=call.id,root=root,args=args,title=Path.GetFileName(Convert.ToString(args["command"])),created=Now(),updated=Now()};
                token=new CancellationTokenSource();rows.Add(row);tokens.Add(row.jobId,token);try{Persist();}catch{rows.Remove(row);tokens.Remove(row.jobId);token.Dispose();throw;}
            }
            var nativeCall=new AgentCall{id=call.id,args=Clone(row.args),tool="run_command"};
            Task.Run(()=>Run(context,row,nativeCall,token));
            context.Jobs?.Changed();context.Notify("job.changed",null);return Read(row.jobId,task.id);
        }
        void Run(ModuleContext context,AgentCommandJob row,AgentCall call,CancellationTokenSource cancellation){
            try{
                lock(gate){cancellation.Token.ThrowIfCancellationRequested();row.state="running";row.updated=Now();Persist();}
                var result=AgentExecution.RunCommand(context,call,cancellation.Token,3600,row.root);foreach(string key in result.Keys.ToArray())if(result[key] is string)result[key]=Redact((string)result[key]);
                var assets=new List<AgentAssetRef>();
                if(String.Equals(AgentExecution.Root(library),row.root,StringComparison.OrdinalIgnoreCase))foreach(string path in AgentTaskState.Get<List<string>>(row.args,"outputs")??new List<string>()){cancellation.Token.ThrowIfCancellationRequested();var asset=AgentExecution.WorkspaceAsset(library,path,"artifact");asset.originCallId=row.callId;assets.Add(asset);}result["assets"]=assets;
                lock(gate){row.result=result;cancellation.Token.ThrowIfCancellationRequested();row.state=Convert.ToInt32(result["exitCode"])==0&&AgentTaskState.Get<bool>(result,"inputsCurrent")?"completed":"failed";row.error=row.state=="failed"?(Convert.ToInt32(result["exitCode"])!=0?"命令以非零退出码结束。":"命令执行期间输入文件已变化，请重新验证。"):"";row.updated=Now();Persist();}
            }catch(OperationCanceledException){lock(gate){row.state="cancelled";row.updated=Now();try{Persist();}catch(IOException){}}}
            catch(Exception error){lock(gate){row.state=cancellation.IsCancellationRequested?"cancelled":"failed";row.error=cancellation.IsCancellationRequested?"":Redact(error.Message);row.updated=Now();try{Persist();}catch(IOException){}}}
            finally{lock(gate){tokens.Remove(row.jobId);}cancellation.Dispose();context.Jobs?.Changed();context.Notify("job.changed",null);}
        }
        internal Dictionary<string,object> Cancel(string id,string taskId=null){lock(gate){var row=Find(id,taskId);CancellationTokenSource token;if(Active(row)&&tokens.TryGetValue(id,out token)){token.Cancel();row.state="cancelling";row.updated=Now();Persist();}return Clone(Public(row));}}
        internal void CancelAll(string taskId=null){lock(gate){bool changed=false;foreach(var row in rows.Where(r=>Active(r)&&(taskId==null||r.taskId==taskId)).ToArray()){CancellationTokenSource token;if(tokens.TryGetValue(row.jobId,out token)){token.Cancel();row.state="cancelling";row.updated=Now();changed=true;}}if(changed)try{Persist();}catch(IOException){}}}
        internal async Task<Dictionary<string,object>> Wait(string id,string taskId,int seconds,CancellationToken token){var end=DateTime.UtcNow.AddSeconds(seconds);for(;;){token.ThrowIfCancellationRequested();var value=Read(id,taskId);string state=Convert.ToString(value["state"]);if(state!="queued"&&state!="running"&&state!="cancelling"||DateTime.UtcNow>=end)return value;await Task.Delay(100,token);}}
        internal List<Job> Snapshot(){lock(gate)return rows.OrderByDescending(r=>r.created,StringComparer.Ordinal).Take(100).Select(r=>new Job{id="agent-command:"+r.jobId,sourceId=r.jobId,module="agent",title=r.title,state=r.state=="completed"?"done":r.state=="interrupted"?"failed":r.state,detail="command-job",unit="step",done=Active(r)?0:1,total=1,requires=Need.NormalLibrary,pausable=false,resumable=false,controllable=false}).ToList();}
    }
    internal static class AgentCommandJobTools {
        internal static readonly string[] Names={"start_command_job","read_command_job","wait_command_job","cancel_command_job"};
        internal static void ValidateStart(AgentAction action,Func<string,bool> exists){
            string command=ModuleContext.Text(action.args,"command");if(String.IsNullOrWhiteSpace(command)||command.Length>260||command.Any(Char.IsControl))throw new FormatException("command must be a valid executable name");
            var args=AgentTaskState.Get<List<string>>(action.args,"args")??new List<string>();if(args.Count>32||args.Any(a=>a==null||a.Length>1024||a.Any(Char.IsControl)))throw new FormatException("Invalid command arguments");
            if(args.Sum(a=>a.Length)>4000)throw new FormatException("argument list is too long");
            if(action.args.ContainsKey("cwd"))AgentExecution.ValidateRelative(ModuleContext.Text(action.args,"cwd"),true);
            var outputs=AgentTaskState.Get<List<string>>(action.args,"outputs")??new List<string>();if(outputs.Count>16)throw new FormatException("outputs allows at most 16 workspace paths");foreach(string path in outputs)AgentExecution.ValidateRelative(path,false);
            var inputs=AgentTaskState.Get<List<string>>(action.args,"inputs")??new List<string>();if(inputs.Count>16)throw new FormatException("inputs allows at most 16 files");foreach(string path in inputs)AgentWorkspaceTools.SafePath(path);
            int timeout=action.args.ContainsKey("timeoutSeconds")?Convert.ToInt32(action.args["timeoutSeconds"]):600;if(timeout<1||timeout>3600)throw new FormatException("timeoutSeconds must be 1-3600");
        }
        internal static AgentToolSpec[] Tools(){
            var id=AgentRules.Str("Job ID returned by start_command_job",32);var readProps=AgentRules.Props("jobId",id);
            return new[]{
                new AgentToolSpec{Name=Names[0],Title="启动后台命令",Write=true,Requires=Need.NormalLibrary,CapabilityId="agent.start_command_job",Signature="start_command_job{command,args?,cwd?,timeoutSeconds?,inputs?,outputs?}",Description="PROPOSE one executable and its exact arguments in the configured workspace. User approval is required. Returns a durable jobId immediately; timeout is 1-3600 seconds (default 600). No shell is added. For code verification declare modified source paths in inputs; results include their raw SHA-256 and whether they stayed current during the command. Use wait_command_job/read_command_job to verify actual completion; startup is not success. At most two jobs run together. Interrupted jobs never replay automatically.",Properties=AgentRules.Props("inputs",AgentRules.Arr("Source files validated by this command, relative to workspace",16,AgentRules.Str("Input file",500)),"command",AgentRules.Str("Executable",260),"args",AgentRules.Arr("Arguments",32,AgentRules.Str("Argument",1024)),"cwd",AgentRules.Str("Relative workspace directory",500),"timeoutSeconds",new Dictionary<string,object>{{"type","integer"},{"minimum",1},{"maximum",3600}},"outputs",AgentRules.Arr("Optional output paths relative to workspace root",16,AgentRules.Str("Relative output path",500))),Required=new[]{"command"},Validate=ValidateStart,ExecuteWithContext=(c,s,call,token)=>Task.FromResult(new AgentToolReceipt{result=AgentCommandJobs.For(c.Library).Start(c,s,call)})},
                new AgentToolSpec{Name=Names[1],Title="查看后台命令",Write=false,Requires=Need.NormalLibrary,CapabilityId="agent.read_command_job",Signature="read_command_job{jobId}",Description="Read the actual current state and bounded output of a command job owned by this task. A failed exit code remains a failed job.",Properties=readProps,Required=new[]{"jobId"},Validate=ValidateId,Read=(l,s,c,t)=>Task.FromResult(AgentCommandJobs.For(l).Read(ModuleContext.Text(c.args,"jobId"),s.id))},
                new AgentToolSpec{Name=Names[2],Title="等待后台命令",Write=false,Requires=Need.NormalLibrary,CapabilityId="agent.wait_command_job",Signature="wait_command_job{jobId,waitSeconds?}",Description="Wait locally for this task's command job up to 60 seconds, then return actual status and output. This wait itself sends no model request. Check state before claiming success.",Properties=AgentRules.Props("jobId",id,"waitSeconds",new Dictionary<string,object>{{"type","integer"},{"minimum",1},{"maximum",60}}),Required=new[]{"jobId"},Validate=(a,e)=>{ValidateId(a,e);int n=a.args.ContainsKey("waitSeconds")?Convert.ToInt32(a.args["waitSeconds"]):30;if(n<1||n>60)throw new FormatException("waitSeconds must be 1-60");},Read=(l,s,c,t)=>AgentCommandJobs.For(l).Wait(ModuleContext.Text(c.args,"jobId"),s.id,c.args.ContainsKey("waitSeconds")?Convert.ToInt32(c.args["waitSeconds"]):30,t)},
                new AgentToolSpec{Name=Names[3],Title="停止后台命令",Write=true,Requires=Need.NormalLibrary,CapabilityId="agent.cancel_command_job",Signature="cancel_command_job{jobId}",Description="PROPOSE cancelling the owned command process group. Existing workspace changes are retained. No completed command is replayed.",Properties=readProps,Required=new[]{"jobId"},Validate=ValidateId,ExecuteWithContext=(c,s,call,token)=>Task.FromResult(new AgentToolReceipt{result=AgentCommandJobs.For(c.Library).Cancel(ModuleContext.Text(call.args,"jobId"),s.id)})}
            };
        }
        static void ValidateId(AgentAction action,Func<string,bool> exists){if(!AgentJournal.ValidId(ModuleContext.Text(action.args,"jobId")))throw new FormatException("jobId must be a valid job identifier");}
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using kok;

static class AgentExecutionTests {
 public static void Run(string root,Action<bool,string> check){
  string workspace=Path.Combine(root,"workspace");Directory.CreateDirectory(workspace);
  File.WriteAllText(Path.Combine(workspace,"alpha.txt"),"HELLO_WORKSPACE",new UTF8Encoding(false));
  Directory.CreateDirectory(Path.Combine(workspace,"notes"));
  var lib=new Library(Path.Combine(root,"library"));lib.SetAppMode("smart",true);
  check(!AgentExecution.Configured(lib),"execution tools stay unavailable until the user enables a workspace root");
  AgentExecution.Save(lib,true,workspace);
  check(AgentExecution.Configured(lib)&&AgentExecution.Root(lib)==workspace,"saving an enabled workspace root makes the execution environment available");
  var tools=AgentExecution.Tools();
  var list=tools.Single(t=>t.Name=="list_folder");var read=tools.Single(t=>t.Name=="read_text_file");var write=tools.Single(t=>t.Name=="write_text_file");var run=tools.Single(t=>t.Name=="run_command");
  check(AgentRules.ReadTools.Contains("list_folder")&&AgentRules.ReadTools.Contains("read_text_file")&&AgentRules.WriteTools.Contains("write_text_file")&&AgentRules.WriteTools.Contains("run_command"),"workspace reads are automatic while writes and commands stay approval-only");
  var state=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{sessionId=Guid.NewGuid().ToString("N"),goal="workspace fixture",allowed=new List<string>()}};
  var listed=list.Read(lib,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="list_folder",args=new Dictionary<string,object>{{"path",""}}},CancellationToken.None).GetAwaiter().GetResult();
  string listing=Convert.ToString(listed["text"]);
  check(listing.Contains("alpha.txt")&&listing.Contains("notes"),"list_folder lists files and folders inside the workspace root");
  var readResult=read.Read(lib,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_text_file",args=new Dictionary<string,object>{{"path","alpha.txt"}}},CancellationToken.None).GetAwaiter().GetResult();
  check(Convert.ToString(readResult["text"]).Contains("HELLO_WORKSPACE"),"read_text_file reads UTF-8 text inside the workspace");
  bool traversal=false;try{AgentRules.ValidateAction(new AgentAction{tool="read_text_file",args=new Dictionary<string,object>{{"path","../secret.txt"}}},id=>true);}catch(FormatException){traversal=true;}check(traversal,"parent traversal is rejected during tool validation before any file access");
  bool absolute=false;try{AgentRules.ValidateAction(new AgentAction{tool="write_text_file",args=new Dictionary<string,object>{{"path","C:\\Windows\\x.txt"},{"text","x"}}},id=>true);}catch(FormatException){absolute=true;}check(absolute,"absolute paths are rejected for workspace writes");
  var ai=new TranslationService(lib.DataDir);
  var context=new ModuleContext(lib,ai,new ModulePolicy(lib),null,CancellationToken.None,null,new Operations(lib,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory);
  var created=write.ExecuteWithContext(context,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="write_text_file",args=new Dictionary<string,object>{{"path","notes/beta.txt"},{"text","BETA_CONTENT"}}},CancellationToken.None).GetAwaiter().GetResult();
  check(File.Exists(Path.Combine(workspace,"notes","beta.txt"))&&File.ReadAllText(Path.Combine(workspace,"notes","beta.txt"))=="BETA_CONTENT","approved writes create UTF-8 files and parent folders only inside the workspace");
  bool noOverwrite=false;try{write.ExecuteWithContext(context,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="write_text_file",args=new Dictionary<string,object>{{"path","notes/beta.txt"},{"text","again"}}},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){noOverwrite=true;}check(noOverwrite,"existing files are not overwritten without an explicit overwrite flag");
  var runResult=run.ExecuteWithContext(context,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="run_command",args=new Dictionary<string,object>{{"command","cmd.exe"},{"args",new List<string>{" /c".Trim(),"echo","COMMAND_OK"}},{"timeoutSeconds",15}}},CancellationToken.None).GetAwaiter().GetResult();
  check(Convert.ToString(runResult.result["text"]).Contains("COMMAND_OK")&&Convert.ToInt32(runResult.result["exitCode"])==0,"approved commands run inside the workspace and return bounded output and the exit code");
  bool killed=false;try{run.ExecuteWithContext(context,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="run_command",args=new Dictionary<string,object>{{"command","cmd.exe"},{"args",new List<string>{"/c","ping","-n","6","127.0.0.1"}},{"timeoutSeconds",1}}},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException error){killed=error.Message.Contains("未完成");}check(killed,"commands that exceed their timeout are terminated");
  string binary=Path.Combine(workspace,"binary.bin");File.WriteAllBytes(binary,new byte[]{0,1,2,3});bool binaryRejected=false;try{read.Read(lib,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_text_file",args=new Dictionary<string,object>{{"path","binary.bin"}}},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){binaryRejected=true;}check(binaryRejected,"binary files are refused by the text reader");
  File.WriteAllText(Path.Combine(workspace,"large.txt"),new string('x',AgentExecution.MaxReadBytes+1));bool oversize=false;try{read.Read(lib,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_text_file",args=new Dictionary<string,object>{{"path","large.txt"}}},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){oversize=true;}check(oversize,"oversized workspace reads are rejected");
  var disabled=new Library(Path.Combine(root,"disabled-library"));disabled.SetAppMode("smart",true);bool notConfigured=false;try{AgentExecution.Resolve(disabled,"alpha.txt",false);}catch(InvalidDataException){notConfigured=true;}check(notConfigured,"workspace resolution requires an explicitly enabled root");  SandboxToolCases(Path.Combine(root,"sandbox-tools"),check);
 }
 // M2-3 codemode: nested read-tool batches for run_code (replay-cache protocol in the renderer).
 static void SandboxToolCases(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  var lib=new Library(Path.Combine(root,"library"));lib.SetAppMode("smart",true);
  var first=lib.CreateDocument("Nested one","md","","NESTED_FACT_ONE");var second=lib.CreateDocument("Nested two","md","","NESTED_FACT_TWO");lib.IndexContentSource(first.id);lib.IndexContentSource(second.id);
  var ai=new TranslationService(lib.DataDir);
  var context=new ModuleContext(lib,ai,new ModulePolicy(lib),null,CancellationToken.None,null,new Operations(lib,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory);
  var service=new AgentService(context);
  var journal=new AgentJournal(lib.DataDir);string taskId=Guid.NewGuid().ToString("N");var options=new AgentTaskOptions{sessionId=Guid.NewGuid().ToString("N"),goal="sandbox tools fixture",kernelVersion=3,allowed=new List<string>{first.id,second.id}};
  journal.Append(taskId,"task.created",new{options,protocol="tools"},options.sessionId,"fixture","created");
  var batch=AgentExecution.SandboxToolBatch(service);
  var requests=new[]{
   new Dictionary<string,object>{{"index",0},{"name","search_library"},{"args",new Dictionary<string,object>{{"query","NESTED_FACT_ONE"}}}},
   new Dictionary<string,object>{{"index",1},{"name","list_collections"},{"args",new Dictionary<string,object>()}},
   new Dictionary<string,object>{{"index",2},{"name","read_items"},{"args",new Dictionary<string,object>{{"ids",new List<string>{first.id}}}}},
   new Dictionary<string,object>{{"index",3},{"name","read_items"},{"args",new Dictionary<string,object>{{"ids",new List<string>{new string('f',32)}}}}},
   new Dictionary<string,object>{{"index",4},{"name","search_memory"},{"args",new Dictionary<string,object>{{"query","nested"}}}},
   new Dictionary<string,object>{{"index",5},{"name","recall_result"},{"args",new Dictionary<string,object>{{"callId",new string('0',32)}}}}
  };
  var results=batch(taskId,requests,CancellationToken.None).GetAwaiter().GetResult();
  string[] texts=results.Select(row=>Convert.ToString(row["text"])??"").ToArray();bool[] errors=results.Select(row=>Convert.ToBoolean(row["isError"])).ToArray();
  check(results.Count==6&&results.Select(row=>Convert.ToInt32(row["index"])).SequenceEqual(Enumerable.Range(0,6))&&!errors[0]&&!errors[1]&&!errors[2]&&texts[2].Contains("NESTED_FACT_ONE")&&errors[3]&&errors[4]&&errors[5]&&texts.All(text=>text.Length<=AgentSandbox.MaxToolResultChars),"run_code nested read tools use the task scope and return per-item isError results instead of failing the batch");
  string ticket="";bool started=false;
  Func<string,Dictionary<string,object>[],CancellationToken,System.Threading.Tasks.Task<List<Dictionary<string,object>>>> stub=(id,items,token)=>{
   return System.Threading.Tasks.Task.FromResult(items.Select(item=>{var args=item["args"] as Dictionary<string,object>??new Dictionary<string,object>();bool fail=args.ContainsKey("fail")&&Convert.ToBoolean(args["fail"]),big=args.ContainsKey("big")&&Convert.ToBoolean(args["big"]);string text=fail?"nested failure":big?new string('x',9000):"ok:"+Convert.ToString(item["index"]);return new Dictionary<string,object>{{"index",item["index"]},{"text",text},{"isError",fail}};}).ToList());
  };
  var sandbox=new AgentSandbox((name,data)=>{if(name!="agentSandboxRun")return;var row=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(data));ticket=Convert.ToString(row["ticket"]);started=true;},()=>{},stub);
  var runTask=sandbox.Run(taskId,"1+1","{}",CancellationToken.None);
  bool Throws(Action action){try{action();return false;}catch(AgentFailure){return true;}catch(InvalidDataException){return true;}catch(InvalidOperationException){return true;}}
  bool badTicket=Throws(()=>sandbox.Tools(new string('b',32),new List<Dictionary<string,object>>(),CancellationToken.None).GetAwaiter().GetResult());
  bool whitelistBlocks=true;foreach(string blocked in new[]{"write_note","run_code","mcp_call","delegate_task","read_image"})whitelistBlocks&=Throws(()=>sandbox.Tools(ticket,new List<Dictionary<string,object>>{new Dictionary<string,object>{{"index",0},{"name",blocked},{"args",new Dictionary<string,object>()}}},CancellationToken.None).GetAwaiter().GetResult());
  bool tooMany=Throws(()=>sandbox.Tools(ticket,Enumerable.Range(0,9).Select(number=>new Dictionary<string,object>{{"index",number},{"name","search_library"},{"args",new Dictionary<string,object>()}}).ToList(),CancellationToken.None).GetAwaiter().GetResult());
  bool oversizedArgs=Throws(()=>sandbox.Tools(ticket,new List<Dictionary<string,object>>{new Dictionary<string,object>{{"index",0},{"name","search_library"},{"args",new Dictionary<string,object>{{"query",new string('q',8001)}}}}},CancellationToken.None).GetAwaiter().GetResult());
  var batchOne=Enumerable.Range(0,8).Select(number=>new Dictionary<string,object>{{"index",number},{"name","search_library"},{"args",number==3?new Dictionary<string,object>{{"fail",true}}:number<2?new Dictionary<string,object>{{"big",true}}:new Dictionary<string,object>()}}).ToList();
  var firstCall=sandbox.Tools(ticket,batchOne,CancellationToken.None).GetAwaiter().GetResult();
  var firstResults=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(firstCall["results"]));
  int firstChars=firstResults.Sum(row=>Convert.ToString(row["text"]).Length);
  bool perItem=firstResults.Count==8&&Convert.ToBoolean(firstResults[3]["isError"])&&Convert.ToString(firstResults[3]["text"])=="nested failure"&&Convert.ToString(firstResults[0]["text"]).Length==AgentSandbox.MaxToolResultChars&&firstResults.Skip(4).All(row=>Convert.ToString(row["text"]).StartsWith("ok:"))&&firstChars<=AgentSandbox.MaxToolTotalChars;
  bool cumulative=batchOne.Count==8&&!Throws(()=>sandbox.Tools(ticket,batchOne,CancellationToken.None).GetAwaiter().GetResult())&&!Throws(()=>sandbox.Tools(ticket,batchOne,CancellationToken.None).GetAwaiter().GetResult())&&Throws(()=>sandbox.Tools(ticket,new List<Dictionary<string,object>>{new Dictionary<string,object>{{"index",0},{"name","search_library"},{"args",new Dictionary<string,object>()}}},CancellationToken.None).GetAwaiter().GetResult());
  sandbox.Complete(ticket,true,"DONE");runTask.GetAwaiter().GetResult();
  bool stale=Throws(()=>sandbox.Tools(ticket,new List<Dictionary<string,object>>{new Dictionary<string,object>{{"index",0},{"name","search_library"},{"args",new Dictionary<string,object>()}}},CancellationToken.None).GetAwaiter().GetResult());
  check(started&&badTicket&&whitelistBlocks&&tooMany&&oversizedArgs&&perItem&&cumulative&&stale,"AgentSandbox validates tickets, the read-only whitelist, per-batch/per-run caps, argument size, per-item errors and per-run lifetime for replay-cache tool batches");
  var agentModule=ModuleRegistry.Builtins.Modules.Single(module=>module.Name=="agent");var sandboxAction=agentModule.Actions.Single(action=>action.Name=="agentSandboxTools");
  var schemaRequest=new Dictionary<string,object>{{"index",0},{"name","search_library"},{"args",new Dictionary<string,object>()}};
  var schemaArgs=new Dictionary<string,object>{{"ticket",new string('a',32)},{"requests",new object[]{schemaRequest}}};
  bool validSchema=true;try{CapabilitySchema.Check(sandboxAction.Schema,schemaArgs);}catch{validSchema=false;}
  var badRequest=new Dictionary<string,object>{{"index",0},{"name","run_code"},{"args",new Dictionary<string,object>()}};
  var badArgs=new Dictionary<string,object>{{"ticket",new string('a',32)},{"requests",new object[]{badRequest}}};
  bool badSchema=false;try{CapabilitySchema.Check(sandboxAction.Schema,badArgs);}catch{badSchema=true;}
  string uiFile=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui","modules","agent.mjs");
  check(sandboxAction.Requires.HasValue&&sandboxAction.Requires.Value==Need.NormalLibrary&&sandboxAction.Writes==true&&sandboxAction.Schema!=null&&validSchema&&badSchema&&File.Exists(uiFile)&&File.ReadAllText(uiFile).Contains("'agentSandboxTools'"),"agentSandboxTools is registered next to agentSandboxResult with the read-only whitelist schema and listed in the WebView native action catalog");
 }}

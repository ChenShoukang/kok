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
  var disabled=new Library(Path.Combine(root,"disabled-library"));disabled.SetAppMode("smart",true);bool notConfigured=false;try{AgentExecution.Resolve(disabled,"alpha.txt",false);}catch(InvalidDataException){notConfigured=true;}check(notConfigured,"workspace resolution requires an explicitly enabled root");
 }
}

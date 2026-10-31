using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Collections.Generic;
using kok;

static class AgentReliability090Tests {
 static Dictionary<string,object> Obj(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
 static bool Reject(Action action){try{action();return false;}catch(InvalidDataException){return true;}catch(AgentFailure){return true;}}
 static AgentTaskState Seed(AgentJournal journal,string policy="each"){var s=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{sessionId=Guid.NewGuid().ToString("N"),goal="Original goal",kernelVersion=3,requestLimit=6,approvalPolicy=policy,allowed=new List<string>{new string('a',32)}}};s.Apply(journal.Append(s.id,"task.created",new{options=s.options,protocol="tools"},s.options.sessionId,"fixture","created"));return s;}
 static AgentAction Action(string tool,object args){return new AgentAction{tool=tool,args=Obj(args)};}
 static AgentCall Prepare(AgentTaskState s,AgentAction a,string turn,string provider){return new AgentCall{id=Guid.NewGuid().ToString("N"),tool=a.tool,args=a.args,turnId=turn,steering=s.steerVersion,status=AgentRules.ReadTools.Contains(a.tool)?"queued":"pending"};}
 public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
 static async Task RunAsync(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);var journal=new AgentJournal(root);string legacyId=Guid.NewGuid().ToString("N"),legacyDir=Path.Combine(root,"agent-tasks",legacyId);Directory.CreateDirectory(legacyDir);
  check(AgentCalculator.Evaluate("(1800 - 1200) / 3")=="200"&&AgentCalculator.Evaluate("0.1+0.2")=="0.3"&&AgentCalculator.Evaluate("-2 * (4+1)")=="-10","arithmetic uses exact decimal values, precedence and unary signs");
  check(Reject(()=>AgentCalculator.Evaluate("1/0"))&&Reject(()=>AgentCalculator.Evaluate("System.IO.File.ReadAllText(x)"))&&Reject(()=>AgentCalculator.Evaluate(new string('(',20)+"1"+new string(')',20))),"calculator refuses division by zero, code and excessive nesting");
  object[] old={new{sequence=1,id=Guid.NewGuid().ToString("N"),kind="task.created",created=DateTime.UtcNow.ToString("o"),data=new{options=new AgentTaskOptions{goal="Old goal",sessionId=legacyId,approvalPolicy="task"},protocol="tools"}},new{sequence=2,id=Guid.NewGuid().ToString("N"),kind="task.cancelled",created=DateTime.UtcNow.ToString("o"),data=new{}}};
  for(int i=0;i<old.Length;i++)File.WriteAllBytes(Path.Combine(legacyDir,(i+1).ToString("D5")+".sealed"),ProtectedData.Protect(Encoding.UTF8.GetBytes(Library.Json.Serialize(old[i])),null,DataProtectionScope.CurrentUser));
  byte[] original=File.ReadAllBytes(Path.Combine(legacyDir,"00001.sealed"));var legacy=AgentTaskState.Replay(legacyId,journal.Events(legacyId));
  check(legacy.cancelRequested&&legacy.options.approvalPolicy=="each"&&legacy.events.All(e=>e.version==1)&&original.SequenceEqual(File.ReadAllBytes(Path.Combine(legacyDir,"00001.sealed"))),"v0 journals migrate in memory with restrictive approval defaults while original encrypted bytes remain unchanged");
  var future=new AgentEvent{version=2,id=Guid.NewGuid().ToString("N"),sequence=1,kind="task.created",data=Obj(new{options=new AgentTaskOptions{goal="Future",sessionId=legacyId}})};
  check(Reject(()=>AgentEventContract.Upgrade(future))&&Reject(()=>AgentEventContract.Upgrade(new AgentEvent{version=1,kind="new.unknown",data=new Dictionary<string,object>()})),"future event envelopes and unknown event kinds fail closed instead of silently replaying");
  check(AgentPartialReply.Visible("{\"actions\":[{\"args\":{\"reply\":\"DO_NOT_DISPLAY\"}}],\"reply\":\"可见\\n文字\\u4f60","json")=="可见\n文字你"&&AgentPartialReply.Visible("{\"actions\":[{\"reply\":\"HIDDEN\"}]}","json")=="","interrupted JSON extracts only top-level display text and never exposes tool arguments");
  check(AgentPartialReply.Visible("{\"reply\":\"A\\u4f","json")=="A"&&AgentPartialReply.Visible("{\"reply\":\"A\\","json")=="A","split Unicode and unfinished escape sequences do not corrupt partial replies");
  var partial=Seed(journal);var arrived=new TaskCompletionSource<bool>();var stream=new AgentTaskEngine(partial,journal,()=>{},async(e,t)=>{e.Emit("request.started",new{reserve=0});e.BeginStream(Guid.NewGuid().ToString("N"),"tools");e.StreamDelta("Delivered text survives cancellation.");arrived.SetResult(true);await Task.Delay(10000,t);return null;},null,null,Prepare,()=>{});stream.Start();await arrived.Task;stream.Stop();await stream.Completion;var reopened=AgentTaskState.Replay(partial.id,journal.Events(partial.id));
  check(reopened.dialogue.Single(d=>d.interrupted).text=="Delivered text survives cancellation."&&reopened.calls.Count==0&&reopened.verification==null&&reopened.round==0,"cancellation persists delivered prose as interrupted, without calls, a completed turn or fabricated verification");
  string chinese=String.Concat(Enumerable.Repeat("中文资料预算测试",100)),english=String.Concat(Enumerable.Repeat("The source has complete evidence. ",100));
  check(AgentTokenBudget.Estimate(chinese)>chinese.Length*.8&&AgentTokenBudget.Estimate(chinese)<chinese.Length*1.6&&AgentTokenBudget.Estimate(english)<english.Length*.55,"CJK and Latin context estimates follow token density rather than counting every UTF-8 byte as a token");
  var calibration=Seed(journal);var calibrationEngine=new AgentTaskEngine(calibration,journal,()=>{},null,null,null,Prepare,()=>{});calibrationEngine.Emit("request.started",new{inputEstimate=100,inputBound=110,reserve=0});calibrationEngine.Emit("request.usage",new{inputTokens=150,outputTokens=2,estimated=false,cost=0,reserve=0});calibrationEngine.Emit("request.finished",new{outcome="success"});
  check(AgentTokenBudget.ForTask(calibration,100)>=160&&AgentTokenBudget.ForTask(calibration,100)<=170,"reported usage calibrates future estimates and remains separately visible from actual token counts");
  var grantState=Seed(journal,"task");int writes=0,models=0;bool foreground=true;Func<AgentTaskState,AgentCall,CancellationToken,Task<AgentToolReceipt>> write=(s,c,t)=>{writes++;return Task.FromResult(new AgentToolReceipt{result=Obj(new{text="Applied"})});};
  Func<AgentTaskEngine,CancellationToken,Task<AgentModelTurn>> model=(e,t)=>{e.Emit("request.started",new{reserve=0});models++;var receipts=e.State.calls.Where(c=>c.tool=="write_note"&&c.status=="done").Select(c=>c.id).ToArray();var actions=models<3?new[]{Action("write_note",new{id=new string('a',32),text="Note "+models,mode="append"})}:new[]{Action("update_plan",new{steps=new[]{new{id="write",title="Update notes",status="done",evidence=receipts}}}),Action("finish_task",new{status="completed",summary="Notes updated",checks=new[]{new{claim="Actual writes completed",evidence=receipts}}})};return Task.FromResult(new AgentModelTurn{reply=new AgentReply{reply="Fixture",actions=actions.ToList()}});};
  var engine=new AgentTaskEngine(grantState,journal,()=>{},model,null,write,Prepare,()=>{},automaticAllowed:()=>foreground);engine.Start();var proposal=grantState.calls.Single();await engine.Decide(proposal.id,true,grant:true);await engine.Completion;
  check(writes==2&&grantState.status=="completed"&&grantState.events.Count(e=>e.kind=="approval.used")==1,"explicit task permission permits a later same-tool write to the same sources and records its use");
  var later=Prepare(grantState,Action("write_note",new{id=new string('b',32),text="Outside"}),"x",null);check(AgentApproval.Find(grantState,later)==null,"task permission never expands to another source");
  foreach(string tool in new[]{"mcp_call","run_code","delegate_task","remember_memory","create_document","open_item"})check(!AgentApproval.Eligible(grantState,new AgentCall{tool=tool,args=Obj(new{id=new string('a',32)})}),"persistent/external/executable actions cannot inherit metadata approval: "+tool);
  engine.Emit("task.adjusted",new{text="Changed requirements"});check(grantState.grants.Count==0&&AgentTaskState.Replay(grantState.id,journal.Events(grantState.id)).grants.Count==0,"changing requirements revokes grants in live and replayed task state");
  var background=Seed(journal,"task");background.grants.Add(new AgentApprovalGrant{id=Guid.NewGuid().ToString("N"),tool="write_note",ids=background.options.allowed,steering=0});foreground=false;models=0;var bg=new AgentTaskEngine(background,journal,()=>{},model,null,write,Prepare,()=>{},automaticAllowed:()=>foreground);bg.Start();int before=writes;await Task.Delay(30);check(bg.IsRunning&&background.calls.Single().status=="pending"&&writes==before,"a background task remains at approval even when a matching grant exists");bg.Stop();await bg.Completion;
  var child=Seed(journal,"task");child.options.parentTaskId=Guid.NewGuid().ToString("N");check(!AgentApproval.Eligible(child,new AgentCall{tool="write_note",args=Obj(new{id=new string('a',32)})}),"child tasks cannot inherit or create parent write permissions");
 }
}

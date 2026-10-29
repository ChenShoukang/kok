using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

class AgentTaskTests {
 static AgentTaskState Seed(AgentJournal journal,string session,string goal){var state=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{sessionId=session,goal=goal}};var e=journal.Append(state.id,"task.created",new{options=state.options,protocol="tools"},session,goal,"created");state.Apply(e);return state;}
 static bool Rejects(Action a){try{a();return false;}catch(InvalidDataException){return true;}}
 public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
 static async Task RunAsync(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);var journal=new AgentJournal(root);var state=Seed(journal,Guid.NewGuid().ToString("N"),"durable fixture");int writes=0,models=0;
  Func<AgentTaskState,AgentAction,string,string,AgentCall> prepare=(s,a,t,p)=>new AgentCall{id=Guid.NewGuid().ToString("N"),turnId=t,tool=a.tool,args=a.args,status=AgentRules.ReadTools.Contains(a.tool)?"queued":"pending"};
  Func<AgentTaskEngine,CancellationToken,Task<AgentModelTurn>> model=(engine,token)=>{models++;engine.Emit("request.started",new{reserve=0});return Task.FromResult(new AgentModelTurn{reply=new AgentReply{reply="review",done=true,actions=new List<AgentAction>{new AgentAction{tool="create_document",args=new Dictionary<string,object>{{"name","fixture"},{"format","md"},{"text","x"}}}}}});};
  Func<AgentTaskState,AgentCall,CancellationToken,Task<Dictionary<string,object>>> read=(s,c,t)=>Task.FromResult(new Dictionary<string,object>{{"text","evidence"}});
  Func<AgentTaskState,AgentCall,CancellationToken,Task<AgentToolReceipt>> write=(s,c,t)=>{writes++;return Task.FromResult(new AgentToolReceipt{result=new Dictionary<string,object>{{"text","applied"}}});};
  var first=new AgentTaskEngine(state,journal,()=>{},model,read,write,prepare,()=>{});first.Start();
  check(first.IsRunning&&state.status=="waiting"&&writes==0,"native task waits for approval independently of the renderer");
  var snapshot=AgentTaskState.Replay(state.id,journal.Events(state.id));check(snapshot.calls.Count==1&&snapshot.calls[0].status=="pending"&&snapshot.requests==1,"journal replays the pending call and original request budget");
  first.Stop();await first.Completion;var reopened=AgentTaskState.Replay(state.id,journal.Events(state.id));
  var second=new AgentTaskEngine(reopened,journal,()=>{},model,read,write,prepare,()=>{});second.Start();check(writes==0&&models==1,"explicit resume does not replay writes or request a new plan while approval is pending");
  await second.Decide(reopened.calls[0].id,true);await second.Completion;check(writes==1&&reopened.status=="completed","a resumed proposal executes exactly once after approval");
  bool duplicate=false;try{await second.Decide(reopened.calls[0].id,true);}catch(InvalidOperationException){duplicate=true;}check(duplicate&&writes==1,"duplicate approval cannot repeat the write");
  var cached=Seed(journal,Guid.NewGuid().ToString("N"),"one full result");var ce=new AgentTaskEngine(cached,journal,()=>{},model,read,write,prepare,()=>{});var c1=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items"};var c2=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items"};ce.Emit("tool.proposed",new{call=c1});ce.Emit("tool.completed",new{callId=c1.id,result=new{text=new string('x',10000)}});ce.Emit("tool.proposed",new{call=c2});ce.Emit("tool.reused",new{callId=c2.id,resultRef=cached.calls[0].resultEventId});var replay=AgentTaskState.Replay(cached.id,journal.Events(cached.id));
  check(replay.calls[1].result["text"].ToString().Length==10000&&Object.ReferenceEquals(replay.calls[0].result,replay.calls[1].result)&&journal.Events(cached.id).Count(e=>e.kind=="tool.completed")==1,"full results are stored once; cached calls resolve their event reference");
  string dir=Path.Combine(root,"agent-tasks",cached.id);File.WriteAllText(Path.Combine(dir,"99999.sealed.tmp"),"partial");check(journal.Events(cached.id).Count==5,"interrupted event temp files do not corrupt recovery");
  var encrypted=File.ReadAllBytes(Path.Combine(dir,"00003.sealed"));check(!Encoding.UTF8.GetString(encrypted).Contains(new string('x',100)),"tool content is DPAPI encrypted on disk");
  string corrupt=Path.Combine(dir,"00002.sealed");File.WriteAllBytes(corrupt,new byte[]{1,2,3});check(Rejects(()=>journal.Events(cached.id))&&File.Exists(corrupt),"corrupt journals fail closed and preserve the original evidence");
  check(Rejects(()=>journal.Events("../outside")),"journal IDs cannot escape the task directory");
  var dispatchLibrary=new Library(Path.Combine(root,"dispatch"));var dispatchContext=new ModuleContext(dispatchLibrary,null,new ModulePolicy(dispatchLibrary),null,CancellationToken.None,null,null,null);var registry=new ModuleRegistry();int calls=0;var spec=new ActionSpec{Name="fixture",Requires=Need.None,Writes=false,Kind="read",Schema=CapabilitySchema.Obj(CapabilitySchema.Fields("id",CapabilitySchema.Str(32)),new[]{"id"}),Validate=a=>{if(!a.ContainsKey("id"))throw new InvalidDataException();},Execute=(h,a)=>{calls++;return Task.FromResult((object)true);}};
  registry.Register(new KokModule{Name="fixture",Kinds=new[]{"document"},Actions=new[]{spec},AgentTools=new AgentToolSpec[0]});
  check(Rejects(()=>registry.Dispatch(dispatchContext,"fixture",new Dictionary<string,object>()).GetAwaiter().GetResult())&&calls==0,"module validation runs before its action handler");await registry.Dispatch(dispatchContext,"fixture",new Dictionary<string,object>{{"id","ok"}});check(calls==1&&Rejects(()=>registry.Register(new KokModule{Name="duplicate",Actions=new[]{spec}}))&&registry.Modules.Count()==1,"duplicate registration is atomic and a registered action dispatches once");
  var library=new Library(Path.Combine(root,"library"));library.SetAppMode("smart",true);var entry=library.CreateDocument("receipt","md","","before");string operation=Guid.NewGuid().ToString("N");
  library.ApplyAgentChanges(new List<AgentChange>{new AgentChange{id=entry.id,before=new Dictionary<string,object>{{"notes",""}},after=new Dictionary<string,object>{{"notes","after"}}}},operation);
  check(new Library(library.DataDir).AgentReceipt(operation)!=null,"a committed metadata operation has a durable receipt for crash reconciliation");
  var deckArgs=new Dictionary<string,object>{{"title","Native deck"},{"slides",new[]{new{title="中文与 English",bullets=new[]{"Evidence <safe>","Second point"}}}}};var bytes=AgentSlides.Build(deckArgs);
  using(var stream=new MemoryStream(bytes)){var deck=PresentationFiles.Read(stream);check(deck.slides.Count==1&&deck.slides[0].shapes.Any(s=>s.text.Contains("Evidence <safe>")),"native Agent PPTX creation roundtrips editable Unicode text and escapes XML");}
  var custom=new AgentToolSpec{Name="fixture_probe",Requires=Need.None,Description="fixture",Signature="fixture_probe{}",Properties=new Dictionary<string,object>(),Required=new string[0],Validate=(a,exists)=>{if(a.args.Count!=0)throw new FormatException();},Read=(l,s,c,t)=>Task.FromResult(new Dictionary<string,object>{{"text","registered handler"}})};
  ModuleRegistry.Builtins.Register(new KokModule{Name="fixture-tool",Kinds=new[]{"document"},AgentTools=new[]{custom}});
  var result=await library.ReadAgentTool(state,new AgentCall{tool="fixture_probe"},CancellationToken.None);
  check(AgentRules.ReadTools.Contains("fixture_probe")&&Library.Json.Serialize(AgentRules.Schemas(false)).Contains("fixture_probe")&&result["text"].ToString()=="registered handler","a registered module tool automatically joins validation, provider schemas and native dispatch");
 }
}

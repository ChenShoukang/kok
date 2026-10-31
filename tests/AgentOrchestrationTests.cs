using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using kok;

static class AgentOrchestrationTests {
 static Dictionary<string,object> Obj(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
 static object Call(string name,object args){return new{id="call_"+Guid.NewGuid().ToString("N"),type="function",function=new{name,arguments=Library.Json.Serialize(args)}};}
 static object Finish(string status,params string[] evidence){return Call("finish_task",new{status,summary="Reviewed parallel fixture",checks=evidence.Length==0?new object[0]:new object[]{new{claim="Observed reports",evidence}}});}
 static HttpResponseMessage Reply(params object[] calls){return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Library.Json.Serialize(new{choices=new[]{new{message=new{role="assistant",content="Fixture",tool_calls=calls},finish_reason="tool_calls"}},usage=new{prompt_tokens=100,completion_tokens=40}}),Encoding.UTF8,"application/json")};}
 static string[] Receipts(Dictionary<string,object> body){return AgentTaskState.Get<List<Dictionary<string,object>>>(body,"messages").Where(m=>Convert.ToString(m["role"])=="tool").Select(m=>Regex.Match(Convert.ToString(m["content"]),@"^\[kok_call_id=([a-f0-9]{32}) status=done\]").Groups[1].Value).Where(id=>id!="").ToArray();}
 sealed class Model:HttpMessageHandler {public Func<Dictionary<string,object>,HttpResponseMessage> run;protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){var body=Library.Json.Deserialize<Dictionary<string,object>>(await request.Content.ReadAsStringAsync());return run(body);}}
 sealed class Fixture {
  public Library lib;public AgentService service;public Model model;public string id;
  public Fixture(string root){lib=new Library(root);lib.SetAppMode("smart",true);model=new Model();var ai=new TranslationService(root,()=>model);ai.SaveConfig(new Dictionary<string,object>{{"endpoint","http://127.0.0.1:17321/v1"},{"model","fixture"},{"clearKey",true}});service=new AgentService(new ModuleContext(lib,ai,new ModulePolicy(lib),null,CancellationToken.None,null,new Operations(lib,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory));}
 }
 static async Task<Dictionary<string,object>> Read(Fixture f,string id){return Obj(await f.service.HandleAgentTaskAction("agentTaskRead",new Dictionary<string,object>{{"taskId",id}}));}
 static async Task<Dictionary<string,object>> Wait(Fixture f,string id,bool pending=false){for(int i=0;i<1500;i++){var snap=await Read(f,id);if(!Convert.ToBoolean(snap["running"])||pending&&AgentTaskState.Get<List<AgentCall>>(snap,"calls").Any(c=>c.status=="pending"))return snap;await Task.Delay(10);}f.service.Stop();throw new Exception("Orchestration fixture did not settle");}
 public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
 static async Task RunAsync(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  var f=new Fixture(Path.Combine(root,"parallel"));var source=f.lib.CreateDocument("ORCH_SOURCE","md","","PARALLEL FACT 42").id;
  int active=0,maxActive=0,childReads=0;
  f.model.run=body=>{
   var messages=AgentTaskState.Get<List<Dictionary<string,object>>>(body,"messages");string json=Library.Json.Serialize(body);
   bool parent=json.Contains("\"name\":\"delegate_parallel\"");
   if(parent){
    bool report=messages.Any(m=>Convert.ToString(m["role"])=="tool"&&Convert.ToString(m["content"]).Contains("PARALLEL SUBTASK REPORTS"));
    return report?Reply(Finish("completed",Receipts(body))):Reply(Call("delegate_parallel",new{goals=new[]{"CHILD_ONE","CHILD_TWO"},ids=new[]{source},requests=2}));
   }
   int now=Interlocked.Increment(ref active);maxActive=Math.Max(maxActive,now);Thread.Sleep(40);Interlocked.Decrement(ref active);
   var receipts=Receipts(body);if(receipts.Length==0){Interlocked.Increment(ref childReads);return Reply(Call("read_items",new{ids=new[]{source}}));}
   return Reply(Finish("completed",receipts));
  };
  check(AgentRules.Tools.Any(t=>t.Name=="delegate_parallel"&&t.Write)&&AgentAdvancedTools.Names.Contains("delegate_parallel"),"delegate_parallel is registered as an approval-gated advanced tool");
  bool rejected=false;try{AgentRules.ValidateAction(new AgentAction{tool="delegate_parallel",args=new Dictionary<string,object>{{"goals",new List<string>{"only one"}},{"ids",new List<string>{source}}}},id=>id==source);}catch(FormatException){rejected=true;}check(rejected,"a parallel delegation proposal needs 2-3 subtasks before it can be approved");
  var started=Obj(await f.service.HandleAgentTaskAction("agentTaskStart",Obj(new{sessionId=Guid.NewGuid().ToString("N"),goal="Orchestrate two independent reads",scope=new{mode="selected",ids=new[]{source}},allowDelegation=true,requestLimit=8})));
  string taskId=Convert.ToString(started["id"]);
  var waiting=await Wait(f,taskId,true);var card=AgentTaskState.Get<List<AgentCall>>(waiting,"calls").Single();check(card.tool=="delegate_parallel"&&childReads==0,"parallel subagents spend no requests before the delegated card is approved");
  await f.service.HandleAgentTaskAction("agentTaskDecide",Obj(new{taskId,callId=card.id,approve=true}));
  var done=await Wait(f,taskId);
  var eventsRaw=await f.service.HandleAgentTaskAction("agentTaskEvents",new Dictionary<string,object>{{"taskId",taskId}});var events=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(eventsRaw));
  var startedEvents=events.Where(e=>Convert.ToString(e["kind"])=="subtask.started").ToList();var childIds=new List<string>();
  foreach(var startedEvent in startedEvents){var detail=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(await f.service.HandleAgentTaskAction("agentTaskEvents",new Dictionary<string,object>{{"taskId",taskId},{"eventId",Convert.ToString(startedEvent["id"])}})));var data=AgentTaskState.Get<Dictionary<string,object>>(detail,"data");childIds.Add(Convert.ToString(data["taskId"]));}
  int completedChildren=0;foreach(var childId in childIds){var child=await Read(f,childId);if(Convert.ToString(child["status"])=="completed")completedChildren++;}
  int requests=Convert.ToInt32(AgentTaskState.Get<Dictionary<string,object>>(done,"usage")["requests"]);
  check(Convert.ToString(done["status"])=="completed"&&completedChildren==2&&childIds.Count==2,"one approved parallel card starts two read-only children and the parent reviews their reports (status="+Convert.ToString(done["status"])+", children="+childIds.Count+", completed="+completedChildren+", reads="+childReads+", active="+maxActive+", requests="+requests+")");
  check(childReads>=2&&maxActive>=2,"parallel children actually run concurrently instead of serially");
  check(requests<=8,"parallel children share the parent request budget");
  await f.service.HandleAgentTaskAction("agentTaskDelete",Obj(new{taskId}));
 }
}

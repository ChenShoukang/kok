using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using kok;
class AgentTests {
 // Replays one fixed provider response and keeps the request body; no socket is opened.
 sealed class FixedProvider:HttpMessageHandler {
  public string body="",type="application/json",request="";public HttpStatusCode status=HttpStatusCode.OK;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message,CancellationToken token){request=await message.Content.ReadAsStringAsync();return new HttpResponseMessage(status){Content=new StringContent(body,Encoding.UTF8,type)};}
 }
 static bool Rejects(Action action){try{action();return false;}catch(InvalidDataException){return true;}catch(InvalidOperationException){return true;}}
 public static void Run(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);var library=new Library(Path.Combine(root,"data"));
  Entry note=library.CreateDocument("Agent note","md","","# Agent note\n\nOrder over space."),other=library.CreateDocument("Second note","md","","Body");
  Func<string,bool> exists=library.AgentHas;string id=note.id;
  var reply=AgentRules.Parse("```json\n{\"reply\":\"Found it\",\"actions\":[{\"tool\":\"read_items\",\"args\":{\"ids\":[\""+id+"\"]},\"why\":\"read\"}],\"done\":false}\n```",exists);
  check(reply.reply=="Found it"&&reply.actions.Count==1&&!reply.done,"agent replies accept fenced JSON with a validated read action");
  check(AgentRules.Parse("{\"reply\":\"Done\"}",exists).done,"a reply without actions always ends the task instead of looping");
  check(AgentRules.Parse("{\"reply\":\"Tag\",\"actions\":[{\"tool\":\"add_tags\",\"args\":{\"ids\":[\""+id+"\",\""+other.id+"\"],\"tags\":[\"design\"]}}],\"done\":true}",exists).actions[0].tool=="add_tags","write tools are parsed as proposals with existing IDs");
  check(Rejects(()=>AgentRules.Parse("{\"reply\":\"x\",\"actions\":[{\"tool\":\"delete_file\",\"args\":{\"id\":\""+id+"\"}}]}",exists)),"unknown tools such as deletion are rejected");
  check(Rejects(()=>AgentRules.Parse("{\"reply\":\"x\",\"actions\":[{\"tool\":\"open_item\",\"args\":{\"id\":\""+new string('a',32)+"\"}}]}",exists)),"invented file IDs are rejected before any action");
  check(Rejects(()=>AgentRules.Parse("{\"reply\":\"x\",\"actions\":["+String.Join(",",Enumerable.Repeat("{\"tool\":\"list_collections\",\"args\":{}}",5))+"]}",exists)),"more than four actions in one round is rejected");
  check(Rejects(()=>AgentRules.Parse("not json",exists))&&Rejects(()=>AgentRules.Parse("{\"reply\":\"\"}",exists)),"invalid or empty agent output never executes");
  check(Rejects(()=>AgentRules.Parse("{\"reply\":\"x\",\"actions\":[{\"tool\":\"create_document\",\"args\":{\"name\":\"../escape\",\"format\":\"md\",\"text\":\"x\"}}]}",exists)),"document names cannot escape the managed folder");
  var readSchemas=Library.Json.Serialize(AgentRules.Schemas(false));
  check(readSchemas.Contains("read_items")&&!readSchemas.Contains("write_note")&&AgentRules.Schemas(true,null,true).Length==AgentRules.Tools.Length&&!readSchemas.Contains("mcp_call"),"discussion requests offer only read tools; connected assist requests offer every registered tool");
  check(AgentRules.ReadTools.SequenceEqual(ModuleRegistry.Builtins.Tools.Where(t=>!t.Write).Select(t=>t.Name))&&AgentRules.ReadTools.Contains("read_clips")&&AgentRules.Protocol(false).Contains("read_items{ids[<=8],question?}"),"the registry drives both tool groups and the JSON fallback protocol");
  AgentReply invalidReply;string problem;
  check(!AgentRules.TryParse("{\"reply\":\"x\",\"actions\":[{\"tool\":\"open_item\",\"args\":{\"id\":\""+new string('a',32)+"\"}}]}",exists,out invalidReply,out problem)&&problem.Contains("action 1 (open_item)")&&problem.Contains("unknown or out-of-scope file ID"),"invalid replies name the action and argument for a repair round");
  var provider=new FixedProvider();var service=new TranslationService(Path.Combine(root,"ai"),()=>provider);
  service.SaveConfig(new Dictionary<string,object>{{"endpoint","http://127.0.0.1:9/v1"},{"model","fixture"},{"language","English"}});
  var messages=new List<object>{new{role="system",content="rules"},new{role="user",content="goal"}};
  Func<object> ask=()=>service.AgentToolChat(messages,AgentRules.Schemas(true),exists,CancellationToken.None,d=>{},u=>{},b=>{},900).GetAwaiter().GetResult();
  Func<string,string> chunk=json=>"data: "+json+"\n\n";
  provider.type="text/event-stream";
  provider.body=chunk("{\"choices\":[{\"delta\":{\"content\":\"Reading\",\"tool_calls\":[{\"index\":0,\"id\":\"call_1\",\"function\":{\"name\":\"read_items\",\"arguments\":\"{\\\"ids\\\":[\\\"\"}}]}}]}")+chunk("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\""+id+"\\\"],\\\"why\\\":\\\"evidence\\\"}\"}}]},\"finish_reason\":\"tool_calls\"}]}")+"data: [DONE]\n\n";
  var turn=(AgentModelTurn)ask();
  check(turn.reply.reply=="Reading"&&turn.reply.actions.Count==1&&turn.reply.actions[0].tool=="read_items"&&turn.reply.actions[0].why=="evidence"&&!turn.reply.actions[0].args.ContainsKey("why")&&turn.providerIds[0]=="call_1"&&!turn.reply.done,"streamed native tool-call fragments are joined, validated and keep the provider call ID");
  check(provider.request.Contains("\"tools\"")&&provider.request.Contains("\"tool_choice\":\"auto\""),"native requests carry the tool schema");
  provider.type="application/json";provider.body="{\"choices\":[{\"message\":{\"content\":\"\",\"tool_calls\":[{\"id\":\"call_2\",\"type\":\"function\",\"function\":{\"name\":\"delete_file\",\"arguments\":\"{}\"}}]},\"finish_reason\":\"tool_calls\"}]}";
  AgentRepair repair=null;try{ask();}catch(AgentRepair e){repair=e;}
  check(repair!=null&&repair.messages.Count==2&&Library.Json.Serialize(repair.messages[1]).Contains("Error: unknown tool"),"an invalid native call becomes a repair round that answers the call with its error");
  provider.status=HttpStatusCode.BadRequest;provider.body="{\"error\":{\"code\":\"unsupported_parameter\",\"param\":\"tools\"}}";bool unsupported=false;try{ask();}catch(AgentToolsUnsupported){unsupported=true;}
  check(unsupported,"a provider that rejects tools signals the JSON fallback instead of failing the task");
  check(Rejects(()=>AgentRules.Parse("{\"reply\":\"x\",\"actions\":[{\"tool\":\"create_presentation\",\"args\":{\"title\":\"Deck\",\"slides\":[]}}]}",exists)),"presentations need at least one slide");
  library.Update(id,new Dictionary<string,object>{{"tags",new List<string>{"design"," design ","plan"}}});
  check(library.Find(id).tags.SequenceEqual(new[]{"design","plan"}),"tag patches are trimmed, de-duplicated and reversible through update");
  check(Rejects(()=>library.AgentCatalog(new List<string>(),10)),"the agent catalog is unavailable outside Smart mode");
  library.SetAppMode("smart",true);string catalog=library.AgentCatalog(new List<string>{id},10);
  check(catalog.Contains(id)&&catalog.Contains("Agent note")&&!catalog.Contains(Path.GetDirectoryName(note.path)),"the agent catalog lists IDs and names without local paths");
  var scope=new AgentScope{mode="selected",ids=new List<string>{id}};
  var scoped=library.ResolveAgentScope(scope);
  check(scoped.SequenceEqual(new[]{id})&&!library.AgentScopedCatalog(scoped,80).Contains(other.id),"selected agent scope excludes unrelated IDs and names");
  check(Rejects(()=>AgentRules.Parse("{\"reply\":\"x\",\"actions\":[{\"tool\":\"read_items\",\"args\":{\"ids\":[\""+other.id+"\"]}}]}",x=>scoped.Contains(x))),"a valid library ID outside the scope is rejected");
  check(Rejects(()=>library.ResolveAgentScope(new AgentScope{mode="selected"})),"an empty selected scope never falls back to all files");
  library.Update(id,new Dictionary<string,object>{{"notes","before"}});
  var changes=new List<AgentChange>{new AgentChange{id=id,before=new Dictionary<string,object>{{"notes","before"}},after=new Dictionary<string,object>{{"notes","agent"}}}};
  library.ApplyAgentChanges(changes);library.Update(id,new Dictionary<string,object>{{"notes","agent plus manual"}});
  var undo=new List<AgentChange>{new AgentChange{id=id,before=changes[0].after,after=changes[0].before}};
  check(Rejects(()=>library.ApplyAgentChanges(undo))&&library.Find(id).notes=="agent plus manual","undo preserves manual edits instead of overwriting them");
  var batch=new List<AgentChange>{new AgentChange{id=other.id,before=new Dictionary<string,object>{{"collection",""}},after=new Dictionary<string,object>{{"collection","Moved"}}},undo[0]};
  check(Rejects(()=>library.ApplyAgentChanges(batch))&&library.Find(other.id).collection=="","a conflict prevents every entry in a metadata batch from being changed");
  string stamp=library.AgentCreatedStamp(id);File.AppendAllText(note.path,"manual file edit");
  check(Rejects(()=>library.UndoAgentCreated(id,stamp))&&library.Find(id)!=null,"undo of a created document preserves later on-disk edits");
  string session="[{\"id\":\""+Guid.NewGuid().ToString("N")+"\",\"draft\":\"LOCAL_PRIVATE_DRAFT\"}]";
  library.SaveAgentSessions(session);var reopened=new Library(library.DataDir);
  check(reopened.ReadAgentSessions()==session&&!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(library.DataDir,"agent-sessions.sealed"))).Contains("LOCAL_PRIVATE_DRAFT"),"agent sessions survive restart and are encrypted for the Windows account");
  library.SetAppMode("pure",false);check(library.ReadAgentSessions()==session&&Rejects(()=>library.ResolveAgentScope(scope)),"Pure mode allows offline session recovery but forbids agent content requests");
 }
}

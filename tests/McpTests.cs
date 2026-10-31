using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

static class McpTests {
 static Dictionary<string,object> Args(string text){return new Dictionary<string,object>{{"text",text}};}
 static string Node(){return Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator).Select(p=>Path.Combine(p,"node.exe")).First(File.Exists);}
 static string Fixture {get{return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..","tests","mcp-fixture.cjs"));}}
 public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
 static async Task RunAsync(string root,Action<bool,string> check){
  var lib=new Library(root);lib.SetAppMode("smart",true);var service=new McpService(lib,()=>{});string marker="synthetic-mcp-env-"+Guid.NewGuid().ToString("N");
  string id=service.Configure(new McpServerConfig{name="fixture",executable=Node(),args=new List<string>{Fixture,"modern"},trusted=true,environment=new Dictionary<string,string>{{"KOK_MCP_ALLOWED",marker}}});
  check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root,"mcp-servers.sealed"))).Contains(marker),"MCP arguments and environment are DPAPI encrypted on disk");
  Environment.SetEnvironmentVariable("KOK_MCP_SECRET_CANARY","not-inherited");var previousInputEncoding=Console.InputEncoding;Console.InputEncoding=new System.Text.UTF8Encoding(true);
  try{
   await service.Connect(id,CancellationToken.None);check(Console.InputEncoding.GetPreamble().Length==3,"MCP startup leaves UTF-8 console encoding unchanged and sends BOM-free protocol bytes");var state=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(service.Snapshot()))[0];var tools=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(state["tools"]));
   check(Convert.ToString(state["protocol"])=="2026-07-28"&&!service.Available&&tools.Any(t=>Convert.ToString(t["name"])=="complex"&&!Convert.ToBoolean(t["supported"])),"modern MCP discovers tools but none are exposed before explicit approval; unsupported refs stay blocked");
   bool blocked=false;try{service.Stamp(id,"echo",Args("test"));}catch(AgentFailure){blocked=true;}check(blocked,"an unapproved MCP tool cannot even become an execution proposal");
   var echo=tools.Single(t=>Convert.ToString(t["name"])=="echo");service.Approve(id,new Dictionary<string,string>{{"echo",Convert.ToString(echo["stamp"])}});string unicode="你好，echo 🌱 \"引号\"\\目录";string stamp=service.Stamp(id,"echo",Args(unicode));
   var call=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="mcp_call",args=new Dictionary<string,object>{{"serverId",id},{"tool","echo"},{"arguments",Args(unicode)}},externalStamp=stamp};var result=await service.Call(call,CancellationToken.None);
   string text=Convert.ToString(result.result["text"]);var payload=Library.Json.Deserialize<Dictionary<string,object>>(text.Substring(text.IndexOf('\n')+1));check(Convert.ToString(payload["echo"])==unicode&&text.Contains(marker)&&text.Contains("\"inherited\":false"),"approved stdio calls roundtrip Unicode-safe JSON and only explicit environment secrets reach the server");
   blocked=false;try{service.Stamp(id,"echo",new Dictionary<string,object>{{"unknown","x"}});}catch(AgentFailure){blocked=true;}check(blocked,"MCP input schemas reject missing and unknown arguments before execution");
   call.externalStamp="stale";blocked=false;try{await service.Call(call,CancellationToken.None);}catch(AgentFailure f){blocked=f.kind=="mcp_stale";}check(blocked,"external calls reject approval receipts from a different tool definition");
   service.Dispose();using(var restored=new McpService(lib,()=>{}))check(!restored.Available&&Library.Json.Serialize(restored.Snapshot()).Contains("\"connected\":false")&&!Library.Json.Serialize(restored.Snapshot()).Contains(marker),"restart preserves encrypted MCP configuration without automatically launching servers or exposing environment values");
   foreach(string mode in new[]{"legacy","legacy-silent","legacy-callback","changed","interactive","oversize","error","metadata-error"}){
    using(var clientService=new McpService(lib,()=>{})){
     string server=clientService.Configure(new McpServerConfig{name=mode,executable=Node(),args=new List<string>{Fixture,mode},trusted=true});await clientService.Connect(server,CancellationToken.None);
     var metadata=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(clientService.Snapshot())).Single(s=>Convert.ToString(s["id"])==server);var tool=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(metadata["tools"])).Single(t=>Convert.ToString(t["name"])=="echo");clientService.Approve(server,new Dictionary<string,string>{{"echo",Convert.ToString(tool["stamp"])}});
     var request=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="mcp_call",args=new Dictionary<string,object>{{"serverId",server},{"tool","echo"},{"arguments",Args("fixture")}},externalStamp=clientService.Stamp(server,"echo",Args("fixture"))};
     if(mode.StartsWith("legacy")){var reply=await clientService.Call(request,CancellationToken.None);check(Convert.ToString(metadata["protocol"])=="2025-11-25"&&(mode!="legacy-callback"||Convert.ToString(reply.result["text"]).Contains("\"denied\":1")),"legacy MCP handshake works and ungranted server sampling is refused: "+mode);}
     else{blocked=false;try{await clientService.Call(request,CancellationToken.None);}catch(AgentFailure f){blocked=f.kind==(mode=="changed"?"mcp_stale":mode=="interactive"?"mcp_interaction":mode=="error"?"external_tool":mode=="metadata-error"?"mcp_definition":"uncertain_external");if(mode=="error")blocked=blocked&&f is AgentToolFailure&&Convert.ToString(((AgentToolFailure)f).result["text"]).Contains("FIXTURE_TOOL_ERROR");}check(blocked,"MCP blocks changed schemas, ungranted interaction and unbounded output without replay: "+mode);}
    }
   }
   using(var client=new McpStdioClient(Node(),new List<string>{Fixture,"wrong-version"},new Dictionary<string,string>())){blocked=false;try{await client.Initialize(CancellationToken.None);}catch(AgentFailure f){blocked=f.kind=="mcp_version";}check(blocked,"a recognized modern MCP version refusal never downgrades to a legacy handshake");}
   using(var client=new McpStdioClient(Node(),new List<string>{Fixture,"hang"},new Dictionary<string,string>())){await client.Initialize(CancellationToken.None);using(var cancel=new CancellationTokenSource(100)){bool cancelled=false;try{await client.Request("tools/call",new Dictionary<string,object>{{"name","echo"},{"arguments",Args("x")}},cancel.Token);}catch(OperationCanceledException){cancelled=true;}check(cancelled&&!client.Connected,"MCP cancellation closes its owned child and cannot leave a hidden call running");}}
   int worker=0;using(var client=new McpStdioClient(Node(),new List<string>{Fixture,"tree"},new Dictionary<string,string>())){await client.Initialize(CancellationToken.None);var catalog=await client.Request("tools/list",new Dictionary<string,object>(),CancellationToken.None);worker=Convert.ToInt32(catalog["workerPid"]);check(worker>0,"a synthetic MCP server can launch a tracked worker for lifecycle verification");}
   bool exited=false;for(int n=0;n<30&&!exited;n++){try{using(var child=System.Diagnostics.Process.GetProcessById(worker))exited=child.HasExited;}catch(ArgumentException){exited=true;}if(!exited)await Task.Delay(50);}check(exited,"closing MCP terminates its worker process tree, not only the parent launcher");
   lib.SetAppMode("pure",false);blocked=false;try{await service.Connect(id,CancellationToken.None);}catch(InvalidOperationException){blocked=true;}check(blocked,"Pure mode blocks MCP process creation and external tool use");
   check(AgentRules.WriteTools.Contains("mcp_call")&&!AgentRules.ReadTools.Contains("mcp_call")&&!Library.Json.Serialize(AgentRules.Schemas(false,lib,true)).Contains("\"name\":\"mcp_call\""),"external MCP calls always require human approval, even when servers claim read-only behavior");
  }finally{service.Dispose();Environment.SetEnvironmentVariable("KOK_MCP_SECRET_CANARY",null);Console.InputEncoding=previousInputEncoding;}
 }
}

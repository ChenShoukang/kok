using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

// Portable-runtime MCP sandbox gate: a copied Node runtime in a user-writable directory is granted
// read/execute access to the AppContainer package, launched through the trusted helper, and must
// complete the standard handshake and an approved tool call.
static class McpSandboxTests {
 static string Node() {
  string path = Environment.GetEnvironmentVariable("PATH") ?? "";
  foreach (var directory in path.Split(Path.PathSeparator)) {
   string candidate = Path.Combine(directory.Trim(), "node.exe");
   if (File.Exists(candidate)) return candidate;
  }
  throw new InvalidOperationException("node.exe is not available on PATH for the portable sandbox fixture.");
 }
 static string Fixture { get { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "tests", "mcp-fixture.cjs")); } }
 public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
 static async Task RunAsync(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  bool supported=OsSandbox.Supported;
  check(supported,"AppContainer is available for portable-runtime MCP sandboxes ("+OsSandbox.Status+")");
  if(!supported)return;
  string runtime=Path.Combine(root,"runtime");Directory.CreateDirectory(runtime);
  string node=Path.Combine(runtime,"node.exe"),fixture=Path.Combine(runtime,"fixture.cjs");
  File.Copy(Node(),node,true);File.Copy(Fixture,fixture,true);
  var library=new Library(Path.Combine(root,"library"));library.SetAppMode("smart",true);
  var service=new McpService(library,()=>{});
  try{
   string id=service.Configure(new McpServerConfig{name="portable node",executable=node,args=new List<string>{fixture,"modern"},environment=new Dictionary<string,string>(),trusted=true,sandbox=true,sandboxDirectory=runtime});
   var state=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(service.Snapshot()))[0];
   check(Convert.ToBoolean(state["sandbox"])&&Convert.ToString(state["sandboxDirectory"])==runtime,"a portable runtime directory is stored with the per-server AppContainer sandbox setting");
   await service.Connect(id,CancellationToken.None);
   var connected=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(service.Snapshot()))[0];
   var tools=AgentTaskState.Get<List<Dictionary<string,object>>>(connected,"tools");
   check(Convert.ToBoolean(connected["connected"])&&Convert.ToString(connected["protocol"])=="2026-07-28"&&tools!=null&&tools.Any(t=>Convert.ToString(t["name"])=="echo"),"a copied portable Node runtime starts inside AppContainer and completes the MCP handshake plus catalog");
   var echo=tools.Single(t=>Convert.ToString(t["name"])=="echo");
   service.Approve(id,new Dictionary<string,string>{{"echo",Convert.ToString(echo["stamp"])}});
   string stamp=service.Stamp(id,"echo",new Dictionary<string,object>{{"text","portable sandboxed"}});
   var receipt=await service.Call(new AgentCall{id=Guid.NewGuid().ToString("N"),tool="mcp_call",args=new Dictionary<string,object>{{"serverId",id},{"tool","echo"},{"arguments",new Dictionary<string,object>{{"text","portable sandboxed"}}}},externalStamp=stamp},CancellationToken.None);
   check(Convert.ToString(receipt.result["text"]).Contains("portable sandboxed")&&Convert.ToString(receipt.result["text"]).Contains("[UNTRUSTED EXTERNAL TOOL RESULT"),"an approved external call round-trips through the sandboxed portable runtime");
   bool rejected=false;try{service.Configure(new McpServerConfig{name="bad runtime",executable=node,args=new List<string>{fixture,"modern"},environment=new Dictionary<string,string>(),trusted=true,sandbox=true,sandboxDirectory=Path.Combine(root,"missing-runtime")});}catch(AgentFailure error){rejected=error.kind=="mcp_sandbox";}check(rejected,"a missing portable runtime directory is rejected before any process launch");
  } finally {service.Dispose();}
 }
}

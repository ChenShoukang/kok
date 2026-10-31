using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

static class McpSandboxTests {
 static string Fixture { get { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "tests", "mcp-sandbox-fixture.ps1")); } }
 static string PowerShell { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"); } }
 public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
 static async Task RunAsync(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  bool supported=OsSandbox.Supported;
  check(supported,"AppContainer is available for sandboxed local MCP servers ("+OsSandbox.Status+")");
  if(!supported)return;
  check(File.Exists(Fixture)&&File.Exists(PowerShell),"the sandbox MCP fixture and Windows PowerShell are available");
  var library=new Library(Path.Combine(root,"library"));library.SetAppMode("smart",true);
  var service=new McpService(library,()=>{});
  try{
   string id=service.Configure(new McpServerConfig{name="sandboxed fixture",executable=PowerShell,args=new List<string>{"-NoLogo","-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",Fixture},environment=new Dictionary<string,string>(),trusted=true,sandbox=true});
   var state=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(service.Snapshot()))[0];
   check(Convert.ToBoolean(state["sandbox"])&&Convert.ToBoolean(state["sandboxSupported"]),"MCP configuration shows the per-server AppContainer sandbox toggle and its OS support");
   await service.Connect(id,CancellationToken.None);
   var connected=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(service.Snapshot()))[0];
   var tools=AgentTaskState.Get<List<Dictionary<string,object>>>(connected,"tools");
   check(Convert.ToBoolean(connected["connected"])&&tools!=null&&tools.Any(t=>Convert.ToString(t["name"])=="echo"),"a local MCP server runs inside the AppContainer sandbox and still completes the standard handshake and catalog");
   var echo=tools.Single(t=>Convert.ToString(t["name"])=="echo");
   service.Approve(id,new Dictionary<string,string>{{"echo",Convert.ToString(echo["stamp"])}});
   string stamp=service.Stamp(id,"echo",new Dictionary<string,object>{{"text","sandboxed"}});
   var receipt=await service.Call(new AgentCall{id=Guid.NewGuid().ToString("N"),tool="mcp_call",args=new Dictionary<string,object>{{"serverId",id},{"tool","echo"},{"arguments",new Dictionary<string,object>{{"text","sandboxed"}}}},externalStamp=stamp},CancellationToken.None);
   check(Convert.ToString(receipt.result["text"]).Contains("SANDBOX-ECHO:sandboxed"),"an approved tool call round-trips through the sandboxed MCP process");
  } finally {service.Dispose();}
 }
}

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;

namespace kok {
 public sealed class McpRpcException:Exception {public readonly int code;public readonly Dictionary<string,object> data;public McpRpcException(int code,Dictionary<string,object> data):base("MCP RPC error "+code){this.code=code;this.data=data;}}
 public interface IMcpClient:IDisposable {
  bool Connected{get;}string Protocol{get;}bool CatalogChanged{get;set;}
  Task Initialize(CancellationToken token);
  Task<Dictionary<string,object>> Request(string method,Dictionary<string,object> args,CancellationToken token,int timeoutMs=15000,bool metadata=true,bool closeOnTimeout=true);
 }
 public sealed class McpStdioClient:IDisposable,IMcpClient {
  public const string ModernVersion="2026-07-28";
  readonly Process process;readonly SemaphoreSlim writer=new SemaphoreSlim(1,1);readonly object gate=new object();readonly Dictionary<string,TaskCompletionSource<Dictionary<string,object>>> pending=new Dictionary<string,TaskCompletionSource<Dictionary<string,object>>>();
  bool closed;int serial;McpPipeProcess pipes;StreamWriter input;string sandboxSpec;public string Protocol {get;private set;}public volatile bool CatalogChanged;bool IMcpClient.CatalogChanged{get{return CatalogChanged;}set{CatalogChanged=value;}}
  public int ProcessId {get{return process.Id;}}public bool Connected {get{try{return !closed&&!process.HasExited;}catch(InvalidOperationException){return false;}}}
  public McpStdioClient(string executable,List<string> args,Dictionary<string,string> environment,bool sandbox=false,string sandboxDirectory=""){
   if(!Path.IsPathRooted(executable)||executable.StartsWith(@"\\",StringComparison.Ordinal)||!File.Exists(executable)||!Path.GetExtension(executable).Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new AgentFailure("mcp_config","请选择已安装的本地 MCP 可执行文件。");
   if(args==null||args.Count>40||args.Any(a=>a==null||a.Length>4000||a.IndexOf('\0')>=0)||environment==null||environment.Count>40||environment.Any(p=>String.IsNullOrWhiteSpace(p.Key)||p.Key.Length>100||p.Key.Contains("=")||p.Value==null||p.Value.Length>8000||p.Value.IndexOf('\0')>=0))throw new AgentFailure("mcp_config","MCP 启动参数无效。");
   var start=new ProcessStartInfo{FileName=Path.GetFullPath(executable),Arguments=String.Join(" ",args.Select(Quote)),UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=new UTF8Encoding(false,true),StandardErrorEncoding=new UTF8Encoding(false,true),WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(executable))};
   // Never inherit API keys/tokens from kok's process environment.
   start.EnvironmentVariables.Clear();foreach(var name in new[]{"SystemRoot","WINDIR","PATH","TEMP","TMP","USERPROFILE","APPDATA","LOCALAPPDATA","PROGRAMDATA","COMSPEC","PATHEXT"}){string value=Environment.GetEnvironmentVariable(name);if(value!=null)start.EnvironmentVariables[name]=value;}foreach(var pair in environment)start.EnvironmentVariables[pair.Key]=pair.Value;start.EnvironmentVariables["PYTHONIOENCODING"]="utf-8";
   if(sandbox){
    if(!OsSandbox.Supported)throw new AgentFailure("mcp_sandbox","AppContainer 沙箱不可用："+OsSandbox.Status);
    string runtimeDirectory=String.IsNullOrWhiteSpace(sandboxDirectory)?Path.GetDirectoryName(Path.GetFullPath(executable)):Path.GetFullPath(sandboxDirectory);
    try{OsSandbox.GrantReadTree(runtimeDirectory);}catch(Exception error){throw new AgentFailure("mcp_sandbox","无法授权便携运行时目录："+error.Message+" 请把运行时放在用户可写目录，或关闭该服务的沙箱。");}
    try{OsSandbox.GrantFile(Path.GetFullPath(executable));}catch(UnauthorizedAccessException){}catch(InvalidDataException){}
    foreach(var argument in args)try{if(argument!=null&&argument.Length>0&&argument.Length<1024&&argument.IndexOf('\0')<0&&Path.IsPathRooted(argument)&&File.Exists(argument))OsSandbox.GrantFile(argument);}catch(UnauthorizedAccessException){}catch(InvalidDataException){}
    var sandboxArgs=new List<string>(args??new List<string>());
    if(Path.GetFileName(executable).Equals("node.exe",StringComparison.OrdinalIgnoreCase)&&!sandboxArgs.Any(a=>String.Equals(a,"--preserve-symlinks-main",StringComparison.OrdinalIgnoreCase))){sandboxArgs.Insert(0,"--preserve-symlinks-main");sandboxArgs.Insert(1,"--preserve-symlinks");}
    sandboxSpec=OsSandbox.WriteSpec(Path.GetFullPath(executable),sandboxArgs,start.WorkingDirectory);
    var helper=new ProcessStartInfo{FileName=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"kok.exe"),Arguments="--sandbox-exec "+Quote(sandboxSpec),WorkingDirectory=start.WorkingDirectory,UseShellExecute=false,CreateNoWindow=true};
    helper.EnvironmentVariables.Clear();foreach(System.Collections.DictionaryEntry pair in start.EnvironmentVariables)helper.EnvironmentVariables[Convert.ToString(pair.Key)]=Convert.ToString(pair.Value);
    start=helper;
   }
   try{pipes=new McpPipeProcess(start);process=pipes.Process;input=pipes.Input;Task.Run((Func<Task>)ReadLoop);Task.Run(async()=>{try{char[] chars=new char[1024];while(await pipes.Error.ReadAsync(chars,0,chars.Length)>0){} }catch{}});}catch(Exception error){pipes?.Dispose();if(error is AgentFailure)throw;throw new AgentFailure("mcp_start","MCP 服务启动失败，请检查本地程序和参数。");}
  }
  public static string Quote(string value){var s=new StringBuilder("\"");int slash=0;foreach(char c in value){if(c=='\\'){slash++;continue;}if(c=='\"'){s.Append('\\',slash*2+1);s.Append(c);}else{s.Append('\\',slash);s.Append(c);}slash=0;}s.Append('\\',slash*2);s.Append('"');return s.ToString();}
  async Task Send(object value){await writer.WaitAsync();try{if(closed)throw new IOException();await input.WriteLineAsync(Library.Json.Serialize(value));}finally{writer.Release();}}
  public async Task<Dictionary<string,object>> Request(string method,Dictionary<string,object> args,CancellationToken token,int timeoutMs=15000,bool metadata=true,bool closeOnTimeout=true){
   token.ThrowIfCancellationRequested();var id=Interlocked.Increment(ref serial).ToString();var source=new TaskCompletionSource<Dictionary<string,object>>(TaskCreationOptions.RunContinuationsAsynchronously);
   lock(gate){if(closed)throw new AgentFailure("mcp_disconnected","MCP 服务未连接，请重新连接。");pending[id]=source;}
   var parameters=new Dictionary<string,object>(args??new Dictionary<string,object>());if(metadata&&(Protocol==null||Protocol==ModernVersion))parameters["_meta"]=new Dictionary<string,object>{{"io.modelcontextprotocol/protocolVersion",ModernVersion},{"io.modelcontextprotocol/clientInfo",new{name="kok",version=AppVersion.Current}},{"io.modelcontextprotocol/clientCapabilities",new Dictionary<string,object>()}};
   try{
    var send=Send(new{jsonrpc="2.0",id,method,@params=parameters});var timeout=Task.Delay(timeoutMs,token);
    if(await Task.WhenAny(send,timeout)!=send){await Cancel(id);if(token.IsCancellationRequested)throw new OperationCanceledException(token);throw new TimeoutException("MCP 请求超时。");}await send;
    if(await Task.WhenAny(source.Task,timeout)!=source.Task){if(closeOnTimeout||token.IsCancellationRequested)await Cancel(id);if(token.IsCancellationRequested)throw new OperationCanceledException(token);throw new TimeoutException("MCP 请求超时。");}
    return await source.Task;
   }finally{lock(gate)pending.Remove(id);}
  }
  async Task Cancel(string id){try{var send=Send(new{jsonrpc="2.0",method="notifications/cancelled",@params=new{requestId=id,reason="Client cancelled"}});await Task.WhenAny(send,Task.Delay(100));}catch{}Dispose();}
  async Task ReadLoop(){try{var buffer=new char[2048];var line=new StringBuilder();int read;while((read=await pipes.Output.ReadAsync(buffer,0,buffer.Length))>0){for(int n=0;n<read;n++){char c=buffer[n];if(c=='\n'){if(line.Length>0){Receive(line.ToString().TrimEnd('\r'));line.Clear();}}else{if(line.Length>=1024*1024)throw new InvalidDataException();line.Append(c);}}}throw new EndOfStreamException();}catch{lock(gate){foreach(var item in pending.Values)item.TrySetException(new AgentFailure("mcp_disconnected","MCP 服务已断开，请检查外部执行结果后重新连接。"));}Dispose();}}
  void Receive(string json){var message=Library.Json.Deserialize<Dictionary<string,object>>(json);if(message==null||!message.ContainsKey("jsonrpc")||Convert.ToString(message["jsonrpc"])!="2.0")throw new InvalidDataException();
   object value;if(message.TryGetValue("method",out value)){
    string method=Convert.ToString(value);if(method=="notifications/tools/list_changed")CatalogChanged=true;
    if(message.ContainsKey("id")){if(Protocol==ModernVersion)throw new InvalidDataException();var id=message["id"];Task.Run(async()=>{try{if(method=="ping")await Send(new{jsonrpc="2.0",id,result=new{}});else await Send(new{jsonrpc="2.0",id,error=new{code=-32601,message="Client capability not supported"}});}catch{}});}return;
   }
   if(!message.TryGetValue("id",out value))throw new InvalidDataException();TaskCompletionSource<Dictionary<string,object>> source;lock(gate){if(!pending.TryGetValue(Convert.ToString(value),out source))return;}
   if(message.TryGetValue("error",out value)){var error=value as Dictionary<string,object>;int code=error!=null&&error.ContainsKey("code")?Convert.ToInt32(error["code"]):-32603;source.TrySetException(new McpRpcException(code,error!=null&&error.ContainsKey("data")?error["data"] as Dictionary<string,object>:null));return;}
   var result=message.ContainsKey("result")?message["result"] as Dictionary<string,object>:null;if(result==null)throw new InvalidDataException();if(result.ContainsKey("resultType")&&Convert.ToString(result["resultType"])!="complete"){source.TrySetException(new AgentFailure("mcp_interaction","外部工具需要额外交互，当前不会自动授权或继续。"));return;}source.TrySetResult(result);
  }
  public async Task Initialize(CancellationToken token){
   bool legacy=false;Dictionary<string,object> discovery=null;
   try{discovery=await Request("server/discover",new Dictionary<string,object>(),token,3000,true,false);}
   catch(McpRpcException error){if(error.code<=-32020&&error.code>=-32099){if(error.code==-32022&&error.data!=null&&error.data.ContainsKey("supported")&&Library.Json.Deserialize<List<string>>(Library.Json.Serialize(error.data["supported"])).Contains(ModernVersion))Protocol=ModernVersion;else throw new AgentFailure("mcp_version","MCP 协议版本不兼容。");}else legacy=true;}
   catch(TimeoutException){legacy=true;}
   if(discovery!=null){if(!discovery.ContainsKey("supportedVersions")||!Library.Json.Deserialize<List<string>>(Library.Json.Serialize(discovery["supportedVersions"])).Contains(ModernVersion))throw new AgentFailure("mcp_version","MCP 协议版本不兼容。");Protocol=ModernVersion;}
   if(legacy){Protocol="2025-11-25";var result=await Request("initialize",new Dictionary<string,object>{{"protocolVersion",Protocol},{"capabilities",new Dictionary<string,object>()},{"clientInfo",new{name="kok",version=AppVersion.Current}}},token,15000,false);string version=result.ContainsKey("protocolVersion")?Convert.ToString(result["protocolVersion"]):"";if(!new[]{"2025-11-25","2025-06-18","2025-03-26","2024-11-05"}.Contains(version))throw new AgentFailure("mcp_version","MCP 协议版本不兼容。");Protocol=version;await Send(new{jsonrpc="2.0",method="notifications/initialized"});}
  }
  public void Dispose(){lock(gate){if(closed)return;closed=true;foreach(var item in pending.Values)item.TrySetException(new AgentFailure("mcp_disconnected","MCP 服务未连接，请重新连接。"));}pipes?.Dispose();if(!String.IsNullOrEmpty(sandboxSpec)){try{File.Delete(sandboxSpec);}catch{}sandboxSpec=null;}}
 }
}

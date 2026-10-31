using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace kok {
 public sealed class LibraryAccessSettings {public int port;public bool capture,read;public List<string> ids=new List<string>();public string captureToken,readToken;}
 public sealed class LibraryConnections:IDisposable {
  readonly Library library;readonly LocalEmbedder embedder;readonly object gate=new object();readonly SemaphoreSlim clients=new SemaphoreSlim(8,8),preparations=new SemaphoreSlim(2,2);TcpListener listener;CancellationTokenSource stop;LibraryAccessSettings settings;ModuleContext context;
  public LibraryConnections(Library library,LocalEmbedder embedder=null){this.library=library;this.embedder=embedder;}
  static string FileName(Library lib){return Path.Combine(lib.DataDir,"library-access.sealed");}
  internal static LibraryAccessSettings Load(string directory){string file=Path.Combine(directory,"library-access.sealed");if(!File.Exists(file)||new FileInfo(file).Length>1024*1024)throw new InvalidDataException("Enable external library access in kok first.");return Library.Json.Deserialize<LibraryAccessSettings>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file),null,DataProtectionScope.CurrentUser)));}
  public object Snapshot(){lock(gate)return new{running=listener!=null,capture=listener!=null&&settings.capture,read=listener!=null&&settings.read,scope=listener==null?0:settings.ids.Count,port=listener==null?0:settings.port};}
  public object Configure(ModuleContext c,bool capture,bool read,List<string> ids){
   c.Policy.Ensure(Need.NormalLibrary,"workspace");if(ids==null||ids.Count>10000||ids.Any(id=>library.Find(id)==null))throw new InvalidDataException("Choose valid sources for external read access.");Dispose();context=c;settings=new LibraryAccessSettings{capture=capture,read=read,ids=ids.Distinct().ToList(),captureToken=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N"),readToken=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N")};
   if(capture||read){int port=0;try{port=Load(library.DataDir).port;}catch{}listener=new TcpListener(IPAddress.Loopback,port);try{listener.Start();}catch(SocketException){listener=new TcpListener(IPAddress.Loopback,0);listener.Start();}settings.port=((IPEndPoint)listener.LocalEndpoint).Port;stop=new CancellationTokenSource();var server=listener;var token=stop.Token;Task.Run(()=>Accept(server,token));}
   string file=FileName(library),temp=file+".tmp";File.WriteAllBytes(temp,ProtectedData.Protect(Encoding.UTF8.GetBytes(Library.Json.Serialize(settings)),null,DataProtectionScope.CurrentUser));if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);return Snapshot();
  }
  public object ConnectionInfo(string executable){lock(gate){if(listener==null)throw new InvalidOperationException("External connections are not enabled.");return new{captureUrl="http://127.0.0.1:"+settings.port+"/capture",captureToken=settings.capture?settings.captureToken:"",mcp=new{command=executable,args=new[]{"--mcp","--data-dir",library.DataDir}},scope=settings.ids.Count};}}
  async Task Accept(TcpListener server,CancellationToken token){while(!token.IsCancellationRequested){TcpClient client;try{client=await server.AcceptTcpClientAsync();}catch(ObjectDisposedException){break;}catch(SocketException){break;}if(!clients.Wait(0)){client.Dispose();continue;}_ = Serve(client,token).ContinueWith(t=>clients.Release());}}
  static bool EqualToken(string actual,string expected){if(actual==null||expected==null||actual.Length!=expected.Length)return false;int diff=0;for(int i=0;i<actual.Length;i++)diff|=actual[i]^expected[i];return diff==0;}
  async Task Serve(TcpClient client,CancellationToken token){using(client)using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){timeout.CancelAfter(45000);using(timeout.Token.Register(()=>client.Close()))try{
   var stream=client.GetStream();var header=new List<byte>();var one=new byte[1];while(header.Count<16384){int n=await stream.ReadAsync(one,0,1,timeout.Token);if(n==0)return;header.Add(one[0]);int count=header.Count;if(count>=4&&header[count-4]==13&&header[count-3]==10&&header[count-2]==13&&header[count-1]==10)break;}if(header.Count>=16384)return;
   var lines=Encoding.ASCII.GetString(header.ToArray()).Split(new[]{"\r\n"},StringSplitOptions.None);var first=lines[0].Split(' ');if(first.Length!=3||first[0]!="POST"||!new[]{"/mcp","/capture"}.Contains(first[1])){await Reject(client,stream,405,"Method not allowed",timeout.Token);return;}var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(var line in lines.Skip(1).Where(l=>l!="")){int at=line.IndexOf(':');if(at<=0||headers.ContainsKey(line.Substring(0,at))){await Reject(client,stream,400,"Invalid headers",timeout.Token);return;}headers[line.Substring(0,at)]=line.Substring(at+1).Trim();}
   string auth,origin,type,length;int size;bool capture=first[1]=="/capture";LibraryAccessSettings active;lock(gate)active=settings;
   // Reject credentials and origins before reading/allocating any body bytes.
   if(active==null||capture&&!active.capture||!capture&&!active.read||!headers.TryGetValue("Authorization",out auth)||!EqualToken(auth,"Bearer "+(capture?active.captureToken:active.readToken))){await Reject(client,stream,403,"Access denied",timeout.Token);return;}
   if(headers.TryGetValue("Origin",out origin)&&(origin=="null"||!origin.StartsWith("chrome-extension://",StringComparison.Ordinal)&&!origin.StartsWith("moz-extension://",StringComparison.Ordinal))){await Reject(client,stream,403,"Origin denied",timeout.Token);return;}
   if(headers.ContainsKey("Transfer-Encoding")||!headers.TryGetValue("Content-Type",out type)||!type.StartsWith("application/json",StringComparison.OrdinalIgnoreCase)||!headers.TryGetValue("Content-Length",out length)||!Int32.TryParse(length,out size)||size<1||size>(capture?24*1024*1024:1024*1024)){await Reject(client,stream,400,"Invalid body",timeout.Token);return;}
   var bytes=new byte[size];for(int pos=0;pos<size;){int read=await stream.ReadAsync(bytes,pos,size-pos,timeout.Token);if(read==0)return;pos+=read;}
   object result;
   if(capture){
    Library.PreparedCapture prepared;await preparations.WaitAsync(timeout.Token);
    try{prepared=await Task.Run(()=>Library.PrepareCapture(Library.Json.Deserialize<CaptureRequest>(Encoding.UTF8.GetString(bytes)),timeout.Token),timeout.Token);}finally{preparations.Release();}
    result=await OnUi(()=>{ValidateAccess(active);timeout.Token.ThrowIfCancellationRequested();var item=library.CapturePrepared(prepared);context.Notify("libraryChanged",null);return Task.FromResult((object)new{saved=true,id=item.id,name=item.name});});
   }else{
    var message=await Task.Run(()=>Library.Json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(bytes)),timeout.Token);
    result=await OnUi(async()=>{ValidateAccess(active);return await Rpc(message,active,timeout.Token);});
   }
   await Reply(stream,200,result,timeout.Token);
  }catch(OperationCanceledException){}catch(ObjectDisposedException){}catch{try{await Reply(client.GetStream(),400,new{error="Request failed or library access is unavailable."},CancellationToken.None);}catch{}}}}
  void ValidateAccess(LibraryAccessSettings active){context.Policy.Ensure(Need.NormalLibrary,"workspace");lock(gate)if(listener==null||settings!=active)throw new InvalidOperationException("Connection was revoked.");}
  Task<object> OnUi(Func<Task<object>> action){if(context.Owner==null||!context.Owner.InvokeRequired)return action();var done=new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);context.Owner.BeginInvoke((Action)(async()=>{try{done.TrySetResult(await action());}catch(Exception e){done.TrySetException(e);}}));return done.Task;}
  static async Task Reject(TcpClient client,NetworkStream stream,int status,string error,CancellationToken token){
   await Reply(stream,status,new{error},token);
   // Send the complete rejection first. Closing with unread TCP bytes can otherwise reset the response.
   try{client.Client.Shutdown(SocketShutdown.Send);}catch(SocketException){return;}catch(ObjectDisposedException){return;}
   using(var grace=CancellationTokenSource.CreateLinkedTokenSource(token)){
    grace.CancelAfter(500);using(grace.Token.Register(()=>client.Close()))try{
     var discard=new byte[8192];int total=0;while(total<24*1024*1024){int n=await stream.ReadAsync(discard,0,discard.Length,grace.Token);if(n==0)break;total+=n;}
    }catch(OperationCanceledException){}catch(IOException){}catch(ObjectDisposedException){}
   }
  }
  static async Task Reply(Stream stream,int status,object value,CancellationToken token){byte[] data=Encoding.UTF8.GetBytes(Library.Json.Serialize(value)),header=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+" Response\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: "+data.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");await stream.WriteAsync(header,0,header.Length,token);await stream.WriteAsync(data,0,data.Length,token);}
  static Dictionary<string,object> Schema(params object[] fields){return CapabilitySchema.Obj(CapabilitySchema.Fields(fields));}
  internal static object[] ToolList(){return new[]{new{name="search_library",description="Search only explicitly shared sources using the same local hybrid/lexical configuration and fallback notices as kok. Results include source locations. Read-only.",inputSchema=Schema("query",CapabilitySchema.Str(200),"limit",new Dictionary<string,object>{{"type","integer"},{"minimum",1},{"maximum",50}}),annotations=new{readOnlyHint=true}},new{name="read_excerpt",description="Read a current indexed excerpt by its chunk ID within the shared scope.",inputSchema=CapabilitySchema.Obj(CapabilitySchema.Fields("chunkId",CapabilitySchema.Str(32)),new[]{"chunkId"}),annotations=new{readOnlyHint=true}},new{name="list_sources",description="List shared source metadata without exposing local paths.",inputSchema=Schema("offset",new Dictionary<string,object>{{"type","integer"},{"minimum",0},{"maximum",10000}},"limit",new Dictionary<string,object>{{"type","integer"},{"minimum",1},{"maximum",100}}),annotations=new{readOnlyHint=true}}}.Cast<object>().ToArray();}
  async Task<object> Rpc(Dictionary<string,object> message,LibraryAccessSettings active,CancellationToken token){
   object id;message.TryGetValue("id",out id);string method=ModuleContext.Text(message,"method");if(method.StartsWith("notifications/",StringComparison.Ordinal))return new{};var args=AgentTaskState.Get<Dictionary<string,object>>(message,"params")??new Dictionary<string,object>();object value;
   if(method=="initialize")value=new{protocolVersion="2025-11-25",capabilities=new{tools=new{listChanged=false}},serverInfo=new{name="kok-library",version=AppVersion.Current}};
   else if(method=="ping")value=new{};
   else if(method=="tools/list")value=new{tools=ToolList()};
   else if(method=="tools/call"){
    string name=ModuleContext.Text(args,"name");var a=AgentTaskState.Get<Dictionary<string,object>>(args,"arguments")??new Dictionary<string,object>();var tool=ToolList().Select(t=>Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(t))).FirstOrDefault(t=>Convert.ToString(t["name"])==name);if(tool==null)return new{jsonrpc="2.0",id,error=new{code=-32602,message="Unknown read-only tool."}};try{CapabilitySchema.Check(AgentTaskState.Get<Dictionary<string,object>>(tool,"inputSchema"),a);var scope=active.ids.Where(k=>library.Find(k)!=null).ToArray();object result;
     if(name=="search_library"){bool semantic=context.Policy.Allows(Need.Ai|Need.NormalLibrary,"workspace")&&library.ReadEmbeddingConfig().enabled;result=await library.SearchKnowledge(ModuleContext.Text(a,"query"),scope,semantic?"hybrid":"lexical",a.ContainsKey("limit")?Convert.ToInt32(a["limit"]):20,token,embedder,validate:()=>ValidateAccess(active));}
     else if(name=="read_excerpt")result=library.ReadContentChunk(ModuleContext.Text(a,"chunkId"),scope);
     else{int offset=a.ContainsKey("offset")?Convert.ToInt32(a["offset"]):0,limit=a.ContainsKey("limit")?Convert.ToInt32(a["limit"]):50;var entries=scope.Select(library.Find).Where(e=>e!=null).OrderBy(e=>e.id).ToArray();result=new{total=entries.Length,sources=entries.Skip(offset).Take(limit).Select(e=>new{e.id,e.name,e.kind,e.collection,e.tags}).ToArray()};}context.Policy.Ensure(Need.NormalLibrary,"workspace");lock(gate)if(listener==null||settings!=active)throw new InvalidOperationException();value=new{content=new[]{new{type="text",text=Library.Json.Serialize(result)}},isError=false};}
    catch{value=new{content=new[]{new{type="text",text="Source is stale, outside the shared scope, or temporarily unavailable."}},isError=true};}
   }else return new{jsonrpc="2.0",id,error=new{code=-32601,message="Method not supported."}};
   return new{jsonrpc="2.0",id,result=value};
  }
  public void Dispose(){lock(gate){stop?.Cancel();listener?.Stop();listener=null;stop?.Dispose();stop=null;}}
 }
 public partial class Library {LibraryConnections connections;internal LibraryConnections Connections{get{lock(gate)return connections??(connections=new LibraryConnections(this));}}internal void StopConnections(){lock(gate)connections?.Dispose();}}
 internal static class ConnectionActions {
  internal static IEnumerable<ActionSpec> Actions(){return new[]{ModuleDefinitions.Action("libraryAccessState",Need.NormalLibrary,false,(c,a)=>Task.FromResult(c.Library.Connections.Snapshot()),CapabilitySchema.Obj()),ModuleDefinitions.Action("libraryAccessConfigure",Need.NormalLibrary,true,(c,a)=>Task.FromResult(c.Library.Connections.Configure(c,a.ContainsKey("capture")&&Convert.ToBoolean(a["capture"]),a.ContainsKey("read")&&Convert.ToBoolean(a["read"]),AgentTaskState.Get<List<string>>(a,"ids")??new List<string>())),CapabilitySchema.Obj(CapabilitySchema.Fields("capture",new Dictionary<string,object>{{"type","boolean"}},"read",new Dictionary<string,object>{{"type","boolean"}},"ids",new Dictionary<string,object>{{"type","array"},{"maxItems",10000},{"items",CapabilitySchema.Str(32)}}))),ModuleDefinitions.Action("libraryAccessConnection",Need.NormalLibrary,false,(c,a)=>Task.FromResult(c.Library.Connections.ConnectionInfo(Path.Combine(c.AppDirectory,"kok.exe"))),CapabilitySchema.Obj())};}
 }
 internal static class McpAccessHost {
  internal static void Run(string[] args){var input=Console.OpenStandardInput();using(var output=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true}){int at=Array.IndexOf(args,"--data-dir");if(at<0||at+1>=args.Length)return;LibraryAccessSettings config;try{config=LibraryConnections.Load(Path.GetFullPath(args[at+1]));if(!config.read||config.port<1)return;}catch{return;}using(var client=new HttpClient(new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(45)}){while(true){var bytes=new List<byte>();int b;while((b=input.ReadByte())>=0&&b!=10&&bytes.Count<=1024*1024)bytes.Add((byte)b);if(b<0)return;if(bytes.Count>1024*1024)return;string line=Encoding.UTF8.GetString(bytes.ToArray()).Trim();if(line=="")continue;Dictionary<string,object> message;try{message=Library.Json.Deserialize<Dictionary<string,object>>(line);}catch{continue;}object id;bool request=message.TryGetValue("id",out id);if(!request)continue;try{using(var req=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:"+config.port+"/mcp")){req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",config.readToken);req.Content=new StringContent(line,Encoding.UTF8,"application/json");using(var result=client.SendAsync(req).GetAwaiter().GetResult()){if(!result.IsSuccessStatusCode)throw new IOException();string text=result.Content.ReadAsStringAsync().GetAwaiter().GetResult();output.WriteLine(text);}}}catch{output.WriteLine(Library.Json.Serialize(new{jsonrpc="2.0",id,error=new{code=-32000,message="Open kok and enable scoped external access before connecting."}}));}}}}}
 }
}

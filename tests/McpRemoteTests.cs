using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

static class McpRemoteTests {
 sealed class Resp { public int status = 200; public string body = ""; public string session = ""; }
 sealed class Request { public string method, path; public Dictionary<string,string> headers = new Dictionary<string,string>(); public Dictionary<string,object> body; }
 sealed class Fixture : IDisposable {
  readonly TcpListener listener;readonly Thread thread;volatile bool running=true;public readonly List<Request> requests=new List<Request>();public string Token="secret-token";public int Port;
  public Func<Request,Resp> Handler;
  public Fixture(){listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Port=((IPEndPoint)listener.LocalEndpoint).Port;thread=new Thread(Loop){IsBackground=true};thread.Start();}
  public string Url{get{return "http://127.0.0.1:"+Port+"/mcp";}}
  void Loop(){
   while(running){try{using(var client=listener.AcceptTcpClient())using(var stream=client.GetStream()){
    var request=new Request();
    var header=new MemoryStream();int matched=0;
    while(matched<4){int value=stream.ReadByte();if(value<0)break;header.WriteByte((byte)value);if((new[]{13,10,13,10})[matched]==value)matched++;else matched=value==13?1:0;}
    string head=Encoding.ASCII.GetString(header.ToArray());var lines=head.Replace("\r\n","\n").Split('\n');request.method=lines.Length>0?lines[0].Split(' ')[0]:"";request.path=lines.Length>0&&lines[0].Split(' ').Length>1?lines[0].Split(' ')[1]:"";
    foreach(var line in lines.Skip(1)){int colon=line.IndexOf(':');if(colon<=0)continue;request.headers[line.Substring(0,colon).Trim().ToLowerInvariant()]=line.Substring(colon+1).Trim();}
    int length=0;string contentLength;if(request.headers.TryGetValue("content-length",out contentLength))Int32.TryParse(contentLength,out length);
    var body=new byte[length];int offset=0;while(offset<length){int read=stream.Read(body,offset,length-offset);if(read<=0)break;offset+=read;}
    if(length>0){try{request.body=Library.Json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(body,0,offset));}catch{}}
    lock(requests)requests.Add(request);
    var response=Handler!=null?Handler(request):new Resp{status=404};
    var bytes=Encoding.UTF8.GetBytes(response.body??"");
    var output=new StringBuilder();output.Append("HTTP/1.1 ").Append(response.status).Append(response.status==202?" Accepted":" OK").Append("\r\n");
    if(bytes.Length>0)output.Append("Content-Type: application/json\r\n");
    output.Append("Content-Length: ").Append(bytes.Length).Append("\r\n");if(!String.IsNullOrEmpty(response.session))output.Append("Mcp-Session-Id: ").Append(response.session).Append("\r\n");output.Append("Connection: close\r\n\r\n");
    var headBytes=Encoding.ASCII.GetBytes(output.ToString());stream.Write(headBytes,0,headBytes.Length);if(bytes.Length>0)stream.Write(bytes,0,bytes.Length);stream.Flush();
   }}catch{}}
  }
  public void Dispose(){running=false;try{listener.Stop();}catch{}}
 }
 static Dictionary<string,object> Obj(string json){return Library.Json.Deserialize<Dictionary<string,object>>(json);}
 static Resp Json(string body){return new Resp{body=body};}
 static string Result(string id,object result){return Library.Json.Serialize(new{jsonrpc="2.0",id,result});}
 static string Error(string id,int code,string message){return Library.Json.Serialize(new{jsonrpc="2.0",id,error=new{code,message}});}
 public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
 static async Task RunAsync(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  using(var fixture=new Fixture()){
   fixture.Handler=request=>{
    string authorization;request.headers.TryGetValue("authorization",out authorization);
    if(authorization!="Bearer secret-token")return new Resp{status=401,body="{\"error\":\"unauthorized\"}"};
    string method=request.body!=null&&request.body.ContainsKey("method")?Convert.ToString(request.body["method"]):"";
    string id=request.body!=null&&request.body.ContainsKey("id")?Convert.ToString(request.body["id"]):"";
    if(method=="server/discover")return Json(Error(id,-32601,"method not found"));
    if(method=="initialize")return new Resp{body=Result(id,new{protocolVersion="2025-11-25",capabilities=new Dictionary<string,object>(),serverInfo=new{name="fixture",version="1"}}),session="session-1"};
    if(method=="notifications/initialized")return new Resp{status=202};
    if(method=="tools/list"){var schema=new Dictionary<string,object>{{"type","object"},{"properties",new Dictionary<string,object>{{"text",new Dictionary<string,object>{{"type","string"}}}}},{"required",new[]{"text"}}};var tool=new Dictionary<string,object>{{"name","echo"},{"description","Echo text"},{"inputSchema",schema}};return Json(Result(id,new Dictionary<string,object>{{"tools",new object[]{tool}}}));}
    if(method=="tools/call"){var parameters=AgentTaskState.Get<Dictionary<string,object>>(request.body,"params");var args=AgentTaskState.Get<Dictionary<string,object>>(parameters,"arguments");string text=args!=null&&args.ContainsKey("text")?Convert.ToString(args["text"]):"";return Json(Result(id,new{content=new object[]{new{type="text",text="ECHO:"+text}}}));}
    return new Resp{status=404};
   };
   bool loopbackHttp=true;try{McpHttpClient.ValidateEndpoint(fixture.Url);}catch(AgentFailure){loopbackHttp=false;}check(loopbackHttp,"remote MCP accepts an explicit loopback HTTP endpoint for local fixtures");
   bool insecure=false;try{McpHttpClient.ValidateEndpoint("http://example.com/mcp");}catch(AgentFailure){insecure=true;}check(insecure,"remote MCP requires HTTPS for non-loopback endpoints");
   using(var client=new McpHttpClient(new Uri(fixture.Url),"secret-token")){
    await client.Initialize(CancellationToken.None);
    check(client.Connected&&client.Protocol=="2025-11-25","remote MCP negotiates the legacy initialize handshake and stores the session");
    var tools=await client.Request("tools/list",new Dictionary<string,object>(),CancellationToken.None);
    var rows=AgentTaskState.Get<List<Dictionary<string,object>>>(tools,"tools");
    check(rows!=null&&rows.Count==1&&Convert.ToString(rows[0]["name"])=="echo","remote MCP lists bounded tool definitions over HTTP");
    var call=await client.Request("tools/call",new Dictionary<string,object>{{"name","echo"},{"arguments",new Dictionary<string,object>{{"text","你好 remote 🌱"}}}},CancellationToken.None);
    var content=AgentTaskState.Get<List<Dictionary<string,object>>>(call,"content");
    check(content!=null&&content.Count==1&&Convert.ToString(content[0]["text"]).Contains("你好 remote 🌱"),"remote MCP roundtrips Unicode tool results over HTTP");
    var sessionRequest=fixture.requests.FirstOrDefault(r=>r.body!=null&&Convert.ToString(r.body.ContainsKey("method")?r.body["method"]:"")=="tools/list");
    string sessionHeader=null;if(sessionRequest!=null)sessionRequest.headers.TryGetValue("mcp-session-id",out sessionHeader);
    check(sessionHeader=="session-1","remote MCP sends the negotiated session id on later requests");
   }
   using(var wrong=new McpHttpClient(new Uri(fixture.Url),"wrong-token")){bool denied=false;try{await wrong.Initialize(CancellationToken.None);}catch(AgentFailure error){denied=error.kind=="mcp_auth";}check(denied,"remote MCP rejects a wrong access token without executing any tool");}
   var lib=new Library(Path.Combine(root,"library"));lib.SetAppMode("smart",true);var service=new McpService(lib,()=>{});
   try{
    string id=service.Configure(new McpServerConfig{name="remote fixture",transport="http",url=fixture.Url,trusted=true},"secret-token");
    string sealedText=Encoding.UTF8.GetString(System.Security.Cryptography.ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(root,"library","mcp-servers.sealed")),Encoding.UTF8.GetBytes("kok.mcp.v1"),System.Security.Cryptography.DataProtectionScope.CurrentUser));check(!sealedText.Contains("secret-token")&&sealedText.Contains("\"transport\"")&&sealedText.Contains("\"protectedToken\""),"remote MCP endpoint and access token are DPAPI encrypted at rest");
    await service.Connect(id,CancellationToken.None);var state=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(service.Snapshot()))[0];
    check(Convert.ToString(state["transport"])=="http"&&Convert.ToBoolean(state["connected"])&&!service.Available,"remote MCP connects, exposes its catalog and requires explicit per-tool approval");
    var tools=AgentTaskState.Get<List<Dictionary<string,object>>>(state,"tools");var echo=tools.Single(t=>Convert.ToString(t["name"])=="echo");service.Approve(id,new Dictionary<string,string>{{"echo",Convert.ToString(echo["stamp"])}});
    string stamp=service.Stamp(id,"echo",new Dictionary<string,object>{{"text","approved"}});
    var receipt=await service.Call(new AgentCall{id=Guid.NewGuid().ToString("N"),tool="mcp_call",args=new Dictionary<string,object>{{"serverId",id},{"tool","echo"},{"arguments",new Dictionary<string,object>{{"text","approved call"}}}},externalStamp=stamp},CancellationToken.None);
    check(Convert.ToString(receipt.result["text"]).Contains("ECHO:approved call"),"approved remote MCP tools execute through the shared approval and schema pipeline");
    bool unknown=false;try{service.Stamp(id,"echo",new Dictionary<string,object>{{"unknown","x"}});}catch(AgentFailure){unknown=true;}check(unknown,"remote MCP input schemas reject unknown arguments before execution");
   } finally {service.Dispose();}
  }
 }
}

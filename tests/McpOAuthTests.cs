using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

static class McpOAuthTests {
 sealed class Resp { public int status = 200; public string body = ""; }
 sealed class Request { public string method = "", path = ""; public Dictionary<string,string> headers = new Dictionary<string,string>(); public string body = ""; public Dictionary<string,object> json; }
 sealed class Fixture : IDisposable {
  readonly TcpListener listener;readonly Thread thread;volatile bool running=true;public string BaseUrl;public Func<Request,Resp> Handler;
  public Fixture(){listener=new TcpListener(IPAddress.Loopback,0);listener.Start();BaseUrl="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;thread=new Thread(Loop){IsBackground=true};thread.Start();}
  public string McpUrl { get { return BaseUrl + "/mcp"; } }
  void Loop(){
   while(running){try{using(var client=listener.AcceptTcpClient())using(var stream=client.GetStream()){
    var header=new MemoryStream();int matched=0;
    while(matched<4){int value=stream.ReadByte();if(value<0)break;header.WriteByte((byte)value);if((new[]{13,10,13,10})[matched]==value)matched++;else matched=value==13?1:0;}
    string head=Encoding.ASCII.GetString(header.ToArray());var lines=head.Replace("\r\n","\n").Split('\n');var request=new Request();var first=lines.Length>0?lines[0].Split(' '):new string[0];
    request.method=first.Length>0?first[0]:"";request.path=first.Length>1?first[1]:"";foreach(var line in lines.Skip(1)){int colon=line.IndexOf(':');if(colon<=0)continue;request.headers[line.Substring(0,colon).Trim().ToLowerInvariant()]=line.Substring(colon+1).Trim();}
    int length=0;string rawLength;if(request.headers.TryGetValue("content-length",out rawLength))Int32.TryParse(rawLength,out length);
    var body=new byte[length];int offset=0;while(offset<length){int read=stream.Read(body,offset,length-offset);if(read<=0)break;offset+=read;}
    request.body=Encoding.UTF8.GetString(body,0,offset);try{request.json=Library.Json.Deserialize<Dictionary<string,object>>(request.body);}catch{}
    var response=Handler!=null?Handler(request):new Resp{status=404};
    byte[] bytes=Encoding.UTF8.GetBytes(response.body??"");var sb=new StringBuilder("HTTP/1.1 ").Append(response.status).Append(response.status==202?" Accepted":" OK").Append("\r\n");
    if(bytes.Length>0)sb.Append("Content-Type: application/json\r\n");sb.Append("Content-Length: ").Append(bytes.Length).Append("\r\nConnection: close\r\n\r\n");
    byte[] headBytes=Encoding.ASCII.GetBytes(sb.ToString());stream.Write(headBytes,0,headBytes.Length);if(bytes.Length>0)stream.Write(bytes,0,bytes.Length);stream.Flush();
   }}catch{}}
  }
  public void Dispose(){running=false;try{listener.Stop();}catch{}}
 }
 static Resp Json(string body){return new Resp{body=body};}
 static string Result(string id,object result){return Library.Json.Serialize(new{jsonrpc="2.0",id,result});}
 static string RpcError(string id,int code,string message){return Library.Json.Serialize(new{jsonrpc="2.0",id,error=new{code,message}});}
 static Dictionary<string,string> Form(string body){var result=new Dictionary<string,string>();foreach(var part in (body??"").Split('&')){int at=part.IndexOf('=');if(at<0)continue;result[Uri.UnescapeDataString(part.Substring(0,at))]=Uri.UnescapeDataString(part.Substring(at+1));}return result;}
 static Dictionary<string,string> Query(string path){int at=path.IndexOf('?');return at<0?new Dictionary<string,string>():Form(path.Substring(at+1));}
 static string Base64Url(byte[] bytes){return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');}
 public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
 static async Task RunAsync(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  string verifierHash="";string authorizeChallenge="";string resourceParameter="";int registrations=0,exchanges=0,refreshes=0,protectedRequests=0;bool protectedResourceServed=false;
  using(var fixture=new Fixture()){
   fixture.Handler=request=>{
    string path=request.path.Split('?')[0];
    if(request.method=="GET"&&(path=="/.well-known/oauth-protected-resource"||path=="/.well-known/oauth-protected-resource/mcp")){protectedResourceServed=true;return Json(Library.Json.Serialize(new{resource=fixture.McpUrl,authorization_servers=new[]{fixture.BaseUrl},scopes_supported=new[]{"mcp.read"}}));}
    if(request.method=="GET"&&path=="/.well-known/oauth-authorization-server")return Json(Library.Json.Serialize(new{issuer=fixture.BaseUrl,authorization_endpoint=fixture.BaseUrl+"/authorize",token_endpoint=fixture.BaseUrl+"/token",registration_endpoint=fixture.BaseUrl+"/register",code_challenge_methods_supported=new[]{"S256"},response_types_supported=new[]{"code"}}));
    if(request.method=="POST"&&path=="/register"){registrations++;return new Resp{status=201,body=Library.Json.Serialize(new{client_id="client-1",token_endpoint_auth_method="none"})};}
    if(request.method=="POST"&&path=="/token"){
     var form=Form(request.body);string grant;form.TryGetValue("grant_type",out grant);
     if(grant=="authorization_code"){
      exchanges++;string verifier,redirect,code,resource;form.TryGetValue("code_verifier",out verifier);form.TryGetValue("redirect_uri",out redirect);form.TryGetValue("code",out code);form.TryGetValue("resource",out resource);
      if(verifier==null||verifier.Length<43||code!="code-1"||String.IsNullOrEmpty(redirect)||resource!=fixture.McpUrl)return new Resp{status=400,body="{\"error\":\"invalid_request\"}"};
      verifierHash=verifier;resourceParameter=resource;
      return Json(Library.Json.Serialize(new{access_token="access-1",refresh_token="refresh-1",token_type="Bearer",expires_in=3600,scope="mcp.read"}));
     }
     if(grant=="refresh_token"){refreshes++;return Json(Library.Json.Serialize(new{access_token="access-2",refresh_token="refresh-2",token_type="Bearer",expires_in=3600}));}
     return new Resp{status=400,body="{\"error\":\"unsupported_grant_type\"}"};
    }
    if(path=="/mcp"){
     protectedRequests++;string authorization;request.headers.TryGetValue("authorization",out authorization);
     string expected="Bearer "+(refreshes>0?"access-2":"access-1");
     if(authorization!=expected)return new Resp{status=401,body="{\"error\":\"unauthorized\"}"};
     string method=request.json!=null&&request.json.ContainsKey("method")?Convert.ToString(request.json["method"]):"";string id=request.json!=null&&request.json.ContainsKey("id")?Convert.ToString(request.json["id"]):"";
     if(method=="server/discover")return Json(RpcError(id,-32601,"method not found"));
     if(method=="initialize")return Json(Result(id,new{protocolVersion="2025-11-25",capabilities=new Dictionary<string,object>(),serverInfo=new{name="oauth-fixture",version="1"}}));
     if(method=="notifications/initialized")return new Resp{status=202};
    if(method=="tools/list"){var schema=new Dictionary<string,object>{{"type","object"},{"properties",new Dictionary<string,object>{{"text",new Dictionary<string,object>{{"type","string"}}}}},{"required",new[]{"text"}}};
    var tool=new Dictionary<string,object>{{"name","echo"},{"description","Echo text"},{"inputSchema",schema}};
    var result=new Dictionary<string,object>{{"tools",new object[]{tool}}};return Json(Result(id,result));}
     if(method=="tools/call"){var parameters=AgentTaskState.Get<Dictionary<string,object>>(request.json,"params");var args=AgentTaskState.Get<Dictionary<string,object>>(parameters,"arguments");var text=args!=null&&args.ContainsKey("text")?Convert.ToString(args["text"]):"";return Json(Result(id,new{content=new object[]{new{type="text",text="OAUTH-ECHO:"+text}}}));}
     return new Resp{status=404};
    }
    return new Resp{status=404};
   };
   string known="test-verifier-0123456789abcdefghijklmnopqrstuvwxyz-A";
   string expectedChallenge=Base64Url(System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(known)));
   check(McpOAuth.Challenge(known)==expectedChallenge,"OAuth PKCE challenges are S256 over a base64url verifier");
   var server=new McpServerConfig{name="oauth fixture",transport="http",url=fixture.McpUrl,auth="oauth",trusted=true};
   string opened="";
   var flow=await McpOAuth.Begin(server,url=>{opened=url;},CancellationToken.None);
   var query=Query(new Uri(flow.AuthorizeUrl).Query);
   string redirect,state,challenge,resource,clientId,scope;
   query.TryGetValue("redirect_uri",out redirect);query.TryGetValue("state",out state);query.TryGetValue("code_challenge",out challenge);query.TryGetValue("resource",out resource);query.TryGetValue("client_id",out clientId);query.TryGetValue("scope",out scope);
   check(opened==flow.AuthorizeUrl&&redirect==flow.RedirectUri&&state==flow.State&&challenge.Length>40&&resource==fixture.McpUrl&&clientId=="client-1"&&scope.Contains("mcp.read")&&registrations==1&&protectedResourceServed,"OAuth starts with protected-resource metadata, dynamic registration, PKCE and a loopback redirect");
   using(var callback=new HttpClient()){
    var response=await callback.GetAsync(flow.RedirectUri+"?code=code-1&state="+Uri.EscapeDataString(state));
    check(response.IsSuccessStatusCode,"the loopback OAuth callback completes without a manual copy/paste step");
   }
   var token=await flow.Completion;
   check(token.access_token=="access-1"&&token.refresh_token=="refresh-1"&&token.expires_at>DateTimeOffset.UtcNow.ToUnixTimeSeconds()&&exchanges==1&&verifierHash.Length>=43&&resourceParameter==fixture.McpUrl,"the authorization code is exchanged with its PKCE verifier and resource indicator");
   server.protectedOAuth=McpOAuth.Protect(token);
   check(!server.protectedOAuth.Contains("access-1")&&McpOAuth.Read(server).access_token=="access-1","OAuth tokens are DPAPI encrypted inside the MCP configuration");
   var refreshed=await McpOAuth.Refresh(server);
   check(refreshed.access_token=="access-2"&&refreshed.refresh_token=="refresh-2"&&refreshes==1,"OAuth refresh tokens issue a new access token");
   var serviceLibrary=new Library(Path.Combine(root,"service"));serviceLibrary.SetAppMode("smart",true);var store=new McpService(serviceLibrary,()=>{});
   try{
    bool stdioRejected=false;try{store.Configure(new McpServerConfig{name="bad",transport="stdio",executable=Environment.GetEnvironmentVariable("ComSpec"),auth="oauth",trusted=true});}catch(AgentFailure error){stdioRejected=error.kind=="mcp_config";}check(stdioRejected,"OAuth is refused for local stdio MCP servers");
   } finally {store.Dispose();}
  }
  var lib=new Library(Path.Combine(root,"library"));lib.SetAppMode("smart",true);
  using(var fixture=new Fixture()){
   string expectedToken="access-2";int mcpCalls=0;
   fixture.Handler=request=>{
    string path=request.path.Split('?')[0];
    if(path!="/mcp")return new Resp{status=404};
    mcpCalls++;string authorization;request.headers.TryGetValue("authorization",out authorization);
    if(authorization!="Bearer "+expectedToken)return new Resp{status=401,body="{\"error\":\"unauthorized\"}"};
    string id=request.json!=null&&request.json.ContainsKey("id")?Convert.ToString(request.json["id"]):"";string method=request.json!=null&&request.json.ContainsKey("method")?Convert.ToString(request.json["method"]):"";
    if(method=="server/discover")return Json(RpcError(id,-32601,"method not found"));
    if(method=="initialize")return Json(Result(id,new{protocolVersion="2025-11-25",capabilities=new Dictionary<string,object>(),serverInfo=new{name="refresh-fixture",version="1"}}));
    if(method=="notifications/initialized")return new Resp{status=202};
    if(method=="tools/list")return Json(Result(id,new Dictionary<string,object>{{"tools",new object[0]}}));
    return new Resp{status=404};
   };
   int refreshCalls=0;
   using(var client=new McpHttpClient(new Uri(fixture.McpUrl),"access-1",async()=>{refreshCalls++;return "access-2";})){
    await client.Initialize(CancellationToken.None);
    check(refreshCalls==1&&mcpCalls>=2&&client.Protocol=="2025-11-25","a remote MCP 401 refreshes the OAuth token once and retries the request");
   }
  }
  using(var fixture=new Fixture()){
   string expectedToken="access-1";
   fixture.Handler=request=>{
    string path=request.path.Split('?')[0];string method=request.json!=null&&request.json.ContainsKey("method")?Convert.ToString(request.json["method"]):"";string id=request.json!=null&&request.json.ContainsKey("id")?Convert.ToString(request.json["id"]):"";
    if(path=="/mcp"){
     string authorization;request.headers.TryGetValue("authorization",out authorization);if(authorization!="Bearer "+expectedToken)return new Resp{status=401,body="{\"error\":\"unauthorized\"}"};
     if(method=="server/discover")return Json(RpcError(id,-32601,"method not found"));
     if(method=="initialize")return Json(Result(id,new{protocolVersion="2025-11-25",capabilities=new Dictionary<string,object>(),serverInfo=new{name="service-fixture",version="1"}}));
     if(method=="notifications/initialized")return new Resp{status=202};
    if(method=="tools/list"){var schema=new Dictionary<string,object>{{"type","object"},{"properties",new Dictionary<string,object>{{"text",new Dictionary<string,object>{{"type","string"}}}}},{"required",new[]{"text"}}};
    var tool=new Dictionary<string,object>{{"name","echo"},{"description","Echo text"},{"inputSchema",schema}};
    var result=new Dictionary<string,object>{{"tools",new object[]{tool}}};return Json(Result(id,result));}
     if(method=="tools/call"){var parameters=AgentTaskState.Get<Dictionary<string,object>>(request.json,"params");var args=AgentTaskState.Get<Dictionary<string,object>>(parameters,"arguments");var text=args!=null&&args.ContainsKey("text")?Convert.ToString(args["text"]):"";return Json(Result(id,new{content=new object[]{new{type="text",text="OAUTH-ECHO:"+text}}}));}
    }
    return new Resp{status=404};
   };
   var service=new McpService(lib,()=>{});
   try{
    string id=service.Configure(new McpServerConfig{name="oauth service",transport="http",url=fixture.McpUrl,auth="oauth",trusted=true});
    var state=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(service.Snapshot()))[0];
    check(Convert.ToString(state["auth"])=="oauth"&&!Convert.ToBoolean(state["oauthConnected"]),"MCP snapshots expose OAuth configuration without leaking tokens");
    service.SaveOAuth(id,new McpOAuthToken{access_token=expectedToken,token_type="Bearer",resource=fixture.McpUrl});
    await service.Connect(id,CancellationToken.None);
    var connected=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(service.Snapshot()))[0];
    check(Convert.ToBoolean(connected["connected"])&&Convert.ToBoolean(connected["oauthConnected"]),"an OAuth access token connects a remote MCP server through the shared approval pipeline");
   } finally {service.Dispose();}
  }
 }
}

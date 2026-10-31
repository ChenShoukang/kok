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

static class McpDeviceOAuthTests {
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
    int length=0;string raw;if(request.headers.TryGetValue("content-length",out raw))Int32.TryParse(raw,out length);
    var body=new byte[length];int offset=0;while(offset<length){int read=stream.Read(body,offset,length-offset);if(read<=0)break;offset+=read;}
    request.body=Encoding.UTF8.GetString(body,0,offset);try{request.json=Library.Json.Deserialize<Dictionary<string,object>>(request.body);}catch{}
    var response=Handler!=null?Handler(request):new Resp{status=404};
    byte[] bytes=Encoding.UTF8.GetBytes(response.body??"");var sb=new StringBuilder("HTTP/1.1 ").Append(response.status).Append(" OK\r\n");
    if(bytes.Length>0)sb.Append("Content-Type: application/json\r\n");sb.Append("Content-Length: ").Append(bytes.Length).Append("\r\nConnection: close\r\n\r\n");
    byte[] headBytes=Encoding.ASCII.GetBytes(sb.ToString());stream.Write(headBytes,0,headBytes.Length);if(bytes.Length>0)stream.Write(bytes,0,bytes.Length);stream.Flush();
   }}catch{}}
  }
  public void Dispose(){running=false;try{listener.Stop();}catch{}}
 }
 static Resp Json(string body){return new Resp{body=body};}
 static Dictionary<string,string> Form(string body){var result=new Dictionary<string,string>();foreach(var part in (body??"").Split('&')){int at=part.IndexOf('=');if(at<0)continue;result[Uri.UnescapeDataString(part.Substring(0,at))]=Uri.UnescapeDataString(part.Substring(at+1));}return result;}
 static Dictionary<string,string> Query(string path){int at=path.IndexOf('?');return at<0?new Dictionary<string,string>():Form(path.Substring(at+1));}
 static Resp Metadata(Fixture f, bool device) {
  return Json(Library.Json.Serialize(new {
   issuer=f.BaseUrl,
   authorization_endpoint=f.BaseUrl+"/authorize",
   token_endpoint=f.BaseUrl+"/token",
   registration_endpoint=f.BaseUrl+"/register",
   device_authorization_endpoint=device?f.BaseUrl+"/device":null,
   code_challenge_methods_supported=new[]{"S256"},
   response_types_supported=new[]{"code"}
  }));
 }
 public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
 static async Task RunAsync(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  string access="",refresh="";int tokenPolls=0,deviceRequests=0;string resourceParam="";
  using(var fixture=new Fixture()){
   fixture.Handler=request=>{
    string path=request.path.Split('?')[0];
    if(request.method=="GET"&&(path=="/.well-known/oauth-protected-resource"||path=="/.well-known/oauth-protected-resource/mcp"))return Json(Library.Json.Serialize(new{resource=fixture.McpUrl,authorization_servers=new[]{fixture.BaseUrl},scopes_supported=new[]{"mcp.read"}}));
    if(request.method=="GET"&&path=="/.well-known/oauth-authorization-server")return Metadata(fixture,true);
    if(request.method=="POST"&&path=="/register")return Json(Library.Json.Serialize(new{client_id="device-client",token_endpoint_auth_method="none"}));
    if(request.method=="POST"&&path=="/device"){deviceRequests++;var form=Form(request.body);string resource;form.TryGetValue("resource",out resource);resourceParam=resource??"";return Json(Library.Json.Serialize(new{device_code="device-code-1",user_code="ABCD-EFGH",verification_uri=fixture.BaseUrl+"/verify",verification_uri_complete=fixture.BaseUrl+"/verify?code=ABCD-EFGH",expires_in=60,interval=1}));}
    if(request.method=="POST"&&path=="/token"){
     var form=Form(request.body);string grant;form.TryGetValue("grant_type",out grant);
     if(grant=="urn:ietf:params:oauth:grant-type:device_code"){
      tokenPolls++;
      if(tokenPolls==1)return new Resp{status=400,body="{\"error\":\"authorization_pending\"}"};
      if(tokenPolls==2)return new Resp{status=400,body="{\"error\":\"slow_down\"}"};
      access="device-access-1";refresh="device-refresh-1";
      return Json(Library.Json.Serialize(new{access_token=access,refresh_token=refresh,token_type="Bearer",expires_in=3600,scope="mcp.read"}));
     }
     if(grant=="refresh_token")return Json(Library.Json.Serialize(new{access_token="device-access-2",refresh_token="device-refresh-2",token_type="Bearer",expires_in=3600}));
     return new Resp{status=400,body="{\"error\":\"unsupported_grant_type\"}"};
    }
    return new Resp{status=404};
   };
   var server=new McpServerConfig{name="device fixture",transport="http",url=fixture.McpUrl,auth="oauth",oauthFlow="device_code",trusted=true};
   McpDeviceFlow shown=null;
   var flow=await McpOAuth.BeginDevice(server,value=>{shown=value;},CancellationToken.None);
   check(flow.UserCode=="ABCD-EFGH"&&flow.VerificationUri.EndsWith("/verify")&&flow.ExpiresIn==60&&shown==flow&&deviceRequests==1&&resourceParam==fixture.McpUrl,"OAuth device authorization returns a user code and verification URI with the resource indicator");
   var token=await flow.Completion;
   check(token.access_token=="device-access-1"&&token.refresh_token=="device-refresh-1"&&token.token_endpoint==fixture.BaseUrl+"/token"&&token.expires_at>DateTimeOffset.UtcNow.ToUnixTimeSeconds(),"device polling survives authorization_pending and slow_down, then stores a token with its endpoint metadata");
   server.protectedOAuth=McpOAuth.Protect(token);
   var refreshed=await McpOAuth.Refresh(server);
   check(refreshed.access_token=="device-access-2"&&refreshed.refresh_token=="device-refresh-2","a device-flow refresh token issues a new access token");
   var serviceLibrary=new Library(Path.Combine(root,"service"));serviceLibrary.SetAppMode("smart",true);
   using(var service=new McpService(serviceLibrary,()=>{})){
    string id=service.Configure(new McpServerConfig{name="device service",transport="http",url=fixture.McpUrl,auth="oauth",oauthFlow="device_code",trusted=true});
    var snapshot=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(service.Snapshot()))[0];
    check(Convert.ToString(snapshot["oauthFlow"])=="device_code","MCP configuration exposes the OAuth device-code flow");
    var info=await service.StartDeviceOAuth(id,_=>{});
    var infoMap=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(info));
    string code=infoMap.ContainsKey("userCode")?Convert.ToString(infoMap["userCode"]):"";
    check(code=="ABCD-EFGH","the MCP service starts a device-code login and returns the user code to the UI");
    bool connected=false;for(int attempt=0;attempt<120&&!connected;attempt++){await Task.Delay(500);var state=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(service.Snapshot()))[0];connected=Convert.ToBoolean(state["oauthConnected"]);}
    var finalState=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(service.Snapshot()))[0];
    check(connected,"device-code completion stores the OAuth token in the MCP configuration (snapshot="+Library.Json.Serialize(finalState)+")");
   }
  }
  using(var fixture=new Fixture()){
   fixture.Handler=request=>{
    string path=request.path.Split('?')[0];
    if(request.method=="GET"&&(path=="/.well-known/oauth-protected-resource"||path=="/.well-known/oauth-protected-resource/mcp"))return Json(Library.Json.Serialize(new{resource=fixture.McpUrl,authorization_servers=new[]{fixture.BaseUrl},scopes_supported=new[]{"mcp.read"}}));
    if(request.method=="GET"&&path=="/.well-known/oauth-authorization-server")return Metadata(fixture,true);
    if(request.method=="POST"&&path=="/register")return Json("{\"client_id\":\"device-client\"}");
    if(request.method=="POST"&&path=="/device")return Json("{\"device_code\":\"denied-1\",\"user_code\":\"DENY-ME\",\"verification_uri\":\""+fixture.BaseUrl+"/verify\",\"expires_in\":30,\"interval\":1}");
    if(request.method=="POST"&&path=="/token")return new Resp{status=400,body="{\"error\":\"access_denied\"}"};
    return new Resp{status=404};
   };
   var server=new McpServerConfig{name="denied",transport="http",url=fixture.McpUrl,auth="oauth",oauthFlow="device_code",trusted=true};
   var flow=await McpOAuth.BeginDevice(server,null,CancellationToken.None);
   bool denied=false;try{await flow.Completion;}catch(AgentFailure error){denied=error.kind=="mcp_auth";}check(denied,"a denied device-code authorization fails without storing a token");
  }
 }
}

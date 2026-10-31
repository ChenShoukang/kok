using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using kok;
class LibraryConnectionTests {
 sealed class Embeddings:HttpMessageHandler {
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){var body=Library.Json.Deserialize<Dictionary<string,object>>(await request.Content.ReadAsStringAsync());var input=AgentTaskState.Get<List<string>>(body,"input");return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Library.Json.Serialize(new{embeddings=input.Select(s=>s.Contains("OUTSIDE_CONNECTION_SCOPE")?new[]{0d,1}:new[]{1d,0}).ToArray()}))};}
 }
 static bool RejectBeforeBody(int port,string key,string origin){using(var socket=new TcpClient("127.0.0.1",port)){socket.ReceiveTimeout=3000;var stream=socket.GetStream();string header="POST /capture HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer "+key+"\r\nContent-Type: application/json\r\nContent-Length: 25165824\r\n"+(origin==null?"":"Origin: "+origin+"\r\n")+"\r\n";var bytes=Encoding.ASCII.GetBytes(header);stream.Write(bytes,0,bytes.Length);var response=new byte[512];int n=stream.Read(response,0,response.Length);return Encoding.ASCII.GetString(response,0,n).StartsWith("HTTP/1.1 403",StringComparison.Ordinal);}}
 public static void Run(string root,Action<bool,string> check){
  var lib=new Library(root);Entry shared=lib.CreateDocument("Shared","md","Shared","CONNECTION_SCOPE_CANARY"),other=lib.CreateDocument("Hidden","md","Hidden","OUTSIDE_CONNECTION_SCOPE");lib.IndexContentSource(shared.id);lib.IndexContentSource(other.id);bool current=true;var c=new ModuleContext(lib,new TranslationService(root),new ModulePolicy(lib,()=>current),null,CancellationToken.None,null,new Operations(lib,null),new Jobs(root,null));
  lib.SetAppMode("smart",true);var embeddings=new LocalEmbedder(()=>new Embeddings());lib.SaveEmbeddingConfig(new EmbeddingConfig{enabled=true,model="fixture",minimumSimilarity=.5});lib.BuildEmbeddings(CancellationToken.None,null,embeddings).GetAwaiter().GetResult();
  using(var gateway=new LibraryConnections(lib,embeddings)){gateway.Configure(c,true,true,new List<string>{shared.id});var cfg=LibraryConnections.Load(root);using(var client=new HttpClient(new HttpClientHandler{UseProxy=false})){Func<string,string,object,string,HttpResponseMessage> post=(route,key,body,origin)=>{var req=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:"+cfg.port+route);req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);if(origin!=null)req.Headers.Add("Origin",origin);req.Content=new StringContent(Library.Json.Serialize(body),Encoding.UTF8,"application/json");return client.SendAsync(req).GetAwaiter().GetResult();};
    check(RejectBeforeBody(cfg.port,"wrong",null),"invalid pairing is rejected before a declared 24 MB body is sent");
    check(RejectBeforeBody(cfg.port,cfg.captureToken,"https://evil.example"),"a disallowed origin is rejected before the capture body is sent");
    using(var denied=post("/capture","wrong",new CaptureRequest{title="Denied",text="bad"},null))check(denied.StatusCode==HttpStatusCode.Forbidden,"browser capture rejects an invalid pairing credential");
    using(var denied=post("/capture",cfg.captureToken,new CaptureRequest{title="Denied",text="bad"},"https://evil.example"))check(denied.StatusCode==HttpStatusCode.Forbidden,"local capture rejects a web origin even with a credential");
    using(var saved=post("/capture",cfg.captureToken,new CaptureRequest{title="Paired: capture",url="https://example.org/",text="PAIRED_CAPTURE_CANARY"},"chrome-extension://test"))check(saved.IsSuccessStatusCode&&lib.Db.items.Count==3,"paired extension capture creates one provenance-linked library source");
    int refused=0;for(int attempt=0;attempt<40;attempt++)using(var denied=post("/mcp",cfg.captureToken,new{jsonrpc="2.0",id=1,method="tools/list",padding=new string('x',attempt%2==0?32768:256)},null))if(denied.StatusCode==HttpStatusCode.Forbidden)refused++;check(refused==40,"capture credentials receive complete 403 responses for repeated small/large MCP requests without reading any source");
    using(var result=post("/mcp",cfg.readToken,new{jsonrpc="2.0",id=2,method="tools/call",@params=new{name="list_sources",arguments=new{}}},null)){string text=result.Content.ReadAsStringAsync().GetAwaiter().GetResult();check(text.Contains("Shared")&&!text.Contains("Hidden")&&!text.Contains(root.Replace("\\","\\\\")),"MCP source listings respect the explicit sharing scope and hide local paths");}
    using(var result=post("/mcp",cfg.readToken,new{jsonrpc="2.0",id=3,method="tools/call",@params=new{name="write_note",arguments=new{}}},null))check(result.Content.ReadAsStringAsync().GetAwaiter().GetResult().Contains("-32602"),"read-only MCP refuses mutation tools");
    using(var result=post("/mcp",cfg.readToken,new{jsonrpc="2.0",id=5,method="tools/call",@params=new{name="search_library",arguments=new{query="SEMANTIC_PARAPHRASE_NO_LEXICAL_MATCH"}}},null)){string text=result.Content.ReadAsStringAsync().GetAwaiter().GetResult();check(text.Contains("hybrid")&&text.Contains("CONNECTION_SCOPE_CANARY")&&!text.Contains("OUTSIDE_CONNECTION_SCOPE"),"MCP shares hybrid retrieval with UI/Agent without widening its read scope");}
    lib.SetAppMode("pure",false);using(var result=post("/mcp",cfg.readToken,new{jsonrpc="2.0",id=6,method="tools/call",@params=new{name="search_library",arguments=new{query="CONNECTION_SCOPE_CANARY"}}},null))check(result.Content.ReadAsStringAsync().GetAwaiter().GetResult().Contains("lexical"),"MCP preserves Pure-mode lexical fallback without contacting embeddings");
    current=false;using(var result=post("/mcp",cfg.readToken,new{jsonrpc="2.0",id=4,method="tools/call",@params=new{name="list_sources",arguments=new{}}},null))check(!result.Content.ReadAsStringAsync().GetAwaiter().GetResult().Contains("CONNECTION_SCOPE_CANARY"),"switching away from the normal library blocks external reads");
   }}
 }
}

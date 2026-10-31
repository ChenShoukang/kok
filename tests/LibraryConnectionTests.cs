using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Collections.Generic;
using kok;
class LibraryConnectionTests {
 public static void Run(string root,Action<bool,string> check){
  var lib=new Library(root);Entry shared=lib.CreateDocument("Shared","md","Shared","CONNECTION_SCOPE_CANARY"),other=lib.CreateDocument("Hidden","md","Hidden","OUTSIDE_CONNECTION_SCOPE");lib.IndexContentSource(shared.id);lib.IndexContentSource(other.id);bool current=true;var c=new ModuleContext(lib,new TranslationService(root),new ModulePolicy(lib,()=>current),null,CancellationToken.None,null,new Operations(lib,null),new Jobs(root,null));
  using(var gateway=new LibraryConnections(lib)){gateway.Configure(c,true,true,new List<string>{shared.id});var cfg=LibraryConnections.Load(root);using(var client=new HttpClient(new HttpClientHandler{UseProxy=false})){Func<string,string,object,string,HttpResponseMessage> post=(route,key,body,origin)=>{var req=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:"+cfg.port+route);req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);if(origin!=null)req.Headers.Add("Origin",origin);req.Content=new StringContent(Library.Json.Serialize(body),Encoding.UTF8,"application/json");return client.SendAsync(req).GetAwaiter().GetResult();};
    using(var denied=post("/capture","wrong",new CaptureRequest{title="Denied",text="bad"},null))check(denied.StatusCode==HttpStatusCode.Forbidden,"browser capture rejects an invalid pairing credential");
    using(var denied=post("/capture",cfg.captureToken,new CaptureRequest{title="Denied",text="bad"},"https://evil.example"))check(denied.StatusCode==HttpStatusCode.Forbidden,"local capture rejects a web origin even with a credential");
    using(var saved=post("/capture",cfg.captureToken,new CaptureRequest{title="Paired: capture",url="https://example.org/",text="PAIRED_CAPTURE_CANARY"},"chrome-extension://test"))check(saved.IsSuccessStatusCode&&lib.Db.items.Count==3,"paired extension capture creates one provenance-linked library source");
    using(var denied=post("/mcp",cfg.captureToken,new{jsonrpc="2.0",id=1,method="tools/list"},null))check(denied.StatusCode==HttpStatusCode.Forbidden,"capture credentials cannot read library data through MCP");
    using(var result=post("/mcp",cfg.readToken,new{jsonrpc="2.0",id=2,method="tools/call",@params=new{name="list_sources",arguments=new{}}},null)){string text=result.Content.ReadAsStringAsync().GetAwaiter().GetResult();check(text.Contains("Shared")&&!text.Contains("Hidden")&&!text.Contains(root.Replace("\\","\\\\")),"MCP source listings respect the explicit sharing scope and hide local paths");}
    using(var result=post("/mcp",cfg.readToken,new{jsonrpc="2.0",id=3,method="tools/call",@params=new{name="write_note",arguments=new{}}},null))check(result.Content.ReadAsStringAsync().GetAwaiter().GetResult().Contains("-32602"),"read-only MCP refuses mutation tools");
    current=false;using(var result=post("/mcp",cfg.readToken,new{jsonrpc="2.0",id=4,method="tools/call",@params=new{name="list_sources",arguments=new{}}},null))check(!result.Content.ReadAsStringAsync().GetAwaiter().GetResult().Contains("CONNECTION_SCOPE_CANARY"),"switching away from the normal library blocks external reads");
   }}
 }
}

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using kok;

static class AgentWebTests {
 sealed class Fixture : IDisposable {
  readonly TcpListener listener;readonly Thread thread;volatile bool running=true;
  readonly Dictionary<string,byte[]> bodies=new Dictionary<string,byte[]>();
  readonly Dictionary<string,string> types=new Dictionary<string,string>();
  readonly Dictionary<string,string> redirects=new Dictionary<string,string>();
  public int Port{get;private set;}
  public Fixture(){
   listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Port=((IPEndPoint)listener.LocalEndpoint).Port;
   thread=new Thread(Loop){IsBackground=true};thread.Start();
  }
  public string Url(string path){return "http://127.0.0.1:"+Port+path;}
  public void Text(string path,string contentType,string text){types[path]=contentType;bodies[path]=Encoding.UTF8.GetBytes(text);}
  public void Raw(string path,string contentType,byte[] body){types[path]=contentType;bodies[path]=body;}
  public void Redirect(string path,string location){redirects[path]=location;}
  void Loop(){
   while(running){
    try{
     using(var client=listener.AcceptTcpClient())using(var stream=client.GetStream()){
      var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true);string request=reader.ReadLine()??"";string line;while(!String.IsNullOrEmpty(line=reader.ReadLine())){}
      string path="/";try{path=request.Split(' ')[1];int query=path.IndexOf('?');if(query>=0)path=path.Substring(0,query);}catch{}
      string location;if(redirects.TryGetValue(path,out location)){Write(stream,"302 Found","text/plain",Encoding.UTF8.GetBytes("redirect"),location);continue;}
      byte[] body;string type;if(bodies.TryGetValue(path,out body)){types.TryGetValue(path,out type);Write(stream,"200 OK",type??"application/octet-stream",body,null);}else Write(stream,"404 Not Found","text/plain",Encoding.UTF8.GetBytes("missing"),null);
     }
    }catch{}
   }
  }
  static void Write(NetworkStream stream,string status,string type,byte[] body,string location){
   var header=new StringBuilder();header.Append("HTTP/1.1 ").Append(status).Append("\r\n");header.Append("Content-Type: ").Append(type).Append("\r\n");header.Append("Content-Length: ").Append(body.Length).Append("\r\n");if(location!=null)header.Append("Location: ").Append(location).Append("\r\n");header.Append("Connection: close\r\n\r\n");
   var bytes=Encoding.ASCII.GetBytes(header.ToString());stream.Write(bytes,0,bytes.Length);stream.Write(body,0,body.Length);stream.Flush();
  }
  public void Dispose(){running=false;try{listener.Stop();}catch{}}
 }
 public static void Run(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  using(var fixture=new Fixture()){
   var article=new StringBuilder();article.Append("<html><head><title>KOK TEST ARTICLE</title></head><body><nav>NAVIGATION_JUNK</nav><article><h1>KOK TEST ARTICLE</h1>");article.Append("<p>SENTINEL_WEB_CONTENT the article body explains the topic in detail with enough prose for readability extraction.</p>");for(int paragraph=0;paragraph<6;paragraph++)article.Append("<p>Paragraph "+paragraph+" contains supporting evidence, a concrete example and a clear conclusion about the topic under discussion.</p>");article.Append("</article><footer>FOOTER_JUNK</footer></body></html>");
   fixture.Text("/article","text/html; charset=utf-8",article.ToString());
   fixture.Redirect("/redirect","/article");
   fixture.Text("/binary","image/png","not really an image");
   fixture.Raw("/big","text/html; charset=utf-8",new byte[3*1024*1024]);
   fixture.Text("/search","application/json","{\"results\":[{\"title\":\"First result\",\"url\":\"https://example.org/a\",\"content\":\"Snippet one\"},{\"title\":\"Second result\",\"url\":\"https://example.org/b\",\"content\":\"Snippet two\"}]}");
   string previous=Environment.GetEnvironmentVariable("KOK_TEST_ALLOW_LOOPBACK_WEB");
   Environment.SetEnvironmentVariable("KOK_TEST_ALLOW_LOOPBACK_WEB","1");
   try{
    bool loopbackBlocked=false;try{AgentWeb.ValidateAsync(fixture.Url("/article"),false,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){loopbackBlocked=true;}check(loopbackBlocked,"web fetch refuses loopback and private network addresses by default");
    var allowed=AgentWeb.ValidateAsync(fixture.Url("/article"),true,CancellationToken.None).GetAwaiter().GetResult();check(allowed.IsLoopback,"explicit test-only loopback permission is required for local fixtures");
    check(AgentWeb.Blocked(IPAddress.Parse("127.0.0.1"))&&AgentWeb.Blocked(IPAddress.Parse("10.1.2.3"))&&AgentWeb.Blocked(IPAddress.Parse("192.168.1.1"))&&AgentWeb.Blocked(IPAddress.Parse("169.254.1.1"))&&AgentWeb.Blocked(IPAddress.Parse("172.20.0.1"))&&AgentWeb.Blocked(IPAddress.IPv6Loopback)&&!AgentWeb.Blocked(IPAddress.Parse("8.8.8.8"))&&!AgentWeb.Blocked(IPAddress.Parse("1.1.1.1")),"SSRF guard blocks private, link-local, loopback and reserved ranges but allows public addresses");
    var lib=new Library(Path.Combine(root,"library"));lib.SetAppMode("smart",true);
    var ai=new TranslationService(lib.DataDir);
    var context=new ModuleContext(lib,ai,new ModulePolicy(lib),null,CancellationToken.None,null,new Operations(lib,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory);
    var state=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=new AgentTaskOptions{sessionId=Guid.NewGuid().ToString("N"),goal="web fixture",allowed=new List<string>()}};
    var tools=AgentWeb.Tools();
    var fetch=tools.Single(t=>t.Name=="fetch_url");var search=tools.Single(t=>t.Name=="web_search");
    check(AgentRules.WriteTools.Contains("fetch_url")&&AgentRules.WriteTools.Contains("web_search")&&!AgentRules.ReadTools.Contains("fetch_url"),"network reads are approval-only tools rather than silently running read tools");
    bool rejectedUrl=false;try{AgentRules.ValidateAction(new AgentAction{tool="fetch_url",args=new Dictionary<string,object>{{"url","file:///C:/secret.txt"}}},id=>true);}catch(FormatException){rejectedUrl=true;}check(rejectedUrl,"fetch_url rejects non-http schemes before a request is proposed");
    var receipt=fetch.ExecuteWithContext(context,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="fetch_url",args=new Dictionary<string,object>{{"url",fixture.Url("/article")}}},CancellationToken.None).GetAwaiter().GetResult();
    string text=Convert.ToString(receipt.result["text"]);
    check(text.Contains("SENTINEL_WEB_CONTENT")&&text.Contains("KOK TEST ARTICLE")&&!text.Contains("NAVIGATION_JUNK"),"SmartReader extraction keeps the article body and title while dropping navigation content");
    var redirected=fetch.ExecuteWithContext(context,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="fetch_url",args=new Dictionary<string,object>{{"url",fixture.Url("/redirect")}}},CancellationToken.None).GetAwaiter().GetResult();
    check(Convert.ToString(redirected.result["url"]).EndsWith("/article"),"fetch_url follows validated redirects and reports the final URL");
    bool oversized=false;try{fetch.ExecuteWithContext(context,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="fetch_url",args=new Dictionary<string,object>{{"url",fixture.Url("/big")}}},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException error){oversized=error.Message.Contains("MB");}check(oversized,"oversized web responses are rejected before entering the model context");
    bool binaryRejected=false;try{fetch.ExecuteWithContext(context,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="fetch_url",args=new Dictionary<string,object>{{"url",fixture.Url("/binary")}}},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidDataException){binaryRejected=true;}check(binaryRejected,"non-text web responses are rejected");
    ai.SaveConfig(new Dictionary<string,object>{{"endpoint","http://127.0.0.1:1/v1"},{"model","fixture-model"},{"language","简体中文"},{"searchEndpoint",fixture.Url("/search")}});
    check(ai.SearchEndpoint==fixture.Url("/search"),"search endpoint is stored encrypted-config-adjacent and exposed to approved web search");
    var found=search.ExecuteWithContext(context,state,new AgentCall{id=Guid.NewGuid().ToString("N"),tool="web_search",args=new Dictionary<string,object>{{"query","kok test"}}},CancellationToken.None).GetAwaiter().GetResult();
    string results=Convert.ToString(found.result["text"]);
    check(results.Contains("First result")&&results.Contains("https://example.org/a")&&results.Contains("Second result"),"web_search parses SearXNG-compatible JSON results with titles, URLs and snippets");
   } finally {Environment.SetEnvironmentVariable("KOK_TEST_ALLOW_LOOPBACK_WEB",previous);}
  }
 }
}

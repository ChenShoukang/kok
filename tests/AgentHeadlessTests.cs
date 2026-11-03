using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using kok;

// Headless `kok agent` CLI: deterministic fixture provider (no external network), exact stdout/stderr
// shapes and exit codes. The fixture mirrors the HttpMessageHandler pattern used by AgentKernelTests.
static class AgentHeadlessTests {
 sealed class Provider:HttpMessageHandler {
  public readonly Func<int,Dictionary<string,object>,CancellationToken,Task<HttpResponseMessage>> respond;
  public int requests;public List<Dictionary<string,object>> bodies=new List<Dictionary<string,object>>();
  public Provider(Func<int,Dictionary<string,object>,CancellationToken,Task<HttpResponseMessage>> respond){this.respond=respond;}
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){var body=Library.Json.Deserialize<Dictionary<string,object>>(await request.Content.ReadAsStringAsync());bodies.Add(body);return await respond(++requests,body,token);}
 }
 static HttpResponseMessage Response(object body){return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Library.Json.Serialize(body),Encoding.UTF8,"application/json")};}
 // RPC test harness: a stdin the test feeds line by line, and a thread-safe stdout the test inspects.
 sealed class ScriptedInput:TextReader {
  readonly BlockingCollection<string> lines=new BlockingCollection<string>();
  public override string ReadLine(){try{return lines.Take();}catch{return null;}}
  public void Send(string line){try{lines.Add(line);}catch{}}
  public void CloseInput(){try{lines.CompleteAdding();}catch{}}
 }
 sealed class RecordingOutput:TextWriter {
  readonly object gate=new object();readonly StringBuilder text=new StringBuilder();
  public override Encoding Encoding=>Encoding.UTF8;
  public override void Write(char value){lock(gate)text.Append(value);}
  public override void Write(string value){lock(gate)text.Append(value);}
  public string Text {get{lock(gate)return text.ToString();}}
 }
 static Dictionary<string,object> RpcMap(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
 static string Cmd(string id,string method,object fields=null){
  var row=new Dictionary<string,object>{{"method",method}};
  if(id!=null)row["id"]=id;
  if(fields!=null)foreach(var pair in RpcMap(fields))row[pair.Key]=pair.Value;
  return Library.Json.Serialize(row);
 }
 static List<Dictionary<string,object>> RpcLines(string text){
  var rows=new List<Dictionary<string,object>>();int start=0;
  for(int n=0;n<text.Length;n++){
   if(text[n]!='\n')continue;
   string line=text.Substring(start,n-start).Trim();start=n+1;
   if(line.Length>0)rows.Add(Library.Json.Deserialize<Dictionary<string,object>>(line));
  }
  return rows;
 }
 static List<Dictionary<string,object>> WaitFor(RecordingOutput output,Func<List<Dictionary<string,object>>,bool> ready,int timeoutMs=10000){
  var watch=Stopwatch.StartNew();
  while(watch.ElapsedMilliseconds<timeoutMs){var rows=RpcLines(output.Text);if(ready(rows))return rows;Thread.Sleep(5);}
  return RpcLines(output.Text);
 }
 static Dictionary<string,object> WaitLine(RecordingOutput output,Func<Dictionary<string,object>,bool> match,int timeoutMs=10000){
  var rows=WaitFor(output,list=>list.Any(match),timeoutMs);
  return rows.FirstOrDefault(match);
 }
 static Dictionary<string,object> WaitResponse(RecordingOutput output,string id,int timeoutMs=10000){return WaitLine(output,row=>Text(row,"type")=="response"&&Text(row,"id")==id,timeoutMs);}
 static Dictionary<string,object> WaitResultLine(RecordingOutput output,int timeoutMs=10000){return WaitLine(output,row=>Text(row,"type")=="result",timeoutMs);}
 static Dictionary<string,object> WaitResultLineAfter(RecordingOutput output,int alreadySeen,int timeoutMs=10000){
  var rows=WaitFor(output,list=>list.Count(row=>Text(row,"type")=="result")>alreadySeen,timeoutMs);
  return rows.Where(row=>Text(row,"type")=="result").ElementAtOrDefault(alreadySeen);
 }
 static HttpResponseMessage SummaryReply(string summary){return Response(new{choices=new[]{new{message=new{content=Library.Json.Serialize(new{summary})},finish_reason="stop"}},usage=new{prompt_tokens=10,completion_tokens=5}});}
 static string Text(Dictionary<string,object> row,string key){object value;if(row==null||!row.TryGetValue(key,out value)||value==null)return null;return Convert.ToString(value);}
 static bool Bool(Dictionary<string,object> row,string key){object value;if(row==null||!row.TryGetValue(key,out value)||value==null)return false;try{return Convert.ToBoolean(value);}catch{return false;}}
 static HttpResponseMessage ToolReply(string tool,object args,string id="call_fixture",string reply="Fixture"){return Response(new{choices=new[]{new{message=new{role="assistant",content=reply,tool_calls=new[]{new{id,type="function",function=new{name=tool,arguments=Library.Json.Serialize(args)}}}},finish_reason="tool_calls"}},usage=new{prompt_tokens=120,completion_tokens=30}});}
 static HttpResponseMessage FinishReply(string evidence,string reply){
  var finish=new{id="call_finish",type="function",function=new{name="finish_task",arguments=Library.Json.Serialize(new{status="completed",summary="Headless reviewed",checks=new[]{new{claim="Read the headless source",evidence=new[]{evidence}}}})}};
  return Response(new{choices=new[]{new{message=new{role="assistant",content=reply,tool_calls=new object[]{finish}},finish_reason="tool_calls"}},usage=new{prompt_tokens=140,completion_tokens=40}});
 }
 static Library SmartLibrary(string dataDir,out string documentId){var library=new Library(dataDir);library.SetAppMode("smart",true);documentId=library.CreateDocument("Headless source","md","","HEADLESS_FACT_42").id;return library;}
 static TranslationService FixtureService(Library library,Provider provider){var ai=new TranslationService(library.DataDir,()=>provider);ai.SaveConfig(new Dictionary<string,object>{{"endpoint","http://127.0.0.1:1234/v1"},{"model","fixture-model"},{"apiKey","synthetic-key-only"}});return ai;}
 static AgentHeadlessOptions Options(string dataDir,string goal,bool json){return new AgentHeadlessOptions{goal=goal,dataDir=dataDir,json=json};}
 static Dictionary<string,object> Map(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
 static List<Dictionary<string,object>> Lines(string text){return text.Split('\n').Where(line=>line.Trim().Length>0).Select(line=>Library.Json.Deserialize<Dictionary<string,object>>(line)).ToList();}
 static string Evidence(Dictionary<string,object> request){var match=System.Text.RegularExpressions.Regex.Match(Library.Json.Serialize(request["messages"]),"\\[kok_call_id=([a-f0-9]{32}) status=done\\]");return match.Success?match.Groups[1].Value:"";}
 static int RunCli(string[] args,out string stdout,out string stderr){
  var previousOut=Console.Out;var previousError=Console.Error;var outWriter=new StringWriter();var errorWriter=new StringWriter();
  Console.SetOut(outWriter);Console.SetError(errorWriter);
  int code;
  try{code=AgentHeadless.Run(args);}
  finally{Console.SetOut(previousOut);Console.SetError(previousError);}
  stdout=outWriter.ToString();stderr=errorWriter.ToString();return code;
 }
 public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
 static async Task RunAsync(string root,Action<bool,string> check){
  Directory.CreateDirectory(root);
  // 1) JSON mode: header first, ordered unique full events, terminal result line, exit 0, no stderr.
  string jsonDoc;var jsonLibrary=SmartLibrary(Path.Combine(root,"json"),out jsonDoc);
  var jsonProvider=new Provider((n,request,token)=>{
   if(n==1)return Task.FromResult(ToolReply("read_items",new{ids=new[]{jsonDoc}}));
   return Task.FromResult(FinishReply(Evidence(request),"HEADLESS_JSON_REPLY"));
  });
  var jsonAi=FixtureService(jsonLibrary,jsonProvider);var jsonOut=new StringWriter();var jsonErr=new StringWriter();
  int jsonCode=await AgentHeadless.Execute(Options(Path.Combine(root,"json"),"HEADLESS_JSON_GOAL",true),jsonLibrary,jsonAi,jsonOut,jsonErr,CancellationToken.None);
  var jsonLines=Lines(jsonOut.ToString());var header=jsonLines[0];var result=jsonLines[jsonLines.Count-1];
  check(jsonCode==0&&jsonErr.ToString()==""&&Convert.ToString(header["type"])=="header"&&Convert.ToInt32(header["version"])==1&&AgentJournal.ValidId(Convert.ToString(header["taskId"]))&&AgentJournal.ValidId(Convert.ToString(header["sessionId"]))&&Convert.ToString(header["model"])=="fixture-model"&&Convert.ToString(header["goal"])=="HEADLESS_JSON_GOAL","headless JSON mode starts with the header line, writes nothing to stderr and exits 0 on completion");
  var eventRows=jsonLines.Skip(1).Take(jsonLines.Count-2).Select(row=>Map(row["event"])).ToList();
  var eventIds=eventRows.Select(row=>Convert.ToString(row["id"])).ToList();var sequences=eventRows.Select(row=>Convert.ToInt32(row["sequence"])).ToList();var kinds=eventRows.Select(row=>Convert.ToString(row["kind"])).ToList();
  check(eventRows.Count>0&&jsonLines.Skip(1).Take(jsonLines.Count-2).All(row=>Convert.ToString(row["type"])=="event")&&eventIds.Distinct(StringComparer.Ordinal).Count()==eventIds.Count&&sequences.SequenceEqual(sequences.OrderBy(number=>number))&&eventRows.All(row=>row.ContainsKey("data")&&row.ContainsKey("created")&&row.ContainsKey("version"))&&kinds.Contains("assistant.reply")&&kinds.Contains("task.completed"),"headless JSON mode streams every journal event once, in order and with its full data");
  var resultUsage=Map(result["usage"]);var resultVerification=Map(result["verification"]);
  check(Convert.ToString(result["type"])=="result"&&Convert.ToString(result["status"])=="completed"&&Convert.ToString(result["reply"])=="HEADLESS_JSON_REPLY"&&Convert.ToInt32(resultUsage["requests"])==2&&Convert.ToString(resultVerification["status"])=="completed"&&jsonProvider.requests==2,"headless JSON mode ends with the result line containing reply, usage and verification");
  // 2) Print mode: stdout is exactly the final assistant text plus one newline.
  string printDoc;var printLibrary=SmartLibrary(Path.Combine(root,"print"),out printDoc);
  var printProvider=new Provider((n,request,token)=>Task.FromResult(n==1?ToolReply("read_items",new{ids=new[]{printDoc}}):FinishReply(Evidence(request),"HEADLESS_PRINT_REPLY")));
  var printAi=FixtureService(printLibrary,printProvider);var printOut=new StringWriter();var printErr=new StringWriter();
  int printCode=await AgentHeadless.Execute(Options(Path.Combine(root,"print"),"HEADLESS_PRINT_GOAL",false),printLibrary,printAi,printOut,printErr,CancellationToken.None);
  check(printCode==0&&printOut.ToString()=="HEADLESS_PRINT_REPLY\n"&&printErr.ToString()=="","headless print mode writes exactly the final assistant text plus a newline");
  // 3) read_only policy: a write proposal is never applied and the task fails without waiting for approval.
  string writeDoc;var writeLibrary=SmartLibrary(Path.Combine(root,"read-only"),out writeDoc);
  var writeProvider=new Provider((n,request,token)=>Task.FromResult(ToolReply("write_note",new{id=writeDoc,text="SHOULD_NOT_BE_WRITTEN"})));
  var writeAi=FixtureService(writeLibrary,writeProvider);var writeOut=new StringWriter();var writeErr=new StringWriter();
  int writeCode=await AgentHeadless.Execute(Options(Path.Combine(root,"read-only"),"HEADLESS_READ_ONLY_GOAL",true),writeLibrary,writeAi,writeOut,writeErr,CancellationToken.None);
  var writeLines=Lines(writeOut.ToString());var writeResult=writeLines[writeLines.Count-1];var writeEvents=writeLines.Where(row=>Convert.ToString(row["type"])=="event").Select(row=>Map(row["event"])).ToList();
  check(writeCode==1&&writeProvider.requests<=3&&writeLibrary.Find(writeDoc).notes==""&&Convert.ToString(writeResult["status"])=="failed"&&!writeEvents.Any(row=>Convert.ToString(row["kind"])=="approval.granted"||Convert.ToString(row["kind"])=="tool.started"||Convert.ToString(row["kind"])=="task.waiting"),"headless read-only policy refuses write proposals without applying them or waiting for approval (code="+writeCode+", requests="+writeProvider.requests+", status="+Convert.ToString(writeResult["status"])+")");
  // 4) Bad arguments exit 2: stderr in print mode, one JSON error line in JSON mode.
  string badOut,badErr;bool badPrint=true,badJson=true,badHelp=false,badParse=true;
  var sessionId=Guid.NewGuid().ToString("N");
  int missingGoal=RunCli(new[]{"agent","--print"},out badOut,out badErr);badPrint&=missingGoal==2&&badErr.Contains("--goal");
  int bothModes=RunCli(new[]{"agent","--goal","x","--print","--json"},out badOut,out badErr);var bothModesLines=Lines(badOut);badJson&=bothModes==2&&badErr==""&&bothModesLines.Count==1&&Convert.ToString(bothModesLines[0]["type"])=="error"&&Convert.ToString(bothModesLines[0]["message"]).Contains("--print");
  int noMode=RunCli(new[]{"agent","--goal","x"},out badOut,out badErr);badPrint&=noMode==2&&badErr.Contains("--print");
  int writes=RunCli(new[]{"agent","--goal","x","--print","--allow-writes"},out badOut,out badErr);badPrint&=writes==2&&badErr.Contains("--allow-writes");
  int approval=RunCli(new[]{"agent","--goal","x","--json","--approval","read_only"},out badOut,out badErr);var approvalLines=Lines(badOut);badJson&=approval==2&&badErr==""&&approvalLines.Count==1&&Convert.ToString(approvalLines[0]["type"])=="error"&&Convert.ToString(approvalLines[0]["message"]).Contains("--approval");
  int unknown=RunCli(new[]{"agent","--goal","x","--json","--unknown"},out badOut,out badErr);var unknownLines=Lines(badOut);badJson&=unknown==2&&badErr==""&&unknownLines.Count==1&&Convert.ToString(unknownLines[0]["type"])=="error"&&Convert.ToString(unknownLines[0]["message"]).Contains("未知参数");
  int help=RunCli(new[]{"agent","--help"},out badOut,out badErr);badHelp=help==0&&badErr==""&&badOut.Contains("--goal")&&badOut.Contains("--print")&&badOut.Contains("--json")&&badOut.Contains("退出码");
  AgentHeadlessOptions parsed,invalidParsed;string parseError;
  bool parsedOk=AgentHeadless.Parse(new[]{"agent","--goal","目标","--json","--scope","selected","--ids",new string('a',32),"--budget","6000","--request-limit","3","--context-tokens","8000","--protocol","tools","--timeout-seconds","45","--no-memory","--session",sessionId,"--title","标题"},out parsed,out parseError);
  bool parsedRejected=!AgentHeadless.Parse(new[]{"agent","--goal","x","--print","--allow-writes"},out invalidParsed,out parseError)&&parseError.Contains("--allow-writes");
  badParse=parsedOk&&parsed.ids.Count==1&&parsed.json&&parsed.scope=="selected"&&parsed.ids.Count==1&&parsed.budget==6000&&parsed.requestLimit==3&&parsed.contextTokens==8000&&parsed.protocol=="tools"&&parsed.timeoutSeconds==45&&parsed.sessionId==sessionId&&parsed.title=="标题"&&parsedRejected;
  check(badPrint&&badJson&&badHelp,"headless CLI rejects bad arguments with exit 2 (stderr in print mode, one JSON error line in JSON mode) and prints usage for --help");
  check(badParse,"the headless CLI parser accepts the documented option set and rejects write-enabling flags (ok="+parsedOk+", rejected="+parsedRejected+", json="+parsed.json+", scope="+parsed.scope+", ids="+parsed.ids.Count+", budget="+parsed.budget+", limit="+parsed.requestLimit+", ctx="+parsed.contextTokens+", protocol="+parsed.protocol+", timeout="+parsed.timeoutSeconds+", sessionMatch="+(parsed.sessionId==sessionId)+", title="+parsed.title+", error="+parseError+")");
  // 5) Timeout: a fixture that never answers in time stops the task and exits 1.
  string timeoutDoc;var timeoutLibrary=SmartLibrary(Path.Combine(root,"timeout"),out timeoutDoc);
  var timeoutProvider=new Provider(async (n,request,token)=>{await Task.Delay(3000,token);return FinishReply(Evidence(request),"HEADLESS_TIMEOUT_REPLY");});
  var timeoutAi=FixtureService(timeoutLibrary,timeoutProvider);var timeoutOut=new StringWriter();var timeoutErr=new StringWriter();
  var timeoutOptions=Options(Path.Combine(root,"timeout"),"HEADLESS_TIMEOUT_GOAL",true);timeoutOptions.timeoutSeconds=1;
  var timeoutWatch=Stopwatch.StartNew();
  int timeoutCode=await AgentHeadless.Execute(timeoutOptions,timeoutLibrary,timeoutAi,timeoutOut,timeoutErr,CancellationToken.None);
  timeoutWatch.Stop();
  var timeoutLines=Lines(timeoutOut.ToString());var timeoutResult=timeoutLines[timeoutLines.Count-1];
  check(timeoutCode==1&&timeoutWatch.Elapsed.TotalSeconds<8&&Convert.ToString(timeoutResult["type"])=="result"&&new[]{"cancelled","stopped"}.Contains(Convert.ToString(timeoutResult["status"])),"a timed-out headless task is stopped, flushed and exits 1");
  // 6) Ctrl+C cancellation stops the task and exits 130.
  string cancelDoc;var cancelLibrary=SmartLibrary(Path.Combine(root,"cancel"),out cancelDoc);
  var cancelProvider=new Provider(async (n,request,token)=>{await Task.Delay(3000,token);return FinishReply(Evidence(request),"HEADLESS_CANCEL_REPLY");});
  var cancelAi=FixtureService(cancelLibrary,cancelProvider);var cancelOut=new StringWriter();var cancelErr=new StringWriter();
  using(var cancellation=new CancellationTokenSource()){
   var cancelTask=AgentHeadless.Execute(Options(Path.Combine(root,"cancel"),"HEADLESS_CANCEL_GOAL",true),cancelLibrary,cancelAi,cancelOut,cancelErr,cancellation.Token);
   await Task.Delay(250);cancellation.Cancel();
   int cancelCode=await cancelTask;
   check(cancelCode==130,"Ctrl+C cancellation stops the headless task and exits 130");
  }
  // 7) Non-smart profile is rejected before any model call.
  var pureLibrary=new Library(Path.Combine(root,"pure"));var pureAi=new TranslationService(pureLibrary.DataDir);
  var pureOut=new StringWriter();var pureErr=new StringWriter();
  int pureCode=await AgentHeadless.Execute(Options(Path.Combine(root,"pure"),"HEADLESS_PURE_GOAL",false),pureLibrary,pureAi,pureOut,pureErr,CancellationToken.None);
  check(pureCode==2&&pureOut.ToString()==""&&pureErr.ToString().Contains("智能模式"),"headless CLI rejects a profile that is not in smart mode with exit 2");
  // 8) RPC mode: ping + shutdown over the deterministic stdin/stdout seam.
  {
   string pingDoc;var pingLibrary=SmartLibrary(Path.Combine(root,"rpc-ping"),out pingDoc);
   var pingAi=FixtureService(pingLibrary,new Provider((n,request,token)=>Task.FromResult(ToolReply("read_items",new{ids=new[]{pingDoc}}))));
   var pingOptions=new AgentHeadlessOptions{dataDir=Path.Combine(root,"rpc-ping"),rpc=true};
   var pingInput=new ScriptedInput();var pingOutput=new RecordingOutput();var pingErrors=new RecordingOutput();
   var pingRun=AgentHeadless.RunRpc(pingOptions,pingLibrary,pingAi,pingInput,pingOutput,pingErrors,CancellationToken.None);
   pingInput.Send(Cmd("1","ping"));
   var ping=WaitResponse(pingOutput,"1");var pingResult=ping==null?null:RpcMap(ping["result"]);
   pingInput.Send(Cmd("2","shutdown"));
   int pingCode=await pingRun;pingInput.CloseInput();
   check(ping!=null&&Bool(ping,"ok")&&pingResult!=null&&Convert.ToInt32(pingResult["version"])==1&&Convert.ToInt32(pingResult["pid"])>0&&pingResult["taskId"]==null&&pingCode==0&&pingErrors.Text=="","RPC ping responds with protocol version, pid and a null taskId, and shutdown exits 0");
  }
  // 9) RPC prompt: immediate response, ordered unique event lines, one result line, shutdown 0.
  {
   string promptDoc;var promptLibrary=SmartLibrary(Path.Combine(root,"rpc-prompt"),out promptDoc);
   var promptProvider=new Provider((n,request,token)=>Task.FromResult(n==1?ToolReply("read_items",new{ids=new[]{promptDoc}}):FinishReply(Evidence(request),"RPC_REPLY")));
   var promptAi=FixtureService(promptLibrary,promptProvider);
   var promptOptions=new AgentHeadlessOptions{dataDir=Path.Combine(root,"rpc-prompt"),rpc=true};
   var promptInput=new ScriptedInput();var promptOutput=new RecordingOutput();var promptErrors=new RecordingOutput();
   var promptRun=AgentHeadless.RunRpc(promptOptions,promptLibrary,promptAi,promptInput,promptOutput,promptErrors,CancellationToken.None);
   promptInput.Send(Cmd("10","prompt",new{goal="RPC_PROMPT_GOAL"}));
   var promptResponse=WaitResponse(promptOutput,"10");var promptStart=promptResponse==null?null:RpcMap(promptResponse["result"]);
   var promptResult=WaitResultLine(promptOutput);
   var promptLines=RpcLines(promptOutput.Text);
   var promptEvents=promptLines.Where(row=>Text(row,"type")=="event").Select(row=>RpcMap(row["event"])).ToList();
   var promptEventIds=promptEvents.Select(row=>Text(row,"id")).ToList();
   bool promptOrdered=true;for(int n=0;n+1<promptEvents.Count;n++)if(Convert.ToInt32(promptEvents[n]["sequence"])>Convert.ToInt32(promptEvents[n+1]["sequence"]))promptOrdered=false;
   promptInput.Send(Cmd("11","shutdown"));int promptCode=await promptRun;promptInput.CloseInput();
   check(promptResponse!=null&&Bool(promptResponse,"ok")&&promptStart!=null&&AgentJournal.ValidId(Text(promptStart,"taskId"))&&AgentJournal.ValidId(Text(promptStart,"sessionId"))&&promptEvents.Count>0&&promptEventIds.Distinct(StringComparer.Ordinal).Count()==promptEventIds.Count&&promptOrdered&&promptEvents.Any(row=>Text(row,"kind")=="assistant.reply")&&promptResult!=null&&Text(promptResult,"status")=="completed"&&Text(promptResult,"reply")=="RPC_REPLY"&&Convert.ToInt32(RpcMap(promptResult["usage"])["requests"])==2&&promptCode==0&&promptErrors.Text=="","RPC prompt answers immediately with taskId/sessionId, then streams ordered unique event lines and one completed result line, and shutdown exits 0");
  }
  // 10) Only one active task: a second prompt fails; a new prompt works after completion.
  {
   string busyDoc;var busyLibrary=SmartLibrary(Path.Combine(root,"rpc-busy"),out busyDoc);
   var busyProvider=new Provider(async (n,request,token)=>{await Task.Delay(500,token);return (n%2==1)?ToolReply("read_items",new{ids=new[]{busyDoc}}):FinishReply(Evidence(request),"RPC_BUSY_REPLY");});
   var busyAi=FixtureService(busyLibrary,busyProvider);
   var busyOptions=new AgentHeadlessOptions{dataDir=Path.Combine(root,"rpc-busy"),rpc=true};
   var busyInput=new ScriptedInput();var busyOutput=new RecordingOutput();var busyErrors=new RecordingOutput();
   var busyRun=AgentHeadless.RunRpc(busyOptions,busyLibrary,busyAi,busyInput,busyOutput,busyErrors,CancellationToken.None);
   busyInput.Send(Cmd("20","prompt",new{goal="RPC_BUSY_FIRST"}));
   var busyFirst=WaitResponse(busyOutput,"20");string busyFirstTask=busyFirst==null?"":Text(RpcMap(busyFirst["result"]),"taskId");
   busyInput.Send(Cmd("21","prompt",new{goal="RPC_BUSY_SECOND"}));
   var busySecond=WaitResponse(busyOutput,"21");
   var busyFirstResult=WaitResultLine(busyOutput);
   busyInput.Send(Cmd("22","prompt",new{goal="RPC_BUSY_THIRD"}));
   var busyThird=WaitResponse(busyOutput,"22");string busyThirdTask=busyThird==null?"":Text(RpcMap(busyThird["result"]),"taskId");
   var busyThirdResult=WaitResultLineAfter(busyOutput,1);
   busyInput.Send(Cmd("23","shutdown"));int busyCode=await busyRun;busyInput.CloseInput();
   check(busyFirst!=null&&Bool(busyFirst,"ok")&&busySecond!=null&&!Bool(busySecond,"ok")&&Convert.ToString(busySecond["error"]).Contains("已有任务在运行")&&busyThird!=null&&Bool(busyThird,"ok")&&busyThirdTask!=busyFirstTask&&busyFirstResult!=null&&busyThirdResult!=null&&busyCode==0,"RPC allows one active task: a second prompt is rejected with 已有任务在运行 and a new prompt works after the first task completes");
  }
  // 11) steer/follow_up reach the native queue and abort stops a running task.
  {
   string queueDoc;var queueLibrary=SmartLibrary(Path.Combine(root,"rpc-queue"),out queueDoc);
   var queueProvider=new Provider(async (n,request,token)=>{await Task.Delay(350,token);return (n%2==1)?ToolReply("read_items",new{ids=new[]{queueDoc}}):FinishReply(Evidence(request),"RPC_QUEUE_REPLY");});
   var queueAi=FixtureService(queueLibrary,queueProvider);
   var queueOptions=new AgentHeadlessOptions{dataDir=Path.Combine(root,"rpc-queue"),rpc=true};
   var queueInput=new ScriptedInput();var queueOutput=new RecordingOutput();var queueErrors=new RecordingOutput();
   var queueRun=AgentHeadless.RunRpc(queueOptions,queueLibrary,queueAi,queueInput,queueOutput,queueErrors,CancellationToken.None);
   queueInput.Send(Cmd("30","prompt",new{goal="RPC_QUEUE_GOAL"}));
   var queueStart=WaitResponse(queueOutput,"30");string queueTask=queueStart==null?"":Text(RpcMap(queueStart["result"]),"taskId");
   queueInput.Send(Cmd("31","steer",new{taskId=queueTask,text="RPC_STEER_TEXT"}));
   var steerResponse=WaitResponse(queueOutput,"31");
   queueInput.Send(Cmd("32","follow_up",new{taskId=queueTask,text="RPC_FOLLOW_TEXT"}));
   var followResponse=WaitResponse(queueOutput,"32");
   var queueResult=WaitResultLine(queueOutput);
   var queueEventTexts=RpcLines(queueOutput.Text).Where(row=>Text(row,"type")=="event").Select(row=>Library.Json.Serialize(row["event"])).ToList();
   bool queueVisible=queueEventTexts.Any(text=>text.Contains("input.queued"))&&queueEventTexts.Any(text=>text.Contains("RPC_STEER_TEXT")||text.Contains("RPC_FOLLOW_TEXT"));
   queueInput.Send(Cmd("33","shutdown"));int queueCode=await queueRun;queueInput.CloseInput();
   string abortDoc;var abortLibrary=SmartLibrary(Path.Combine(root,"rpc-abort"),out abortDoc);
   var abortProvider=new Provider(async (n,request,token)=>{await Task.Delay(4000,token);return FinishReply("","RPC_ABORT_REPLY");});
   var abortAi=FixtureService(abortLibrary,abortProvider);
   var abortOptions=new AgentHeadlessOptions{dataDir=Path.Combine(root,"rpc-abort"),rpc=true};
   var abortInput=new ScriptedInput();var abortOutput=new RecordingOutput();var abortErrors=new RecordingOutput();
   var abortRun=AgentHeadless.RunRpc(abortOptions,abortLibrary,abortAi,abortInput,abortOutput,abortErrors,CancellationToken.None);
   abortInput.Send(Cmd("40","prompt",new{goal="RPC_ABORT_GOAL"}));
   var abortStart=WaitResponse(abortOutput,"40");string abortTask=abortStart==null?"":Text(RpcMap(abortStart["result"]),"taskId");
   abortInput.Send(Cmd("41","abort",new{taskId=abortTask,cancel=true}));
   var abortResponse=WaitResponse(abortOutput,"41");var abortResult=WaitResultLine(abortOutput);
   abortInput.Send(Cmd("42","shutdown"));int abortCode=await abortRun;abortInput.CloseInput();
   check(steerResponse!=null&&Bool(steerResponse,"ok")&&followResponse!=null&&Bool(followResponse,"ok")&&queueResult!=null&&queueVisible&&queueCode==0&&abortResponse!=null&&Bool(abortResponse,"ok")&&abortResult!=null&&new[]{"cancelled","stopped"}.Contains(Text(abortResult,"status"))&&abortCode==0,"RPC steer/follow_up surface through later event lines and abort stops a running task with a cancelled/stopped result line");
  }
  // 12) state/compact shapes, unknown method and malformed JSON keep the loop alive.
  {
   string miscDoc;var miscLibrary=SmartLibrary(Path.Combine(root,"rpc-misc"),out miscDoc);
   var miscProvider=new Provider(async (n,request,token)=>{
    await Task.Delay(700,token);
    if(!request.ContainsKey("tools"))return SummaryReply("## Goal\nRPC 压缩摘要内容。");
    return (n%2==1)?ToolReply("read_items",new{ids=new[]{miscDoc}}):FinishReply(Evidence(request),"RPC_MISC_REPLY");
   });
   var miscAi=FixtureService(miscLibrary,miscProvider);
   var miscOptions=new AgentHeadlessOptions{dataDir=Path.Combine(root,"rpc-misc"),rpc=true};
   var miscInput=new ScriptedInput();var miscOutput=new RecordingOutput();var miscErrors=new RecordingOutput();
   var miscRun=AgentHeadless.RunRpc(miscOptions,miscLibrary,miscAi,miscInput,miscOutput,miscErrors,CancellationToken.None);
   miscInput.Send(Cmd("50","prompt",new{goal="RPC_MISC_GOAL"}));
   var miscStart=WaitResponse(miscOutput,"50");string miscTask=miscStart==null?"":Text(RpcMap(miscStart["result"]),"taskId");
   miscInput.Send(Cmd("51","state",new{taskId=miscTask}));
   var stateResponse=WaitResponse(miscOutput,"51");
   miscInput.Send(Cmd("52","compact",new{taskId=miscTask,instructions="RPC_INSTRUCTIONS"}));
   var compactResponse=WaitResponse(miscOutput,"52");
   miscInput.Send(Cmd("53","not_a_method"));
   var unknownResponse=WaitResponse(miscOutput,"53");
   miscInput.Send("this is not json");
   var malformedLine=WaitLine(miscOutput,row=>Text(row,"type")=="error");
   miscInput.Send(Cmd("54","ping"));
   var pingAfter=WaitResponse(miscOutput,"54");
   var miscResult=WaitResultLine(miscOutput);
   miscInput.Send(Cmd("55","shutdown"));int miscCode=await miscRun;miscInput.CloseInput();
   var stateValue=stateResponse==null?null:RpcMap(stateResponse["result"]);var compactValue=compactResponse==null?null:RpcMap(compactResponse["result"]);
   check(stateResponse!=null&&Bool(stateResponse,"ok")&&stateValue!=null&&stateValue.ContainsKey("status")&&stateValue.ContainsKey("running")&&compactResponse!=null&&Bool(compactResponse,"ok")&&compactValue!=null&&compactValue.ContainsKey("status")&&unknownResponse!=null&&!Bool(unknownResponse,"ok")&&Convert.ToString(unknownResponse["error"])=="未知命令。"&&malformedLine!=null&&Text(malformedLine,"message")!=null&&pingAfter!=null&&Bool(pingAfter,"ok")&&miscResult!=null&&miscCode==0,"RPC state/compact return task snapshots, unknown methods fail with 未知命令 without stopping the loop and malformed JSON emits one error line then keeps serving");
  }
  // 13) EOF handling and CLI combination rejection.
  {
   string eofDoc;var eofLibrary=SmartLibrary(Path.Combine(root,"rpc-eof"),out eofDoc);
   var eofProvider=new Provider((n,request,token)=>Task.FromResult(n==1?ToolReply("read_items",new{ids=new[]{eofDoc}}):FinishReply(Evidence(request),"RPC_EOF_REPLY")));
   var eofAi=FixtureService(eofLibrary,eofProvider);
   var eofOptions=new AgentHeadlessOptions{dataDir=Path.Combine(root,"rpc-eof"),rpc=true};
   var idleOutput=new RecordingOutput();var idleErrors=new RecordingOutput();
   int idleCode=await AgentHeadless.RunRpc(eofOptions,eofLibrary,eofAi,new StringReader(""),idleOutput,idleErrors,CancellationToken.None);
   var eofInput=new ScriptedInput();var eofOutput=new RecordingOutput();var eofErrors=new RecordingOutput();
   var eofRun=AgentHeadless.RunRpc(eofOptions,eofLibrary,eofAi,eofInput,eofOutput,eofErrors,CancellationToken.None);
   eofInput.Send(Cmd("60","prompt",new{goal="RPC_EOF_GOAL"}));WaitResponse(eofOutput,"60");eofInput.CloseInput();
   int eofCode=await eofRun;var eofResult=WaitLine(eofOutput,row=>Text(row,"type")=="result",3000);
   string rpcBadOut,rpcBadErr;int goalRpc=RunCli(new[]{"agent","--rpc","--goal","x"},out rpcBadOut,out rpcBadErr);var goalLines=Lines(rpcBadOut);
   int jsonRpc=RunCli(new[]{"agent","--rpc","--json"},out rpcBadOut,out rpcBadErr);var rpcJsonLines=Lines(rpcBadOut);
   int printRpc=RunCli(new[]{"agent","--rpc","--print","--goal","x"},out rpcBadOut,out rpcBadErr);
   string rpcHelpOut,rpcHelpErr;int rpcHelp=RunCli(new[]{"agent","--help"},out rpcHelpOut,out rpcHelpErr);
   AgentHeadlessOptions parsedRpc;string rpcParseError;
   bool rpcParsed=AgentHeadless.Parse(new[]{"agent","--rpc","--session",Guid.NewGuid().ToString("N"),"--budget","6000","--request-limit","2","--context-tokens","8000","--protocol","tools","--timeout-seconds","30","--title","RPC"},out parsedRpc,out rpcParseError);
   check(idleCode==0&&idleOutput.Text==""&&eofCode==130&&eofResult!=null&&goalRpc==2&&rpcBadErr==""&&goalLines.Count==1&&Text(goalLines[0],"type")=="error"&&jsonRpc==2&&rpcJsonLines.Count==1&&Text(rpcJsonLines[0],"type")=="error"&&printRpc==2&&rpcHelp==0&&rpcHelpOut.Contains("--rpc")&&rpcHelpOut.Contains("JSONL")&&rpcParsed&&parsedRpc.rpc&&parsedRpc.budget==6000&&parsedRpc.requestLimit==2&&parsedRpc.contextTokens==8000,"RPC exits 0 on idle EOF and 130 when stdin closes mid-task, --rpc combined with --goal/--print/--json is rejected with one JSON error line and exit 2 before serving, and --help documents the mode");
  }
 }
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
 // The native host owns permission and lifetime. The renderer only hosts the WASM interpreter.
 // QuickJS is synchronous-only, so read-tool callbacks use a replay cache: the worker aborts a round
 // with pending requests, the renderer asks native for the results, then re-runs the same code with them.
 public sealed class AgentSandbox {
  sealed class SandboxRun {
   public string taskId;
   public readonly TaskCompletionSource<Dictionary<string,object>> completion=new TaskCompletionSource<Dictionary<string,object>>(TaskCreationOptions.RunContinuationsAsynchronously);
   public int toolCalls,toolChars;
  }
  internal static readonly string[] ToolNames={"search_library","read_items","recall_result","search_memory","list_collections"};
  internal const int MaxToolsPerBatch=8,MaxToolCalls=24,MaxToolRequestChars=8000,MaxToolResultChars=8000,MaxToolTotalChars=32000;
  readonly Action<string,object> notify;readonly Action validate;readonly object gate=new object();
  readonly Dictionary<string,SandboxRun> pending=new Dictionary<string,SandboxRun>();
  // Scoped read-only batch executor supplied by AgentExecution (task id -> results for normalized requests).
  internal Func<string,Dictionary<string,object>[],CancellationToken,Task<List<Dictionary<string,object>>>> ToolBatch;
  public AgentSandbox(Action<string,object> notify,Action validate,Func<string,Dictionary<string,object>[],CancellationToken,Task<List<Dictionary<string,object>>>> toolBatch=null){this.notify=notify;this.validate=validate;ToolBatch=toolBatch;}
  public async Task<Dictionary<string,object>> Run(string taskId,string code,string input,CancellationToken token){
   token.ThrowIfCancellationRequested();validate();if(String.IsNullOrWhiteSpace(code)||code.Length>8000||input==null||input.Length>16000)throw new AgentFailure("sandbox","隔离计算的代码或输入超过限制。");try{Library.Json.DeserializeObject(input);}catch{throw new AgentFailure("sandbox","隔离计算输入必须是 JSON。");}
   string ticket=Guid.NewGuid().ToString("N");var run=new SandboxRun{taskId=taskId};lock(gate){if(pending.Count>0)throw new AgentFailure("sandbox","已有隔离计算正在运行。");pending[ticket]=run;}
   using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
    timeout.CancelAfter(10000);using(timeout.Token.Register(()=>run.completion.TrySetCanceled()))try{notify("agentSandboxRun",new{ticket,taskId,code,input});var result=await run.completion.Task;validate();token.ThrowIfCancellationRequested();if(!Convert.ToBoolean(result["ok"]))throw new AgentToolFailure("sandbox","隔离计算失败，请查看本地结果。",new Dictionary<string,object>{{"text",Convert.ToString(result["output"])}});return new Dictionary<string,object>{{"text",Convert.ToString(result["output"])},{"artifact",true},{"sandbox","quickjs-wasm"}};}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;throw new AgentFailure("sandbox_timeout","隔离计算超时，已停止。");}finally{lock(gate)pending.Remove(ticket);notify("agentSandboxCancel",new{ticket});}
   }
  }
  public bool Complete(string ticket,bool ok,string output){validate();if(!AgentJournal.ValidId(ticket)||output==null||output.Length>16000)throw new AgentFailure("sandbox","隔离计算返回内容无效。");lock(gate){SandboxRun run;if(!pending.TryGetValue(ticket,out run))return false;return run.completion.TrySetResult(new Dictionary<string,object>{{"ok",ok},{"output",output}});}}
  // Executes one batch of read-only tool requests for the active run. Protocol violations (ticket,
  // whitelist, batch/cumulative caps, oversized args) fail the batch; per-item execution errors are
  // returned as {index,text,isError:true} so the isolated code may catch them.
  internal async Task<Dictionary<string,object>> Tools(string ticket,List<Dictionary<string,object>> requests,CancellationToken token){
   token.ThrowIfCancellationRequested();validate();
   if(!AgentJournal.ValidId(ticket))throw new AgentFailure("sandbox","隔离计算票据无效。");
   SandboxRun run;lock(gate){if(!pending.TryGetValue(ticket,out run))throw new AgentFailure("sandbox","隔离计算票据无效。");}
   if(requests!=null&&requests.Count>MaxToolsPerBatch)throw new AgentFailure("sandbox","隔离计算每次最多请求 8 个工具。");
   var batch=new List<Dictionary<string,object>>();var indexes=new HashSet<int>();
   if(requests!=null)foreach(var request in requests){
    if(request==null)throw new AgentFailure("sandbox","隔离计算工具请求无效。");
    int index;try{index=Convert.ToInt32(request.ContainsKey("index")?request["index"]:null);}catch{throw new AgentFailure("sandbox","隔离计算工具请求序号无效。");}
    if(index<0||index>=MaxToolCalls||!indexes.Add(index))throw new AgentFailure("sandbox","隔离计算工具请求序号无效。");
    string name=ModuleContext.Text(request,"name");
    if(!ToolNames.Contains(name))throw new AgentFailure("sandbox","隔离计算不能调用工具："+name+"。");
    var args=request.ContainsKey("args")?request["args"] as Dictionary<string,object>:null;args=args??new Dictionary<string,object>();
    if(Library.Json.Serialize(args).Length>MaxToolRequestChars)throw new AgentFailure("sandbox","隔离计算工具参数超过 8000 字符限制。");
    batch.Add(new Dictionary<string,object>{{"index",index},{"name",name},{"args",args}});
   }
   lock(gate){if(run.toolCalls+batch.Count>MaxToolCalls)throw new AgentFailure("sandbox","隔离计算的工具调用超过 24 次上限。");run.toolCalls+=batch.Count;}
   var executor=ToolBatch;if(executor==null)throw new AgentFailure("sandbox","隔离计算工具调用不可用。");
   var executed=await executor(run.taskId,batch.ToArray(),token);
   token.ThrowIfCancellationRequested();
   var results=new List<Dictionary<string,object>>();
   lock(gate){
    if(!pending.ContainsKey(ticket))throw new AgentFailure("sandbox","隔离计算已结束。");
    foreach(var item in executed??new List<Dictionary<string,object>>()){
     int index;try{index=Convert.ToInt32(item.ContainsKey("index")?item["index"]:0);}catch{index=0;}
     string text=item.ContainsKey("text")?Convert.ToString(item["text"]):"";if(text==null)text="";
     bool isError=item.ContainsKey("isError")&&item["isError"]!=null&&Convert.ToBoolean(item["isError"]);
     int remaining=Math.Max(0,MaxToolTotalChars-run.toolChars),limit=Math.Min(MaxToolResultChars,remaining);
     if(limit<=0){text="[sandbox tool result budget exhausted]";isError=true;}
     else if(text.Length>limit){text=limit>32?text.Substring(0,limit-32)+"\n[sandbox tool result truncated]":text.Substring(0,limit);}
     run.toolChars+=Math.Min(text.Length,Math.Max(0,MaxToolTotalChars-run.toolChars));
     results.Add(new Dictionary<string,object>{{"index",index},{"text",text},{"isError",isError}});
    }
   }
   return new Dictionary<string,object>{{"results",results}};
  }
  public void Stop(){lock(gate){foreach(var run in pending.Values)run.completion.TrySetCanceled();pending.Clear();}notify("agentSandboxCancel",new{ticket=""});}
 }
}

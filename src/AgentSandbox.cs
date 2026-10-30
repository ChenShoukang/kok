using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
 // The native host owns permission and lifetime. The renderer only hosts the WASM interpreter.
 public sealed class AgentSandbox {
  readonly Action<string,object> notify;readonly Action validate;readonly object gate=new object();
  readonly Dictionary<string,TaskCompletionSource<Dictionary<string,object>>> pending=new Dictionary<string,TaskCompletionSource<Dictionary<string,object>>>();
  public AgentSandbox(Action<string,object> notify,Action validate){this.notify=notify;this.validate=validate;}
  public async Task<Dictionary<string,object>> Run(string taskId,string code,string input,CancellationToken token){
   token.ThrowIfCancellationRequested();validate();if(String.IsNullOrWhiteSpace(code)||code.Length>8000||input==null||input.Length>16000)throw new AgentFailure("sandbox","隔离计算的代码或输入超过限制。");try{Library.Json.DeserializeObject(input);}catch{throw new AgentFailure("sandbox","隔离计算输入必须是 JSON。");}
   string ticket=Guid.NewGuid().ToString("N");var completion=new TaskCompletionSource<Dictionary<string,object>>(TaskCreationOptions.RunContinuationsAsynchronously);lock(gate){if(pending.Count>0)throw new AgentFailure("sandbox","已有隔离计算正在运行。");pending[ticket]=completion;}
   using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
    timeout.CancelAfter(10000);using(timeout.Token.Register(()=>completion.TrySetCanceled()))try{notify("agentSandboxRun",new{ticket,taskId,code,input});var result=await completion.Task;validate();token.ThrowIfCancellationRequested();if(!Convert.ToBoolean(result["ok"]))throw new AgentToolFailure("sandbox","隔离计算失败，请查看本地结果。",new Dictionary<string,object>{{"text",Convert.ToString(result["output"])}});return new Dictionary<string,object>{{"text",Convert.ToString(result["output"])},{"artifact",true},{"sandbox","quickjs-wasm"}};}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;throw new AgentFailure("sandbox_timeout","隔离计算超时，已停止。");}finally{lock(gate)pending.Remove(ticket);notify("agentSandboxCancel",new{ticket});}
   }
  }
  public bool Complete(string ticket,bool ok,string output){validate();if(!AgentJournal.ValidId(ticket)||output==null||output.Length>16000)throw new AgentFailure("sandbox","隔离计算返回内容无效。");lock(gate){TaskCompletionSource<Dictionary<string,object>> completion;if(!pending.TryGetValue(ticket,out completion))return false;return completion.TrySetResult(new Dictionary<string,object>{{"ok",ok},{"output",output}});}}
  public void Stop(){lock(gate){foreach(var task in pending.Values)task.TrySetCanceled();pending.Clear();}notify("agentSandboxCancel",new{ticket=""});}
 }
}

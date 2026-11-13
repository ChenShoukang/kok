using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
    // Long-lived JSONL worker. The SDK owns its model loop; this port returns native-tool interrupts
    // to kok, where scope, approval, actual writes, evidence and undo remain authoritative.
    internal sealed class AgentResearchKernel : IDisposable {
        readonly Process process;
        readonly StreamWriter input;
        readonly SemaphoreSlim steps=new SemaphoreSlim(1,1);
        bool disposed;
        internal static bool Available(string appDirectory){
            string root=Path.Combine(appDirectory,"runtime","research");
            return File.Exists(Path.Combine(root,"node.exe"))&&File.Exists(Path.Combine(root,"worker.mjs"));
        }
        internal AgentResearchKernel(string appDirectory){
            string root=Path.Combine(appDirectory,"runtime","research");
            if(!Available(appDirectory))throw new AgentFailure("runtime","研究内核运行组件缺失，请重新构建或完整解压 v0.13.0 安装包。旧任务仍使用兼容内核。",true);
            var start=new ProcessStartInfo(Path.Combine(root,"node.exe"),"\""+Path.Combine(root,"worker.mjs")+"\""){
                WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,
                RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
                StandardOutputEncoding=new UTF8Encoding(false),StandardErrorEncoding=new UTF8Encoding(false)
            };
            // Provider keys enter only over stdin. Disable ambient tracing for local private source text.
            start.EnvironmentVariables.Remove("LANGCHAIN_API_KEY");start.EnvironmentVariables.Remove("LANGSMITH_API_KEY");
            start.EnvironmentVariables["LANGCHAIN_TRACING_V2"]="false";start.EnvironmentVariables["LANGSMITH_TRACING"]="false";
            process=new Process{StartInfo=start};
            process.ErrorDataReceived+=(s,e)=>{};
            if(!process.Start())throw new AgentFailure("runtime","研究内核无法启动。",true);
            process.BeginErrorReadLine();
            input=new StreamWriter(process.StandardInput.BaseStream,new UTF8Encoding(false)){AutoFlush=true};
        }
        void Send(object message){input.WriteLine(Library.Json.Serialize(message));input.Flush();}
        internal async Task<Dictionary<string,object>> Step(Dictionary<string,object> input,Action<Dictionary<string,object>> onModelStart,Action<Dictionary<string,object>> onEvent,CancellationToken token){
            await steps.WaitAsync(token);
            try{
                token.ThrowIfCancellationRequested();if(disposed)throw new ObjectDisposedException("research kernel");
                using(token.Register(Dispose)){
                    Send(input);
                    for(;;){
                        string line=await process.StandardOutput.ReadLineAsync();token.ThrowIfCancellationRequested();
                        if(line==null)throw new AgentFailure("runtime","研究内核已退出，已有资料读取和操作记录已保留，请继续任务。",true);
                        if(line.Length>2000000)throw new AgentFailure("runtime","研究内核结果超过大小上限。",false);
                        Dictionary<string,object> message;
                        try{message=Library.Json.Deserialize<Dictionary<string,object>>(line);}catch{throw new AgentFailure("runtime","研究内核返回了无效事件。",false);}
                        string type=AgentTaskState.Get<string>(message,"type");
                        if(type=="model_start"){
                            try{onModelStart(message);Send(new{type="model_permit",requestId=AgentTaskState.Get<string>(message,"requestId"),allow=true});}
                            catch{try{Send(new{type="model_permit",requestId=AgentTaskState.Get<string>(message,"requestId"),allow=false,reason="宿主请求或费用上限已达到。"});}catch{}Dispose();throw;}
                        }else if(type=="result")return message;
                        else if(type=="error")throw new AgentFailure("service",AgentTaskState.Get<string>(message,"message"),true);
                        else onEvent(message);
                    }
                }
            }catch(Exception)when(token.IsCancellationRequested){token.ThrowIfCancellationRequested();throw;}
            finally{steps.Release();}
        }
        public void Dispose(){
            if(disposed)return;disposed=true;
            try{if(!process.HasExited)process.Kill();}catch{}
            try{input.Dispose();}catch{}
            try{process.Dispose();}catch{}
        }
    }
}

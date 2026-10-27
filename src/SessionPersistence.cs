using System;
using System.Threading;
using System.Threading.Tasks;

namespace kok {
    // A renderer may stop answering while the native window is still usable.
    // Bound the read; never treat a timeout as an empty/successful snapshot.
    public static class SessionPersistence {
        public static async Task<string> ReadSnapshot(Task<string> read,int timeoutMilliseconds) {
            if(read==null)throw new ArgumentNullException("read");
            if(timeoutMilliseconds<1)throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            using(var timeout=new CancellationTokenSource()){
                var deadline=Task.Delay(timeoutMilliseconds,timeout.Token);
                if(await Task.WhenAny(read,deadline)!=read){
                    // WebView2 does not offer cancellation for ExecuteScriptAsync.
                    // Observe a later fault when the renderer/window is disposed.
                    ObserveLateFailure(read);
                    throw new TimeoutException("窗口暂时无响应，未完成本次保存。");
                }
                timeout.Cancel();return await read;
            }
        }
        static void ObserveLateFailure(Task read){read.ContinueWith(task=>{var observed=task.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);}
    }
}

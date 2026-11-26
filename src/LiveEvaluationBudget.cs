using System;
using System.IO;
using System.Text;
using System.Threading;
namespace kok {
 // Activated only by an explicitly configured, silent desktop test process.
 internal static class LiveEvaluationBudget {
  static string path;static int maximum;static Mutex mutex;
  internal const int MaxRequests=660;
  internal static void Initialize(string[] args){
   if(!Array.Exists(args,a=>a=="--debug")||!Array.Exists(args,a=>a=="--test-silent")||Environment.GetEnvironmentVariable("KOK_LIVE_AI")!="1")return;
   string file=Environment.GetEnvironmentVariable("KOK_LIVE_LEDGER");int limit;
   if(String.IsNullOrWhiteSpace(file)||!Int32.TryParse(Environment.GetEnvironmentVariable("KOK_LIVE_MAX_REQUESTS"),out limit)||limit<1||limit>MaxRequests)
    throw new InvalidOperationException("An explicit live evaluation ledger and a valid request limit are required.");
   path=Path.GetFullPath(file);maximum=limit;mutex=new Mutex(false,"Local\\kok-live-"+Library.StudyHash(path).Substring(0,32));
  }
  internal static void Spend(){
   if(path==null)return;
   bool owned=false;
   try{
    try{owned=mutex.WaitOne(5000);}catch(AbandonedMutexException){owned=true;}
    if(!owned)throw new AgentFailure("budget","Live evaluation budget is busy; no request was sent.");
    int used=0;
    if(File.Exists(path)&&(!Int32.TryParse(File.ReadAllText(path,Encoding.UTF8),out used)||used<0))throw new AgentFailure("budget","Live evaluation ledger is invalid; no request was sent.");
    if(used>=maximum)throw new AgentFailure("budget","The authorized live evaluation request limit has been reached.");
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    string next=path+".tmp";
    File.WriteAllText(next,(used+1).ToString(),new UTF8Encoding(false));
    if(File.Exists(path))File.Replace(next,path,null);else File.Move(next,path);
   }finally{if(owned)mutex.ReleaseMutex();}
  }
 }
}

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using kok;

class SessionPersistenceTests {
    public static void Run(Action<bool,string> check){
        check(SessionPersistence.ReadSnapshot(Task.FromResult("[]"),1000).GetAwaiter().GetResult()=="[]","ready window snapshots are returned unchanged");
        var pending=new TaskCompletionSource<string>();bool timedOut=false;
        try{SessionPersistence.ReadSnapshot(pending.Task,25).GetAwaiter().GetResult();}catch(TimeoutException){timedOut=true;}
        check(timedOut&&!pending.Task.IsCompleted,"an unresponsive renderer times out without fabricating a saved snapshot or cancelling the renderer task");
        pending.SetResult("late snapshot");check(pending.Task.Result=="late snapshot","late renderer completion remains safe after the close request has timed out");
        bool fault=false;try{SessionPersistence.ReadSnapshot(Task.FromException<string>(new IOException("fixture failure")),1000).GetAwaiter().GetResult();}catch(IOException){fault=true;}
        check(fault,"renderer failures propagate to the close confirmation path");
        bool cancelled=false;try{SessionPersistence.ReadSnapshot(Task.FromCanceled<string>(new CancellationToken(true)),1000).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
        check(cancelled,"cancelled snapshot reads never become successful empty saves");
    }
}

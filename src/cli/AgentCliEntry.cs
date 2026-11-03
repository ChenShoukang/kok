using System;

namespace kok {
    // Console-subsystem launcher for the headless Agent CLI. A /target:winexe host is not
    // waited on by cmd/PowerShell, so scripts and CI should call kok-agent.exe to get a
    // real process exit code and a stable stdout/stderr stream. It forwards to the same
    // AgentHeadless entry point used by `kok.exe agent ...`.
    public static class AgentCliEntry {
        [STAThread]
        public static int Main(string[] args) {
            if(args==null||args.Length==0)return AgentHeadless.Run(new[]{"agent","--help"});
            var full=new string[args.Length+1];
            full[0]="agent";
            Array.Copy(args,0,full,1,args.Length);
            return AgentHeadless.Run(full);
        }
    }
}

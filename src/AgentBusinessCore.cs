using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

namespace kok {
    public sealed class AgentBusinessEnvironment {
        public bool workspace,rendering;
        public List<string> tools=new List<string>(),selectedFormats=new List<string>();
    }
    internal sealed class AgentBusinessPlan {
        public int version;public string language="";
        public AgentTaskContract contract;
        public List<string> tools=new List<string>();
        public AgentBusinessContext context=new AgentBusinessContext();
        public bool coding,renderingRequired;
    }
    internal sealed class AgentBusinessContext {
        public string taskId="",goal="",domain="",workspaceId="";
        public List<string> files=new List<string>();
    }

    // Transport and native permission facts only. Business decisions run in the bundled TypeScript core.
    internal static class AgentBusinessCore {
        static string application=AppDomain.CurrentDomain.BaseDirectory;
        internal static string NodePath {get{return Path.Combine(application,"runtime","research","node.exe");}}
        internal static void Configure(string directory){if(!String.IsNullOrWhiteSpace(directory))application=Path.GetFullPath(directory);}
        internal static bool Available(string directory){return File.Exists(Path.Combine(directory,"runtime","research","business.mjs"))&&File.Exists(Path.Combine(directory,"runtime","research","node.exe"));}
        internal static T Invoke<T>(string operation,object input){
            string directory=Path.Combine(application,"runtime","research");
            if(!Available(application))throw new AgentFailure("runtime","TypeScript Agent 主体组件缺失，请完整解压新版安装包。",true);
            var start=new ProcessStartInfo{FileName=Path.Combine(directory,"node.exe"),Arguments="--max-old-space-size=160 "+AgentExecution.QuoteArgument(Path.Combine(directory,"business.mjs")),WorkingDirectory=directory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=new UTF8Encoding(false),StandardErrorEncoding=new UTF8Encoding(false)};
            start.EnvironmentVariables.Remove("NODE_OPTIONS");start.EnvironmentVariables.Remove("NODE_PATH");
            using(var process=Process.Start(start)){
                var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
                try{
                    using(var writer=new StreamWriter(process.StandardInput.BaseStream,new UTF8Encoding(false))){writer.WriteLine(Library.Json.Serialize(new{version=1,operation=operation,input=input}));writer.Flush();
                        if(!process.WaitForExit(8000))throw new TimeoutException("TypeScript Agent 业务计算超时。");}
                    string result=output.GetAwaiter().GetResult();
                    if(process.ExitCode!=0||result.Length>4000000)throw new AgentFailure("runtime","TypeScript Agent 主体未返回有效结果。",true);
                    var response=Library.Json.Deserialize<Dictionary<string,object>>(result.TrimStart('\uFEFF'));
                    if(AgentTaskState.Get<int>(response,"version")!=1||!AgentTaskState.Get<bool>(response,"ok"))throw new AgentFailure("runtime","TypeScript Agent 业务规则执行失败："+AgentWorkspaceTools.Redact(ModuleContext.Text(response,"error")),true);
                    return AgentTaskState.Get<T>(response,"value");
                }finally{if(!process.HasExited)try{process.Kill();}catch(InvalidOperationException){}if(process.HasExited)errors.GetAwaiter().GetResult();}
            }
        }

        internal static void Prepare(AgentTaskOptions options,Library library,string directory){
            Configure(directory);if(!Available(application))throw new AgentFailure("runtime","TypeScript Agent 主体组件缺失，请完整解压新版安装包。",true);
            options.businessVersion=1;
            options.businessEnvironment=new AgentBusinessEnvironment{
                workspace=AgentExecution.Configured(library),rendering=PresentationRenderer.Available,
                tools=AgentRules.Tools.Where(tool=>ModuleRegistry.Builtins.ToolEnabled(tool.Name,library)).Select(tool=>tool.Name).ToList(),
                selectedFormats=options.scope.mode=="selected"?(options.scope.ids??new List<string>()).Select(id=>library.Find(id)).Where(item=>item!=null).Select(item=>item.ext).Distinct().ToList():new List<string>()};
            options.businessCache=null;options.businessCacheKey=null;
            Plan(options,null);
        }
        internal static AgentBusinessPlan Plan(AgentTaskOptions options,IEnumerable<string> adjustments){
            if(options.businessEnvironment==null)throw new AgentFailure("runtime","Agent 业务环境尚未初始化。",false);
            var input=new{goal=options.goal??"",adjustments=(adjustments??Enumerable.Empty<string>()).ToArray(),allowed=options.allowed??new List<string>(),intent=options.intent,approvalPolicy=options.approvalPolicy,scope=options.scope,
                features=new{memory=options.useMemory,delegation=options.allowDelegation,sandbox=options.allowSandbox,search=!String.IsNullOrEmpty(options.searchEndpoint),parent=!String.IsNullOrEmpty(options.parentTaskId)},
                explicitTools=options.allowedToolsAuto?null:options.allowedTools,environment=options.businessEnvironment,
                context=new{taskId=options.continuationTaskId??"",goal=options.continuationGoal??"",domain=options.continuationDomain??"",workspaceId=options.continuationWorkspaceId??"",files=options.continuationFiles??new List<string>()}};
            string key=Library.Json.Serialize(input);
            lock(options){
                if(options.businessCache!=null&&options.businessCacheKey==key)return options.businessCache;
                var plan=Invoke<AgentBusinessPlan>("plan",input);
                if(plan.version!=1||plan.language!="TypeScript"||plan.contract==null||plan.tools==null)throw new AgentFailure("runtime","Agent 业务主体协议不匹配。",false);
                // The portable core cannot widen the native source scope or expose unregistered capabilities.
                plan.contract.RequiredSourceIds=(plan.contract.RequiredSourceIds??new List<string>()).Where((options.allowed??new List<string>()).Contains).Distinct().ToList();
                plan.tools=plan.tools.Where(options.businessEnvironment.tools.Contains).Distinct().ToList();
                options.businessCacheKey=key;options.businessCache=plan;return plan;
            }
        }
    }
}

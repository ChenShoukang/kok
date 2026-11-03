using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
    // Headless `kok agent` entry point: analysis-only tasks without a window or WebView.
    // Buffers are passed to Execute so native tests can verify the exact CLI stream deterministically.
    internal sealed class AgentHeadlessOptions {
        public string goal="",dataDir="",scope="all",collection="",protocol="auto",sessionId="",title="";
        public List<string> ids=new List<string>();
        public int budget=14000,requestLimit=6,contextTokens=32000,timeoutSeconds=300;
        public bool json,rpc;
    }
    public static class AgentHeadless {
        const string HelpText=
            "kok agent - 无界面只读 Agent\n"+
            "用法: kok agent --goal \"<目标>\" (--print | --json) [选项]\n"+
            "      kok agent --rpc [选项]      (常驻 JSONL RPC 模式)\n"+
            "必填:\n"+
            "  --goal <文本>             任务目标（1-2000 字符）\n"+
            "  --print | --json          输出模式：--print 只输出最终回答；--json 输出 JSONL 事件流\n"+
            "  --rpc                     常驻 RPC 模式：stdin 每行一个 JSON 命令，stdout 返回 JSONL 响应/事件/结果\n"+
            "选项:\n"+
            "  --data-dir <目录>         资料库目录（默认程序目录下的 data）\n"+
            "  --scope all|selected|collection   RPC prompt 的默认资料范围（默认 all）\n"+
            "  --ids <id,id,...>         --scope selected 时的资料 ID（1-24 个）\n"+
            "  --collection <名称>       --scope collection 时的分类名\n"+
            "  --budget 6000|14000|24000|48000|64000   上下文预算（默认 14000）\n"+
            "  --request-limit 1..24     模型请求上限（默认 6）\n"+
            "  --context-tokens 8000..128000   上下文窗口（默认 32000）\n"+
            "  --protocol auto|tools|json      模型协议（默认 auto）\n"+
            "  --session <32hex>         指定会话 ID（默认随机；RPC 模式下所有 prompt 复用该会话）\n"+
            "  --title <文本>            会话标题（最多 64 字符）\n"+
            "  --timeout-seconds 1..3600 任务超时后停止并退出 1（默认 300）\n"+
            "  --no-memory               不启用长期记忆（无界面模式始终关闭记忆）\n"+
            "  --help                    显示本帮助\n"+
            "RPC 命令: ping / prompt / steer / follow_up / abort / state / compact / shutdown\n"+
            "  ping -> {\"type\":\"response\",\"id\":..,\"ok\":true,\"result\":{\"version\":1,\"pid\":..,\"taskId\":null}}\n"+
            "  prompt {goal,scope?,ids?,collection?,budget?,requestLimit?,contextTokens?,protocol?,title?}\n"+
            "  steer/follow_up {taskId,text}；abort {taskId,cancel}；state {taskId}；compact {taskId,instructions?}\n"+
            "  事件与结果是 {\"type\":\"event\"|\"result\",...} 行；未知命令返回 ok:false；坏 JSON 返回一行 error 后继续。\n"+
            "退出码: 0 已完成/正常关闭；2 需要补充信息/部分完成/参数错误或未启用智能模式；1 失败/超限/停止/超时；130 手动中断或任务运行中 EOF\n"+
            "只读边界: 无界面模式固定使用 read_only 分析策略，不支持 --approval / --allow-writes。\n";
        public static int Run(string[] args){
            bool jsonRequested=args!=null&&args.Contains("--json");
            bool rpcRequested=args!=null&&args.Contains("--rpc");
            try{Console.OutputEncoding=new UTF8Encoding(false);}catch{}
            try{
                if(args==null||args.Length==0)return 2;
                if(args.Skip(1).Any(a=>a=="--help"||a=="-h")){Console.Out.Write(HelpText);Console.Out.Flush();return 0;}
                AgentHeadlessOptions options;string error;
                if(!Parse(args,out options,out error)){WriteError(rpcRequested||jsonRequested,error);return 2;}
                if(options.rpc){
                    using(var cancel=new CancellationTokenSource()){
                        ConsoleCancelEventHandler onRpcCancel=(sender,cancelArgs)=>{cancelArgs.Cancel=true;try{cancel.Cancel();}catch{}};
                        Console.CancelKeyPress+=onRpcCancel;
                        try{return RunRpc(options,Console.In,Console.Out,Console.Error,cancel.Token).GetAwaiter().GetResult();}
                        finally{Console.CancelKeyPress-=onRpcCancel;}
                    }
                }
                string dir=options.dataDir!=""?options.dataDir:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");
                Library library;TranslationService translations;
                try{library=new Library(dir);translations=new TranslationService(library.DataDir);}
                catch(Exception ex){WriteError(options.json,Message(ex));return 2;}
                using(var cancel=new CancellationTokenSource()){
                    ConsoleCancelEventHandler onCancel=(sender,args2)=>{args2.Cancel=true;try{cancel.Cancel();}catch{}};
                    Console.CancelKeyPress+=onCancel;
                    try{return Execute(options,library,translations,Console.Out,Console.Error,cancel.Token).GetAwaiter().GetResult();}
                    finally{Console.CancelKeyPress-=onCancel;}
                }
            }catch(Exception ex){WriteError(rpcRequested||jsonRequested,Message(ex));return 2;}
        }
        internal static bool Parse(string[] args,out AgentHeadlessOptions options,out string error){
            options=new AgentHeadlessOptions();error="";int print=0,json=0,rpc=0;
            for(int i=1;i<args.Length;i++){
                string name=args[i];string value;
                switch(name){
                    case "--print":print++;break;
                    case "--json":json++;break;
                    case "--rpc":rpc++;break;
                    case "--no-memory":break;
                    case "--help":case "-h":error="请使用 kok agent --help 查看用法。";return false;
                    case "--goal":if(!Take(args,ref i,out value)){error="--goal 需要参数值。";return false;}options.goal=value.Trim();break;
                    case "--data-dir":if(!Take(args,ref i,out value)){error="--data-dir 需要参数值。";return false;}options.dataDir=value;break;
                    case "--scope":if(!Take(args,ref i,out value)){error="--scope 需要参数值。";return false;}options.scope=value.Trim();break;
                    case "--ids":if(!Take(args,ref i,out value)){error="--ids 需要参数值。";return false;}options.ids=value.Split(',').Select(id=>id.Trim()).Where(id=>id!="").ToList();break;
                    case "--collection":if(!Take(args,ref i,out value)){error="--collection 需要参数值。";return false;}options.collection=value.Trim();break;
                    case "--budget":if(!Take(args,ref i,out value)||!TryInt(value,out options.budget)){error="--budget 需要 6000/14000/24000/48000/64000。";return false;}break;
                    case "--request-limit":if(!Take(args,ref i,out value)||!TryInt(value,out options.requestLimit)){error="--request-limit 需要 1-24 的整数。";return false;}break;
                    case "--context-tokens":if(!Take(args,ref i,out value)||!TryInt(value,out options.contextTokens)){error="--context-tokens 需要 8000-128000 的整数。";return false;}break;
                    case "--protocol":if(!Take(args,ref i,out value)){error="--protocol 需要 auto|tools|json。";return false;}options.protocol=value.Trim();break;
                    case "--session":if(!Take(args,ref i,out value)){error="--session 需要 32 位十六进制会话 ID。";return false;}options.sessionId=value.Trim();break;
                    case "--title":if(!Take(args,ref i,out value)){error="--title 需要参数值。";return false;}options.title=value.Trim();break;
                    case "--timeout-seconds":if(!Take(args,ref i,out value)||!TryInt(value,out options.timeoutSeconds)){error="--timeout-seconds 需要 1-3600 的整数。";return false;}break;
                    case "--approval":error="无界面模式固定为只读策略，不支持 --approval。";return false;
                    case "--allow-writes":error="无界面模式只读，不支持 --allow-writes。";return false;
                    default:error="未知参数："+name;return false;
                }
            }
            if(rpc>0){
                if(rpc!=1){error="--rpc 只能指定一次。";return false;}
                if(print+json>0){error="--rpc 不能与 --print 或 --json 一起使用。";return false;}
                if(options.goal.Length>0){error="--rpc 不能与 --goal 一起使用。";return false;}
                options.rpc=true;
                return ValidateCommon(options,out error);
            }
            if(!Validate(options,print,json,out error))return false;
            options.json=json==1;
            return true;
        }
        static bool Validate(AgentHeadlessOptions options,int print,int json,out string error){
            error="";
            if(print+json!=1){error=print==0&&json==0?"请指定输出模式 --print 或 --json。":"--print 与 --json 只能选择一个。";return false;}
            if(options.goal.Length==0||options.goal.Length>2000){error="--goal 需要 1-2000 字符。";return false;}
            return ValidateCommon(options,out error);
        }
        static bool ValidateCommon(AgentHeadlessOptions options,out string error){
            error="";
            if(!new[]{"all","selected","collection"}.Contains(options.scope)){error="--scope 需要 all、selected 或 collection。";return false;}
            if(options.scope=="selected"){
                if(options.ids.Count<1||options.ids.Count>24){error="--scope selected 需要 1-24 个 --ids。";return false;}
                if(options.ids.Distinct(StringComparer.Ordinal).Count()!=options.ids.Count||options.ids.Any(id=>!AgentJournal.ValidId(id))){error="--ids 需要 32 位十六进制资料 ID。";return false;}
            }else if(options.ids.Count>0){error="--ids 只能与 --scope selected 一起使用。";return false;}
            if(options.scope=="collection"){
                if(options.collection.Length==0||options.collection.Length>100){error="--scope collection 需要 1-100 字符的 --collection。";return false;}
            }else if(options.collection!=""){error="--collection 只能与 --scope collection 一起使用。";return false;}
            if(!new[]{6000,14000,24000,48000,64000}.Contains(options.budget)){error="--budget 需要 6000/14000/24000/48000/64000。";return false;}
            if(options.requestLimit<1||options.requestLimit>AgentRules.MaxRounds){error="--request-limit 需要 1-24。";return false;}
            if(options.contextTokens<8000||options.contextTokens>128000){error="--context-tokens 需要 8000-128000。";return false;}
            if(!new[]{"auto","tools","json"}.Contains(options.protocol)){error="--protocol 需要 auto、tools 或 json。";return false;}
            if(options.sessionId!=""&&!AgentJournal.ValidId(options.sessionId)){error="--session 需要 32 位十六进制会话 ID。";return false;}
            if(options.title.Length>AgentSessionStore.MaxTitle){error="--title 最多 64 字符。";return false;}
            if(options.timeoutSeconds<1||options.timeoutSeconds>3600){error="--timeout-seconds 需要 1-3600。";return false;}
            if(options.dataDir!=""){
                try{options.dataDir=Path.GetFullPath(options.dataDir);}
                catch{error="--data-dir 路径无效。";return false;}
            }
            return true;
        }
        internal static async Task<int> Execute(AgentHeadlessOptions options,Library library,TranslationService translations,TextWriter stdout,TextWriter stderr,CancellationToken cancellationToken){
            bool json=options.json;
            Action<string> fail=message=>{
                if(json)WriteJson(stdout,new Dictionary<string,object>{{"type","error"},{"message",message}});
                else stderr.WriteLine(message);
            };
            try{
                if(library.IsPrivate){fail("私密文件夹不启用 AI，请先返回普通资料库。");return 2;}
                if(!library.AiEnabled){fail("请先在 kok 中启用智能模式。");return 2;}
                var jobs=new Jobs(library.DataDir,null);
                var context=new ModuleContext(library,translations,new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),jobs,null,appDirectory:AppDomain.CurrentDomain.BaseDirectory);
                var service=new AgentService(context);
                string sessionId=options.sessionId!=""?options.sessionId:Guid.NewGuid().ToString("N");
                if(options.title!=""){service.SessionStore.Ensure(sessionId,options.title);service.SessionStore.Rename(sessionId,options.title);}
                var scope=new Dictionary<string,object>{{"mode",options.scope}};
                if(options.scope=="selected")scope["ids"]=options.ids;
                if(options.scope=="collection")scope["collection"]=options.collection;
                var startArgs=new Dictionary<string,object>{
                    {"sessionId",sessionId},{"goal",options.goal},{"scope",scope},{"budget",options.budget},
                    {"requestLimit",options.requestLimit},{"contextTokens",options.contextTokens},{"approvalPolicy","read_only"},
                    {"semanticCompression",true},{"intent","assist"},{"protocol",options.protocol},{"useMemory",false},
                    {"history",new List<ChatTurn>()},{"sessionLeafId",""}};
                Dictionary<string,object> snapshot;
                try{snapshot=Map(await service.HandleAgentTaskAction("agentTaskStart",startArgs));}
                catch(Exception error){fail(Message(error));return 2;}
                string taskId=Text(snapshot,"id");
                if(json)WriteJson(stdout,new Dictionary<string,object>{{"type","header"},{"version",1},{"taskId",taskId},{"sessionId",sessionId},{"model",ModelName(translations)},{"goal",options.goal}});
                var emitted=new HashSet<string>(StringComparer.Ordinal);var watch=Stopwatch.StartNew();bool timedOut=false,interrupted=false;
                while(true){
                    if(cancellationToken.IsCancellationRequested){interrupted=true;await StopTask(service,taskId);break;}
                    if(watch.Elapsed.TotalSeconds>options.timeoutSeconds){timedOut=true;await StopTask(service,taskId);break;}
                    snapshot=Map(await service.HandleAgentTaskAction("agentTaskRead",new Dictionary<string,object>{{"taskId",taskId}}));
                    if(json)await EmitEvents(service,taskId,stdout,emitted);
                    if(!Bool(snapshot,"running"))break;
                    try{await Task.Delay(120,cancellationToken);}catch(OperationCanceledException){}
                }
                if(timedOut||interrupted){
                    snapshot=await Settle(service,taskId);
                    if(json)await EmitEvents(service,taskId,stdout,emitted);
                }
                string status=Text(snapshot,"status"),reply=FinalReply(snapshot);
                if(json)WriteResult(stdout,taskId,status,reply,snapshot);
                else{stdout.Write(reply);stdout.Write('\n');if(status!="completed")stderr.WriteLine("任务状态："+status);}
                stdout.Flush();stderr.Flush();
                if(interrupted)return 130;
                if(timedOut)return 1;
                return ExitCode(status);
            }catch(Exception error){try{fail(Message(error));}catch{}return 2;}
        }
        // ---------------- RPC mode (long-lived JSONL over stdin/stdout) ----------------

        // CLI entry: builds the GUI-free services, then runs the loop. Startup failures are one JSON error line.
        internal static async Task<int> RunRpc(AgentHeadlessOptions options,TextReader input,TextWriter stdout,TextWriter stderr,CancellationToken cancellationToken){
            string dir=options.dataDir!=""?options.dataDir:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");
            Library library;TranslationService translations;
            try{library=new Library(dir);translations=new TranslationService(library.DataDir);}
            catch(Exception error){WriteRpcError(stdout,Message(error));return 2;}
            return await RunRpc(options,library,translations,input,stdout,stderr,cancellationToken);
        }
        // Testable seam: same loop against supplied services and streams (deterministic fixtures feed stdin).
        internal static async Task<int> RunRpc(AgentHeadlessOptions options,Library library,TranslationService translations,TextReader input,TextWriter stdout,TextWriter stderr,CancellationToken cancellationToken){
            // The command loop below waits on stdin synchronously between commands; yield so a caller (and
            // the CLI/reader side) gets the Task before the loop starts consuming input.
            await Task.Yield();
            var writeGate=new object();
            Action<object> write=value=>{lock(writeGate){stdout.Write(Library.Json.Serialize(value));stdout.Write('\n');stdout.Flush();}};
            Action<string> writeError=message=>write(new Dictionary<string,object>{{"type","error"},{"message",message}});
            Action<string,bool,object> respond=(id,ok,result)=>{
                var payload=new Dictionary<string,object>{{"type","response"},{"ok",ok}};
                if(!String.IsNullOrEmpty(id))payload["id"]=id;
                payload[ok?"result":"error"]=ok?(result??new Dictionary<string,object>()):Convert.ToString(result??"命令执行失败。");
                write(payload);
            };
            Action<string,string> fail=(id,message)=>respond(id,false,message);
            try{
                if(library.IsPrivate){writeError("私密文件夹不启用 AI，请先返回普通资料库。");return 2;}
                if(!library.AiEnabled){writeError("请先在 kok 中启用智能模式。");return 2;}
                var jobs=new Jobs(library.DataDir,null);
                var context=new ModuleContext(library,translations,new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),jobs,null,appDirectory:AppDomain.CurrentDomain.BaseDirectory);
                var service=new AgentService(context);
                var stateGate=new object();string activeTaskId=null,activeSessionId=null;Task activePoller=null;
                Func<string> activeTask=()=>{lock(stateGate)return activeTaskId;};
                Func<string> activeSession=()=>{lock(stateGate)return activeSessionId;};
                Action clearIfCurrent=()=>{lock(stateGate){activeTaskId=null;activeSessionId=null;activePoller=null;}};
                async Task PollAsync(string taskId){
                    var emitted=new HashSet<string>(StringComparer.Ordinal);var watch=Stopwatch.StartNew();bool timedOut=false;
                    try{
                        while(true){
                            if(!timedOut&&options.timeoutSeconds>0&&watch.Elapsed.TotalSeconds>options.timeoutSeconds){
                                timedOut=true;
                                try{await service.HandleAgentTaskAction("agentTaskStop",new Dictionary<string,object>{{"taskId",taskId},{"cancel",true}});}catch{}
                            }
                            var snapshot=Map(await service.HandleAgentTaskAction("agentTaskRead",new Dictionary<string,object>{{"taskId",taskId}}));
                            await EmitEvents(service,taskId,stdout,emitted,writeGate);
                            if(!Bool(snapshot,"running")){
                                lock(stateGate){if(String.Equals(activeTaskId,taskId,StringComparison.Ordinal)){activeTaskId=null;activeSessionId=null;activePoller=null;}}
                                write(ResultPayload(taskId,Text(snapshot,"status"),FinalReply(snapshot),snapshot));
                                break;
                            }
                            await Task.Delay(80);
                        }
                    }catch(Exception error){writeError(Message(error));}
                    finally{lock(stateGate){if(String.Equals(activeTaskId,taskId,StringComparison.Ordinal)){activeTaskId=null;activeSessionId=null;activePoller=null;}}}
                }
                async Task StopActiveAsync(){
                    string taskId=activeTask();if(String.IsNullOrEmpty(taskId))return;
                    try{await service.HandleAgentTaskAction("agentTaskStop",new Dictionary<string,object>{{"taskId",taskId},{"cancel",true}});}catch{}
                    Task poller;lock(stateGate)poller=activePoller;
                    if(poller!=null)try{await Task.WhenAny(poller,Task.Delay(5000));}catch{}
                }
                async Task<bool> HandleCommand(Dictionary<string,object> row){
                    string method=Text(row,"method"),id=Text(row,"id");
                    switch(method){
                        case "ping":
                            respond(id,true,new Dictionary<string,object>{{"version",1},{"pid",Process.GetCurrentProcess().Id},{"taskId",activeTask()}});
                            return false;
                        case "prompt":{
                            if(activeTask()!=null){fail(id,"已有任务在运行。");return false;}
                            string goal=Text(row,"goal")??"";
                            string sessionId=options.sessionId!=""?options.sessionId:Guid.NewGuid().ToString("N");
                            string title=Text(row,"title");if(String.IsNullOrEmpty(title))title=options.title;
                            if(!String.IsNullOrEmpty(title)){try{service.SessionStore.Ensure(sessionId,title.Length>AgentSessionStore.MaxTitle?title.Substring(0,AgentSessionStore.MaxTitle):title);}catch{}}
                            var startArgs=new Dictionary<string,object>{
                                {"sessionId",sessionId},{"goal",goal},{"scope",ScopeArgs(row,options)},
                                {"budget",IntOr(row,"budget",options.budget)},{"requestLimit",IntOr(row,"requestLimit",options.requestLimit)},
                                {"contextTokens",IntOr(row,"contextTokens",options.contextTokens)},{"protocol",ProtocolOr(row,options)},
                                {"approvalPolicy","read_only"},{"semanticCompression",true},{"intent","assist"},{"useMemory",false},
                                {"history",new List<ChatTurn>()},{"sessionLeafId",""}};
                            try{
                                var snapshot=Map(await service.HandleAgentTaskAction("agentTaskStart",startArgs));
                                string taskId=Text(snapshot,"id");
                                lock(stateGate){activeTaskId=taskId;activeSessionId=sessionId;}
                                var poller=Task.Run(()=>PollAsync(taskId));lock(stateGate)activePoller=poller;
                                respond(id,true,new Dictionary<string,object>{{"taskId",taskId},{"sessionId",sessionId}});
                            }catch(Exception error){fail(id,Message(error));}
                            return false;
                        }
                        case "steer":case "follow_up":{
                            string taskId=Text(row,"taskId");if(String.IsNullOrEmpty(taskId))taskId=activeTask();
                            string text=Text(row,"text")??"";
                            try{
                                await service.HandleAgentTaskAction(method=="steer"?"agentTaskSteer":"agentTaskFollowUp",new Dictionary<string,object>{{"taskId",taskId},{"text",text}});
                                respond(id,true,new Dictionary<string,object>{{"taskId",taskId},{"queued",true}});
                            }catch(Exception error){fail(id,Message(error));}
                            return false;
                        }
                        case "abort":{
                            string taskId=Text(row,"taskId");if(String.IsNullOrEmpty(taskId))taskId=activeTask();
                            bool cancel=!row.ContainsKey("cancel")||Bool(row,"cancel");
                            try{
                                object stopped=await service.HandleAgentTaskAction("agentTaskStop",new Dictionary<string,object>{{"taskId",taskId},{"cancel",cancel}});
                                respond(id,true,new Dictionary<string,object>{{"taskId",taskId},{"stopped",Convert.ToBoolean(stopped)}});
                            }catch(Exception error){fail(id,Message(error));}
                            return false;
                        }
                        case "state":{
                            string taskId=Text(row,"taskId");if(String.IsNullOrEmpty(taskId))taskId=activeTask();
                            if(String.IsNullOrEmpty(taskId)){fail(id,"找不到任务记录。");return false;}
                            try{respond(id,true,Map(await service.HandleAgentTaskAction("agentTaskRead",new Dictionary<string,object>{{"taskId",taskId}})));}
                            catch(Exception error){fail(id,Message(error));}
                            return false;
                        }
                        case "compact":{
                            string taskId=Text(row,"taskId");if(String.IsNullOrEmpty(taskId))taskId=activeTask();
                            string instructions=Text(row,"instructions")??"";
                            try{respond(id,true,Map(await service.HandleAgentTaskAction("agentTaskCompact",new Dictionary<string,object>{{"taskId",taskId},{"instructions",instructions}})));}
                            catch(Exception error){fail(id,Message(error));}
                            return false;
                        }
                        case "shutdown":
                            respond(id,true,new Dictionary<string,object>{{"shutdown",true}});
                            await StopActiveAsync();
                            return true;
                        default:
                            fail(id,"未知命令。");
                            return false;
                    }
                }
                using(var commands=new System.Collections.Concurrent.BlockingCollection<string>(new System.Collections.Concurrent.ConcurrentQueue<string>())){
                    var reader=Task.Run(()=>{try{string line;while((line=input.ReadLine())!=null)commands.Add(line);}catch{}finally{try{commands.CompleteAdding();}catch{}}});
                    try{
                        while(true){
                            if(cancellationToken.IsCancellationRequested){await StopActiveAsync();return 130;}
                            string line;
                            if(!commands.TryTake(out line,100)){
                                if(commands.IsCompleted)break;
                                continue;
                            }
                            if(line==null||line.Trim().Length==0)continue;
                            Dictionary<string,object> row;
                            try{row=Library.Json.DeserializeObject(line) as Dictionary<string,object>;}
                            catch(Exception error){writeError(Message(error));continue;}
                            if(row==null){writeError("命令格式无效。");continue;}
                            bool shutdown;
                            try{shutdown=await HandleCommand(row);}catch(Exception error){fail(Text(row,"id"),Message(error));shutdown=false;}
                            if(shutdown)return 0;
                        }
                    }finally{try{commands.CompleteAdding();}catch{}}
                    // EOF: a running task is stopped first (130); idle stdin closes normally (0).
                    if(activeTask()!=null){await StopActiveAsync();return 130;}
                    return 0;
                }
            }catch(Exception error){writeError(Message(error));return 2;}
        }
        static Dictionary<string,object> ScopeArgs(Dictionary<string,object> row,AgentHeadlessOptions options){
            var scope=new Dictionary<string,object>{{"mode",options.scope}};
            object raw;
            if(row.TryGetValue("scope",out raw)&&raw!=null){
                if(raw is string)scope["mode"]=Convert.ToString(raw);
                else{var map=raw as Dictionary<string,object>;if(map!=null){scope=new Dictionary<string,object>(map);if(!scope.ContainsKey("mode"))scope["mode"]="all";}}
            }
            if(!scope.ContainsKey("ids")){
                object ids;if(row.TryGetValue("ids",out ids)&&ids!=null)scope["ids"]=ids;else if(options.ids.Count>0)scope["ids"]=options.ids;
            }
            if(!scope.ContainsKey("collection")){
                string collection=Text(row,"collection");if(!String.IsNullOrEmpty(collection))scope["collection"]=collection;else if(options.collection!="")scope["collection"]=options.collection;
            }
            return scope;
        }
        static string ProtocolOr(Dictionary<string,object> row,AgentHeadlessOptions options){string value=Text(row,"protocol");return String.IsNullOrEmpty(value)?options.protocol:value;}
        static int IntOr(Dictionary<string,object> row,string key,int fallback){object value;if(row==null||!row.TryGetValue(key,out value)||value==null)return fallback;try{return Convert.ToInt32(value,CultureInfo.InvariantCulture);}catch{return fallback;}}
        static void WriteRpcError(TextWriter output,string message){output.Write(Library.Json.Serialize(new Dictionary<string,object>{{"type","error"},{"message",message}}));output.Write('\n');output.Flush();}
        static async Task StopTask(AgentService service,string taskId){
            try{await service.HandleAgentTaskAction("agentTaskStop",new Dictionary<string,object>{{"taskId",taskId},{"cancel",true}});}catch{}
        }
        static async Task<Dictionary<string,object>> Settle(AgentService service,string taskId){
            Dictionary<string,object> snapshot=null;
            for(int n=0;n<50;n++){
                snapshot=Map(await service.HandleAgentTaskAction("agentTaskRead",new Dictionary<string,object>{{"taskId",taskId}}));
                if(!Bool(snapshot,"running"))return snapshot;
                await Task.Delay(100);
            }
            return snapshot;
        }
        static async Task EmitEvents(AgentService service,string taskId,TextWriter stdout,HashSet<string> emitted,object writeGate=null){
            object rowsValue=await service.HandleAgentTaskAction("agentTaskEvents",new Dictionary<string,object>{{"taskId",taskId}});
            var rows=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(rowsValue));
            if(rows==null)return;
            foreach(var row in rows.OrderBy(row=>Convert.ToInt32(row.ContainsKey("sequence")?row["sequence"]:0))){
                string id=Text(row,"id");if(String.IsNullOrEmpty(id)||emitted.Contains(id))continue;
                object detail=await service.HandleAgentTaskAction("agentTaskEvents",new Dictionary<string,object>{{"taskId",taskId},{"eventId",id}});
                var payload=new Dictionary<string,object>{{"type","event"},{"taskId",taskId},{"event",detail}};
                if(writeGate==null)WriteJson(stdout,payload);else WriteJsonLocked(stdout,writeGate,payload);
                emitted.Add(id);
            }
        }
        static Dictionary<string,object> ResultPayload(string taskId,string status,string reply,Dictionary<string,object> snapshot){
            var usage=snapshot!=null&&snapshot.ContainsKey("usage")?snapshot["usage"] as Dictionary<string,object>:null;
            var verification=snapshot!=null&&snapshot.ContainsKey("verification")?snapshot["verification"] as Dictionary<string,object>:null;
            return new Dictionary<string,object>{
                {"type","result"},{"taskId",taskId},{"status",status},{"reply",reply},
                {"usage",new Dictionary<string,object>{{"inputTokens",Int(usage,"inputTokens")},{"outputTokens",Int(usage,"outputTokens")},{"cachedTokens",Int(usage,"cachedTokens")},{"cost",Double(usage,"cost")},{"requests",Int(usage,"requests")}}},
                {"verification",verification==null?(object)null:new Dictionary<string,object>{{"status",Text(verification,"status")},{"summary",Text(verification,"summary")}}}};
        }
        static void WriteResult(TextWriter stdout,string taskId,string status,string reply,Dictionary<string,object> snapshot){
            WriteJson(stdout,ResultPayload(taskId,status,reply,snapshot));
        }
        static void WriteJson(TextWriter writer,object value){writer.Write(Library.Json.Serialize(value));writer.Write('\n');}
        static void WriteJsonLocked(TextWriter writer,object writeGate,object value){lock(writeGate){writer.Write(Library.Json.Serialize(value));writer.Write('\n');writer.Flush();}}
        static void WriteError(bool json,string message){
            if(json){WriteJson(Console.Out,new Dictionary<string,object>{{"type","error"},{"message",message}});Console.Out.Flush();}
            else{Console.Error.WriteLine(message);Console.Error.Flush();}
        }
        static int ExitCode(string status){
            switch(status){
                case "completed":return 0;
                case "needs_input":case "review_required":case "partial":return 2;
                case "cancelled":case "stopped":case "failed":case "limited":return 1;
                default:return 1;
            }
        }
        static string FinalReply(Dictionary<string,object> snapshot){
            string reply="";
            if(snapshot==null||!snapshot.ContainsKey("messages"))return reply;
            List<Dictionary<string,object>> messages;
            try{messages=Library.Json.Deserialize<List<Dictionary<string,object>>>(Library.Json.Serialize(snapshot["messages"]));}
            catch{return reply;}
            if(messages==null)return reply;
            foreach(var message in messages){
                if(Text(message,"role")!="agent"||Bool(message,"meta")||Bool(message,"interrupted"))continue;
                reply=Text(message,"text")??"";
            }
            return reply;
        }
        static string ModelName(TranslationService translations){
            try{var config=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(translations.PublicConfig()));return config!=null&&config.ContainsKey("model")?Convert.ToString(config["model"])??"":"";}catch{return "";}
        }
        static Dictionary<string,object> Map(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
        static bool Take(string[] args,ref int index,out string value){
            value=null;
            if(index+1>=args.Length)return false;
            value=args[++index];
            return true;
        }
        static bool TryInt(string value,out int number){return Int32.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out number);}
        static string Text(Dictionary<string,object> row,string key){
            object value;
            if(row==null||!row.TryGetValue(key,out value)||value==null)return null;
            return Convert.ToString(value,CultureInfo.InvariantCulture);
        }
        static bool Bool(Dictionary<string,object> row,string key){
            object value;
            if(row==null||!row.TryGetValue(key,out value)||value==null)return false;
            try{return Convert.ToBoolean(value,CultureInfo.InvariantCulture);}catch{return false;}
        }
        static int Int(Dictionary<string,object> row,string key){
            object value;if(row==null||!row.TryGetValue(key,out value)||value==null)return 0;
            try{return Convert.ToInt32(value,CultureInfo.InvariantCulture);}catch{return 0;}
        }
        static double Double(Dictionary<string,object> row,string key){
            object value;if(row==null||!row.TryGetValue(key,out value)||value==null)return 0;
            try{return Convert.ToDouble(value,CultureInfo.InvariantCulture);}catch{return 0;}
        }
        static string Message(Exception error){
            string message=error==null?"未知错误。":error.Message;
            if(message==null)message="未知错误。";
            message=message.Trim();
            if(message.Length>2000)message=message.Substring(0,2000);
            return message;
        }
    }
}

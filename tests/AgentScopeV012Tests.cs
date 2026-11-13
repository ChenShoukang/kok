using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

// v0.12.0 P0-1: task-created artifacts join the effective scope (create -> read back -> edit -> finish,
// replay/restart/undo) plus P1 repaired-failure state and intention-based tool profiles.
// Owned by scope-agent; keep the Run(root,check) signature stable.
class AgentScopeV012Tests {
    static string Id(){return Guid.NewGuid().ToString("N");}
    static Dictionary<string,object> Dict(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    static bool Throws(Action action){try{action();return false;}catch{return true;}}
    static void Emit(AgentJournal journal,AgentTaskState state,string kind,object data){state.Apply(journal.Append(state.id,kind,data,state.options.sessionId,"fixture",state.status));}
    static AgentTaskState Seed(AgentJournal journal,string goal,params string[] allowed){
        var state=new AgentTaskState{id=Id(),options=new AgentTaskOptions{sessionId=Id(),goal=goal,kernelVersion=3,requestLimit=6,allowed=new List<string>(allowed)}};
        Emit(journal,state,"task.created",new{options=state.options,protocol="tools"});return state;
    }
    static AgentAction Action(string tool,object args){return new AgentAction{tool=tool,args=Dict(args)};}
    static AgentModelTurn Reply(string text,params AgentAction[] actions){return new AgentModelTurn{reply=new AgentReply{reply=text,done=true,actions=actions.ToList()}};}
    static void Propose(AgentJournal journal,AgentTaskState state,AgentCall call){Emit(journal,state,"tool.proposed",new{call});}
    static void Complete(AgentJournal journal,AgentTaskState state,AgentCall call,object result){Emit(journal,state,"tool.completed",new{callId=call.id,result=Dict(result),stamp=(string)null});}
    static void Fail(AgentJournal journal,AgentTaskState state,AgentCall call,string kind,string message){Emit(journal,state,"tool.proposed",new{call});Emit(journal,state,"tool.failed",new{callId=call.id,kind,message,retryable=false});}
    static Library NewLibrary(string root,string name){var library=new Library(Path.Combine(root,name));library.SetAppMode("smart",true);return library;}
    static AgentTaskState Replay(AgentJournal journal,AgentTaskState state){return AgentTaskState.Replay(state.id,journal.Events(state.id));}
    static AgentCall PendingCall(AgentTaskEngine engine,string tool){
        for(int n=0;n<1500;n++){var call=engine.ReadSnapshot(()=>engine.State.calls.FirstOrDefault(c=>c.tool==tool&&c.status=="pending"));if(call!=null)return call;if(!engine.IsRunning)break;Thread.Sleep(5);}return null;
    }
    static void SaveDocument(Library library,string id,string text,string signature){library.SaveDocument(id,text,signature,null);}

    public static void Run(string root,Action<bool,string> check){RunAsync(root,check).GetAwaiter().GetResult();}
    static async Task RunAsync(string root,Action<bool,string> check){
        Directory.CreateDirectory(root);
        ScopeStateChecks(Path.Combine(root,"state"),check);
        var library=NewLibrary(root,"library");
        ResultContentChecks(library,check);
        TailMetadataChecks(library,check);
        UnauthorizedProposalChecks(Path.Combine(root,"unauthorized"),check);
        ToolProfileChecks(library,check);
        await FullTaskFlow(Path.Combine(root,"flow"),check);
    }

    // P0-1 state rules: artifact ids from host receipts join the scope; replay, undo and repair maps stay consistent.
    static void ScopeStateChecks(string root,Action<bool,string> check){
        var journal=new AgentJournal(root);
        string source=Id(),outside=Id();
        var state=Seed(journal,"根据资料写报告",source);
        var create=new AgentCall{id=Id(),tool="create_document",args=Dict(new{name="报告",format="md",text="初稿"})};
        Propose(journal,state,create);string report=Id();
        Complete(journal,state,create,new{id=report,name="报告",kind="document",ext="MD",text="Created 报告"});
        check(state.InScope(report)&&state.InScope(source)&&!state.InScope(outside)&&!state.InScope(null)&&state.TaskScope.SequenceEqual(new[]{source,report}),"a host create receipt joins the effective scope without widening the original source boundary");
        var replay=Replay(journal,state);var restarted=new AgentJournal(root);
        var replayRestart=AgentTaskState.Replay(state.id,restarted.Events(state.id));
        check(replay.createdArtifactIds.SequenceEqual(new[]{report})&&replay.InScope(report)&&replayRestart.TaskScope.SequenceEqual(state.TaskScope)&&replayRestart.createdArtifactIds.SequenceEqual(state.createdArtifactIds),"replaying the durable journal (and a fresh journal reader) reproduces created artifacts and the effective scope");
        // Model-claimed ids never become scope: only the completed host receipt is authoritative.
        var forged=Seed(journal,"伪造成果",source);
        var fake=new AgentCall{id=Id(),tool="create_document",args=Dict(new{name="伪造",format="md",text="x",id=report})};
        Propose(journal,forged,fake);Complete(journal,forged,fake,new{id="MODEL_SAID_SO",name="伪造",text="nope"});
        var fake2=new AgentCall{id=Id(),tool="create_document",args=Dict(new{name="伪造2",format="md",text="x"})};
        Propose(journal,forged,fake2);Complete(journal,forged,fake2,new{id="zzz-not-an-id",text="nope"});
        check(forged.createdArtifactIds.Count==0&&!forged.InScope("MODEL_SAID_SO")&&!forged.InScope(report)&&forged.TaskScope.SequenceEqual(new[]{source}),"model-declared or malformed artifact ids are ignored; only a completed host receipt with a valid library id changes the scope");
        // tool.failed must not evict a real artifact, tool.undone must.
        var undoState=Seed(journal,"撤销成果",source);
        var createdCall=new AgentCall{id=Id(),tool="create_document",args=Dict(new{name="成果",format="md",text="x"})};
        Propose(journal,undoState,createdCall);string artifact=Id();Complete(journal,undoState,createdCall,new{id=artifact,name="成果",kind="document",text="created"});
        var failedRead=new AgentCall{id=Id(),tool="read_items",args=Dict(new{ids=new[]{source}})};
        Fail(journal,undoState,failedRead,"validation","temporary read failure");
        check(undoState.createdArtifactIds.SequenceEqual(new[]{artifact}),"a later tool failure never removes a created artifact from the scope");
        Emit(journal,undoState,"tool.undone",new{callId=createdCall.id});
        check(undoState.createdArtifactIds.Count==0&&!undoState.InScope(artifact)&&undoState.InScope(source),"tool.undone removes the artifact from the effective scope but keeps the original selection");
    }

    // P1 repaired failures: only a later success with the same tool+args resolves; uncertain/conflict writes never do.
    static void RepairStateChecks(AgentJournal journal,Action<bool,string> check){
        var state=Seed(journal,"补救测试",Id());
        var failed=new AgentCall{id=Id(),tool="read_items",args=Dict(new{ids=new[]{state.options.allowed[0]}})};
        Fail(journal,state,failed,"validation","source busy");
        var retry=new AgentCall{id=Id(),tool="read_items",args=Dict(new{ids=new[]{state.options.allowed[0]}})};
        Propose(journal,state,retry);Complete(journal,state,retry,new{text="read ok"});
        check(state.FailureResolved(failed)&&state.resolvedBy[failed.id]==retry.id,"a later successful call with the same tool and args repairs the earlier failed step");
        var different=new AgentCall{id=Id(),tool="read_items",args=Dict(new{ids=new[]{Id()}})};
        Fail(journal,state,different,"validation","source busy");
        var otherTool=new AgentCall{id=Id(),tool="search_library",args=Dict(new{query="x"})};
        Propose(journal,state,otherTool);Complete(journal,state,otherTool,new{text="search ok"});
        check(!state.FailureResolved(different),"a success with different args or a different tool does not resolve an unrelated failure");
        var uncertain=Seed(journal,"不确定写入",Id());
        var uncertainFail=new AgentCall{id=Id(),tool="read_items",args=Dict(new{ids=new[]{Id()}})};
        Fail(journal,uncertain,uncertainFail,"uncertain_write","write result unknown");
        var uncertainRetry=new AgentCall{id=Id(),tool="read_items",args=uncertainFail.args};
        Propose(journal,uncertain,uncertainRetry);Complete(journal,uncertain,uncertainRetry,new{text="read ok"});
        check(!uncertain.FailureResolved(uncertainFail)&&!uncertain.resolvedBy.ContainsKey(uncertainFail.id),"uncertain_write (and conflict/external) failures are never auto-resolved by a later success");
        var undoResolver=Seed(journal,"撤销补救",Id());
        var undoneFailed=new AgentCall{id=Id(),tool="read_items",args=Dict(new{ids=new[]{Id()}})};
        Fail(journal,undoResolver,undoneFailed,"validation","source busy");
        var undoneSuccess=new AgentCall{id=Id(),tool="read_items",args=Dict(new{ids=undoneFailed.args["ids"]})};
        Propose(journal,undoResolver,undoneSuccess);Complete(journal,undoResolver,undoneSuccess,new{text="read ok"});
        bool resolvedFirst=undoResolver.FailureResolved(undoneFailed);
        Emit(journal,undoResolver,"tool.undone",new{callId=undoneSuccess.id});
        var undoneReplay=Replay(journal,undoResolver);
        check(resolvedFirst&&!undoResolver.FailureResolved(undoneFailed)&&!undoResolver.resolvedBy.ContainsKey(undoneFailed.id)&&!undoneReplay.FailureResolved(undoneFailed),"undoing the repairing call clears the resolution link durably");
    }

    // P0-2 boundary owned here: 7999/8000 stay exact; 8001/10000 keep a large head+tail window and an explicit
    // middle-omission/recall_result offset hint instead of the old 8,000 -> ~1,000 cliff.
    static void ResultContentChecks(Library library,Action<bool,string> check){
        var service=new AgentService(new ModuleContext(library,null,new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),null));
        var method=typeof(AgentService).GetMethod("CallContents",BindingFlags.NonPublic|BindingFlags.Instance);
        Func<int,string> sent=n=>{
            var state=new AgentTaskState{id=Id(),options=new AgentTaskOptions{budget=14000}};
            var call=new AgentCall{id=Id(),tool="read_items",status="done",result=Dict(new{text=new string('x',n-8)+"TAIL_END"})};
            state.calls.Add(call);
            var map=(Dictionary<string,string>)method.Invoke(service,new object[]{state});
            return map[call.id];
        };
        check(sent(7999).Length==7999&&sent(7999).EndsWith("TAIL_END")&&sent(8000).Length==8000&&sent(8000).EndsWith("TAIL_END"),"tool results of 7999/8000 characters are passed through unchanged");
        string over=sent(8001),longer=sent(10000);
        check(over.Length>=7000&&over.Contains("TAIL_END")&&over.Contains("recall_result")&&over.Contains("offset=")&&longer.Length>=7000&&longer.Contains("TAIL_END")&&longer.Contains("middle省略"),"8001/10000-character results keep >=7000 characters with the tail preserved and an explicit middle-omission/recall offset hint");
        check(over.Length>6000&&longer.Length>6000&&longer.Length<=8000,"oversized results no longer collapse to the old ~1000-character excerpt");
        // Surrogate pairs must never be split by the head/tail window (CJK and emoji text).
        var emojiState=new AgentTaskState{id=Id(),options=new AgentTaskOptions{budget=14000}};
        var emojiCall=new AgentCall{id=Id(),tool="read_items",status="done",result=Dict(new{text=String.Concat(Enumerable.Repeat("😀",4200))+"TAIL_END"})};
        emojiState.calls.Add(emojiCall);
        var emojiSent=((Dictionary<string,string>)method.Invoke(service,new object[]{emojiState}))[emojiCall.id];
        check(emojiSent.EndsWith("TAIL_END")&&!LoneSurrogate(emojiSent),"middle omission keeps surrogate pairs intact for CJK/emoji results");
        RepairStateChecks(new AgentJournal(Path.Combine(library.DataDir,"repair-journal")),check);
    }
    static bool LoneSurrogate(string text){
        for(int n=0;n<text.Length;n++){
            if(Char.IsHighSurrogate(text[n])&&(n+1>=text.Length||!Char.IsLowSurrogate(text[n+1])))return true;
            if(Char.IsLowSurrogate(text[n])&&(n==0||!Char.IsHighSurrogate(text[n-1])))return true;
        }return false;
    }

    // Lead self-check: created-artifact metadata must live in the dynamic host tail (each round), never in the
    // cacheable prefix; the model can combine the tool receipt id with this tail to read the artifact back.
    static void TailMetadataChecks(Library library,Action<bool,string> check){
        var entry=library.CreateDocument("动态成果","md","","TAIL_BODY");
        var state=new AgentTaskState{id=Id(),options=new AgentTaskOptions{sessionId=Id(),goal="检查动态元数据",allowed=new List<string>(),kernelVersion=3}};
        state.createdArtifactIds.Add(entry.id);
        var service=new AgentService(new ModuleContext(library,null,new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),null));
        var ctxMethod=typeof(AgentService).GetMethod("CreatedArtifactContext",BindingFlags.NonPublic|BindingFlags.Instance);
        var context=(string)ctxMethod.Invoke(service,new object[]{state});
        var messagesMethod=typeof(AgentService).GetMethod("TaskMessages",BindingFlags.NonPublic|BindingFlags.Instance);
        var messages=(List<object>)messagesMethod.Invoke(service,new object[]{state,"catalog","guidance",context,true});
        Func<object,string> content=message=>Convert.ToString(message.GetType().GetProperty("content").GetValue(message,null));
        string first=content(messages[0]),last=content(messages[messages.Count-1]);
        check(context.Contains(entry.id)&&context.Contains("动态成果")&&last.Contains(entry.id)&&last.Contains("本任务已创建的成果")&&!first.Contains(entry.id),"created-artifact metadata appears in the dynamic host tail state rather than the cacheable prompt prefix");
    }

    // P1 safety net: a proposal refused before execution (policy/format validation) becomes a durable failed
    // call instead of failing the whole task; unexpected exceptions still abort so real bugs are not hidden.
    static void UnauthorizedProposalChecks(string root,Action<bool,string> check){
        var journal=new AgentJournal(Path.Combine(root,"policy"));
        var state=Seed(journal,"只读任务",Id());state.options.approvalPolicy="read_only";
        int rounds=0;
        Func<AgentTaskEngine,CancellationToken,Task<AgentModelTurn>> model=(engine,token)=>{
            rounds++;engine.Emit("request.started",new{reserve=0});
            if(rounds==1)return Task.FromResult(Reply("尝试写入",Action("create_document",new{name="x",format="md",text="x"})));
            return Task.FromResult(Reply("如实部分完成",Action("finish_task",new{status="partial",summary="未执行未授权修改",checks=new object[0]})));
        };
        Func<AgentTaskState,AgentAction,string,string,AgentCall> prepare=(s,a,turn,provider)=>{
            var spec=AgentRules.Tools.FirstOrDefault(t=>t.Name==a.tool);
            if(spec==null)throw new FormatException("unknown tool");
            if(s.options.approvalPolicy=="read_only"&&spec.Write)throw new AgentFailure("policy","此工具未获得当前任务授权。");
            return new AgentCall{id=Id(),providerId=provider,turnId=turn,tool=a.tool,args=a.args,why=a.why,status=AgentRules.ReadTools.Contains(a.tool)?"queued":"pending"};
        };
        var engine=new AgentTaskEngine(state,journal,()=>{},model,null,null,prepare,()=>{});
        engine.Start();engine.Completion.GetAwaiter().GetResult();
        var rejected=state.calls.FirstOrDefault(c=>c.tool=="create_document");
        check(state.status=="partial"&&!state.events.Any(e=>e.kind=="task.failed")&&rejected!=null&&rejected.status=="failed"&&rejected.errorKind=="policy"&&state.events.Any(e=>e.kind=="tool.failed"&&AgentTaskState.Get<string>(e.data,"kind")=="policy")&&rounds==2,"an unauthorized tool proposal is recorded as a failed policy call and the task can still finish partial instead of failing");
        var boomJournal=new AgentJournal(Path.Combine(root,"unexpected"));
        var boom=Seed(boomJournal,"异常任务",Id());int boomRounds=0;
        Func<AgentTaskEngine,CancellationToken,Task<AgentModelTurn>> boomModel=(engine,token)=>{boomRounds++;engine.Emit("request.started",new{reserve=0});return Task.FromResult(Reply("读",Action("read_items",new{ids=new[]{Id()}})));};
        Func<AgentTaskState,AgentAction,string,string,AgentCall> boomPrepare=(s,a,turn,provider)=>{throw new InvalidOperationException("unexpected prepare bug");};
        var boomEngine=new AgentTaskEngine(boom,boomJournal,()=>{},boomModel,null,null,boomPrepare,()=>{});
        boomEngine.Start();boomEngine.Completion.GetAwaiter().GetResult();
        check(boom.status=="failed"&&boom.errorKind=="validation"&&boomRounds==1,"unexpected non-policy/non-FormatException prepare errors still abort the task instead of being hidden");
    }

    // P1: profile by goal/intent, report <=14 and no media/subtitle tools, PPT keeps all four presentation tools,
    // discuss/read-only never expose writes, and an explicit caller allow-list always wins.
    static void ToolProfileChecks(Library library,Action<bool,string> check){
        var source=library.CreateDocument("报告资料","md","","REPORT_SOURCE_FACT");
        Func<AgentTaskOptions,List<string>> profile=o=>AgentToolProfiles.ForTask(o,library);
        var report=profile(new AgentTaskOptions{goal="根据资料写一份完整的报告",allowed=new List<string>{source.id}});
        check(report.Count<=14&&report.Contains("create_document")&&report.Contains("read_document")&&report.Contains("replace_document_text")&&report.Contains("finish_task")&&!report.Contains("generate_subtitles")&&!report.Contains("index_pdf_pages")&&!report.Contains("index_video_frames"),"the default report profile exposes <=14 tools including create/read/replace/finish and no subtitle/media-index tools");
        // v0.12.1 C1: the 14-tool cap applies to the pure report/read BASE profile only; capability groups
        // configured by the user (memory/delegation/sandbox/web/folder/MCP) are appended after the cap and can
        // never be silently trimmed (#19).
        var execDir=Path.Combine(library.DataDir,"exec-workspace");Directory.CreateDirectory(execDir);
        AgentExecution.Save(library,true,execDir,false);
        var capped=profile(new AgentTaskOptions{goal="根据资料写一份完整的报告，并查看工作区代码",allowed=new List<string>{source.id},useMemory=true});
        check(capped.Contains("create_document")&&capped.Contains("replace_document_text")&&capped.Contains("read_document")&&capped.Contains("read_items")&&capped.Contains("finish_task")&&capped.Contains("search_memory")&&capped.Contains("remember_memory")&&capped.Contains("list_folder")&&capped.Contains("read_text_file")&&capped.Contains("write_text_file"),"the base-profile cap trims only the base profile: configured memory/execution capabilities and the delivery write/read tools all survive");
        // Mixed intents take the union of every matched profile: organizing AND writing must keep both groups.
        var mixed=profile(new AgentTaskOptions{goal="给这份笔记打标签并写一份摘要",allowed=new List<string>{source.id}});
        check(mixed.Contains("add_tags")&&mixed.Contains("write_note")&&mixed.Contains("set_collection")&&mixed.Contains("create_document")&&mixed.Contains("replace_document_text")&&mixed.Contains("read_items")&&mixed.Contains("finish_task"),"mixed organize+write goals keep both the organize writes and the report creation tools (union, not single priority)");
        // Organize-only goals still need to produce and save a document (agent-output-recovery fixture).
        var organize=profile(new AgentTaskOptions{goal="整理这四份资料，输出整合结果并保存",allowed=new List<string>{source.id}});
        check(organize.Contains("create_document")&&organize.Contains("read_document")&&organize.Contains("add_tags")&&organize.Contains("write_note"),"organize goals that must save a consolidated result keep create_document/read_document/write_note");
        var deck=profile(new AgentTaskOptions{goal="把这份报告做成 8 页 PPT",allowed=new List<string>{source.id}});
        check(new[]{"create_presentation","read_presentation","copy_presentation","edit_presentation"}.All(deck.Contains),"the presentation profile keeps create/read/copy/edit_presentation");
        var discuss=profile(new AgentTaskOptions{goal="解释这份报告",intent="discuss",allowed=new List<string>{source.id}});
        var readOnly=profile(new AgentTaskOptions{goal="解释这份报告",approvalPolicy="read_only",allowed=new List<string>{source.id}});
        check(discuss.Count>0&&discuss.All(name=>!AgentRules.Tools.First(t=>t.Name==name).Write)&&readOnly.All(name=>!AgentRules.Tools.First(t=>t.Name==name).Write),"discuss and read-only profiles never expose a write tool");
        var explicitList=new List<string>{"read_items","finish_task"};
        var explicitProfile=profile(new AgentTaskOptions{goal="根据资料写一份完整的报告",allowed=new List<string>{source.id},allowedTools=explicitList});
        check(explicitProfile.SequenceEqual(explicitList),"an explicit allowedTools list wins over the automatic goal profile");
        // Visible schema count through the real host filter (reflection): the probe measures TaskTools.
        var service=new AgentService(new ModuleContext(library,null,new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),null));
        var taskTools=typeof(AgentService).GetMethod("TaskTools",BindingFlags.NonPublic|BindingFlags.Instance);
        Func<AgentTaskOptions,string[]> visible=o=>{var state=new AgentTaskState{id=Id(),options=o};var specs=(AgentToolSpec[])taskTools.Invoke(service,new object[]{state});return specs.Select(t=>t.Name).ToArray();};
        string[] reportTools=visible(new AgentTaskOptions{goal="根据资料写一份完整的报告",allowed=new List<string>{source.id},allowedTools=report});
        check(reportTools.Length<=14&&reportTools.Contains("create_document")&&reportTools.Contains("finish_task")&&!reportTools.Contains("generate_subtitles")&&!reportTools.Contains("index_pdf_pages")&&!reportTools.Contains("index_video_frames"),"the report task's visible tool schema is <=14 and excludes subtitle/media-index capabilities");
        string[] deckTools=visible(new AgentTaskOptions{goal="把这份报告做成 8 页 PPT",allowed=new List<string>{source.id},allowedTools=deck});
        check(deckTools.Contains("create_presentation")&&deckTools.Contains("read_presentation")&&deckTools.Contains("copy_presentation")&&deckTools.Contains("edit_presentation"),"the PPT task's visible tool schema includes the presentation tool group");
        string[] discussTools=visible(new AgentTaskOptions{goal="解释这份报告",intent="discuss",allowed=new List<string>{source.id},allowedTools=discuss});
        check(discussTools.All(name=>!AgentRules.Tools.First(t=>t.Name==name).Write),"the discuss task's visible tool schema contains no write tools");
        var deckEntry=library.CreatePresentation("自有演示","",AgentSlides.Build(Dict(new{title="x",slides=new object[]{new{title="one"}}})));
        var scopeDeck=profile(new AgentTaskOptions{goal="整理这些资料",scope=new AgentScope{mode="selected",ids=new List<string>{deckEntry.id}},allowed=new List<string>{deckEntry.id}});
        check(scopeDeck.Contains("edit_presentation")&&scopeDeck.Contains("read_presentation"),"a selected PPTX source switches the profile to the presentation tool group");
    }

    // P0-1 end-to-end: create (host receipt) -> automatic host read-back -> targeted edit -> finish completed in one
    // task; replay, a fresh journal reader and a reloaded library all agree; out-of-scope ids still fail; undo removes it.
    static async Task FullTaskFlow(string root,Action<bool,string> check){
        Directory.CreateDirectory(root);var library=new Library(root);library.SetAppMode("smart",true);
        var source=library.CreateDocument("原始资料","md","","项目资料：活动预算 9,600 元，负责人江岚。结尾保留事实。");
        string outside=library.CreateDocument("范围外资料","md","","OUTSIDE_SECRET").id;
        var options=new AgentTaskOptions{sessionId=Id(),goal="根据资料写一份报告",allowed=new List<string>{source.id},kernelVersion=3,budget=14000,requestLimit=6,approvalPolicy="each"};
        var state=new AgentTaskState{id=Id(),options=options,protocol="tools"};
        var journal=new AgentJournal(root);int rounds=0;
        Func<AgentTaskState,AgentAction,string,string,AgentCall> prepare=(s,a,turn,provider)=>{var call=library.PrepareAgentCall(s,a,turn,provider);call.steering=s.steerVersion;return call;};
        Func<AgentTaskState,AgentCall,CancellationToken,Task<Dictionary<string,object>>> read=(s,c,t)=>library.ReadAgentTool(s,c,t);
        Func<AgentTaskState,AgentCall,CancellationToken,Task<AgentToolReceipt>> write=(s,c,t)=>Task.FromResult(library.WriteAgentTool(s,c,t));
        Func<AgentTaskEngine,CancellationToken,Task<AgentModelTurn>> model=(engine,token)=>{
            rounds++;engine.Emit("request.started",new{reserve=0});
            var s=engine.State;
            if(rounds==1)return Task.FromResult(Reply("创建报告",Action("create_document",new{name="项目报告",format="md",text="报告初稿：活动预算 9,600 元，负责人江岚。结尾保留事实。"})));
            if(rounds==2){string created=s.createdArtifactIds.First();return Task.FromResult(Reply("定点修改预算",Action("replace_document_text",new{id=created,old_text="9,600",new_text="12,600"})));}
            var readBack=s.calls.Where(c=>c.hostGenerated&&c.tool=="read_items"&&c.status=="done").Select(c=>c.id).ToArray();
            var createdCall=s.calls.First(c=>c.tool=="create_document"&&c.status=="done").id;
            var editCall=s.calls.FirstOrDefault(c=>c.tool=="replace_document_text"&&c.status=="done");
            var steps=new List<object>{new{id="create",title="创建报告",status="done",evidence=new[]{createdCall}},new{id="read",title="回读报告",status="done",evidence=readBack}};
            if(editCall!=null)steps.Insert(1,new{id="edit",title="定点修改预算",status="done",evidence=new[]{editCall.id}});
            return Task.FromResult(Reply("核对完成",Action("update_plan",new{steps=steps.ToArray()}),Action("finish_task",new{status="completed",summary="已创建并修改报告",checks=new[]{new{claim="已回读保存后的报告",evidence=readBack}}})));
        };
        var engine=new AgentTaskEngine(state,journal,()=>{},model,read,write,prepare,()=>{},c=>c.status=="done");
        engine.Start();
        AgentCall create=PendingCall(engine,"create_document");
        check(create!=null&&state.status=="waiting","a create_document proposal waits for approval before writing anything");
        await engine.Decide(create.id,true);
        AgentCall edit=PendingCall(engine,"replace_document_text");
        await engine.Decide(edit.id,true);
        var completion=engine.Completion;var finished=await Task.WhenAny(completion,Task.Delay(30000))==completion;
        check(finished&&state.status=="completed","create -> host read-back -> targeted edit -> finish completed in one task");
        string artifact=state.createdArtifactIds.Single();
        var hostRead=state.calls.FirstOrDefault(c=>c.hostGenerated&&c.tool=="read_items"&&c.status=="done");
        check(hostRead!=null&&hostRead.args.ContainsKey("ids")&&AgentTaskState.Get<List<string>>(hostRead.args,"ids").Contains(artifact)&&Convert.ToString(hostRead.result["text"]).Contains("报告初稿"),"the host automatically reads back the created artifact with a real read_items call after approval");
        var editCall=state.calls.First(c=>c.tool=="replace_document_text"&&c.status=="done");
        var editRead=state.calls.FirstOrDefault(c=>c.hostGenerated&&c.tool=="read_items"&&c.sequence>editCall.sequence&&c.status=="done"&&c.sources.ContainsKey(artifact));
        check(editRead!=null&&Convert.ToString(editRead.result["text"]).Contains("12,600"),"after an approved in-place edit the host reads the edited document back (hostGenerated read_items with the edited id at a later sequence)");
        check(state.InScope(artifact)&&state.InScope(source.id)&&!state.InScope(outside)&&library.Find(artifact)!=null,"the created artifact is readable by the same task while the original source boundary stays intact");
        var edited=library.DocumentSource(artifact);
        check(edited.text.Contains("12,600")&&edited.text.Contains("结尾保留事实")&&edited.text.Contains("负责人江岚"),"the targeted replacement applies exactly and preserves the rest of the saved document");
        var replay=AgentTaskState.Replay(state.id,new AgentJournal(root).Events(state.id));
        var restartJournal=new AgentJournal(root);var replayRestart=AgentTaskState.Replay(state.id,restartJournal.Events(state.id));
        var restartedLibrary=new Library(root);
        check(replay.status=="completed"&&replay.createdArtifactIds.SequenceEqual(new[]{artifact})&&replay.InScope(artifact)&&replayRestart.InScope(artifact)&&restartedLibrary.Find(artifact)!=null,"replay and a restart (fresh journal reader + reloaded library) reproduce the artifact scope and receipt-verified outcome");
        bool outOfScope=Throws(()=>library.PrepareAgentCall(state,new AgentAction{tool="read_items",args=Dict(new{ids=new[]{outside}})},Id(),null));
        check(outOfScope,"a library file outside the original selection is still rejected by the effective scope");
        // Undo removes the artifact from the scope and replay of the undone event is consistent.
        Emit(journal,state,"tool.undone",new{callId=create.id});
        bool undoneRejected=Throws(()=>library.PrepareAgentCall(state,new AgentAction{tool="read_items",args=Dict(new{ids=new[]{artifact}})},Id(),null));
        var undoneReplay=AgentTaskState.Replay(state.id,journal.Events(state.id));
        check(state.createdArtifactIds.Count==0&&!state.InScope(artifact)&&state.InScope(source.id)&&undoneRejected&&undoneReplay.createdArtifactIds.Count==0,"tool.undone removes the artifact from the task scope and blocks further writes/reads; replay agrees");
        // C1 host wiring: MakeEngine installs the delivery acceptance delegate owned by AgentDelivery.
        var service=new AgentService(new ModuleContext(library,null,new ModulePolicy(library),null,CancellationToken.None,null,new Operations(library,null),null));
        var makeEngine=typeof(AgentService).GetMethod("MakeEngine",BindingFlags.NonPublic|BindingFlags.Instance);
        var wired=(AgentTaskEngine)makeEngine.Invoke(service,new object[]{state});
        check(wired.DeliveryCheck!=null,"MakeEngine wires the host DeliveryCheck delegate into the task engine");
    }
}

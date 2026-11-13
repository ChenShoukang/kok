using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

// v0.12.1 C3 (#14/#12/#13): corrected-parameter read retries resolve, but uncertain/conflicting writes and
// unrelated targets never do; the repaired relation survives journal replay and undo; a mid-task instruction
// re-derives both the host acceptance requirement and an auto-derived tool profile from the latest request.
// Owned by recovery-agent; keep the Run(root,check) signature stable (registered in NativeTests.cs).
class AgentRecoveryV013Tests {
    static string Id(){return Guid.NewGuid().ToString("N");}
    static Dictionary<string,object> Dict(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    static void Emit(AgentJournal journal,AgentTaskState state,string kind,object data){state.Apply(journal.Append(state.id,kind,data,state.options.sessionId,"fixture",state.status));}
    static AgentTaskState Seed(AgentJournal journal,string goal,params string[] allowed){return Seed(journal,goal,null,allowed);}
    static AgentTaskState Seed(AgentJournal journal,string goal,Action<AgentTaskOptions> configure,params string[] allowed){
        var state=new AgentTaskState{id=Id(),options=new AgentTaskOptions{sessionId=Id(),goal=goal,kernelVersion=3,requestLimit=6,allowed=new List<string>(allowed),scope=new AgentScope{mode="selected",ids=new List<string>(allowed)}}};
        if(configure!=null)configure(state.options);
        Emit(journal,state,"task.created",new{options=state.options,protocol="tools"});return state;
    }
    static AgentCall Propose(AgentJournal journal,AgentTaskState state,string tool,object args){
        var call=new AgentCall{id=Id(),tool=tool,args=Dict(args),why="fixture"};
        Emit(journal,state,"tool.proposed",new{call});return state.calls.Last();
    }
    static void Fail(AgentJournal journal,AgentTaskState state,AgentCall call,string kind,string message){Emit(journal,state,"tool.failed",new{callId=call.id,kind,message,retryable=false});}
    static void Complete(AgentJournal journal,AgentTaskState state,AgentCall call,object result){Emit(journal,state,"tool.completed",new{callId=call.id,result=Dict(result),stamp=(string)null});}
    static AgentTaskState Replay(AgentJournal journal,AgentTaskState state){return AgentTaskState.Replay(state.id,journal.Events(state.id));}

    public static void Run(string root,Action<bool,string> check){
        Directory.CreateDirectory(root);
        CorrectedReadChecks(Path.Combine(root,"corrected-read"),check);
        NonRepairingChecks(Path.Combine(root,"non-repairing"),check);
        UndoRepairChecks(Path.Combine(root,"undo-repair"),check);
        RealReaderChecks(Path.Combine(root,"real-reader"),check);
        AdjustmentChecks(Path.Combine(root,"adjustment"),check);
    }

    // #14: a failed read with an out-of-range/garbled parameter is repaired by a later successful read of the
    // same target with corrected parameters, even though the JSON args differ. Durable across replay.
    static void CorrectedReadChecks(string root,Action<bool,string> check){
        var journal=new AgentJournal(Path.Combine(root,"journal"));
        string source=Id();
        var state=Seed(journal,"读取这份资料",source);
        var failed=Propose(journal,state,"read_document",new{id=source,offset=2000});
        Fail(journal,state,failed,"validation","文档原文偏移超出范围。");
        var corrected=Propose(journal,state,"read_document",new{id=source,offset=0});
        Complete(journal,state,corrected,new{text="文档原文",total_chars=1000});
        check(state.FailureResolved(failed)&&state.resolvedBy[failed.id]==corrected.id,"#14: a corrected read_document retry (offset 2000 failed -> offset 0 ok) resolves the earlier failure although the JSON args differ");
        var replay=Replay(journal,state);
        check(replay.FailureResolved(failed)&&replay.resolvedBy[failed.id]==corrected.id&&!state.resolvedBy.ContainsKey(corrected.id),"#14: the corrected-parameter repair link is rebuilt deterministically from journal replay");
        // Same tool/args remains the strongest signal (legacy rule preserved).
        var exact=Seed(journal,"读取另一份资料",source);
        var first=Propose(journal,exact,"read_items",new{ids=new[]{source}});
        Fail(journal,exact,first,"source","资料暂时不可读。");
        var again=Propose(journal,exact,"read_items",new{ids=new[]{source}});
        Complete(journal,exact,again,new{text="ok"});
        check(exact.FailureResolved(first)&&exact.resolvedBy[first.id]==again.id,"#14: the legacy same-tool/same-args repair rule keeps working for non-read tools and identical retries");
        // read_items list overlap: a corrected single-source page of an id included in the failed multi-read counts.
        var list=Seed(journal,"读取两份资料",source,Id());
        string other=list.options.allowed[1];
        var broad=Propose(journal,list,"read_items",new{ids=new[]{source,other},offset=5000});
        Fail(journal,list,broad,"source","页码越界。");
        var narrow=Propose(journal,list,"read_items",new{ids=new[]{other},offset=0});
        Complete(journal,list,narrow,new{text="第二份"});
        check(list.FailureResolved(broad),"#14: a corrected read of one overlapping target id repairs a failed multi-id read");
        // Cross-tool exact-read correction (read_document failed -> read_items succeeded on the same id).
        var cross=Seed(journal,"读取资料",source);
        var docFail=Propose(journal,cross,"read_document",new{id=source,offset=2000});
        Fail(journal,cross,docFail,"missing_source","源文件暂时缺失。");
        var itemOk=Propose(journal,cross,"read_items",new{ids=new[]{source}});
        Complete(journal,cross,itemOk,new{text="原文"});
        check(cross.FailureResolved(docFail),"#14: another exact read tool on the same target id (read_document -> read_items) resolves the failed read");
    }

    // A success that cannot be a parameter correction must not clear the failure.
    static void NonRepairingChecks(string root,Action<bool,string> check){
        var journal=new AgentJournal(Path.Combine(root,"journal"));
        string source=Id(),other=Id();
        var different=Seed(journal,"读取这份资料",source,other);
        var failed=Propose(journal,different,"read_document",new{id=source,offset=2000});
        Fail(journal,different,failed,"validation","offset 越界");
        var wrongTarget=Propose(journal,different,"read_document",new{id=other,offset=0});
        Complete(journal,different,wrongTarget,new{text="另一份"});
        check(!different.FailureResolved(failed)&&!different.resolvedBy.ContainsKey(failed.id),"#14: a corrected read of a DIFFERENT source id does not resolve the failure");
        var search=Seed(journal,"读取这份资料",source);
        var readFail=Propose(journal,search,"read_document",new{id=source,offset=2000});
        Fail(journal,search,readFail,"validation","offset 越界");
        var searchOk=Propose(journal,search,"search_library",new{query="资料"});
        Complete(journal,search,searchOk,new{text="摘要命中"});
        check(!search.FailureResolved(readFail),"#14: a later non-read success (search_library) never repairs a failed exact read");
        // Only validation/format/source/missing_source are deterministic; transient/policy errors stay open.
        foreach(var kind in new[]{"timeout","network","permission","policy","uncertain_write","uncertain_external","conflict"}){
            var state=Seed(journal,"读取资料",source);
            var bad=Propose(journal,state,"read_document",new{id=source,offset=2000});
            Fail(journal,state,bad,kind,"不能自动判定为已解决");
            var ok=Propose(journal,state,"read_document",new{id=source,offset=0});
            Complete(journal,state,ok,new{text="ok"});
            check(!state.FailureResolved(bad)&&!state.resolvedBy.ContainsKey(bad.id),"#14: errorKind "+kind+" is never auto-resolved by a corrected read retry");
        }
        var sameArgsUncertain=Seed(journal,"读取资料",source);
        var uncertain=Propose(journal,sameArgsUncertain,"read_document",new{id=source,offset=0});
        Fail(journal,sameArgsUncertain,uncertain,"uncertain_write","写入结果未知");
        var sameAgain=Propose(journal,sameArgsUncertain,"read_document",new{id=source,offset=0});
        Complete(journal,sameArgsUncertain,sameAgain,new{text="ok"});
        check(!sameArgsUncertain.FailureResolved(uncertain),"#14: an uncertain_write failure is not resolved even by an identical later success");
    }

    static void UndoRepairChecks(string root,Action<bool,string> check){
        var journal=new AgentJournal(Path.Combine(root,"journal"));
        string source=Id();
        var state=Seed(journal,"读取资料",source);
        var failed=Propose(journal,state,"read_document",new{id=source,offset=2000});
        Fail(journal,state,failed,"validation","offset 越界");
        var corrected=Propose(journal,state,"read_document",new{id=source,offset=0});
        Complete(journal,state,corrected,new{text="ok"});
        bool resolvedBeforeUndo=state.FailureResolved(failed);
        Emit(journal,state,"tool.undone",new{callId=corrected.id});
        check(resolvedBeforeUndo&&!state.FailureResolved(failed)&&!state.resolvedBy.ContainsKey(failed.id)&&!Replay(journal,state).FailureResolved(failed),"#14: undoing the corrected retry removes the repair link and the removal survives replay");
    }

    // Production-path integration: the real reader rejects an out-of-range offset with a validation failure and
    // the real offset-0 retry is what resolves it (not a hand-crafted fixture event).
    static void RealReaderChecks(string root,Action<bool,string> check){
        var library=new Library(Path.Combine(root,"library"));library.SetAppMode("smart",true);
        var document=library.CreateDocument("纠正读取资料","md","",new string('a',1000));
        var journal=new AgentJournal(Path.Combine(root,"journal"));
        var state=Seed(journal,"读取这份资料",document.id);
        var failed=library.PrepareAgentCall(state,new AgentAction{tool="read_document",args=Dict(new{id=document.id,offset=2000})},Id(),null);
        Emit(journal,state,"tool.proposed",new{call=failed});failed=state.calls.Last();
        string level="";
        try{library.ReadAgentTool(state,failed,CancellationToken.None).GetAwaiter().GetResult();}
        catch(Exception error){level=AgentFailure.Classify(error).kind;Fail(journal,state,failed,level,error.Message);}
        check(level=="validation","the production read_document reports an out-of-range offset as a validation failure");
        var corrected=library.PrepareAgentCall(state,new AgentAction{tool="read_document",args=Dict(new{id=document.id,offset=0})},Id(),null);
        Emit(journal,state,"tool.proposed",new{call=corrected});corrected=state.calls.Last();
        var result=library.ReadAgentTool(state,corrected,CancellationToken.None).GetAwaiter().GetResult();
        Complete(journal,state,corrected,result);
        check(state.FailureResolved(failed)&&Replay(journal,state).FailureResolved(failed),"the real offset=0 retry (result includes total_chars/next_offset) resolves the real failed read in the live state and after replay");
        check(state.calls.Count(c=>c.tool=="read_document")==2&&state.calls.Count(c=>c.status=="failed")==1&&state.calls.Count(c=>c.status=="done")==1,"the production reader integration records exactly one failed and one successful read call");
    }

    // #12/#13 + Lead contract: task.adjusted re-derives options.delivery and (only for an auto profile) the
    // allowed tool list, deterministically, with no Library dependency and replay-safe.
    static void AdjustmentChecks(string root,Action<bool,string> check){
        var library=new Library(Path.Combine(root,"library"));library.SetAppMode("smart",true);
        string source=library.CreateDocument("调整来源","md","","ADJUST_SOURCE").id;
        var journal=new AgentJournal(Path.Combine(root,"journal"));
        // Delivery: 8-page deck adjusted to 5 pages -> the latest request governs acceptance.
        var deck=Seed(journal,"制作8页PPT。",o=>{o.workflow="slides";},source);
        Emit(journal,deck,"task.adjusted",new{text="改为5页PPT，不要8页。"});
        check(deck.options.delivery!=null&&deck.options.delivery.requestedPages==5,"#12: task.adjusted re-derives options.delivery so the latest requested page count (5) governs acceptance and tool instructions");
        var deckReplay=Replay(journal,deck);
        check(deckReplay.options.delivery!=null&&deckReplay.options.delivery.requestedPages==5,"#12: the re-derived delivery requirement is replay-safe");
        // Auto tools: read-only request adjusted to also save a document -> create_document becomes available.
        var auto=Seed(journal,"总结这份资料",o=>{o.allowedTools=new List<string>{"read_items","finish_task"};o.allowedToolsAuto=true;},source);
        Emit(journal,auto,"task.adjusted",new{text="把总结保存为一个新的文档。"});
        check(auto.options.allowedTools!=null&&auto.options.allowedTools.Contains("create_document")&&auto.options.allowedTools.Contains("read_items"),"#13: an auto-derived allowedTools profile is re-derived from task.adjusted so the newly added deliverable exposes create_document while keeping reads");
        check(Replay(journal,auto).options.allowedTools.Contains("create_document"),"#13: the re-derived auto tool profile is replay-safe");
        // Explicit user-picked list stays authoritative.
        var explicitList=new List<string>{"read_items","finish_task"};
        var explicitState=Seed(journal,"总结这份资料",o=>{o.allowedTools=new List<string>(explicitList);o.allowedToolsAuto=false;},source);
        Emit(journal,explicitState,"task.adjusted",new{text="把总结保存为一个新的文档。"});
        check(explicitState.options.allowedTools.SequenceEqual(explicitList),"#13: an explicit user-picked allowedTools list is never widened by a mid-task instruction");
        // allowedToolsAuto persists through the journal so a restarted task keeps the same policy.
        var flag=Seed(journal,"整理资料",o=>o.allowedToolsAuto=true,source);
        check(Replay(journal,flag).options.allowedToolsAuto,"the host auto-profile marker persists in the task journal and replays with the task");
        check(!Seed(journal,"整理资料",source).options.allowedToolsAuto,"an untouched task starts with allowedToolsAuto=false (explicit list, legacy semantics)");
        // Legacy journal compatibility: a task without a persisted delivery requirement still derives on adjust.
        var legacy=Seed(journal,"制作8页PPT。",source);
        check(legacy.options.delivery==null,"a task started without PrepareDelivery keeps its legacy null delivery until an instruction arrives");
        Emit(journal,legacy,"task.adjusted",new{text="改为5页PPT，不要8页。"});
        check(legacy.options.delivery!=null&&legacy.options.delivery.requestedPages==5,"#12: legacy tasks without a persisted requirement still re-derive from task.adjusted");
    }
}

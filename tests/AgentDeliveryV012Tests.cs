using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using kok;

// v0.12.0 P0-3/P1: delivery acceptance participates in "completed" (artifact exists, PPT page count,
// read-back, full-source coverage) and repaired failures no longer block completion.
// Owned by delivery-agent; register/keep Run(root,check) signature stable.
// Fixtures are fully offline: real Library documents/PPTs and native task events, no model requests.
class AgentDeliveryV012Tests {
    static Action<bool,string> check;
    public static void Run(string root,Action<bool,string> check){
        AgentDeliveryV012Tests.check=check;
        Directory.CreateDirectory(root);
        Derivation(Path.Combine(root,"derive"));
        ArtifactAcceptance(Path.Combine(root,"artifact"));
        PageCountAcceptance(Path.Combine(root,"pages"));
        TargetedEditAcceptance(Path.Combine(root,"edit"));
        FullSourceCoverage(Path.Combine(root,"coverage"));
        RepairedFailureSemantics(Path.Combine(root,"repair"));
    }
    static void Verify(bool ok,string name){check(ok,name);}
    static List<string> Ids(int n){var ids=new List<string>();for(int i=0;i<n;i++)ids.Add(new string((char)('a'+i),32));return ids;}

    sealed class Fixture {
        public Library Lib;public AgentService Service;
        public Fixture(string root){
            Lib=new Library(root);Lib.SetAppMode("smart",true);
            Service=new AgentService(new ModuleContext(Lib,new TranslationService(Lib.DataDir),new ModulePolicy(Lib),null,CancellationToken.None,null,new Operations(Lib,null),null,appDirectory:AppDomain.CurrentDomain.BaseDirectory));
        }
    }

    // ---- shared native task plumbing -------------------------------------------------------------
    static AgentTaskState Seed(AgentJournal journal,AgentTaskOptions options){
        string id=Guid.NewGuid().ToString("N");
        options.sessionId=options.sessionId??Guid.NewGuid().ToString("N");
        var e=journal.Append(id,"task.created",new{options=options,protocol="tools"},options.sessionId,"fixture","created");
        return AgentTaskState.Replay(id,new List<AgentEvent>{e});
    }
    static bool Current(Library lib,AgentTaskState state,AgentCall call){
        try{
            if(lib.OperationStatus(call.id)=="undone")return false;
            if((call.tool=="read_items"||call.tool=="read_document"||call.tool=="read_presentation")&&call.sources!=null&&call.sources.Count>0&&Library.Json.Serialize(call.sources)!=Library.Json.Serialize(lib.AgentSourceStamps(call.sources.Keys)))return false;
            return AgentEvidence.ChangesCurrent(lib,state,call)&&AgentEvidence.DocumentCurrent(lib,state,call);
        }catch{return false;}
    }
    static AgentTaskEngine RealEngine(Library lib,AgentJournal journal,AgentTaskState state){
        Func<AgentCall,bool> current=c=>Current(lib,state,c);
        Func<AgentTaskState,AgentAction,string,string,AgentCall> prepare=(s,a,turn,provider)=>{var c=lib.PrepareAgentCall(s,a,turn,provider);c.steering=s.steerVersion;return c;};
        return new AgentTaskEngine(state,journal,()=>{},null,
            (s,c,t)=>lib.ReadAgentTool(s,c,t),
            (s,c,t)=>Task.FromResult(lib.WriteAgentTool(s,c,t)),
            prepare,()=>{},current,()=>true,c=>lib.OperationStatus(c.id)!="undone");
    }
    static AgentTaskEngine MockEngine(AgentTaskState state,AgentJournal journal){
        Func<AgentTaskState,AgentCall,CancellationToken,Task<Dictionary<string,object>>> read=(s,c,t)=>Task.FromResult(new Dictionary<string,object>{{"text","fixture"}});
        Func<AgentTaskState,AgentCall,CancellationToken,Task<AgentToolReceipt>> write=(s,c,t)=>Task.FromResult(new AgentToolReceipt{result=new Dictionary<string,object>{{"text","changed"}}});
        Func<AgentTaskState,AgentAction,string,string,AgentCall> prepare=(s,a,turn,provider)=>new AgentCall{id=Guid.NewGuid().ToString("N"),tool=a.tool,args=a.args,turnId=turn,providerId=provider,steering=s.steerVersion,status=AgentRules.ReadTools.Contains(a.tool)?"queued":"pending"};
        return new AgentTaskEngine(state,journal,()=>{},null,read,write,prepare,()=>{},c=>true,()=>true,c=>true);
    }
    static AgentCall EmitProposal(AgentTaskEngine engine,AgentCall call){engine.Emit("tool.proposed",new{call=call});return call;}
    static AgentCall Stored(AgentTaskState state,string id){return state.calls.First(c=>c.id==id);}
    static void EmitCompleted(AgentTaskEngine engine,string callId,Dictionary<string,object> result,string stamp=null){engine.Emit("tool.completed",new{callId=callId,result=result,stamp=stamp});}
    static void EmitPlan(AgentTaskEngine engine,params AgentPlanStep[] steps){engine.Emit("task.plan",new{steps=steps,steering=engine.State.steerVersion});}
    static AgentPlanStep Step(string id,string title,params string[] evidence){var step=new AgentPlanStep{id=id,title=title,status="done"};step.evidence=evidence.ToList();return step;}
    static AgentCall FinishCall(AgentTaskState state,string status,string[] evidence){
        object[] checks=evidence==null||evidence.Length==0?new object[0]:new object[]{new{claim="按目标核对实际结果",evidence=evidence}};
        var args=Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(new{status,summary="已核对实际结果和限制",checks}));
        return new AgentCall{id=Guid.NewGuid().ToString("N"),tool="finish_task",args=args,steering=state.steerVersion};
    }
    static string FinishProblem(AgentTaskEngine engine,Func<AgentCall,bool> current,string status,string[] evidence){
        try{AgentCognition.Run(engine,FinishCall(engine.State,status,evidence),current);return "";}
        catch(AgentFailure f){return f.kind+": "+f.Message;}
    }
    static bool FinishSucceeds(AgentTaskEngine engine,Func<AgentCall,bool> current,string status,string[] evidence){
        string problem=FinishProblem(engine,current,status,evidence);
        return problem==""&&engine.State.verification!=null&&engine.State.verification.status==status;
    }

    // ---- real native tool events -----------------------------------------------------------------
    static AgentCall ReadProposal(Library lib,AgentTaskState state,params string[] ids){
        var call=lib.PrepareAgentCall(state,new AgentAction{tool="read_items",args=new Dictionary<string,object>{{"ids",ids.ToList()}}},Guid.NewGuid().ToString("N"),null);
        call.steering=state.steerVersion;return call;
    }
    // v0.12.1 C1: read-back is verified by AgentReading.ReadBackSufficient, so the fixture must emit a real
    // exact read with coverage rows instead of a synthetic text-only receipt.
    static AgentCall EmitReadBack(AgentTaskEngine engine,Library lib,AgentTaskState state,string id){
        return EmitRealRead(engine,lib,state,id);
    }
    static AgentCall EmitRealRead(AgentTaskEngine engine,Library lib,AgentTaskState state,params string[] ids){
        var call=ReadProposal(lib,state,ids);engine.Emit("tool.proposed",new{call=call});engine.Emit("tool.sources",new{callId=call.id,sources=call.sources});
        var result=lib.ReadAgentTool(state,Stored(state,call.id),CancellationToken.None).GetAwaiter().GetResult();
        EmitCompleted(engine,call.id,result);return Stored(state,call.id);
    }
    static AgentCall EmitCreate(AgentTaskEngine engine,Library lib,AgentTaskState state,string tool,Dictionary<string,object> args,out Entry artifact){
        var call=lib.PrepareAgentCall(state,new AgentAction{tool=tool,args=args},Guid.NewGuid().ToString("N"),null);call.steering=state.steerVersion;
        engine.Emit("tool.proposed",new{call=call});
        var receipt=lib.WriteAgentTool(state,Stored(state,call.id),CancellationToken.None);
        EmitCompleted(engine,call.id,receipt.result,receipt.stamp);
        artifact=lib.Find(Convert.ToString(receipt.result["id"]));return Stored(state,call.id);
    }
    static AgentCall EmitCreateDocument(AgentTaskEngine engine,Library lib,AgentTaskState state,string name,string text,out Entry artifact){
        return EmitCreate(engine,lib,state,"create_document",new Dictionary<string,object>{{"name",name},{"format","md"},{"text",text}},out artifact);
    }
    static AgentCall EmitCreatePresentation(AgentTaskEngine engine,Library lib,AgentTaskState state,string title,int pages,out Entry artifact){
        var slides=new List<Dictionary<string,object>>();
        for(int n=1;n<=pages;n++)slides.Add(new Dictionary<string,object>{{"title","第 "+n+" 页"},{"bullets",new[]{"要点 "+n}}});
        return EmitCreate(engine,lib,state,"create_presentation",new Dictionary<string,object>{{"title",title},{"slides",slides}},out artifact);
    }
    static AgentCall EmitReplace(AgentTaskEngine engine,Library lib,AgentTaskState state,string id,string oldText,string newText,out Entry artifact){
        var call=lib.PrepareAgentCall(state,new AgentAction{tool="replace_document_text",args=new Dictionary<string,object>{{"id",id},{"old_text",oldText},{"new_text",newText}}},Guid.NewGuid().ToString("N"),null);call.steering=state.steerVersion;
        engine.Emit("tool.proposed",new{call=call});
        var receipt=lib.WriteAgentTool(state,Stored(state,call.id),CancellationToken.None);
        EmitCompleted(engine,call.id,receipt.result,receipt.stamp);
        artifact=lib.Find(id);return Stored(state,call.id);
    }
    static AgentCall EmitReadDocument(AgentTaskEngine engine,Library lib,AgentTaskState state,string id){
        var call=lib.PrepareAgentCall(state,new AgentAction{tool="read_document",args=new Dictionary<string,object>{{"id",id}}},Guid.NewGuid().ToString("N"),null);call.steering=state.steerVersion;
        engine.Emit("tool.proposed",new{call=call});engine.Emit("tool.sources",new{callId=call.id,sources=call.sources});
        var result=lib.ReadAgentTool(state,Stored(state,call.id),CancellationToken.None).GetAwaiter().GetResult();
        EmitCompleted(engine,call.id,result);return Stored(state,call.id);
    }
    static AgentTaskOptions Options(string goal,List<string> allowed,int limit=6){
        return new AgentTaskOptions{sessionId=Guid.NewGuid().ToString("N"),goal=goal,kernelVersion=3,requestLimit=limit,allowed=allowed??new List<string>(),scope=new AgentScope{mode=allowed!=null&&allowed.Count>0?"selected":"library",ids=allowed??new List<string>()}};
    }

    // ---- C3 derivation: goal -> host-verifiable delivery requirement -----------------------------
    static void Derivation(string root){
        var fx=new Fixture(root);
        var report=new AgentTaskOptions{goal="根据这三份资料生成一份总结报告",allowed=Ids(3)};
        fx.Service.PrepareDelivery(report);
        Verify(report.delivery!=null&&report.delivery.kind=="document"&&report.delivery.requiresArtifact&&!report.delivery.artifactIsPresentation&&report.delivery.requiredSourceIds.Count==3,"delivery: a report goal requires a real document artifact and keeps the original source scope");
        var ppt=new AgentTaskOptions{goal="把这份报告转成 8 页 PPT",allowed=Ids(1)};
        fx.Service.PrepareDelivery(ppt);
        Verify(ppt.delivery.kind=="presentation"&&ppt.delivery.requiresArtifact&&ppt.delivery.artifactIsPresentation&&ppt.delivery.requestedPages==8,"delivery: a conversion to an 8-page deck requires a PPTX artifact with the requested page count");
        var convert=new AgentTaskOptions{goal="把报告转 8 页 PPT",allowed=Ids(1)};
        fx.Service.PrepareDelivery(convert);
        Verify(convert.delivery.kind=="presentation"&&convert.delivery.requiresArtifact&&convert.delivery.artifactIsPresentation&&convert.delivery.requestedPages==8,"delivery: a bare conversion phrase (报告转 8 页 PPT) is recognized as a required deck with page count");
        var longReport=new AgentTaskOptions{goal="根据这三份长资料按最终修订生成一份报告",allowed=Ids(3)};
        fx.Service.PrepareDelivery(longReport);
        Verify(longReport.delivery.requiresArtifact&&longReport.delivery.requiresFullSourceCoverage,"delivery: generating a report from provided long sources requires exact full-source coverage even without the word 全文");
        var brief=new AgentTaskOptions{goal="只看第一份资料，写一段简要摘要",allowed=Ids(3)};
        fx.Service.PrepareDelivery(brief);
        Verify(!brief.delivery.requiresFullSourceCoverage,"delivery: an explicitly partial/single-source summary does not require full-source coverage");
        var chinese=new AgentTaskOptions{goal="制作一个十页的幻灯片介绍方案",allowed=Ids(1)};
        fx.Service.PrepareDelivery(chinese);
        Verify(chinese.delivery.kind=="presentation"&&chinese.delivery.requestedPages==10,"delivery: a Chinese page count (十页) is parsed for deck creation");
        var edit=new AgentTaskOptions{goal="只把志愿者 6 人改为 8 人，其他内容不要动",allowed=Ids(1)};
        fx.Service.PrepareDelivery(edit);
        Verify(edit.delivery.kind=="edit"&&edit.delivery.requiresTargetedEdit&&!edit.delivery.requiresArtifact,"delivery: a targeted revision requires a successful edit rather than a new artifact");
        foreach(string goal in new[]{"修改这份现有报告：只把6名志愿者改为8名志愿者，不要创建新报告，其他文字和预算完全保留。保存后核对。","Edit this existing report; do not create a new document.","Don't create a report, revise this existing document.","不要新建报告而是修改这份报告。","Rewrite this existing report without creating another document."}){
            var revision=new AgentTaskOptions{goal=goal,allowed=Ids(1)};fx.Service.PrepareDelivery(revision);
            Verify(revision.delivery.kind=="edit"&&revision.delivery.requiresTargetedEdit&&!revision.delivery.requiresArtifact,"delivery: a prohibition on creating documents does not demand a new artifact: "+goal);
        }
        var preserveOriginal=new AgentTaskOptions{goal="不要修改现有报告，创建一份新的摘要文档。",allowed=Ids(1)};fx.Service.PrepareDelivery(preserveOriginal);
        Verify(preserveOriginal.delivery.kind=="document"&&!preserveOriginal.delivery.requiresTargetedEdit,"delivery: a prohibited edit does not hide a later positive creation request");
        var continuation=new AgentTaskOptions{goal="续写接下来的情节",allowed=Ids(1)};
        fx.Service.PrepareDelivery(continuation);
        Verify(!continuation.delivery.requiresTargetedEdit&&!continuation.delivery.requiresArtifact,"delivery: a continuation goal is content generation, not a body-edit requirement");
        var targeted=new AgentTaskOptions{goal="把报告中的志愿者人数从 6 改成 8",allowed=Ids(1)};
        fx.Service.PrepareDelivery(targeted);
        Verify(targeted.delivery.kind=="edit"&&targeted.delivery.requiresTargetedEdit,"delivery: an explicit existing target plus a replacement verb is a targeted edit");
        var paragraph=new AgentTaskOptions{goal="把这份报告第 2 段改成新的开头",allowed=Ids(1)};
        fx.Service.PrepareDelivery(paragraph);
        Verify(paragraph.delivery.kind=="edit"&&paragraph.delivery.requiresTargetedEdit,"delivery: a page/paragraph-anchored rewrite is a targeted edit");
        var noteSummary=new AgentTaskOptions{goal="给这份笔记打标签并写一份摘要",allowed=Ids(1)};
        fx.Service.PrepareDelivery(noteSummary);
        Verify(!noteSummary.delivery.requiresTargetedEdit,"delivery: metadata tagging plus summary creation is not a targeted body edit");
        var englishNote=new AgentTaskOptions{goal="Append this note",allowed=Ids(1)};
        fx.Service.PrepareDelivery(englishNote);
        Verify(!englishNote.delivery.requiresTargetedEdit,"delivery: an English note-append goal (write_note) is not a body-edit requirement");
        var metadata=new AgentTaskOptions{goal="为这两份资料建议添加标签，先提出方案让我确认",allowed=Ids(2)};
        fx.Service.PrepareDelivery(metadata);
        Verify(!metadata.delivery.requiresTargetedEdit&&!metadata.delivery.requiresArtifact,"delivery: metadata-only work (tags/notes/collections) is not misread as a body rewrite requirement");
        var coverage=new AgentTaskOptions{goal="通读全部资料，回答有哪些风险",allowed=Ids(3)};
        fx.Service.PrepareDelivery(coverage);
        Verify(coverage.delivery.requiresFullSourceCoverage&&!coverage.delivery.requiresArtifact&&coverage.delivery.requiredSourceIds.Count==3,"delivery: a read-everything goal requires exact full-source coverage");
        var question=new AgentTaskOptions{goal="这份资料讲了什么",allowed=Ids(1)};
        fx.Service.PrepareDelivery(question);
        Verify(question.delivery.kind=="read"&&!question.delivery.requiresArtifact&&!question.delivery.requiresTargetedEdit&&!question.delivery.requiresFullSourceCoverage,"delivery: a plain question has no delivery requirement");
        var discuss=new AgentTaskOptions{goal="生成一份报告",intent="discuss",allowed=Ids(1)};
        fx.Service.PrepareDelivery(discuss);
        Verify(!discuss.delivery.requiresArtifact,"delivery: discussion-only tasks never require a written artifact");
        var readOnly=new AgentTaskOptions{goal="生成一份报告",approvalPolicy="read_only",allowed=Ids(1)};
        fx.Service.PrepareDelivery(readOnly);
        Verify(!readOnly.delivery.requiresArtifact,"delivery: read-only tasks never require a written artifact");
    }

    // ---- required artifact + current read-back ---------------------------------------------------
    static void ArtifactAcceptance(string root){
        var fx=new Fixture(root);var lib=fx.Lib;
        var first=lib.CreateDocument("资料甲","md","","资料甲：预算总额 12,600 元，资金上限 15,000 元。");
        var second=lib.CreateDocument("资料乙","md","","资料乙：有效反馈至少 60 份，验收负责人江岚。");
        var options=Options("根据这两份资料生成一份总结报告",new List<string>{first.id,second.id});fx.Service.PrepareDelivery(options);
        var journal=new AgentJournal(lib.DataDir);
        // 1) report required, only a read happened -> completed rejected with actionable text, no review.
        var state=Seed(journal,options);var engine=RealEngine(lib,journal,state);engine.DeliveryCheck=s=>fx.Service.DeliveryProblem(s);
        var read=EmitRealRead(engine,lib,state,first.id);
        string problem=fx.Service.DeliveryProblem(state);
        Verify(problem.Contains("没有成功创建")&&problem.Contains("create_document")&&problem.Contains("保留"),"delivery: without any real artifact the host names the missing create tool and asks to keep passed work");
        string rejected=FinishProblem(engine,c=>Current(lib,state,c),"completed",new[]{read.id});
        Verify(rejected.StartsWith("verification")&&rejected.Contains("没有成功创建")&&state.verification==null,"delivery: finish_task(completed) throws a verification failure instead of recording a fake completion");
        // 2) real create + current read-back of the artifact + full coverage of the given sources.
        var state2=Seed(journal,options);var engine2=RealEngine(lib,journal,state2);engine2.DeliveryCheck=s=>fx.Service.DeliveryProblem(s);
        Entry report;var created=EmitCreateDocument(engine2,lib,state2,"交付总结报告","# 总结\n\n预算 12,600 元；反馈至少 60 份。",out report);
        Verify(fx.Service.DeliveryProblem(state2).Contains("回读"),"delivery: an artifact without a post-creation read-back is rejected with a read-back instruction");
        var back=EmitRealRead(engine2,lib,state2,report.id);
        string uncovered=fx.Service.DeliveryProblem(state2);
        Verify(uncovered.Contains("覆盖")||uncovered.Contains("未读"),"delivery: a report created from provided sources still requires those sources to be read exactly");
        var firstRead=EmitRealRead(engine2,lib,state2,first.id);var secondRead=EmitRealRead(engine2,lib,state2,second.id);
        Verify(fx.Service.DeliveryProblem(state2)=="","delivery: a real owned artifact, its current read-back and full-source coverage satisfy the host check");
        EmitPlan(engine2,Step("create","创建报告",created.id),Step("read-back","回读报告",back.id),Step("read-a","读取资料甲",firstRead.id),Step("read-b","读取资料乙",secondRead.id));
        Verify(FinishSucceeds(engine2,c=>Current(lib,state2,c),"completed",new[]{secondRead.id}),"delivery: finish_task(completed) passes after a real artifact, its read-back and full-source coverage");
        // 3) artifact created by this task then undone -> rejected again (undo removes the deliverable).
        var state3=Seed(journal,options);var engine3=RealEngine(lib,journal,state3);engine3.DeliveryCheck=s=>fx.Service.DeliveryProblem(s);
        Entry doomed;var doomedCall=EmitCreateDocument(engine3,lib,state3,"将被撤销的报告","# 草稿",out doomed);EmitRealRead(engine3,lib,state3,doomed.id);EmitRealRead(engine3,lib,state3,first.id);EmitRealRead(engine3,lib,state3,second.id);
        Verify(fx.Service.DeliveryProblem(state3)=="","delivery: created+read-back artifact with covered sources passes before undo");
        lib.OperationUndo(doomedCall.id);engine3.Emit("tool.undone",new{callId=doomedCall.id});
        Verify(fx.Service.DeliveryProblem(state3).Contains("已不在资料库"),"delivery: an undone/deleted artifact stops counting as a deliverable");
        // 4) A foreign file cannot satisfy the requirement: a receipt that was not created by this task's
        // durable operation (or host-maintained artifact set) is not an owned deliverable. Build the state
        // directly so no host tool.completed event can vouch for the foreign id.
        var foreign=lib.CreateDocument("别人的报告","md","","不是本任务创建的成果");
        var state4=new AgentTaskState{id=Guid.NewGuid().ToString("N"),options=options,calls=new List<AgentCall>{new AgentCall{id=Guid.NewGuid().ToString("N"),tool="create_document",status="done",result=new Dictionary<string,object>{{"id",foreign.id}}}}};
        Verify(fx.Service.DeliveryProblem(state4)!="","delivery: a file this task did not create cannot be claimed as its deliverable");
        // 5) v0.11.2 compatibility: a journal persisted before the delivery field existed replays with
        // delivery=null and must keep the old finish semantics even when the current host wires the check.
        var legacyOptions=new AgentTaskOptions{sessionId=Guid.NewGuid().ToString("N"),goal="创建并保存一份总结报告",kernelVersion=3,requestLimit=6,allowed=new List<string>()};
        var legacyState=Seed(journal,legacyOptions);
        Verify(legacyState.options.delivery==null,"delivery: a pre-0.12 journal replays with delivery=null");
        var legacyEngine=MockEngine(legacyState,journal);legacyEngine.DeliveryCheck=s=>fx.Service.DeliveryProblem(s);
        var legacyRead=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=new Dictionary<string,object>{{"ids",new List<string>{new string('a',32)}}},status="queued",steering=legacyState.steerVersion};
        legacyEngine.Emit("tool.proposed",new{call=legacyRead});legacyEngine.Emit("tool.completed",new{callId=legacyRead.id,result=new{text="历史任务读取结果"}});
        Verify(FinishSucceeds(legacyEngine,c=>true,"completed",new[]{legacyRead.id}),"delivery: a resumed pre-0.12 task without a persisted requirement is not blocked by the delivery layer");
        // 6) Regression from the desktop UI suite: "续写接下来的情节" is generation, so a completed finish
        // without any replace_document_text/edit_presentation must not be blocked by the delivery layer.
        var continueOptions=new AgentTaskOptions{sessionId=Guid.NewGuid().ToString("N"),goal="续写接下来的情节",kernelVersion=3,requestLimit=6,allowed=new List<string>()};
        fx.Service.PrepareDelivery(continueOptions);
        var continueState=Seed(journal,continueOptions);
        Verify(fx.Service.DeliveryProblem(continueState)=="","delivery: a continuation goal has no host delivery requirement");
        var continueEngine=MockEngine(continueState,journal);continueEngine.DeliveryCheck=s=>fx.Service.DeliveryProblem(s);
        var continueRead=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=new Dictionary<string,object>{{"ids",new List<string>{new string('b',32)}}},status="queued",steering=continueState.steerVersion};
        continueEngine.Emit("tool.proposed",new{call=continueRead});continueEngine.Emit("tool.completed",new{callId=continueRead.id,result=new{text="情节前文"}});
        Verify(FinishSucceeds(continueEngine,c=>true,"completed",new[]{continueRead.id}),"delivery: finish_task(completed) passes for a continuation goal without any edit call");
    }

    // ---- requested PPT page count ----------------------------------------------------------------
    static void PageCountAcceptance(string root){
        var fx=new Fixture(root);var lib=fx.Lib;
        var options=Options("把资料整理成 3 页 PPT",new List<string>());fx.Service.PrepareDelivery(options);
        var journal=new AgentJournal(lib.DataDir);
        var wrong=Seed(journal,options);var wrongEngine=RealEngine(lib,journal,wrong);wrongEngine.DeliveryCheck=s=>fx.Service.DeliveryProblem(s);
        Entry five;var fiveCall=EmitCreatePresentation(wrongEngine,lib,wrong,"五页演示",5,out five);var fiveBack=EmitReadBack(wrongEngine,lib,wrong,five.id);
        string problem=fx.Service.DeliveryProblem(wrong);
        Verify(problem.Contains("实际 5 页")&&problem.Contains("3 页")&&problem.Contains("保留"),"delivery: a deck whose real page count differs from the request is rejected with both numbers");
        EmitPlan(wrongEngine,Step("create","创建五页演示",fiveCall.id),Step("read","回读五页演示",fiveBack.id));
        string rejected=FinishProblem(wrongEngine,c=>Current(lib,wrong,c),"completed",new[]{fiveCall.id});
        Verify(rejected.Contains("实际 5 页")&&wrong.verification==null,"delivery: finish_task(completed) is rejected when the PPTX page count is wrong");
        var right=Seed(journal,options);var rightEngine=RealEngine(lib,journal,right);rightEngine.DeliveryCheck=s=>fx.Service.DeliveryProblem(s);
        Entry three;var threeCall=EmitCreatePresentation(rightEngine,lib,right,"三页演示",3,out three);var threeBack=EmitReadBack(rightEngine,lib,right,three.id);
        Verify(fx.Service.DeliveryProblem(right)=="","delivery: a deck with exactly the requested page count passes");
        EmitPlan(rightEngine,Step("create","创建三页演示",threeCall.id),Step("read","回读三页演示",threeBack.id));
        Verify(FinishSucceeds(rightEngine,c=>Current(lib,right,c),"completed",new[]{threeBack.id}),"delivery: completed is accepted when the real PPTX page count equals the request");
    }

    // ---- targeted revision ----------------------------------------------------------------------
    static void TargetedEditAcceptance(string root){
        var fx=new Fixture(root);var lib=fx.Lib;
        var doc=lib.CreateDocument("志愿者名单","md","","活动说明。\n\n志愿者 6 人。\n\n其他内容必须保留。");
        var options=Options("修改这份现有报告：只把志愿者 6 人改为 8 人，不要创建新报告，其他文字和预算完全保留。保存后核对。",new List<string>{doc.id});fx.Service.PrepareDelivery(options);
        var journal=new AgentJournal(lib.DataDir);
        var state=Seed(journal,options);var engine=RealEngine(lib,journal,state);engine.DeliveryCheck=s=>fx.Service.DeliveryProblem(s);
        string untried=fx.Service.DeliveryProblem(state);
        Verify(untried.Contains("定点改稿")&&untried.Contains("replace_document_text"),"delivery: a revision goal without any edit call is rejected and names the edit tool");
        string rejected=FinishProblem(engine,c=>Current(lib,state,c),"completed",new string[0]);
        Verify(rejected.Contains("定点改稿")&&state.verification==null,"delivery: finish_task(completed) is rejected when the requested edit never happened");
        Entry edited;var editCall=EmitReplace(engine,lib,state,doc.id,"志愿者 6 人","志愿者 8 人",out edited);var after=EmitReadDocument(engine,lib,state,doc.id);
        Verify(fx.Service.DeliveryProblem(state)=="","delivery: a successful current replace_document_text plus read-back satisfies the revision requirement");
        string text=lib.DocumentSource(doc.id).text;
        Verify(text.Contains("志愿者 8 人")&&text.Contains("其他内容必须保留")&&!text.Contains("志愿者 6 人"),"delivery: the targeted edit changes the requested text and preserves unrelated content");
        EmitPlan(engine,Step("edit","完成定点改稿",editCall.id),Step("read","回读修改后正文",after.id));
        Verify(FinishSucceeds(engine,c=>Current(lib,state,c),"completed",new[]{after.id}),"delivery: finish_task(completed) passes after the requested revision is applied and read back");
    }

    // ---- full-source coverage -------------------------------------------------------------------
    static void FullSourceCoverage(string root){
        var fx=new Fixture(root);var lib=fx.Lib;
        var first=lib.CreateDocument("资料甲","md","","资料甲：预算总额 12,600 元，资金上限 15,000 元。");
        var second=lib.CreateDocument("资料乙","md","","资料乙：有效反馈至少 60 份，验收负责人江岚。");
        var options=Options("通读全部资料，写清楚两个来源的关键事实",new List<string>{first.id,second.id});fx.Service.PrepareDelivery(options);
        var journal=new AgentJournal(lib.DataDir);
        var state=Seed(journal,options);var engine=RealEngine(lib,journal,state);engine.DeliveryCheck=s=>fx.Service.DeliveryProblem(s);
        string none=fx.Service.DeliveryProblem(state);
        Verify(none.Contains("覆盖")||none.Contains("读取"),"delivery: a full-source goal with no reads is rejected with a coverage problem");
        var firstRead=EmitRealRead(engine,lib,state,first.id);
        string partial=fx.Service.DeliveryProblem(state);
        Verify(partial!=""&&(partial.Contains("未读")||partial.Contains("覆盖")),"delivery: reading only one source still leaves full-source coverage incomplete");
        var secondRead=EmitRealRead(engine,lib,state,second.id);
        Verify(fx.Service.DeliveryProblem(state)=="","delivery: exact reads of every source satisfy full-source coverage");
        EmitPlan(engine,Step("read-a","读取资料甲",firstRead.id),Step("read-b","读取资料乙",secondRead.id));
        Verify(FinishSucceeds(engine,c=>Current(lib,state,c),"completed",new[]{secondRead.id}),"delivery: finish_task(completed) is accepted after all sources are exactly read");
    }

    // ---- P1 repaired failures -------------------------------------------------------------------
    static void RepairedFailureSemantics(string root){
        var lib=new Library(root);lib.SetAppMode("smart",true);var journal=new AgentJournal(lib.DataDir);
        var readArgs=new Dictionary<string,object>{{"ids",new List<string>{new string('b',32)}}};
        // Repaired failure: the same tool+args succeeds later -> completed passes.
        var repaired=Seed(journal,Options("核对读取结果",new List<string>()));
        var repairedEngine=MockEngine(repaired,journal);
        var failed=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=readArgs,status="queued",steering=repaired.steerVersion};
        repairedEngine.Emit("tool.proposed",new{call=failed});
        repairedEngine.Emit("tool.failed",new{callId=failed.id,kind="source",message="资料读取失败",retryable=false});
        var retry=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=readArgs,status="queued",steering=repaired.steerVersion};
        repairedEngine.Emit("tool.proposed",new{call=retry});
        repairedEngine.Emit("tool.completed",new{callId=retry.id,result=new{text="读取成功"}});
        Verify(FinishSucceeds(repairedEngine,c=>true,"completed",new[]{retry.id}),"P1: a failed read repaired by a later successful same-tool same-args call no longer blocks completed");
        // Unrepaired failure still blocks; partial is unaffected.
        var broken=Seed(journal,Options("核对读取结果",new List<string>()));
        var brokenEngine=MockEngine(broken,journal);
        var brokenCall=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=readArgs,status="queued",steering=broken.steerVersion};
        brokenEngine.Emit("tool.proposed",new{call=brokenCall});
        brokenEngine.Emit("tool.failed",new{callId=brokenCall.id,kind="source",message="资料读取失败",retryable=false});
        var fallback=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=new Dictionary<string,object>{{"ids",new List<string>{new string('c',32)}}},status="queued",steering=broken.steerVersion};
        brokenEngine.Emit("tool.proposed",new{call=fallback});brokenEngine.Emit("tool.completed",new{callId=fallback.id,result=new{text="另一份资料读取成功"}});
        string blocked=FinishProblem(brokenEngine,c=>true,"completed",new[]{fallback.id});
        Verify(blocked.StartsWith("verification")&&blocked.Contains("未解决")&&blocked.Contains("partial")&&broken.verification==null,"P1: an unrepaired failure still blocks completed and points at partial/repair");
        Verify(FinishSucceeds(brokenEngine,c=>true,"partial",new[]{fallback.id}),"P1: partial is not affected by unrepaired failures");
        // uncertain_write is never repaired automatically.
        var uncertain=Seed(journal,Options("核对读取结果",new List<string>()));
        var uncertainEngine=MockEngine(uncertain,journal);
        var uncertainCall=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=readArgs,status="queued",steering=uncertain.steerVersion};
        uncertainEngine.Emit("tool.proposed",new{call=uncertainCall});
        uncertainEngine.Emit("tool.failed",new{callId=uncertainCall.id,kind="uncertain_write",message="结果不确定，需人工核对",retryable=false});
        var uncertainRetry=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=readArgs,status="queued",steering=uncertain.steerVersion};
        uncertainEngine.Emit("tool.proposed",new{call=uncertainRetry});
        uncertainEngine.Emit("tool.completed",new{callId=uncertainRetry.id,result=new{text="读取成功"}});
        string uncertainProblem=FinishProblem(uncertainEngine,c=>true,"completed",new[]{uncertainRetry.id});
        Verify(uncertainProblem.StartsWith("verification")&&uncertainProblem.Contains("未解决")&&uncertain.verification==null,"P1: an uncertain write failure is not repaired by a same-args success and still blocks completed");
        // skipped and undone still block; a resolver that was later undone stops counting.
        var skipped=Seed(journal,Options("核对读取结果",new List<string>()));
        var skippedEngine=MockEngine(skipped,journal);
        var skippedCall=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="write_note",args=new Dictionary<string,object>{{"id",new string('d',32)},{"text","x"}},status="pending",steering=skipped.steerVersion};
        skippedEngine.Emit("tool.proposed",new{call=skippedCall});skippedEngine.Emit("tool.skipped",new{callId=skippedCall.id,reason="user"});
        var skippedDone=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=readArgs,status="queued",steering=skipped.steerVersion};
        skippedEngine.Emit("tool.proposed",new{call=skippedDone});skippedEngine.Emit("tool.completed",new{callId=skippedDone.id,result=new{text="ok"}});
        string skippedProblem=FinishProblem(skippedEngine,c=>true,"completed",new[]{skippedDone.id});
        Verify(skippedProblem.Contains("未解决")&&skipped.verification==null,"P1: a skipped step still blocks completed");
        var undoneSeed=Seed(journal,Options("核对读取结果",new List<string>()));
        var undoneEngine=MockEngine(undoneSeed,journal);
        var undoneCall=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="write_note",args=new Dictionary<string,object>{{"id",new string('e',32)},{"text","y"}},status="pending",steering=undoneSeed.steerVersion};
        undoneEngine.Emit("tool.proposed",new{call=undoneCall});undoneEngine.Emit("tool.completed",new{callId=undoneCall.id,result=new{text="changed"}});
        undoneEngine.Emit("tool.undone",new{callId=undoneCall.id});
        var undoneDone=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=readArgs,status="queued",steering=undoneSeed.steerVersion};
        undoneEngine.Emit("tool.proposed",new{call=undoneDone});undoneEngine.Emit("tool.completed",new{callId=undoneDone.id,result=new{text="ok"}});
        string undoneProblem=FinishProblem(undoneEngine,c=>true,"completed",new[]{undoneDone.id});
        Verify(undoneProblem.Contains("未解决")&&undoneSeed.verification==null,"P1: an undone step still blocks completed");
        var resolverUndone=Seed(journal,Options("核对读取结果",new List<string>()));
        var resolverEngine=MockEngine(resolverUndone,journal);
        var resolverFailed=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=readArgs,status="queued",steering=resolverUndone.steerVersion};
        resolverEngine.Emit("tool.proposed",new{call=resolverFailed});resolverEngine.Emit("tool.failed",new{callId=resolverFailed.id,kind="source",message="失败",retryable=false});
        var resolver=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=readArgs,status="queued",steering=resolverUndone.steerVersion};
        resolverEngine.Emit("tool.proposed",new{call=resolver});resolverEngine.Emit("tool.completed",new{callId=resolver.id,result=new{text="ok"}});
        resolverEngine.Emit("tool.undone",new{callId=resolver.id});
        var evidenceAfter=new AgentCall{id=Guid.NewGuid().ToString("N"),tool="read_items",args=new Dictionary<string,object>{{"ids",new List<string>{new string('f',32)}}},status="queued",steering=resolverUndone.steerVersion};
        resolverEngine.Emit("tool.proposed",new{call=evidenceAfter});resolverEngine.Emit("tool.completed",new{callId=evidenceAfter.id,result=new{text="ok"}});
        string resolverProblem=FinishProblem(resolverEngine,c=>true,"completed",new[]{evidenceAfter.id});
        Verify(resolverProblem.Contains("未解决")&&resolverUndone.verification==null,"P1: undoing the repairing call invalidates the resolution and restores the block");
    }
}

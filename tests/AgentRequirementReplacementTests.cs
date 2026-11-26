using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using kok;

static class AgentRequirementReplacementTests {
    static string Id(){return Guid.NewGuid().ToString("N");}
    static Dictionary<string,object> D(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    static AgentTaskEngine Engine(AgentTaskState state,AgentJournal journal){
        AgentTaskEngine engine=null;
        engine=new AgentTaskEngine(state,journal,()=>{},(e,t)=>Task.FromException<AgentModelTurn>(new AgentFailure("fixture_end","No model request is needed in this recovery fixture.")),(s,c,t)=>Task.FromResult(new Dictionary<string,object>()),
            (s,c,t)=>Task.FromResult(new AgentToolReceipt{result=engine.ConfirmTaskSpec(c)}),(s,a,t,p)=>new AgentCall(),()=>{});
        return engine;
    }
    static bool Fails(Action action,string kind){try{action();return false;}catch(AgentFailure error){return error.kind==kind;}}
    public static void Run(string root,Action<bool,string> check){
        var library=new Library(root);library.SetAppMode("smart",true);AgentBusinessCore.Configure(AppDomain.CurrentDomain.BaseDirectory);
        var source=library.CreateDocument("给定资料","md","","最终预算8万元，60人，不收集手机号。未知信息待确认。");string sourceHash=AgentWorkspaceTools.Hash(File.ReadAllBytes(source.path));
        string goal="根据资料创建400-600汉字DOCX报告，并制作8页PPT。",change="这回文字材料那份先缓一缓，只留下用于展示的那份。";
        var options=new AgentTaskOptions{runtime="research",requirementsVersion=1,artifactReviewVersion=2,goal=goal,sessionId=Id(),allowedToolsAuto=true,allowed=new List<string>{source.id},scope=new AgentScope{mode="selected",ids=new List<string>{source.id}}};
        AgentBusinessCore.Prepare(options,library,AppDomain.CurrentDomain.BaseDirectory);
        var journal=new AgentJournal(root);var state=new AgentTaskState{id=Id(),options=options};var engine=Engine(state,journal);engine.Emit("task.created",new{options,protocol="tools"});engine.Emit("task.adjusted",new{text=change});
        AgentCall Proposal(object changes=null){return new AgentCall{id=Id(),tool="propose_task_spec",status="pending",steering=state.steerVersion,requirementRevision=state.RequirementRevision,args=D(new{baseRevision=state.SpecRevision,userRevision=state.adjustments.Count,basis=state.adjustments.Last(),changes=changes??new{document=new{required=false}}})};}
        var tool=AgentRules.Tools.Single(t=>t.Name=="propose_task_spec");check(tool.Write&&AgentCognition.IsControl(tool.Name)&&!AgentApproval.Eligible(state,new AgentCall{tool=tool.Name}),"requirements: conflicting changes use an individually reviewed metadata proposal, outside reusable write grants");
        var call=Proposal();AgentRequirementUpdates.Prepare(state,call);var initial=AgentContracts.Task(state);
        check(initial.deliverables.Count==2&&call.beforeText.Contains("\"RequiresDocument\":true")&&call.afterText.Contains("\"RequiresDocument\":false")&&state.RequirementRevision==0,"requirements: an unparsed cancellation produces an actual before/after preview without changing current obligations");
        check(Fails(()=>engine.UpdateTaskSpec(new AgentCall{id=Id(),tool="update_task_spec",steering=state.steerVersion,args=call.args}),"validation"),"requirements: a read-only refinement cannot apply the same conflicting cancellation");
        check(Fails(()=>engine.ConfirmTaskSpec(call),"approval"),"requirements: model arguments or a pending status cannot authorize a replacement");
        var unapproved=Proposal();AgentRequirementUpdates.Prepare(state,unapproved);engine.Emit("tool.proposed",new{call=unapproved});engine.Emit("tool.started",new{callId=unapproved.id});
        check(Fails(()=>engine.ConfirmTaskSpec(state.calls.Single(c=>c.id==unapproved.id)),"approval"),"requirements: a generic tool-start record cannot impersonate individual confirmation");engine.Emit("tool.failed",new{callId=unapproved.id,kind="approval",message="Synthetic unapproved start refused",retryable=false});
        engine.Emit("tool.proposed",new{call});engine.IsRunning=true;engine.Decide(call.id,false).GetAwaiter().GetResult();
        check(state.RequirementRevision==0&&AgentContracts.Task(state).deliverables.Count==2&&state.calls.Single(c=>c.id==call.id).status=="skipped","requirements: declining the proposal preserves the old document and presentation requirements");
        check(Fails(()=>AgentRequirementUpdates.Prepare(state,Proposal()),"policy"),"requirements: the same declined change cannot be proposed again without a new user instruction");
        engine.Emit("task.adjusted",new{text=change+"我确认这个意图。"});var tampered=Proposal();AgentRequirementUpdates.Prepare(state,tampered);
        check(tampered.beforeText.Length>0&&state.RequirementRevision==0,"requirements: a later actual user message permits a newly grounded proposal");
        tampered.afterText="{}";engine.Emit("tool.proposed",new{call=tampered});engine.Decide(tampered.id,true).GetAwaiter().GetResult();
        check(state.RequirementRevision==0&&state.calls.Single(c=>c.id==tampered.id).errorKind=="conflict","requirements: approval cannot apply a preview that differs from the current validated projection");
        var stale=Proposal();AgentRequirementUpdates.Prepare(state,stale);engine.Emit("tool.proposed",new{call=stale});
        check(Fails(()=>engine.Decide(stale.id,true,batch:true).GetAwaiter().GetResult(),"approval"),"requirements: batch approval cannot accept a conflicting requirement proposal");
        check(Fails(()=>engine.Decide(stale.id,true,grant:true).GetAwaiter().GetResult(),"approval"),"requirements: an ongoing task grant cannot accept a conflicting requirement proposal");
        engine.Emit("task.adjusted",new{text=change+"仍按现有资料。"});engine.Decide(stale.id,true).GetAwaiter().GetResult();
        check(state.RequirementRevision==0&&state.calls.Single(c=>c.id==stale.id).errorKind=="superseded","requirements: a new user revision between preview and approval makes the old proposal inapplicable");
        var accepted=Proposal();AgentRequirementUpdates.Prepare(state,accepted);engine.Emit("tool.proposed",new{call=accepted});engine.Decide(accepted.id,true).GetAwaiter().GetResult();engine.IsRunning=false;
        var spec=AgentContracts.Task(state);var receipt=state.calls.Single(c=>c.id==accepted.id);
        check(state.RequirementRevision==1&&spec.deliverables.Count==1&&spec.deliverables[0].kind=="presentation"&&spec.deliverables[0].pages==8&&spec.excludedDeliverables.Contains("document"),"requirements: individual approval cancels only the requested document and preserves the eight-page presentation");
        check(receipt.status=="done"&&ModuleContext.Text(receipt.result,"interpretation")=="user_confirmed"&&AgentRequirementUpdates.IndividuallyApproved(state,receipt),"requirements: the actual native confirmation and applied interpretation remain in the durable record");
        check(AgentWorkspaceTools.Hash(File.ReadAllBytes(source.path))==sourceHash&&state.options.allowed.SequenceEqual(new[]{source.id})&&state.options.requestLimit==6&&state.options.approvalPolicy=="each"&&state.createdArtifactIds.Count==0,"requirements: confirming a change preserves source bytes, scope, budgets and files");
        check(Fails(()=>AgentRequirementUpdates.Prepare(state,Proposal(new{document=new{required=true},sourceIds=new[]{Id()}})),"validation"),"requirements: the confirmed-change route cannot widen the allowed source scope");
        var injected=Proposal(new{document=new{required=true}});injected.args["approved"]=true;
        check(Fails(()=>AgentRequirementUpdates.Prepare(state,injected),"validation"),"requirements: the shared strict schema rejects model-supplied approval claims");
        bool noOp=false,safe=false;try{AgentRequirementUpdates.Prepare(state,Proposal(new{presentation=new{pages=8}}));}catch(FormatException){noOp=true;}
        try{AgentRequirementUpdates.Prepare(state,Proposal(new{purpose="用于展示"}));}catch(FormatException){safe=true;}
        check(noOp,"requirements: a no-op does not generate an approval proposal");check(safe,"requirements: a safe refinement uses the existing automatic path instead of requesting redundant approval");
        int started=state.events.First(e=>e.kind=="tool.started"&&AgentTaskState.Get<string>(e.data,"callId")==accepted.id).sequence;
        int applied=state.events.Single(e=>e.kind=="task.requirements_updated"&&AgentTaskState.Get<string>(e.data,"callId")==accepted.id).sequence;
        foreach(var limit in new[]{started,applied}){
            string recoveryRoot=Path.Combine(root,"recovery-"+limit);var recoveredJournal=new AgentJournal(recoveryRoot);
            foreach(var entry in journal.Events(state.id).Where(e=>e.sequence<=limit))recoveredJournal.Append(state.id,entry.kind,entry.data,options.sessionId,goal,"running");
            var recovered=AgentTaskState.Replay(state.id,recoveredJournal.Events(state.id));var replayEngine=Engine(recovered,recoveredJournal);replayEngine.Start();replayEngine.Completion.GetAwaiter().GetResult();
            check(recovered.calls.Single(c=>c.id==accepted.id).status=="done"&&recovered.RequirementRevision==1&&recoveredJournal.Events(state.id).Count(e=>e.kind=="task.requirements_updated")==1&&!AgentContracts.Task(recovered).deliverables.Any(d=>d.kind=="document"),limit==started?"requirements: interruption after approval but before the update safely completes only the approved metadata change":"requirements: interruption after the update reuses it and completes the receipt without duplicating the change");
        }
        var readOnly=AgentTaskState.Replay(state.id,journal.Events(state.id).Where(e=>e.sequence<=started).ToList());readOnly.options.approvalPolicy="read_only";
        check(Fails(()=>Engine(readOnly,journal).ConfirmTaskSpec(readOnly.calls.Single(c=>c.id==accepted.id)),"approval"),"requirements: a read-only policy cannot replay a conflicting change even with an old approval record");
        engine.Emit("task.adjusted",new{text="现在再写一份DOCX报告，其他不变。"});var restored=AgentContracts.Task(state);
        check(restored.deliverables.Any(d=>d.kind=="document")&&!restored.excludedDeliverables.Contains("document")&&restored.deliverables.Any(d=>d.kind=="presentation"&&d.pages==8),"requirements: a later clear user instruction can explicitly restore a previously cancelled artifact");
    }
}

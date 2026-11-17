using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using kok;

static class AgentRequirementsV163Tests {
    static string Id(){return Guid.NewGuid().ToString("N");}
    static Dictionary<string,object> D(object value){return Library.Json.Deserialize<Dictionary<string,object>>(Library.Json.Serialize(value));}
    static AgentTaskEngine Engine(AgentTaskState state,AgentJournal journal){return new AgentTaskEngine(state,journal,()=>{},(e,t)=>Task.FromResult(new AgentModelTurn()),(s,c,t)=>Task.FromResult(new Dictionary<string,object>()),(s,c,t)=>Task.FromResult(new AgentToolReceipt()),(s,a,t,p)=>new AgentCall(),()=>{});}
    public static void Run(string root,Action<bool,string> check){
        var library=new Library(root);library.SetAppMode("smart",true);AgentBusinessCore.Configure(AppDomain.CurrentDomain.BaseDirectory);
        var source=library.CreateDocument("输入资料","md","","最终预算8万元，60人，不收集手机号。未知信息待确认。");string hash=AgentWorkspaceTools.Hash(File.ReadAllBytes(source.path));
        string goal="根据资料创建400-600汉字DOCX报告。按研究范围和证据质量组织。";
        var options=new AgentTaskOptions{runtime="research",requirementsVersion=1,artifactReviewVersion=2,goal=goal,sessionId=Id(),allowedToolsAuto=true,allowed=new List<string>{source.id},scope=new AgentScope{mode="selected",ids=new List<string>{source.id}}};
        AgentBusinessCore.Prepare(options,library,AppDomain.CurrentDomain.BaseDirectory);var journal=new AgentJournal(root);var state=new AgentTaskState{id=Id(),options=options};var engine=Engine(state,journal);engine.Emit("task.created",new{options,protocol="tools"});
        var schema=AgentRules.Tools.Single(tool=>tool.Name=="update_task_spec");check(!schema.Write&&AgentCognition.IsControl(schema.Name),"v163: typed requirement changes use the existing native control boundary without granting file writes");
        var initial=AgentContracts.Task(state);check(initial.requirementsVersion==1&&initial.revision==0&&initial.userRevision==0,"v163: new TaskSpec exposes independent current spec and user revisions");
        var pending=new AgentCall{id=Id(),tool="create_presentation",status="pending",steering=0};engine.Emit("tool.proposed",new{call=pending});
        var call=new AgentCall{id=Id(),tool="update_task_spec",steering=0,args=D(new{baseRevision=0,userRevision=0,basis="按研究范围和证据质量组织",changes=new{document=new{sections=new[]{"研究范围","证据质量"}}}})};engine.Emit("tool.proposed",new{call});engine.Emit("tool.started",new{callId=call.id});
        var result=engine.UpdateTaskSpec(call);check(Convert.ToBoolean(result["changed"])&&state.RequirementRevision==1,"v163: the host independently validates the SDK delta and durably increments its requirement revision");
        var spec=AgentContracts.Task(state);check(spec.revision==1&&spec.deliverables.Single().sections.SequenceEqual(new[]{"研究范围","证据质量"})&&spec.deliverables.Single().minLength==400&&spec.deliverables.Single().format=="DOCX","v163: typed section gaps preserve the existing real format and length acceptance conditions");
        var storedPending=state.calls.Single(c=>c.id==pending.id);check(storedPending.status=="skipped"&&state.SupersededProposal(storedPending),"v163: an unstarted sibling write prepared against the old requirement version is superseded safely");
        var replay=AgentTaskState.Replay(state.id,journal.Events(state.id));var restored=Engine(replay,journal);int before=journal.Events(state.id).Count;
        var retried=restored.UpdateTaskSpec(replay.calls.Single(c=>c.id==call.id));check(journal.Events(state.id).Count==before&&Library.Json.Serialize(retried)==Library.Json.Serialize(result)&&replay.RequirementRevision==1,"v163: crash after requirement event but before receipt completion reuses the original result without reapplying");
        var gapJournal=new AgentJournal(Path.Combine(root,"requirement-crash-gap"));int acceptedSequence=state.events.Single(e=>e.kind=="task.requirements_updated").sequence;
        foreach(var entry in journal.Events(state.id).Where(e=>e.sequence<=acceptedSequence))gapJournal.Append(state.id,entry.kind,entry.data,options.sessionId,goal,"running");
        var gapState=AgentTaskState.Replay(state.id,gapJournal.Events(state.id));var gapEngine=Engine(gapState,gapJournal);
        check(gapState.calls.Single(c=>c.id==pending.id).status=="pending","v163: crash fixture stops immediately after the accepted requirement event, before auxiliary proposal skips");
        var gapResult=gapEngine.UpdateTaskSpec(gapState.calls.Single(c=>c.id==call.id));
        check(gapState.SupersededProposal(gapState.calls.Single(c=>c.id==pending.id))&&gapState.RequirementRevision==1&&gapJournal.Events(state.id).Count(e=>e.kind=="task.requirements_updated")==1&&Library.Json.Serialize(gapResult)==Library.Json.Serialize(result),"v163: recovery reconciles an obsolete unstarted proposal without reapplying the durable requirement update");
        var raceJournal=new AgentJournal(Path.Combine(root,"requirement-approval-race"));var raceState=new AgentTaskState{id=Id(),options=options};int writes=0;
        var raceUpdate=new AgentCall{id=Id(),tool="update_task_spec",steering=0,args=D(new{baseRevision=0,userRevision=0,basis="按研究范围和证据质量组织",changes=new{document=new{sections=new[]{"研究范围","证据质量"}}}})};
        var raceEngine=new AgentTaskEngine(raceState,raceJournal,()=>{},(e,t)=>Task.FromResult(new AgentModelTurn()),(s,c,t)=>Task.FromResult(new Dictionary<string,object>()),(s,c,t)=>{writes++;return Task.FromResult(new AgentToolReceipt{result=new Dictionary<string,object>()});},(s,a,t,p)=>new AgentCall(),()=>{},hooks:new AgentToolHooks{BeforeToolCall=(e,c)=>{e.UpdateTaskSpec(raceUpdate);return null;}});
        raceEngine.Emit("task.created",new{options,protocol="tools"});var racePending=new AgentCall{id=Id(),tool="create_document",status="pending",steering=0};raceEngine.Emit("tool.proposed",new{call=racePending});raceEngine.Emit("tool.proposed",new{call=raceUpdate});raceEngine.IsRunning=true;
        try{raceEngine.Decide(racePending.id,true).GetAwaiter().GetResult();}catch(InvalidOperationException){}finally{raceEngine.IsRunning=false;}
        check(writes==0&&!raceState.events.Any(e=>e.kind=="tool.started"&&AgentTaskState.Get<string>(e.data,"callId")==racePending.id)&&raceState.SupersededProposal(raceState.calls.Single(c=>c.id==racePending.id)),"v163: approval rechecks an intervening requirement update atomically before starting the old write");
        bool Reject(object proposal){try{restored.UpdateTaskSpec(new AgentCall{id=Id(),tool="update_task_spec",steering=replay.steerVersion,args=D(proposal)});return false;}catch(AgentFailure error){return error.kind=="validation";}}
        check(Reject(new{baseRevision=0,userRevision=0,basis="按研究范围和证据质量组织",changes=new{document=new{sections=new[]{"其他"}}}}),"v163: a stale optimistic revision cannot overwrite newer requirements");
        check(Reject(new{baseRevision=1,userRevision=0,basis="文件里说不必交付",changes=new{document=new{required=false}}}),"v163: source text cannot masquerade as an actual current user instruction");
        check(Reject(new{baseRevision=1,userRevision=0,basis="按研究范围和证据质量组织",changes=new{document=new{required=false}}}),"v163: even a real user substring cannot authorize the model to cancel active delivery requirements");
        check(Reject(new{baseRevision=1,userRevision=0,basis="根据资料",changes=new{sourceIds=new[]{Id()}}}),"v163: model-selected source IDs cannot escape the host task scope");
        check(Reject(new{baseRevision=1,userRevision=0,basis="根据资料",changes=new{budget=999999,approvalPolicy="task"}}),"v163: the shared strict schema rejects budget or permission injection");
        var noop=restored.UpdateTaskSpec(new AgentCall{id=Id(),tool="update_task_spec",steering=0,args=D(new{baseRevision=1,userRevision=0,basis="按研究范围和证据质量组织",changes=new{document=new{sections=new[]{"研究范围","证据质量"}}}})});
        check(!Convert.ToBoolean(noop["changed"])&&replay.RequirementRevision==1,"v163: an already-satisfied update adds no event or revision");
        restored.Emit("task.adjusted",new{text="格式改为TXT，其他不变"});var adjusted=AgentContracts.Task(replay);
        check(adjusted.revision==2&&adjusted.userRevision==1&&adjusted.deliverables.Single().format=="TXT"&&adjusted.deliverables.Single().sections.Count==2,"v163: later user format steering preserves typed sections and advances the combined revision");
        check(AgentWorkspaceTools.Hash(File.ReadAllBytes(source.path))==hash&&options.requestLimit==6&&options.approvalPolicy=="each"&&options.allowed.SequenceEqual(new[]{source.id}),"v163: metadata updates preserve source bytes, approvals, request limits and native scope");
        var oldState=new AgentTaskState{id=Id(),options=new AgentTaskOptions{runtime="research",requirementsVersion=0}};bool oldBlocked=false;try{Engine(oldState,journal).UpdateTaskSpec(call);}catch(AgentFailure error){oldBlocked=error.kind=="policy";}check(oldBlocked,"v163: old tasks cannot opt into structured updates during replay");
    }
}

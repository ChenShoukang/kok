using System;
using System.Linq;
using System.Collections.Generic;

namespace kok {
    internal static class AgentRequirementUpdates {
        // Detailed field types have one source of truth in the SDK's Zod schema. The native boundary
        // revalidates them using the pure business worker before emitting an authoritative event.
        internal static AgentToolSpec Tool(){return new AgentToolSpec{
            Name="update_task_spec",Title="更新交付要求",Requires=Need.NormalLibrary,Mode="serial",CapabilityId="agent.update_task_spec",
            Signature="update_task_spec{baseRevision,userRevision,basis,changes}",
            Description="Fill complex USER requirement gaps in the current host specification. Use current revision/userRevision, quote the latest user's exact words as basis, and change only named fields. Supports purpose/audience (content guidance, not certification), document/format/sections, presentation/pages, measured length, chat-only, targeted edit, full reading, source subset and code verification. Cannot change permissions, tools, workspace, budgets or scope, or relax existing host-verifiable obligations; explicit user changes are first applied by the host. This is a model interpretation, not intent or completion proof. Unnamed fields and saved work remain. Simple tasks and no-ops need no update. Update before dependent writes.",
            Properties=AgentRules.Props("baseRevision",new Dictionary<string,object>{{"type","integer"},{"minimum",0}},"userRevision",new Dictionary<string,object>{{"type","integer"},{"minimum",0}},"basis",AgentRules.Str("Exact latest USER quotation",800),"changes",CapabilitySchema.Obj(additional:true)),Required=new[]{"baseRevision","userRevision","basis","changes"},
            Validate=(action,exists)=>{if(Library.Json.Serialize(action.args).Length>12000)throw new FormatException("Requirement update is too large");}
        };}
    }
    public sealed partial class AgentTaskEngine {
        // Call while holding gate. The requirement event can survive a crash before these auxiliary
        // skips; recovery reconciles them without repeating the accepted update or a started write.
        void RetireObsoleteRequirementProposals(){
            if(State.options.requirementsVersion!=1)return;
            foreach(var pending in State.calls.Where(c=>c.status=="pending"&&c.requirementRevision<State.RequirementRevision&&!State.events.Any(e=>e.kind=="tool.started"&&AgentTaskState.Get<string>(e.data,"callId")==c.id)).ToList())Emit("tool.skipped",new{callId=pending.id,reason="superseded"});
        }
        internal Dictionary<string,object> UpdateTaskSpec(AgentCall call){lock(gate){
            var task=State;var options=task.options;
            if(options.requirementsVersion!=1||!AgentService.ResearchRuntime(task))throw new AgentFailure("policy","该任务版本不支持结构化需求变更。");
            var applied=task.events.LastOrDefault(e=>e.kind=="task.requirements_updated"&&AgentTaskState.Get<string>(e.data,"callId")==call.id);
            if(applied!=null){RetireObsoleteRequirementProposals();return AgentTaskState.Get<Dictionary<string,object>>(applied.data,"result");}
            if(call.steering!=task.steerVersion)throw new AgentFailure("superseded","用户要求已更新，请使用当前版本。");
            var validation=AgentBusinessCore.Invoke<Dictionary<string,object>>("requirements_update",new{plan=AgentBusinessCore.Input(options,task.adjustments),revision=task.SpecRevision,proposal=call.args});
            if(!AgentTaskState.Get<bool>(validation,"valid"))throw new AgentFailure("validation",ModuleContext.Text(validation,"error"));
            bool changed=AgentTaskState.Get<bool>(validation,"changed");
            var plan=AgentTaskState.Get<AgentBusinessPlan>(validation,"plan");
            var proposal=AgentTaskState.Get<Dictionary<string,object>>(validation,"proposal");
            var result=new Dictionary<string,object>{{"text",changed?"已按当前用户原话更新交付要求；保留已有成果，继续核对并完成实际交付。":"当前交付要求已包含这些内容，无需重复更新。"},{"revision",task.SpecRevision+(changed?1:0)},{"requirements",plan.contract},{"basis",ModuleContext.Text(proposal,"basis")},{"interpretation","model_proposed"},{"changed",changed}};
            if(changed){
                Emit("task.requirements_updated",new{callId=call.id,proposal=proposal,result=result});
                // A sibling write was prepared against the old requirements; it has not run or been approved.
                RetireObsoleteRequirementProposals();
            }
            return result;
        }}
    }
}

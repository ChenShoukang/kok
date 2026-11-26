using System;
using System.Linq;
using System.Collections.Generic;

namespace kok {
    internal static class AgentRequirementUpdates {
        internal static bool IsTool(string name){return name=="update_task_spec"||name=="propose_task_spec";}
        internal static bool IndividuallyApproved(AgentTaskState task,AgentCall call){return call.tool=="propose_task_spec"&&task.options.requirementsVersion==1&&task.events.Any(e=>e.kind=="tool.started"&&AgentTaskState.Get<string>(e.data,"callId")==call.id&&AgentTaskState.Get<string>(e.data,"approval")=="individual_requirements");}
        internal static AgentToolSpec[] Tools(){var proposal=Tool();proposal.Name="propose_task_spec";proposal.Title="确认变更交付要求";proposal.Write=true;proposal.Signature="propose_task_spec{baseRevision,userRevision,basis,changes}";proposal.Description="Propose a complex USER cancellation/replacement that conflicts with current host acceptance. Same strict typed schema and exact latest-user quotation as update_task_spec. The host shows actual before/after requirements and needs individual user approval before changing anything. Use update_task_spec for safe refinements; do not request approval for no-ops. This cannot change permissions, budgets, tools, workspace or source scope. Saved files remain. Never claim the proposal is already applied; never repeat a declined change without a new user request.";return new[]{Tool(),proposal};}
        internal static Dictionary<string,object> Validate(AgentTaskState task,AgentCall call,bool allowReplacement){
            var result=AgentBusinessCore.Invoke<Dictionary<string,object>>("requirements_update",new{plan=AgentBusinessCore.Input(task.options,task.adjustments),revision=task.SpecRevision,proposal=call.args,allowReplacement});
            if(!AgentTaskState.Get<bool>(result,"valid"))throw new AgentFailure("validation",ModuleContext.Text(result,"error"));
            return result;
        }
        internal static void Prepare(AgentTaskState task,AgentCall call){
            if(call.tool!="propose_task_spec")return;
            var result=Validate(task,call,true);
            if(!AgentTaskState.Get<bool>(result,"changed"))throw new FormatException("The current requirements already match this proposal. Continue directly with the required native deliverable. Do not submit the same requirement change again.");
            if(!AgentTaskState.Get<bool>(result,"requiresApproval"))throw new FormatException("No conflicting acceptance change needs approval. Use update_task_spec only for the user-stated fields still missing from current requirements.");
            call.args=AgentTaskState.Get<Dictionary<string,object>>(result,"proposal");
            if(task.calls.Any(c=>c.tool==call.tool&&c.status=="skipped"&&c.error=="user"&&c.steering==task.steerVersion&&Library.Json.Serialize(AgentTaskState.Get<Dictionary<string,object>>(c.args,"changes"))==Library.Json.Serialize(AgentTaskState.Get<Dictionary<string,object>>(call.args,"changes"))))throw new AgentFailure("policy","该需求变更已被拒绝，请保留当前要求，等待新的用户指令。");
            call.beforeText=Library.Json.Serialize(AgentTaskState.Get<AgentTaskContract>(result,"before"));call.afterText=Library.Json.Serialize(AgentTaskState.Get<AgentBusinessPlan>(result,"plan").contract);
        }
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
        internal Dictionary<string,object> UpdateTaskSpec(AgentCall call){return UpdateTaskSpec(call,false);}
        internal Dictionary<string,object> ConfirmTaskSpec(AgentCall call){
            if(!AgentRequirementUpdates.IndividuallyApproved(State,call)||call.status!="running"||State.options.approvalPolicy=="read_only"||State.options.intent=="discuss")throw new AgentFailure("approval","需求变更需要单独确认。");
            return UpdateTaskSpec(call,true);
        }
        Dictionary<string,object> UpdateTaskSpec(AgentCall call,bool allowReplacement){lock(gate){
            var task=State;var options=task.options;
            if(options.requirementsVersion!=1||!AgentService.ResearchRuntime(task))throw new AgentFailure("policy","该任务版本不支持结构化需求变更。");
            var applied=task.events.LastOrDefault(e=>e.kind=="task.requirements_updated"&&AgentTaskState.Get<string>(e.data,"callId")==call.id);
            if(applied!=null){RetireObsoleteRequirementProposals();return AgentTaskState.Get<Dictionary<string,object>>(applied.data,"result");}
            if(call.steering!=task.steerVersion)throw new AgentFailure("superseded","用户要求已更新，请使用当前版本。");
            var validation=AgentRequirementUpdates.Validate(task,call,allowReplacement);
            bool changed=AgentTaskState.Get<bool>(validation,"changed");
            var plan=AgentTaskState.Get<AgentBusinessPlan>(validation,"plan");
            var proposal=AgentTaskState.Get<Dictionary<string,object>>(validation,"proposal");
            if(allowReplacement&&(call.beforeText!=Library.Json.Serialize(AgentTaskState.Get<AgentTaskContract>(validation,"before"))||call.afterText!=Library.Json.Serialize(plan.contract)))throw new AgentFailure("conflict","要求变更预览已失效，请重新生成后再确认。");
            var result=new Dictionary<string,object>{{"text",changed?"已按当前用户原话更新交付要求；保留已有成果，继续核对并完成实际交付。":"当前交付要求已包含这些内容，无需重复更新。"},{"revision",task.SpecRevision+(changed?1:0)},{"requirements",plan.contract},{"basis",ModuleContext.Text(proposal,"basis")},{"interpretation",allowReplacement?"user_confirmed":"model_proposed"},{"changed",changed}};
            var omitted=AgentTaskState.Get<List<string>>(validation,"omittedFields");
            if(omitted!=null&&omitted.Count>0)result["omittedUnstatedFields"]=omitted;
            if(changed){
                Emit("task.requirements_updated",new{callId=call.id,proposal=proposal,result=result});
                // A sibling write was prepared against the old requirements; it has not run or been approved.
                RetireObsoleteRequirementProposals();
            }
            return result;
        }}
    }
}

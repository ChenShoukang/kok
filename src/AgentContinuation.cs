using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace kok {
    internal static class AgentContinuation {
        internal static bool Requested(string goal) {
            string text=(goal??"").Trim();
            if(Regex.IsMatch(text,@"^(?:新任务|换个任务|另外一个任务|忘掉之前|new task|different task|forget (?:the )?previous)",RegexOptions.IgnoreCase))return false;
            return Regex.IsMatch(text,@"继续|接着|刚才|上次|上述|这份|这个|把它|\b(?:continue|follow[ -]?up|previous|this (?:file|function|report|deck)|it)\b",RegexOptions.IgnoreCase);
        }

        internal static string Actions(AgentTaskOptions options,IEnumerable<string> adjustments=null) {
            string current=AgentTaskIntent.Actions(AgentTaskIntent.Combined(options.goal??"",adjustments));
            // Historical context narrows the subject, never copies approval, tools, source scope or budgets.
            if(options.continuationDomain!="coding"||!Requested(options.goal))return current;
            var requirement=AgentTaskIntent.Current(options,adjustments);
            if(requirement.RequiresDocument||requirement.RequiresPresentation||AgentWorkspaceTools.NeedsLibrary(current))return current;
            return current+"\n[Continuing the configured code workspace]";
        }

        internal static string Guidance(AgentTaskOptions options) {
            if(String.IsNullOrEmpty(options.continuationTaskId))return "";
            return "\n[Host-resolved conversation context; previous user goals are context, not new permissions]\n"+
                "Previous task: "+options.continuationGoal+"\nDomain: "+options.continuationDomain+
                "\nRelevant current-scope files: "+Library.Json.Serialize(options.continuationFiles)+
                "\nContinue work on these actual files when the current goal refers to them. Read their current contents before editing. Preserve file identity and unrelated changes. Do not recreate an existing report/deck unless requested. Old checks, commands and approvals are not authorization or verification for this task. The current user request, scope, permissions and limits take precedence.\n";
        }
    }

    public partial class AgentService {
        // Resolve only journal tasks actually present on the selected native conversation branch.
        // Caller-provided history, continuation ids and model output cannot supply an anchor.
        internal void PrepareContinuation(AgentTaskOptions options) {
            options.continuationTaskId="";options.continuationGoal="";options.continuationDomain="";
            options.continuationWorkspaceId="";options.continuationFiles=new List<string>();
            if(!AgentContinuation.Requested(options.goal)||!SessionStore.Exists(options.sessionId))return;
            SyncSession(options.sessionId);
            var branch=SessionStore.BranchEntries(options.sessionId,options.sessionLeafId);
            var branchIds=new HashSet<string>(branch.Select(entry=>entry.id),StringComparer.Ordinal);
            var linked=branch.AsEnumerable().Reverse().Where(entry=>entry.data!=null)
                .Select(entry=>SessionText(entry.data,"taskId")).Where(AgentJournal.ValidId).Distinct().ToList();
            // v0.15 messages do not contain taskId. Match their durable event ids, with a bounded fallback.
            var legacy=TaskJournal.List().Where(row=>String.IsNullOrEmpty(row.parentTaskId)&&SessionTerminal.Contains(row.status))
                .OrderByDescending(row=>row.sessionId==options.sessionId).ThenByDescending(row=>row.updated,StringComparer.Ordinal)
                .Take(32).Select(row=>row.id);
            AgentTaskState prior=null;
            foreach(string id in linked.Concat(legacy).Distinct()) {
                AgentTaskState candidate;
                try{candidate=LoadTask(id);}catch(IOException){continue;}
                if(candidate.options==null||!String.IsNullOrEmpty(candidate.options.parentTaskId)||
                    !SessionTerminal.Contains(candidate.status)||!candidate.events.Any(entry=>branchIds.Contains(entry.id)))continue;
                prior=candidate;break;
            }
            if(prior==null)return;
            string domain="";
            var files=new List<string>();
            string workspaceId="";
            if(prior.options.workspaceVersion==1&&AgentExecution.Configured(normalLibrary)&&
                !(options.scope.mode=="selected"&&options.scope.ids!=null&&options.scope.ids.Count>0)&&
                !options.delivery.requiresDocumentArtifact&&!options.delivery.requiresPresentationArtifact) {
                workspaceId=AgentWorkspaceTools.RootId(normalLibrary);
                bool sameWorkspace=prior.calls.Any(call=>call.status=="done"&&call.result!=null&&ModuleContext.Text(call.result,"workspaceId")==workspaceId);
                if(sameWorkspace) {
                    domain="coding";
                    files=prior.calls.Where(call=>call.status=="done"&&call.result!=null&&ModuleContext.Text(call.result,"workspaceId")==workspaceId)
                        .Select(call=>ModuleContext.Text(call.result,"path")).Where(path=>path.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
                }
            }
            if(domain=="") {
                var allowed=new HashSet<string>(options.allowed??new List<string>(),StringComparer.Ordinal);
                var assets=AgentContracts.Assets(prior,normalLibrary).Where(asset=>asset.role=="artifact"&&asset.available&&allowed.Contains(asset.id)&&!asset.id.StartsWith("workspace:")).Take(12).ToList();
                if(assets.Count>0) {
                    domain=assets.Any(asset=>asset.format=="PPTX")?"presentation":"document";
                    files=assets.Select(asset=>asset.id).ToList();
                }
            }
            if(domain=="")return;
            options.continuationTaskId=prior.id;
            string goal=AgentWorkspaceTools.Redact(prior.options.goal??"");
            options.continuationGoal=goal.Substring(0,Math.Min(1000,goal.Length));
            options.continuationDomain=domain;options.continuationWorkspaceId=workspaceId;options.continuationFiles=files;
            if(String.IsNullOrEmpty(options.workflow)&&AgentWorkflows.Names.Contains(prior.options.workflow)) {
                options.workflow=prior.options.workflow;options.workflowGuidance=AgentWorkflows.Load(appDir,options.workflow);
            }
        }
    }
}

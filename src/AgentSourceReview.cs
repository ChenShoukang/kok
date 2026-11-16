using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace kok {
    internal static class AgentSourceReview {
        internal static AgentToolSpec Tool(){return new AgentToolSpec{
            Name="review_sources",Title="检查资料阅读状态",Write=false,Requires=Need.NormalLibrary,CapabilityId="agent.review_sources",
            Signature="review_sources{ids?}",Description="Inspect native evidence for up to 24 current-scope sources: parsed, actually processed, complete/partial/unread/stale. A source being parsed is not proof the model processed it; a reading receipt is not semantic citation verification. This tool never adds reading coverage. Follow missing ranges with actual source reads.",
            Properties=AgentRules.Props("ids",AgentRules.Arr("Optional current-scope sources; defaults to selected or already used sources",24,AgentRules.Id)),Required=new string[0],
            Validate=(action,exists)=>{var ids=AgentTaskState.Get<List<string>>(action.args,"ids")??new List<string>();if(ids.Count>24||ids.Any(id=>!AgentJournal.ValidId(id)||!exists(id)))throw new FormatException("unknown or out-of-scope file ID");},
            Read=(library,state,call,token)=>Task.Run(()=>{
                token.ThrowIfCancellationRequested();var ids=AgentTaskState.Get<List<string>>(call.args,"ids");
                if(ids==null||ids.Count==0)ids=state.options.scope.mode=="selected"?(state.options.scope.ids??new List<string>()):state.calls.Where(row=>row.status=="done"&&row.sources!=null).SelectMany(row=>row.sources.Keys).Except(state.createdArtifactIds).Distinct().Take(24).ToList();
                var rows=AgentReading.ReviewSources(library,state,ids.Where(state.InScope));
                return new Dictionary<string,object>{{"text",Library.Json.Serialize(new{sources=rows,note="Parsing, source processing and output citations are separate. This report does not certify factual support or increase coverage."})},{"sourceReview",rows}};
            },token)};}
    }
}

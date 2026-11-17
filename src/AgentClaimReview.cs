using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace kok {
    internal static class AgentClaimReview {
        internal static AgentToolSpec Tool(){
            var claim=CapabilitySchema.Obj(AgentRules.Props("claim",AgentRules.Str("Conclusion to trace",600),"sourceId",AgentRules.Id,"quote",AgentRules.Str("Exact source quotation, not a paraphrase",1200),"sourceVersion",AgentRules.Str("Optional exact digest from a reading coverage row",64),"offset",new Dictionary<string,object>{{"type","integer"},{"minimum",0},{"maximum",380000}},"relationship",new Dictionary<string,object>{{"type","string"},{"enum",new[]{"supports","contradicts","inference","unverified"}}}),new[]{"claim","sourceId","quote","relationship"});
            return new AgentToolSpec{Name="review_claims",Title="核对结论与原文",Write=false,Requires=Need.NormalLibrary,CapabilityId="agent.review_claims",
                Signature="review_claims{claims[],artifact_id?}",Description="Trace up to 12 claims to exact current ORIGINAL INPUT text-source quotes and actual native text reading ranges (not presentation page reads). Returns source digest, LF-normalized UTF-16 offsets, lines and original read-call IDs. Repeated quotations need offset. For a saved report check, provide artifact_id of the managed MD/TXT/DOCX and its verbatim conclusion as claim; sourceId/quote must refer to original input evidence, not the report itself. Current-task artifacts used as sources produce artifact_used_as_source. Exact text plus reading is NOT semantic proof: supports/contradicts/inference are your proposed relationships. No reading coverage is added. Read missing passages first; record conflicts and unknowns honestly.",
                Properties=AgentRules.Props("claims",AgentRules.Arr("Claims and exact quotations",12,claim),"artifact_id",AgentRules.Id),Required=new[]{"claims"},
                Validate=(action,exists)=>{
                    var claims=AgentTaskState.Get<List<Dictionary<string,object>>>(action.args,"claims");
                    if(claims==null||claims.Count<1||claims.Count>12)throw new FormatException("claims requires 1-12 rows");
                    string artifactId=ModuleContext.Text(action.args,"artifact_id");if(action.args.ContainsKey("artifact_id")&&(!AgentJournal.ValidId(artifactId)||!exists(artifactId)))throw new FormatException("unknown or out-of-scope artifact ID");
                    foreach(var row in claims){string id=ModuleContext.Text(row,"sourceId"),text=ModuleContext.Text(row,"claim"),quote=ModuleContext.Text(row,"quote"),version=ModuleContext.Text(row,"sourceVersion");
                        if(!AgentJournal.ValidId(id)||!exists(id))throw new FormatException("unknown or out-of-scope source ID");
                        if(String.IsNullOrWhiteSpace(text)||text.Length>600||String.IsNullOrWhiteSpace(quote)||quote.Length>1200||version.Length>64||!new[]{"supports","contradicts","inference","unverified"}.Contains(ModuleContext.Text(row,"relationship")))throw new FormatException("Invalid claim or quotation");
                        AgentDocumentTools.Number(row,"offset",0,0,380000);
                    }
                },
                Read=(library,state,call,token)=>Task.Run(()=>{
                    token.ThrowIfCancellationRequested();var claims=AgentTaskState.Get<List<Dictionary<string,object>>>(call.args,"claims");var ids=claims.Select(row=>ModuleContext.Text(row,"sourceId")).Distinct().ToArray();
                    var sources=ids.Select(id=>AgentReading.ClaimSource(library,state,id)).ToArray();
                    string artifactId=ModuleContext.Text(call.args,"artifact_id");object artifact=null;
                    if(!String.IsNullOrEmpty(artifactId)){var saved=library.DocumentSource(artifactId);artifact=new{id=artifactId,version=saved.signature,text=saved.text,available=!saved.recovery};}
                    var report=AgentBusinessCore.Invoke<Dictionary<string,object>>("review_claims",new{claims,sources,artifact});
                    return new Dictionary<string,object>{{"text",Library.Json.Serialize(report)},{"claimReview",report},{"_sourceStamps",library.AgentSourceStamps(ids.Concat(String.IsNullOrEmpty(artifactId)?new string[0]:new[]{artifactId}))}};
                },token)};
        }
    }
}

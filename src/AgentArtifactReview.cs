using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace kok {
    internal sealed class AgentArtifactFinding {
        public string code="",severity="warning",message="",shapeId="";
        public int page,line;
        public Dictionary<string,object> source;
    }
    internal sealed class AgentArtifactReport {
        public int version=1,errors,warnings,pages,characters,words;
        public string id="",kind="",fileVersion="",method="",status="",scope="";
        public bool rendered,visualReviewed,truncated;
        public List<string> headings=new List<string>();
        public List<Dictionary<string,object>> sectionChecks=new List<Dictionary<string,object>>();
        public List<AgentArtifactFinding> findings=new List<AgentArtifactFinding>();
        public Dictionary<string,int> measurements;
        public Dictionary<string,object> deliveryCheck;
        public Dictionary<string,object> contentRequirements;
    }

    internal static class AgentArtifactReview {
        static object[] ReadSources(Library library,AgentTaskState task,string artifactId){
            if(task==null||task.options.artifactReviewVersion<3)return Array.Empty<object>();
            var ids=task.calls.Where(call=>call.status=="done"&&(call.tool=="read_items"||call.tool=="read_document"))
                .SelectMany(AgentReading.CoverageRows).Where(row=>row.domain!=AgentReading.DomainPresentation)
                .Select(row=>row.id).Where(id=>id!=artifactId&&task.InScope(id)&&!task.createdArtifactIds.Contains(id))
                .Distinct().Take(9).ToArray();
            // Do not form a factual objection from an arbitrarily selected subset of a large source set.
            return ids.Length<=8?ids.Select(id=>AgentReading.ClaimSource(library,task,id)).ToArray():Array.Empty<object>();
        }
        static string[] UserInstructions(AgentTaskState task){
            return task==null?Array.Empty<string>():new[]{task.options.goal}.Concat(task.adjustments??new List<string>()).ToArray();
        }
        internal static AgentArtifactReport Document(Library library,string id,IEnumerable<string> sections=null,AgentDeliveryRequirement requirement=null,AgentTaskState task=null) {
            var item=library.Find(id);if(item==null||!item.ownedDocument||!new[]{"MD","TXT","DOCX"}.Contains(item.ext))throw new InvalidDataException("检查仅支持资料库内可编辑的 MD、TXT、DOCX 文档。");
            var saved=library.DocumentSource(id);
            return AgentBusinessCore.Invoke<AgentArtifactReport>("review_artifact",new{id=id,kind="document",format=item.ext,fileVersion=saved.signature,text=saved.text,recovery=saved.recovery,sections=(sections??Enumerable.Empty<string>()).ToArray(),requirement=requirement,sources=ReadSources(library,task,id),userInstructions=UserInstructions(task),
                measurements=new{hanzi=AgentDeliveryRules.MeasuredLength(saved.text,AgentMeasureUnit.Hanzi),characters=AgentDeliveryRules.MeasuredLength(saved.text,AgentMeasureUnit.Characters),words=AgentDeliveryRules.MeasuredLength(saved.text,AgentMeasureUnit.Words)}});
        }
        internal static AgentArtifactReport Presentation(Library library,string id,AgentTaskState task=null) {
            var item=library.Find(id);if(item==null||item.ext!="PPTX")throw new InvalidDataException("请使用 PPTX 格式进行检查。");
            var saved=PresentationFiles.Read(item.path,false);
            return AgentBusinessCore.Invoke<AgentArtifactReport>("review_artifact",new{id=id,kind="presentation",fileVersion=library.PresentationCoverageDigest(id),deck=saved,sources=ReadSources(library,task,id),userInstructions=UserInstructions(task)});
        }

        internal static AgentToolSpec DocumentTool(){return Tool("inspect_document","检查文档成果","Inspect the actual saved managed MD/TXT/DOCX: version, measured Hanzi/non-whitespace characters/English words and required sections with nonempty bodies. For current task deliverables, locates explicit attendance claims using counts whose current read sources say only headcount or an estimate. Findings carry the saved line and original evidence. Keep the source qualification or read affirmative evidence before repairing. This check does not certify all facts or DOCX layout and adds no source-reading coverage.",true);}
        internal static AgentToolSpec PresentationTool(){return Tool("inspect_presentation","检查演示文稿","Inspect the actual PPTX version and each parsed slide for off-canvas objects, text intersections, small text and estimated overflow. For current task deliverables, also checks explicit actual attendance claims against counts in current read sources. Findings carry page and shape IDs for targeted edits. This is not a substitute for Office rendering or full content review.",false);}
        static AgentToolSpec Tool(string name,string title,string description,bool document) {
            return new AgentToolSpec{Name=name,Title=title,Write=false,Requires=Need.NormalLibrary,CapabilityId=document?"documents.documentSource":"presentations.readPresentation",
                Signature=name+"{id"+(document?",required_sections?":"")+"}",Description=description,
                Properties=document?AgentRules.Props("id",AgentRules.Id,"required_sections",AgentRules.Arr("Literal section titles to check; at most 12",12,AgentRules.Str("Section title",60))):AgentRules.Props("id",AgentRules.Id),Required=new[]{"id"},
                Validate=(action,exists)=>{if(!AgentJournal.ValidId(ModuleContext.Text(action.args,"id"))||!exists(ModuleContext.Text(action.args,"id")))throw new FormatException("unknown or out-of-scope file ID");if(document){var sections=AgentTaskState.Get<List<string>>(action.args,"required_sections")??new List<string>();if(sections.Count>12||sections.Any(section=>String.IsNullOrWhiteSpace(section)||section.Length>60||section.Any(Char.IsControl)))throw new FormatException("Invalid section checks");}},
                Read=(library,state,call,token)=>Task.Run(()=>{
                    token.ThrowIfCancellationRequested();
                    string id=ModuleContext.Text(call.args,"id");
                    bool deliverable=state.createdArtifactIds.Contains(id)||state.calls.Any(c=>c.status=="done"&&AgentTaskState.EditTargetId(c)==id);
                    var requirement=state.options.requirementsVersion==1&&deliverable?AgentDeliveryRules.Derive(state.options,state.adjustments):null;
                    var sections=(AgentTaskState.Get<List<string>>(call.args,"required_sections")??new List<string>()).Concat(requirement==null?Enumerable.Empty<string>():requirement.requiredSections).Distinct();
                    var report=document?Document(library,id,sections,requirement,deliverable?state:null):Presentation(library,id,deliverable?state:null);
                    return new Dictionary<string,object>{{"text",Library.Json.Serialize(report)},{"id",report.id},{"artifactReview",report}};
                },token)};
        }
    }

    public partial class AgentService {
        AgentToolReceipt ReviewWrittenArtifact(AgentTaskState task,AgentCall call,AgentToolReceipt receipt) {
            if(task.options.artifactReviewVersion<1||receipt==null||receipt.result==null)return receipt;
            string id=ModuleContext.Text(receipt.result,"id");if(!AgentJournal.ValidId(id))return receipt;
            if(!new[]{"create_document","replace_document_text","create_presentation","copy_presentation","edit_presentation"}.Contains(call.tool))return receipt;
            try{var item=normalLibrary.Find(id);var requirement=AgentDeliveryRules.Derive(task.options,task.adjustments);if(item!=null)receipt.result["artifactReview"]=item.ext=="PPTX"?AgentArtifactReview.Presentation(normalLibrary,id,task):AgentArtifactReview.Document(normalLibrary,id,requirement.requiredSections,requirement,task);}
            catch(Exception error){receipt.result["artifactReview"]=new Dictionary<string,object>{{"version",1},{"id",id},{"status","unavailable"},{"error",AgentWorkspaceTools.Redact(error.Message)},{"rendered",false},{"visualReviewed",false}};}
            return receipt;
        }
    }
}
